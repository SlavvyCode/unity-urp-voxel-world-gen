using System;
using Project.Scripts.DOTS.Other;
using Project.Scripts.DOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static Project.Scripts.DOTS.Other.DOTS_Utils;

[BurstCompile]
[UpdateAfter(typeof(ChunkDespawnSystem))]
public partial struct MeshGenerationSystem : ISystem
{
    #region vars
    int maxChunks; // this will be set based on player render distance

    private const int INITIAL_SIZE = 1024; // initial size for the buffers
    private static NativeArray<Vertex> vertices;
    private static NativeArray<int> triangles;
    private static NativeArray<float2> uvs;
    public BufferLookup<DOTS_Block> BlockLookup;

    public NativeQueue<MeshSlice> meshSliceQueue;
    public NativeQueue<Entity> meshEntityQueue;

    AtomicCounter vertexCounter;
    AtomicCounter triangleCounter;
    AtomicCounter uvCounter;
    
    private NativeHashSet<Entity> chunksToGenerateHashSet;
    public static int maxChunksPerFrame = 2; // chunks processed per frame

    public static JobHandle LastMeshJobHandle;
    
    
    //exists so that i don't create it every frame
    private EntityQuery desiredChunks; // query for chunks that need mesh generation
    #endregion
    
    public struct MeshSlice
    {
        public Entity MeshEntity; // the entity that this mesh slice belongs to
        public int VerticesStart, VerticesLength;
        public int TrianglesStart, TrianglesLength;
        public int UVsStart, UVsLength;
    }
    public struct MeshBuffers : IComponentData
    {
        public NativeArray<Vertex> vertices;
        public NativeArray<int> triangles;
        public NativeArray<float2> uvs;
        public NativeQueue<MeshSlice> meshSliceQueue;
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
        
        chunksToGenerateHashSet = new NativeHashSet<Entity>(maxChunks, Allocator.Persistent);

        
        
        desiredChunks = SystemAPI.QueryBuilder()
            .WithAll<DOTS_Chunk, DOTS_ChunkRenderData, ChunkMeshPending>()
            .Build();
        
        
        state.EntityManager.CreateSingleton(new MeshBuffers {
            meshSliceQueue = meshSliceQueue,
            vertices = new NativeArray<Vertex>(INITIAL_SIZE, Allocator.Persistent),
            triangles = new NativeList<int>(Allocator.Persistent),
            uvs = new NativeList<float2>(Allocator.Persistent)
        });

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
        if (!GetPlayerRenderDistance(ref state)) return;
        if (!ChunkMeshesPending(ref state)) return;
        
        chunksToGenerateHashSet.Clear(); // clear the queue at the start of the frame to make sure deleted chunks don't stay in the queue
        // Collect entities needing meshes.
        foreach (var (chunk,chunkEntity) 
                 in SystemAPI.Query<RefRO<DOTS_Chunk>>()
                     .WithAll<ChunkMeshPending>()
                     .WithEntityAccess())
            if (!chunksToGenerateHashSet.Contains(chunkEntity))
                chunksToGenerateHashSet.Add(chunkEntity);

        #region Vars Init and Reset
        BlockLookup.Update(ref state);


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
            // doing `xArray = new NativeArray` can cause a memory leak if you didn't dispose of the one that was there prveiously
            if (vertices.IsCreated) vertices.Dispose();
            if (uvs.IsCreated) uvs.Dispose();
            vertices = new NativeArray<Vertex>(vertsRequiredSize, Allocator.Persistent);
            uvs = new NativeArray<float2>(vertsRequiredSize, Allocator.Persistent);
        }

        if (triangles.Length < trisRequiredSize)
        {
            // doing `xArray = new NativeArray` can cause a memory leak if you didn't dispose of the one that was there prveiously
            if (triangles.IsCreated) triangles.Dispose();
            triangles = new NativeArray<int>(trisRequiredSize, Allocator.Persistent);
        }

        
        //give the messenger entity the buffers
        var messengerEntity = SystemAPI.GetSingletonEntity<MeshBuffers>();
        var buffers = SystemAPI.GetSingleton<MeshBuffers>();
        
        // reset counters
        vertexCounter.Reset();
        triangleCounter.Reset();
        uvCounter.Reset();
        
        // todo meshSliceQueue.Clear() and meshEntityQueue.Clear() may discard slices if job hasn’t finished; clear only after job completion.
        // or use temporary lists inside jobs???
        
        // force all jobs that touch meshSliceQueue/meshEntityQueue to finish
        //prevents queueues from being cleared while jobs holding them are still running
        state.Dependency.Complete();  
        meshSliceQueue.Clear();
        meshEntityQueue.Clear();

        var ecbSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        var ecb = ecbSystem.CreateCommandBuffer();
        var ecbParallel = ecb.AsParallelWriter();

        #endregion
        
