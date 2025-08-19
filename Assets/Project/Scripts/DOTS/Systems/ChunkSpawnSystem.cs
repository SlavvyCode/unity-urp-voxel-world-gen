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
    private Entity chunkPrefabEntity;
    // private EntityArchetype chunkPrefabEntityArchetype;

    private bool entitiesFound;


    public void OnCreate(ref SystemState state)
    {
    }

    public void OnDestroy(ref SystemState state)
    {
    }

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

        if (!entitiesFound)
        {
            EntitiesReferences entitiesReferences = SystemAPI.GetSingleton<EntitiesReferences>();
            chunkPrefabEntity = entitiesReferences.chunkPrefabEntity;
            entitiesFound = true;
        }

        EntityManager entityManager = state.EntityManager;

        //ecb non system version:
        // var ecb = new EntityCommandBuffer(Allocator.TempJob);
        // var ecbParallelWriter = ecb.AsParallelWriter();

        // ----


        // ECB system version:
        // Get ECB system here (managed)
        //run before simulation so that it can be used in parallel jobs
        var ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        var ecbParallelWriter = ecbSystem.CreateCommandBuffer().AsParallelWriter();



        foreach (var (
                     settings,
                     chunkCoord,
                     loadedChunks) in
                 SystemAPI.Query<
                     RefRO<PlayerSettings>,
                     RefRO<EntityChunkCoords>,
                     DynamicBuffer<PlayerLoadedChunk>>())
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
                ECB = ecbParallelWriter,
                // ECB = ecb.AsParallelWriter(),
                ChunkPrefabEntity = chunkPrefabEntity,
            };


            var handle = job.Schedule(total, math.max(1, total / 64), state.Dependency);
            handle.Complete();
  
            // state.Dependency = JobHandle.CombineDependencies(state.Dependency, handle);


            offsets.Dispose();
        }

        //for non-system ecb, you need to playback the commands after the job is done
        // ecb.Playback(state.EntityManager);
        // ecb.Dispose();

    }
}


[BurstCompile]
public partial struct DOTS_SpawnChunksJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<int3> Offsets;
    [ReadOnly] public int3 PlayerChunkCoord;
    [ReadOnly] public int RenderDistance;
    public EntityCommandBuffer.ParallelWriter ECB;
    public Entity ChunkPrefabEntity;

    public void Execute(int index)
    {
        int3 offset = Offsets[index];
        if (math.lengthsq(offset) > RenderDistance * RenderDistance)
            return;

        int3 newCoords = PlayerChunkCoord + offset;

        DotsDebugLog("ChunkSpawn: Spawning chunk at " + newCoords);
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
            ECB.SetComponent<DOTS_Chunk>(index, chunk, new DOTS_Chunk
            {
                ChunkCoord = newCoords
            });
            ECB.AddComponent<LoadedChunksPending>(index, chunk);
            ECB.AddBuffer<DOTS_Block>(index, chunk); 
            // ECB.AddComponent(index, chunk, new DOTS_Chunk{ ChunkCoord = newCoords });
            // Debug.Log("ChunkSpawn: adding chunk to queue " + newCoords);
    }
}

[UpdateAfter(typeof(ChunkSpawnSystem))]
[UpdateBefore(typeof(ChunkBlockGenerationSystem))]

public partial struct FillLoadedChunksSystem : ISystem
{
    EntityCommandBuffer ECB;
    public void OnCreate(ref SystemState state)
    {

    }

    public void OnUpdate(ref SystemState state)
    {
        var ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        ECB = ecbSystem.CreateCommandBuffer();

        //find player and their loaded chunks
        //we need to wait for the ECB to finish before we can fill the loaded chunks, that's why this exists instead of adding it inside chunkspawnsystem 
        foreach (var (settings,loadedChunks, chunkCoords) in
                 SystemAPI.Query<
                     RefRO<PlayerSettings>,
                     DynamicBuffer<PlayerLoadedChunk>,
                     RefRO<EntityChunkCoords>>())
        {
            


            foreach (var (chunk,pending,entity)  in SystemAPI.Query<DOTS_Chunk,LoadedChunksPending>().WithEntityAccess())
            {
                loadedChunks.Add(new PlayerLoadedChunk
                {
                    ChunkCoord = chunk.ChunkCoord,
                    ChunkEntity = entity
                });
                ECB.RemoveComponent<LoadedChunksPending>(entity);
                ECB.AddComponent<ChunkBlocksPending>(entity);

                
            }
            

        }
        
    }
}
