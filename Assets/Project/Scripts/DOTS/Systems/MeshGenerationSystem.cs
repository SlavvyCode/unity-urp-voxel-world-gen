using System;
using Project.Scripts.DOTS.Other;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.PlayerLoop;
using static Project.Scripts.DOTS.Other.DOTS_Utils;

[BurstCompile]
public partial struct MeshGenerationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (chunk, blocks, renderData) in
                 SystemAPI.Query<RefRO<DOTS_Chunk>, DynamicBuffer<DOTS_Block>, RefRO<DOTS_ChunkRenderData>>())
        {
            if (blocks.IsEmpty) return;

            // Generate mesh data...
            NativeList<Vertex> vertices = new NativeList<Vertex>(Allocator.Temp);
            NativeList<int> triangles = new NativeList<int>(Allocator.Temp);

            // todo this seems like a useful tool, even if it is bollocks.
            // ForEachBlockInChunk((x, y, z) =>
            // {

            // AddVisibleFaces(x, y, z, vertices, triangles);
            // });
            //foreach block
            for (int x = 0; x < CHUNK_SIZE; x++)
            for (int y = 0; y < CHUNK_SIZE; y++)
            for (int z = 0; z < CHUNK_SIZE; z++)
            {
                int index = ToIndex(x, y, z);
                var block = blocks[index];

                if (block.Value != BlockType.Air)
                {
                    AddVisibleFaces(x, y, z, block.Value, blocks, vertices, triangles);
                }
            }


            var persistentVerts = new NativeList<Vertex>(vertices.Length, Allocator.Persistent);
            persistentVerts.AddRange(vertices.AsArray());

            var persistentTris = new NativeList<int>(triangles.Length, Allocator.Persistent);
            persistentTris.AddRange(triangles.AsArray());
            var persistentUVs = new NativeList<float2>(vertices.Length, Allocator.Persistent);
            for (int i = 0; i < vertices.Length; i++)
            {
                persistentUVs.Add(vertices[i].uv);
            }
            MeshUploadQueue.Queue.Enqueue(new MeshUploadRequest
            {
                MeshEntity = renderData.ValueRO.MeshEntity,
                Vertices = persistentVerts,
                Triangles = persistentTris,
                UVs = persistentUVs
            });
        }

        ecb.Playback(state.EntityManager);
        // state.Enabled = false;
    }

    private void AddVisibleFaces(int x, int y, int z, BlockType blockType, DynamicBuffer<DOTS_Block> blocks,
        NativeList<Vertex> vertices, NativeList<int> triangles)
    {
        foreach (var face in FaceData.AllFaces)
        {
            //todo backface culling MIGHT not be needed ~ just the fact that the triangles are not faced towards the camera is enough to not render them.
            // theoretically we could save some small performance by not adding triangles that are not facing the camera in the first place.


            int3 neighborPos = new int3(x, y, z) + face.direction;

            //is this neigbor pos chunk border or air? add face
            // todo render all for now
            // todo this is inefficient once there will be multiple chunks together which will block each other
            // if (!IsInBoundsOfChunk(neighborPos) ||
            //     !WorldUtils.IsBlockSolidAndInChunk(neighborPos.x, neighborPos.y, neighborPos.z, blocks, CHUNK_SIZE))
            // {
            // If neighbor is out of bounds or not solid, add this face
            // Add 4 vertices for the face
            int startIndex = vertices.Length;

            for (int i = 0; i < 4; i++)
            {
                float3 worldPos = new float3(x, y, z) + face.cornerOffsets[i];
                float2 uv = GetBlockUV(blockType, i);
                vertices.Add(new Vertex
                {
                    position = worldPos,
                    normal = face.normal,
                    uv = uv
                });
            }

            // basically:
            // Correct winding → triangle shows up from the front (visible).
            // Wrong winding → triangle invisible from the front (backface culling).


            // v2------v3
            // |       |
            // |       |
            // v0------v1
            // Triangle order (0, 2, 1), (1, 2, 3). like the image. this order because it's like ABCD
            //This preserves clockwise winding order (which tells the GPU which side is the front). If you reverse it, the face might become invisible due to backface culling.


            // so yeah we just split it up by threes and hope that the triangles were added  correctly. 
            triangles.Add(startIndex + 0);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 1);

            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }
    }



    public static float2 GetBlockUV(BlockType type, int face)
    {
        // get the total number of block types in the enum
        // Enum.GetValues() returns an array of all values in the enum.
        int totalTiles = (int)BlockType.COUNT;


        float tileWidth = 1f / totalTiles;

        int index = (int)type;

        // Bottom-left corner of the tile (index * tileWidth on X, always 0 on Y)
        float2 uvMin = new float2(index * tileWidth, 0f);

        switch (face)
        {
            case 0: return uvMin + new float2(0f, 0f); // Bottom-left
            case 1: return uvMin + new float2(tileWidth, 0f); // Bottom-right
            case 2: return uvMin + new float2(0f, 1f); // Top-left
            case 3: return uvMin + new float2(tileWidth, 1f); // Top-right

        }

        throw new ArgumentException("Invalid face index");
    }

    private int ToIndex(int x, int y, int z)
    {
        return x + CHUNK_SIZE * (y + CHUNK_SIZE * z);
    }

}

// For main-thread mesh assignment
public struct MeshUploadData : IComponentData
{
    public NativeArray<Vertex> Vertices;
    public NativeArray<int> Triangles;
}