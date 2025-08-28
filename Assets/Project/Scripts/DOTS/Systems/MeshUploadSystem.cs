using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

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
            MeshUploadQueues.EnsureCreated();
        }


        public void OnDestroy(ref SystemState state)
        {
            MeshUploadQueues.DisposeAll();
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
    public static NativeQueue<MeshDataRequest> QueueA;
    public static NativeQueue<MeshDataRequest> QueueB;

    public static bool useA = true;
    public static JobHandle LastWriteHandle;

    public static void EnsureCreated()
    {
        if (!QueueA.IsCreated) QueueA = new NativeQueue<MeshDataRequest>(Allocator.Persistent);
        if (!QueueB.IsCreated) QueueB = new NativeQueue<MeshDataRequest>(Allocator.Persistent);
    }

    public static void DisposeAll()
    {
        if (QueueA.IsCreated)
        {
            // make sure no jobs are using it
            LastWriteHandle.Complete();
            QueueA.Dispose();
        }
        if (QueueB.IsCreated)
        {
            LastWriteHandle.Complete();
            QueueB.Dispose();
        }
    }

    public static NativeQueue<MeshDataRequest> CurrentQueue
    {
        get
        {
            EnsureCreated();
            return useA ? QueueA : QueueB;
        }
    }

    public static NativeQueue<MeshDataRequest> PreviousQueue
    {
        get
        {
            EnsureCreated();
            return useA ? QueueB : QueueA;
        }
    }

    public static void Swap()
    {
        // clear previous queue safely (ensure it exists)
        if (PreviousQueue.IsCreated)
            PreviousQueue.Clear();
        useA = !useA;
    }
}

