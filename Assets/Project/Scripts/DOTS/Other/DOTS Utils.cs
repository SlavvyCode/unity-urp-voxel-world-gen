using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Project.Scripts.DOTS.Other
{
    [BurstCompile]
    public static class DOTS_Utils
    {
        #region faces

        public static class FaceData
        {
            public static readonly Face[] AllFaces = new Face[6]
            {
                // +X (Right)
                new Face(
                    new int3(1, 0, 0),
                    new float3(1, 0, 0),
                    new float3[]
                    {
                        new float3(1, 0, 0), // v0
                        new float3(1, 0, 1), // v1
                        new float3(1, 1, 0), // v2
                        new float3(1, 1, 1) // v3
                    }
                ),

                // -X (Left)
                new Face(
                    new int3(-1, 0, 0),
                    new float3(-1, 0, 0),
                    new float3[]
                    {
                        new float3(0, 0, 1), // v0
                        new float3(0, 0, 0), // v1
                        new float3(0, 1, 1), // v2
                        new float3(0, 1, 0) // v3
                    }
                ),

                // +Y (Top)
                new Face(
                    new int3(0, 1, 0),
                    new float3(0, 1, 0),
                    new float3[]
                    {
                        new float3(0, 1, 0), // v0
                        new float3(1, 1, 0), // v1
                        new float3(0, 1, 1), // v2
                        new float3(1, 1, 1) // v3
                    }
                ),

                // -Y (Bottom)
                new Face(
                    new int3(0, -1, 0),
                    new float3(0, -1, 0),
                    new float3[]
                    {
                        new float3(1, 0, 0), // v0
                        new float3(0, 0, 0), // v1
                        new float3(1, 0, 1), // v2
                        new float3(0, 0, 1) // v3
                    }
                ),

                // +Z (Front)
                new Face(
                    new int3(0, 0, 1),
                    new float3(0, 0, 1),
                    new float3[]
                    {
                        new float3(1, 0, 1), // v0
                        new float3(0, 0, 1), // v1
                        new float3(1, 1, 1), // v2
                        new float3(0, 1, 1) // v3
                    }
                ),

                // -Z (Back)
                new Face(
                    new int3(0, 0, -1),
                    new float3(0, 0, -1),
                    new float3[]
                    {
                        new float3(0, 0, 0), // v0
                        new float3(1, 0, 0), // v1
                        new float3(0, 1, 0), // v2
                        new float3(1, 1, 0) // v3
                    }
                )
            };
        }

        public struct Face
        {
            public int3 direction;
            public float3[] cornerOffsets;
            public float3 normal;

            public Face(int3 dir, float3 normal, float3[] cornerOffsets)
            {
                this.direction = dir;
                this.cornerOffsets = cornerOffsets;
                this.normal = normal;
            }
        }

        # endregion

        public const int CHUNK_SIZE = 16;
        public const int CHUNK_VOLUME = 16 * 16 * 16;

        // Burst-compatible methods must:
        // 1. Be static
        // 2. Only use blittable types/Unity.Mathematics
        // 3. Avoid managed types (string, class references)

        [BurstCompile]
        /*
         * transform world position into the position of the chunk the world position is in in chunk space 
         */
        public static int3 WorldPosToChunkCoord(float3 worldPos, int chunkSize = CHUNK_SIZE)
        {
            return new int3(
                (int)math.floor(worldPos.x / chunkSize),
                (int)math.floor(worldPos.y / chunkSize),
                (int)math.floor(worldPos.z / chunkSize)
            );
        }
        [BurstCompile]
        public static float3 GetChunkWorldPos(int3 chunkCoords, int chunkSize = CHUNK_SIZE)
        {
            return chunkCoords * chunkSize;
        }

        [BurstCompile]
        public static bool IsBlockSolid(BlockType block)
        {
            return block != BlockType.Air;
        }
        
        
        
        [BurstCompile]

        public static float2 GetBlockUV(BlockType type, int face)
        {
            // get the total number of block types in the enum
            // Enum.GetValues() returns an array of all values in the enum.
            int totalTiles = (int)BlockType.COUNT;


            float tileWidth = 1f / totalTiles;

            int index = (int)type;

            // Bottom-left corner of the tile (index * tileWidth on X, always 0 on Y)
            float2 uvMin = new float2(index * tileWidth, 0f);

            switch (face)
            {
                case 0: return uvMin + new float2(0f, 0f); // Bottom-left
                case 1: return uvMin + new float2(tileWidth, 0f); // Bottom-right
                case 2: return uvMin + new float2(0f, 1f); // Top-left
                case 3: return uvMin + new float2(tileWidth, 1f); // Top-right
            }

            throw new ArgumentException("Invalid face index");
        }
        [BurstCompile]
        public static int ToIndex(int x, int y, int z)
        {
            return x + CHUNK_SIZE * (y + CHUNK_SIZE * z);
        }
        
        
        
// Add this attribute to disable Burst for debugging
        [BurstDiscard]
        public static void DotsDebugLog(string message)
        {
            Debug.Log(message);
        }
        
        public static void DotsDebugLogFormat(string message, object[] args)
        {
            Debug.LogFormat(message, args);
        }

        public static void DotsDebugLogFormat(LogType logType, LogOption logOption, UnityEngine.Object context, string message, params object[] args)
        {
            if (logType == LogType.Error)
            {
                Debug.LogErrorFormat(context, message, args);
            }
            else if (logType == LogType.Warning)
            {
                Debug.LogWarningFormat(context, message, args);
            }
            else
            {
                Debug.LogFormat(context, message, args);
            }
        }
    }
}