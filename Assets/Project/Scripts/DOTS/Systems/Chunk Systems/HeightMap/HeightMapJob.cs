using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Project.Scripts.DOTS.Other.DOTS_Utils;

namespace Project.Scripts.DOTS.Systems
{

    [BurstCompile]
    public struct HeightMapForBlockColumnsJob : IJobFor
    {
        #region terrainGen

        public WorldParams worldParams;
        
        public float2 perlinOffset;
        private float terrainRoughness;
        private float baseHeight;
        private float heightVariation;
        private int noiseLayers;

        #endregion


        // public NativeParallelHashMap<int2, int>.ParallelWriter coordsToHeightsHashMap; // Array of pillar coordinates (x,z)
        [ReadOnly] public NativeList<int2> chunkXZCoords;
        public int2 centerBlockCoord;

        [NativeDisableParallelForRestriction] public NativeArray<int> blockHeightsWindow;
        public int renderDistance;
        public int windowEdgeBlockLength;

        //do for each chunk
        [BurstCompile]
        public void Execute(int jobIndex)
        {
            
            terrainRoughness = worldParams.terrainRoughness;
            baseHeight = worldParams.baseHeight;
            heightVariation = worldParams.heightVariation;
            noiseLayers = worldParams.noiseLayers;
            
            int centerWorldX = centerBlockCoord.x;
            int centerWorldZ = centerBlockCoord.y;
            
            // half window size in blocks (works for even and odd)
            int windowHalf = windowEdgeBlockLength / 2;
            
            // get a chunk based on index
            // how many (x,z) samples per pillar
            int2 chunkColumnCoord = chunkXZCoords[jobIndex];

            for (int localX = 0; localX < CHUNK_SIZE; localX++)
            for (int localZ = 0; localZ < CHUNK_SIZE; localZ++)
            {
                // compute world x,z coordinates
                int2 worldColumn = new int2(chunkColumnCoord.x + localX, chunkColumnCoord.y + localZ);

                // Compute offsets in **blocks** relative to player's chunk's center
                int dx = worldColumn.x - centerWorldX;
                int dz = worldColumn.y - centerWorldZ;

                // Skip blocks outside window
                // if (math.abs(dx) > windowHalf || math.abs(dz) > windowHalf)
                    // continue;
                    // todo this has got to be the issue, never let a program silently fail on somethign so critical 

                // Compute height
                int height = (int)CalculateTerrainHeight(worldColumn.x, worldColumn.y);
                
                // Write into window
                int index = getBlockWindowIndexXZ(dx, dz, windowEdgeBlockLength);
                if (index >= 0 && index < blockHeightsWindow.Length)
                {
                    blockHeightsWindow[index] = height;
                    
                    // //every tenth index, log it
                    // if (index % 10 == 0)
                    // {
                    //     DotsDebugLog("HeightMapJob: Set height at index " + index + " (dx: " + dx + ", dz: " + dz + ") to " + height);
                    // }
                }
                
            }
        }

        float CalculateTerrainHeight(int pillarX, int pillarY)
        {
            {
                //stolen from chunk.cs
                float perlinScale = terrainRoughness;

                float sampleX = (pillarX + perlinOffset.x) * perlinScale;
                float sampleZ = (pillarY + perlinOffset.y) * perlinScale;

                float total = 0f;
                float max = 0f;
                float amplitude = 1f;
                float frequency = 1f;

                for (int i = 0; i < noiseLayers; i++)
                {
                    total += MyPerlin.Noise(sampleX * frequency, sampleZ * frequency) * amplitude;
                    max += amplitude;
                    amplitude *= 0.5f;
                    frequency *= 2f;
                }

                float normalized = total / max;
                return baseHeight + (normalized * heightVariation);
            }
        }
    }
}