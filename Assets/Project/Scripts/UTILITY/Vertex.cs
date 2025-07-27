using Unity.Mathematics;

public struct Vertex
{
    public float3 position;
    public float3 normal;
    public float2 uv;

    public Vertex(float3 position, float3 normal, float2 uv)
    {
        this.position = position;
        this.normal = normal;
        this.uv = uv;
    }
}