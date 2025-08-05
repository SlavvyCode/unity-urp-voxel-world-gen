using UnityEngine;
using UnityEditor;

public static class MeshAssetCreator
{
    [MenuItem("Assets/Create/EmptyMesh")]
    public static void CreateEmptyMesh()
    {
        Mesh mesh = new Mesh();
        AssetDatabase.CreateAsset(mesh, "Assets/EmptyMesh.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("Empty mesh asset created at Assets/EmptyMesh.asset");
    }
}