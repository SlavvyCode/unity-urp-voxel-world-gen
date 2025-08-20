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
    
        
    
    
    public void OnCreate(ref SystemState state)
    {
        
        
        chunksLookup = SystemAPI.GetComponentLookup<DOTS_Chunk>(true);
        blocksLookup = SystemAPI.GetBufferLookup<DOTS_Block>(false);
    }

    public void OnUpdate(ref SystemState state)
    {
        var desiredChunks = SystemAPI.QueryBuilder()
            .WithAny<ChunkBlocksPending>().Build().ToEntityArray(Allocator.Temp);

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
        
        // instead i need too....
        // 1. query all dots chunks, ask them for their coords and
        // 2. split into pillars (list chunk entities) based on Y axis. then
        // 3.  make a job foreach pillar in which i write hte relevant stuff all together a time
        
        // chunkQuery
        NativeParallelMultiHashMap<int2, Entity> pillars = new NativeParallelMultiHashMap<int2, Entity>(desiredChunks.Length, Allocator.TempJob);
        foreach (var chunkEntity in desiredChunks)
        {
            var chunk = SystemAPI.GetComponent<DOTS_Chunk>(chunkEntity);
            int2 pillarCoord = new int2(chunk.ChunkCoord.x, chunk.ChunkCoord.z);
            pillars.Add(pillarCoord, chunkEntity);
        }
        
        NativeArray<Entity> allChunks = new NativeArray<Entity>(desiredChunks.Length, Allocator.TempJob);
        NativeArray<int> pillarStartsArray = new NativeArray<int>(pillars.Count(), Allocator.TempJob);
        NativeArray<int> chunksPerPillarArr = new NativeArray<int>(pillars.Count(), Allocator.TempJob);
        
        // Fill allChunks linearly and record start/count per pillar
        int pillarIndex = 0;
        int currentPillarStart = 0;
        int currentPillarCount = 0;
        foreach (var chunkEntity in desiredChunks)
        {
            allChunks[pillarIndex] = chunkEntity;
            var chunk = SystemAPI.GetComponent<DOTS_Chunk>(chunkEntity);
            int2 pillarCoord = new int2(chunk.ChunkCoord.x, chunk.ChunkCoord.z);
            
            if (pillarIndex == 0 || !pillars.ContainsKey(pillarCoord))
            {
                // New pillar starts here
                if (currentPillarCount > 0)
                {
                    pillarStartsArray[pillarIndex - currentPillarCount] = currentPillarStart;
                    chunksPerPillarArr[pillarIndex - currentPillarCount] = currentPillarCount;
                }
                currentPillarStart = pillarIndex;
                currentPillarCount = 0;
            }
            
            currentPillarCount++;
            pillarIndex++;
        }
        // After the loop
        if (currentPillarCount > 0)
        {
            pillarStartsArray[pillarIndex - currentPillarCount] = currentPillarStart;
            chunksPerPillarArr[pillarIndex - currentPillarCount] = currentPillarCount;
        }

        
        
        var pillarKeys = pillars.GetKeyArray(Allocator.TempJob);
        
        ecb.AddComponent<ChunkMeshPending>(desiredChunks);
        ecb.RemoveComponent<ChunkBlocksPending>(desiredChunks);
        // make a job which generates blocks for each pillar in the map
        
        
         var pillarBlockGenerationJob = new PillarBlockGenerationJob
        {
            chunkPillars = pillars,
            pillarsKeyArray = pillarKeys,
            worldSeed = worldParams.worldSeed,
            terrainRoughness = worldParams.terrainRoughness,
            baseHeight = worldParams.baseHeight,
            heightVariation = worldParams.heightVariation,
            noiseLayers = worldParams.noiseLayers,
            // ECB = ecbParallelWriter,
            
            chunksLookup = chunksLookup,
            blocksLookup = blocksLookup,
            
            
            pillarStartsArray = pillarStartsArray,
            chunksPerPillarArr = chunksPerPillarArr,
            allChunks = allChunks
            
        };



        state.Dependency = pillarBlockGenerationJob.ScheduleParallel(pillarKeys.Length, 1, state.Dependency);

        // ECB.Playback(state.EntityManager);
        // ECB.Dispose();
    }
}

public partial struct PillarBlockGenerationJob : IJobFor
{
    
