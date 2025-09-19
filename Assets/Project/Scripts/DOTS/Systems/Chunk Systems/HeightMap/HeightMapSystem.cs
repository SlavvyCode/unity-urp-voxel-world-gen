using System;
using Project.Scripts.DOTS.Other;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Project.Scripts.DOTS.Other.DOTS_Utils;

namespace Project.Scripts.DOTS.Systems
{



    [UpdateAfter(typeof(ChunkDespawnMarkerSystem))]
    public partial struct HeightMapSystem : ISystem
    {
        private ComponentLookup<DOTS_Chunk> chunksLookup;

        // private ComponentLookup<DOTS_ChunkState> chunkStateLookup;
        private BufferLookup<DOTS_Block> blocksLookup;
        private NativeList<int2> newChunkColumnsList;

        // todo
        // maybe we only need to generate blocks:
        // - for chunks that are within a certain distance of the player (e.g., some buffer) - eg. a creeper coming up behind you and exploding - for simulations
        // - chunks that are within our FOV

        // IMPORTANT NOTE!
        // CANNOT BE REFACTORED

        // TODO CONCRETE PLAN TO SPEED UP
        // TODO
        // 1. flatten hashmap;
        // move jobs into individual systems
        // use shader;
        // use sorting of chunks (=regions).- seems very insignificant.
        //ask deepseek deep think mode for acctual architectural improvements it blew my mind with the suggestions 2,3,4
        // TODO NEW PLAN about HOW to flatten the hashmap; make a sliding render distance wide and tall 2D array window.
        // TODO ... in this window, we can recalculate chunks easily and their positions etc etc isntead of finding or passing them hardly or whatever
        // todo cache heightmaps/generated terrain(blocks included)
        private NativeList<Entity> desiredChunks;
        private EntityQuery allChunksQuery;

        private float2 perlinOffset;

        // private NativeArray<int> blockHeightsWindow; 
        private int windowEdgeChunkLength;
        private int windowEdgeBlockLength;
        private int windowBlockLength;
        private int renderDistance;
        private WorldParams worldParams;
        private int2 playerChunkCoordXZ;
        public NativeArray<int> blockHeightsWindow;
        public JobHandle heightMapJobHandle;

        public int2
            lastWindowCenterChunkCoordXZ; // chunk coord of window center (player position floored to chunk coords)

        private bool heightMapReady;
        private bool firstRun;
        public void OnCreate(ref SystemState state)
        {
            firstRun = true;
            chunksLookup = SystemAPI.GetComponentLookup<DOTS_Chunk>(true);
            blocksLookup = SystemAPI.GetBufferLookup<DOTS_Block>(false);
            // chunkStateLookup = SystemAPI.GetComponentLookup<DOTS_ChunkState>(false);

            allChunksQuery = SystemAPI.QueryBuilder()
                .WithAny<DOTS_ChunkState>()
                .Build();


            desiredChunks = new NativeList<Entity>(Allocator.Persistent);

            perlinOffset = float2.zero;

            newChunkColumnsList = new NativeList<int2>(Allocator.Persistent);
            
            heightMapReady = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            desiredChunks.Dispose();
            if (blockHeightsWindow.IsCreated) blockHeightsWindow.Dispose();
            newChunkColumnsList.Dispose();
        }


