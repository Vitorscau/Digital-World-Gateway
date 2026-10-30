using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Os Digitamas (ovos) do início do jogo. Cada um choca um Digimon Fresh diferente.
/// Cores e desenhos conforme os ovos de Digimon World -next 0rder-:
/// Botamon (laranja com bolinhas amarelas), Punimon (azul-claro com marcas azul-escuras),
/// Poyomon (azul-escuro com fios ondulados roxos), Pabumon (fúcsia com círculos roxo-escuros)
/// e Kuramon (branco com losangos pretos na metade de baixo).
/// O ovo é uma malha gerada aqui (formato de ovo, mais largo embaixo) e a estampa é pintada por código.
/// </summary>
public static class Digitamas
{
    public enum Padrao { Bolinhas, Manchas, Ondas, Circulos, Losangos, MeioAMeio, Nuvens, Liso, Escamas }

    public class Tipo
    {
        public string Fresh;
        public string Descricao;
        public Color Base, Detalhe;
        public Padrao Padrao;
    }

    private static Color H(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

    public static readonly Tipo[] Todos =
    {
        new Tipo { Fresh = "Botamon", Descricao = "Ovo laranja com bolinhas amarelas", Base = H("#F5872A"), Detalhe = H("#FFE45C"), Padrao = Padrao.Bolinhas },
        new Tipo { Fresh = "Punimon", Descricao = "Ovo azul-claro com marcas azul-escuras", Base = H("#8FD3F7"), Detalhe = H("#1F4E9E"), Padrao = Padrao.Manchas },
        new Tipo { Fresh = "Poyomon", Descricao = "Ovo azul-escuro com fios ondulados roxos", Base = H("#23306E"), Detalhe = H("#A15BD6"), Padrao = Padrao.Ondas },
        new Tipo { Fresh = "Pabumon", Descricao = "Ovo fúcsia com círculos roxo-escuros", Base = H("#E5409A"), Detalhe = H("#4E1A63"), Padrao = Padrao.Circulos },
        new Tipo { Fresh = "Kuramon", Descricao = "Ovo branco com losangos pretos na metade de baixo", Base = H("#F2F2F2"), Detalhe = H("#1A1A1A"), Padrao = Padrao.Losangos },
        // Ovos de Next Order / Re:Digitize / Digimon Story (descrições curtas: cor e, quando há, o desenho).
        new Tipo { Fresh = "Pichimon", Descricao = "Ovo azul-céu com nuvens brancas", Base = H("#6EC6F5"), Detalhe = H("#FFFFFF"), Padrao = Padrao.Nuvens },
        new Tipo { Fresh = "Conomon", Descricao = "Ovo de duas metades, branco em cima e laranja embaixo", Base = H("#F4F1EA"), Detalhe = H("#E8873A"), Padrao = Padrao.MeioAMeio },
        new Tipo { Fresh = "Jyarimon", Descricao = "Ovo vermelho", Base = H("#D6312A"), Detalhe = H("#9E1B17"), Padrao = Padrao.Liso },
        new Tipo { Fresh = "Dodomon", Descricao = "Ovo azul", Base = H("#2F5FD0"), Detalhe = H("#1E3F96"), Padrao = Padrao.Liso },
        new Tipo { Fresh = "Chibomon", Descricao = "Ovo vermelho-claro", Base = H("#F06A5E"), Detalhe = H("#C9443B"), Padrao = Padrao.Liso },
    };

    // ------------------------------------------------------------ Malha

    public const float Altura = 1.3f, Largura = 1f;
    private static Mesh _malha;

    /// <summary>Raio do ovo na altura v (0 = base, 1 = topo), entre 0 e 0,5.</summary>
    private static float Raio(float v)
    {
        var y = v * 2f - 1f;
        return Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)) * (1f - 0.16f * y) * 0.5f * Largura;
    }

    /// <summary>Malha de ovo com UV dando a volta (u) e de baixo para cima (v). Base no chão (y = 0).</summary>
    public static Mesh Malha()
    {
        if (_malha != null) return _malha;
        const int voltas = 48, aneis = 40;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (var i = 0; i <= aneis; i++)
        {
            var v = i / (float)aneis;
            var r = Raio(v);
            for (var j = 0; j <= voltas; j++)
            {
                var u = j / (float)voltas;
                var a = u * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * r, v * Altura, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(u, v));
            }
        }
        for (var i = 0; i < aneis; i++)
        for (var j = 0; j < voltas; j++)
        {
            var a = i * (voltas + 1) + j;
            var b = a + voltas + 1;
            // Sentido horário visto de fora (frente das faces para fora no Unity).
            tris.Add(a); tris.Add(a + 1); tris.Add(b);
            tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
        }
        _malha = new Mesh { name = "Digitama" };
        _malha.SetVertices(verts);
        _malha.SetUVs(0, uvs);
        _malha.SetTriangles(tris, 0);
        _malha.RecalculateNormals();
        // Costura (u = 0 e u = 1): mesma normal dos dois lados, para não aparecer uma linha.
        var normais = _malha.normals;
        for (var i = 0; i <= aneis; i++)
        {
            var a = i * (voltas + 1);
            var b = a + voltas;
            var n = (normais[a] + normais[b]).normalized;
            normais[a] = normais[b] = n;
        }
        _malha.normals = normais;
        _malha.RecalculateBounds();
        return _malha;
    }

    // ------------------------------------------------------------ Estampa

    private static readonly Dictionary<string, Texture2D> _texturas = new Dictionary<string, Texture2D>();

    /// <summary>Textura da estampa (512 x 256): u dá a volta no ovo, v vai da base ao topo.</summary>
    public static Texture2D Textura(Tipo t)
    {
        if (_texturas.TryGetValue(t.Fresh, out var pronta) && pronta != null) return pronta;
        const int w = 512, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Digitama_" + t.Fresh, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        // Medidas "reais" na superfície: a volta tem ~3,14 unidades e a altura 1,3.
        const float volta = Mathf.PI * Largura;
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var u = (x + 0.5f) / w;
            var v = (y + 0.5f) / h;
            var m = EmPontos(t.Padrao)
                ? Mascara3D(t.Padrao, Ponto(u, v))
                : Mascara(t.Padrao, u * volta, v * Altura, volta, v);
            px[y * w + x] = Color.Lerp(t.Base, t.Detalhe, m);
        }
        tex.SetPixels(px);
        tex.Apply(true);
        _texturas[t.Fresh] = tex;
        return tex;
    }

    // ------------------------------------------------------------ Estampas espalhadas na superfície 3D
    // Bolinhas, círculos, manchas e nuvens são colocados em pontos espalhados por igual sobre o ovo
    // (espiral de Fibonacci) e medidos em 3D: não se espremem perto das pontas do ovo.

    private static bool EmPontos(Padrao p) =>
        p == Padrao.Bolinhas || p == Padrao.Circulos || p == Padrao.Manchas || p == Padrao.Nuvens;

    /// <summary>Ponto da superfície do ovo para (u, v) da textura.</summary>
    private static Vector3 Ponto(float u, float v)
    {
        var r = Raio(v);
        var a = u * Mathf.PI * 2f;
        return new Vector3(Mathf.Cos(a) * r, v * Altura, Mathf.Sin(a) * r);
    }

    private static readonly Dictionary<Padrao, List<(Vector3 c, float r)>> _centros = new Dictionary<Padrao, List<(Vector3, float)>>();

    /// <summary>Centros (e raios) dos desenhos de cada padrão, espalhados por igual pelo ovo.</summary>
    private static List<(Vector3 c, float r)> Centros(Padrao p)
    {
        if (_centros.TryGetValue(p, out var pronto)) return pronto;
        int n; float raio, variacao;
        switch (p)
        {
            case Padrao.Bolinhas: n = 64; raio = 0.058f; variacao = 0.012f; break;
            case Padrao.Circulos: n = 24; raio = 0.095f; variacao = 0.01f; break;
            case Padrao.Manchas: n = 11; raio = 0.1f; variacao = 0.03f; break;
            default: n = 9; raio = 0.05f; variacao = 0.01f; break; // nuvens
        }
        var lista = new List<(Vector3, float)>();
        for (var i = 0; i < n; i++)
        {
            var y = 1f - 2f * (i + 0.5f) / n;           // -1..1 (espiral de Fibonacci)
            var v = (y + 1f) * 0.5f;
            if (v < 0.07f || v > 0.95f) continue;       // as pontas ficam lisas
            var u = Mathf.Repeat(i * 0.381966f + Ruido(i, 11) * 0.05f, 1f);
            var r = raio + (Ruido(i, 5) - 0.5f) * 2f * variacao;
            var centro = Ponto(u, v);
            if (p == Padrao.Manchas)
            {
                // Mancha irregular: três círculos encostados.
                for (var k = 0; k < 3; k++)
                {
                    var du = (Ruido(i, 20 + k) - 0.5f) * 0.06f;
                    var dv = (Ruido(i, 30 + k) - 0.5f) * 0.12f;
                    lista.Add((Ponto(Mathf.Repeat(u + du, 1f), Mathf.Clamp01(v + dv)), r * (0.7f + Ruido(i, 40 + k) * 0.5f)));
                }
            }
            else if (p == Padrao.Nuvens)
            {
                // Nuvem: quatro bolinhas fofas em fila.
                for (var k = 0; k < 4; k++)
                    lista.Add((Ponto(Mathf.Repeat(u + (k - 1.5f) * 0.035f, 1f), v + (k % 2) * 0.025f), r + (k % 2) * 0.015f));
            }
            else lista.Add((centro, r));
        }
        _centros[p] = lista;
        return lista;
    }

    private static float Mascara3D(Padrao p, Vector3 ponto)
    {
        var m = 0f;
        foreach (var (c, r) in Centros(p))
        {
            var d = Vector3.Distance(ponto, c);
            if (d < r + 0.02f) m = Mathf.Max(m, Borda(d, r));
        }
        return m;
    }

    /// <summary>Quanto do desenho cobre o ponto (0 = cor do ovo, 1 = cor do detalhe), com borda suave.</summary>
    private static float Mascara(Padrao p, float x, float y, float volta, float v)
    {
        switch (p)
        {
            case Padrao.Bolinhas:
            {
                // Bolinhas em grade desencontrada, com um leve sorteio de posição.
                var m = 0f;
                for (var linha = 0; linha < 7; linha++)
                {
                    var cy = 0.12f + linha * 0.18f;
                    var colunas = 10;
                    for (var c = 0; c < colunas; c++)
                    {
                        var cx = (c + (linha % 2) * 0.5f + Ruido(linha, c) * 0.25f) * volta / colunas;
                        var r = 0.065f + Ruido(c, linha) * 0.02f;
                        m = Mathf.Max(m, Borda(Distancia(x, y, cx, cy + Ruido(c + 7, linha) * 0.04f, volta), r));
                    }
                }
                return m;
            }
            case Padrao.Manchas:
            {
                // Marcas irregulares (manchas de borda ondulada).
                var m = 0f;
                for (var i = 0; i < 9; i++)
                {
                    var cx = (i + Ruido(i, 3) * 0.6f) * volta / 9f;
                    var cy = 0.2f + Ruido(i, 9) * 0.9f;
                    var ang = Mathf.Atan2(y - cy, Delta(x, cx, volta));
                    var r = 0.13f + Ruido(i, 1) * 0.06f;
                    r *= 1f + 0.25f * Mathf.Sin(ang * 3f + i) + 0.12f * Mathf.Sin(ang * 5f + i * 2f);
                    m = Mathf.Max(m, Borda(Distancia(x, y, cx, cy, volta), r));
                }
                return m;
            }
            case Padrao.Ondas:
            {
                // Três fios ondulados dando a volta no ovo.
                var m = 0f;
                for (var i = 0; i < 3; i++)
                {
                    var cy = 0.32f + i * 0.3f;
                    var onda = cy + 0.07f * Mathf.Sin(x / volta * Mathf.PI * 2f * 5f + i * 1.7f);
                    m = Mathf.Max(m, Borda(Mathf.Abs(y - onda), 0.028f));
                }
                return m;
            }
            case Padrao.Circulos:
            {
                // Círculos grandes espalhados.
                var m = 0f;
                for (var linha = 0; linha < 4; linha++)
                {
                    var cy = 0.2f + linha * 0.3f;
                    var colunas = 6;
                    for (var c = 0; c < colunas; c++)
                    {
                        var cx = (c + (linha % 2) * 0.5f) * volta / colunas;
                        m = Mathf.Max(m, Borda(Distancia(x, y, cx, cy, volta), 0.11f));
                    }
                }
                return m;
            }
            case Padrao.MeioAMeio:
                // Cápsula: metade de cima de uma cor, de baixo de outra, com a "junta" levemente ondulada.
                return Mathf.Clamp01((0.62f + 0.015f * Mathf.Sin(x / volta * Mathf.PI * 2f * 6f) - y) * 80f);
            case Padrao.Nuvens:
            {
                // Nuvens brancas: grupos de bolinhas fofas.
                var m = 0f;
                for (var i = 0; i < 7; i++)
                {
                    var cx = (i + Ruido(i, 4) * 0.5f) * volta / 7f;
                    var cy = 0.25f + Ruido(i, 8) * 0.8f;
                    for (var k = 0; k < 4; k++)
                    {
                        var ox = (k - 1.5f) * 0.075f;
                        var oy = (k % 2) * 0.035f;
                        m = Mathf.Max(m, Borda(Distancia(x, y, cx + ox, cy + oy, volta), 0.06f + (k % 2) * 0.015f));
                    }
                }
                return m;
            }
            case Padrao.Escamas:
            {
                // Escamas: meias-luas escuras em fileiras.
                var linha = Mathf.Floor(y / 0.11f);
                var cy = (linha + 1f) * 0.11f;
                var passo = volta / 18f;
                var cx = (Mathf.Floor(x / passo + (linha % 2) * 0.5f) + 0.5f - (linha % 2) * 0.5f) * passo;
                var d = Distancia(x, y, cx, cy, volta);
                return Mathf.Clamp01((Mathf.Abs(d - 0.07f) < 0.012f && y < cy) ? 1f : 0f);
            }
            case Padrao.Liso:
                // Só um degradê suave para a base (o ovo é de uma cor só).
                return Mathf.Clamp01(0.35f - y * 0.3f);
            default:
            {
                // Losangos pretos numa faixa da metade de baixo.
                if (v > 0.5f) return 0f;
                var m = 0f;
                const int n = 8;
                for (var c = 0; c < n; c++)
                {
                    var cx = c * volta / n;
                    var d = Mathf.Abs(Delta(x, cx, volta)) / 0.17f + Mathf.Abs(y - 0.36f) / 0.2f;
                    m = Mathf.Max(m, Mathf.Clamp01((1f - d) * 25f));
                }
                return m;
            }
        }
    }

    /// <summary>Diferença em x dando a volta no ovo (o fim da textura encosta no começo).</summary>
    private static float Delta(float x, float cx, float volta)
    {
        var d = x - cx;
        d -= Mathf.Round(d / volta) * volta;
        return d;
    }

    private static float Distancia(float x, float y, float cx, float cy, float volta)
    {
        var dx = Delta(x, cx, volta);
        var dy = y - cy;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static float Borda(float distancia, float raio) => Mathf.Clamp01((raio - distancia) * 120f);

    private static float Ruido(int a, int b)
    {
        var n = (uint)(a * 374761393 + b * 668265263);
        n = (n ^ (n >> 13)) * 1274126177u;
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    // ------------------------------------------------------------ Objeto

    /// <summary>Cria o ovo na cena (com colisor para ser tocado).</summary>
    public static GameObject Criar(Tipo t, Transform pai)
    {
        var go = new GameObject("Digitama_" + t.Fresh);
        go.transform.SetParent(pai, false);
        go.AddComponent<MeshFilter>().sharedMesh = Malha();
        var r = go.AddComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = "Digitama_" + t.Fresh };
        var tex = Textura(t);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        mat.mainTexture = tex;
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.62f);
        r.sharedMaterial = mat;
        var col = go.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, Altura / 2f, 0);
        col.height = Altura;
        col.radius = Largura / 2f;
        return go;
    }
}
