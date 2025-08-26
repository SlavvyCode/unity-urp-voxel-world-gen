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
    
    public BufferLookup<DOTS_Block> blockLookup;
    public ComponentLookup<DOTS_Chunk> chunkLookup;
    private NativeList<Entity> desiredChunks;
    public void OnCreate(ref SystemState state)
    { 
        desiredChunks = new NativeList<Entity>(Allocator.Persistent);
        chunkLookup = SystemAPI.GetComponentLookup<DOTS_Chunk>(true);
        blockLookup = SystemAPI.GetBufferLookup<DOTS_Block>(false);
    }

    public void OnDestroy(ref SystemState state)
    {
        desiredChunks.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var allChunks = SystemAPI.QueryBuilder()
            .WithAny<DOTS_ChunkState>().Build().ToEntityArray(Allocator.Temp);

        desiredChunks.Clear();
        var chunkStates = SystemAPI.GetComponentLookup<DOTS_ChunkState>(true); // read-only

        for (int i = 0; i < allChunks.Length; i++)
        {
            var entity = allChunks[i];
            if (chunkStates[entity].Value == ChunkStateEnum.BlockGenPending)
            {
                desiredChunks.Add(entity);
            }
        }
        
        
        
        if (desiredChunks.Length == 0)
        {
            // Debug.Log("No chunks need BLOCK generation");
            return;
        }
        
        chunkLookup.Update(ref state);
        blockLookup.Update(ref state);
        // Debug.Log($"Found {desiredChunks.Length} chunks that need BLOCK generation");



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
                ECB = ECB.AsParallelWriter(),
                desiredChunks = desiredChunks,
                blockLookup = blockLookup,
                chunkLookup = chunkLookup,
            }
            .ScheduleParallel(desiredChunks.Length,1, state.Dependency);

        state.Dependency.Complete();

        ECB.Playback(state.EntityManager);
        ECB.Dispose();
    }
}


public partial struct GeneratePerlinBlocksJob : IJobFor
{
    public int worldSeed;
    public float terrainRoughness;
    public float baseHeight;
    public float heightVariation;
    public int noiseLayers;

    public EntityCommandBuffer.ParallelWriter ECB;
    [ReadOnly] public NativeList<Entity> desiredChunks;
    [NativeDisableParallelForRestriction] public BufferLookup<DOTS_Block> blockLookup;
    [ReadOnly] public ComponentLookup<DOTS_Chunk> chunkLookup;
    public void Execute(int index)
    {

        var entity = desiredChunks[index];
        var chunk = chunkLookup[entity];
        // DotsDebugLog($"Generating blocks for chunk at {chunk.ChunkCoord}");
        var blocks = blockLookup[entity];
        int3 chunkCoord = chunk.ChunkCoord;
        InitializeBlocks(ref blocks);
        
        ECB.SetComponent(index,entity,new DOTS_ChunkState { Value =ChunkStateEnum.MeshPending});

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

