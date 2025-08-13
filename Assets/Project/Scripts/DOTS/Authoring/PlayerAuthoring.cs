using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    public int renderDistance;
    public class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerTag>(entity);
            AddComponent(entity,new PlayerSettings
            {
                renderDistance = authoring.renderDistance,
            });
            AddComponent<EntityChunkCoords>(entity);
            //singleton
            AddComponent<LastChunkCoords>(entity);
            //add buffer for loaded chunks
            AddBuffer<LoadedChunk>(entity);
            //add newly spawned player tag
            AddComponent<NewlySpawnedPlayerTag>(entity);
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
public struct EntityChunkCoords : IComponentData
{
    public int3 newChunkCoords;
    
    //event, since we can't just modify LocalTransform
    public bool OnChunkChange;
}

public struct LastChunkCoords : IComponentData
{
    public int3 Value;
}

public struct LoadedChunk : IBufferElementData
{
    public int3 ChunkCoord;
    public Entity ChunkEntity;
}


// used to mark player that just spawned, so we can initialize their chunk coords
// and not do it every frame
public struct NewlySpawnedPlayerTag : IComponentData
{
}