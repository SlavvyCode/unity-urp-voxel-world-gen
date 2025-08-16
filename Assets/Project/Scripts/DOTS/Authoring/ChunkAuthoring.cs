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
//todo sortout
//apparently nativearray in components is a problem
// public struct DOTS_Blocks : IComponentData
// {
//     public NativeArray<DOTS_Block> Blocks;
// }


public struct MeshDataRequest
{
    public Entity MeshEntity;
    public NativeArray<Vertex> Vertices;
    public NativeArray<int> Triangles;
    public NativeArray<float2> UVs;
}



public static class MeshUploadQueue
{
    public static NativeQueue<MeshDataRequest> Queue = new NativeQueue<MeshDataRequest>(Allocator.Persistent);
}


public struct ChunkBlocksPending : IComponentData
{
} 


public struct LoadedChunksPending : IComponentData
{
}