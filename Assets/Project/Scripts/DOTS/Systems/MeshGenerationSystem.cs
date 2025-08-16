using System;
using System.Linq;
using System.Threading;
using Project.Scripts.DOTS.Other;
using Project.Scripts.DOTS.Systems;
using Project.Scripts.UTILITY;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.PlayerLoop;
using static Project.Scripts.DOTS.Other.DOTS_Utils;

// [BurstCompile]

[BurstCompile]
[UpdateAfter(typeof(ChunkDespawnSystem))]
public partial struct MeshGenerationSystem : ISystem
{
    // private const int MAX_TOTAL_VERTICES = 1000000; // adjust based on max expected mesh size
    // private const int MAX_TOTAL_TRIANGLES = 2000000;
    int maxChunks; // this will be set based on player render distance

    private const int INITIAL_SIZE = 1024; // initial size for the buffers
    private NativeArray<Vertex> vertices;
    private NativeArray<int> triangles;
    private NativeArray<float2> uvs;
    public BufferLookup<DOTS_Block> BlockLookup;


    public NativeQueue<Entity> MeshQueue; // or (chunkEntity, renderData) if needed
    public static int maxChunksPerFrame = 2; // chunks processed per frame

    NativeQueue<MeshSlice> meshSliceQueue;
    NativeQueue<Entity> meshEntityQueue;

    AtomicCounter vertexCounter;
    AtomicCounter triangleCounter;
    AtomicCounter uvCounter;

    public struct MeshSlice
    {
        public int VerticesStart, VerticesLength;
        public int TrianglesStart, TrianglesLength;
        public int UVsStart, UVsLength;
        public bool InUse;
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
        // Initialize the mesh upload queue
        
        
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

        // Allocate big shared buffers (TempJob because disposed end of frame)
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

        // ecb.Playback(state.EntityManager);
        // Enqueue mesh upload requests on main thread

        // non queue variant
        // SendMeshRequest(meshSlices, meshEntities);


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


    private void SendMeshRequest(NativeList<MeshSlice> meshSlices, NativeList<Entity> meshEntities)
    {
        for (int i = 0; i < meshSlices.Length; i++)
        {
            var slice = meshSlices[i];
            var entity = meshEntities[i];

            var vertsList = new NativeList<Vertex>(slice.VerticesLength, Allocator.Persistent);
            vertsList.AddRange(vertices.GetSubArray(slice.VerticesStart, slice.VerticesLength));

            var trisList = new NativeList<int>(slice.TrianglesLength, Allocator.Persistent);
            trisList.AddRange(triangles.GetSubArray(slice.TrianglesStart, slice.TrianglesLength));

            var uvsList = new NativeList<float2>(slice.UVsLength, Allocator.Persistent);
            uvsList.AddRange(uvs.GetSubArray(slice.UVsStart, slice.UVsLength));

            MeshUploadQueue.Queue.Enqueue(new MeshDataRequest
            {
                MeshEntity = entity,
                Vertices = vertsList,
                Triangles = trisList,
                UVs = uvsList
            });
        }
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

            // Record slice and entity
            // MeshSlices.AddNoResize(meshSlice);
            // MeshEntities.AddNoResize(renderData.MeshEntity);

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

public struct MeshSlice
{
    public int VerticesStart;
    public int VerticesLength;
    public int TrianglesStart;
    public int TrianglesLength;
    public int UVsStart;
    public int UVsLength;
}

public static class MeshWriter
{
    // vertexBuffer = shared NativeList = has all chunks’ vertices in one big continuous array.
    // vertices = chunk-specific NativeArray = just the vertices generated for one chunk

    // Adds vertices and records their start index and count in the slice
    public static void AddVertices(NativeList<Vertex> vertexBuffer, ref MeshSlice slice, NativeArray<Vertex> vertices)
    {
        slice.VerticesStart = vertexBuffer.Length;
        slice.VerticesLength = vertices.Length;
        vertexBuffer.AddRange(vertices);
    }

    // Adds triangles and records their start index and count in the slice
    public static void AddTriangles(NativeList<int> triangleBuffer, ref MeshSlice slice, NativeArray<int> triangles)
    {
        slice.TrianglesStart = triangleBuffer.Length;
        slice.TrianglesLength = triangles.Length;
        triangleBuffer.AddRange(triangles);
    }

    // Adds UVs and records their start index and count in the slice
    public static void AddUVs(NativeList<float2> uvBuffer, ref MeshSlice slice, NativeArray<float2> uvs)
    {
        slice.UVsStart = uvBuffer.Length;
        slice.UVsLength = uvs.Length;
        uvBuffer.AddRange(uvs);
    }

    // Convenience method to add all mesh data in one call
    public static void AddMeshData(
        NativeList<Vertex> vertexBuffer,
        NativeList<int> triangleBuffer,
        NativeList<float2> uvBuffer,
        ref MeshSlice slice,
        NativeArray<Vertex> vertices,
        NativeArray<int> triangles,
        NativeArray<float2> uvs)
    {
        AddVertices(vertexBuffer, ref slice, vertices);
        AddTriangles(triangleBuffer, ref slice, triangles);
        AddUVs(uvBuffer, ref slice, uvs);
    }
}


[BurstCompile]
public struct AtomicCounter
{
    private NativeArray<int> counter;

    public AtomicCounter(Allocator allocator)
    {
        counter = new NativeArray<int>(1, allocator);
        counter[0] = 0;
    }

    public void Dispose()
    {
        if (counter.IsCreated) counter.Dispose();
    }

    public int Add(int value)
    {
        // Use Interlocked.Add for atomic addition
        unsafe
        {
            int* ptr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(counter);
            return Interlocked.Add(ref *ptr, value) - value;
        }
    }

    public int Value
    {
        get => counter[0];
        set => counter[0] = value;
    }
    
    
    public void Reset()
    {
        if (counter.IsCreated)
        {
            counter[0] = 0;
        }
    }
}