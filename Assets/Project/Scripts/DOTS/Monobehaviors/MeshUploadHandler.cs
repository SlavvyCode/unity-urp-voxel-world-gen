using System.Collections.Generic;
using Project.Scripts.DOTS.Other;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using static Project.Scripts.DOTS.Other.DOTS_Utils;
public class MeshUploadHandler : MonoBehaviour
{
    public EntityManager entityManager;

    // Simple mesh pool
    private Stack<Mesh> meshPool = new Stack<Mesh>();

    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    void LateUpdate()
    {
        while (MeshUploadQueues.PreviousQueue.TryDequeue(out var request))
        {
            MeshFilter filter = entityManager.GetComponentObject<MeshFilter>(request.MeshEntity);
            MeshRenderer renderer = entityManager.GetComponentObject<MeshRenderer>(request.MeshEntity);

            //todo maybe i shouldn't be using world coords for bounds, but chunk local coords?

            // entityManager.SetComponentEnabled<MaterialMeshInfo>(request.MeshEntity, true);

            // get mesh from pool or create new
            Mesh mesh = meshPool.Count > 0 ? meshPool.Pop() : new Mesh { indexFormat = IndexFormat.UInt32 };
            //clear is useless if we overwrite
            // mesh.Clear();

            int vertCount = request.Vertices.Length;
            int triCount = request.Triangles.Length;

            // Set single-stream vertex buffer layout

            // this and changing the [] in the Vertex struct fixed it!
            mesh.SetVertexBufferParams(vertCount, new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal,   VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            });
            mesh.SetVertexBufferData(request.Vertices, 0, 0, vertCount, 0, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetIndexBufferParams(triCount, IndexFormat.UInt32);

            mesh.SetIndexBufferData(request.Triangles, 0, 0, triCount, MeshUpdateFlags.DontValidateIndices);

            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, triCount, MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds);
            // Assign mesh
            filter.mesh = mesh;
            renderer.enabled = true;

            float3 chunkOrigin = entityManager.GetComponentData<LocalTransform>(request.MeshEntity).Position;
            float3 chunkSize   = new float3(CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE); // replace with your actual chunk dimensions
            float3 center      = chunkOrigin + chunkSize * 0.5f;

            mesh.bounds = new Bounds(center, chunkSize);

            
            //todo maybe first try to get it simpler and simplify into Method-like snippets that you can easily understand, then the errors should be evident
            // Return NativeArrays to dispose
            request.Vertices.Dispose();
            request.Triangles.Dispose();
            request.UVs.Dispose();
        }
    }
}