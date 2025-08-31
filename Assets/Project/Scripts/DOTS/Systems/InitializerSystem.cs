using Project.Scripts.DOTS.Other;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static Project.Scripts.DOTS.Other.DOTS_Utils;

namespace Project.Scripts.DOTS.Systems
{
    public partial struct InitializerSystem : ISystem
    {
        public static BlobAssetReference<FaceBlob> faceBlob;
        public void OnCreate(ref SystemState state)
        {
            // DOTS_Utils.FaceData.Init();
            InitFaceBlob();
        }

        void InitFaceBlob()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<FaceBlob>();
            var facesArray = builder.Allocate(ref root.Faces, 6);

            // +X

            // +X (right)
            facesArray[0] = new Face(
                new int3(1, 0, 0),
                new float3(1, 0, 0), // normal
                new float3(1, 0, 0),
                new float3(1, 0, 1),
                new float3(1, 1, 0),
                new float3(1, 1, 1)
            );

            // -X
            facesArray[1] = new Face(
                new int3(-1, 0, 0),
                new float3(-1, 0, 0), // normal
                new float3(0, 0, 1),
                new float3(0, 0, 0),
                new float3(0, 1, 1),
                new float3(0, 1, 0)
            );

            // +Y AKA TOP
            facesArray[2] = new Face(
                new int3(0, 1, 0),
                new float3(0, 1, 0), // normal
                new float3(0, 1, 0),
                new float3(1, 1, 0),
                new float3(0, 1, 1),
                new float3(1, 1, 1)
            );

            // -Y (BOT)
            facesArray[3] = new Face(
                new int3(0, -1, 0),
                new float3(0, -1, 0), // normal
                new float3(1, 0, 0),
                new float3(0, 0, 0),
                new float3(1, 0, 1),
                new float3(0, 0, 1)
            );

            // +Z AKA FRONT
            facesArray[4] = new Face(
                new int3(0, 0, 1),
                new float3(0, 0, 1), // normal
                new float3(1, 0, 1),
                new float3(0, 0, 1),
                new float3(1, 1, 1),
                new float3(0, 1, 1)
            );

            // -Z AKA BACK
            facesArray[5] = new Face(
                new int3(0, 0, -1),
                new float3(0, 0, -1), // normal
                new float3(0, 0, 0),
                new float3(1, 0, 0),
                new float3(0, 1, 0),
                new float3(1, 1, 0)
            );

            faceBlob = builder.CreateBlobAssetReference<FaceBlob>(Allocator.Persistent);
            builder.Dispose();
        }
    }
}