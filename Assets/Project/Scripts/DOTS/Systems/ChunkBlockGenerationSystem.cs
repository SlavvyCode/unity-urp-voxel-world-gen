using System;
using Project.Scripts.DOTS.Other;
using Project.Scripts.DOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Project.Scripts.DOTS.Other.DOTS_Utils;

[BurstCompile]
[UpdateAfter(typeof(ChunkDespawnSystem))]
[UpdateBefore(typeof(MeshGenerationSystem))]
public partial struct ChunkBlockGenerationSystem : ISystem
{
    private ComponentLookup<DOTS_Chunk> chunksLookup;

    // private ComponentLookup<DOTS_ChunkState> chunkStateLookup;
    private BufferLookup<DOTS_Block> blocksLookup;


    // todo
    // maybe we only need to generate blocks:
    // - for chunks that are within a certain distance of the player (e.g., some buffer) - eg. a creeper coming up behind you and exploding - for simulations
    // - chunks that are within our FOV

    // IMPORTANT NOTE!
    // CANNOT BE REFACTORED

    // todo cache heightmaps/generated terrain(blocks included)
    // for pillars so we dont have to recalculate them every time a chunk is generated in that pillar
    private NativeList<Entity> desiredChunks;

    // private NativeList<int2> chunkYColumnCoords;
    private NativeParallelHashMap<int2, int> blockColumnCoordsToHeightHashMap;
    private EntityQuery allChunksQuery;

    private NativeList<int2> chunkXZCoords;

    // todo Chunk pooling
    // - Reuse DOTS_Block buffers for chunks that leave the radius instead of reallocating
    public void OnCreate(ref SystemState state)
    {
        chunksLookup = SystemAPI.GetComponentLookup<DOTS_Chunk>(true);
        blocksLookup = SystemAPI.GetBufferLookup<DOTS_Block>(false);
        // chunkStateLookup = SystemAPI.GetComponentLookup<DOTS_ChunkState>(false);

        allChunksQuery = SystemAPI.QueryBuilder()
            .WithAny<DOTS_ChunkState>()
            .Build();


        desiredChunks = new NativeList<Entity>(Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        //can not be disposed i think
        // allChunksQuery.Dispose();
        if (blockColumnCoordsToHeightHashMap.IsCreated)
            blockColumnCoordsToHeightHashMap.Dispose();
        desiredChunks.Dispose();

        if (!chunkXZCoords.IsCreated)
            chunkXZCoords.Dispose();
        // chunkYColumnCoords.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        #region init uninteresting vars

        desiredChunks.Clear();

        var allChunks =
            allChunksQuery.ToEntityArray(Allocator
                .Temp); //remove from desired chunks entities which have chunkstate of different kind than ready forblockgeneration
        var chunkStates = SystemAPI.GetComponentLookup<DOTS_ChunkState>(true); // read-only

        for (int i = 0; i < allChunks.Length; i++)
        {
            var entity = allChunks[i];
            if (chunkStates[entity].Value == ChunkState.InChunkArr)
            {
                desiredChunks.Add(entity);
            }
        }


        if (desiredChunks.Length == 0)
            return;

        chunksLookup.Update(ref state);
        blocksLookup.Update(ref state);

        // Debug.Log($"Found {desiredChunks.Length} chunks that need BLOCK generation");
        var worldQuery = SystemAPI.QueryBuilder()
            .WithAll<WorldParams>()
            .Build();

        var worldParams = worldQuery.GetSingleton<WorldParams>();

        var ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        var ecb = ecbSystem.CreateCommandBuffer();
        // var ecbParallelWriter = ecb.AsParallelWriter();


        //todo replace the hashmap to make it faster if that's where the bottleneck is
        if (!blockColumnCoordsToHeightHashMap.IsCreated)
            blockColumnCoordsToHeightHashMap =
                new NativeParallelHashMap<int2, int>(desiredChunks.Length, Allocator.Persistent);
        else if (blockColumnCoordsToHeightHashMap.Count() < desiredChunks.Length)
        {
            blockColumnCoordsToHeightHashMap.Dispose();
            blockColumnCoordsToHeightHashMap =
                new NativeParallelHashMap<int2, int>(desiredChunks.Length, Allocator.Persistent);
        }
        else blockColumnCoordsToHeightHashMap.Clear(); // safe only if no jobs are scheduled


        if (!chunkXZCoords.IsCreated)
            chunkXZCoords = new NativeList<int2>(Allocator.Persistent);
        else
            chunkXZCoords.Clear();
        //todo can be made much faster with array i think

        #endregion

        // 1. get unique xz block coordinates (x,z) to pass to the heightmap job?
        foreach (var chunkEntity in desiredChunks)
        {
            var chunk = chunksLookup[chunkEntity];
            int3 chunkCoord = chunk.ChunkCoord;
            // int startIndex = chunkYColumnCoords.Length; // index of first pillar for this chunk
            int2 chunkColumnXZCoord = new int2(chunkCoord.x * CHUNK_SIZE, chunkCoord.z * CHUNK_SIZE);
            //todo is it okay to just put a temp value? it's certainly faster than making a new variable array and then disposing of  it
            // we can just change the keys later right?
            chunkXZCoords.Add(chunkColumnXZCoord);
        }


        int uniqueXZblockCount = blockColumnCoordsToHeightHashMap.Count();
        int totalBlocks = uniqueXZblockCount * CHUNK_SIZE * CHUNK_SIZE;
        int totalUniqueXZChunks = totalBlocks / (CHUNK_SIZE * CHUNK_SIZE);


        //2. do heightmap job
        // todo WHAT DO I NEED TO KNOW TO GENERATE HEIGHT MAP FOR ANY GIVEN BLOCK COLUMN
        var heightMapForBlockColumnsJob = new heightMapForBlockColumnsJob
        {
            worldSeed = worldParams.worldSeed,
            terrainRoughness = worldParams.terrainRoughness,
            baseHeight = worldParams.baseHeight,
            heightVariation = worldParams.heightVariation,
            noiseLayers = worldParams.noiseLayers,

            chunkXZCoords = chunkXZCoords,
            coordsToHeightsHashMap = blockColumnCoordsToHeightHashMap.AsParallelWriter()
        };

        //3 do generation job 
        // todo WHAT DO I NEED TO HAVE ASSIGN HEIGHTMAPS TO EACH CHUNK
        // heightmap or heights
        // each chunk 
        var generateChunkBlocksJob = new GenerateChunkBlocksJob
        {
            chunksLookup = chunksLookup,
            blocksLookup = blocksLookup,
            ecb = ecb.AsParallelWriter(),

            desiredChunks = desiredChunks,
            blockColumnCoordsToHeightHashMap = blockColumnCoordsToHeightHashMap,
        };

        var handle = heightMapForBlockColumnsJob.ScheduleParallel(totalUniqueXZChunks, 1, state.Dependency);
        handle = generateChunkBlocksJob.ScheduleParallel(desiredChunks.Length, 1, handle);

        state.Dependency = handle;
        // pillars.Dispose();
        handle.Complete(); // MAIN THREAD waits here


        // ECB.Playback(state.EntityManager);
        // ECB.Dispose();
    }
}

public partial struct heightMapForBlockColumnsJob : IJobFor
{
    public int worldSeed;
    public float terrainRoughness;
    public float baseHeight;
    public float heightVariation;
    public int noiseLayers;

