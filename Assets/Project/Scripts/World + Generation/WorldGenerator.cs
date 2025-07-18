using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static VoxelConstants;

public class WorldGenerator : MonoBehaviour
{
    public GameObject chunkPrefab;
    public int renderDistance = 2; // chunks radius
    public Transform player;

    private Vector2Int playerChunkCoords = new Vector2Int(int.MinValue, int.MinValue);
    private Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    [SerializeField] private bool randomizeSeed;
    
    [SerializeField] private int worldSeed = 0; 
    
    
    // 0.005f → Very large hills
    // 0.02f → Natural looking hills
    // 0.1f → Small noisy bumps
    
    [Tooltip("smaller values = more detail\n" +
             "larger values = smoother terrain")]
    
    [Range(0.005f,1f)]
    [SerializeField] private float perlinScale = .5f; // Scale for Perlin noise
    private void Start()
    {
        //randomize seed
        if (randomizeSeed)
        {
            worldSeed = UnityEngine.Random.Range(0, int.MaxValue);
            Debug.Log("Randomized World Seed: " + worldSeed);
        }
        
        
    }

    void Update()
    {
        Vector2Int playerChunk = new Vector2Int(
            Mathf.FloorToInt(player.position.x / CHUNK_SIZE),
            Mathf.FloorToInt(player.position.z / CHUNK_SIZE)
        );
        
        if (playerChunk != playerChunkCoords)
        {
            playerChunkCoords = playerChunk;
            UpdateVisibleChunks(playerChunkCoords);
        }
    }

    void UpdateVisibleChunks(Vector2Int center)
    {
        // Unload distant chunks
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var kvp in chunks)
        {
            if (Vector2Int.Distance(kvp.Key, center) > renderDistance)
            {
                Destroy(kvp.Value.gameObject);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove) chunks.Remove(key);

        // Load new chunks
        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int z = -renderDistance; z <= renderDistance; z++)
            {
                Vector2Int coord = new Vector2Int(center.x + x, center.y + z);
                if (!chunks.ContainsKey(coord))
                {
                    Vector3 worldPos = new Vector3(
                        coord.x * CHUNK_SIZE,
                        0,
                        coord.y * CHUNK_SIZE
                    );
                    
                    GameObject chunkObj = Instantiate(
                        chunkPrefab,
                        worldPos,
                        Quaternion.identity
                    );
                    
                    Chunk chunk = chunkObj.GetComponent<Chunk>();
                    chunk.Initialize(worldSeed, coord, perlinScale);
                    chunks.Add(coord, chunk);
                }
            }
        }
    }}
