using Project.Scripts.DOTS.Systems;
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
    private bool entitiesFound;
    NativeList<int3> validCoords;

    // new ones for pooling
    private Entity poolEntity;

    private NativeArray<int3> offsets;

    public void OnCreate(ref SystemState state)
    {
        validCoords = new NativeList<int3>(Allocator.Persistent);
        poolEntity = Entity.Null;
    }

    public void OnDestroy(ref SystemState state)
    {
        if (validCoords.IsCreated) validCoords.Dispose();        
    }

    // [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {

        // find player and check that they just changed chunks
        
        var playerChangedChunks = PlayerChangedChunks(ref state);
        if (!playerChangedChunks)
            return;

        checkEntitiesReferences();
        
        // find pool singleton
        if (poolEntity == Entity.Null)
        {
            if (!SystemAPI.TryGetSingletonEntity<ChunkPool>(out poolEntity))
                return; // pool not ready yet
        }
        
        var poolBuffer = SystemAPI.GetBuffer<ChunkPoolElement>(poolEntity);
        var poolData = SystemAPI.GetComponent<ChunkPool>(poolEntity);

        
        var ECBSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        var ecb = ECBSystem.CreateCommandBuffer();
        foreach (var (
                     settings,
                     chunkCoord) in
                 SystemAPI.Query<
                     RefRO<PlayerSettings>,
                     RefRO<EntityChunkCoords>>())
        {
            int renderDist = settings.ValueRO.renderDistance;
            int3 playerChunkCoord = chunkCoord.ValueRO.newChunkCoords;

            InitOffsets(renderDist);
            //todo this can probably also be a sliding window too but it's much less impactful than the heightmap one
            InitValidCoords(renderDist, playerChunkCoord);

            // prepare array of Entities to use (either from pool or newly instantiated)
            int chunksNeededAmount = validCoords.Length;
            var chunksToUse = new NativeArray<Entity>(chunksNeededAmount, Allocator.Temp);


            TakeFromPool(ref state, chunksNeededAmount, poolBuffer, chunksToUse, ecb, poolData);

            InitializeChunks(chunksNeededAmount, chunksToUse, ecb);

            chunksToUse.Dispose();
        
        // oldChunkInstantiation(ecb);
        }
    }

    private void InitializeChunks(int chunksNeededAmount, NativeArray<Entity> chunksToUse, EntityCommandBuffer ecb)
    {
        // initialize/position each chunk entity
        for (int i = 0; i < chunksNeededAmount; i++)
        {
            Entity chunkEnt = chunksToUse[i];
            int3 coord = validCoords[i];

            GetChunkWorldPos(coord, out float3 position);

            // Set components (use ECB to avoid StructuralChange during queries)
            ecb.SetComponent(chunkEnt, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            ecb.SetComponent(chunkEnt, new DOTS_Chunk { ChunkCoord = coord });
            ecb.SetComponent(chunkEnt, new DOTS_ChunkState { Value = ChunkStateEnum.ArrayPending });
        }
    }

    private ChunkPool TakeFromPool(ref SystemState state, int chunksNeededAmount, DynamicBuffer<ChunkPoolElement> poolBuffer,
        NativeArray<Entity> chunksToUse, EntityCommandBuffer ecb, ChunkPool poolData)
    {
        int takenFromPool = 0;

        // take chunks from pool to use for spawning if available
        while (takenFromPool < chunksNeededAmount && poolBuffer.Length > 0)
        {
            int lastIdx = poolBuffer.Length - 1;
            var elem = poolBuffer[lastIdx];
            poolBuffer.RemoveAt(lastIdx);
            chunksToUse[takenFromPool++] = elem.Value;
        }

        // instantiate remainder if pool was insufficient
        if (takenFromPool < chunksNeededAmount)
        {
            int instantiateCount = chunksNeededAmount - takenFromPool;
            var newEntities = new NativeArray<Entity>(instantiateCount, Allocator.Temp);
            ecb.Instantiate(chunkPrefabEntity, newEntities);

            for (int i = 0; i < instantiateCount; i++)
                chunksToUse[takenFromPool + i] = newEntities[i];

            newEntities.Dispose();
        }

// update pool metadata (ActiveCount) immediately on the singleton
        poolData.ActiveCount += chunksNeededAmount;
        state.EntityManager.SetComponentData(poolEntity, poolData);
        return poolData;
    }

    private void oldChunkInstantiation(EntityCommandBuffer ecb)
    {
        // batch instantiate
        NativeArray<Entity> chunks = new NativeArray<Entity>(validCoords.Length, Allocator.Temp);
        ecb.Instantiate(chunkPrefabEntity, chunks);

        for (int i = 0; i < validCoords.Length; i++)
        {
            var chunk = chunks[i];
            GetChunkWorldPos(validCoords[i], out float3 position);
            ecb.SetComponent(chunk, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            
            ecb.SetComponent(chunk, new DOTS_Chunk { ChunkCoord = validCoords[i] });
            ecb.SetComponent(chunk, new DOTS_ChunkState { Value = ChunkStateEnum.ArrayPending });
        }
        chunks.Dispose();
    }

    private void InitOffsets(int renderDist)
    {
        //total offsets 
        // 4/3πr^3
        int total = (2 * renderDist + 1) * (2 * renderDist + 1) * (2 * renderDist + 1);
        
        if (offsets.IsCreated && offsets.Length == total)
            return;
        
        if (offsets.IsCreated)
            offsets.Dispose();
        
        // The sphere radius in chunks is r.
        // A cube containing it has side length 2r + 1 (to include center chunk).
        // The total positions in that cube = (2r+1)^3
        // This is the upper bound because a sphere will always fit inside that cube,
        // and the cube’s volume is the maximum number of integer offsets you’d need to store.

        // total = new int3[((int)math.ceil(4f / 3f * math.PI * math.pow(r, 3)))];
        offsets = new NativeArray<int3>(total, Allocator.Persistent);
        int idx = 0;
        for (int x = -renderDist; x <= renderDist; x++)
        for (int y = -renderDist; y <= renderDist; y++)
        for (int z = -renderDist; z <= renderDist; z++)
            offsets[idx++] = new int3(x, y, z);
    }

    private void InitValidCoords(int renderDist, int3 playerChunkCoord)
    {
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
    }

    private void checkEntitiesReferences()
    {
        if (!entitiesFound)
        {
            EntitiesReferences entitiesReferences = SystemAPI.GetSingleton<EntitiesReferences>();
            chunkPrefabEntity = entitiesReferences.chunkPrefabEntity;
            entitiesFound = true;
        }
    }

    public bool PlayerChangedChunks(ref SystemState state)
    {
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
            if (chunkCoord.ValueRO.OnChunkChange)
            {
                return true;
            }
        }

        return false;
    }
}

[UpdateAfter(typeof(ChunkSpawnSystem))]
[UpdateBefore(typeof(HeightMapSystem))]
public partial struct FillLoadedChunksSystem : ISystem
{

    public void OnCreate(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        var ECBSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        var ECB = ECBSystem.CreateCommandBuffer();
        //find player and their loaded chunks
        //we need to wait for the ECB to finish before we can fill the loaded chunks, that's why this exists instead of adding it inside chunkspawnsystem 
        foreach (var (settings, chunkCoords) in
                 SystemAPI.Query<
                     RefRO<PlayerSettings>,
                     RefRO<EntityChunkCoords>>())
        {
            foreach (var (chunk, chunkState, entity) in SystemAPI.Query<DOTS_Chunk, DOTS_ChunkState>()
                         .WithEntityAccess())
            {
                if (chunkState.Value == ChunkStateEnum.ArrayPending)
                {
                    ECB.SetComponent(entity, new DOTS_ChunkState { Value = ChunkStateEnum.BlockGenPending });
                }
            }
        }
    }
}