    public NativeParallelHashMap<int2, int>.ParallelWriter coordsToHeightsHashMap; // Array of pillar coordinates (x,z)
    public NativeList<int2> chunkXZCoords;


    //do for each chunk
    public void Execute(int jobIndex)
    {
        // get a chunk based on index
        // how many (x,z) samples per pillar
        int xzBlocksPerPillar = CHUNK_SIZE * CHUNK_SIZE;

        // figure out which pillar this index belongs to
        // same number of chunks as there is of this job's executes
        int chunkIndex = jobIndex;

        for (int i = 0; i < CHUNK_SIZE; i++)
        for (int j = 0; j < CHUNK_SIZE; j++)
        {
            int2 columnCoord = chunkXZCoords[jobIndex];
            int height = (int)CalculateTerrainHeight(columnCoord.x, columnCoord.y);
            coordsToHeightsHashMap.TryAdd(columnCoord, height);
        }
    }

    float CalculateTerrainHeight(int pillarX, int pillarY)
    {
        {
            //stolen from chunk.cs
            float perlinScale = terrainRoughness;
            Vector2 perlinOffset = new Vector2(
                math.sin(worldSeed * 0.1f) * 1000f,
                math.cos(worldSeed * 0.1f) * 1000f
            );

            float sampleX = (pillarX + perlinOffset.x) * perlinScale;
            float sampleZ = (pillarY + perlinOffset.y) * perlinScale;

            float total = 0f;
            float max = 0f;
            float amplitude = 1f;
            float frequency = 1f;

            for (int i = 0; i < noiseLayers; i++)
            {
                total += MyPerlin.Noise(sampleX * frequency, sampleZ * frequency) * amplitude;
                max += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            float normalized = total / max;
            return baseHeight + (normalized * heightVariation);
        }
    }
}

public partial struct GenerateChunkBlocksJob : IJobFor
{
    [ReadOnly] public NativeArray<Entity> desiredChunks;

