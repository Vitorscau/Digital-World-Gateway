using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD da batalha sem AR (Battle_Scene), no layout do Figma (tela "2394"): o inimigo em cima à direita e o
/// parceiro embaixo à esquerda, cada um com nome, nível e barra de HP (o parceiro também tem a de MP).
/// As barras descem suaves e mudam de cor (verde → amarelo → vermelho). Montada por código.
/// </summary>
public class HudBatalha : MonoBehaviour
{
    private static readonly Color CorVerde = new Color32(0x4C, 0xD9, 0x64, 0xFF);
    private static readonly Color CorAmarela = new Color32(0xFF, 0xCC, 0x00, 0xFF);
    private static readonly Color CorVermelha = new Color32(0xFF, 0x3B, 0x30, 0xFF);
    private static readonly Color CorMP = new Color32(0x3E, 0xA8, 0xFF, 0xFF);

    private class Cartao
    {
        public CriaturaArena Quem;
        public RectTransform Vida, Mp;
        public Image ImagemVida;
        public TMP_Text TextoVida, TextoMp;
        public float VidaMostrada = 1f, MpMostrado = 1f;
    }

    private Cartao _jogador, _inimigo;

    public static HudBatalha Criar(Transform canvas, CriaturaArena jogador, CriaturaArena inimigo)
    {
        var hud = new GameObject("HUD_Batalha", typeof(RectTransform)).AddComponent<HudBatalha>();
        var rt = (RectTransform)hud.transform;
        rt.SetParent(canvas, false);
        UIPadrao.Esticar(rt);
        hud._inimigo = hud.MontarCartao(rt, inimigo, new Vector2(1f, 1f), new Vector2(-40f, -110f), new Vector2(620f, 170f), false);
        hud._jogador = hud.MontarCartao(rt, jogador, new Vector2(0f, 0f), new Vector2(40f, 150f), new Vector2(680f, 230f), true);
        return hud;
    }

    private Cartao MontarCartao(RectTransform pai, CriaturaArena quem, Vector2 canto, Vector2 posicao, Vector2 tamanho, bool comMp)
    {
        var c = new Cartao { Quem = quem };
        var cartao = UIPadrao.Cartao(pai, quem != null ? quem.name : "Cartao");
        cartao.anchorMin = cartao.anchorMax = canto;
        cartao.pivot = canto;
        cartao.anchoredPosition = posicao;
        cartao.sizeDelta = tamanho;
        cartao.GetComponent<Image>().raycastTarget = false;

        var w = tamanho.x - 60f;
        var nome = quem != null && quem.Dados != null ? $"{quem.Dados.Base.Name.Trim()}  <size=32>Lv.{quem.Dados.Level}</size>" : quem != null ? quem.name : "";
        Colocar(UIPadrao.Texto(cartao, nome, 42f, true, TextAlignmentOptions.MidlineLeft).rectTransform, 30f, 16f, w, 56f);

        c.Vida = Barra(cartao, 30f, 84f, w, 30f, CorVerde, out c.ImagemVida);
        c.TextoVida = UIPadrao.Texto(cartao, "", 30f, false, TextAlignmentOptions.MidlineRight);
        Colocar(c.TextoVida.rectTransform, 30f, 118f, w, 40f);
        if (comMp)
        {
            c.Mp = Barra(cartao, 30f, 168f, w * 0.7f, 20f, CorMP, out _);
            c.TextoMp = UIPadrao.Texto(cartao, "", 28f, false, TextAlignmentOptions.MidlineRight);
            Colocar(c.TextoMp.rectTransform, 30f, 160f, w, 36f);
        }
        return c;
    }

    /// <summary>Barra de cantos arredondados; retorna a parte cheia (a largura é a fração, pela âncora).</summary>
    private static RectTransform Barra(RectTransform pai, float x, float y, float w, float h, Color cor, out Image cheia)
    {
        var fundo = UIPadrao.Imagem(pai, "Barra", FigmaUI.Arredondado(), new Color(0f, 0f, 0f, 0.35f), true);
        Colocar(fundo, x, y, w, h);
        fundo.GetComponent<Image>().raycastTarget = false;
        var parte = UIPadrao.Imagem(fundo, "Cheia", FigmaUI.Arredondado(), cor, true);
        parte.anchorMin = Vector2.zero;
        parte.anchorMax = Vector2.one;
        parte.offsetMin = parte.offsetMax = Vector2.zero;
        cheia = parte.GetComponent<Image>();
        cheia.raycastTarget = false;
        return parte;
    }

    private static void Colocar(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private void Update()
    {
        Atualizar(_jogador);
        Atualizar(_inimigo);
    }

    private static void Atualizar(Cartao c)
    {
        if (c == null || c.Quem == null) return;
        var vida = c.Quem.VidaAtual / (float)Mathf.Max(1, c.Quem.VidaMaxima);
        c.VidaMostrada = Mathf.MoveTowards(c.VidaMostrada, vida, Time.unscaledDeltaTime * 1.5f);
        c.Vida.anchorMax = new Vector2(Mathf.Max(0.02f, c.VidaMostrada), 1f);
        c.ImagemVida.color = vida > 0.5f ? CorVerde : vida > 0.2f ? CorAmarela : CorVermelha;
        c.TextoVida.text = $"{c.Quem.VidaAtual}/{c.Quem.VidaMaxima}";
        if (c.Mp == null) return;
        var mp = c.Quem.MPAtual / (float)Mathf.Max(1, c.Quem.MPMaximo);
        c.MpMostrado = Mathf.MoveTowards(c.MpMostrado, mp, Time.unscaledDeltaTime * 2f);
        c.Mp.anchorMax = new Vector2(Mathf.Max(0.02f, c.MpMostrado), 1f);
        c.TextoMp.text = $"MP {c.Quem.MPAtual}/{c.Quem.MPMaximo}";
    }
}
