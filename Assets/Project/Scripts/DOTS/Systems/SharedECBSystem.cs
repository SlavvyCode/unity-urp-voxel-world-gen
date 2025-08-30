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
        
        
        // todo research and implement logic using EETS ExclusiveEntityTransactions
        
        // as mentioned here https://discussions.unity.com/t/procedural-generation-into-a-separate-world/945718/6
        // https://chatgpt.com/c/68b2b51d-86a0-832b-9bd4-45b0f8c6162d
        // usage in other systems:
        // var ecb = SharedECBSystem.GetECB();

    }
}