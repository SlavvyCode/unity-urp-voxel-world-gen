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

    // [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (transform, 
                     chunkCoord, 
                     lastChunkPos, entity) 
                 in
                 SystemAPI.Query<
                     RefRO<LocalTransform>,
                     RefRW<EntityChunkCoords>,
                     RefRW<LastChunkCoords>>()
                     .WithAll<PlayerTag>()
                     .WithNone<NewlySpawnedPlayerTag>()
                     .WithEntityAccess())
        {
            int3 newChunk = WorldPosToChunkCoord(transform.ValueRO.Position);
            
            if (!newChunk.Equals(lastChunkPos.ValueRO.Value))
            {
                Debug.Log("Chunk changed from " + lastChunkPos.ValueRO.Value + " to " + newChunk);
                chunkCoord.ValueRW.newChunkCoords = newChunk;
                chunkCoord.ValueRW.OnChunkChange = true;
                lastChunkPos.ValueRW.Value = newChunk;
            }
        }
    }
}