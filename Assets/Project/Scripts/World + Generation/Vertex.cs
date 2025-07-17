using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Vertex
{
    public Vector3 position; // position in world space
    public Vector3 normal;   // normal vector for lighting = the average normal of all faces that share that vertex.
                             // This fakes smooth lighting when interpolated across a surface.
    public Vector2 uv;       // UV coordinates for texture mapping

    public Vertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        this.position = position;
        this.normal = normal;
        this.uv = uv;
    }
}