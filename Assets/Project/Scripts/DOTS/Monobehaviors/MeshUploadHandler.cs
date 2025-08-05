using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class MeshUploadHandler : MonoBehaviour
{
    public EntityManager entityManager;
    
    void Start()
    {
     
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }


    void LateUpdate()
    {
        while (MeshUploadQueue.Queue.TryDequeue(out var meshUploadRequest))
        {
            var filter = entityManager.GetComponentObject<MeshFilter>(meshUploadRequest.MeshEntity);
            var renderer = entityManager.GetComponentObject<MeshRenderer>(meshUploadRequest.MeshEntity);
            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            for (int i = 0; i < meshUploadRequest.Vertices.Length; i++)
                verts.Add(meshUploadRequest.Vertices[i].position);
            mesh.SetVertices(verts);

            List<int> tris = new List<int>();
            for (int i = 0; i < meshUploadRequest.Triangles.Length; i++)
                tris.Add(meshUploadRequest.Triangles[i]); 
            
            
            
            List<Vector2> uvs = new List<Vector2>();
            for (int i = 0; i < meshUploadRequest.UVs.Length; i++)
                uvs.Add(meshUploadRequest.UVs[i]); 

            
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.SetUVs(0,uvs);
            filter.mesh = mesh;
            renderer.enabled = true;

            meshUploadRequest.Vertices.Dispose();
            meshUploadRequest.Triangles.Dispose();
            // this.enabled = false;
        }
    }
}
