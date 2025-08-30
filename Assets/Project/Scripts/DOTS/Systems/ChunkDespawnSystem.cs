using Project.Scripts.DOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Project.Scripts.DOTS.Systems

{
    [UpdateAfter(typeof(ChunkSpawnSystem))]
    [UpdateBefore(typeof(MeshGenerationSystem))]
    public partial struct ChunkDespawnMarkerSystem : ISystem
    {
        private ComponentLookup<DOTS_ChunkState> chunkStateLookup;

        public void OnCreate(ref SystemState state)
        {
            chunkStateLookup = SystemAPI.GetComponentLookup<DOTS_ChunkState>(false);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            chunkStateLookup.Update(ref state);
            //considering onyl one player, we will never render for our main player other players' chunks, it's stupid even if multiple players exist
            foreach (var (
                         settings,
                         PlayerChunkCoord) in
                     SystemAPI.Query<
                         RefRO<PlayerSettings>,
                         RefRO<EntityChunkCoords>>())
            {
                var rendDist = settings.ValueRO.renderDistance;
                foreach (var (chunk, chunkState, entity) in SystemAPI.Query<DOTS_Chunk, DOTS_ChunkState>()
                             .WithEntityAccess())
                {
                    int3 chunkCoord = chunk.ChunkCoord;
                    int3 delta = chunkCoord - PlayerChunkCoord.ValueRO.newChunkCoords;


                    if (math.abs(delta.x) > rendDist || math.abs(delta.y) > rendDist ||
                        math.abs(delta.z) > rendDist)
                    {
                        // todo give the chunk a new state in the chunkStateEnum
                        var chunkChunkState = chunkStateLookup[entity];
                        // STRUCT, NEED TO WRITE BACK
                        chunkChunkState.Value = ChunkStateEnum.DespawnQueued;
                        chunkStateLookup[entity] = chunkChunkState; 
                    }
                }
            }
        }
    }

    [UpdateAfter(typeof(ChunkDespawnMarkerSystem))]
    public partial struct ChunkDespawnSystem : ISystem
    {
        private ComponentLookup<DOTS_ChunkState> chunkStateLookup;

        public void OnCreate(ref SystemState state)
        {
            chunkStateLookup = SystemAPI.GetComponentLookup<DOTS_ChunkState>(false);
        }
        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();


            chunkStateLookup.Update(ref state);
            //considering onyl one player, we will never render for our main player other players' chunks, it's stupid even if multiple players exist
            foreach (var (
                         settings,
                         PlayerChunkCoord) in
                     SystemAPI.Query<
                         RefRO<PlayerSettings>,
                         RefRO<EntityChunkCoords>>())
            {
                foreach (var (chunkState, entity) in SystemAPI.Query<DOTS_ChunkState>()
                             .WithEntityAccess())
                {
                    if (chunkState.Value == ChunkStateEnum.DespawnQueued)
                    {
                        ecb.DestroyEntity(entity);
                    }
                }
            }
        }
    }
}