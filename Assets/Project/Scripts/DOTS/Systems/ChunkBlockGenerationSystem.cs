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
    private BufferLookup<DOTS_Block> blocksLookup;


    // todo
    // maybe we only need to generate blocks:
    // - for chunks that are within a certain distance of the player (e.g., some buffer) - eg. a creeper coming up behind you and exploding - for simulations
    // - chunks that are within our FOV

    // todo cache heightmaps/generated terrain(blocks included)
    // for pillars so we dont have to recalculate them every time a chunk is generated in that pillar


    // todo Chunk pooling
    // - Reuse DOTS_Block buffers for chunks that leave the radius instead of reallocating
    public void OnCreate(ref SystemState state)
    {
        chunksLookup = SystemAPI.GetComponentLookup<DOTS_Chunk>(true);
        blocksLookup = SystemAPI.GetBufferLookup<DOTS_Block>(false);
    }

    public void OnUpdate(ref SystemState state)
    {
        var desiredChunks = SystemAPI.QueryBuilder()
            .WithAny<ChunkBlocksPending>().Build().ToEntityArray(Allocator.Persistent);

        if (desiredChunks.Length == 0)
        {
            // Debug.Log("No chunks need BLOCK generation");
            desiredChunks.Dispose();
            return;
        }

        chunksLookup.Update(ref state);
        blocksLookup.Update(ref state);


        // Debug.Log($"Found {desiredChunks.Length} chunks that need BLOCK generation");
        var worldQuery = SystemAPI.QueryBuilder()
            .WithAll<WorldParams>()
            .Build();

        var worldParams = worldQuery.GetSingleton<WorldParams>();

        var ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        var ecb = ecbSystem.CreateCommandBuffer();
        var ecbParallelWriter = ecb.AsParallelWriter();


        ecb.AddComponent<ChunkMeshPending>(desiredChunks);
        ecb.RemoveComponent<ChunkBlocksPending>(desiredChunks);
        // make a job which generates blocks for each pillar in the map


        var allChunkPillarCoords = new NativeList<int2>(Allocator.TempJob);
        NativeList<int>
            pillarChunkStartIndices =
                new NativeList<int>(Allocator.TempJob); // track which pillar belongs to which chunk

        // Collect all pillar coordinates (x,z) for the chunks that need block generation
        foreach (var chunkEntity in desiredChunks)
        {
            var chunk = chunksLookup[chunkEntity];
            int3 chunkCoord = chunk.ChunkCoord;
            int startIndex = allChunkPillarCoords.Length; // index of first pillar for this chunk
            pillarChunkStartIndices.Add(startIndex);
            // Add all pillar coordinates for this chunk

            int2 chunkPillarCoord = new int2(chunkCoord.x * CHUNK_SIZE, chunkCoord.z * CHUNK_SIZE);
            allChunkPillarCoords.Add(chunkPillarCoord);
        }

        //todo don't allocate this every frame
        NativeArray<int> allXZBlockHeights =
            new NativeArray<int>(allChunkPillarCoords.Length * CHUNK_SIZE * CHUNK_SIZE, Allocator.Persistent);
        // flattened heightmaps

        var heightMapBlockPillarJob = new heightMapBlockPillarJob
        {
            worldSeed = worldParams.worldSeed,
            terrainRoughness = worldParams.terrainRoughness,
            baseHeight = worldParams.baseHeight,
            heightVariation = worldParams.heightVariation,
            noiseLayers = worldParams.noiseLayers,

            pillars = allChunkPillarCoords.AsArray(),
            allXZBlockHeights = allXZBlockHeights,
        };

        var chunkBlockGenerationJob = new ChunkBlockGenerationJob
        {
            worldSeed = worldParams.worldSeed,
            terrainRoughness = worldParams.terrainRoughness,
            baseHeight = worldParams.baseHeight,
            heightVariation = worldParams.heightVariation,
            noiseLayers = worldParams.noiseLayers,
            // ECB = ecbParallelWriter,

            chunksLookup = chunksLookup,
            blocksLookup = blocksLookup,


            allChunks = desiredChunks,
            pillars = allChunkPillarCoords,

            allXZBlockHeights = allXZBlockHeights,
            pillarChunkStartIndices = pillarChunkStartIndices
        };

        int totalBlocks = allChunkPillarCoords.Length * CHUNK_SIZE * CHUNK_SIZE;
        var handle = heightMapBlockPillarJob.ScheduleParallel(totalBlocks, 1, state.Dependency);
        handle = chunkBlockGenerationJob.ScheduleParallel(desiredChunks.Length, 1, handle);
        state.Dependency = handle;

        // pillars.Dispose();

        // ECB.Playback(state.EntityManager);
        // ECB.Dispose();
    }
}

public partial struct heightMapBlockPillarJob : IJobFor
{
    public int worldSeed;
    public float terrainRoughness;
    public float baseHeight;
    public float heightVariation;
    public int noiseLayers;
    [ReadOnly] public NativeArray<int2> pillars; // Array of pillar coordinates (x,z)


