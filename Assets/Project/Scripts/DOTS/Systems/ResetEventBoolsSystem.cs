using Unity.Burst;
using Unity.Entities;


[UpdateInGroup(typeof(LateSimulationSystemGroup))]
// [UpdateBefore(typeof(ChunkSpawnSystem))] // not applicable here since different group
partial struct ResetEventBoolsSystem : ISystem
{
  
    // for some reason it shows reseteventboolsystem taking 80ms to run, and as waiting for a job complete...
    // but why?? makes no sense
    // ah it's waiting for generatechunkblocksjob for some reason
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        //query for all things containing event bools and reset them to false
        foreach (var chunkCoords 
                 in SystemAPI.Query<RefRW<EntityChunkCoords>>())
        {
            chunkCoords.ValueRW.OnChunkChange = false;
        }
        
        
        
        
    }

}
