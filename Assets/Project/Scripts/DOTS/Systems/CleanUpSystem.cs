using Project.Scripts.DOTS.Other;
using Unity.Entities;
using Unity.VisualScripting;

namespace Project.Scripts.DOTS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct CleanUpSystem : ISystem
    {
        // public void OnCreate(ref SystemState state) { }
        // public void OnUpdate(ref SystemState state) { }
        //
        public void OnDestroy(ref SystemState state)
        {
            DOTS_Utils.FaceData.Dispose();
        }
    }
}