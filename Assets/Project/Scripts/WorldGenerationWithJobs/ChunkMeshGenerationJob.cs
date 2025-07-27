using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static VoxelConstants;
using static WorldUtils;

[BurstCompile]
public struct ChunkMeshGenerationJob : IJob
{
    [ReadOnly] public NativeArray<BlockType> blocks;
    [ReadOnly] public int3 chunkCoord;
    public int chunkSize;
    [ReadOnly]
    public NativeArray<Int3FaceDataForBurst> facesToUse;

    public NativeList<Vertex> vertices;
    public NativeList<int> triangles;

    public void Execute()
    {
        for (int x = 0; x < chunkSize; x++)
        for (int y = 0; y < chunkSize; y++)
        for (int z = 0; z < chunkSize; z++)
        {
            int index = x * chunkSize * chunkSize + y * chunkSize + z;
            if (blocks[index] == BlockType.Air) continue;

            AddVisibleFaces(x, y, z);
        }
    }

    private void AddVisibleFaces(int x, int y, int z)
    {
        BlockType current = blocks[CoordsToIndex(x, y, z)];
        foreach (var face in facesToUse)
        {
            int nx = x + face.direction.x;
            int ny = y + face.direction.y;
            int nz = z + face.direction.z;

            bool outOfBounds = nx < 0 || ny < 0 || nz < 0 ||
                               nx >= chunkSize || ny >= chunkSize || nz >= chunkSize;

            bool neighborIsAir = outOfBounds || blocks[CoordsToIndex(nx, ny, nz)] == BlockType.Air;

            if (neighborIsAir)
            {
                int startIndex = vertices.Length;

                for (int i = 0; i < 4; i++)
                {
                    vertices.Add(new Vertex
                    {
                        position = new float3(x, y, z) + face.GetCornerOffset(i),
                        normal = face.normal,
                        uv = GetBlockUV(current, i)
                    });
                }

                triangles.Add(startIndex + 0);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 3);
            }
        }
    }

    private int CoordsToIndex(int x, int y, int z)
    {
        return x * chunkSize * chunkSize + y * chunkSize + z;
    }
}
