using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class ChunkMeshAuthoring : MonoBehaviour
{
    public class Baker:Baker<ChunkMeshAuthoring>
    {
        public override void Bake(ChunkMeshAuthoring authoring) {
            Entity entity = GetEntity(TransformUsageFlags.Renderable);
            // Link them
            AddComponentObject(entity,GetComponent<MeshFilter>());
            AddComponentObject(entity,GetComponent<MeshRenderer>());
            
            AddComponent(entity, new DOTS_ChunkRenderData 
            {
                MeshEntity = entity
            });
        }
    }
}