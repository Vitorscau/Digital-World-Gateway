using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tela "Bag" (Figma: 2456): itens do jogador com quantidade e descrição, com busca.
/// Tocar num item usa ele no parceiro.
/// </summary>
public class TelaBag : TelaFigma
{
    private RectTransform _lista;
    private TMP_InputField _busca;

    protected override void Construir()
    {
        MontarMolduraComTitulo("Bag");
        _busca = TelaStorage.CampoBusca(Raiz, 219f, _ => Atualizar());
        FigmaUI.Divisor(Raiz, 429f);

        var area = FigmaUI.Ret(Raiz, "Lista", 71f, 465f, 884f, 1500f);
        _lista = TelaStorage.Rolagem(area);
        var v = _lista.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 40f;
        v.childControlHeight = false;
        v.childControlWidth = false;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = false;

        FigmaUI.BotaoBack(Raiz, (FigmaUI.Largura - 395f) / 2f, 2061.5f, Fechar);
        Atualizar();
    }

    protected override void AoMudarPerfil() => Atualizar();

    private void Atualizar()
    {
        foreach (Transform c in _lista) Destroy(c.gameObject);
        var p = PerfilJogador.Atual;
        var filtro = (_busca != null ? _busca.text : "").Trim().ToLowerInvariant();
        var itens = p.itens.Where(i => i.quantidade > 0).Select(i => (salvo: i, def: Itens.Buscar(i.id)))
            .Where(x => x.def != null && (string.IsNullOrEmpty(filtro) || x.def.Nome.ToLowerInvariant().Contains(filtro)));
        var algum = false;
        foreach (var (salvo, def) in itens) { Linha(def, salvo.quantidade); algum = true; }
        if (!algum)
        {
            var vazio = new GameObject("Vazio", typeof(RectTransform));
            ((RectTransform)vazio.transform).SetParent(_lista, false);
            ((RectTransform)vazio.transform).sizeDelta = new Vector2(884f, 120f);
            var t = FigmaUI.Texto(vazio.transform, "Bag vazia. Vença batalhas para ganhar itens.", 36f, FigmaUI.Peso.Normal, TextAlignmentOptions.Left);
            FigmaUI.Esticar(t.rectTransform);
        }
    }

    /// <summary>
    /// Ícone do item recortado como no Figma (esconde as bordas da imagem original) dentro de <paramref name="janela"/>.
    /// Recortes: Meat 132% x 143% deslocado -17,6% / -16,5%; Bandage 127% deslocado -13,5%.
    /// </summary>
    public static void IconeRecortado(RectTransform janela, string itemId)
    {
        janela.gameObject.AddComponent<RectMask2D>();
        var w = janela.sizeDelta.x;
        var h = janela.sizeDelta.y;
        var icone = itemId == Itens.Bandage
            ? FigmaUI.Local(janela, "Imagem", -0.1354f * w, -0.1354f * h, 1.2708f * w, 1.2708f * h)
            : FigmaUI.Local(janela, "Imagem", -0.176f * w, -0.1646f * h, 1.3232f * w, 1.4286f * h);
        FigmaUI.Imagem(icone, Itens.Icone(itemId));
    }

    /// <summary>Uma linha da Bag, no desenho do Figma: ícone com cantos, quantidade, nome e descrição.</summary>
    private void Linha(DefinicaoItem def, int quantidade)
    {
        var go = new GameObject(def.Id, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_lista, false);
        rt.sizeDelta = new Vector2(884f, 220f);

        // Ícone (188 x 188 em 11, 11) recortado como no Figma, com fundo cinza claro.
        IconeRecortado(FigmaUI.Local(rt, "Icone", 11f, 16f, 188f, 188f), def.Id);

        // Cantos de mira (62 x 56) nos quatro cantos do ícone, como no Figma.
        var a = FigmaUI.S("item_canto_a");
        var b = FigmaUI.S("item_canto_b");
        Canto(rt, a, 0f, 5f, 180f, false);           // cima-esquerda
        Canto(rt, a, 0f, 5f + 154f, 180f, true);     // baixo-esquerda
        Canto(rt, b, 145.5f, 5f, 0f, true);          // cima-direita
        Canto(rt, b, 145.5f, 5f + 154f, 0f, false);  // baixo-direita

        var qtd = FigmaUI.Texto(rt, $"{quantidade}x", 48f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Left, false);
        Reposicionar(qtd.rectTransform, 205f, 65f, 143f, 90f);

        var nome = FigmaUI.Texto(rt, def.Nome, 40f, FigmaUI.Peso.Negrito, TextAlignmentOptions.TopLeft, false);
        Reposicionar(nome.rectTransform, 348f, 0f, 520f, 50f);
        var desc = FigmaUI.Texto(rt, def.Descricao, 36f, FigmaUI.Peso.Normal, TextAlignmentOptions.TopLeft, false);
        desc.textWrappingMode = TextWrappingModes.Normal;
        Reposicionar(desc.rectTransform, 348f, 59f, 480f, 110f);

        var alvo = def;
        FigmaUI.BotaoEm(rt, () => Usar(alvo));
    }

    private static void Canto(RectTransform pai, Sprite s, float x, float y, float rotacao, bool espelharY)
    {
        var rt = FigmaUI.Local(pai, "Canto", x, y, 62.4f, 56.2f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition += new Vector2(31.2f, -28.1f);
        rt.localEulerAngles = new Vector3(0, 0, rotacao);
        if (espelharY) rt.localScale = new Vector3(1, -1, 1);
        FigmaUI.Imagem(rt, s);
    }

    private static RectTransform Reposicionar(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private static void Usar(DefinicaoItem def)
    {
        var p = PerfilJogador.Atual;
        if (p.Quantidade(def.Id) <= 0) return;
        var msg = def.Usar(p);
        if (msg == null)
        {
            AvisoNaTela.Mostrar(def.Id == Itens.Bandage ? "A Bandage já está ativa para a próxima batalha." : $"Não dá para usar {def.Nome} agora.");
            return;
        }
        p.GastarItem(def.Id);
        p.Salvar();
        AvisoNaTela.Mostrar(msg);
    }
}
