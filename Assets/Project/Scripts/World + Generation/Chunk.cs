using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public struct FaceData
{
    public Vector3Int direction; // direction of the face (e.g., up, down, left, etc.)
    public Vector3[] cornerOffsets; // 4 corners relative to block
    public Vector3 normal;
}


public class Chunk : MonoBehaviour
{
    [SerializeField] private Material blockMaterial; // drag this in the Inspector
    public int chunkDimensions = 16;

    public void Start()
    { 
        GenerateSemiRandomBlocks();
        GenerateMesh();
    }

    // generates a single mesh merged together 
    // todo Optional future tip: switch to a flat BlockType[] with manual index math for performance. OR something else
    BlockType[][][] blocks;

//todo i do'nt quite understand i think? it'd be good to visualize this on a cube?
//like i need to make notes like in a math class to understand this properly
    private static readonly FaceData[] faces = new FaceData[]
    {
        new FaceData
        {
            direction = Vector3Int.up,
            normal = Vector3.up,
            cornerOffsets = new Vector3[]
            {
                new Vector3(0, 1, 0), // front-left
                new Vector3(1, 1, 0), // front-right
                new Vector3(0, 1, 1), // back-left
                new Vector3(1, 1, 1) // back-right
            }
        },
        new FaceData
        {
            direction = Vector3Int.down,
            normal = Vector3.down,
            cornerOffsets = new Vector3[]
            {
                new Vector3(0, 0, 0), // front-left
                new Vector3(1, 0, 0), // front-right
                new Vector3(0, 0, 1), // back-left
                new Vector3(1, 0, 1) // back-right
            }
        },
        new FaceData
        {
            direction = Vector3Int.forward,
            normal = Vector3.forward,
            cornerOffsets = new Vector3[]
            {
                new Vector3(0, 0, 1),
                new Vector3(1, 0, 1),
                new Vector3(0, 1, 1),
                new Vector3(1, 1, 1)
            }
        },
        new FaceData
        {
            direction = Vector3Int.back,
            normal = Vector3.back,
            cornerOffsets = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0),
                new Vector3(1, 1, 0)
            }
        },
        new FaceData
        {
            direction = Vector3Int.left,
            normal = Vector3.left,
            cornerOffsets = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(0, 0, 1),
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 1)
            }
        },
        new FaceData
        {
            direction = Vector3Int.right,
            normal = Vector3.right,
            cornerOffsets = new Vector3[]
            {
                new Vector3(1, 0, 0),
                new Vector3(1, 0, 1),
                new Vector3(1, 1, 0),
                new Vector3(1, 1, 1)
            }
        }
    };

    // chunkdimensions for x and y and z


    //code adds 4 verts for only unobscured faces by solid blocks
    // each vert has own info - norm, uv (which area of the atlast texture to sue)
// add    normals for proper shading


// mesh then assigned to a chunk object to get rendered

