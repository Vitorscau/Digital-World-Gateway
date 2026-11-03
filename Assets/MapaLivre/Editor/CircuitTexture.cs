using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gera uma textura repetível de placa de circuito (chão do tema Original):
/// trilhas quase sempre horizontais, em grupos paralelos ("barramentos"), que desviam em 45°,
/// nunca se cruzam e terminam em anéis vazados.
/// </summary>
public static class CircuitTexture
{
    private const int Grade = 32;           // espaço entre trilhas (px)
    private const float MeiaLargura = 2f;   // trilha de 4 px
    private const float RaioAnel = 7.5f;
    private const int Tentativas = 1600;

    public static Texture2D Create(string assetPath, Color baseColor, Color lineColor, int size = 1024, int seed = 7)
    {
        var cobertura = Gerar(size, seed);
        var px = new Color[size * size];
        for (var i = 0; i < px.Length; i++) px[i] = Color.Lerp(baseColor, lineColor, cobertura[i] * lineColor.a);

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

    /// <summary>Quanto de trilha cobre cada pixel (0 a 1). Linha 0 = base da textura.</summary>
    public static float[] Gerar(int size, int seed)
    {
        var cob = new float[size * size];
        var n = size / Grade;                // células por lado
        var ocupado = new bool[2 * n, 2 * n]; // grade dobrada: nós e pontos do meio dos trechos
        var rnd = new Rnd((uint)seed);
        var aneis = new List<Vector2>();

        for (var t = 0; t < Tentativas; t++)
        {
            var faixas = rnd.Double() < 0.55 ? 1 : 2 + rnd.Next(3);
            var c0 = rnd.Next(n);
            var r0 = rnd.Next(n);

            // Forma da trilha: trechos retos para a direita com desvios de 45° para cima ou para baixo.
            var passos = new List<int>(); // 0 = reto, +1 = desce, -1 = sobe
            var alvo = 6 + rnd.Next(16);
            while (passos.Count < alvo)
            {
                var reto = 2 + rnd.Next(6);
                for (var i = 0; i < reto; i++) passos.Add(0);
                if (rnd.Double() < 0.6)
                {
                    var lado = rnd.Next(2) == 0 ? -1 : 1;
                    var desvio = 1 + rnd.Next(3);
                    for (var i = 0; i < desvio; i++) passos.Add(lado);
                }
            }

            // Cada faixa segue a mesma forma, uma célula abaixo da anterior, e para onde bateria em outra trilha.
            var nesta = new HashSet<(int, int)>();
            for (var f = 0; f < faixas; f++)
            {
                var c = c0;
                var r = r0 + f;
                if (Livre(ocupado, nesta, 2 * c, 2 * r, n) == false) break;
                var pontos = new List<(int, int)> { (2 * c, 2 * r) };
                var nos = new List<Vector2Int> { new Vector2Int(c, r) };
                foreach (var dy in passos)
                {
                    var meio = (2 * c + 1, 2 * r + dy);
                    var prox = (2 * (c + 1), 2 * (r + dy));
                    if (!Livre(ocupado, nesta, meio.Item1, meio.Item2, n) || !Livre(ocupado, nesta, prox.Item1, prox.Item2, n)) break;
                    pontos.Add(meio);
                    pontos.Add(prox);
                    c += 1;
                    r += dy;
                    nos.Add(new Vector2Int(c, r));
                }
                if (nos.Count < 4) { if (f == 0) break; continue; } // curta demais
                foreach (var p in pontos) nesta.Add((Mod(p.Item1, 2 * n), Mod(p.Item2, 2 * n)));

                for (var i = 0; i + 1 < nos.Count; i++) Segmento(cob, size, Centro(nos[i]), Centro(nos[i + 1]));
                aneis.Add(Centro(nos[0]));
                aneis.Add(Centro(nos[nos.Count - 1]));
            }
            foreach (var (x, y) in nesta) ocupado[x, y] = true;
        }

        // Anéis vazados nas pontas: abre o furo e desenha a borda.
        foreach (var a in aneis) Furo(cob, size, a);
        foreach (var a in aneis) Anel(cob, size, a);
        return cob;
    }

    private static Vector2 Centro(Vector2Int no) => new Vector2(no.x * Grade + Grade / 2f, no.y * Grade + Grade / 2f);

    private static int Mod(int v, int m) => ((v % m) + m) % m;

    private static bool Livre(bool[,] ocupado, HashSet<(int, int)> nesta, int x, int y, int n)
    {
        x = Mod(x, 2 * n);
        y = Mod(y, 2 * n);
        return !ocupado[x, y] && !nesta.Contains((x, y));
    }

    // ------------------------------------------------------------ Desenho (com anti-aliasing e repetição nas bordas)

    private static void Marcar(float[] cob, int size, int x, int y, float a)
    {
        if (a <= 0f) return;
        var i = Mod(y, size) * size + Mod(x, size);
        if (a > cob[i]) cob[i] = Mathf.Min(1f, a);
    }

    private static void Segmento(float[] cob, int size, Vector2 a, Vector2 b)
    {
        var x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - 4f);
        var x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + 4f);
        var y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - 4f);
        var y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + 4f);
        var ab = b - a;
        var len2 = Vector2.Dot(ab, ab);
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            var d = Vector2.Distance(p, a + ab * t);
            Marcar(cob, size, x, y, Mathf.Clamp01(MeiaLargura + 0.5f - d));
        }
    }

    private static void Furo(float[] cob, int size, Vector2 c)
    {
        var r = Mathf.CeilToInt(RaioAnel + 2f);
        for (var y = -r; y <= r; y++)
        for (var x = -r; x <= r; x++)
        {
            var px = Mathf.FloorToInt(c.x) + x;
            var py = Mathf.FloorToInt(c.y) + y;
            var d = Vector2.Distance(new Vector2(px + 0.5f, py + 0.5f), c);
            var dentro = Mathf.Clamp01(RaioAnel - MeiaLargura + 0.5f - d); // 1 no miolo do anel
            if (dentro <= 0f) continue;
            var i = Mod(py, size) * size + Mod(px, size);
            cob[i] *= 1f - dentro;
        }
    }

    private static void Anel(float[] cob, int size, Vector2 c)
    {
        var r = Mathf.CeilToInt(RaioAnel + 2f);
        for (var y = -r; y <= r; y++)
        for (var x = -r; x <= r; x++)
        {
            var px = Mathf.FloorToInt(c.x) + x;
            var py = Mathf.FloorToInt(c.y) + y;
            var d = Vector2.Distance(new Vector2(px + 0.5f, py + 0.5f), c);
            Marcar(cob, size, px, py, Mathf.Clamp01(MeiaLargura + 0.5f - Mathf.Abs(d - RaioAnel)));
        }
    }

    /// <summary>Gerador simples (xorshift32), igual em qualquer plataforma.</summary>
    private class Rnd
    {
        private uint _s;
        public Rnd(uint seed) => _s = seed == 0 ? 0x9E3779B9u : seed * 2654435761u;

        private uint Proximo()
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return _s;
        }

        public int Next(int max) => (int)(Proximo() % (uint)max);
        public double Double() => Proximo() / 4294967296.0;
    }
}