        int chunksThisFrame = math.min(maxChunksPerFrame, chunksToGenerateHashSet.Count);
        if (chunksThisFrame > 0)
        {
            MeshJobNTimesPerFrame(ref state, chunksThisFrame, ecbParallel);
        }


// Only replace fields that can change
        buffers.vertices = vertices;
        buffers.triangles = triangles;
        buffers.uvs = uvs;

        
// queues stay untouched because they were already initialized
        state.EntityManager.SetComponentData(messengerEntity, buffers);
    }



    private void MeshJobNTimesPerFrame(ref SystemState state, int chunksThisFrame, EntityCommandBuffer.ParallelWriter ecbParallel)
    {
        var batchSliceQueue = new NativeQueue<MeshSlice>(Allocator.TempJob);
        var batchEntityQueue = new NativeQueue<Entity>(Allocator.TempJob);
        var jobData = new NativeArray<MeshGenerationJob.MeshGenJobVars>(chunksThisFrame, Allocator.TempJob);

        for (int i = 0; i < chunksThisFrame; i++)
        {
            // Removes and outputs the element at the front of this queue
            // if (chunksToGenerateHashSet.TryDequeue(out var entity))
            if (!chunksToGenerateHashSet.IsEmpty)
            {
                var enumerator = chunksToGenerateHashSet.GetEnumerator();
                enumerator.MoveNext();  // move to first element
                var entity = enumerator.Current;
                chunksToGenerateHashSet.Remove(entity);
                
                jobData[i] = new MeshGenerationJob.MeshGenJobVars()
                {
                    ChunkEntity = entity,
                    ChunkData = state.EntityManager.GetComponentData<DOTS_Chunk>(entity),
                    RenderData = state.EntityManager.GetComponentData<DOTS_ChunkRenderData>(entity)
                };
            }
        }

        var meshJob = new MeshGenerationJob
        {
            BlockLookup = BlockLookup,

            Vertices = vertices,
            Triangles = triangles,
            UVs = uvs,

            VertexCounter = vertexCounter,
            TriangleCounter = triangleCounter,
            UVCounter = uvCounter,

            BatchSliceQueueParallel = batchSliceQueue.AsParallelWriter(),

            ecb = ecbParallel,
            meshGenJobVarsArray = jobData
                
        };
        var meshJobHandle = meshJob.ScheduleParallel(chunksThisFrame, 1, state.Dependency);
        LastMeshJobHandle = meshJobHandle; // store the job handle for later use, e.g. in MeshUploadSystem
        state.Dependency = meshJobHandle; // make future work depend on it        // handle.Complete();
        // Continuation job merges batch queues into global queues after this batch completes
        
        // meshJobHandle.Complete();
        var mergeJob = new MergeQueueJob
        {
            BatchSliceQueue = batchSliceQueue,
            GlobalSliceQueue = meshSliceQueue.AsParallelWriter(),
        };        
        //todo multithreaded merge job
        var mergeHandle = mergeJob.Schedule(meshJobHandle);
        LastMeshJobHandle = mergeHandle; // store the job handle for later use, e.g. in MeshUploadSystem
        state.Dependency = mergeHandle; // make future work depend on it
        
        mergeHandle.Complete();
        
        // jobData.Dispose(); doesnt need disposal, it's a allocator.tempjob
        batchSliceQueue.Dispose();
        batchEntityQueue.Dispose();
    }
// Merge job
    [BurstCompile]
    public struct MergeQueueJob : IJob
    {
        public NativeQueue<MeshSlice> BatchSliceQueue;
        public NativeQueue<MeshSlice>.ParallelWriter GlobalSliceQueue;

        public void Execute()
        {
            while (BatchSliceQueue.TryDequeue(out var slice))
                GlobalSliceQueue.Enqueue(slice);
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
    public partial struct MeshGenerationJob : 
        // IJobEntity
        IJobFor
    {
        [ReadOnly] public BufferLookup<DOTS_Block> BlockLookup;

        [NativeDisableParallelForRestriction] public NativeArray<Vertex> Vertices;
        [NativeDisableParallelForRestriction] public NativeArray<int> Triangles;
        [NativeDisableParallelForRestriction] public NativeArray<float2> UVs;

        public AtomicCounter VertexCounter;
        public AtomicCounter TriangleCounter;
        public AtomicCounter UVCounter;

        public NativeQueue<MeshSlice>.ParallelWriter BatchSliceQueueParallel;
        
        
        public EntityCommandBuffer.ParallelWriter ecb;

        // public struct MeshGenJobVarsThatIUsedToGetFromEntityQuery
        public struct MeshGenJobVars
        {
            public Entity ChunkEntity;
            public DOTS_Chunk ChunkData;
            public DOTS_ChunkRenderData RenderData;
        }
        [ReadOnly] public NativeArray<MeshGenJobVars> meshGenJobVarsArray;
        public void Execute(int index)
        {
            var vars = meshGenJobVarsArray[index];
            OldEntityExecute(index, vars.ChunkEntity, vars.ChunkData, vars.RenderData, new ChunkMeshPending());
        }
        
        void OldEntityExecute([EntityIndexInQuery] int sortKey, in Entity entity, in DOTS_Chunk chunk,
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
                MeshEntity = renderData.MeshEntity,
                VerticesStart = vertStart,
                VerticesLength = localVertices.Length,
                TrianglesStart = triStart,
                TrianglesLength = localTriangles.Length,
                UVsStart = uvStart,
                UVsLength = localVertices.Length
            };

            BatchSliceQueueParallel.Enqueue(meshSlice);

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
                // Triangle order (0, 2, 1), (1, 2, 3). like the image. this order because it's like ABCD in geometry class
                //This preserves clockwise winding order (which tells the GPU which side is the front).
                //If you reverse it, the face might become invisible due to backface culling.

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
