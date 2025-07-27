using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static VoxelConstants;
using static WorldUtils;

[BurstCompile]
public struct GenerateMeshDataJob : IJob
{
    [ReadOnly] public NativeArray<BlockType> blocks;
    public NativeList<Vertex> vertices;
    public NativeList<int> triangles;
    public int chunkSize;




    public void Execute()
    {
        for (int x = 0; x < chunkSize; x++)
        for (int y = 0; y < chunkSize; y++)
        for (int z = 0; z < chunkSize; z++)
        {
            int index = x * chunkSize * chunkSize + y * chunkSize + z;
            if (blocks[index] == BlockType.Air) continue;

            // Naive: always add 6 faces for now (improve later)
            AddVisibleFaces(x, y, z, blocks, vertices, triangles);
        }
    }

    private void AddVisibleFaces(int x, int y, int z, NativeArray<BlockType> blocks, NativeList<Vertex> vertices, NativeList<int> triangles)
    {
        BlockType current = blocks[CoordsToIndex(x, y, z)];

        foreach (var face in WorldUtils.AllFaceDataForBurst)        
        {

            int3 neighborPos = new int3(x, y, z) + face.direction;

            if (!IsInBoundsOfChunkInt3(neighborPos) ||
                !IsBlockSolidAndInChunk(neighborPos.x, neighborPos.y, neighborPos.z, blocks, CHUNK_SIZE))
            {
                int startIndex = vertices.Length;

                for (int i = 0; i < 4; i++)
                {
                    vertices.Add(new Vertex
                    {
                        position = new float3 (new int3(x, y, z) + face.GetCornerOffset(i)),
                        normal = face.normal,
                        uv = GetBlockUV(current, i)
                    });
                }
            
                // basically:
                // Correct winding → triangle shows up from the front (visible).
                // Wrong winding → triangle invisible from the front (backface culling).

                    
                // v2------v3
                // |       |
                // |       |
                // v0------v1
                // Triangle order (0, 2, 1), (1, 2, 3). like the image. this order because it's like ABCD
                //This preserves clockwise winding order (which tells the GPU which side is the front). If you reverse it, the face might become invisible due to backface culling.

                // so we just split it up by threes and hope that the triangles were added  correctly. 
                triangles.Add(startIndex + 0);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 1);
                
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 3);
            }
        }
    }


}