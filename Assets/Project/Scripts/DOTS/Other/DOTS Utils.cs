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
        #region facesNew

        [BurstCompile]
        public static class FaceData
        {
            private static NativeArray<Face> AllFacesPrivate;
            public static NativeArray<Face>.ReadOnly AllFaces => AllFacesPrivate.AsReadOnly();
            private static bool initialized;

            //constructor
            public static void Init()
            {
                if (initialized) return;


                AllFacesPrivate = new NativeArray<Face>(6, Allocator.Persistent);


                // +X (right)
                AllFacesPrivate[0] = new Face(
                    new int3(1, 0, 0),
                    new float3(1, 0, 0), // normal
                    new float3(1, 0, 0),
                    new float3(1, 0, 1),
                    new float3(1, 1, 0),
                    new float3(1, 1, 1)
                );

                // -X
                AllFacesPrivate[1] = new Face(
                    new int3(-1, 0, 0),
                    new float3(-1, 0, 0), // normal
                    new float3(0, 0, 1),
                    new float3(0, 0, 0),
                    new float3(0, 1, 1),
                    new float3(0, 1, 0)
                );

                // +Y AKA TOP
                AllFacesPrivate[2] = new Face(
                    new int3(0, 1, 0),
                    new float3(0, 1, 0), // normal
                    new float3(0, 1, 0),
                    new float3(1, 1, 0),
                    new float3(0, 1, 1),
                    new float3(1, 1, 1)
                );

                // -Y (BOT)
                AllFacesPrivate[3] = new Face(
                    new int3(0, -1, 0),
                    new float3(0, -1, 0), // normal
                    new float3(1, 0, 0),
                    new float3(0, 0, 0),
                    new float3(1, 0, 1),
                    new float3(0, 0, 1)
                );

                // +Z AKA FRONT
                AllFacesPrivate[4] = new Face(
                    new int3(0, 0, 1),
                    new float3(0, 0, 1), // normal
                    new float3(1, 0, 1),
                    new float3(0, 0, 1),
                    new float3(1, 1, 1),
                    new float3(0, 1, 1)
                );

                // -Z AKA BACK
                AllFacesPrivate[5] = new Face(
                    new int3(0, 0, -1),
                    new float3(0, 0, -1), // normal
                    new float3(0, 0, 0),
                    new float3(1, 0, 0),
                    new float3(0, 1, 0),
                    new float3(1, 1, 0)
                );

                initialized = true;
            }

            public static void Dispose()
            {
                if (AllFacesPrivate.IsCreated)
                    AllFacesPrivate.Dispose();
                initialized = false;
            }
        }

        public struct Face
        {
            public int3 direction;
            public float3 normal;

            // fixed-size corner offsets
            public float3 v0;
            public float3 v1;
            public float3 v2;
            public float3 v3;

            public Face(int3 dir, float3 normal, float3 v0, float3 v1, float3 v2, float3 v3)
            {
                this.direction = dir;
                this.normal = normal;
                this.v0 = v0;
                this.v1 = v1;
                this.v2 = v2;
                this.v3 = v3;
            }

            public float3 GetCorner(int i)
            {
                return i switch
                {
                    0 => v0,
                    1 => v1,
                    2 => v2,
                    3 => v3,
                    _ => throw new System.IndexOutOfRangeException()
                };
            }
        }

        #endregion


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
        public static void WorldPosToChunkCoord(in float3 worldPos,out int3 result, int chunkSize = CHUNK_SIZE)
        {
            result = new int3(
                (int)math.floor(worldPos.x / chunkSize),
                (int)math.floor(worldPos.y / chunkSize),
                (int)math.floor(worldPos.z / chunkSize)
            );
            return;
        }
        //where performance is CRITICAL, replace with
        //int3 chunkCoord = (int3)math.floor(worldPos / chunkSize);
        // burst doesn't work well with external helper functions


        [BurstCompile]
        public static void GetChunkWorldPos(in int3 chunkCoords, out float3 result, int chunkSize = CHUNK_SIZE)
        {
            result = chunkCoords * chunkSize;
        }

        [BurstCompile]
        public static bool IsBlockSolid(BlockType block)
        {
            return block != BlockType.Air;
        }


        [BurstCompile]
        public static void  GetBlockUV(in BlockType type, int face, out float2 result)
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
                case 0: result = uvMin + new float2(0f, 0f); // Bottom-left
                    return;
                case 1: result = uvMin + new float2(tileWidth, 0f); // Bottom-right
                    return;
                case 2: result = uvMin + new float2(0f, 1f); // Top-left
                    return;
                case 3: result = uvMin + new float2(tileWidth, 1f); // Top-right
                    return;
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

        public static void DotsDebugLogFormat(LogType logType, LogOption logOption, UnityEngine.Object context,
            string message, params object[] args)
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