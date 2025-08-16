using System;
using Project.Scripts.DOTS.Other;
using Project.Scripts.DOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static Project.Scripts.DOTS.Other.DOTS_Utils;

[BurstCompile]
[UpdateAfter(typeof(ChunkDespawnSystem))]
public partial struct MeshGenerationSystem : ISystem
{
    #region vars
    int maxChunks; // this will be set based on player render distance

    private const int INITIAL_SIZE = 1024; // initial size for the buffers
    private NativeArray<Vertex> vertices;
    private NativeArray<int> triangles;
    private NativeArray<float2> uvs;
    public BufferLookup<DOTS_Block> BlockLookup;

    NativeQueue<MeshSlice> meshSliceQueue;
    NativeQueue<Entity> meshEntityQueue;

    AtomicCounter vertexCounter;
    AtomicCounter triangleCounter;
    AtomicCounter uvCounter;
    
    private NativeQueue<Entity> chunksToGenerateQueue;
    public static int maxChunksPerFrame = 2; // chunks processed per frame

    #endregion
    
    public struct MeshSlice
    {
        public int VerticesStart, VerticesLength;
        public int TrianglesStart, TrianglesLength;
        public int UVsStart, UVsLength;
    }

    
    public void OnCreate(ref SystemState state)
    {
        initializeMeshVars();
        BlockLookup = state.GetBufferLookup<DOTS_Block>(true);
        
        
        vertexCounter = new AtomicCounter(Allocator.Persistent);
        triangleCounter = new AtomicCounter(Allocator.Persistent);
        uvCounter = new AtomicCounter(Allocator.Persistent); 
        
        meshSliceQueue = new NativeQueue<MeshSlice>(Allocator.Persistent);
        meshEntityQueue = new NativeQueue<Entity>(Allocator.Persistent);
        
        chunksToGenerateQueue = new NativeQueue<Entity>(Allocator.Persistent);

    }

    public void OnDestroy(ref SystemState state)
    {
        // Dispose of the buffers
        if (vertices.IsCreated) vertices.Dispose();
        if (triangles.IsCreated) triangles.Dispose();
        if (uvs.IsCreated) uvs.Dispose();

        vertexCounter.Dispose();
        triangleCounter.Dispose();
        uvCounter.Dispose();
        
        meshSliceQueue.Dispose();
        meshEntityQueue.Dispose();

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        //return if player doesn't exist and get render distance
        if (!GetPlayerRenderDistance(ref state))
            return;

        if (!ChunkMeshesPending(ref state)) return;

        foreach (var (chunk,chunkEntity) in SystemAPI.Query<RefRO<DOTS_Chunk>>().WithAll<ChunkMeshPending>().WithEntityAccess())
        {
            chunksToGenerateQueue.Enqueue(chunkEntity);
        }

        BlockLookup.Update(ref state);

        #region Vars Init and Reset

        //check if disposed
        if (vertices.IsCreated == false || triangles.IsCreated == false || uvs.IsCreated == false)
        {
            // Reinitialize the buffers if they were disposed
            initializeMeshVars();
        }

        var trisRequiredSize = CalculateMaxRequiredTriangles(maxChunks);
        var vertsRequiredSize = CalculateMaxRequiredVertices(maxChunks);

        if (vertices.Length < vertsRequiredSize)
        {
            vertices = new NativeArray<Vertex>(vertsRequiredSize, Allocator.Persistent);
            uvs = new NativeArray<float2>(vertsRequiredSize, Allocator.Persistent);
        }

        if (triangles.Length < trisRequiredSize)
        {
            triangles = new NativeArray<int>(trisRequiredSize, Allocator.Persistent);
        }

        
        // reset counters
        vertexCounter.Reset();
        triangleCounter.Reset();
        uvCounter.Reset();
        
        
        meshSliceQueue.Clear();
        meshEntityQueue.Clear();

        var ecbSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        var ecb = ecbSystem.CreateCommandBuffer();
        var ecbParallel = ecb.AsParallelWriter();

        #endregion



        var meshGenerationJob = new MeshGenerationJob
        {
            BlockLookup = BlockLookup,

            Vertices = vertices,
            Triangles = triangles,
            UVs = uvs,

            VertexCounter = vertexCounter,
            TriangleCounter = triangleCounter,
            UVCounter = uvCounter,

            MeshSliceQueue = meshSliceQueue.AsParallelWriter(),
            MeshEntityQueue = meshEntityQueue.AsParallelWriter(),

            ecb = ecbParallel,
        };

        state.Dependency = meshGenerationJob.ScheduleParallel(state.Dependency);
        state.Dependency.Complete();


        while (meshSliceQueue.TryDequeue(out var slice) && meshEntityQueue.TryDequeue(out var entity))
        {
            MeshUploadQueue.Queue.Enqueue(new MeshDataRequest
            {
                MeshEntity = entity,
                Vertices = vertices.GetSubArray(slice.VerticesStart, slice.VerticesLength),
                Triangles = triangles.GetSubArray(slice.TrianglesStart, slice.TrianglesLength),
                UVs = uvs.GetSubArray(slice.UVsStart, slice.UVsLength)
            });
        }

    }

