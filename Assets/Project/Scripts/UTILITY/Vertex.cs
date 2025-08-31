using System.Runtime.InteropServices;
using Unity.Mathematics;


[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Vertex
{
    public float3 position; //12 bytes
    public float3 normal;//12 bytes
    public float2 uv;//8bytes

    public Vertex(float3 position, float3 normal, float2 uv)
    {
        this.position = position;
        this.normal = normal;
        this.uv = uv;
    }
}