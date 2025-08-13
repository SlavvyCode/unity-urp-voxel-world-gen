using Unity.Entities;
using UnityEngine;

public class EntitiesReferencesAuthoring : MonoBehaviour
{
    public GameObject ChunkPrefabGameObject;
    
    public class EntitiesReferencesAuthoringBaker : Baker<EntitiesReferencesAuthoring>
    {
        public override void Bake(EntitiesReferencesAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity,new EntitiesReferences
            {
                chunkPrefabEntity = GetEntity(authoring.ChunkPrefabGameObject,TransformUsageFlags.Dynamic)
            });
        }
    }
}

public struct EntitiesReferences:IComponentData
{
    public Entity chunkPrefabEntity;

}