// THEN add frustum culling etc..
//


    // Initialize block data
    // fill the 3D array with test patterns 
    public void GenerateSemiRandomBlocks()
    {
        blocks = new BlockType[chunkDimensions][][];
        for (int x = 0; x < chunkDimensions; x++)
        {
            blocks[x] = new BlockType[chunkDimensions][];
            for (int y = 0; y < chunkDimensions; y++)
            {
                blocks[x][y] = new BlockType[chunkDimensions];
                for (int z = 0; z < chunkDimensions; z++)
                {
                    // Randomly assign block types for testing from the BlockType enum
                    blocks[x][y][z] = (BlockType)Random.Range(0, System.Enum.GetValues(typeof(BlockType)).Length);
                }
            }
        }
    }

    // Main mesh construction pipeline
    public void GenerateMesh()
    {
        List<Vertex> vertices = new List<Vertex>();
        List<int> triangles = new List<int>();

        // Step 1: Face culling & vertex generation
        for (var i = 0; i < blocks.Length; i++)
        {
        }

        ForEachBlockInChunk((x, y, z) =>
        {
            if (WorldUtils.IsBlockSolid(x, y, z, blocks))
            {
                AddVisibleFaces(x, y, z, vertices, triangles);
            }
        });
        //
        // Step 2: Build the mesh
        ApplyMeshData(vertices, triangles);
    }

    
    // OK
    // Iterate through each block in the chunk and apply the action 
    private void ForEachBlockInChunk(Action<int, int, int> action)
    {
        for (int x = 0; x < chunkDimensions; x++)
        {
            for (int y = 0; y < chunkDimensions; y++)
            {
                for (int z = 0; z < chunkDimensions; z++)
                {
                    action(x, y, z);
                }
            }
        }
    }

    // OK
    // for a line-based texture atlas, calculate UV coordinates for a given block type and face.
    Vector2 GetBlockUV(BlockType type, int face)
    {
        // get the total number of block types in the enum
        // Enum.GetValues() returns an array of all values in the enum.
        int totalTiles = BlockType.GetValues(typeof(BlockType)).Length;

        
        float tileWidth = 1f / totalTiles;

        int index = (int)type;

        // Bottom-left corner of the tile (index * tileWidth on X, always 0 on Y)
        Vector2 uvMin = new Vector2(index * tileWidth, 0f);

        switch (face)
        {
            case 0: return uvMin + new Vector2(0, 0);                     // Bottom-left
            case 1: return uvMin + new Vector2(tileWidth, 0);             // Bottom-right
            case 2: return uvMin + new Vector2(0, 1f);                    // Top-left
            case 3: return uvMin + new Vector2(tileWidth, 1f);            // Top-right
        }

        throw new ArgumentException("Invalid face index");
    }



    // Check if neighboring blocks are solid (if yes, skip that face).
    // For visible faces, add 4 vertices and 6 triangle indices (2 triangles per quad).
    private void AddVisibleFaces(int x, int y, int z, List<Vertex> vertices, List<int> triangles)
    {
        //xyz is the position of the block in the chunk
        BlockType current = blocks[x][y][z];

        foreach (var face in faces)
        {
            Vector3Int neighborPos = new Vector3Int(x, y, z) + face.direction;
            
            //is this neigbor pos chunk border or air? add face
            // todo this is inefficient once there will be multiple chunks together which will block each other
            if (!IsInBounds(neighborPos) ||
                !WorldUtils.IsBlockSolid(neighborPos.x, neighborPos.y, neighborPos.z, blocks))
            {
                // If neighbor is out of bounds or not solid, add this face
                // Add 4 vertices for the face
                int startIndex = vertices.Count;

                for (int i = 0; i < 4; i++)
                {
                    Vector3 worldPos = new Vector3(x, y, z) + face.cornerOffsets[i];
                    Vector2 uv = GetBlockUV(current, i);
                    vertices.Add(new Vertex
                    {
                        position = worldPos,
                        normal = face.normal,
                        uv = uv
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
                
                
                // todo so yeah we just split it up by threes and hope that the triangles were added  correctly. it's unsafe for parallelism though
                triangles.Add(startIndex + 0);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 1);
                
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 3);
            }
        }
    }

    // todo what is this?
    // This is the part that actually creates the Unity Mesh object and assigns it to a MeshFilter.
    private void ApplyMeshData(List<Vertex> vertices, List<int> triangles)
    {
        Mesh mesh = new Mesh();

        Vector3[] positions = new Vector3[vertices.Count];
        Vector3[] normals = new Vector3[vertices.Count];
        Vector2[] uvs = new Vector2[vertices.Count];

        for (int i = 0; i < vertices.Count; i++)
        {
            positions[i] = vertices[i].position;
            normals[i] = vertices[i].normal;
            uvs[i] = vertices[i].uv;
        }

        mesh.vertices = positions;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles.ToArray();

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();

        // This part was missing — assign the material
        if (blockMaterial != null)
            mr.material = blockMaterial;
        else
            Debug.LogWarning("Block material not assigned in the inspector.");
    }
    
    
    private bool IsInBounds(Vector3Int neighborPos)
    {
        return neighborPos.x >= 0 && neighborPos.x < chunkDimensions &&
               neighborPos.y >= 0 && neighborPos.y < chunkDimensions &&
               neighborPos.z >= 0 && neighborPos.z < chunkDimensions;
    }
}