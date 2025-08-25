using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using System.Diagnostics;
using Unity.Collections;

namespace Project.Scripts.DOTS.Systems
{
    //     //RESULTS OF TEST -
    //     // 400ish ms for add 
    //     // 12 ms for set 
    // public partial struct TestECBAddVsSetSystem : ISystem
    // {
    //     
    //     public void OnUpdate(ref SystemState state)
    //     {
    //         var ecb= new EntityCommandBuffer(Allocator.Temp);   
    //         int number = 10000; // commands per entity
    //
    //         foreach (var (transform, chunkCoord, lastChunkPos, entity) in SystemAPI.Query<
    //                          RefRO<LocalTransform>,
    //                          RefRW<EntityChunkCoords>,
    //                          RefRW<LastChunkCoords>>()
    //                      .WithAll<PlayerTag>()
    //                      .WithEntityAccess())
    //         {
    //             // --- Enqueue AddComponent commands ---
    //             for (int i = 0; i < number; i++)
    //             {
    //                 ecb.AddComponent<SomeTestComponent>(entity);
    //                 ecb.RemoveComponent<SomeTestComponent>(entity);
    //             }
    //         }
    //
    //         var stopwatchAdd = Stopwatch.StartNew();
    //         ecb.Playback(state.EntityManager);
    //         stopwatchAdd.Stop();
    //         UnityEngine.Debug.Log(
    //             $"ECB AddComponent playback took {stopwatchAdd.ElapsedMilliseconds} ms for {number} adds");
    //         ecb.Dispose();
    //         foreach (var (transform, chunkCoord, lastChunkPos, entity) in SystemAPI.Query<
    //                          RefRO<LocalTransform>,
    //                          RefRW<EntityChunkCoords>,
    //                          RefRW<LastChunkCoords>>()
    //                      .WithAll<PlayerTag>()
    //                      .WithEntityAccess())
    //         {
    //             //
    //
    //             // --- Re-create ECB for SetComponent test ---
    //              ecb= new EntityCommandBuffer(Allocator.Temp);   
    //             // Make sure component exists for SetComponent
    //             if (!state.EntityManager.HasComponent<SomeTestComponent>(entity))
    //             {
    //                 ecb.AddComponent(entity, new SomeTestComponent { Value = 0 });
    //             }
    //
    //             // --- Enqueue SetComponent commands ---
    //             for (int i = 0; i < number; i++)
    //             {
    //                 ecb.SetComponent(entity, new SomeTestComponent { Value = i });
    //             }
    //         }
    //
    //         var stopwatchSet = Stopwatch.StartNew();
    //         ecb.Playback(state.EntityManager);
    //         ecb.Dispose();
    //         stopwatchSet.Stop();
    //         UnityEngine.Debug.Log(
    //             $"ECB SetComponent playback took {stopwatchSet.ElapsedMilliseconds} ms for {number} sets");
    //     }
    // }
    //
    // public struct SomeTestComponent : IComponentData
    // {
    //     public int Value;
    // }
}