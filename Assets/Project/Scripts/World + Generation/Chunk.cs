using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

using static VoxelConstants;


public struct FaceData
{
    public Vector3Int direction; // direction of the face (e.g., up, down, left, etc.)
    public Vector3[] cornerOffsets; // 4 corners relative to block
    public Vector3 normal;
}


public class Chunk : MonoBehaviour
{
    [SerializeField] private Material blockMaterial; // drag this in the Inspector
    private MeshRenderer meshRenderer;
    private int seed; // for random generation
    private Vector2Int chunkCoord; // coordinates of the chunk in the world
    private float perlinScale; // scale for Perlin noise
    public void Awake()
    { 
        
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.enabled = false; // Start hidden and ONLY show when there is a reason to display the mesh (player looks at it)

        //
        // GenerateSemiRandomBlocks();
        // GenerateMesh();
    }


    // Call this from WorldGenerator right after Instantiate
    public void Initialize(int worldSeed, Vector2Int coord, float perlinScale)
    {
        this.perlinScale = perlinScale;

        seed = worldSeed;
        chunkCoord = coord;
        transform.position = new Vector3(chunkCoord.x * CHUNK_SIZE, 0, chunkCoord.y * CHUNK_SIZE);
        Debug.Log($"Chunk initialized at {transform.position}."); 
        
        GenerateBlocks();
        GenerateMesh();
    }
    
    
    void GenerateBlocks()
    {
        blocks = new BlockType[CHUNK_SIZE* CHUNK_SIZE * CHUNK_SIZE];

        // for each coordinate - each block - it generates a height based on Perlin noise
        for (int x = 0; x < CHUNK_SIZE; x++)
        for (int z = 0; z < CHUNK_SIZE; z++)
        {
            
            Vector2 offset = new Vector2(
                Mathf.Sin(seed * 0.1f) * 1000f,
                Mathf.Cos(seed * 0.1f) * 1000f
            );
            
            // World position of block
            int worldX = chunkCoord.x * CHUNK_SIZE + x;
            int worldZ = chunkCoord.y * CHUNK_SIZE + z;
            
            float sampleX = (worldX + offset.x) * perlinScale;
            float sampleZ = (worldZ + offset.y) * perlinScale;
            
            
            // Generate a noise value based on world position   
            float noise = Perlin.Noise(sampleX, sampleZ);            
            int height = Mathf.FloorToInt(noise * CHUNK_SIZE); // Scale to chunk height

            for (int y = 0; y < CHUNK_SIZE; y++)
            {
                blocks[ToIndex(x,y,z)] = y < height ? BlockType.Grass : BlockType.Air;
            }
        }
    }

    
    
    public void Update()
    {
        
        // FRUSTUM CULLING!
        
        
        // Each frame:
            // Gets the camera's view frustum as a set of 6 clipping planes
            // Compares them to the chunk’s bounding box
            // Enables rendering only if it's inside the frustum
        // unity already does frustum culling BUT it's per gameobject, so if we spawn chunks in one gameobject or mesh, it won't work.
        if (Camera.main != null)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
            // if the chunk's bounds are within the camera's frustum, enable the meshRenderer
            bool visible = GeometryUtility.TestPlanesAABB(planes, meshRenderer.bounds);
            meshRenderer.enabled = visible;
        }
        
        // TestPlanesAABB
            // AABB = Axis-Aligned Bounding Box (your chunk’s 3D box)
            // Unity tests: "Is this box inside or overlapping the view?"
    }


    // generates a single mesh merged together 
    // todo Optional future tip: switch to a flat BlockType[] with manual index math for performance. OR something else
    BlockType[] blocks;

