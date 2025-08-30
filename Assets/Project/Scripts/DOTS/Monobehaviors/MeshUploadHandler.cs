// using System.Collections;
// using System.Collections.Generic;
// using NUnit.Framework;
// using Unity.Collections;
// using Unity.Entities;
// using Unity.Mathematics;
// using UnityEngine;
//
// public class MeshUploadHandler : MonoBehaviour
// {
//     public EntityManager entityManager;
//     
//     void Start()
//     {
//      
//         entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
//     }
//
//
//     void LateUpdate()
//     {
//         while (MeshUploadQueues.PreviousQueue.TryDequeue(out var meshUploadRequest))
//         {
//             var filter = entityManager.GetComponentObject<MeshFilter>(meshUploadRequest.MeshEntity);
//             var renderer = entityManager.GetComponentObject<MeshRenderer>(meshUploadRequest.MeshEntity);
//             Mesh mesh = new Mesh();
//   
//             
//             var vertsArray = meshUploadRequest.Vertices;
//             Vector3[] verts = new Vector3[vertsArray.Length];
//             for (int i = 0; i < vertsArray.Length; i++)
//                 verts[i] = vertsArray[i].position;
//
//
//             // List<int> tris = new List<int>();
//             // for (int i = 0; i < meshUploadRequest.Triangles.Length; i++)
//             //     tris.Add(meshUploadRequest.Triangles[i]); 
//             NativeArray<int> trisArray = meshUploadRequest.Triangles;
//             int[] tris = new int[trisArray.Length];
//             for (int i = 0; i < trisArray.Length; i++)
//                 tris[i] = trisArray[i];
//            
//             
//             
//             
//             // List<Vector2> uvs = new List<Vector2>();
//             // for (int i = 0; i < meshUploadRequest.UVs.Length; i++)
//             //     uvs.Add(meshUploadRequest.UVs[i]); 
//             NativeArray<float2> uvsArray = meshUploadRequest.UVs;
//             Vector2[] uvs = new Vector2[uvsArray.Length];
//             for (int i = 0; i < uvsArray.Length; i++)
//                 uvs[i] = new Vector2(uvsArray[i].x, uvsArray[i].y);
//             
//
//             
//             mesh.SetVertices(verts);
//             mesh.SetTriangles(tris, 0);
//             mesh.RecalculateNormals();
//             mesh.SetUVs(0,uvs);
//             filter.mesh = mesh;
//             renderer.enabled = true;
//             
//             // Debug.Log("MeshUploadHandler: mesh uploaded");
//
//             
//             // meshUploadRequest.MeshEntity.RemoveComponent<ChunkMeshPending>();
//             meshUploadRequest.Vertices.Dispose();
//             meshUploadRequest.Triangles.Dispose();
//             // this.enabled = false;
//         }
//     }
// }
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

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

        // get mesh from pool or create new
        Mesh mesh = meshPool.Count > 0 ? meshPool.Pop() : new Mesh { indexFormat = IndexFormat.UInt32 };
        mesh.Clear();

        int vertCount = request.Vertices.Length;
        int triCount = request.Triangles.Length;

        // Set single-stream vertex buffer layout

        mesh.SetVertexBufferParams(vertCount, new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position),
            new VertexAttributeDescriptor(VertexAttribute.Normal),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0)
        });
        mesh.SetVertexBufferData(request.Vertices, 0, 0, vertCount, 0, MeshUpdateFlags.DontRecalculateBounds);
        mesh.SetIndexBufferParams(triCount, IndexFormat.UInt32);

        mesh.SetIndexBufferParams(triCount, IndexFormat.UInt32);
        mesh.SetIndexBufferData(request.Triangles, 0, 0, triCount, MeshUpdateFlags.DontValidateIndices);

        mesh.subMeshCount = 1;
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, triCount, MeshTopology.Triangles), MeshUpdateFlags.DontRecalculateBounds);
        // Assign mesh
        filter.mesh = mesh;
        renderer.enabled = true;

        // Return NativeArrays to dispose
        request.Vertices.Dispose();
        request.Triangles.Dispose();
        request.UVs.Dispose();
    }
}

}
