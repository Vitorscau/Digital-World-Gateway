using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Popup de recompensa sobre o mapa (Figma: tela "2444", Frame 24 de 872 x 877):
/// Acquired Tamer EXP, Acquired Digimon EXP, Acquired Money, Acquired Itens e o botão OK.
/// As recompensas entram numa fila (<see cref="Enfileirar"/>) e aparecem uma por vez quando o jogador está no
/// mapa sem outra tela aberta (quem mostra é a <see cref="HUDParceiro"/>).
/// </summary>
public class TelaRecompensa : TelaFigma
{
    public struct Recompensa
    {
        public int XpTamer, XpDigimon, Bits;
        public RecompensaItem[] Itens;
    }

    private static readonly Queue<Recompensa> Fila = new Queue<Recompensa>();

    /// <summary>Disparado quando o jogador fecha o popup (para a HUD verificar Level Up em seguida).</summary>
    public static event Action Fechou;

    // Posição do popup na tela do Figma
    private const float X = 79f, Y = 474f, W = 872f, H = 877f;

    private Recompensa _r;

    public static bool TemPendente => Fila.Count > 0;

    public static void Enfileirar(Recompensa r) => Fila.Enqueue(r);

    /// <summary>Mostra a próxima recompensa da fila (só no mapa e sem outra tela aberta). Retorna true se mostrou.</summary>
    public static bool MostrarProxima()
    {
        if (Fila.Count == 0 || AlgumaAberta || SceneManager.GetActiveScene().name != Cenas.Mapa) return false;
        var r = Fila.Dequeue();
        var tela = Abrir<TelaRecompensa>();
        tela._r = r;
        tela.Preencher();
        return true;
    }

    protected override void Construir()
    {
        // Sem fundo: o mapa continua aparecendo (como no Figma). Só bloqueia os toques.
        var bloqueio = FigmaUI.Ret(Raiz, "Bloqueio", 0, 0, 1, 1);
        FigmaUI.TelaCheia(bloqueio);
        FigmaUI.Imagem(bloqueio, null, new Color(0, 0, 0, 0)).raycastTarget = true;
    }

    private void Preencher()
    {
        var sprite = FigmaUI.S("painel_recompensa");

        // Borda branca: o mesmo formato, um pouco maior, atrás do painel.
        FigmaUI.Imagem(FigmaUI.Ret(Raiz, "Borda", X - 4f, Y - 4f, W + 8f, H + 8f), sprite, Color.white);

        // Painel azul com a textura de pixels recortada no formato dele.
        var painel = FigmaUI.Ret(Raiz, "Painel", X, Y, W, H);
        FigmaUI.Imagem(painel, sprite);
        painel.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var textura = FigmaUI.Local(painel, "Textura", 0, 0, W, H);
        FigmaUI.Imagem(textura, FigmaUI.S("textura_pixel"), new Color(1, 1, 1, 0.15f));

        Linha(painel, "Acquired Tamer EXP", $"{_r.XpTamer} EXP", 102f, 102f);
        Divisor(painel, 194f);
        Linha(painel, "Acquired Digimon EXP", $"{_r.XpDigimon} EXP", 210f, 201f);
        Divisor(painel, 303f);
        Linha(painel, "Acquired Money", $"{_r.Bits} BIT", 320f, 310f);
        Divisor(painel, 421f);
        Texto(painel, "Acquired Items", 77f, 438f, 484f, 43f, 36f, TextAlignmentOptions.Left);

        var itens = _r.Itens ?? Array.Empty<RecompensaItem>();
        if (itens.Length == 0)
            Texto(painel, "No items", 84f, 570f, 600f, 58f, 36f, TextAlignmentOptions.Left).color = new Color(1, 1, 1, 0.8f);
        for (var i = 0; i < itens.Length && i < 4; i++)
        {
            var x = 84f + i * 197f; // Figma: ícones em x = 84 e 281
            var janela = FigmaUI.Local(painel, "Item", x, 538f, 122f, 122f);
            FigmaUI.Imagem(janela, null, new Color(0.85f, 0.85f, 0.85f));
            TelaBag.IconeRecortado(janela, itens[i].Item);
            Cantos(painel, x, 538f);
            Texto(painel, $"{itens[i].Quantidade}x", x + 122f, 570f, 80f, 58f, 48f, TextAlignmentOptions.Left);
        }

        // Botão OK (Figma: Group 12, 395 x 134 em x = 238, y = 726 do popup).
        FigmaUI.BotaoBack(Raiz, X + 238f, Y + 726f, Fechar, "OK");
    }

    public override void Fechar()
    {
        base.Fechar();
        Fechou?.Invoke();
    }

    private static void Linha(RectTransform pai, string rotulo, string valor, float yRotulo, float yValor)
    {
        Texto(pai, rotulo, 77f, yRotulo, 484f, 43f, 36f, TextAlignmentOptions.Left);
        Texto(pai, valor, 433f, yValor, 364f, 43f, 36f, TextAlignmentOptions.Right);
    }

    private static void Divisor(RectTransform pai, float y) =>
        FigmaUI.Imagem(FigmaUI.Local(pai, "Divisor", 91f, y - 2.5f, 690f, 5f), FigmaUI.S("divisor"));

    /// <summary>Cantos de mira (40 x 40) em volta do ícone do item (122 x 122), como no Figma.</summary>
    private static void Cantos(RectTransform pai, float x, float y)
    {
        const float t = 40f, lado = 130f;
        x -= 4f;
        y -= 4f;
        var s = FigmaUI.S("canto_1");
        void Canto(float cx, float cy, float rotacao)
        {
            var rt = FigmaUI.Local(pai, "Canto", cx, cy, t, t);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition += new Vector2(t / 2f, -t / 2f);
            rt.localEulerAngles = new Vector3(0, 0, rotacao);
            FigmaUI.Imagem(rt, s);
        }
        Canto(x, y, 0f);
        Canto(x + lado - t, y, -90f);
        Canto(x + lado - t, y + lado - t, 180f);
        Canto(x, y + lado - t, 90f);
    }

    private static TMP_Text Texto(RectTransform pai, string texto, float x, float y, float w, float h, float tamanho, TextAlignmentOptions alinhamento)
    {
        var t = FigmaUI.Texto(pai, texto, tamanho, FigmaUI.Peso.Negrito, alinhamento);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return t;
    }
}
