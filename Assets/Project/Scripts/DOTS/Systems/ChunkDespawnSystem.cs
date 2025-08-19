using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Project.Scripts.DOTS.Systems

{
    [UpdateAfter(typeof(ChunkSpawnSystem))]
    [UpdateBefore(typeof(MeshGenerationSystem))]
    public partial struct ChunkDespawnSystem : ISystem
    {
        private NativeList<int3> ChunksToDespawn;


        public void OnCreate(ref SystemState state)
        {
            ChunksToDespawn = new NativeList<int3>(Allocator.Persistent);
            // ECB = new EntityCommandBuffer(Allocator.Persistent);
            // state.RequireForUpdate<EndSimulationEntityCommandBufferSystem>();
            // state.RequireForUpdate<PlayerSettings>();
            // state.RequireForUpdate<DOTS_Chunk>();
        }

        public void OnUpdate(ref SystemState state)
        {
            
            //considering onyl one player, we will never render for our main player other players' chunks, it's stupid even if multiple players exist
            foreach (var (
                         settings,
                         PlayerChunkCoord,
                         loadedChunks) in
                     SystemAPI.Query<
                         RefRO<PlayerSettings>,
                         RefRO<EntityChunkCoords>,
                         DynamicBuffer<PlayerLoadedChunk>>())
            {
                var rendDist = settings.ValueRO.renderDistance;
                
                for (int i = 0; i < loadedChunks.Length; i++)
                {
                    int3 chunkCoord = loadedChunks[i].ChunkCoord;
                    int3 delta = chunkCoord - PlayerChunkCoord.ValueRO.newChunkCoords;

                    if (math.abs(delta.x) > rendDist || math.abs(delta.y) > rendDist ||
                        math.abs(delta.z) > rendDist)
                    {
                        if (!ChunksToDespawn.Contains(chunkCoord))
                        {
                            ChunksToDespawn.Add(chunkCoord);
                            Debug.Log("Chunk to despawn added to out of chunk coords: " + chunkCoord);
                        }
                    }
                }


                var ecbSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
                var ecb = ecbSystem.CreateCommandBuffer();

                // RemoveChunksOldVer(ref state, loadedChunks, offsets, ecbSystem, ecb);

                //remove chunks new ver:
                // pop a few chunks from the despawn queue, 
                // remove them from  loaded chunks
                // destroy their entities


                //maybe making it a jobi s not worth it since the only thing
                //we really do is destroy entities which might get done at the end of the frame automatically in ECB anyway
                int ChunksToProcessPerFrame = 2;
                for (int i = 0; i < ChunksToProcessPerFrame && !ChunksToDespawn.IsEmpty; i++)
                {
                    var despawnChunkCoord = ChunksToDespawn[0];
                    ChunksToDespawn.RemoveAt(0);

                    // Remove from loadedChunks buffer by index, iterate backwards to avoid skipping
                    for (int j = loadedChunks.Length - 1; j >= 0; j--)
                    {
                        if (loadedChunks[j].ChunkCoord.Equals(despawnChunkCoord))
                        {
                            var entityToDestroy = loadedChunks[j].ChunkEntity;
                            loadedChunks.RemoveAt(j);
                            ecb.DestroyEntity(entityToDestroy);
                            // Debug.Log("Destroyed chunk at: " + despawnChunkCoord);
                            break; // we found and removed the chunk, no need to continue
                        }
                    }
                }


                //gets called automatically
                // ecb.Playback(state.EntityManager);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            if (ChunksToDespawn.IsCreated)
            {
                ChunksToDespawn.Dispose();
            }
            // if (ECB.IsCreated)
            // {
            //     ECB.Dispose();
            // }
        }

        private void RemoveChunksOldVer(ref SystemState state, DynamicBuffer<PlayerLoadedChunk> loadedChunks,
            NativeArray<int3> offsets,
            EndSimulationEntityCommandBufferSystem ecbSystem, EntityCommandBuffer ecb)
        {
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

            chunksToRemove.Dispose();
            return;
        }

        private static NativeArray<int3> getLoadedChunkOffsets(int total, int r)
        {
            NativeArray<int3> offsets = new NativeArray<int3>(total, Allocator.TempJob);
            int idx = 0;
            for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
            for (int z = -r; z <= r; z++)
                offsets[idx++] = new int3(x, y, z);
            return offsets;
        }
    }
}