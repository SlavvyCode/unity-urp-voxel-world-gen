using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using static Project.Scripts.DOTS.Other.DOTS_Utils;


public class ChunkAuthoring : MonoBehaviour
{

    //contains renderer and filter
    public GameObject meshChildGameObject;
    
    public class Baker:Baker<ChunkAuthoring>
    {
        public override void Bake(ChunkAuthoring authoring) {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<DOTS_Chunk>(entity);
            AddBuffer<DOTS_Block>(entity);
            
            Entity meshEntity = GetEntity(authoring.meshChildGameObject, TransformUsageFlags.Renderable);
            AddComponent(entity, new DOTS_ChunkRenderData 
            {
                MeshEntity = meshEntity
            });
            
            AddComponent(entity,new DOTS_ChunkState
            {
                Value = ChunkState.NotSpawned
            });
        }
    }
}

// Render linkage
public struct DOTS_ChunkRenderData : IComponentData 
{
    public Entity MeshEntity; 
}

public struct DOTS_Chunk : IComponentData
{
    public int3 ChunkCoord;  // (0,0,0), (1,0,0), etc.
    
    //events
    // public bool onSpawn;
    // public bool onRender;

}
public struct DOTS_Block : IBufferElementData {
    public BlockType Value;
}

public struct MeshDataRequest
{
    public Entity MeshEntity;
    public NativeArray<Vertex> Vertices;
    public NativeArray<int> Triangles;
    public NativeArray<float2> UVs;
    
    // {
    //     public Entity MeshEntity;
    //     public int VerticesStart;
    //     public int VerticesLength;
    //     public int TrianglesStart;
    //     public int TrianglesLength;
    //     public int UVsStart;
    //     public int UVsLength;
    // }

}


//todo no reason to not have it as : byte right?
public enum ChunkState : byte
{
    NotSpawned,
    Spawned,       // Chunk just created, not filled yet
    InChunkArr,         // in chunkArray
    BlocksGenerated,         // Blocks filled and Waiting for mesh generation
    MeshGenerated,  // Mesh ready
    DespawnQueued,  // Should be removed
}

public struct DOTS_ChunkState : IComponentData
{
    public ChunkState Value;
}


