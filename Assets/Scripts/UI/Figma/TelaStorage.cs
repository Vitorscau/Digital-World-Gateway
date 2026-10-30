using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tela "Storage" (Figma: 202508): coleção de Digimons em grade de 4 colunas, com busca.
/// Tocar num Digimon escolhe ele como parceiro.
/// </summary>
public class TelaStorage : TelaFigma
{
    private RectTransform _grade;
    private TMP_InputField _busca;

    protected override void Construir()
    {
        MontarMolduraComTitulo("Storage");
        _busca = CampoBusca(Raiz, 219f, _ => Atualizar());
        FigmaUI.Divisor(Raiz, 429f);

        // Grade: 4 colunas de 215 x 214, espaço de 8, começando em x = 71, y = 465. Rola se passar da tela.
        var area = FigmaUI.Ret(Raiz, "Lista", 71f, 465f, 884f, 1500f);
        _grade = Rolagem(area);
        var g = _grade.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(215f, 214f);
        g.spacing = new Vector2(8f, 6f);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 4;

        FigmaUI.BotaoBack(Raiz, (FigmaUI.Largura - 395f) / 2f, 2061.5f, Fechar);
        Atualizar();
    }

    protected override void AoMudarPerfil() => Atualizar();

    private void Atualizar()
    {
        foreach (Transform c in _grade) Destroy(c.gameObject);
        var p = PerfilJogador.Atual;
        var filtro = (_busca != null ? _busca.text : "").Trim().ToLowerInvariant();
        var lista = p.colecao
            .Where(d => string.IsNullOrEmpty(filtro) || (d.Ficha != null ? d.Ficha.Name : d.especie).ToLowerInvariant().Contains(filtro))
            .OrderByDescending(d => d.id == p.parceiroId).ThenByDescending(d => d.nivel);
        foreach (var d in lista) Celula(d, d.id == p.parceiroId);
    }

    /// <summary>Uma célula da grade, no desenho do Figma: fundo, moldura, selo de nível e canto de tipo.</summary>
    private void Celula(DigimonSalvo d, bool parceiro)
    {
        var ficha = d.Ficha;
        var go = new GameObject(d.especie, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_grade, false);

        var fundo = FigmaUI.Local(rt, "Fundo", 4f, 4f, 207f, 207f);
        FigmaUI.Imagem(fundo, null, CorDoAtributo(ficha));
        var foto = FigmaUI.Local(rt, "Foto", 4f, 4f, 207f, 207f);
        var img = foto.gameObject.AddComponent<RawImage>();
        img.texture = RetratoDigimon.Obter(ficha, 256);
        img.raycastTarget = false;
        if (img.texture == null) img.color = new Color(1, 1, 1, 0);

        FigmaUI.Imagem(FigmaUI.Local(rt, "SeloNivel", 99f, 168f, 115f, 46f), FigmaUI.S("tile_lv_fundo"));
        var moldura = FigmaUI.Imagem(FigmaUI.Local(rt, "Moldura", 0, 0, 214.5f, 214f), FigmaUI.S("tile_moldura"));
        if (parceiro) moldura.color = EstiloUI.Atual.Destaque; // o parceiro fica com a moldura amarela
        var nivel = FigmaUI.Texto(rt, $"LV.{d.nivel}", 36f, FigmaUI.Peso.Negrito, TextAlignmentOptions.BottomRight);
        var nr = nivel.rectTransform;
        nr.anchorMin = nr.anchorMax = new Vector2(0, 1);
        nr.pivot = new Vector2(1, 0);
        nr.anchoredPosition = new Vector2(209f, -213f);
        nr.sizeDelta = new Vector2(110f, 44f);
        FigmaUI.Imagem(FigmaUI.Local(rt, "CantoTipo", 4f, 4.5f, 58f, 58f), FigmaUI.S("tile_tipo_canto"), CorDoTipo(ficha));
        var letra = FigmaUI.Texto(FigmaUI.Local(rt, "Tipo", 4f, 4.5f, 58f, 58f), ficha != null ? LetraDoTipo(ficha.Type) : "?", 30f,
            FigmaUI.Peso.Negrito, TextAlignmentOptions.Center, false);
        FigmaUI.Esticar(letra.rectTransform);

        var alvo = d;
        FigmaUI.BotaoEm(rt, () => EscolherParceiro(alvo));
    }

