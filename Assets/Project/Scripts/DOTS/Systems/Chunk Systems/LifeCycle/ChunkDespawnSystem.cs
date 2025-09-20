using Project.Scripts.DOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
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
        private ComponentLookup<DOTS_ChunkRenderData> chunkRenderDataLookup;
        
        private Entity poolEntity;
        public void OnCreate(ref SystemState state)
        {
            chunkStateLookup = SystemAPI.GetComponentLookup<DOTS_ChunkState>(false);
            chunkRenderDataLookup = SystemAPI.GetComponentLookup<DOTS_ChunkRenderData>(false);
            poolEntity= Entity.Null;
        }
        public void OnUpdate(ref SystemState state)
        {
            if (poolEntity == Entity.Null)
            {
                if (!SystemAPI.TryGetSingletonEntity<ChunkPool>(out poolEntity)) return;
            }
            var poolBuffer = SystemAPI.GetBuffer<ChunkPoolElement>(poolEntity);
            var poolData = SystemAPI.GetComponent<ChunkPool>(poolEntity);
            int returned = 0;
            
            chunkStateLookup.Update(ref state);
            chunkRenderDataLookup.Update(ref state);


            var ecbSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();
            var entityManager = state.EntityManager;

            
            foreach (var (chunkState, entity) in SystemAPI.Query<DOTS_ChunkState>().WithEntityAccess())
            {
                if (chunkState.Value == ChunkStateEnum.DespawnQueued)
                {
                    // todo reset mesh data etc
                    // MeshFilter filter = ecb.SetEnabled(request.MeshEntity)<MeshFilter>;
                    // MeshRenderer renderer = entityManager.GetComponentObject<MeshRenderer>(request.MeshEntity);
                    // var renderData = SystemAPI.GetComponent<DOTS_ChunkRenderData>(entity);
                    // var filter = entityManager.GetComponentObject<MeshFilter>(renderData.MeshEntity);
                    // var renderer = entityManager.GetComponentObject<MeshRenderer>(renderData.MeshEntity);
                    //
                    // var mesh = filter.mesh;
                    // mesh.bounds = new Bounds(Vector3.zero, Vector3.zero);
                    // filter.mesh = null;
                    // renderer.enabled = false;
                    // ecb.SetComponent(renderData.MeshEntity, new RenderBounds {
                    //     Value = new AABB { Center = float3.zero, Extents = float3.zero }
                    // });
                    // ecb.SetComponentEnabled<MaterialMeshInfo>(renderData.MeshEntity, false);
                    // ecb.SetComponent(renderData.MeshEntity, new RenderBounds {
                    //     Value = new AABB { Center = float3.zero, Extents = float3.zero }
                    // });
                    //
                    // Reset mesh data if it exists
                    if (chunkRenderDataLookup.HasComponent(entity))
                    {
                        var renderData = chunkRenderDataLookup[entity];
                    
                        // Disable the renderer to make the chunk invisible
                        ecb.SetComponentEnabled<MaterialMeshInfo>(renderData.MeshEntity, false);
                    
                        // Reset the render bounds to zero
                        ecb.SetComponent(renderData.MeshEntity, new RenderBounds 
                        { 
                            Value = new AABB { Center = float3.zero, Extents = float3.zero } 
                        });
                    
                        // Also reset the LocalTransform to zero position
                        ecb.SetComponent(renderData.MeshEntity, new LocalTransform 
                        { 
                            Position = float3.zero,
                            Rotation = quaternion.identity,
                            Scale = 1f
                        });
                    }

                    
                    // mark chunk as pooled and return to buffer immediately
                    ecb.SetComponent(entity, new DOTS_ChunkState { Value = ChunkStateEnum.Pooled });
                    poolBuffer.Add(new ChunkPoolElement { Value = entity });
                    returned++;
                }
            }

            // update active count in pool
            if (returned > 0)
            {
                poolData.ActiveCount = math.max(0, poolData.ActiveCount - returned);
                state.EntityManager.SetComponentData(poolEntity, poolData);
            }

            //considering onyl one player, we will never render for our main player other players' chunks, it's stupid even if multiple players exist
            // OldChunkDestruction(ecb);
        }

        private void OldChunkDestruction(ref SystemState state,EntityCommandBuffer ecb)
        {
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