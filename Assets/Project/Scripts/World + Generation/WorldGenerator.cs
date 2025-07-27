using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using static VoxelConstants;

public class OldWorldGenerator : MonoBehaviour
{
    // 0.005f → Very large hills
    // 0.02f → Natural looking hills
    // 0.1f → Small noisy bumps

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
    [SerializeField] private int worldSeed = 0;
    [Tooltip("How many noise layers to combine (more = more detail)")]
    [Range(1, 8)] 
    [SerializeField] private int noiseLayers = 4;

    
    
    private Vector3Int playerChunkCoords = new Vector3Int(0, 0, 0); // Player's current chunk coordinates
    public  Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
    private newHeightCache heightCache;
    
    Queue<Vector3Int> chunkSpawnQueue = new Queue<Vector3Int>();
    bool isSpawning = false;


    
    //todo culling for entire chunks if obscured.

    
    private void Start()
    {
        //randomize seed
        if (randomizeSeed)
        {
            worldSeed = UnityEngine.Random.Range(0, int.MaxValue);
            Debug.Log("Randomized World Seed: " + worldSeed);
        }
        // heightCache = new HeightCache(worldSeed, perlinScale);
        heightCache = new newHeightCache(worldSeed, terrainRoughness, baseHeight, heightVariation, noiseLayers);
        
    }

    void Update()
    {
        Vector3Int currentChunk = WorldUtils.GetChunkCoords(player.position);
        if (currentChunk != playerChunkCoords)
        {
            playerChunkCoords = currentChunk;
            UpdateVisibleChunks(playerChunkCoords);
        }
    }

    void UpdateVisibleChunks(Vector3Int center)
    {
        // Remove distant chunks
        List<Vector3Int> toRemove = new List<Vector3Int>();
        foreach (var kvp in chunks)
        {
            if (Vector3Int.Distance(kvp.Key, center) > renderDistance)
            {
                Destroy(kvp.Value.gameObject);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove) chunks.Remove(key);

        // Gather chunks to spawn
        List<Vector3Int> chunksToSpawn = new List<Vector3Int>();

        for (int dx = -renderDistance; dx <= renderDistance; dx++)
        for (int dz = -renderDistance; dz <= renderDistance; dz++)
        {
            if (dx * dx + dz * dz > renderDistance * renderDistance)
                continue;
            
//occlusion culling done inside chunk.cs            

            Vector2Int column = new Vector2Int(center.x + dx, center.z + dz);
            float maxHeight = heightCache.GetMaxHeight(column);
            int maxChunkY = Mathf.FloorToInt((maxHeight - 1) / CHUNK_SIZE);

            int minY = Mathf.Max(0, center.y - renderDistance);
            int maxY = Mathf.Min(maxChunkY, center.y + renderDistance);

            for (int y = minY; y <= maxY; y++)
            {
                Vector3Int chunkCoord = new Vector3Int(column.x, y, column.y);

                if ((chunkCoord - center).sqrMagnitude > renderDistance * renderDistance)
                    continue;

                if (!chunks.ContainsKey(chunkCoord))
                    chunksToSpawn.Add(chunkCoord);
            }
        }

        // Sort by distance to center (closest first)
        chunksToSpawn.Sort((a, b) => 
            ((a - center).sqrMagnitude).CompareTo((b - center).sqrMagnitude));
        // Enqueue sorted chunks
        chunkSpawnQueue.Clear();
        foreach (var chunkCoord in chunksToSpawn)
        {
            chunkSpawnQueue.Enqueue(chunkCoord);
        }

        if (!isSpawning && chunkSpawnQueue.Count > 0)
            StartCoroutine(SpawnChunksFromQueue());
        
    }

    
    
    IEnumerator SpawnChunksFromQueue()
    {
        isSpawning = true;

        while (chunkSpawnQueue.Count > 0)
        {
            Vector3Int chunkCoord = chunkSpawnQueue.Dequeue();

            if (chunks.ContainsKey(chunkCoord))
                continue;

            Vector3 worldPos = new Vector3(
                chunkCoord.x * CHUNK_SIZE,
                chunkCoord.y * CHUNK_SIZE,
                chunkCoord.z * CHUNK_SIZE
            );

            GameObject chunkObj = Instantiate(chunkPrefab, worldPos, Quaternion.identity);
            Chunk chunk = chunkObj.GetComponent<Chunk>();
            // chunk.Initialize(worldSeed, chunkCoord, perlinScale);
            chunk.Initialize(worldSeed, chunkCoord,chunks, terrainRoughness, baseHeight, heightVariation, noiseLayers);
            chunks.Add(chunkCoord, chunk);
            chunk.GenerateMesh(chunks);
            

            yield return null; // wait one frame per chunk
        }

        isSpawning = false;
    }


    
}