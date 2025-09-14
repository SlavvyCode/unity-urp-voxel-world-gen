using Unity.Entities;
using Unity.Transforms;

namespace Project.Scripts.DOTS.Systems
{
    public  partial struct HeightMapUtilities : ISystem
    {
        // public bool PlayerChangedChunks(ref SystemState state)
        // {
        //     foreach (var (transform,
        //                  chunkCoord,
        //                  lastChunkPos)
        //              in
        //              SystemAPI.Query<
        //                      RefRO<LocalTransform>,
        //                      RefRW<EntityChunkCoords>,
        //                      RefRW<LastChunkCoords>>()
        //                  .WithAll<PlayerTag>())
        //     {
        //         if (chunkCoord.ValueRO.OnChunkChange == false)
        //         {
        //             return true;
        //         }
        //     }
        //
        //     return false;
        // }
    }
}