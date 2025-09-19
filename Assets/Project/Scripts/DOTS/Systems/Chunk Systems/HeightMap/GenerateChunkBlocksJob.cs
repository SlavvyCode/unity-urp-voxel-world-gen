using Project.Scripts.DOTS.Other;using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static  Project.Scripts.DOTS.Other.DOTS_Utils;

namespace Project.Scripts.DOTS.Systems
{
  
        [BurstCompile(CompileSynchronously = true)]
        public struct GenerateChunkBlocksJob : IJobFor
        {
            [ReadOnly] public NativeArray<Entity> desiredChunks;

            // [ReadOnly] public NativeArray<int2> chunkYColumnCoords;
            [ReadOnly] public ComponentLookup<DOTS_Chunk> chunksLookup;

            [NativeDisableParallelForRestriction] public BufferLookup<DOTS_Block> blocksLookup;

            // [ReadOnly] public NativeParallelHashMap<int2, int> blockColumnCoordsToHeightHashMap;
            public EntityCommandBuffer.ParallelWriter ecb;

            public int2 playerChunkCoordXZ;
            [NativeDisableParallelForRestriction] public NativeArray<int> blockHeightsWindow;
            public int windowEdgeBlockLength;
            public int renderDistance;

            //todo
            // pillar height depends on render distance (spawned chunks).
            // i could still make the cylinder render distance as opposed to a sphere to reduce the pillar height.
            // i do worry though if the player would ever encoutner any "fake bottomless holes" or "cut off mountaintops" though.
            // alternatively i coudl disable chunk rendering if they're fully obstructed by another chunk (chunk completely enclosed from all sides.)
            // - but that's a miniscule effect on the CPU right now i think, mostly gpu does that and my game is very gpu light for now still


            // EXECUTES ONCE PER EACH CHUNK IN THE RENDER DISTANCE
            [BurstCompile]
            public void Execute(int jobIndex)
            {
                Entity chunkEntity = desiredChunks[jobIndex];
                int3 chunkCoord = chunksLookup[chunkEntity].ChunkCoord;
                DynamicBuffer<DOTS_Block> blocks = blocksLookup[chunkEntity];
                InitializeChunkBlocks(ref blocks);

                int2 centerBlockCoord = new int2(
                    playerChunkCoordXZ.x * CHUNK_SIZE + CHUNK_SIZE / 2,
                    playerChunkCoordXZ.y * CHUNK_SIZE + CHUNK_SIZE / 2
                );
                int2 chunkColumnWorldCoord = new int2(chunkCoord.x * CHUNK_SIZE, chunkCoord.z * CHUNK_SIZE);

                int columnHeight;
                int stoneBottom = 0;

                // already all Air
                for (int localX = 0; localX < CHUNK_SIZE; localX++)
                for (int localZ = 0; localZ < CHUNK_SIZE; localZ++)
                {
                    int worldX = chunkColumnWorldCoord.x + localX;
                    int worldZ = chunkColumnWorldCoord.y + localZ;

                    int dx = worldX - centerBlockCoord.x;
                    int dz = worldZ - centerBlockCoord.y;
                    int index = getBlockWindowIndexXZ(dx, dz, windowEdgeBlockLength);
                    columnHeight = blockHeightsWindow[index];

                    int stoneTop = math.max(stoneBottom, columnHeight - 4);
                    int dirtTop = math.max(stoneTop, columnHeight - 1);
                    int grassTop = columnHeight;

                    // describes the world location of the chunk in unity units
                    int chunkWorldY = chunkCoord.y * CHUNK_SIZE;


                    // Stone
                    int stoneLocalBottom = math.clamp(stoneBottom - chunkWorldY, 0, CHUNK_SIZE);
                    int stoneLocalTop = math.clamp(stoneTop - chunkWorldY, 0, CHUNK_SIZE);
                    for (int y = stoneLocalBottom; y < stoneLocalTop; y++)
                        blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Stone };

                    // Dirt
                    int dirtLocalBottom = math.clamp(stoneTop - chunkWorldY, 0, CHUNK_SIZE);
                    int dirtLocalTop = math.clamp(dirtTop - chunkWorldY, 0, CHUNK_SIZE);
                    for (int y = dirtLocalBottom; y < dirtLocalTop; y++)
                        blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Dirt };

                    // Grass
                    int grassLocalBottom = math.clamp(dirtTop - chunkWorldY, 0, CHUNK_SIZE);
                    int grassLocalTop = math.clamp(grassTop - chunkWorldY, 0, CHUNK_SIZE);
                    for (int y = grassLocalBottom; y < grassLocalTop; y++)
                        blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Grass };
                }


                // foreach (var chunk in desiredChunks)
                // {
                // ecb.SetComponent<DOTS_ChunkState>(chunkEntity, new DOTS_ChunkState { Value = ChunkState.BlocksGenerated });

                ecb.SetComponent(jobIndex, chunkEntity, new DOTS_ChunkState { Value = ChunkStateEnum.MeshPending });
                // }
            }


            private void InitializeChunkBlocks(ref DynamicBuffer<DOTS_Block> blocks)
            {
                if (blocks.Length == CHUNK_VOLUME)
                {
                    return;
                }

                // If the buffer is not the correct size, we need to initialize it
                blocks.ResizeUninitialized(CHUNK_VOLUME);

                for (int i = 0; i < CHUNK_VOLUME; i++)
                    blocks[i] = new DOTS_Block { Value = BlockType.Air };
            }


        }


    }