    private static void EscolherParceiro(DigimonSalvo d)
    {
        var p = PerfilJogador.Atual;
        if (d.id == p.parceiroId) return;
        p.DefinirParceiro(d);
        var controlador = FindFirstObjectByType<DigimonController>();
        if (controlador != null && d.Ficha != null) controlador.SetDigimon(d.Ficha);
        AvisoNaTela.Mostrar($"{(d.Ficha != null ? d.Ficha.Name.Trim() : d.especie)} agora é seu parceiro!");
    }

    private static Color CorDoAtributo(DigimonBase f)
    {
        if (f == null) return new Color(0.4f, 0.5f, 0.6f);
        switch (f.Atribute)
        {
            case DigimonAttribute.Fire: return new Color(0.93f, 0.62f, 0.38f);
            case DigimonAttribute.Water: return new Color(0.42f, 0.72f, 0.95f);
            case DigimonAttribute.Plant: return new Color(0.5f, 0.82f, 0.46f);
            case DigimonAttribute.Wind: return new Color(0.66f, 0.9f, 0.88f);
            case DigimonAttribute.Dark: return new Color(0.55f, 0.45f, 0.7f);
            case DigimonAttribute.Light: return new Color(0.98f, 0.92f, 0.6f);
            default: return new Color(0.83f, 0.74f, 0.55f);
        }
    }

    private static Color CorDoTipo(DigimonBase f)
    {
        if (f == null) return Color.white;
        switch (f.Type)
        {
            case DigimonType.Vaccine: return new Color(0.35f, 0.75f, 1f);
            case DigimonType.Data: return new Color(0.45f, 0.85f, 0.45f);
            case DigimonType.Virus: return Color.white; // vermelho original do Figma
            default: return new Color(0.75f, 0.75f, 0.75f);
        }
    }

    private static string LetraDoTipo(DigimonType t) =>
        t == DigimonType.Vaccine ? "Va" : t == DigimonType.Data ? "Da" : t == DigimonType.Virus ? "Vi" : "Fr";

    // ------------------------------------------------------------ Peças compartilhadas com a Bag

    /// <summary>Campo "Buscar" do Figma (fundo #00608B, borda branca de 5, cantos de 10).</summary>
    public static TMP_InputField CampoBusca(RectTransform raiz, float y, System.Action<string> aoMudar)
    {
        var rt = FigmaUI.Ret(raiz, "Buscar", 41f, y, 947f, 80f);
        var img = FigmaUI.Imagem(rt, FigmaUI.Arredondado(), Color.white);
        img.type = Image.Type.Sliced;
        var dentro = FigmaUI.Local(rt, "Dentro", 5f, 5f, 937f, 70f);
        var di = FigmaUI.Imagem(dentro, FigmaUI.Arredondado(), FigmaUI.AzulBarraFundo);
        di.type = Image.Type.Sliced;
        di.raycastTarget = true;

        var area = FigmaUI.Local(dentro, "Texto", 20f, 0, 897f, 70f);
        area.gameObject.AddComponent<RectMask2D>();
        var dica = FigmaUI.Texto(area, "Buscar", 35f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Center, false);
        dica.color = new Color(1, 1, 1, 0.75f);
        FigmaUI.Esticar(dica.rectTransform);
        var texto = FigmaUI.Texto(area, "", 35f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Center, false);
        FigmaUI.Esticar(texto.rectTransform);

        var campo = dentro.gameObject.AddComponent<TMP_InputField>();
        campo.textViewport = area;
        campo.textComponent = texto;
        campo.placeholder = dica;
        campo.fontAsset = texto.font;
        campo.pointSize = 35f;
        campo.onValueChanged.AddListener(v => aoMudar?.Invoke(v));
        return campo;
    }

    /// <summary>Área com rolagem vertical; retorna o conteúdo onde os itens vão.</summary>
    public static RectTransform Rolagem(RectTransform area)
    {
        area.gameObject.AddComponent<RectMask2D>();
        var fundo = area.gameObject.AddComponent<Image>();
        fundo.color = new Color(0, 0, 0, 0);
        var conteudo = FigmaUI.Local(area, "Conteudo", 0, 0, area.sizeDelta.x, 0);
        conteudo.anchorMin = new Vector2(0, 1);
        conteudo.anchorMax = new Vector2(1, 1);
        conteudo.sizeDelta = new Vector2(0, 0);
        conteudo.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = area.gameObject.AddComponent<ScrollRect>();
        sr.content = conteudo;
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;
        return conteudo;
    }
}