        public void OnUpdate(ref SystemState state)
        {
            // only update on chunk crossing, since window only moves then.
            InitDesiredChunks(ref state);
            var hasDesiredChunks = desiredChunks.Length > 0;

            playerChunkCoordXZ = GetPlayerPositionAndRenderDistance(ref state);
            var playerChangedChunks = PlayerChangedChunks(ref state);

            // DEPENDS ON RENDER DISTANCE, WATCH OUT FOR THE ORDER
            InitWindowData();
            chunksLookup.Update(ref state);
            blocksLookup.Update(ref state);
            
            // make sure to check if the heightmap job is done AND that it's already been started
            if (heightMapJobHandle.IsCompleted && !heightMapJobHandle.Equals(default(JobHandle)))
            {
                heightMapJobHandle = default;
                heightMapReady = true;
                // Debug.Log("Heightmap job completed, heightmap is ready.");
            }

            
            if (playerChangedChunks  )
            {
                #region init uninteresting vars


                GetWorldParams(ref state);
                // Debug.Log($"Found {desiredChunks.Length} chunks that need BLOCK generation");

                #endregion


                int2 oldCenter = lastWindowCenterChunkCoordXZ; // store the previous
                int2 newCenter = playerChunkCoordXZ; // get current

                // Update the heightmap window if the player has moved to a new chunk
                MoveWindow(newCenter, oldCenter);

                GetNewChunkColumnCoords(oldCenter, newCenter, CHUNK_SIZE);

                // Calculate the center of the window in block coordinates
                // We use a half-block offset to handle the even chunk size later on
                int2 centerBlockOfChunkPos = new int2(
                    playerChunkCoordXZ.x * CHUNK_SIZE + CHUNK_SIZE / 2,
                    playerChunkCoordXZ.y * CHUNK_SIZE + CHUNK_SIZE / 2
                );

                ScheduleHeightMapJob(ref state, centerBlockOfChunkPos);
                heightMapReady = false; // Heightmap is now being regenerated
        
                // Don't schedule block generation in the same frame
                return;
            }
     
            // BUG! we don't check if the heightmap is ready before generating blocks
            // used to be solved by job dependencies
            if (hasDesiredChunks && heightMapReady)
                ScheduleBlockGenerationJob(ref state);

            
        }
    
        private void ScheduleBlockGenerationJob(ref SystemState state)
        {
            if (!blockHeightsWindow.IsCreated || blockHeightsWindow.Length == 0)
                return;
        
            // make a check if all the blocks in the window are 0s - garbage data, even if it waits for the heightmap job handle
            // bool allZero = true;
            // for (int i = 0; i < blockHeightsWindow.Length; i++)
            // {
            //     if (blockHeightsWindow[i] != 0)
            //     {
            //         allZero = false;
            //         break;
            //     }
            // }
            // if (allZero)
            //     throw new Exception("Block heights window is uninitialized (all zeros). Heightmap job may not have run.");
            
            // Get player position for block generation
        
            var ECBSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var ecb = ECBSystem.CreateCommandBuffer();
        
            var generateChunkBlocksJob = new GenerateChunkBlocksJob
            {
                chunksLookup = chunksLookup,
                blocksLookup = blocksLookup,
                ecb = ecb.AsParallelWriter(),
                desiredChunks = desiredChunks,
                blockHeightsWindow = blockHeightsWindow,
                windowEdgeBlockLength = windowEdgeBlockLength, 
                playerChunkCoordXZ = playerChunkCoordXZ,
            };
     
            // Check if we have a valid heightmap job handle
            // If we updated the heightmap this frame, use that job handle
            // Otherwise, just use the current state dependency
            JobHandle dependency = state.Dependency;
            if (!heightMapJobHandle.Equals(default(JobHandle)))
            {
                dependency = JobHandle.CombineDependencies(dependency, heightMapJobHandle);
            }
            var generateHandle = generateChunkBlocksJob.ScheduleParallel(
                desiredChunks.Length, 1, 
                dependency);
            
            state.Dependency = generateHandle;
        }
        
        
        private void GetWorldParams(ref SystemState state)
        {
            if (math.all(perlinOffset == float2.zero))
                perlinOffset = GetPerlinOffset(worldParams);

            if (worldParams.Equals(default(WorldParams)))
            {
                var worldQuery = SystemAPI.QueryBuilder()
                    .WithAll<WorldParams>()
                    .Build();
                worldParams = worldQuery.GetSingleton<WorldParams>();
            }
        }

        private int2 GetPlayerPositionAndRenderDistance(ref SystemState state)
        {
            renderDistance = 0;
            int2 playerChunkCoordXZ = new int2(0, 0);
            foreach (var (settings, playerChunkCoords, tag) in SystemAPI
                         .Query<RefRO<PlayerSettings>, RefRO<EntityChunkCoords>, RefRO<PlayerTag>>())
            {
                renderDistance = settings.ValueRO.renderDistance;
                playerChunkCoordXZ = playerChunkCoords.ValueRO.newChunkCoords.xz;
            }

            return (playerChunkCoordXZ);
        }

