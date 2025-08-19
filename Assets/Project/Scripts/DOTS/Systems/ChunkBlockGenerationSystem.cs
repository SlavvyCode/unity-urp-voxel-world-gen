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
        // Debug.Log($"Found {desiredChunks.Length} chunks that need BLOCK generation");


        var chunkQuery = SystemAPI.QueryBuilder()
            .WithAll<DOTS_Chunk, ChunkBlocksPending, DOTS_Block>()
            .Build();

        var worldQuery = SystemAPI.QueryBuilder()
            .WithAll<WorldParams>()
            .Build();

        var worldParams = worldQuery.GetSingleton<WorldParams>();

        var ECB = new EntityCommandBuffer(Allocator.TempJob);
        state.Dependency = new GeneratePerlinBlocksJob
            {
                worldSeed = worldParams.worldSeed,
                terrainRoughness = worldParams.terrainRoughness,
                baseHeight = worldParams.baseHeight,
                heightVariation = worldParams.heightVariation,
                noiseLayers = worldParams.noiseLayers,
                ECB = ECB.AsParallelWriter()
            }
            .ScheduleParallel(chunkQuery, state.Dependency);

        state.Dependency.Complete();

        ECB.Playback(state.EntityManager);
        ECB.Dispose();
    }
}


public partial struct GeneratePerlinBlocksJob : IJobEntity
{
    public int worldSeed;
    public float terrainRoughness;
    public float baseHeight;
    public float heightVariation;
    public int noiseLayers;

    public EntityCommandBuffer.ParallelWriter ECB;

    //does for each chunk
    public void Execute([EntityIndexInQuery] int index, in DOTS_Chunk chunk, ref DynamicBuffer<DOTS_Block> blocks,
        Entity entity)
    {
        DotsDebugLog($"Generating blocks for chunk at {chunk.ChunkCoord}");

        ECB.AddComponent<ChunkMeshPending>(index, entity);
        ECB.RemoveComponent<ChunkBlocksPending>(index, entity);


        int3 chunkCoord = chunk.ChunkCoord;
        InitializeBlocks(ref blocks);

// Heightmap for this chunk (X,Z plane)
        int[] heightmap = new int[CHUNK_SIZE * CHUNK_SIZE];
        for (int localX = 0; localX < CHUNK_SIZE; localX++)
        {
            for (int localZ = 0; localZ < CHUNK_SIZE; localZ++)
            {
                int worldX = chunkCoord.x * CHUNK_SIZE + localX;
                int worldZ = chunkCoord.z * CHUNK_SIZE + localZ;
                int height = (int)CalculateTerrainHeight(worldX, worldZ);
                heightmap[localX + localZ * CHUNK_SIZE] = height;
            }
        }

// Fill blocks using heightmap
        for (int localX = 0; localX < CHUNK_SIZE; localX++)
        for (int localZ = 0; localZ < CHUNK_SIZE; localZ++)
        {
            int height = heightmap[localX + localZ * CHUNK_SIZE];

            for (int localY = 0; localY < CHUNK_SIZE; localY++)
            {
                int worldY = chunkCoord.y * CHUNK_SIZE + localY;
                int blockIndex = localX + localY * CHUNK_SIZE + localZ * CHUNK_SIZE * CHUNK_SIZE;
                ref var block = ref blocks.ElementAt(blockIndex);

                if (worldY < height)
                {
                    if (worldY >= height - 1) block.Value = BlockType.Grass;
                    else if (worldY >= height - 4) block.Value = BlockType.Dirt;
                    else block.Value = BlockType.Stone;
                }
                else
                {
                    block.Value = BlockType.Air;
                }
            }
        }

        // makes the blocks on that x and z have different blocktypes.
        // var blockVarietyJob = new BlockVarietyJob();
    }


    private void InitializeBlocks(ref DynamicBuffer<DOTS_Block> blocks)
    {
        if (blocks.Length != CHUNK_VOLUME)
        {
            // If the buffer is not the correct size, we need to initialize it
            blocks.ResizeUninitialized(CHUNK_VOLUME);
        }

        // if(blocks[CHUNK_VOLUME - 1].Value != BlockType.Air)
        // return; // Already initialized, no need to go on
        blocks.Clear();
        for (int i = 0; i < CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE; i++)
        {
            blocks.Add(new DOTS_Block { Value = BlockType.Air });
        }
    }

    private float CalculateTerrainHeight(int worldX, int worldZ)
    {
        //stolen from chunk.cs
        float perlinScale = terrainRoughness;
        Vector2 perlinOffset = new Vector2(
            Mathf.Sin(worldSeed * 0.1f) * 1000f,
            Mathf.Cos(worldSeed * 0.1f) * 1000f
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