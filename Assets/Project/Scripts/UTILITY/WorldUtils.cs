using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
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
    public static bool IsBlockSolidAndInChunk(int x, int y, int z, NativeArray<BlockType> flatBlocks, int dim)
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

    
    
    public struct Vector3FaceData
    {
        public Vector3Int direction; // direction of the face (e.g., up, down, left, etc.)
        public Vector3[] cornerOffsets; // 4 corners relative to block
        public Vector3 normal;
    }
    
    public static readonly Vector3FaceData[] vector3Faces = new Vector3FaceData[]
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
    new Vector3FaceData {
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
    new Vector3FaceData {
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
    new Vector3FaceData {
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
    new Vector3FaceData {
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
    new Vector3FaceData {
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
    new Vector3FaceData {
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

    // In WorldUtils.cs or where Int3FaceData is defined
    
    public struct Int3FaceData
    {
        public int3 direction;
        public int3 normal;
        public int3[] cornerOffsets;

    }
    
    public struct Int3FaceDataForBurst
    {
        public int3 direction;
        public int3 normal;
    
        // <-- THESE ARE THE CHANGES: four separate int3 fields (value types)
        public int3 cornerOffset0; 
        public int3 cornerOffset1;
        public int3 cornerOffset2;
        public int3 cornerOffset3;

        // We added a constructor for convenience, but the fields are what matter
        public Int3FaceDataForBurst(int3 direction, int3 normal, int3 c0, int3 c1, int3 c2, int3 c3)
        {
            this.direction = direction;
            this.normal = normal;
            this.cornerOffset0 = c0;
            this.cornerOffset1 = c1;
            this.cornerOffset2 = c2;
            this.cornerOffset3 = c3;
        }
        
        public int3 GetCornerOffset(int index)
        {
            switch (index)
            {
                case 0: return cornerOffset0;
                case 1: return cornerOffset1;
                case 2: return cornerOffset2;
                case 3: return cornerOffset3;
                default: throw new ArgumentOutOfRangeException(nameof(index), "Index must be between 0 and 3");
            }
        }
    }
    
    
    public static readonly Int3FaceDataForBurst[] AllFaceDataForBurst = new Int3FaceDataForBurst[]
    {
        // Top (+Y)
        new Int3FaceDataForBurst(
            int3Directions.up, int3Directions.up,
            new int3(0, 1, 0),
            new int3(1, 1, 0),
            new int3(0, 1, 1),
            new int3(1, 1, 1)
        ),
        // Bottom (-Y)
        new Int3FaceDataForBurst(
            int3Directions.down, int3Directions.down,
            new int3(1, 0, 0),
            new int3(0, 0, 0),
            new int3(1, 0, 1),
            new int3(0, 0, 1)
        ),
        // Front (+Z)
        new Int3FaceDataForBurst(
            int3Directions.forward, int3Directions.forward,
            new int3(1, 0, 1),
            new int3(0, 0, 1),
            new int3(1, 1, 1),
            new int3(0, 1, 1)
        ),
        // Back (-Z)
        new Int3FaceDataForBurst(
            int3Directions.back, int3Directions.back,
            new int3(0, 0, 0),
            new int3(1, 0, 0),
            new int3(0, 1, 0),
            new int3(1, 1, 0)
        ),
        // Left (-X)
        new Int3FaceDataForBurst(
            int3Directions.left, int3Directions.left,
            new int3(0, 0, 1),
            new int3(0, 0, 0),
            new int3(0, 1, 1),
            new int3(0, 1, 0)
        ),
        // Right (+X)
        new Int3FaceDataForBurst(
            int3Directions.right, int3Directions.right,
            new int3(1, 0, 0),
            new int3(1, 0, 1),
            new int3(1, 1, 0),
            new int3(1, 1, 1)
        )
    };
        
    
    
    
    public static readonly Int3FaceData[] int3Faces = new Int3FaceData[]
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
    new Int3FaceData {
        direction = int3Directions.up,
        normal = int3Directions.up,
        cornerOffsets = new int3[] {
            new int3(0, 1, 0), // front-left
            new int3(1, 1, 0), // front-right
            new int3(0, 1, 1), // back-left
            new int3(1, 1, 1)  // back-right
        }
    },


    // Bottom (-Y)
    new Int3FaceData {
        direction = int3Directions.down,
        normal = int3Directions.down,
        cornerOffsets = new int3[] {
            new int3(1, 0, 0),  // v0 (bottom-left in local -Y)
            new int3(0, 0, 0),  // v1 (bottom-right — flipped X)
            new int3(1, 0, 1),  // v2 (top-left — forward in Z)
            new int3(0, 0, 1),  // v3 (top-right)
        }
    },

    // Front (+Z)
    new Int3FaceData {
        direction = int3Directions.forward,
        normal = int3Directions.forward,
        cornerOffsets = new int3[] {
            new int3(1, 0, 1),
            new int3(0, 0, 1),
            new int3(1, 1, 1),
            new int3(0, 1, 1),
        }
    },

    // OK
    // Back (-Z)
    new Int3FaceData {
        direction = int3Directions.back,
        normal = int3Directions.back,
        cornerOffsets = new int3[] {
            new int3(0, 0, 0),  // bottom-left
            new int3(1, 0, 0),  // bottom-right
            new int3(0, 1, 0),  // top-left
            new int3(1, 1, 0)   // top-right
        }
    },

    // Left (-X)
    new Int3FaceData {
        direction = int3Directions.left,
        normal = int3Directions.left,
        cornerOffsets = new int3[] {
            new int3(0, 0, 1),  // bottom-right
            new int3(0, 0, 0),  // bottom-left
            new int3(0, 1, 1),   // top-right
            new int3(0, 1, 0),  // top-left
        }
    },

    // Right (+X)
    new Int3FaceData {
        direction = int3Directions.right,
        normal = int3Directions.right,
        cornerOffsets = new int3[] {
            new int3(1, 0, 0),  // bottom-right
            new int3(1, 0, 1),  // bottom-left
            new int3(1, 1, 0),   // top-right
            new int3(1, 1, 1),  // top-left
        }
    }
    };
    public static class int3Directions
    {
        public static readonly int3 right  = new int3(1, 0, 0);
        public static readonly int3 left   = new int3(-1, 0, 0);
        public static readonly int3 up     = new int3(0, 1, 0);
        public static readonly int3 down   = new int3(0, -1, 0);
        public static readonly int3 forward = new int3(0, 0, 1);
        public static readonly int3 back   = new int3(0, 0, -1);
    }

    
    
    
    
    
    
    
    
    
    
    public static Vector3Int GetChunkCoords(Vector3 position)
    {
        return new Vector3Int(
            Mathf.FloorToInt(position.x / CHUNK_SIZE),
            Mathf.FloorToInt(position.y / CHUNK_SIZE),
            Mathf.FloorToInt(position.z / CHUNK_SIZE)
        );
    }

    public static Vector2 GetBlockUV(BlockType type, int face)
    {
        // get the total number of block types in the enum
        // Enum.GetValues() returns an array of all values in the enum.
        int totalTiles = (int)BlockType.COUNT;

        
        float tileWidth = 1f / totalTiles;

        int index = (int)type;

        // Bottom-left corner of the tile (index * tileWidth on X, always 0 on Y)
        Vector2 uvMin = new Vector2(index * tileWidth, 0f);

        switch (face)
        {
            case 0: return uvMin + new Vector2(0, 0);                     // Bottom-left
            case 1: return uvMin + new Vector2(tileWidth, 0);             // Bottom-right
            case 2: return uvMin + new Vector2(0, 1f);                    // Top-left
            case 3: return uvMin + new Vector2(tileWidth, 1f);            // Top-right
        }

        throw new ArgumentException("Invalid face index");
    }
    
    // OK
    // for a line-based texture atlas, calculate UV coordinates for a given block type and face.
    public static int CoordsToIndex(int x, int y, int z)
    {
        return x * CHUNK_SIZE * CHUNK_SIZE + y * CHUNK_SIZE + z;
    }
    
    
    //todo these last two don't make sense to be inside utils if they're only for chunks.
    public static bool IsChunkBorder(int x, int y, int z)
    {
        return x == 0 || x == CHUNK_SIZE - 1 ||
               y == 0 || y == CHUNK_SIZE - 1 ||
               z == 0 || z == CHUNK_SIZE - 1;
    }
    
    public static bool IsInBoundsOfChunk(Vector3Int neighborPos)
    {
        return neighborPos.x >= 0 && neighborPos.x < CHUNK_SIZE &&
               neighborPos.y >= 0 && neighborPos.y < CHUNK_SIZE &&
               neighborPos.z >= 0 && neighborPos.z < CHUNK_SIZE;
    }
    public static bool IsInBoundsOfChunkInt3(int3 neighborPos)
    {
        return neighborPos.x >= 0 && neighborPos.x < CHUNK_SIZE &&
               neighborPos.y >= 0 && neighborPos.y < CHUNK_SIZE &&
               neighborPos.z >= 0 && neighborPos.z < CHUNK_SIZE;
    }

}