        private void ScheduleHeightMapJob(ref SystemState state, int2 centerBlockCoord)
        {
            var heightMapForBlockColumnsJob = new HeightMapForBlockColumnsJob
            {
                perlinOffset = perlinOffset,
                worldParams = worldParams,

                centerBlockCoord = centerBlockCoord,

                chunkXZCoords = newChunkColumnsList,
                blockHeightsWindow = blockHeightsWindow,
                renderDistance = renderDistance,
                windowEdgeBlockLength = windowEdgeBlockLength
            };

            heightMapJobHandle =
                heightMapForBlockColumnsJob.ScheduleParallel(newChunkColumnsList.Length, 1, state.Dependency);

            state.Dependency = heightMapJobHandle;
        }

        private void MoveWindow(int2 newCenter, int2 oldCenter)
        {
            if (firstRun || !newCenter.Equals(oldCenter))
            {
                int dx = newCenter.x - oldCenter.x;
                int dz = newCenter.y - oldCenter.y;

                if (firstRun || math.abs(dx) > 1 || math.abs(dz) > 1)
                {
                    // For large movements/teleports: Reset entire window
                    if (blockHeightsWindow.IsCreated)
                        blockHeightsWindow.Dispose();
                    blockHeightsWindow = new NativeArray<int>(windowBlockLength, Allocator.Persistent);
                }
                else
                {
                    // For small movements: Shift existing window
                    ShiftBlockHeightsWindow(oldCenter, newCenter, blockHeightsWindow, windowEdgeBlockLength);
                }

                lastWindowCenterChunkCoordXZ = newCenter;
                // gets set in another function  firstRun = false;
            }
        }
        // rend dist is 5, chunk size is 16 (chunks are cubes but with xz we obviously count squares instead)
        // windowedgeblocklength is
        // blockheightswindow len is
        //
        //
        // ... what if the problem is caused by the fact that the CENTER isn't REALLY the center. it's the origin coordinates of the chunk the player is on. which COULD mean that one side is still being offset in some calculations 
        //
        // i mean that would make sense to me

        private void InitWindowData()
        {
            // windows are 2D and centered on the player.
            windowEdgeChunkLength = (2 * renderDistance) + 1;
            windowEdgeBlockLength = windowEdgeChunkLength * CHUNK_SIZE;
            windowBlockLength = windowEdgeBlockLength * windowEdgeBlockLength;

            if (!blockHeightsWindow.IsCreated || windowBlockLength != blockHeightsWindow.Length)
            {
                if (blockHeightsWindow.IsCreated) blockHeightsWindow.Dispose();
                blockHeightsWindow = new NativeArray<int>(windowBlockLength, Allocator.Persistent);
            }
        }


        private void InitDesiredChunks(ref SystemState state)
        {
            desiredChunks.Clear();

            var allChunksQuery = SystemAPI.QueryBuilder()
                .WithAll<DOTS_ChunkState>()
                .Build();
            var allChunks =
                allChunksQuery.ToEntityArray(Allocator
                    .Temp); //remove from desired chunks entities which have chunkstate of different kind than ready forblockgeneration
            var chunkStates = SystemAPI.GetComponentLookup<DOTS_ChunkState>(true); // read-only

            for (int i = 0; i < allChunks.Length; i++)
            {
                var entity = allChunks[i];
                if (chunkStates[entity].Value == ChunkStateEnum.BlockGenPending)
                    if (chunkStates[entity].Value == ChunkStateEnum.BlockGenPending)
                    {
                        desiredChunks.Add(entity);
                    }
            }
        }


        private static float2 GetPerlinOffset(WorldParams worldParams)
        {
            return new float2(
                math.sin(worldParams.worldSeed * 0.1f) * 1000f,
                math.cos(worldParams.worldSeed * 0.1f) * 1000f
            );
        }

