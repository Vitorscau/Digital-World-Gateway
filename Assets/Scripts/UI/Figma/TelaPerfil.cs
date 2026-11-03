using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tela "Profile" (Figma: Frame 32): nível e EXP do Tamer, foto do parceiro com nível e EXP,
/// botões Moves (golpes do parceiro) e Back.
/// </summary>
public class TelaPerfil : TelaFigma
{
    private TMP_Text _tamerNivel, _tamerXp, _parceiroNome, _parceiroXp;
    private RectTransform _barraTamer, _cheioTamer, _barraParceiro, _cheioParceiro;
    private RawImage _foto;
    private GameObject _golpes;

    protected override void Construir()
    {
        FigmaUI.Fundo(Raiz);

        // Título e divisor (padding de 120 no topo, espaços de 40).
        FigmaUI.TextoEm(Raiz, 0, 120f, FigmaUI.Largura, 42f, "Profile", 48f, FigmaUI.Peso.Negrito, sombra: false);
        FigmaUI.Divisor(Raiz, 202f);

        // Tamer (bloco de 324 em y = 242)
        FigmaUI.TextoEm(Raiz, 0, 242f + 58f, FigmaUI.Largura, 58f, "Tamer", 64f, FigmaUI.Peso.Negrito);
        _tamerNivel = FigmaUI.TextoEm(Raiz, 0, 242f + 135f, FigmaUI.Largura, 58f, "", 48f, FigmaUI.Peso.Negrito);
        _barraTamer = FigmaUI.Barra(Raiz, 128.5f, 242f + 212f, 772f, 54f, 0f, out _cheioTamer);
        _tamerXp = FigmaUI.TextoEm(Raiz, 128.5f, 242f + 266f, 772f, 58f, "", 40f, FigmaUI.Peso.Normal, TextAlignmentOptions.Right);
        FigmaUI.Divisor(Raiz, 606f);

        // Partner (título em y = 646, caixa de 748 em y = 744)
        FigmaUI.TextoEm(Raiz, 0, 646f, FigmaUI.Largura, 58f, "Partner", 64f, FigmaUI.Peso.Negrito);
        const float caixaX = (FigmaUI.Largura - 704f) / 2f, caixaY = 744f + 32f;
        FigmaUI.CaixaVidro(Raiz, caixaX, caixaY, 704f, 704f);
        FigmaUI.Cantos(Raiz, caixaX + 38.7f, caixaY + 32.9f, 704f - 77.4f, 704f - 65.8f);
        var foto = FigmaUI.Ret(Raiz, "Foto", caixaX + 77f, caixaY + 70f, 550f, 550f);
        _foto = foto.gameObject.AddComponent<RawImage>();
        _foto.raycastTarget = false;

        // Nome do parceiro, barra e EXP (bloco de 189 em y = 1532)
        _parceiroNome = FigmaUI.TextoEm(Raiz, 0, 1532f, FigmaUI.Largura, 58f, "", 48f, FigmaUI.Peso.Negrito);
        _barraParceiro = FigmaUI.Barra(Raiz, 128.5f, 1532f + 77f, 772f, 54f, 0f, out _cheioParceiro);
        _parceiroXp = FigmaUI.TextoEm(Raiz, 128.5f, 1532f + 131f, 772f, 58f, "", 40f, FigmaUI.Peso.Normal, TextAlignmentOptions.Right);
        FigmaUI.Divisor(Raiz, 1761f);

        FigmaUI.BotaoLargo(Raiz, (FigmaUI.Largura - 855f) / 2f, 1846f, "Moves", MostrarGolpes);
        FigmaUI.BotaoBack(Raiz, (FigmaUI.Largura - 395f) / 2f, 2019.5f, Fechar);

        AoMudarPerfil();
    }

