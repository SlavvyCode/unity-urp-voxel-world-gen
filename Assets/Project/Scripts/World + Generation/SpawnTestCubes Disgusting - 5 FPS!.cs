using System;
using UnityEditor;
using UnityEngine;

public class SpawnTestCubes : MonoBehaviour
{
    private void Start()
    {
        SpawnCubes();
    }


    static void SpawnCubes()
    {
        for (int i = 0; i < 200; i++)
        {
            for (int j = 0; j < 200; j++)
            {
                for (int k = 0; k < 5; k++)
                {

                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.position = new Vector3(i, k, j);
                }
            }

        }
        Debug.Log("Spawned 1000 cubes (badly).");
    }

}