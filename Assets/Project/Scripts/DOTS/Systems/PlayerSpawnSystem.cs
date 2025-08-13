using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Project.Scripts.DOTS.Other.DOTS_Utils;

namespace Project.Scripts.DOTS.Systems
{
    [RequireMatchingQueriesForUpdate]
    [UpdateBefore(typeof(ChunkSpawnSystem))]
    public partial struct PlayerSpawnInitSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();

            Queue<Entity> playersToRemoveTag = new Queue<Entity>();

            foreach (var (transform, chunkCoord, lastChunkCoord, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, RefRW<EntityChunkCoords>, RefRW<LastChunkCoords>>()
                         .WithAll<NewlySpawnedPlayerTag>()
                         .WithEntityAccess())
            {
                Debug.Log("PlayerSpawnInitSystem: player spawned at " + WorldPosToChunkCoord(transform.ValueRO.Position));
                int3 startChunk = WorldPosToChunkCoord(transform.ValueRO.Position);

                chunkCoord.ValueRW.newChunkCoords = startChunk;
                chunkCoord.ValueRW.OnChunkChange = true;
                lastChunkCoord.ValueRW.Value = startChunk;

                playersToRemoveTag.Enqueue(entity);
            }

            while (playersToRemoveTag.Count > 0)
            {
                Entity playerEntity = playersToRemoveTag.Dequeue();
                ecb.RemoveComponent<NewlySpawnedPlayerTag>(playerEntity);
            }

            ecbSystem.AddJobHandleForProducer(state.Dependency);
        }
    }
}