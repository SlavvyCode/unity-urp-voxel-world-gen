using Unity.Entities;

namespace Project.Scripts.DOTS.Systems
{

    public struct ChunkPool : IComponentData
    {
        public int Capacity;    // max pool size before cleanup removes extras
        public int ActiveCount; // number of chunks currently checked out (informational)
    }

    public struct ChunkPoolElement : IBufferElementData
    {
        public Entity Value;
    }


    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ChunkPoolManagerSystem : ISystem
    {
        private Entity poolEntity;
        private bool created;

        public void OnCreate(ref SystemState state)
        {
            // create singleton pool entity
            poolEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(poolEntity, new ChunkPool { Capacity = 512, ActiveCount = 0 });
            state.EntityManager.AddBuffer<ChunkPoolElement>(poolEntity);
            created = true;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (created && state.EntityManager.Exists(poolEntity))
            {
                // destroy pool entity (will also cleanup buffer)
                state.EntityManager.DestroyEntity(poolEntity);
            }
        }

            // nothing per-frame for now — pool lives on the singleton entity
        public void OnUpdate(ref SystemState state)
        {
        }
    }

}