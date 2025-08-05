using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    public class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerTag>(entity);
            AddComponent<PlayerSettings>(entity);
            AddComponent<ContainerChunkCoords>(entity);
            AddComponent<LastChunkCoords>(entity);
            //todo make sure you don't need local transform?
        }
    }
}

public struct PlayerSettings : IComponentData
{
    public int renderDistance;
}


public struct PlayerTag : IComponentData
{
}

// probably used for moving Entities - animals, arrows
// - don't need rendered globally
// probably every-frame update, should be alone
public struct ContainerChunkCoords : IComponentData
{
    public int3 Value;
}

public struct LastChunkCoords : IComponentData
{
    public int3 Value;
}