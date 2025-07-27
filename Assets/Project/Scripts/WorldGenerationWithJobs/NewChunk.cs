using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static WorldUtils;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class NewChunk : MonoBehaviour
{
    private MeshFilter meshFilter;
    public NativeArray<BlockType> blocks;    
    public int3 chunkCoord; // coordinates of the chunk in the world
    public int CHUNK_SIZE;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    private void OnDestroy()
    {
        if (blocks.IsCreated)
        {
            blocks.Dispose();
        }
    }


    public NewChunk Initialize(NativeArray<BlockType> blocksData, int3 coord)
    {
        // Dispose old if any
        if (blocks.IsCreated)
            blocks.Dispose();

        blocks = new NativeArray<BlockType>(blocksData.Length, Allocator.Persistent);
        blocks.CopyFrom(blocksData);
        chunkCoord = coord;
        return this;
    }


    public void BuildMeshFromBlocks(NativeArray<BlockType> blocks, int chunkSize)
    {
        var verts = new NativeList<Vertex>(Allocator.TempJob);
        var tris = new NativeList<int>(Allocator.TempJob);

        var job = new GenerateMeshDataJob
        {
            blocks = blocks,
            chunkSize = chunkSize,
            vertices = verts,
            triangles = tris
        };

        job.Run(); // or Schedule().Complete() later

        Mesh mesh = new Mesh();
        var vertsVec3 = new List<Vector3>(verts.Length);
        var uvsVec2 = new List<Vector2>(verts.Length);

        for (int i = 0; i < verts.Length; i++)
        {
            vertsVec3.Add((Vector3)verts[i].position);
            uvsVec2.Add((Vector2)verts[i].uv);
        }

        var trisArray = new int[tris.Length];
        for (int i = 0; i < tris.Length; i++)
            trisArray[i] = tris[i];

        mesh.SetVertices(vertsVec3);
        mesh.SetTriangles(trisArray, 0);
        mesh.SetUVs(0, uvsVec2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;

        verts.Dispose();
        tris.Dispose();
    }


    private void AddCube(Vector3 pos, ref NativeList<Vector3> verts, ref NativeList<int> tris, ref NativeList<Vector2> uvs, ref int faceCount)
    {
        Vector3[] cubeVerts = {
            pos + new Vector3(0,0,0), pos + new Vector3(1,0,0), pos + new Vector3(1,1,0), pos + new Vector3(0,1,0), // front
            pos + new Vector3(0,0,1), pos + new Vector3(1,0,1), pos + new Vector3(1,1,1), pos + new Vector3(0,1,1)  // back
        };

        int[][] cubeFaces = {
            new[]{0,1,2,3}, // Front
            new[]{5,4,7,6}, // Back
            new[]{4,0,3,7}, // Left
            new[]{1,5,6,2}, // Right
            new[]{3,2,6,7}, // Top
            new[]{4,5,1,0}  // Bottom
        };

        foreach (var face in cubeFaces)
        {
            int startIndex = verts.Length;
            verts.Add(cubeVerts[face[0]]);
            verts.Add(cubeVerts[face[1]]);
            verts.Add(cubeVerts[face[2]]);
            verts.Add(cubeVerts[face[3]]);

            tris.Add(startIndex + 0);
            tris.Add(startIndex + 1);
            tris.Add(startIndex + 2);
            tris.Add(startIndex + 0);
            tris.Add(startIndex + 2);
            tris.Add(startIndex + 3);

            uvs.Add(new Vector2(0,0));
            uvs.Add(new Vector2(1,0));
            uvs.Add(new Vector2(1,1));
            uvs.Add(new Vector2(0,1));

            faceCount++;
        }
    }

    //
    // // Check if this chunk is obscured / occluded by its neighbors
    // private bool HiddenByNeighbors(Vector3Int pos, Dictionary<Vector3Int, Chunk> allChunks)
    // {
    //     
    //     foreach (var dir in WorldUtils.neighborDirs)
    //     {
    //         if (!allChunks.TryGetValue(pos + dir, out Chunk neighbor))
    //             return false;
    //
    //         if (!IsNeighborWallSolid(neighbor, -dir)) // -dir because you want the side facing this chunk
    //             return false;
    //     }
    //
    //     return true;
    //     
    // }
    //
    //
    // public bool IsNeighborWallSolid(Chunk neighbor, Vector3Int direction)
    // {
    //     // The position on the face to check.
    //     int x, y, z;
    //
    //     // Iterate over the two dimensions of the face
    //     for (int i = 0; i < CHUNK_SIZE; i++)
    //     {
    //         for (int j = 0; j < CHUNK_SIZE; j++)
    //         {
    //             // Determine the constant axis and the iterating axes
    //             if (direction.x != 0) // Checking the face on the X-axis
    //             {
    //                 // The X coordinate is constant (at the boundary)
    //                 x = direction.x > 0 ? 0 : CHUNK_SIZE - 1;
    //                 y = i;
    //                 z = j;
    //             }
    //             else if (direction.y != 0) // Checking the face on the Y-axis
    //             {
    //                 // The Y coordinate is constant
    //                 x = i;
    //                 y = direction.y > 0 ? 0 : CHUNK_SIZE - 1;
    //                 z = j;
    //             }
    //             else // Checking the face on the Z-axis
    //             {
    //                 // The Z coordinate is constant
    //                 x = i;
    //                 y = j;
    //                 z = direction.z > 0 ? 0 : CHUNK_SIZE - 1;
    //             }
    //
    //             // Check if the block is air.
    //             // If even one block on the face is air, the wall is not solid.
    //             if (neighbor.blocks[CoordsToIndex(x, y, z)] == BlockType.Air)
    //                 return false;
    //         }
    //     }
    //
    //     // If the loop completes, it means no air blocks were found.
    //     return true;
    // }

}
