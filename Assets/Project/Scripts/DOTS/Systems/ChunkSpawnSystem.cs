using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Project.Scripts.DOTS.Other.DOTS_Utils; 
// [BurstCompile]
public partial struct ChunkSpawnSystem : ISystem
{
    private NativeHashSet<int3> _spawnedChunks;
    private Entity chunkPrefabEntity;
    private ChunkPrefabReference prefabComponent;
    public void OnCreate(ref SystemState state)
    {
        _spawnedChunks = new NativeHashSet<int3>(1024, Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_spawnedChunks.IsCreated)
            _spawnedChunks.Dispose();
    }

    //todo make happen on event instead, this is inefficient af
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        
        EntityQuery query = SystemAPI.QueryBuilder()
            .WithAll<ChunkPrefabReference>()
            .Build();

        var entities = query.ToEntityArray(Allocator.Temp);
        if (entities.Length > 0)
        {
            chunkPrefabEntity = entities[0];
            prefabComponent = SystemAPI.GetComponent<ChunkPrefabReference>(chunkPrefabEntity);
            // use chunkPrefabEntity
        }
        entities.Dispose();
        
        foreach (var (
                     settings,
                     coord) in 
                 SystemAPI.Query<
                     RefRO<PlayerSettings>,
                     RefRO<ContainerChunkCoords>
                 >())
        {
            int r = settings.ValueRO.renderDistance;

            //todo make spawn in spherical radius instead of a cube
            for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
            for (int z = -r; z <= r; z++)
            {
                //player's chunk is the default off which we deviate
                int3 newCoords = coord.ValueRO.Value + new int3(x, 0, z);

                if (_spawnedChunks.Add(newCoords)) // Only spawn if newly added
                    SpawnChunk(ref state, newCoords, ref ecb,prefabComponent.Prefab);
            }
        }

        ecb.Playback(state.EntityManager);
    }
    
    
    void SpawnChunk(ref SystemState state, int3 newChunkCoords, ref EntityCommandBuffer ecb, Entity ChunkPrefab)
    {
        Entity chunk = ecb.Instantiate(ChunkPrefab); // <- must be a valid prefab entity
        Debug.Log("instantiating chunk at" + newChunkCoords);
        ecb.SetComponent(chunk, new LocalTransform
        {
            //todo transform local to world?
            Position = (newChunkCoords * CHUNK_SIZE),
            Rotation = quaternion.identity,
            Scale = 1f
        });

        ecb.AddComponent(chunk, new DOTS_Chunk
        {
            ChunkCoord = newChunkCoords
        });

        // ecb.AddComponent<ChunkNeedsMeshTag>(chunk);
    }


}
