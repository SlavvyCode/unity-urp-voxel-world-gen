using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// modified taken from https://en.wikipedia.org/w/index.php?title=Perlin_noise&oldid=1230993513
// unity already has a Perlin noise implementation, but this one is better.
public static class Perlin 
{
    private struct Vector2f
    {
        public float x, y;
        public Vector2f(float x, float y) { this.x = x; this.y = y; }
    }

    // 64-bit hash function for better distribution
    private static ulong Hash64(int ix, int iy)
    {
        ulong x = (ulong)(ix & 0xFFFFF); // only lower 20 bits
        ulong y = (ulong)(iy & 0xFFFFF);
        
        // Large primes from https://primes.utm.edu/lists/2small/0bit.html
        ulong prime1 = 11400714819323199549UL;
        ulong prime2 = 14029467366897019727UL;
        ulong prime3 = 1609587929392839161UL;
        
        ulong hash = prime1;
        hash = (hash ^ x) * prime2;
        hash = (hash ^ y) * prime3;
        return hash;
    }

    private static Vector2f RandomGradient(int ix, int iy)
    {
        ulong hash = Hash64(ix, iy);
        // Use all 64 bits for angle precision
        double angle = (hash / 18446744073709551616.0) * Mathf.PI * 2.0;
        return new Vector2f((float)Math.Cos(angle), (float)Math.Sin(angle));
    }

    private static float DotGridGradient(int ix, int iy, float x, float y)
    {
        Vector2f gradient = RandomGradient(ix, iy);
        float dx = x - ix;
        float dy = y - iy;
        return dx * gradient.x + dy * gradient.y;
    }

    private static float Interpolate(float a0, float a1, float w)
    {
        // Quintic interpolation for smoother results
        float w3 = w * w * w;
        float w4 = w3 * w;
        float w5 = w4 * w;
        return a0 + (a1 - a0) * (6 * w5 - 15 * w4 + 10 * w3);
    }

    public static float Noise(float x, float y)
    {
        x = Mathf.Repeat(x, 10000f);
        y = Mathf.Repeat(y, 10000f);
        
        int x0 = Mathf.FloorToInt(x);
        int x1 = x0 + 1;
        int y0 = Mathf.FloorToInt(y);
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
        return Mathf.Clamp((value + 1f) * 0.5f, 0f, 1f);
    }
}