    protected override void AoMudarPerfil()
    {
        var p = PerfilJogador.Atual;
        var xpProx = PerfilJogador.XpParaProximoNivel(p.nivel);
        _tamerNivel.text = $"{p.nome} {p.nivel} Level";
        _tamerXp.text = $"{p.xp}/{xpProx}";
        FigmaUI.AtualizarBarra(_barraTamer, _cheioTamer, (float)p.xp / xpProx);

        var d = p.Parceiro;
        var ficha = d?.Ficha;
        var xpDig = d != null ? PerfilJogador.XpDigimonParaProximoNivel(d.nivel) : 1;
        _parceiroNome.text = d != null ? $"{(ficha != null ? ficha.Name.Trim() : d.especie)} {d.nivel} Level" : "-";
        _parceiroXp.text = d != null ? $"{d.xp}/{xpDig}" : "";
        FigmaUI.AtualizarBarra(_barraParceiro, _cheioParceiro, d != null ? (float)d.xp / xpDig : 0f);
        _foto.texture = RetratoDigimon.Obter(ficha);
        _foto.color = _foto.texture != null ? Color.white : new Color(1, 1, 1, 0);
    }

    /// <summary>Lista dos golpes do parceiro (os que ele já aprendeu e os próximos).</summary>
    private void MostrarGolpes()
    {
        if (_golpes != null) { Destroy(_golpes); _golpes = null; return; }
        var p = PerfilJogador.Atual;
        var d = p.Parceiro;
        var ficha = d?.Ficha;

        var painel = FigmaUI.CaixaVidro(Raiz, 87f, 1150f, 855f, 660f);
        _golpes = painel.gameObject;
        var fundo = FigmaUI.Local(painel, "FundoEscuro", 0, 0, 855f, 660f);
        FigmaUI.Esticar(fundo);
        FigmaUI.Imagem(fundo, FigmaUI.Arredondado(), new Color(0f, 0.38f, 0.55f, 0.92f)).type = Image.Type.Sliced;
        fundo.SetAsFirstSibling();

        var titulo = FigmaUI.Texto(painel, "Moves", 44f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Center);
        titulo.rectTransform.anchorMin = new Vector2(0, 1);
        titulo.rectTransform.anchorMax = new Vector2(1, 1);
        titulo.rectTransform.pivot = new Vector2(0.5f, 1);
        titulo.rectTransform.anchoredPosition = new Vector2(0, -30);
        titulo.rectTransform.sizeDelta = new Vector2(0, 60);

        var golpes = ficha != null && ficha.LearnableMoves != null ? ficha.LearnableMoves.Where(g => g != null && g.movebase != null).ToList() : null;
        var y = 110f;
        if (golpes == null || golpes.Count == 0)
        {
            var t = FigmaUI.Texto(painel, "No moves registered.", 34f, FigmaUI.Peso.Normal, TextAlignmentOptions.Center);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 1);
            t.rectTransform.anchoredPosition = new Vector2(0, -y - 30);
            return;
        }
        foreach (var g in golpes)
        {
            var aprendido = d != null && d.nivel >= g.level;
            var linha = FigmaUI.Local(painel, "Golpe", 40f, y, 775f, 110f);
            var nome = FigmaUI.Texto(linha, (aprendido ? "" : "<alpha=#88>") + g.movebase.Name, 40f, FigmaUI.Peso.Negrito, TextAlignmentOptions.TopLeft);
            FigmaUI.Esticar(nome.rectTransform);
            var info = FigmaUI.Texto(linha, aprendido
                    ? $"Power {g.movebase.Power}  ·  Cost {g.movebase.Cost}  ·  {g.movebase.Type}"
                    : $"Learns at Lv. {g.level}", 30f, FigmaUI.Peso.Normal, TextAlignmentOptions.BottomLeft, false);
            FigmaUI.Esticar(info.rectTransform);
            if (!aprendido) info.color = new Color(1, 1, 1, 0.6f);
            y += 125f;
        }
    }
}
