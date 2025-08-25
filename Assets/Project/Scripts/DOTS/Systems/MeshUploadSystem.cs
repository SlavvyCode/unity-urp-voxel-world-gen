using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;

namespace Project.Scripts.DOTS.Systems
{
    [UpdateAfter(typeof(MeshGenerationSystem))]
    public partial struct MeshUploadSystem : ISystem
    {
        private MeshGenerationSystem.MeshBuffers buffersFromSingleton;
        private MeshGenerationSystem.MeshBuffers singleton;


        public NativeArray<Vertex> vertices;
        public NativeArray<int> triangles;
        public NativeArray<float2> uvs;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MeshGenerationSystem.MeshBuffers>();
        }
        
        public void OnDestroy(ref SystemState state)
        {
            // if (MeshUploadQueue.Queue.IsCreated)
                // MeshUploadQueue.Queue.Dispose();
        }
        public void OnUpdate(ref SystemState state)
        {
            // Make sure jobs that wrote slices are finished
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, MeshGenerationSystem.LastMeshJobHandle);
            state.Dependency.Complete();

            var buffers = SystemAPI.GetSingleton<MeshGenerationSystem.MeshBuffers>();

            var vertices  = buffers.vertices;
            var triangles = buffers.triangles;
            var uvs       = buffers.uvs;

            // Read from the global slice queue (produced by MergeQueueJob)
            while (buffers.meshSliceQueue.TryDequeue(out var slice))
            {
                MeshUploadQueues.CurrentQueue.Enqueue(new MeshDataRequest
                {
                    MeshEntity = slice.MeshEntity,
                    Vertices   = vertices.GetSubArray(slice.VerticesStart, slice.VerticesLength),
                    Triangles  = triangles.GetSubArray(slice.TrianglesStart, slice.TrianglesLength),
                    UVs        = uvs.GetSubArray(slice.UVsStart, slice.UVsLength)
                });
            }

            // Done with this frame’s data, flip buffers
             MeshUploadQueues.Swap();
        }

        
    }
    
}


public static class MeshUploadQueues
{
    public static NativeQueue<MeshDataRequest> QueueA = new NativeQueue<MeshDataRequest>(Allocator.Persistent);
    public static NativeQueue<MeshDataRequest> QueueB = new NativeQueue<MeshDataRequest>(Allocator.Persistent);

    public static bool useA = true;

    public static JobHandle LastWriteHandle;

    public static NativeQueue<MeshDataRequest> CurrentQueue => useA ? QueueA : QueueB;
    public static NativeQueue<MeshDataRequest> PreviousQueue => useA ? QueueB : QueueA;

    public static void Swap()
    {
        PreviousQueue.Clear();
        useA = !useA;
    }
}


//
// public static class MeshUploadQueue
// {
//     public static NativeQueue<MeshDataRequest> Queue = new NativeQueue<MeshDataRequest>(Allocator.Persistent);
//
//     public static void Init()
//     {
//         if (!Queue.IsCreated)
//             Queue = new NativeQueue<MeshDataRequest>(Allocator.Persistent);
//     }
//     // i dont think this needs to be disposed, it's just a class, not a dots system
//     // //destroy on shutdown
//     // public static void Dispose()
//     // {
//     //     if (Queue.IsCreated)
//     //         Queue.Dispose();
//     // }
// }
