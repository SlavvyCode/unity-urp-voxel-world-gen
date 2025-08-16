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
  
            
            var vertsArray = meshUploadRequest.Vertices;
            Vector3[] verts = new Vector3[vertsArray.Length];
            for (int i = 0; i < vertsArray.Length; i++)
                verts[i] = vertsArray[i].position;


            // List<int> tris = new List<int>();
            // for (int i = 0; i < meshUploadRequest.Triangles.Length; i++)
            //     tris.Add(meshUploadRequest.Triangles[i]); 
            NativeArray<int> trisArray = meshUploadRequest.Triangles;
            int[] tris = new int[trisArray.Length];
            for (int i = 0; i < trisArray.Length; i++)
                tris[i] = trisArray[i];
           
            
            
            
            // List<Vector2> uvs = new List<Vector2>();
            // for (int i = 0; i < meshUploadRequest.UVs.Length; i++)
            //     uvs.Add(meshUploadRequest.UVs[i]); 
            NativeArray<float2> uvsArray = meshUploadRequest.UVs;
            Vector2[] uvs = new Vector2[uvsArray.Length];
            for (int i = 0; i < uvsArray.Length; i++)
                uvs[i] = new Vector2(uvsArray[i].x, uvsArray[i].y);
            

            
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.SetUVs(0,uvs);
            filter.mesh = mesh;
            renderer.enabled = true;
            
            // Debug.Log("MeshUploadHandler: mesh uploaded");

            
            // meshUploadRequest.MeshEntity.RemoveComponent<ChunkMeshPending>();
            meshUploadRequest.Vertices.Dispose();
            meshUploadRequest.Triangles.Dispose();
            // this.enabled = false;
        }
    }
}
