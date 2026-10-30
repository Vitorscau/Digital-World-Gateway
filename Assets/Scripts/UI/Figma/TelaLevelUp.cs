using TMPro;
using UnityEngine;

/// <summary>
/// Tela "Level Up" (Figma: 202509): aparece por cima do mapa quando o Tamer sobe de nível.
/// Mostra o novo nível no hexágono e o prêmio (1 Meat por nível) no canto de cima.
/// </summary>
public class TelaLevelUp : TelaFigma
{
    private int _nivel, _premio;

    /// <summary>Mostra a tela se o Tamer subiu de nível desde a última vez. Retorna true se mostrou.</summary>
    public static bool MostrarSePreciso()
    {
        var p = PerfilJogador.Atual;
        if (p.nivel <= p.nivelMostrado) return false;
        var niveis = p.nivel - p.nivelMostrado;
        p.nivelMostrado = p.nivel;
        p.AdicionarItem(Itens.Meat, niveis); // prêmio: 1 Meat por nível
        p.Salvar();
        var tela = Abrir<TelaLevelUp>();
        tela.Preencher(p.nivel, niveis);
        return true;
    }

    protected override void Construir()
    {
        // Fundo azul a 66%, deixando o mapa aparecer por trás.
        FigmaUI.Fundo(Raiz, 0.66f);
    }

    private void Preencher(int nivel, int premio)
    {
        _nivel = nivel;
        _premio = premio;

        FigmaUI.TextoEm(Raiz, 0, 514f, FigmaUI.Largura, 107f, "Level Up", 128f, FigmaUI.Peso.Normal);

        // Hexágono (289 x 250) com o número.
        var hex = FigmaUI.Ret(Raiz, "Hexagono", 370f, 693f, 289f, 289f);
        var img = FigmaUI.Imagem(FigmaUI.Local(hex, "Imagem", 19.4f, 0, 250.3f, 289f), FigmaUI.S("levelup_hexagono"));
        img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.rectTransform.anchoredPosition += new Vector2(125.15f, -144.5f);
        img.rectTransform.localEulerAngles = new Vector3(0, 0, 90f);
        FigmaUI.TextoEm(Raiz, 0, 784f, FigmaUI.Largura, 107f, nivel.ToString(), 128f, FigmaUI.Peso.Negrito);

        FigmaUI.BotaoBack(Raiz, 317f, 1472f, Fechar);

        // Prêmio no canto de cima: ícone do Meat com cantos de mira e "1x".
        var janela = FigmaUI.Ret(Raiz, "Premio", 845f, 41f, 122f, 122f);
        janela.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        FigmaUI.Imagem(FigmaUI.Local(janela, "Imagem", -0.176f * 122f, -0.1646f * 122f, 1.3232f * 122f, 1.4286f * 122f), Itens.Icone(Itens.Meat));
        var a = FigmaUI.S("levelup_canto_a");
        var b = FigmaUI.S("levelup_canto_b");
        Canto(838f, 34f, b, 180f, false);
        Canto(838f, 134f, b, 180f, true);
        Canto(932.5f, 34f, a, 0f, true);
        Canto(932.5f, 134f, a, 0f, false);
        FigmaUI.TextoEm(Raiz, 967f, 73f, 120f, 58f, $"{premio}x", 48f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Left);
    }

    private void Canto(float x, float y, Sprite s, float rotacao, bool espelharY)
    {
        var rt = FigmaUI.Ret(Raiz, "Canto", x, y, 40.5f, 36.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition += new Vector2(20.25f, -18.25f);
        rt.localEulerAngles = new Vector3(0, 0, rotacao);
        if (espelharY) rt.localScale = new Vector3(1, -1, 1);
        FigmaUI.Imagem(rt, s);
    }
}
