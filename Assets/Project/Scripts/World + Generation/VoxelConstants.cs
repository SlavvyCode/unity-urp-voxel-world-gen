using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class VoxelConstants
{
    public const int CHUNK_SIZE = 16; // Size of each chunk in voxels
    public const int WORLD_SIZE = 1024; // Total size of the world in voxels (WORLD_SIZE x WORLD_SIZE x WORLD_SIZE)
    public const float VOXEL_SIZE = 1.0f; // Size of each voxel in world units
    public const float CHUNK_LOAD_DISTANCE = 5.0f; // Distance from the player to load chunks
    
    public const int MAX_BLOCK_TYPES = 256; // Maximum number of different block types
}