    private void initializeMeshVars()
    {
        vertices = new NativeArray<Vertex>(INITIAL_SIZE, Allocator.Persistent);
        triangles = new NativeArray<int>(INITIAL_SIZE, Allocator.Persistent);
        uvs = new NativeArray<float2>(INITIAL_SIZE, Allocator.Persistent);
    }

    private bool ChunkMeshesPending(ref SystemState state)
    {
        var desiredChunks = SystemAPI.QueryBuilder()
            .WithAll<DOTS_Chunk, DOTS_ChunkRenderData, ChunkMeshPending>()
            .Build();
        if (desiredChunks.CalculateEntityCount() == 0)
        {
            // Debug.LogWarning("No chunks need MESH generation.");
            return false;
        }
        else
        {
            // Debug.Log($"Found {desiredChunks.CalculateEntityCount()} chunks that need MESH generation.");
        }

        return true;
    }

    // Calculate based on worst-case scenario (all blocks visible)
    public static int CalculateMaxRequiredVertices(int chunkCount)
    {
        const int BLOCKS_PER_CHUNK = CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE;
        const int VERTICES_PER_BLOCK = 24; // 6 faces * 4 vertices
        return chunkCount * BLOCKS_PER_CHUNK * VERTICES_PER_BLOCK;
    }

    public static int CalculateMaxRequiredTriangles(int chunkCount)
    {
        const int BLOCKS_PER_CHUNK = CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE;
        const int TRIANGLES_PER_BLOCK = 12; // 6 faces * 2 triangles
        return chunkCount * BLOCKS_PER_CHUNK * TRIANGLES_PER_BLOCK;
    }


    private bool GetPlayerRenderDistance(ref SystemState state)
    {
        // Get the player's render distance from PlayerSettings
        foreach (var playerSettings in SystemAPI.Query<RefRO<PlayerSettings>>().WithAll<PlayerTag>())
        {
            int renderDistance = playerSettings.ValueRO.renderDistance;
            int chunksToRender = renderDistance * 2 + 1;
            maxChunks = chunksToRender * chunksToRender * chunksToRender;
            return true;
        }

        maxChunks = 0; // or any other default value
        // player doesn't exist
        return false;
    }


    [BurstCompile]
    private partial struct MeshGenerationJob : IJobEntity
    {
        [ReadOnly] public BufferLookup<DOTS_Block> BlockLookup;

        [NativeDisableParallelForRestriction] public NativeArray<Vertex> Vertices;
        [NativeDisableParallelForRestriction] public NativeArray<int> Triangles;
        [NativeDisableParallelForRestriction] public NativeArray<float2> UVs;

        public AtomicCounter VertexCounter;
        public AtomicCounter TriangleCounter;
        public AtomicCounter UVCounter;

        public NativeQueue<MeshSlice>.ParallelWriter MeshSliceQueue;
        public NativeQueue<Entity>.ParallelWriter MeshEntityQueue;

        public EntityCommandBuffer.ParallelWriter ecb;

