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
    NativeList<int3> validCoords;


    public void OnCreate(ref SystemState state)
    {
        validCoords = new NativeList<int3>(Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        validCoords.Dispose();
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
        var ecb = ecbSystem.CreateCommandBuffer();
        var ecbParallelWriter = ecb.AsParallelWriter();

        foreach (var (
                     settings,
                     chunkCoord,
                     loadedChunks) in
                 SystemAPI.Query<
                     RefRO<PlayerSettings>,
                     RefRO<EntityChunkCoords>,
                     DynamicBuffer<PlayerLoadedChunk>>())
        {
            int renderDist = settings.ValueRO.renderDistance;
            int3 playerChunkCoord = chunkCoord.ValueRO.newChunkCoords;
            //total offsets 

            // 4/3πr^3
            int total = (2 * renderDist + 1) * (2 * renderDist + 1) * (2 * renderDist + 1);
            // The sphere radius in chunks is r.
            // A cube containing it has side length 2r + 1 (to include center chunk).
            // The total positions in that cube = (2r+1)^3
            // This is the upper bound because a sphere will always fit inside that cube,
            // and the cube’s volume is the maximum number of integer offsets you’d need to store.

            // total = new int3[((int)math.ceil(4f / 3f * math.PI * math.pow(r, 3)))];
            NativeArray<int3> offsets = new NativeArray<int3>(total, Allocator.TempJob);
            int idx = 0;
            for (int x = -renderDist; x <= renderDist; x++)
            for (int y = -renderDist; y <= renderDist; y++)
            for (int z = -renderDist; z <= renderDist; z++)
                offsets[idx++] = new int3(x, y, z);

            // NativeList<int3> validCoords = new NativeList<int3>(Allocator.Temp);
            validCoords.Clear();

            for (int x = -renderDist; x <= renderDist; x++)
            for (int y = -renderDist; y <= renderDist; y++)
            for (int z = -renderDist; z <= renderDist; z++)
            {
                int3 offset = new int3(x, y, z);
                if (math.lengthsq(offset.xz) > renderDist * renderDist) // cylinder
                    continue;

                validCoords.Add(playerChunkCoord + offset);
            }

// batch instantiate
            NativeArray<Entity> chunks = new NativeArray<Entity>(validCoords.Length, Allocator.Temp);
            ecb.Instantiate(chunkPrefabEntity, chunks);

            for (int i = 0; i < validCoords.Length; i++)
            {
                var chunk = chunks[i];
                ecb.SetComponent(chunk, new LocalTransform
                {
                    Position = GetChunkWorldPos(validCoords[i]),
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                ecb.SetComponent(chunk, new DOTS_Chunk { ChunkCoord = validCoords[i] });
                ecb.SetComponent(chunk, new DOTS_ChunkState { Value = ChunkState.Spawned });
            }

            chunks.Dispose();
        }
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
        foreach (var (settings, loadedChunks, chunkCoords) in
                 SystemAPI.Query<
                     RefRO<PlayerSettings>,
                     DynamicBuffer<PlayerLoadedChunk>,
                     RefRO<EntityChunkCoords>>())
        {
            foreach (var (chunk, chunkState, entity) in SystemAPI.Query<DOTS_Chunk, DOTS_ChunkState>()
                         .WithEntityAccess())
            {
                if (chunkState.Value == ChunkState.Spawned)
                {
                    loadedChunks.Add(new PlayerLoadedChunk
                    {
                        ChunkCoord = chunk.ChunkCoord,
                        ChunkEntity = entity
                    });
                    ECB.SetComponent(entity, new DOTS_ChunkState { Value = ChunkState.InChunkArr });
                }
            }
        }
    }
}