using Unity.Burst;
using Unity.Mathematics;

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
        public static int3 GetChunkCoord(float3 worldPos, int chunkSize = CHUNK_SIZE)
        {
            return new int3(
                (int)math.floor(worldPos.x / chunkSize),
                (int)math.floor(worldPos.y / chunkSize),
                (int)math.floor(worldPos.z / chunkSize)
            );
        }

        [BurstCompile]
        public static bool IsBlockSolid(BlockType block)
        {
            return block != BlockType.Air;
        }
    }
}