//todo i do'nt quite understand i think? it'd be good to visualize this on a cube?
//like i need to make notes like in a math class to understand this properly
private static readonly FaceData[] faces = new FaceData[]
{
    //
    // | Face        | Constant Axis | Direction | What changes          |
    // | ----------- | ------------- | --------- | --------------------- |
    // | Top (+Y)    | Y = 1         | Up        | X and Z vary from 0→1 |
    // | Bottom (–Y) | Y = 0         | Down      | X and Z vary from 0→1 |
    // | Front (+Z)  | Z = 1         | Forward   | X and Y vary from 0→1 |
    // | Back (–Z)   | Z = 0         | Backward  | X and Y vary from 0→1 |
    // | Left (–X)   | X = 0         | Left      | Y and Z vary from 0→1 |
    // | Right (+X)  | X = 1         | Right     | Y and Z vary from 0→1 |

    // when it's wrong winding, you can swap inside the first and second pairs of the face
    //eg 
    //
    // A new Vector3(0, 1, 0), // front-left
    // B new Vector3(1, 1, 0), // front-right
    // C new Vector3(0, 1, 1), // back-left
    // D new Vector3(1, 1, 1)  // back-right    

    // B new Vector3(1, 1, 0), // front-right
    // A new Vector3(0, 1, 0), // front-left
    // D new Vector3(1, 1, 1)  // back-right    
    // C new Vector3(0, 1, 1), // back-left

    
    // OK
    // Top (+Y)
    new FaceData {
        direction = Vector3Int.up,
        normal = Vector3.up,
        cornerOffsets = new Vector3[] {
            new Vector3(0, 1, 0), // front-left
            new Vector3(1, 1, 0), // front-right
            new Vector3(0, 1, 1), // back-left
            new Vector3(1, 1, 1)  // back-right
        }
    },
  

    // Bottom (-Y)
    new FaceData {
        direction = Vector3Int.down,
        normal = Vector3.down,
        cornerOffsets = new Vector3[] {
            new Vector3(1, 0, 0),  // v0 (bottom-left in local -Y)
            new Vector3(0, 0, 0),  // v1 (bottom-right — flipped X)
            new Vector3(1, 0, 1),  // v2 (top-left — forward in Z)
            new Vector3(0, 0, 1),  // v3 (top-right)
        }
    },

    // Front (+Z)
    new FaceData {
        direction = Vector3Int.forward,
        normal = Vector3.forward,
        cornerOffsets = new Vector3[] {
            new Vector3(1, 0, 1),
            new Vector3(0, 0, 1),
            new Vector3(1, 1, 1),
            new Vector3(0, 1, 1),
        }
    },

    // OK
    // Back (-Z)
    new FaceData {
        direction = Vector3Int.back,
        normal = Vector3.back,
        cornerOffsets = new Vector3[] {
            new Vector3(0, 0, 0),  // bottom-left
            new Vector3(1, 0, 0),  // bottom-right
            new Vector3(0, 1, 0),  // top-left
            new Vector3(1, 1, 0)   // top-right
        }
    },

    // Left (-X)
    new FaceData {
        direction = Vector3Int.left,
        normal = Vector3.left,
        cornerOffsets = new Vector3[] {
            new Vector3(0, 0, 1),  // bottom-right
            new Vector3(0, 0, 0),  // bottom-left
            new Vector3(0, 1, 1),   // top-right
            new Vector3(0, 1, 0),  // top-left
        }
    },

    // Right (+X)
    new FaceData {
        direction = Vector3Int.right,
        normal = Vector3.right,
        cornerOffsets = new Vector3[] {
            new Vector3(1, 0, 0),  // bottom-right
            new Vector3(1, 0, 1),  // bottom-left
            new Vector3(1, 1, 0),   // top-right
            new Vector3(1, 1, 1),  // top-left
        }
    }
};


    // CHUNK_SIZE for x and y and z


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
        blocks = new BlockType[CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE];
        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int y = 0; y < CHUNK_SIZE; y++)
            {
                for (int z = 0; z < CHUNK_SIZE; z++)
                {
                    // Randomly assign block types for testing from the BlockType enum
                    // since blocs is a 2d array now, we use index math.
                    // essentially, every CHUNK_SIZE, we make a new row of blocks. 
                    blocks[x * CHUNK_SIZE * CHUNK_SIZE + y * CHUNK_SIZE + z] = 
                        (BlockType)Random.Range(0, Enum.GetValues(typeof(BlockType)).Length);
                    
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
        ForEachBlockInChunk((x, y, z) =>
        {
            if (WorldUtils.IsBlockSolidAndInChunk(x, y, z, blocks, CHUNK_SIZE))
            {
                AddVisibleFaces(x, y, z, vertices, triangles);
            }
        });
        // Build the mesh
        ApplyMeshData(vertices, triangles);
    }

    
    
    // frustum cullign - faces beyond FOV are discarded
    // then more culling (occlusion culling) by faces that are obscured by solid blocks are discarded as well (behind a hill)
    
    
    
    // THEN let's try to make some sort of actual world generation
    
    
    
    // also optimize vertexes themselves - positions can be represented as bytes (smallest data type) since they're whole numbers.
    
    
    //profiling as done in the video-  spam Stopwatches everywhere to see where the bottlenecks are - unity's default profiler is just everythings a big green  blob
    
    
    
    
    // OK
    // Iterate through each block in the chunk and apply the action 
    private void ForEachBlockInChunk(Action<int, int, int> action)
    {
        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int y = 0; y < CHUNK_SIZE; y++)
            {
                for (int z = 0; z < CHUNK_SIZE; z++)
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

    private int ToIndex(int x, int y, int z)
    {
        return x * CHUNK_SIZE * CHUNK_SIZE + y * CHUNK_SIZE + z;
    }


    // Check if neighboring blocks are solid (if yes, skip that face).
    // For visible faces, add 4 vertices and 6 triangle indices (2 triangles per quad).
    private void AddVisibleFaces(int x, int y, int z, List<Vertex> vertices, List<int> triangles)
    {
        //xyz is the position of the block in the chunk
        BlockType current = blocks[ToIndex(x, y, z)];

        foreach (var face in faces)
        {

            Vector3Int neighborPos = new Vector3Int(x, y, z) + face.direction;
            
            //is this neigbor pos chunk border or air? add face
            // todo this is inefficient once there will be multiple chunks together which will block each other
            if (!IsInBounds(neighborPos) ||
                !WorldUtils.IsBlockSolidAndInChunk(neighborPos.x, neighborPos.y, neighborPos.z, blocks, CHUNK_SIZE))
                // if (true)
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
                
                
                // todo make safe for parallelism with Jobs later?
                // so yeah we just split it up by threes and hope that the triangles were added  correctly. 
                triangles.Add(startIndex + 0);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 1);
                
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 3);
            }
        }
    }

    //  creates the Unity Mesh object and assigns it to a MeshFilter.
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

        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // This part was missing — assign the material
        if (blockMaterial != null)
            meshRenderer.material = blockMaterial;
        else
            Debug.LogWarning("Block material not assigned in the inspector.");
    }
    
    
    private bool IsInBounds(Vector3Int neighborPos)
    {
        return neighborPos.x >= 0 && neighborPos.x < CHUNK_SIZE &&
               neighborPos.y >= 0 && neighborPos.y < CHUNK_SIZE &&
               neighborPos.z >= 0 && neighborPos.z < CHUNK_SIZE;
    }
}