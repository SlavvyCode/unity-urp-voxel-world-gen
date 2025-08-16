using Project.Scripts.DOTS.Other;
using Project.Scripts.DOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Project.Scripts.DOTS.Other.DOTS_Utils;

[BurstCompile]
[UpdateAfter(typeof(ChunkDespawnSystem))]
[UpdateBefore(typeof(MeshGenerationSystem))]
public partial struct ChunkBlockGenerationSystem : ISystem
{
    // public void OnCreate(ref SystemState state) 

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


        var query = SystemAPI.QueryBuilder()
            .WithAll<DOTS_Chunk,ChunkBlocksPending,DOTS_Block>() 
            .Build();
        
        var ECB = new EntityCommandBuffer(Allocator.TempJob);
        state.Dependency = new ChunkBlockGenerationJob
            {
                ECB = ECB.AsParallelWriter()
            }
            .ScheduleParallel(query,state.Dependency);

        state.Dependency.Complete();
        
        ECB.Playback(state.EntityManager);
        ECB.Dispose();
    }
}


public partial struct ChunkBlockGenerationJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;
    void Execute([EntityIndexInQuery] int sortKey,Entity entity, DynamicBuffer<DOTS_Block> blocks,
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
        ECB.AddComponent<ChunkMeshPending>(sortKey,entity);
        ECB.RemoveComponent<ChunkBlocksPending>(sortKey, entity);

        // DotsDebugLog($"Added {blockCount} blocks to chunk at {chunk.ChunkCoord}");
    }
}