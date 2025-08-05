using System.Collections;
using System.Collections.Generic;
using Project.Scripts.DOTS.Other;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Project.Scripts.DOTS.Other.DOTS_Utils;
public partial struct EntityChunkPositionTrackerSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (transform, chunkPos, lastChunkPos,entity) in
                 //todo make sure local transform actualy gets gotten
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<ContainerChunkCoords>, RefRW<LastChunkCoords>>().WithAll<PlayerTag>().WithEntityAccess())
        {
            int3 newChunk = GetChunkCoord(transform.ValueRO.Position);
            
            if (!newChunk.Equals(lastChunkPos.ValueRO.Value))
            {
                chunkPos.ValueRW.Value = newChunk;
                lastChunkPos.ValueRW.Value = newChunk;
                
                // Debug.Log("Player newchunk = " + newChunk);
                // Optional: Add event component if needed
                // ecb.AddComponent<ChunkPositionChanged>(entity);
            }
        }
    }



}
