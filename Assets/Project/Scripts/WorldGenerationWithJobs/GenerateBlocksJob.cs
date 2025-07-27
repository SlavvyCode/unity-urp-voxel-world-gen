using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

using static VoxelConstants; 

[BurstCompile]
public struct GenerateBlocksJob : IJobParallelFor
{
    public int worldSeed;
    public Vector3Int chunkCoord;
    public float terrainRoughness;
    public float baseHeight;
    public float heightVariation;
    public int noiseLayers;

    // This NativeArray will hold the generated block data
    public NativeArray<BlockType> blocks;

    public void Execute(int index)
    {
        int x = index % CHUNK_SIZE;
        int y = (index / CHUNK_SIZE) % CHUNK_SIZE;
        int z = index / (CHUNK_SIZE * CHUNK_SIZE);
        
        // Calculate world position of this block
        int worldX = chunkCoord.x * CHUNK_SIZE + x;
        int worldY = chunkCoord.y * CHUNK_SIZE + y;
        int worldZ = chunkCoord.z * CHUNK_SIZE + z;

        // Calculate terrain height
        float height = CalculateTerrainHeight(worldX, worldZ);
        
        if (worldY < height)
        {
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

    private float CalculateTerrainHeight(int worldX, int worldZ)
    {
        //stolen from chunk.cs
        float perlinScale = terrainRoughness;
        Vector2 perlinOffset = new Vector2(
            Mathf.Sin(worldSeed * 0.1f) * 1000f,
            Mathf.Cos(worldSeed * 0.1f) * 1000f
        );
        
        float sampleX = (worldX + perlinOffset.x) * perlinScale;
        float sampleZ = (worldZ + perlinOffset.y) * perlinScale;

        float total = 0f;
        float max = 0f;
        float amplitude = 1f;
        float frequency = 1f;

        for (int i = 0; i < noiseLayers; i++)
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