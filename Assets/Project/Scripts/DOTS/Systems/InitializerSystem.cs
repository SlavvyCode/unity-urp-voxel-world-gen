using Project.Scripts.DOTS.Other;
using Unity.Entities;

namespace Project.Scripts.DOTS.Systems
{
    public partial struct InitializerSystem: ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            DOTS_Utils.FaceData.Init();
        }
    }
}