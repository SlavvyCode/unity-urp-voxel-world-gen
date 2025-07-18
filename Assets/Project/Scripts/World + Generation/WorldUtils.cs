using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldUtils
{
    // Utility methods for world generation

    // Check if a block is solid
    public static bool IsBlockSolidAndInChunk(int x, int y, int z, BlockType[] flatBlocks, int dim)
    {
        if (x < 0 || y < 0 || z < 0 || x >= dim || y >= dim || z >= dim)
            return false;

        int index = x * dim * dim + y * dim + z;
        return flatBlocks[index] != BlockType.Air;
    }

 
}