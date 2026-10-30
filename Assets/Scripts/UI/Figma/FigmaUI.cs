using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Peças das telas desenhadas no Figma ("Digital World Gateway", Page 2).
/// As medidas são as do Figma: tela de 1029 x 2228, origem no canto de cima à esquerda.
/// As imagens ficam em Resources/UI/Figma (baixadas do Figma).
/// </summary>
public static class FigmaUI
{
    public const float Largura = 1029f;
    public const float Altura = 2228f;

    // Cores do Figma
    public static readonly Color AzulFundo = Hex("#00B0FF");
    public static readonly Color AzulBarraFundo = Hex("#00608B");
    public static readonly Color AzulBarra = Hex("#0DB6F9");
    public static readonly Color AzulBalao = Hex("#3174A5");

    public enum Peso { Normal, Negrito }

    private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
    private static Sprite _arredondado, _vidro;

    public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

    /// <summary>Imagem baixada do Figma (Resources/UI/Figma/nome).</summary>
    public static Sprite S(string nome)
    {
        if (_sprites.TryGetValue(nome, out var s) && s != null) return s;
        s = Resources.Load<Sprite>("UI/Figma/" + nome);
        if (s == null) Debug.LogWarning($"[FigmaUI] Imagem não encontrada: UI/Figma/{nome}");
        _sprites[nome] = s;
        return s;
    }

    // ------------------------------------------------------------ Canvas e posicionamento