    public NativeArray<int> allXZBlockHeights;

    //do for each block
    public void Execute(int jobIndex)
    {
        // how many (x,z) samples per pillar
        int xzBlocksPerPillar = CHUNK_SIZE * CHUNK_SIZE;

        // figure out which pillar this index belongs to
        int pillarIndex = jobIndex / xzBlocksPerPillar;
        int localIndex = jobIndex % xzBlocksPerPillar;

        int localX = localIndex % CHUNK_SIZE;
        int localZ = localIndex / CHUNK_SIZE;

        int2 pillarCoord = pillars[pillarIndex];
        int2 blockCoord = new int2(pillarCoord.x + localX, pillarCoord.y + localZ);

        int startIndex = pillarIndex * xzBlocksPerPillar;

        allXZBlockHeights[startIndex + localIndex] = (int)CalculateTerrainHeight(blockCoord.x, blockCoord.y);
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

public partial struct ChunkBlockGenerationJob : IJobFor
{
    public int worldSeed;
    public float terrainRoughness;
    public float baseHeight;
    public float heightVariation;
    public int noiseLayers;
    [ReadOnly] public NativeArray<Entity> allChunks;
    [ReadOnly] public NativeArray<int2> pillars;

    [ReadOnly] public NativeArray<int> allXZBlockHeights;

    [ReadOnly] public NativeList<int> pillarChunkStartIndices;
    [ReadOnly] public ComponentLookup<DOTS_Chunk> chunksLookup;
    [NativeDisableParallelForRestriction] public BufferLookup<DOTS_Block> blocksLookup;


    //todo
    // pillar height depends on render distance (spawned chunks).
    // i could still make the cylinder render distance as opposed to a sphere to reduce the pillar height.
    // i do worry though if the player would ever encoutner any "fake bottomless holes" or "cut off mountaintops" though.
    // alternatively i coudl disable chunk rendering if they're fully obstructed by another chunk (chunk completely enclosed from all sides.)
    // - but that's a miniscule effect on the CPU right now i think, mostly gpu does that and my game is very gpu light for now still

    public void Execute(int jobIndex)
    {
        Entity chunkEntity = allChunks[jobIndex];
        int3 chunkCoord = chunksLookup[chunkEntity].ChunkCoord;
        var blocks = blocksLookup[chunkEntity];

        InitializeChunkBlocks(ref blocks);


        // get pillar index by mat
        var startPillarIndex = pillarChunkStartIndices[jobIndex]; // first pillar index for this chunk;
        var heightmap = new NativeArray<int>(CHUNK_SIZE * CHUNK_SIZE, Allocator.Temp);


        int startBlockIndex = startPillarIndex * CHUNK_SIZE * CHUNK_SIZE;

        for (int localX = 0; localX < CHUNK_SIZE; localX++)
        for (int localZ = 0; localZ < CHUNK_SIZE; localZ++)
        {
            int height = allXZBlockHeights[startBlockIndex + localX + localZ * CHUNK_SIZE];

            int stoneStart = 0;
            // Determine world Y ranges for each material
            int stoneTop = math.max(stoneStart, height - 4);
            int dirtTop = math.max(stoneTop, height - 1);
            int grassTop = height;

            int baseWorldY = chunkCoord.y * CHUNK_SIZE;

            // Stone
            int stoneLocalStart = math.clamp(stoneStart - baseWorldY, 0, CHUNK_SIZE);
            int stoneLocalEnd = math.clamp(stoneTop - baseWorldY, 0, CHUNK_SIZE);


            //todo first check the regular works
            // if (grassTop < baseWorldY) {
            //     FillChunk(ref blocks, BlockType.Stone);
            //     return;
            // }
            // if (stoneTop > baseWorldY + CHUNK_SIZE) {
            //     FillChunk(ref blocks, BlockType.Air);
            //     return;
            // }
            //


            for (int y = stoneLocalStart; y < stoneLocalEnd; y++)
                blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Stone };

            // Dirt
            int dirtLocalStart = math.clamp(stoneTop - baseWorldY, 0, CHUNK_SIZE);
            int dirtLocalEnd = math.clamp(dirtTop - baseWorldY, 0, CHUNK_SIZE);
            for (int y = dirtLocalStart; y < dirtLocalEnd; y++)
                blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Dirt };

            // Grass
            int grassLocalStart = math.clamp(dirtTop - baseWorldY, 0, CHUNK_SIZE);
            int grassLocalEnd = math.clamp(grassTop - baseWorldY, 0, CHUNK_SIZE);
            for (int y = grassLocalStart; y < grassLocalEnd; y++)
                blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Grass };

            // Air
            int airLocalStart = math.clamp(grassTop - baseWorldY, 0, CHUNK_SIZE);
            for (int y = airLocalStart; y < CHUNK_SIZE; y++)
                blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Air };
        }
    }

    private void FillChunk(ref DynamicBuffer<DOTS_Block> blocks, BlockType blocktype)
    {
        for (int i = 0; i < CHUNK_VOLUME; i++)
            blocks[i] = new DOTS_Block { Value = blocktype };
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

    private void InitializePillarBlocks(NativeArray<Entity> chunks)
    {
        for (int i = 0; i < chunks.Length; i++)
        {
            var chunkEntity = chunks[i];
            var blocks = blocksLookup[chunkEntity];

            if (blocks.Length != CHUNK_VOLUME)
                blocks.ResizeUninitialized(CHUNK_VOLUME);

            for (int j = 0; j < CHUNK_VOLUME; j++)
                blocks[j] = new DOTS_Block { Value = BlockType.Air };
        }
    }

// public partial struct GeneratePerlinBlocksJob : IJobEntity
// {
//     public int worldSeed;
//     public float terrainRoughness;
//     public float baseHeight;
//     public float heightVariation;
//     public int noiseLayers;
//
//     public EntityCommandBuffer.ParallelWriter ECB;
//
//     //does for each chunk
//     public void Execute([EntityIndexInQuery] int index, in DOTS_Chunk chunk, ref DynamicBuffer<DOTS_Block> blocks,
//         Entity entity)
//     {
//         // DotsDebugLog($"Generating blocks for chunk at {chunk.ChunkCoord}");
//
//         ECB.AddComponent<ChunkMeshPending>(index, entity);
//         ECB.RemoveComponent<ChunkBlocksPending>(index, entity);
//
//
//         int3 chunkCoord = chunk.ChunkCoord;
//         InitializeBlocks(ref blocks);
//
// // Heightmap for this chunk (X,Z plane)
//         int[] heightmap = new int[CHUNK_SIZE * CHUNK_SIZE];
//         
//         for (int localX = 0; localX < CHUNK_SIZE; localX++)
//         for (int localZ = 0; localZ < CHUNK_SIZE; localZ++)
//         {
//             int worldX = chunkCoord.x * CHUNK_SIZE + localX;
//             int worldZ = chunkCoord.z * CHUNK_SIZE + localZ;
//             int height = (int)CalculateTerrainHeight(worldX, worldZ);
//             heightmap[localX + localZ * CHUNK_SIZE] = height;
//         }
//
// // Fill blocks using heightmap
//         for (int localX = 0; localX < CHUNK_SIZE; localX++)
//         for (int localZ = 0; localZ < CHUNK_SIZE; localZ++)
//         {
//             int height = heightmap[localX + localZ * CHUNK_SIZE];
//
//             for (int localY = 0; localY < CHUNK_SIZE; localY++)
//             {
//                 int worldY = chunkCoord.y * CHUNK_SIZE + localY;
//                 int blockIndex = localX + localY * CHUNK_SIZE + localZ * CHUNK_SIZE * CHUNK_SIZE;
//                 ref var block = ref blocks.ElementAt(blockIndex);
//
//                 if (worldY < height)
//                 {
//                     if (worldY >= height - 1) block.Value = BlockType.Grass;
//                     else if (worldY >= height - 4) block.Value = BlockType.Dirt;
//                     else block.Value = BlockType.Stone;
//                 }
//                 else
//                 {
//                     block.Value = BlockType.Air;
//                 }
//             }
//         }
//
//         // makes the blocks on that x and z have different blocktypes.
//         // var blockVarietyJob = new BlockVarietyJob();
//     }


    public float CalculateTerrainHeight(int worldX, int worldZ)
    {
        //stolen from chunk.cs
        float perlinScale = terrainRoughness;
        Vector2 perlinOffset = new Vector2(
            math.sin(worldSeed * 0.1f) * 1000f,
            math.cos(worldSeed * 0.1f) * 1000f
        );

        float sampleX = (worldX + perlinOffset.x) * perlinScale;
        float sampleZ = (worldZ + perlinOffset.y) * perlinScale;

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

public partial struct TestChunkBlockGenerationJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    void Execute([EntityIndexInQuery] int sortKey, Entity entity, DynamicBuffer<DOTS_Block> blocks,
        in DOTS_Chunk chunk, ChunkBlocksPending pending)
    {
        // DotsDebugLog($"Generating blocks for chunk at {chunk.ChunkCoord}");
        blocks.Clear();
        int blockCount = 0;
        // Fill first layer with grass
        for (int x = 0; x < CHUNK_SIZE; x++)
        for (int y = 0; y < CHUNK_SIZE; y++)
        for (int z = 0; z < CHUNK_SIZE; z++)
        {
            var value = BlockType.Air;
            if (y == 0)
            {
                value = BlockType.Grass;
            }
            else if (y == 1)
            {
                value = BlockType.Stone;
            }
            else if (y == 2)
            {
                value = BlockType.Dirt;
            }

            blockCount++;

            blocks.Add(new DOTS_Block { Value = value });
        }

        ECB.AddComponent<ChunkMeshPending>(sortKey, entity);
        ECB.RemoveComponent<ChunkBlocksPending>(sortKey, entity);

        // DotsDebugLog($"Added {blockCount} blocks to chunk at {chunk.ChunkCoord}");
    }
}