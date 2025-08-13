using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Project.Scripts.DOTS.Other.DOTS_Utils; 

[UpdateBefore(typeof(MeshGenerationSystem))]
// [BurstCompile]
public partial struct ChunkSpawnSystem : ISystem
{
    private NativeParallelHashSet<int3> _spawnedChunks;
    private Entity chunkPrefabEntity;
    // private EntityArchetype chunkPrefabEntityArchetype;

    private bool entitiesFound;
    private NativeArray<int3> totalOffsets;
    
    
    public void OnCreate(ref SystemState state)
    {
        _spawnedChunks = new NativeParallelHashSet<int3>(1024, Allocator.Persistent);
        

    }

    public void OnDestroy(ref SystemState state)
    {
        if (_spawnedChunks.IsCreated)
            _spawnedChunks.Dispose();
    }

    //todo make happen on event instead, this is inefficient af
    // [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // find player and check that they just changed chunks
        foreach (var (transform,
                     chunkCoord,
                     lastChunkPos)
                 in
                 SystemAPI.Query<
                         RefRO<LocalTransform>,
                         RefRW<EntityChunkCoords>,
                         RefRW<LastChunkCoords>>()
                     .WithAll<PlayerTag>())
        {
            if (chunkCoord.ValueRO.OnChunkChange == false)
            {
                return;
            }

        }

        if(!entitiesFound)
        {
            EntitiesReferences entitiesReferences = SystemAPI.GetSingleton<EntitiesReferences>();
            chunkPrefabEntity = entitiesReferences.chunkPrefabEntity;
            entitiesFound = true;
        }   

        EntityManager entityManager = state.EntityManager;

        //ecb non system version:
        // var ecb = new EntityCommandBuffer(Allocator.TempJob);
        // ----
        
        
        // ECB system version:
        // Get ECB system here (managed)
        //run before simulation so that it can be used in parallel jobs
        var ecbSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        var ecbParallelWriter = ecbSystem.CreateCommandBuffer().AsParallelWriter();
        
        

        NativeQueue<(int3, Entity)> chunksToAddQueue = new NativeQueue<(int3, Entity)>(Allocator.TempJob);

        foreach (var (
                     settings,
                     chunkCoord,
                     loadedChunks) in 
                 SystemAPI.Query<
                     RefRO<PlayerSettings>,
                     RefRO<EntityChunkCoords>,
                     DynamicBuffer<LoadedChunk>>())
        {
            int r = settings.ValueRO.renderDistance;

            //total offsets 

            // 4/3πr^3
            int total = (2 * r + 1) * (2 * r + 1) * (2 * r + 1);
            // The sphere radius in chunks is r.
                // A cube containing it has side length 2r + 1 (to include center chunk).
                // The total positions in that cube = (2r+1)^3
            // This is the upper bound because a sphere will always fit inside that cube,
                // and the cube’s volume is the maximum number of integer offsets you’d need to store.

            // total = new int3[((int)math.ceil(4f / 3f * math.PI * math.pow(r, 3)))];
            NativeArray<int3> offsets = new NativeArray<int3>(total, Allocator.TempJob);
            int idx = 0;
            for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
            for (int z = -r; z <= r; z++)
                offsets[idx++] = new int3(x, y, z);
            
            
            
            var job = new DOTS_SpawnChunksJob
            {
                Offsets = offsets,
                PlayerChunkCoord = chunkCoord.ValueRO.newChunkCoords,
                RenderDistance = r,
                SpawnedChunks = _spawnedChunks.AsParallelWriter(),
                ECB = ecbParallelWriter,
                // ECB = ecb.AsParallelWriter(),
                ChunkPrefabEntity = chunkPrefabEntity,
                ChunksToAddQueue = chunksToAddQueue.AsParallelWriter()
            };
            

            var handle = job.Schedule(total, math.max(1, total / 64), state.Dependency);
            handle.Complete();

            state.Dependency = JobHandle.CombineDependencies(state.Dependency, handle);

            //queue is not enumerable...
            var tempList = new NativeList<(int3, Entity)>(Allocator.Temp);
            while (chunksToAddQueue.TryDequeue(out var item))
            {
                tempList.Add(item);
            }

            
            foreach (var (chunkCoordToAdd, chunkEntityToAdd) in tempList)
            {
                loadedChunks.Add(new LoadedChunk
                {
                    ChunkCoord = chunkCoordToAdd,
                    ChunkEntity = chunkEntityToAdd
                });
            }
         

            chunksToAddQueue.Dispose();
            offsets.Dispose();
        }

        totalOffsets.Dispose();
    }
    
}



[BurstCompile]
public partial struct DOTS_SpawnChunksJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<int3> Offsets;
    [ReadOnly] public int3 PlayerChunkCoord;
    [ReadOnly] public int RenderDistance;
    public NativeParallelHashSet<int3>.ParallelWriter SpawnedChunks;
    public EntityCommandBuffer.ParallelWriter ECB;
    public Entity ChunkPrefabEntity { get; set; }
    public NativeQueue<(int3, Entity)>.ParallelWriter ChunksToAddQueue;

    public void Execute(int index)
    {
        int3 offset = Offsets[index];
        if (math.lengthsq(offset) > RenderDistance * RenderDistance)
            return;

        int3 newCoords = PlayerChunkCoord + offset;

        if (SpawnedChunks.Add(newCoords))
        {
            // Queue chunk spawn
            Entity chunk = ECB.Instantiate(index, ChunkPrefabEntity);
            // When you do ECB.CreateEntity(index) with an empty command buffer (not from a prefab), the entity starts with no components.
            //first param is sortkey - determines the order of execution
            ECB.SetComponent(index, chunk, new LocalTransform
            {
                Position = (GetChunkWorldPos(newCoords)),
                Rotation = quaternion.identity,
                Scale = 1f
            });
            ECB.AddComponent<ChunkBlocksPending>(index,chunk);
            // ECB.AddComponent<ChunkMeshPending>(index, chunk);
            ECB.AddBuffer<DOTS_Block>(index, chunk);  // Add this line
            // ECB.AddComponent(index, chunk, new DOTS_Chunk{ ChunkCoord = newCoords });
            ChunksToAddQueue.Enqueue((newCoords, chunk));
            Debug.Log("ChunkSpawn: adding chunk to queue " + newCoords);
        }

    }
}
