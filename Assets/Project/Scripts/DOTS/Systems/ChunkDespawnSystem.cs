using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Project.Scripts.DOTS.Systems

{
    [UpdateAfter(typeof(ChunkSpawnSystem))]
    [UpdateBefore(typeof(MeshGenerationSystem))]
    public partial struct ChunkDespawnSystem: ISystem
    {
        public void OnUpdate(ref SystemState state)
        {

            //considering onyl one player, we will never render for our main player other players' chunks, it's stupid even if multiple players exist
            foreach (var (
                         settings,
                         chunkCoord,
                         loadedChunks) in
                     SystemAPI.Query<
                         RefRO<PlayerSettings>,
                         RefRO<EntityChunkCoords>,
                         DynamicBuffer<LoadedChunk>>())
            {
                if (!chunkCoord.ValueRO.OnChunkChange)
                {
                    return; // No chunk change, skip this update
                }
                var ecbSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
                var ecb = ecbSystem.CreateCommandBuffer();

                var r = settings.ValueRO.renderDistance;
                int total = (2 * r + 1) * (2 * r + 1) * (2 * r + 1);

                NativeArray<int3> offsets = new NativeArray<int3>(total, Allocator.TempJob);
                int idx = 0;
                for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                for (int z = -r; z <= r; z++)
                    offsets[idx++] = new int3(x, y, z);

                // Remove distant chunks
                var chunksToRemove = new NativeList<int3>(Allocator.TempJob);

// Collect chunk coords to remove
                for (int i = 0; i < loadedChunks.Length; i++)
                {
                    if (!offsets.Contains(loadedChunks[i].ChunkCoord))
                    {
                        chunksToRemove.Add(loadedChunks[i].ChunkCoord);
                    }
                }

// Remove from loadedChunks buffer by index, iterate backwards to avoid skipping
                for (int i = loadedChunks.Length - 1; i >= 0; i--)
                {
                    if (chunksToRemove.Contains(loadedChunks[i].ChunkCoord))
                    {
                        loadedChunks.RemoveAt(i);
                    }
                }
                // tells system to wait for jobs to finish before playing back
                ecbSystem.AddJobHandleForProducer(state.Dependency);
// Destroy chunk entities
                foreach (var (chunkData, chunkEntity) in SystemAPI.Query<DOTS_Chunk>().WithEntityAccess())
                {
                    if (chunksToRemove.Contains(chunkData.ChunkCoord))
                    {
                        ecb.DestroyEntity(chunkEntity);
                    }
                }

                //gets called automatically
                // ecb.Playback(state.EntityManager);

                chunksToRemove.Dispose();
                offsets.Dispose();
            }
        }
    }
}