    /// <summary>Canvas na escala do Figma (altura 2228 sempre cabe na tela).</summary>
    public static Canvas Canvas(Transform pai, string nome, int ordem)
    {
        var go = new GameObject(nome, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(pai, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = ordem;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(Largura, Altura);
        s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        s.matchWidthOrHeight = 1f;
        if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        return c;
    }

    /// <summary>Retângulo nas coordenadas do Figma (x, y a partir do canto de cima à esquerda da tela).</summary>
    public static RectTransform Ret(Transform pai, string nome, float x, float y, float w, float h)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        Posicionar(rt, x, y, w, h);
        return rt;
    }

    /// <summary>Posiciona em coordenadas do Figma, centralizado na horizontal e preso no topo.</summary>
    public static void Posicionar(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x - Largura / 2f, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    /// <summary>Retângulo dentro de outro, em coordenadas locais (x, y a partir do canto de cima à esquerda do pai).</summary>
    public static RectTransform Local(Transform pai, string nome, float x, float y, float w, float h)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    public static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------ Peças

    public static Image Imagem(RectTransform rt, Sprite sprite, Color? cor = null)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = cor ?? Color.white;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Fundo azul com textura de pixels, igual ao das telas (#00B0FF + textura a 15%).</summary>
    public static void Fundo(RectTransform tela, float alfa = 1f)
    {
        var cor = Ret(tela, "Fundo", 0, 0, 1, 1);
        Esticar(cor);
        Imagem(cor, null, new Color(AzulFundo.r, AzulFundo.g, AzulFundo.b, alfa)).raycastTarget = true; // bloqueia toques no mapa
        var textura = Ret(tela, "Textura", 0, 0, 1, 1);
        Esticar(textura);
        var img = Imagem(textura, S("textura_pixel"), new Color(1, 1, 1, 0.15f * alfa));
        img.preserveAspect = false;
    }

    public static TMP_Text Texto(Transform pai, string texto, float tamanho, Peso peso, TextAlignmentOptions alinhamento, bool sombra = true)
    {
        var go = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        var estilo = EstiloUI.Atual;
        var fonte = peso == Peso.Negrito ? estilo.FonteNegrito : estilo.FonteNormal;
        if (fonte != null) t.font = fonte;
        t.text = texto;
        t.fontSize = tamanho;
        t.color = Color.white;
        t.alignment = alinhamento;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        if (sombra) Sombra(t);
        return t;
    }

    /// <summary>Sombra suave preta atrás do texto (text-shadow do Figma).</summary>
    public static void Sombra(TMP_Text t, float forca = 0.6f)
    {
        var m = t.fontMaterial; // instância própria deste texto
        m.EnableKeyword("UNDERLAY_ON");
        m.SetColor("_UnderlayColor", new Color(0, 0, 0, forca));
        m.SetFloat("_UnderlaySoftness", 0.6f);
        m.SetFloat("_UnderlayDilate", 0.15f);
    }

    /// <summary>Texto numa posição de tela (coordenadas do Figma).</summary>
    public static TMP_Text TextoEm(Transform pai, float x, float y, float w, float h, string texto, float tamanho, Peso peso,
        TextAlignmentOptions alinhamento = TextAlignmentOptions.Center, bool sombra = true)
    {
        var t = Texto(pai, texto, tamanho, peso, alinhamento, sombra);
        Posicionar(t.rectTransform, x, y, w, h);
        return t;
    }

    /// <summary>Linha branca divisória (875 x 5), centralizada.</summary>
    public static void Divisor(Transform pai, float y) =>
        Imagem(Ret(pai, "Divisor", (Largura - 875f) / 2f, y - 2.5f, 875f, 5f), S("divisor"));

    /// <summary>Botão "Back" (395 x 133,5). x, y = canto de cima à esquerda.</summary>
    public static Button BotaoBack(Transform pai, float x, float y, Action aoTocar, string rotulo = "Back", float escala = 1f)
    {
        var rt = Ret(pai, "Botao_" + rotulo, x, y, 395f * escala, 133.5f * escala);
        var fundo = Local(rt, "Fundo", 5f * escala, 9f * escala, 382f * escala, 119.5f * escala);
        Imagem(fundo, S("botao_back_fundo"));
        var contorno = Local(rt, "Contorno", 0, 0, 395f * escala, 133.5f * escala);
        Imagem(contorno, S("botao_back_contorno"));
        var t = Texto(rt, rotulo, 35f * escala, Peso.Normal, TextAlignmentOptions.Center);
        Esticar(t.rectTransform);
        return BotaoEm(rt, aoTocar);
    }

    /// <summary>Botão largo (855 x 133,5), como "Moves" e "Scan Data".</summary>
    public static Button BotaoLargo(Transform pai, float x, float y, string rotulo, Action aoTocar)
    {
        var rt = Ret(pai, "Botao_" + rotulo, x, y, 855f, 133.5f);
        Imagem(rt, S("botao_largo"));
        var t = Texto(rt, rotulo, 34f, Peso.Negrito, TextAlignmentOptions.Center, false);
        Esticar(t.rectTransform);
        return BotaoEm(rt, aoTocar);
    }

    public static Button BotaoEm(RectTransform rt, Action aoTocar)
    {
        var alvo = rt.GetComponent<Image>();
        if (alvo == null)
        {
            alvo = rt.gameObject.AddComponent<Image>();
            alvo.color = new Color(0, 0, 0, 0);
        }
        alvo.raycastTarget = true;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = alvo;
        var cores = b.colors;
        cores.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        b.colors = cores;
        b.onClick.AddListener(() => aoTocar?.Invoke());
        return b;
    }

    /// <summary>Barra de progresso do Figma (fundo #00608B, parte cheia #0DB6F9, cantos de 10).</summary>
    public static RectTransform Barra(Transform pai, float x, float y, float w, float h, float progresso, out RectTransform cheio)
    {
        var fundo = Ret(pai, "Barra", x, y, w, h);
        var img = Imagem(fundo, Arredondado(), AzulBarraFundo);
        img.type = Image.Type.Sliced;
        fundo.gameObject.AddComponent<UnityEngine.UI.Shadow>().effectDistance = new Vector2(0, -4);
        cheio = Local(fundo, "Cheio", 0, 0, Mathf.Max(h, w * Mathf.Clamp01(progresso)), h);
        var c = Imagem(cheio, Arredondado(), AzulBarra);
        c.type = Image.Type.Sliced;
        cheio.gameObject.SetActive(progresso > 0.001f);
        return fundo;
    }

    public static void AtualizarBarra(RectTransform fundo, RectTransform cheio, float progresso)
    {
        var w = fundo.sizeDelta.x;
        cheio.gameObject.SetActive(progresso > 0.001f);
        cheio.sizeDelta = new Vector2(Mathf.Max(fundo.sizeDelta.y, w * Mathf.Clamp01(progresso)), cheio.sizeDelta.y);
    }

    /// <summary>Os quatro cantos brancos de mira em volta de uma área (como na caixa do Partner e do QR).</summary>
    public static void Cantos(Transform pai, float x, float y, float w, float h, float tamanho = 93.4f)
    {
        var s = S("canto_1");
        Canto(pai, s, x, y, tamanho, 0f);
        Canto(pai, s, x + w - tamanho, y, tamanho, -90f);
        Canto(pai, s, x + w - tamanho, y + h - tamanho, tamanho, 180f);
        Canto(pai, s, x, y + h - tamanho, tamanho, 90f);
    }

    private static void Canto(Transform pai, Sprite s, float x, float y, float t, float rotacao)
    {
        var rt = Ret(pai, "Canto", x, y, t, t);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition += new Vector2(t / 2f, -t / 2f);
        rt.localEulerAngles = new Vector3(0, 0, rotacao);
        Imagem(rt, s);
    }

    /// <summary>Caixa de vidro: borda branca de 5, cantos de 20, fundo branco translúcido.</summary>
    public static RectTransform CaixaVidro(Transform pai, float x, float y, float w, float h)
    {
        var rt = Ret(pai, "Vidro", x, y, w, h);
        var img = Imagem(rt, Vidro());
        img.type = Image.Type.Sliced;
        return rt;
    }

    // ------------------------------------------------------------ Sprites gerados (cantos arredondados)

    public static Sprite Arredondado()
    {
        if (_arredondado == null) _arredondado = GerarArredondado(48, 10, 0, Color.white, Color.white);
        return _arredondado;
    }

    private static Sprite Vidro()
    {
        if (_vidro == null) _vidro = GerarArredondado(96, 20, 5, new Color(1, 1, 1, 0.1f), Color.white);
        return _vidro;
    }

    private static Sprite GerarArredondado(int tam, int raio, int borda, Color preench, Color corBorda)
    {
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (var y = 0; y < tam; y++)
        for (var x = 0; x < tam; x++)
        {
            // distância até a borda do retângulo arredondado (positiva = dentro)
            var cx = Mathf.Clamp(x + 0.5f, raio, tam - raio);
            var cy = Mathf.Clamp(y + 0.5f, raio, tam - raio);
            var d = raio - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            var a = Mathf.Clamp01(d + 0.5f);
            Color c;
            if (borda > 0 && d < borda) c = new Color(corBorda.r, corBorda.g, corBorda.b, corBorda.a * a);
            else c = new Color(preench.r, preench.g, preench.b, preench.a * a);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        var b = raio + borda + 1;
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }
}
