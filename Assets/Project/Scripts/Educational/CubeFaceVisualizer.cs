using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CubeFaceVisualizer : MonoBehaviour
{
    public Vector3[] faceCorners;
    public Color lineColor = Color.green;
    public float sphereRadius = 0.05f;

    void OnDrawGizmos()
    {
        if (faceCorners == null || faceCorners.Length != 4)
            return;

        Gizmos.color = lineColor;

        Vector3 origin = transform.position;

        // Draw spheres at each corner
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawSphere(origin + faceCorners[i], sphereRadius);
        }

        // Draw lines between corners
        Gizmos.DrawLine(origin + faceCorners[0], origin + faceCorners[1]);
        Gizmos.DrawLine(origin + faceCorners[1], origin + faceCorners[3]);
        Gizmos.DrawLine(origin + faceCorners[3], origin + faceCorners[2]);
        Gizmos.DrawLine(origin + faceCorners[2], origin + faceCorners[0]);
    }
}