        /// <summary>
        ///  Calculates which new chunk XZ columns need to be generated based on the player's movement.
        ///  If the player has moved more than 1 chunk in any direction, the entire render window is regenerated.
        ///  // todo wait why can't we just shift by more than one row/column?
        ///       //     i mean in 99% of it won't matter but still
        /// </summary>
        /// <param name="oldCenter"></param>
        /// <param name="newCenter"></param>
        /// <param name="renderDistance"></param>
        /// <param name="chunkSize"></param>
        /// <returns></returns>
        public NativeList<int2> GetNewChunkColumnCoords(int2 oldCenter, int2 newCenter, int chunkSize)
        {
            newChunkColumnsList.Clear();
            int dx = newCenter.x - oldCenter.x;
            int dz = newCenter.y - oldCenter.y;
    
            // For large movements/teleports: Generate entire window
            if (firstRun || math.abs(dx) > 1 || math.abs(dz) > 1)
            {
                firstRun = false;
        
                for (int cz = newCenter.y - renderDistance; cz <= newCenter.y + renderDistance; cz++)
                for (int cx = newCenter.x - renderDistance; cx <= newCenter.x + renderDistance; cx++)
                    newChunkColumnsList.Add(new int2(cx * chunkSize, cz * chunkSize));
            }
            else // For small movements: Generate only new strips
            {
                // +X strip
                if (dx > 0)
                {
                    int stripX = newCenter.x + renderDistance;
                    for (int z = newCenter.y - renderDistance; z <= newCenter.y + renderDistance; z++)
                        newChunkColumnsList.Add(new int2(stripX * chunkSize, z * chunkSize));
                }
                // -X strip
                if (dx < 0)
                {
                    int stripX = newCenter.x - renderDistance;
                    for (int z = newCenter.y - renderDistance; z <= newCenter.y + renderDistance; z++)
                        newChunkColumnsList.Add(new int2(stripX * chunkSize, z * chunkSize));
                }
                // +Z strip
                if (dz > 0)
                {
                    int stripZ = newCenter.y + renderDistance;
                    for (int x = newCenter.x - renderDistance; x <= newCenter.x + renderDistance; x++)
                        newChunkColumnsList.Add(new int2(x * chunkSize, stripZ * chunkSize));
                }
                // -Z strip
                if (dz < 0)
                {
                    int stripZ = newCenter.y - renderDistance;
                    for (int x = newCenter.x - renderDistance; x <= newCenter.x + renderDistance; x++)
                        newChunkColumnsList.Add(new int2(x * chunkSize, stripZ * chunkSize));
                }
            }
            return newChunkColumnsList;
        }
        
        
        // todo can i make a system that is a static UTIL system to share this function? 
        public bool PlayerChangedChunks(ref SystemState state)
        {
            foreach (var (transform,
                         chunkCoord,
                         lastChunkPos)
                     in
                     SystemAPI.Query<
                             RefRO<LocalTransform>,
                             RefRW<EntityChunkCoords>,
                             RefRW<LastChunkCoords>>()
                         .WithAll<PlayerTag>())
            {
                if (chunkCoord.ValueRO.OnChunkChange)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///  Shifts the block heights window based on the player's movement.
        /// </summary>
        /// <param name="oldCenter"></param>
        /// <param name="newCenter"></param>
        /// <param name="window"></param>
        /// <param name="windowEdgeBlockLength"></param>
        void ShiftBlockHeightsWindow(int2 oldCenter, int2 newCenter,
            NativeArray<int> window, int windowEdgeBlockLength)
        {
            int shiftX = (newCenter.x - oldCenter.x) * CHUNK_SIZE;
            int shiftZ = (newCenter.y - oldCenter.y) * CHUNK_SIZE;
            if (shiftX == 0 && shiftZ == 0) return;

            // Create a temporary copy of the current window
            var oldCopy = new NativeArray<int>(window, Allocator.Temp);
            // Clear the current window (we'll repopulate it)
            for (int i = 0; i < window.Length; i++)
            {
                window[i] = 0;
            }

            // Calculate the half size using the same logic as getBlockWindowIndexXZ
            int half = windowEdgeBlockLength / 2;

            // Iterate through all positions in the old window
            for (int z = 0; z < windowEdgeBlockLength; z++)
            for (int x = 0; x < windowEdgeBlockLength; x++)
            {
                // compute block offset relative to *new* center
                int dxNew = x - half;
                int dzNew = z - half;

                // corresponding position in old window (relative to old center)
                int dxOld = dxNew + shiftX;
                int dzOld = dzNew + shiftZ;

                int newIndex = z * windowEdgeBlockLength + x;
                // Check if the new block offsets are within the window bounds
                  
                // Check if the old block offsets were within the window bounds
                if (dxOld >= -half && dxOld < half && dzOld >= -half && dzOld < half)
                {
                    int oldX = dxOld + half;
                    int oldZ = dzOld + half;
                    int oldIndex = oldZ * windowEdgeBlockLength + oldX;
                    window[newIndex] = oldCopy[oldIndex];
                }
                // else
                // {
                    // outside copied region -> mark as empty/uninitialized sentinel (so height job will fill it)
                    // window[newIndex] = 0;
                // }
            }

            oldCopy.Dispose();
        }
    }
}