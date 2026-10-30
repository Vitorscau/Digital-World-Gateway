using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Gera uma textura repetível com trilhas de circuito clarinhas (chão do tema Original).</summary>
public static class CircuitTexture
{
    public static Texture2D Create(string assetPath, Color baseColor, Color lineColor, int size = 512, int seed = 7)
    {
        var px = new Color[size * size];
        for (var i = 0; i < px.Length; i++) px[i] = baseColor;
        var rnd = new System.Random(seed);
        const int grid = 16;
        var cells = size / grid;
        var dirs = new[] { (1, 0), (0, 1), (-1, 0), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

        for (var t = 0; t < 70; t++)
        {
            var cx = rnd.Next(cells) * grid;
            var cy = rnd.Next(cells) * grid;
            var d = rnd.Next(4); // começa sempre reto
            var lanes = rnd.NextDouble() < 0.3 ? rnd.Next(2, 4) : 1; // às vezes um "barramento"
            var steps = rnd.Next(2, 6);
            Ring(px, size, cx, cy, 5, lineColor);
            for (var s = 0; s < steps; s++)
            {
                var len = rnd.Next(1, 5) * grid;
                var (dx, dy) = dirs[d];
                for (var l = 0; l < lanes; l++)
                {
                    // faixas paralelas, deslocadas na perpendicular
                    var ox = -dy * l * 7;
                    var oy = dx * l * 7;
                    Line(px, size, cx + ox, cy + oy, cx + ox + dx * len, cy + oy + dy * len, lineColor);
                }
                cx += dx * len;
                cy += dy * len;
                // vira 45° para um lado ou outro
                var turn = rnd.Next(3) - 1;
                d = TurnIndex(d, turn);
            }
            for (var l = 0; l < lanes; l++)
            {
                var (dx, dy) = dirs[d];
                Ring(px, size, cx - dy * l * 7, cy + dx * l * 7, 4, lineColor);
            }
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.SetPixels(px);
        tex.Apply();
        var full = Path.Combine(Application.dataPath, "..", assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllBytes(full, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.mipmapEnabled = true;
        imp.filterMode = FilterMode.Trilinear;
        imp.anisoLevel = 8;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    // Ordem das 8 direções em volta do círculo, para virar de 45 em 45 graus.
    private static readonly int[] Around = { 0, 4, 1, 6, 2, 7, 3, 5 };

    private static int TurnIndex(int d, int turn)
    {
        var pos = Array.IndexOf(Around, d);
        return Around[(pos + turn + 8) % 8];
    }

    private static void Plot(Color[] px, int size, int x, int y, Color c, float a)
    {
        x = ((x % size) + size) % size;
        y = ((y % size) + size) % size;
        var i = y * size + x;
        px[i] = Color.Lerp(px[i], c, a * c.a);
    }

    private static void Line(Color[] px, int size, int x0, int y0, int x1, int y1, Color c)
    {
        var steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (var i = 0; i <= steps; i++)
        {
            var x = x0 + (x1 - x0) * i / Math.Max(1, steps);
            var y = y0 + (y1 - y0) * i / Math.Max(1, steps);
            for (var oy = -1; oy <= 1; oy++)
            for (var ox = -1; ox <= 1; ox++)
                Plot(px, size, x + ox, y + oy, c, ox == 0 && oy == 0 ? 1f : 0.6f);
        }
    }

    private static void Ring(Color[] px, int size, int cx, int cy, int r, Color c)
    {
        for (var y = -r - 1; y <= r + 1; y++)
        for (var x = -r - 1; x <= r + 1; x++)
        {
            var d = Mathf.Sqrt(x * x + y * y);
            var a = Mathf.Clamp01(1.6f - Mathf.Abs(d - r) * 1.2f);
            if (a > 0) Plot(px, size, cx + x, cy + y, c, a);
        }
    }
}
