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
    public GameObject chunkPrefab;  
    
    public class Baker:Baker<ChunkAuthoring>
    {
        public override void Bake(ChunkAuthoring authoring) {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent<DOTS_Chunk>(entity);
            //todo sortout
            //  which of these two?
            AddBuffer<DOTS_Block>(entity);
            
            Entity meshEntity = GetEntity(authoring.meshChildGameObject, TransformUsageFlags.Renderable);
                     

            Entity prefabEntity = GetEntity(authoring.chunkPrefab, TransformUsageFlags.Renderable);
            AddComponent(new ChunkPrefabReference { Prefab = prefabEntity });
            
            // Link them
            AddComponent(entity, new DOTS_ChunkRenderData 
            {
                MeshEntity = meshEntity
            });

            //apparently this is worse somehow?
            // AddComponent<DOTS_Blocks>(entity);
        }
    }
}

public struct ChunkPrefabReference :IComponentData
{
    public Entity Prefab;
}

// Render linkage
public struct DOTS_ChunkRenderData : IComponentData 
{
    public Entity MeshEntity; 
}

public struct DOTS_Chunk : IComponentData
{
    public int3 ChunkCoord;  // (0,0,0), (1,0,0), etc.

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


public struct MeshUploadRequest
{
    public Entity MeshEntity;
    public NativeList<Vertex> Vertices;
    public NativeList<int> Triangles;
    public NativeList<float2> UVs;
    
}
public static class MeshUploadQueue
{
    
    public static NativeQueue<MeshUploadRequest> Queue = new NativeQueue<MeshUploadRequest>(Allocator.Persistent);
}
