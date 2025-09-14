using Project.Scripts.DOTS.Other;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Project.Scripts.DOTS.Other.DOTS_Utils;
using static Project.Scripts.DOTS.Systems.WorldHeightmapWindowHolder;

namespace Project.Scripts.DOTS.Systems
{
    public static class WorldHeightmapWindowHolder
    {
        public static NativeArray<int> blockHeightsWindow;
        public static JobHandle heightMapJobHandle;

        public static int2
            lastWindowCenterChunkCoordXZ; // chunk coord of window center (player position floored to chunk coords)

        public static bool firstRun = true;
    }


    [UpdateAfter(typeof(ChunkDespawnMarkerSystem))]
    public partial struct HeightMapSystem : ISystem
    {
        private ComponentLookup<DOTS_Chunk> chunksLookup;

        // private ComponentLookup<DOTS_ChunkState> chunkStateLookup;
        private BufferLookup<DOTS_Block> blocksLookup;
        private NativeList<int2> newColumnsList;

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

        public void OnCreate(ref SystemState state)
        {
            chunksLookup = SystemAPI.GetComponentLookup<DOTS_Chunk>(true);
            blocksLookup = SystemAPI.GetBufferLookup<DOTS_Block>(false);
            // chunkStateLookup = SystemAPI.GetComponentLookup<DOTS_ChunkState>(false);

            allChunksQuery = SystemAPI.QueryBuilder()
                .WithAny<DOTS_ChunkState>()
                .Build();


            desiredChunks = new NativeList<Entity>(Allocator.Persistent);

            perlinOffset = float2.zero;

            newColumnsList = new NativeList<int2>(Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            desiredChunks.Dispose();
            if (blockHeightsWindow.IsCreated) blockHeightsWindow.Dispose();
            newColumnsList.Dispose();
        }


        public void OnUpdate(ref SystemState state)
        {
            // only update on chunk crossing, since window only moves then.
            if (!PlayerChangedChunks(ref state)) return;

            #region init uninteresting vars

            InitDesiredChunks(ref state);
            if (desiredChunks.Length == 0)
                return;

            chunksLookup.Update(ref state);
            blocksLookup.Update(ref state);
            GetWorldParams(ref state);
            // Debug.Log($"Found {desiredChunks.Length} chunks that need BLOCK generation");

            var playerChunkCoordXZ = GetPlayerPositionAndRenderDistance(ref state);

            InitWindowData();

            #endregion


            int2 oldCenter = lastWindowCenterChunkCoordXZ; // store the previous
            int2 newCenter = playerChunkCoordXZ; // get current

            UpdateWindow(newCenter, oldCenter);

            GetNewColumnCoords(oldCenter, newCenter, renderDistance, CHUNK_SIZE);

            ScheduleHeightMapJob(ref state, playerChunkCoordXZ);
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

        private void ScheduleHeightMapJob(ref SystemState state, int2 playerChunkCoordXZ)
        {
            var heightMapForBlockColumnsJob = new HeightMapForBlockColumnsJob
            {
                perlinOffset = perlinOffset,
                worldParams = worldParams,

                playerChunkCoordXZ = playerChunkCoordXZ,

                chunkXZCoords = newColumnsList,
                blockHeightsWindow = blockHeightsWindow,
                renderDistance = renderDistance,
                windowEdgeBlockLength = windowEdgeBlockLength
            };

            heightMapJobHandle =
                heightMapForBlockColumnsJob.ScheduleParallel(newColumnsList.Length, 1, state.Dependency);

            state.Dependency = heightMapJobHandle;
        }

        private void UpdateWindow(int2 newCenter, int2 oldCenter)
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
        public NativeList<int2> GetNewColumnCoords(int2 oldCenter, int2 newCenter, int renderDistance, int chunkSize)
        {
            // newColumnsList.Clear();
            int dx = newCenter.x - oldCenter.x;
            int dz = newCenter.y - oldCenter.y;

            // For large movements/teleports: Generate entire window
            if (firstRun || math.abs(dx) > 1 || math.abs(dz) > 1)
            {
                firstRun = false;
                
                newColumnsList.Clear();
                for (int cz = newCenter.y - renderDistance; cz <= newCenter.y + renderDistance; cz++)
                for (int cx = newCenter.x - renderDistance; cx <= newCenter.x + renderDistance; cx++)
                    newColumnsList.Add(new int2(cx * chunkSize, cz * chunkSize));
            }
            else // For small movements: Generate only new strips
            {
                // +X strip
                if (dx > 0)
                {
                    int stripX = newCenter.x + renderDistance; // chunk coord
                    for (int z = newCenter.y - renderDistance; z <= newCenter.y + renderDistance; z++)
                        newColumnsList.Add(new int2(stripX * chunkSize, z * chunkSize));
                }

                // -X strip
                if (dx < 0)
                {
                    int stripX = newCenter.x - renderDistance;
                    for (int z = newCenter.y - renderDistance; z <= newCenter.y + renderDistance; z++)
                        newColumnsList.Add(new int2(stripX * chunkSize, z * chunkSize));
                }

                // +Z strip
                if (dz > 0)
                {
                    int stripZ = newCenter.y + renderDistance;
                    for (int x = newCenter.x - renderDistance; x <= newCenter.x + renderDistance; x++)
                        newColumnsList.Add(new int2(x * chunkSize, stripZ * chunkSize));
                }

                // -Z strip
                if (dz < 0)
                {
                    int stripZ = newCenter.y - renderDistance;
                    for (int x = newCenter.x - renderDistance; x <= newCenter.x + renderDistance; x++)
                        newColumnsList.Add(new int2(x * chunkSize, stripZ * chunkSize));
                }
            }

            return newColumnsList;
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
                if (chunkCoord.ValueRO.OnChunkChange == false)
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
            var oldWindowCopy = new NativeArray<int>(window, Allocator.Temp);

            // Clear the current window (we'll repopulate it)
            for (int i = 0; i < window.Length; i++)
            {
                window[i] = 0;
            }

            
            // todo Wait... so should i  rework this to not consider the center chunk as special?
            // Calculate the half size using the same logic as getBlockWindowIndexXZ
            int blockWindowHalf = (windowEdgeBlockLength - CHUNK_SIZE) / 2;

            // Iterate through all positions in the old window
            for (int z = 0; z < windowEdgeBlockLength; z++)
            {
                for (int x = 0; x < windowEdgeBlockLength; x++)
                {
                    // Convert array indices to block offsets relative to the old center
                    int dx = x - blockWindowHalf;
                    int dz = z - blockWindowHalf;

                    // Calculate the new block offsets after the shift
                    int newDx = dx - shiftX;
                    int newDz = dz - shiftZ;

                    // Check if the new block offsets are within the window bounds
                    if (newDx >= -blockWindowHalf && newDx < blockWindowHalf + CHUNK_SIZE &&
                        newDz >= -blockWindowHalf && newDz < blockWindowHalf + CHUNK_SIZE)
                    {
                        // Calculate the index in the old window (current x,z)
                        int oldIndex = z * windowEdgeBlockLength + x;

                        // Calculate the index in the new window for the new offsets
                        try
                        {
                            int newIndex = getBlockWindowIndexXZ(newDx, newDz, windowEdgeBlockLength);
                            window[newIndex] = oldWindowCopy[oldIndex];
                        }
                        catch
                        {
                            // If we get an index out of bounds, skip this block
                            // This might happen at the edges due to rounding or off-by-one
                        }
                    }
                }
            }

            oldWindowCopy.Dispose();
        }
    }
}