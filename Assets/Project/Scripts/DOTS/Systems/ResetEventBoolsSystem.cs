using Unity.Burst;
using Unity.Entities;


[UpdateInGroup(typeof(LateSimulationSystemGroup))]
partial struct ResetEventBoolsSystem : ISystem
{
  
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var chunkCoords 
                 in SystemAPI.Query<RefRW<EntityChunkCoords>>())
        {
            chunkCoords.ValueRW.OnChunkChange = false;
        }
        //query for all things containing event bools and reset them to false
    }

}
