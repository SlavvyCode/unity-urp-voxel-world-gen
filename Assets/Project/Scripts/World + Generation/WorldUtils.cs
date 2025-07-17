using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldUtils
{
    // Utility methods for world generation

    // Check if a block is solid
    public static bool IsBlockSolid(int x, int y, int z, BlockType[][][] blocks)
    {
        return blocks[x][y][z] != BlockType.Air;
    }
 
}