// using System;
// using Project.Scripts.DOTS.Other;
// using Project.Scripts.DOTS.Systems;
// using Unity.Burst;
// using Unity.Collections;
// using Unity.Entities;
// using Unity.Jobs;
// using Unity.Mathematics;
// using Unity.Transforms;
// using UnityEngine;
// using static Project.Scripts.DOTS.Other.DOTS_Utils;
//
//
//
// [BurstCompile]
// [UpdateAfter(typeof(ChunkDespawnMarkerSystem))]
// [UpdateBefore(typeof(MeshGenerationSystem))]
// public partial struct ChunkBlockGenerationSystem : ISystem
// {
//     private ComponentLookup<DOTS_Chunk> chunksLookup;
//
//     // private ComponentLookup<DOTS_ChunkState> chunkStateLookup;
//     private BufferLookup<DOTS_Block> blocksLookup;
//
//
//     public struct WorldHeightmapWindow : IComponentData
//     {
//         public int2 CenterPosition; // World XZ of window center = player position floored to chunk coords
//         public int2 WindowSize;    
//         public NativeArray<int> blockColHeights; // Flat array for the window
//         // public NativeArray<int>  ; // Flat array for the window
//     }
//     // todo
//     // maybe we only need to generate blocks:
//     // - for chunks that are within a certain distance of the player (e.g., some buffer) - eg. a creeper coming up behind you and exploding - for simulations
//     // - chunks that are within our FOV
//
//     // IMPORTANT NOTE!
//     // CANNOT BE REFACTORED
//     
//     // TODO CONCRETE PLAN TO SPEED UP
//     // TODO
//     // 1. flatten hashmap;
//     // move jobs into individual systems
//     // use shader;
//     // use sorting of chunks (=regions).- seems very insignificant.
//
//     //ask deepseek deep think mode for acctual architectural improvements it blew my mind with the suggestions 2,3,4
//     
//     
//    // TODO NEW PLAN about HOW to flatten the hashmap; make a sliding render distance wide and tall 2D array window.
//    // TODO ... in this window, we can recalculate chunks easily and their positions etc etc isntead of finding or passing them hardly or whatever
//     
//     
//     
//     // todo cache heightmaps/generated terrain(blocks included)
//     // for pillars so we dont have to recalculate them every time a chunk is generated in that pillar
//     private NativeList<Entity> desiredChunks;
//
//     // private NativeList<int2> chunkYColumnCoords;
//     // private NativeParallelHashMap<int2, int> blockColumnCoordsToHeightHashMap;
//     private EntityQuery allChunksQuery;
//     private float2 perlinOffset;
//     private NativeList<int2> uniqueChunkXZCoords;
//
//
//     private NativeArray<int> blockHeightsWindow; 
//     
//     // todo Chunk pooling
//     // - Reuse DOTS_Block buffers for chunks that leave the radius instead of reallocating
//     public void OnCreate(ref SystemState state)
//     {
//         
//         
//         
//         chunksLookup = SystemAPI.GetComponentLookup<DOTS_Chunk>(true);
//         blocksLookup = SystemAPI.GetBufferLookup<DOTS_Block>(false);
//         // chunkStateLookup = SystemAPI.GetComponentLookup<DOTS_ChunkState>(false);
//
//         allChunksQuery = SystemAPI.QueryBuilder()
//             .WithAny<DOTS_ChunkState>()
//             .Build();
//
//
//         desiredChunks = new NativeList<Entity>(Allocator.Persistent);
//
//         perlinOffset = float2.zero;
//     }
//
//     public void OnDestroy(ref SystemState state)
//     {
//         //can not be disposed i think
//         // allChunksQuery.Dispose();
//         // if (blockColumnCoordsToHeightHashMap.IsCreated)
//             // blockColumnCoordsToHeightHashMap.Dispose();
//         desiredChunks.Dispose();
//
//         if (!uniqueChunkXZCoords.IsCreated)
//             uniqueChunkXZCoords.Dispose();
//         // chunkYColumnCoords.Dispose();
//         
//         if (blockHeightsWindow.IsCreated)
//             blockHeightsWindow.Dispose();
//         
//         
//         
//     }
//
//     public void OnUpdate(ref SystemState state)
//     {
//     // #region init uninteresting vars
//     //
//     //     desiredChunks.Clear();
//     //
//     //     var allChunks =
//     //         allChunksQuery.ToEntityArray(Allocator
//     //             .Temp); //remove from desired chunks entities which have chunkstate of different kind than ready forblockgeneration
//     //     var chunkStates = SystemAPI.GetComponentLookup<DOTS_ChunkState>(true); // read-only
//     //
//     //     for (int i = 0; i < allChunks.Length; i++)
//     //     {
//     //         var entity = allChunks[i];
//     //         if (chunkStates[entity].Value == ChunkStateEnum.BlockGenPending)
//     //             if (chunkStates[entity].Value == ChunkStateEnum.BlockGenPending)
//     //             {
//     //                 desiredChunks.Add(entity);
//     //             }
//     //     }
//     //
//     //
//     //     if (desiredChunks.Length == 0)
//     //         return;
//     //
//     //     chunksLookup.Update(ref state);
//     //     blocksLookup.Update(ref state);
//     //
//     //     // Debug.Log($"Found {desiredChunks.Length} chunks that need BLOCK generation");
//     //     var worldQuery = SystemAPI.QueryBuilder()
//     //         .WithAll<WorldParams>()
//     //         .Build();
//     //
//     //     var worldParams = worldQuery.GetSingleton<WorldParams>();
//     //
//     //     var ECBSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
//     //     var ecb = ECBSystem.CreateCommandBuffer();
//     //     // var ecbParallelWriter = ecb.AsParallelWriter();
//     //
//     //
//     //     if (!uniqueChunkXZCoords.IsCreated)
//     //         uniqueChunkXZCoords = new NativeList<int2>(Allocator.Persistent);
//     //     else
//     //         uniqueChunkXZCoords.Clear();
//     //     // 1. get unique xz block coordinates (x,z) to pass to the heightmap job?
//     //     foreach (var chunkEntity in desiredChunks)
//     //     {
//     //         var chunk = chunksLookup[chunkEntity];
//     //         int3 chunkCoord = chunk.ChunkCoord;
//     //         // int startIndex = chunkYColumnCoords.Length; // index of first pillar for this chunk
//     //         int2 chunkColumnXZCoord = new int2(chunkCoord.x * CHUNK_SIZE, chunkCoord.z * CHUNK_SIZE);
//     //         //todo is it okay to just put a temp value? it's certainly faster than making a new variable array and then disposing of  it
//     //         // we can just change the keys later right?
//     //         uniqueChunkXZCoords.Add(chunkColumnXZCoord);
//     //     }
//     //
//     //    
//     //     #endregion
//     //
//     //
//     //     //TODO PLANNING!
//     //     //todo create a sliding window in array form. of heights. we should be able to calculate the index based on the coords in the window.
//     //
//     //     int renderDistance = 0;
//     //     int2 playerChunkCoordXZ = new int2(0, 0);
//     //     foreach (var (settings,playerChunkCoords,tag) in SystemAPI.Query<RefRO<PlayerSettings>,RefRO<EntityChunkCoords>, RefRO<PlayerTag>>())
//     //     {
//     //         renderDistance = settings.ValueRO.renderDistance;
//     //         playerChunkCoordXZ = playerChunkCoords.ValueRO.newChunkCoords.xz;
//     //     }
//     //
//     //     // windows are 2D, centered on the player.
//     //     int windowEdgeChunkLength = (2 * renderDistance)+1;
//     //     int windowEdgeBlockLength = windowEdgeChunkLength * CHUNK_SIZE;
//     //     
//     //     int windowChunkLength = windowEdgeChunkLength * windowEdgeChunkLength;
//     //     int windowBlockLength = windowEdgeBlockLength * windowEdgeBlockLength;
//     //     
//     //     
//     //     if (!blockHeightsWindow.IsCreated || windowBlockLength != blockHeightsWindow.Length)
//     //     {
//     //         if (blockHeightsWindow.IsCreated) blockHeightsWindow.Dispose();
//     //         blockHeightsWindow = new NativeArray<int>(windowBlockLength, Allocator.Persistent);
//     //     }
//     //    
//     //     
//     //
//     //     if (math.all(perlinOffset == float2.zero))
//     //     {
//     //         perlinOffset = new float2(
//     //             math.sin(worldParams.worldSeed * 0.1f) * 1000f,
//     //             math.cos(worldParams.worldSeed * 0.1f) * 1000f
//     //         );
//     //     }
//     //     //2. do heightmap job
//     //     
//     //     
//     //     // think: "WHAT DO I NEED TO KNOW TO GENERATE HEIGHT MAP FOR ANY GIVEN BLOCK COLUMN"
//     //     var heightMapForBlockColumnsJob = new HeightMapForBlockColumnsJob
//     //     {
//     //         #region worldparams
//     //         perlinOffset = perlinOffset,
//     //         terrainRoughness = worldParams.terrainRoughness,
//     //         baseHeight = worldParams.baseHeight,
//     //         heightVariation = worldParams.heightVariation,
//     //         noiseLayers = worldParams.noiseLayers,
//     //         #endregion
//     //         chunkXZCoords = uniqueChunkXZCoords,
//     //         // coordsToHeightsHashMap = blockColumnCoordsToHeightHashMap.AsParallelWriter()
//     //         blockHeightsWindow = blockHeightsWindow,
//     //         renderDistance = renderDistance,
//     //         windowEdgeBlockLength = windowEdgeBlockLength
//     //     };
//
//         //3 do generation job 
//         // todo WHAT DO I NEED TO HAVE ASSIGN HEIGHTMAPS TO EACH CHUNK
//         // heightmap or heights
//         // each chunk 
//         var generateChunkBlocksJob = new GenerateChunkBlocksJob
//         {
//             chunksLookup = chunksLookup,
//             blocksLookup = blocksLookup,
//             ecb = ecb.AsParallelWriter(),
//
//             desiredChunks = desiredChunks,
//             blockHeightsWindow = blockHeightsWindow,
//             // renderDistance = renderDistance,
//             windowEdgeBlockLength = windowEdgeBlockLength, 
//             playerChunkCoordXZ= playerChunkCoordXZ,
//         };
//
//         var handle1 = heightMapForBlockColumnsJob.ScheduleParallel(uniqueChunkXZCoords.Length, 1, state.Dependency);
//         var handle2 = generateChunkBlocksJob.ScheduleParallel(desiredChunks.Length, 1, handle1);
//
//         state.Dependency = handle2;
//         state.Dependency.Complete();
//         // ECB.Playback(state.EntityManager);
//         // ECB.Dispose();
//     }
//
// }
//
//
//
//
//
//
