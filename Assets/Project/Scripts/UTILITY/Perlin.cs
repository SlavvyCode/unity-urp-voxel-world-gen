using Unity.Mathematics;
using static Unity.Mathematics.math;

public static class MyPerlin
{
    private static ulong Hash64(int ix, int iy)
    {
        ulong x = (ulong)(ix & 0xFFFFF);
        ulong y = (ulong)(iy & 0xFFFFF);

        ulong prime1 = 11400714819323199549UL;
        ulong prime2 = 14029467366897019727UL;
        ulong prime3 = 1609587929392839161UL;

        ulong hash = prime1;
        hash = (hash ^ x) * prime2;
        hash = (hash ^ y) * prime3;
        return hash;
    }

    private static float2 RandomGradient(int ix, int iy)
    {
        ulong hash = Hash64(ix, iy);
        float angle = ((hash & 0xFFFFFFFF) / (float)uint.MaxValue) * PI * 2f;
        return float2(cos(angle), sin(angle));
    }

    private static float DotGridGradient(int ix, int iy, float x, float y)
    {
        float2 gradient = RandomGradient(ix, iy);
        float2 diff = float2(x - ix, y - iy);
        return dot(gradient, diff);
    }

    private static float Interpolate(float a0, float a1, float w)
    {
        float w3 = w * w * w;
        float w4 = w3 * w;
        float w5 = w4 * w;
        return lerp(a0, a1, 6f * w5 - 15f * w4 + 10f * w3);
    }

    public static float Noise(float x, float y)
    {
        x = fmod(x, 10000f); // equivalent to Repeat
        y = fmod(y, 10000f);

        int x0 = (int)floor(x);
        int x1 = x0 + 1;
        int y0 = (int)floor(y);
        int y1 = y0 + 1;

        float sx = x - x0;
        float sy = y - y0;

        float n0 = DotGridGradient(x0, y0, x, y);
        float n1 = DotGridGradient(x1, y0, x, y);
        float ix0 = Interpolate(n0, n1, sx);

        n0 = DotGridGradient(x0, y1, x, y);
        n1 = DotGridGradient(x1, y1, x, y);
        float ix1 = Interpolate(n0, n1, sx);

        float value = Interpolate(ix0, ix1, sy);
        return clamp((value + 1f) * 0.5f, 0f, 1f);
    }
}