using System.Collections.Generic;
using UnityEngine;

namespace MiniTank
{
    /// <summary>Kamuflaj desenlerini oyun sırasında üretir ve önbellekte tutar.</summary>
    public static class CamoTextures
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(CamoDef camo)
        {
            if (camo == null || !camo.pattern) return null;
            Texture2D tex;
            if (cache.TryGetValue(camo.id, out tex) && tex != null) return tex;

            const int n = 256;
            tex = new Texture2D(n, n, TextureFormat.RGB24, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;

            float seedA = 10f + Mathf.Abs(camo.id.Length * 37 + camo.id[0] * 13) % 500;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    // Döşenebilir iki katman: büyük lekeler ve küçük lekeler
                    float big = Tile(u, v, 3f, seedA);
                    float small = Tile(u, v, 7f, seedA + 50f);
                    Color c = camo.a;
                    if (big > 0.53f) c = camo.b;
                    if (small > 0.58f) c = camo.c;
                    // Hafif kirlilik
                    float grain = 0.93f + 0.07f * Mathf.PerlinNoise(u * 60f + seedA, v * 60f);
                    px[y * n + x] = c * grain;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            cache[camo.id] = tex;
            return tex;
        }

        /// <summary>Kenarları birbirine uyan (dikişsiz) Perlin gürültüsü.</summary>
        static float Tile(float u, float v, float freq, float offset)
        {
            float a = Mathf.PerlinNoise(u * freq + offset, v * freq + offset);
            float b = Mathf.PerlinNoise((u - 1f) * freq + offset, v * freq + offset);
            float c = Mathf.PerlinNoise(u * freq + offset, (v - 1f) * freq + offset);
            float d = Mathf.PerlinNoise((u - 1f) * freq + offset, (v - 1f) * freq + offset);
            return a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + d * u * v;
        }
    }
}
