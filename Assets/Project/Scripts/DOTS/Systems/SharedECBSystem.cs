using Unity.Entities;

namespace Project.Scripts.DOTS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SharedECBSystem : ISystem
    {
        public static EndSimulationEntityCommandBufferSystem ECBSystem;

        // Helper to get a fresh ECB for jobs or main thread work
        public static EntityCommandBuffer GetECB() => ECBSystem.CreateCommandBuffer();

        public static EntityCommandBuffer.ParallelWriter GetParallelECB() =>
            ECBSystem.CreateCommandBuffer().AsParallelWriter();

        public void OnUpdate(ref SystemState state)
        {
            //todo make sure every endsimulation takes this one since createcommandbuffer() adds one to list so it doesn't add pointlessly
            ECBSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }
        
        
        // usage in other systems:
        // var ecb = SharedECBSystem.GetECB();

    }
}