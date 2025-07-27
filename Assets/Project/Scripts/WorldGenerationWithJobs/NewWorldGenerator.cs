using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;
using static VoxelConstants;
using static WorldUtils;

public class NewWorldGenerator : MonoBehaviour
{
    [Tooltip("smaller values = more detail\n" +
             "larger values = smoother terrain")]
    [Range(0.005f, 1f)]
    [SerializeField]
    private float perlinScale = .5f; // Scale for Perlin noise


    [Header("Core Settings")]
    public GameObject chunkPrefab;
    public Transform player;
    public int renderDistance = 2;

    
    [Header("Terrain Shape")]
    [Tooltip("Base height of the terrain (world units)")]
    [SerializeField] private float baseHeight = 48f;
    
    [Tooltip("Height variation amplitude (world units)")]
    [SerializeField] private float heightVariation = 64f;
    
    [Tooltip("Controls how quickly terrain features change (smaller = smoother)")]
    [Range(0.001f, 0.1f)]
    [SerializeField] private float terrainRoughness = 0.02f;
    
    [Header("Advanced")]
    [SerializeField] private bool randomizeSeed = true;
    [SerializeField] private int seed = 0;
    [Tooltip("How many noise layers to combine (more = more detail)")]
    [Range(1, 8)] 
    [SerializeField] private int noiseLayers = 4;
    
    
    private Vector3Int playerChunkCoords = new Vector3Int(0, 0, 0); // Player's current chunk coordinates
    public  Dictionary<Vector3Int, NewChunk> chunks = new Dictionary<Vector3Int, NewChunk>();
    private newHeightCache heightCache;

    private NativeArray<Int3FaceDataForBurst> _faceDataForJobs; 
    
    
    // Changed this to match the key type used in `chunks` for consistency
    private Dictionary<Vector3Int, NewChunk> activeChunks = new(); 

    private void Start()
    {
        if (randomizeSeed)
        {
            seed = UnityEngine.Random.Range(0, int.MaxValue);
            Debug.Log("Randomized World Seed: " + seed);
        }
        // heightCache = new HeightCache(worldSeed, perlinScale);
        heightCache = new newHeightCache(seed, terrainRoughness, baseHeight, heightVariation, noiseLayers);

        
        _faceDataForJobs = new NativeArray<Int3FaceDataForBurst>(
            WorldUtils.AllFaceDataForBurst, // Your source C# array
            Allocator.Persistent             // Specifies that this memory should persist
            // until manually disposed
        );
    }    
    void OnDestroy()
    {
        // IMPORTANT: Dispose the NativeArray when the GameObject is destroyed
        if (_faceDataForJobs.IsCreated) // Check if it was actually created
        {
            _faceDataForJobs.Dispose();
        }

        // Also dispose all blocks from remaining active chunks if the world generator is destroyed
        foreach (var chunk in chunks.Values)
        {
            if (chunk != null) // Ensure the chunk object still exists
            {
                // OnDestroy will be called when the GameObject is destroyed, but if it's not destroyed
                // or if we somehow keep references, explicitly dispose here.
                // However, the Destroy(kvp.Value.gameObject) in SpawnChunksAroundPosition should handle it
                // for chunks that go out of range. This is mainly for when the WorldGenerator itself is destroyed.
                if (chunk.blocks.IsCreated)
                {
                    chunk.blocks.Dispose();
                }
                Destroy(chunk.gameObject); // Ensure game objects are cleaned up
            }
        }
        chunks.Clear(); // Clear the dictionary after disposing/destroying
        activeChunks.Clear(); // Ensure this is also cleared
    }
    void Update()
    {
        Vector3Int currentChunk = WorldUtils.GetChunkCoords(player.position);
        if (currentChunk != playerChunkCoords)
        {
            playerChunkCoords = currentChunk;
            SpawnChunksAroundPosition(playerChunkCoords);
        }
    }
    
