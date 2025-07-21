using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
    private Vector3Int chunkCoord; // coordinates of the chunk in the world
    private float perlinScale; // scale for Perlin noise
    // chunks
    private Vector2 perlinOffset; // offset for Perlin noise to create variation
    public void Awake()
    { 
        
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.enabled = false; // Start hidden and ONLY show when there is a reason to display the mesh (player looks at it)

        //
        // GenerateSemiRandomBlocks();
        // GenerateMesh();
    }
    
    private static readonly FaceData[] faces = WorldUtils.faces;
    
    public void Update()
    {
        
        // FRUSTUM CULLING!
        
        
        // Each frame:
            // Gets the camera's view frustum as a set of 6 clipping planes
            // Compares them to the chunk’s bounding box
            // Enables rendering only if it's inside the frustum
        // unity already does frustum culling BUT it's per gameobject, so if we spawn chunks in one gameobject or mesh, it won't work.
        if (Camera.main != null)
            
            //todo inefficient in update
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
    BlockType[] blocks;


    // CHUNK_SIZE for x and y and z


    //code adds 4 verts for only unobscured faces by solid blocks
    // each vert has own info - norm, uv (which area of the atlast texture to sue)
// add    normals for proper shading


// mesh then assigned to a chunk object to get rendered

// THEN add frustum culling etc..
//


// -------------



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
    public void GenerateMesh( Dictionary<Vector3Int,Chunk>chunks)
    {
        List<Vertex> vertices = new List<Vertex>();
        List<int> triangles = new List<int>();

        // if player is inside this chunk, it MUST be rendered.
        if(!PlayerInside() && (IsHiddenByNeighbors(chunkCoord, chunks)))
            return;
        
        // Step 1: Face culling & vertex generation
        ForEachBlockInChunk((x, y, z) =>
        {
            
            
            if (WorldUtils.IsBlockSolidAndInChunk(x, y, z, blocks, CHUNK_SIZE)
                // && 
                // todo Occlusion culling if this chunk is obscured by another chunk
                )
            {
                AddVisibleFaces(x, y, z, vertices, triangles);
            }
        });
        
        // if blocks empty or just air, skip mesh generation
        if (vertices.Count == 0)
        {
            meshRenderer.enabled = false; // Hide the mesh renderer if no visible blocks
            // Debug.LogWarning("No visible blocks in this chunk, skipping mesh generation.");
            return;
        }
        
        
        // Build the mesh
        ApplyMeshData(vertices, triangles);
    }

    private bool PlayerInside()
    {
        Vector3 playerPos = Camera.main.transform.position; // Assuming the camera is the player
        Vector3Int playerChunkCoord = WorldUtils.GetChunkCoords(playerPos);
        
        return playerChunkCoord == chunkCoord;
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
        int totalTiles = Enum.GetValues(typeof(BlockType)).Length;

        
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
            //todo backface culling MIGHT not be needed ~ just the fact that the triangles are not faced towards the camera is enough to not render them.
            // theoretically we could save some small performance by not adding triangles that are not facing the camera in the first place.
            
            
            Vector3Int neighborPos = new Vector3Int(x, y, z) + face.direction;
            
            //is this neigbor pos chunk border or air? add face
            // todo this is inefficient once there will be multiple chunks together which will block each other
            if (!IsInBounds(neighborPos) ||
                !WorldUtils.IsBlockSolidAndInChunk(neighborPos.x, neighborPos.y, neighborPos.z, blocks, CHUNK_SIZE))
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
    
    // Check if this chunk is obscured / occluded by its neighbors
    private bool IsHiddenByNeighbors(Vector3Int pos, Dictionary<Vector3Int, Chunk> allChunks)
    {
        
        foreach (var dir in WorldUtils.neighborDirs)
        {
            if (!allChunks.TryGetValue(pos + dir, out Chunk neighbor))
                return false;

            if (!IsNeighborWallSolid(neighbor, -dir)) // -dir because you want the side facing this chunk
                return false;
        }

        return true;
        
    }

    //todo is this done multiple times in the class? perhaps shoudl be stored.
    private bool IsClosed()
    {
        for (int x = 0; x < CHUNK_SIZE; x++)
        for (int y = 0; y < CHUNK_SIZE; y++)
        for (int z = 0; z < CHUNK_SIZE; z++)
        {
            // Optional: restrict this to just the border blocks
            if (IsBorder(x, y, z) && blocks[ToIndex(x, y, z)] == BlockType.Air)
                return false;
        }

        return true; // No air blocks on the border → chunk is closed
    }
    public bool IsNeighborWallSolid(Chunk neighbor, Vector3Int direction)
    {
        // The position on the face to check.
        int x, y, z;
    
        // Iterate over the two dimensions of the face
        for (int i = 0; i < CHUNK_SIZE; i++)
        {
            for (int j = 0; j < CHUNK_SIZE; j++)
            {
                // Determine the constant axis and the iterating axes
                if (direction.x != 0) // Checking the face on the X-axis
                {
                    // The X coordinate is constant (at the boundary)
                    x = direction.x > 0 ? 0 : CHUNK_SIZE - 1;
                    y = i;
                    z = j;
                }
                else if (direction.y != 0) // Checking the face on the Y-axis
                {
                    // The Y coordinate is constant
                    x = i;
                    y = direction.y > 0 ? 0 : CHUNK_SIZE - 1;
                    z = j;
                }
                else // Checking the face on the Z-axis
                {
                    // The Z coordinate is constant
                    x = i;
                    y = j;
                    z = direction.z > 0 ? 0 : CHUNK_SIZE - 1;
                }

                // Check if the block is air.
                // If even one block on the face is air, the wall is not solid.
                if (neighbor.blocks[ToIndex(x, y, z)] == BlockType.Air)
                    return false;
            }
        }

        // If the loop completes, it means no air blocks were found.
        return true;
    }


    private bool IsBorder(int x, int y, int z)
    {
        return x == 0 || x == CHUNK_SIZE - 1 ||
               y == 0 || y == CHUNK_SIZE - 1 ||
               z == 0 || z == CHUNK_SIZE - 1;
    }
    private bool IsInBounds(Vector3Int neighborPos)
    {
        return neighborPos.x >= 0 && neighborPos.x < CHUNK_SIZE &&
               neighborPos.y >= 0 && neighborPos.y < CHUNK_SIZE &&
               neighborPos.z >= 0 && neighborPos.z < CHUNK_SIZE;
    }


public void Initialize(int worldSeed, Vector3Int chunkCoord, Dictionary<Vector3Int, Chunk>chunks , float terrainRoughness, 
                        float baseHeight, float heightVariation, int noiseLayers)
{
    this.seed = worldSeed;
    this.chunkCoord = chunkCoord;
    this.perlinScale = terrainRoughness;
    
    transform.position = new Vector3(
        chunkCoord.x * CHUNK_SIZE, 
        chunkCoord.y * CHUNK_SIZE, 
        chunkCoord.z * CHUNK_SIZE
    );
    
    perlinOffset = new Vector2(
        Mathf.Sin(seed * 0.1f) * 1000f,
        Mathf.Cos(seed * 0.1f) * 1000f
    );
    
    GenerateBlocks(baseHeight, heightVariation, noiseLayers);
}

private void GenerateBlocks(float baseHeight, float heightVariation, int noiseLayers)
{
    blocks = new BlockType[CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE];
    
    // Calculate world position of this chunk's base
    int worldBaseY = chunkCoord.y * CHUNK_SIZE;
    
    for (int x = 0; x < CHUNK_SIZE; x++)
    for (int z = 0; z < CHUNK_SIZE; z++)
    {
        int worldX = chunkCoord.x * CHUNK_SIZE + x;
        int worldZ = chunkCoord.z * CHUNK_SIZE + z;
        
        // Calculate terrain height at this (x,z) position
        float height = CalculateTerrainHeight(worldX, worldZ, baseHeight, heightVariation, noiseLayers);
        
        // Fill blocks from bottom up to the calculated height
        for (int y = 0; y < CHUNK_SIZE; y++)
        {
            int worldY = worldBaseY + y;
            int index = ToIndex(x, y, z);
            
            if (worldY < height)
            {
                // Different block types based on height
                if (worldY >= height - 1) 
                    blocks[index] = BlockType.Grass;
                else if (worldY >= height - 4)
                    blocks[index] = BlockType.Dirt;
                else
                    blocks[index] = BlockType.Stone;
            }
            else
            {
                blocks[index] = BlockType.Air;
            }
        }
    }
}




private float CalculateTerrainHeight(int worldX, int worldZ, float baseHeight, float heightVariation, int layers)
{
    float sampleX = (worldX + perlinOffset.x) * perlinScale;
    float sampleZ = (worldZ + perlinOffset.y) * perlinScale;

    float total = 0f;
    float max = 0f;
    float amplitude = 1f;
    float frequency = 1f;

    for (int i = 0; i < layers; i++)
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