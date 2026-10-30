using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Peças de interface no padrão do jogo (EstiloUI): canvas, painel, cartão, botão e texto.
/// Usado pelas telas criadas por código para ficarem iguais às desenhadas.
/// </summary>
public static class UIPadrao
{
    private static EstiloUI E => EstiloUI.Atual;

    public static Canvas Canvas(Transform pai, string nome, int ordem)
    {
        var go = new GameObject(nome, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(pai, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = ordem;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1080, 1920);
        s.matchWidthOrHeight = 0.5f;
        if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        return c;
    }

    /// <summary>Painel azul com textura e moldura branca (como "Stay Aware" e Missões).</summary>
    public static RectTransform Painel(Transform pai, string nome)
    {
        var rt = Imagem(pai, nome, E.Fundo, E.Fundo != null ? Color.white : E.AzulCartao, false);
        var img = rt.GetComponent<Image>();
        img.type = Image.Type.Simple;
        if (E.Moldura != null)
        {
            var moldura = Imagem(rt, "Moldura", E.Moldura, Color.white, true);
            Esticar(moldura);
            moldura.GetComponent<Image>().raycastTarget = false;
        }
        return rt;
    }

    /// <summary>Cartão de lista (fundo azul claro, borda branca de cantos cortados).</summary>
    public static RectTransform Cartao(Transform pai, string nome) =>
        Imagem(pai, nome, E.Cartao, E.Cartao != null ? Color.white : E.AzulCartao, true);

    /// <summary>Botão octogonal azul com texto (ou selecionado, azul escuro).</summary>
    public static Button Botao(Transform pai, string rotulo, Action aoTocar, bool selecionado = false, float tamanhoFonte = 40)
    {
        var rt = Imagem(pai, rotulo, selecionado ? E.BotaoSelecionado : E.Botao,
            E.Botao != null ? Color.white : (selecionado ? E.AzulSelecionado : E.Azul), true);
        var b = rt.gameObject.AddComponent<Button>();
        b.onClick.AddListener(() => aoTocar?.Invoke());
        var t = Texto(rt, rotulo, tamanhoFonte, true, TextAlignmentOptions.Center);
        Esticar(t.rectTransform, 12, 4);
        return b;
    }

    /// <summary>Troca a aparência de um botão entre normal e selecionado.</summary>
    public static void Selecionar(Button b, bool selecionado)
    {
        var img = b.GetComponent<Image>();
        if (E.Botao != null) img.sprite = selecionado ? E.BotaoSelecionado : E.Botao;
        else img.color = selecionado ? E.AzulSelecionado : E.Azul;
    }

    /// <summary>Texto em Lato (negrito ou normal), branco.</summary>
    public static TMP_Text Texto(Transform pai, string conteudo, float tamanho, bool negrito, TextAlignmentOptions alinhamento)
    {
        var go = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        var fonte = negrito ? E.FonteNegrito : E.FonteNormal;
        if (fonte != null) t.font = fonte;
        t.text = conteudo;
        t.fontSize = tamanho;
        t.color = E.TextoClaro;
        t.alignment = alinhamento;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    public static RectTransform Imagem(Transform pai, string nome, Sprite sprite, Color cor, bool fatiada)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(pai, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = cor;
        if (sprite != null && fatiada && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        return (RectTransform)go.transform;
    }

    public static void Esticar(RectTransform rt, float margemX = 0, float margemY = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margemX, margemY);
        rt.offsetMax = new Vector2(-margemX, -margemY);
    }

    public static void Ancorar(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }
}
