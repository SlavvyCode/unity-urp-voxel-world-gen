using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static VoxelConstants;

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
    
    
    
    public static readonly Vector3Int[] neighborDirs = new Vector3Int[]
    {
        new Vector3Int( 1, 0, 0), // +X
        new Vector3Int(-1, 0, 0), // -X
        new Vector3Int( 0, 1, 0), // +Y
        new Vector3Int( 0,-1, 0), // -Y
        new Vector3Int( 0, 0, 1), // +Z
        new Vector3Int( 0, 0,-1)  // -Z
    };

    
    
    public static readonly FaceData[] faces = new FaceData[]
    {
    //
    // | Face        | Constant Axis | Direction | What changes          |
    // | ----------- | ------------- | --------- | --------------------- |
    // | Top (+Y)    | Y = 1         | Up        | X and Z vary from 0→1 |
    // | Bottom (–Y) | Y = 0         | Down      | X and Z vary from 0→1 |
    // | Front (+Z)  | Z = 1         | Forward   | X and Y vary from 0→1 |
    // | Back (–Z)   | Z = 0         | Backward  | X and Y vary from 0→1 |
    // | Left (–X)   | X = 0         | Left      | Y and Z vary from 0→1 |
    // | Right (+X)  | X = 1         | Right     | Y and Z vary from 0→1 |

    // when it's wrong winding, you can swap inside the first and second pairs of the face
    //eg 
    //
    // A new Vector3(0, 1, 0), // front-left
    // B new Vector3(1, 1, 0), // front-right
    // C new Vector3(0, 1, 1), // back-left
    // D new Vector3(1, 1, 1)  // back-right    

    // B new Vector3(1, 1, 0), // front-right
    // A new Vector3(0, 1, 0), // front-left
    // D new Vector3(1, 1, 1)  // back-right    
    // C new Vector3(0, 1, 1), // back-left


    // OK
    // Top (+Y)
    new FaceData {
        direction = Vector3Int.up,
        normal = Vector3.up,
        cornerOffsets = new Vector3[] {
            new Vector3(0, 1, 0), // front-left
            new Vector3(1, 1, 0), // front-right
            new Vector3(0, 1, 1), // back-left
            new Vector3(1, 1, 1)  // back-right
        }
    },


    // Bottom (-Y)
    new FaceData {
        direction = Vector3Int.down,
        normal = Vector3.down,
        cornerOffsets = new Vector3[] {
            new Vector3(1, 0, 0),  // v0 (bottom-left in local -Y)
            new Vector3(0, 0, 0),  // v1 (bottom-right — flipped X)
            new Vector3(1, 0, 1),  // v2 (top-left — forward in Z)
            new Vector3(0, 0, 1),  // v3 (top-right)
        }
    },

    // Front (+Z)
    new FaceData {
        direction = Vector3Int.forward,
        normal = Vector3.forward,
        cornerOffsets = new Vector3[] {
            new Vector3(1, 0, 1),
            new Vector3(0, 0, 1),
            new Vector3(1, 1, 1),
            new Vector3(0, 1, 1),
        }
    },

    // OK
    // Back (-Z)
    new FaceData {
        direction = Vector3Int.back,
        normal = Vector3.back,
        cornerOffsets = new Vector3[] {
            new Vector3(0, 0, 0),  // bottom-left
            new Vector3(1, 0, 0),  // bottom-right
            new Vector3(0, 1, 0),  // top-left
            new Vector3(1, 1, 0)   // top-right
        }
    },

    // Left (-X)
    new FaceData {
        direction = Vector3Int.left,
        normal = Vector3.left,
        cornerOffsets = new Vector3[] {
            new Vector3(0, 0, 1),  // bottom-right
            new Vector3(0, 0, 0),  // bottom-left
            new Vector3(0, 1, 1),   // top-right
            new Vector3(0, 1, 0),  // top-left
        }
    },

    // Right (+X)
    new FaceData {
        direction = Vector3Int.right,
        normal = Vector3.right,
        cornerOffsets = new Vector3[] {
            new Vector3(1, 0, 0),  // bottom-right
            new Vector3(1, 0, 1),  // bottom-left
            new Vector3(1, 1, 0),   // top-right
            new Vector3(1, 1, 1),  // top-left
        }
    }
    };

    public static Vector3Int GetChunkCoords(Vector3 position)
    {
        return new Vector3Int(
            Mathf.FloorToInt(position.x / CHUNK_SIZE),
            Mathf.FloorToInt(position.y / CHUNK_SIZE),
            Mathf.FloorToInt(position.z / CHUNK_SIZE)
        );
    }

 
}