    // [ReadOnly] public NativeArray<int2> chunkYColumnCoords;
    [ReadOnly] public ComponentLookup<DOTS_Chunk> chunksLookup;
    [NativeDisableParallelForRestriction] public BufferLookup<DOTS_Block> blocksLookup;
    [ReadOnly] public NativeParallelHashMap<int2, int> blockColumnCoordsToHeightHashMap;
    public EntityCommandBuffer.ParallelWriter ecb;

    //todo
    // pillar height depends on render distance (spawned chunks).
    // i could still make the cylinder render distance as opposed to a sphere to reduce the pillar height.
    // i do worry though if the player would ever encoutner any "fake bottomless holes" or "cut off mountaintops" though.
    // alternatively i coudl disable chunk rendering if they're fully obstructed by another chunk (chunk completely enclosed from all sides.)
    // - but that's a miniscule effect on the CPU right now i think, mostly gpu does that and my game is very gpu light for now still


    // EXECUTES ONCE PER EACH CHUNK IN THE RENDER DISTANCE
    public void Execute(int jobIndex)
    {
        Entity chunkEntity = desiredChunks[jobIndex];
        int3 chunkCoord = chunksLookup[chunkEntity].ChunkCoord;
        DynamicBuffer<DOTS_Block> blocks = blocksLookup[chunkEntity];

        InitializeChunkBlocks(ref blocks);


        //find a way to get all chunks that are in the current chunk column
// todo???
        int2 chunkColumnCoord = new int2(chunkCoord.x * CHUNK_SIZE, chunkCoord.z * CHUNK_SIZE);
        int columnHeight;
        blockColumnCoordsToHeightHashMap.TryGetValue(chunkColumnCoord, out columnHeight);

        int chunkWorldYStart = chunkCoord.y * CHUNK_SIZE;
        int chunkWorldYEnd = chunkWorldYStart + CHUNK_SIZE;

        int stoneStart = int.MinValue;
        int stoneTop   = math.max(stoneStart, columnHeight - 4);
        int dirtTop    = math.max(stoneTop, columnHeight - 1);
        int grassTop   = columnHeight;
 
        // already all Air
        for (int localX = 0; localX < CHUNK_SIZE; localX++)
        for (int localZ = 0; localZ < CHUNK_SIZE; localZ++)
        {
            int baseWorldY = chunkCoord.y * CHUNK_SIZE;


            // Stone
            int stoneLocalStart = math.clamp(stoneStart - baseWorldY, 0, CHUNK_SIZE);
            int stoneLocalEnd   = math.clamp(stoneTop   - baseWorldY, 0, CHUNK_SIZE);
            for (int y = stoneLocalStart; y < stoneLocalEnd; y++)
                blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Stone };

            // Dirt
            int dirtLocalStart = math.clamp(stoneTop - baseWorldY, 0, CHUNK_SIZE);
            int dirtLocalEnd   = math.clamp(dirtTop  - baseWorldY, 0, CHUNK_SIZE);
            for (int y = dirtLocalStart; y < dirtLocalEnd; y++)
                blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Dirt };

            // Grass
            int grassLocalStart = math.clamp(dirtTop - baseWorldY, 0, CHUNK_SIZE);
            int grassLocalEnd   = math.clamp(grassTop- baseWorldY, 0, CHUNK_SIZE);
            for (int y = grassLocalStart; y < grassLocalEnd; y++)
                blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Grass };
        }



        // foreach (var chunk in desiredChunks)
        // {
        // ecb.SetComponent<DOTS_ChunkState>(chunkEntity, new DOTS_ChunkState { Value = ChunkState.BlocksGenerated });

        ecb.SetComponent(jobIndex, chunkEntity, new DOTS_ChunkState { Value = ChunkState.BlocksGenerated });
        // }
    }


    private void InitializeChunkBlocks(ref DynamicBuffer<DOTS_Block> blocks)
    {
        if (blocks.Length == CHUNK_VOLUME)
        {
            return;
        }

        // If the buffer is not the correct size, we need to initialize it
        blocks.ResizeUninitialized(CHUNK_VOLUME);

        for (int i = 0; i < CHUNK_VOLUME; i++)
            blocks[i] = new DOTS_Block { Value = BlockType.Air };
    }
}