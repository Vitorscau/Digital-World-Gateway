using UnityEngine;

/// <summary>Sprites e texturas simples desenhados por código (coração, anel, círculo, sombra).</summary>
public static class Formas
{
    private static Sprite _coracao, _anel, _circulo;
    private static Texture2D _sombra;

    public static Sprite Coracao()
    {
        if (_coracao != null) return _coracao;
        const int n = 128;
        var t = Nova(n);
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            // Curva do coração: (x² + y² - 1)³ - x² y³ <= 0
            var px = (x / (float)n - 0.5f) * 2.6f;
            var py = (y / (float)n - 0.45f) * 2.6f;
            var v = Mathf.Pow(px * px + py * py - 1f, 3f) - px * px * py * py * py;
            var a = Mathf.Clamp01(-v * 40f);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply();
        return _coracao = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Anel branco (contorno de círculo), para o minigame de treino.</summary>
    public static Sprite Anel()
    {
        if (_anel != null) return _anel;
        const int n = 256;
        var t = Nova(n);
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            var d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
            var a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) * 22f);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply();
        return _anel = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Sprite Circulo()
    {
        if (_circulo != null) return _circulo;
        const int n = 128;
        var t = Nova(n);
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            var d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 30f)));
        }
        t.Apply();
        return _circulo = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite _raios, _brilho;

    /// <summary>Raios de luz saindo do centro (fundo clássico de digivolução), em branco para colorir.</summary>
    public static Sprite Raios()
    {
        if (_raios != null) return _raios;
        const int n = 512;
        var t = Nova(n);
        const int fatias = 16;
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            var dx = x - n / 2f;
            var dy = y - n / 2f;
            var ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * fatias;
            var f = ang - Mathf.Floor(ang);
            var raio = Mathf.Clamp01((0.5f - Mathf.Abs(f - 0.5f)) * 6f - 1f);
            var dist = Mathf.Sqrt(dx * dx + dy * dy) / (n / 2f);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, raio * Mathf.Clamp01(1.1f - dist) * Mathf.Clamp01(dist * 4f)));
        }
        t.Apply();
        return _raios = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Brilho redondo bem suave (luz atrás do Digimon).</summary>
    public static Sprite Brilho()
    {
        if (_brilho != null) return _brilho;
        const int n = 256;
        var t = Nova(n);
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            var d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
            var a = Mathf.Clamp01(1f - d);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
        }
        t.Apply();
        return _brilho = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Mancha escura redonda e suave (sombra no chão, para o Digimon "pisar" no mundo real).</summary>
    public static Texture2D Sombra()
    {
        if (_sombra != null) return _sombra;
        const int n = 64;
        _sombra = Nova(n);
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            var d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
            _sombra.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01(1f - d) * 0.55f));
        }
        _sombra.Apply();
        return _sombra;
    }

    private static Texture2D Nova(int n) => new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

    /// <summary>Material sem luz (cor ou textura), que funciona no URP e fora dele.</summary>
    public static Material MaterialSemLuz(Color cor, Texture tex = null, bool transparente = false)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
        var m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cor);
        m.color = cor;
        if (tex != null)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
        }
        if (transparente && m.HasProperty("_Surface"))
        {
            // URP Unlit em modo transparente.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        return m;
    }
}
