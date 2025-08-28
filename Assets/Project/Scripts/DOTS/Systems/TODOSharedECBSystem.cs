using Unity.Entities;

namespace Project.Scripts.DOTS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TODOSharedECBSystem : ISystem
    {
        public static EntityCommandBuffer ECB;
    
        public void OnUpdate(ref SystemState state)
        {
            
            //todo make sure every endsimulation takes this one since createcommandbuffer() adds one to list so it doesn't add pointlessly
            // var ecbSys = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
            // ECB = ecbSys.CreateCommandBuffer();
        }
    }

}