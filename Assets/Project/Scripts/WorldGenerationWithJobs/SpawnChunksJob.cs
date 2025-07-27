using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct SpawnChunksJob : IJob
{
    public int3 playerChunkCoord;
    public int renderDistance;

    public NativeList<int3> chunkCoords;

    public void Execute()
    {
        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int y = 0; y < 1; y++) // Flat terrain for now
            {
                for (int z = -renderDistance; z <= renderDistance; z++)
                {
                    int3 coord = new int3(
                        playerChunkCoord.x + x,
                        playerChunkCoord.y + y,
                        playerChunkCoord.z + z
                    );
                    chunkCoords.Add(coord);
                }
            }
        }
    }
}