    public int worldSeed;
    public float terrainRoughness;
    public float baseHeight;
    public float heightVariation;
    public int noiseLayers;
    [ReadOnly] public NativeParallelMultiHashMap<int2, Entity> chunkPillars;
    [ReadOnly] public NativeArray<int2> pillarsKeyArray;
    // public EntityCommandBuffer.ParallelWriter ECB;
    [ReadOnly] public NativeArray<int> pillarStartsArray;
    [ReadOnly] public NativeArray<int> chunksPerPillarArr;
    [ReadOnly] public NativeArray<Entity> allChunks;
    
    
    //todo
    // pillar height depends on render distance (spawned chunks).
    // i could still make the cylinder render distance as opposed to a sphere to reduce the pillar height.
    // i do worry though if the player would ever encoutner any "fake bottomless holes" or "cut off mountaintops" though.
    // alternatively i coudl disable chunk rendering if they're fully obstructed by another chunk (chunk completely enclosed from all sides.)
    // - but that's a miniscule effect on the CPU right now i think, mostly gpu does that and my game is very gpu light for now still
    [ReadOnly] public ComponentLookup<DOTS_Chunk> chunksLookup;
    [NativeDisableParallelForRestriction] public BufferLookup<DOTS_Block> blocksLookup;
    public void Execute(int index)
    {
        //happens for each pillar.
        NativeParallelMultiHashMapIterator<int2> it;
        Entity chunkEntity;
        
        //iteration's values
        var currPillarStart = pillarStartsArray[index];
        var currPillarChunkCount = chunksPerPillarArr[index];
        if (currPillarChunkCount == 0)
            return; // skip empty pillars

        //get chunks that belong to this pillar
        var pillarChunkEntities = allChunks.Slice(currPillarStart, currPillarChunkCount);

        Span<int> heightmap = stackalloc int[CHUNK_SIZE * CHUNK_SIZE];
        // Get the pillar coordinate from the index
        int2 pillarCoord = pillarsKeyArray[index];

        
        var firstChunkOfPillar = pillarChunkEntities[0];
        //todo consider having this func return all pillar blocks 
        InitializePillarBlocks(allChunks);
        int3 chunkCoord = chunksLookup[firstChunkOfPillar].ChunkCoord;
            
        
        // heightmap for this chunk (X,Z plane)
        //this is too heavy, arrayis better.
        // int[] heightmap = new int[CHUNK_SIZE * CHUNK_SIZE];
        heightmap.Clear(); // Clear the heightmap array
        // todo is it smart to declare here? probably better outside the loop, but then i need to dispose it.
        // NativeArray<int> heightmap = new NativeArray<int>(CHUNK_SIZE*CHUNK_SIZE, Allocator.Temp);
        // calculate heightmap for this chunk/pillar (same thing)
        for (int localX = 0; localX < CHUNK_SIZE; localX++)
        for (int localZ = 0; localZ < CHUNK_SIZE; localZ++)
        {
            int worldX = chunkCoord.x * CHUNK_SIZE + localX;
            int worldZ = chunkCoord.z * CHUNK_SIZE + localZ;
            int height = (int)CalculateTerrainHeight(worldX, worldZ);
            heightmap[localX + localZ * CHUNK_SIZE] = height;
        }
        
        for (int localX = 0; localX < CHUNK_SIZE; localX++)
        for (int localZ = 0; localZ < CHUNK_SIZE; localZ++)
        {
            int height = heightmap[localX + localZ * CHUNK_SIZE];

            int stoneStart = 0;
            // Determine world Y ranges for each material
            int stoneTop = math.max(stoneStart, height - 4);
            int dirtTop = math.max(stoneTop, height - 1);
            int grassTop = height;

            for (int c = 0; c < pillarChunkEntities.Length; c++)
            {
                var chunk = chunksLookup[pillarChunkEntities[c]];
                var blocks = blocksLookup[pillarChunkEntities[c]];
                int baseWorldY = chunk.ChunkCoord.y * CHUNK_SIZE;

                // Stone
                int stoneLocalStart = math.clamp(stoneStart - baseWorldY, 0, CHUNK_SIZE);
                int stoneLocalEnd   = math.clamp(stoneTop  - baseWorldY, 0, CHUNK_SIZE);
                for (int y = stoneLocalStart; y < stoneLocalEnd; y++)
                    blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Stone };

                // Dirt
                int dirtLocalStart = math.clamp(stoneTop - baseWorldY, 0, CHUNK_SIZE);
                int dirtLocalEnd   = math.clamp(dirtTop   - baseWorldY, 0, CHUNK_SIZE);
                for (int y = dirtLocalStart; y < dirtLocalEnd; y++)
                    blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Dirt };

                // Grass
                int grassLocalStart = math.clamp(dirtTop - baseWorldY, 0, CHUNK_SIZE);
                int grassLocalEnd   = math.clamp(grassTop - baseWorldY, 0, CHUNK_SIZE);
                for (int y = grassLocalStart; y < grassLocalEnd; y++)
                    blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Grass };

                // Air
                int airLocalStart = math.clamp(grassTop - baseWorldY, 0, CHUNK_SIZE);
                for (int y = airLocalStart; y < CHUNK_SIZE; y++)
                    blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Air };
            }

            // heightmap.Dispose(); // Dispose the heightmap after use
        }



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


    private float CalculateTerrainHeight(int worldX, int worldZ)
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