    private void SpawnChunksAroundPosition(Vector3Int center)
    {

        // Remove distant chunks relative to the new center position
        List<Vector3Int> toRemove = new List<Vector3Int>();
        foreach (var kvp in chunks)
        {
            // Use sqrMagnitude for distance comparison for performance
            if ((kvp.Key - center).sqrMagnitude > renderDistance * renderDistance)
            {
                Destroy(kvp.Value.gameObject); // This will trigger NewChunk's OnDestroy
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
        {
            chunks.Remove(key);
            // Crucially, remove from activeChunks too if it's being used for active tracking
            // Make sure the key type matches (Vector3Int)
            activeChunks.Remove(key); 
        }

        
        
        
        
        // Gather chunks to spawn
        List<Vector3Int> chunksToSpawnCoords = new List<Vector3Int>();
        //todo i can probably increment by chunk_size instead of 1.
        for (int dx = -renderDistance; dx <= renderDistance; dx++)
        for (int dz = -renderDistance; dz <= renderDistance; dz++)
        {
            if (dx * dx + dz * dz > renderDistance * renderDistance)
                continue;
            
//occlusion culling done inside chunk.cs            

            Vector2Int column = new Vector2Int(center.x + dx, center.z + dz);
            float maxHeight = heightCache.GetMaxHeight(column);
            int maxChunkY = Mathf.FloorToInt((maxHeight - 1) / CHUNK_SIZE);

            
            //todo i can probably increment by chunk_size instead of 1.
            // we have to start and end indexing somewhere, 
            int minY = Mathf.Max(0, center.y - renderDistance);
            int maxY = Mathf.Min(maxChunkY, center.y + renderDistance);

            
            for (int y = minY; y <= maxY; y++)
            {
                Vector3Int chunkCoord = new Vector3Int(column.x, y, column.y);

                if ((chunkCoord - center).sqrMagnitude > renderDistance * renderDistance)
                    continue;

                // Check both dictionaries, though `chunks` is the primary one for spawned instances
                if (!chunks.ContainsKey(chunkCoord)) 
                    chunksToSpawnCoords.Add(chunkCoord);
            }
        }
        // Sort by distance to center (closest first)
        chunksToSpawnCoords.Sort((a, b) => 
            ((a - center).sqrMagnitude).CompareTo((b - center).sqrMagnitude));
        
        // Queue<Vector3Int> chunkSpawnQueue = new Queue<Vector3Int>();

        // Enqueue sorted chunks
        // chunkSpawnQueue.Clear();
        // foreach (var chunkCoord in chunksToSpawn)
        // {
            // chunkSpawnQueue.Enqueue(chunkCoord);
        // }

        // chunksToSpawn.ForEach(coord => 
        //         {
        //             if (!activeChunks.ContainsKey(coord))
        //             {
        //                 GenerateChunkAt(coord);
        //             }
        //         });
                        

        
        // WAIT for all the chunks and blocks to be generated before rendering
        List<JobHandle> handleList = new();
        List<NativeArray<BlockType>> blockResults = new();
        List<NewChunk> chunksToRender = new();
        List<Vector3Int> chunkCoords = new(); // Use Vector3Int here for consistency with chunkDict keys

        foreach (var coord in chunksToSpawnCoords)
        {
            var blocks = new NativeArray<BlockType>(CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE, Allocator.TempJob);

            var generateBlocksJob = new GenerateBlocksJob
            {
                worldSeed = this.seed,
                chunkCoord = coord,
                terrainRoughness = terrainRoughness,
                baseHeight = baseHeight,
                heightVariation = heightVariation,
                noiseLayers = noiseLayers,
                blocks = blocks
            };

            var handle = generateBlocksJob.Schedule(CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE, 64);
            handleList.Add(handle);

            blockResults.Add(blocks);
            chunkCoords.Add(coord);
        }

        for (int i = 0; i < chunkCoords.Count; i++)
        {
            Vector3Int coord = chunkCoords[i];
            Vector3 worldPos = (Vector3)(coord * CHUNK_SIZE);
            
            GameObject chunkObj = Instantiate(chunkPrefab, worldPos, Quaternion.identity);
            NewChunk chunk = chunkObj.GetComponent<NewChunk>();
            chunk.CHUNK_SIZE = CHUNK_SIZE; // Set the chunk size
            chunks[coord] = chunk;
            activeChunks[coord] = chunk; // Add to activeChunks here too
            chunksToRender.Add(chunk);
        }

        var handleArray = new NativeArray<JobHandle>(handleList.Count, Allocator.Temp);
        for (int i = 0; i < handleList.Count; i++)
            handleArray[i] = handleList[i];
        JobHandle.CompleteAll(handleArray);
        handleArray.Dispose();

// Now that all blocks are safe to read, initialize and render
        for (int i = 0; i < chunksToRender.Count; i++)
        {
            var chunk = chunksToRender[i];
            var coord = chunksToSpawnCoords[i];
            chunk.Initialize(blockResults[i], new int3(coord.x, coord.y, coord.z));
        }

        
        GenerateMeshesForChunks(chunksToRender);
        for (int i = 0; i < blockResults.Count; i++)
        {
            if (blockResults[i].IsCreated)
                blockResults[i].Dispose();
        }

    }

    private void GenerateMeshesForChunks(List<NewChunk> chunksToRender)
    {
        var handles = new NativeList<JobHandle>(chunksToRender.Count, Allocator.TempJob);
        List<NativeList<Vertex>> vertsList = new();
        List<NativeList<int>> trisList = new();

        for (int i = 0; i < chunksToRender.Count; i++)
        {
            var chunk = chunksToRender[i];

            var verts = new NativeList<Vertex>(Allocator.Persistent);
            var tris = new NativeList<int>(Allocator.Persistent);

            // use local copies, not shared lists
            var job = new ChunkMeshGenerationJob
            {
                blocks = chunk.blocks,
                chunkCoord = chunk.chunkCoord,
                chunkSize = CHUNK_SIZE,
                facesToUse = _faceDataForJobs,
                vertices = verts,
                triangles = tris
            };
            Debug.Assert(handles.IsCreated);
            Debug.Assert(chunk.blocks.IsCreated);
            Debug.Assert(verts.IsCreated);
            Debug.Assert(tris.IsCreated);

            JobHandle handle = job.Schedule(); // sequentially safe
            handles.Add(handle);
            vertsList.Add(verts);
            trisList.Add(tris);
        }

        // Important: wait for all jobs BEFORE using results
        JobHandle.CompleteAll(handles);
        handles.Dispose();
        for (int i = 0; i < chunksToRender.Count; i++)
        {
            BuildUnityMesh(vertsList[i], trisList[i], chunksToRender[i]);

            vertsList[i].Dispose();
            trisList[i].Dispose();
        }
    }


    private void BuildUnityMesh(NativeList<Vertex> verts, NativeList<int> tris, NewChunk chunk)
    {
        Mesh mesh = new Mesh();

        var vertsVec3 = new List<Vector3>(verts.Length);
        var uvsVec2 = new List<Vector2>(verts.Length);

        for (int i = 0; i < verts.Length; i++)
        {
            vertsVec3.Add((Vector3)verts[i].position);
            uvsVec2.Add((Vector2)verts[i].uv);
        }

        mesh.SetVertices(vertsVec3);
        mesh.SetTriangles(tris.AsArray().ToArray(), 0);
        mesh.SetUVs(0, uvsVec2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        chunk.GetComponent<MeshFilter>().mesh = mesh;
        

    }


    
    private NativeArray<int3> GetChunkCoords(List<NewChunk> chunks, Allocator allocator)
    {
        var coords = new NativeArray<int3>(chunks.Count, allocator);
        for (int i = 0; i < chunks.Count; i++)
        {
            coords[i] = chunks[i].chunkCoord; // assuming NewChunk has int3 chunkCoord property
        }
        return coords;
    }

    
    private NativeArray<BlockType> ConcatenateChunkBlocks(List<NewChunk> chunks, int chunkSize, Allocator allocator)
    {
        int blocksPerChunk = chunkSize * chunkSize * chunkSize;
        int totalBlocks = chunks.Count * blocksPerChunk;
        var allBlocks = new NativeArray<BlockType>(totalBlocks, allocator);

        for (int i = 0; i < chunks.Count; i++)
        {
            NativeArray<BlockType> chunkBlocks = chunks[i].blocks; // assuming NewChunk.blocks is NativeArray<BlockType>
            int baseIndex = i * blocksPerChunk;
            for (int j = 0; j < blocksPerChunk; j++)
            {
                allBlocks[baseIndex + j] = chunkBlocks[j];
            }
        }

        return allBlocks;
    }
    // Check if this chunk is obscured / occluded by its neighbors
    private bool IsHiddenByNeighbors(Vector3Int pos, Dictionary<Vector3Int, NewChunk> allChunks)
    {
        
        foreach (var dir in WorldUtils.neighborDirs)
        {
            if (!allChunks.TryGetValue(pos + dir, out NewChunk neighbor))
                return false;

            if (!IsNeighborWallSolid(neighbor, -dir)) // -dir because you want the side facing this chunk
                return false;
        }

        return true;
        
    }

    public bool IsNeighborWallSolid(NewChunk neighbor, Vector3Int direction)
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
                if (neighbor.blocks[CoordsToIndex(x, y, z)] == BlockType.Air)
                    return false;
            }
        }

        // If the loop completes, it means no air blocks were found.
        return true;
    }


}