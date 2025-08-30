using Project.Scripts.DOTS.Other;
using Unity.Entities;
using Unity.VisualScripting;

namespace Project.Scripts.DOTS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct InitAndCleanupSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            DOTS_Utils.FaceData.Init();
        }
        public void OnDestroy(ref SystemState state)
        {
            DOTS_Utils.FaceData.Dispose();
        }
    }
}