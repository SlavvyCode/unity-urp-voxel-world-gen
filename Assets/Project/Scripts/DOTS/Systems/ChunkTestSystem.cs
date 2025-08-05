using Project.Scripts.DOTS.Other;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static Project.Scripts.DOTS.Other.DOTS_Utils;
[BurstCompile]
public partial struct ChunkTestSystem : ISystem
{
    // public void OnCreate(ref SystemState state) 

    public void OnUpdate(ref SystemState state) 
    {
        foreach (var (blocks, chunk) in 
                 SystemAPI.Query<
                     DynamicBuffer<DOTS_Block>, 
                     RefRO<DOTS_Chunk>
                 >())
        {
            // Clear existing blocks (optional)
            blocks.Clear();

            // Fill first layer with grass
            for (int x = 0; x < CHUNK_SIZE; x++)
            for (int y = 0; y < CHUNK_SIZE; y++)
            for (int z = 0; z < CHUNK_SIZE; z++)
            {

                var value = BlockType.Air;
                if(y==0)
                {
                    value = BlockType.Grass;
                }
                else if (y == 1)
                {
                    value = BlockType.Stone;

                }      else if (y == 2)
                {
                    value = BlockType.Dirt;

                }
            
                blocks.Add(new DOTS_Block { Value = value });
                
            }

        }
    }
}