        void Execute([EntityIndexInQuery] int sortKey, in Entity entity, in DOTS_Chunk chunk,
            in DOTS_ChunkRenderData renderData, in ChunkMeshPending meshPending)
        {
            DynamicBuffer<DOTS_Block> blocks = BlockLookup[entity];

            if (blocks.IsEmpty)
                throw new InvalidOperationException(
                    $"No blocks found for entity {entity}. Ensure the chunk has been initialized with blocks.");
         

            // Local temporary storage
            var localVertices = new NativeList<Vertex>(Allocator.Temp);
            var localTriangles = new NativeList<int>(Allocator.Temp);

            //generate mesh data by filling local lists
            GenerateChunkMesh(blocks, ref localVertices, ref localTriangles);

            // Reserve space atomically
            int vertStart = VertexCounter.Add(localVertices.Length);
            int triStart = TriangleCounter.Add(localTriangles.Length);
            int uvStart = UVCounter.Add(localVertices.Length);

            localVertices.AsArray().CopyTo(Vertices.GetSubArray(vertStart, localVertices.Length));
            localTriangles.AsArray().CopyTo(Triangles.GetSubArray(triStart, localTriangles.Length));


            for (int i = 0; i < localVertices.Length; i++)
                UVs[uvStart + i] = localVertices[i].uv;


            var meshSlice = new MeshSlice
            {
                VerticesStart = vertStart,
                VerticesLength = localVertices.Length,
                TrianglesStart = triStart,
                TrianglesLength = localTriangles.Length,
                UVsStart = uvStart,
                UVsLength = localVertices.Length
            };


            MeshSliceQueue.Enqueue(meshSlice);
            MeshEntityQueue.Enqueue(renderData.MeshEntity);


            ecb.RemoveComponent<ChunkMeshPending>(sortKey, entity);

            localVertices.Dispose();
            localTriangles.Dispose();
        }

        private void GenerateChunkMesh(DynamicBuffer<DOTS_Block> blocks, ref NativeList<Vertex> vertices,
            ref NativeList<int> triangles)
        {
            for (int x = 0; x < CHUNK_SIZE; x++)
            for (int y = 0; y < CHUNK_SIZE; y++)
            for (int z = 0; z < CHUNK_SIZE; z++)
            {
                int index = ToIndex(x, y, z);
                var block = blocks[index];

                if (block.Value != BlockType.Air)
                {
                    AddVisibleFacesToBlock(x, y, z, block.Value, blocks, vertices, triangles);
                }
            }
        }


        public void AddVisibleFacesToBlock(int x, int y, int z, BlockType blockType, DynamicBuffer<DOTS_Block> blocks,
            NativeList<Vertex> vertices, NativeList<int> triangles)
        {
            foreach (var face in FaceData.AllFaces)
            {
                //todo backface culling MIGHT not be needed ~ just the fact that the triangles are not faced towards the camera is enough to not render them.
                // theoretically we could save some small performance by not adding triangles that are not facing the camera in the first place.


                //todo Only update the faces at chunk borders when a neighboring chunk is loaded.

                int3 neighborPos = new int3(x, y, z) + face.direction;
                //if neighborPos is out of bounds, 
                bool neighborOutOfBounds = neighborPos.x < 0 || neighborPos.x >= CHUNK_SIZE ||
                                           neighborPos.y < 0 || neighborPos.y >= CHUNK_SIZE ||
                                           neighborPos.z < 0 || neighborPos.z >= CHUNK_SIZE;
                if (!neighborOutOfBounds)
                {
                    // if neighbor is solid, don't add those faces
                    if (blocks[ToIndex(neighborPos.x, neighborPos.y, neighborPos.z)].Value != BlockType.Air)
                    {
                        continue;
                    }
                }

                // If neighbor is out of bounds or not solid, add this face
                // Add 4 vertices for the face
                int startIndex = vertices.Length;

                for (int i = 0; i < 4; i++)
                {
                    float3 worldPos = new float3(x, y, z) + face.cornerOffsets[i];
                    float2 uv = GetBlockUV(blockType, i);
                    vertices.Add(new Vertex
                    {
                        position = worldPos,
                        normal = face.normal,
                        uv = uv
                    });
                }

                # region explanation

                // basically:
                // Correct winding → triangle shows up from the front (visible).
                // Wrong winding → triangle invisible from the front (backface culling).


                // v2------v3
                // |       |
                // |       |
                // v0------v1
                // Triangle order (0, 2, 1), (1, 2, 3). like the image. this order because it's like ABCD
                //This preserves clockwise winding order (which tells the GPU which side is the front). If you reverse it, the face might become invisible due to backface culling.

                // so yeah we just split it up by threes and hope that the triangles were added  correctly. 

                #endregion

                triangles.Add(startIndex + 0);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 1);

                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 3);
            }
        }
    }
}
