using System.Collections.Generic;
using UnityEngine;
using static VoxelConstants;


public class newHeightCache
{
    private Dictionary<Vector2Int, float> maxHeights = new Dictionary<Vector2Int, float>();
    private readonly float roughness;
    private readonly float baseHeight;
    private readonly float variation;
    private readonly int layers;
    private readonly Vector2 noiseOffset;

    public newHeightCache(int seed, float roughness, float baseHeight, float variation, int layers)
    {
        this.roughness = roughness;
        this.baseHeight = baseHeight;
        this.variation = variation;
        this.layers = layers;
        
        // Create a consistent noise offset based on seed
        noiseOffset = new Vector2(
            Mathf.Sin(seed * 0.1f) * 1000f,
            Mathf.Cos(seed * 0.1f) * 1000f
        );
    }

    public float GetMaxHeight(Vector2Int coord)
    {
        if (maxHeights.TryGetValue(coord, out float cached))
            return cached;

        float maxHeight = CalculateMaxHeightForColumn(coord);
        maxHeights[coord] = maxHeight;
        return maxHeight;
    }

    private float CalculateMaxHeightForColumn(Vector2Int coord)
    {
        float maxHeight = 0f;
        const int samples = 3; // Sample 3x3 points in the chunk

        for (int dx = 0; dx < samples; dx++)
        for (int dz = 0; dz < samples; dz++)
        {
            int worldX = coord.x * CHUNK_SIZE + dx * (CHUNK_SIZE / (samples - 1));
            int worldZ = coord.y * CHUNK_SIZE + dz * (CHUNK_SIZE / (samples - 1));

            float height = CalculateTerrainHeight(worldX, worldZ);
            maxHeight = Mathf.Max(maxHeight, height);
        }

        return maxHeight;
    }

    public float CalculateTerrainHeight(int worldX, int worldZ)
    {
        float sampleX = (worldX + noiseOffset.x) * roughness;
        float sampleZ = (worldZ + noiseOffset.y) * roughness;

        float total = 0f;
        float max = 0f;
        float amplitude = 1f;
        float frequency = 1f;

        for (int i = 0; i < layers; i++)
        {
            total += MyPerlin.Noise(sampleX * frequency, sampleZ * frequency) * amplitude;
            max += amplitude;
            amplitude *= 0.5f; // Each layer contributes half as much as the previous
            frequency *= 2f;  // Each layer has double the frequency
        }

        float normalized = total / max; // 0-1 range
        return baseHeight + (normalized * variation); // Scale to desired height range
    }
}