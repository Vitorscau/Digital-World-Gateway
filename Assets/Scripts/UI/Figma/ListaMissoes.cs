using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Preenche um painel de missões da Map_Scene (imagens "Frame 41 / 26 / 43", que sobem com o
/// <see cref="MissionMenuController"/>) com as missões de verdade: troca a imagem pelo fundo sem os cartões de
/// exemplo e monta os cartões (ícone, progresso, título, EXP, Money e itens) nas medidas da imagem (1001 x 2227).
/// </summary>
public class ListaMissoes : MonoBehaviour
{
    // Medidas da imagem do painel (px)
    private const float Largura = 1001f, Altura = 2227f;
    private const float ListaX = 33f, ListaY = 365f, CartaoW = 935f;
    private const float LinhaX = (CartaoW - 875f) / 2f;

    private static readonly Color CorAmarelo = FigmaUI.Hex("#FFD21F");
    // Tons multiplicados sobre o octógono do Figma (azul com borda branca).
    private static readonly Color CorPassoBloqueado = new Color(0.45f, 0.62f, 0.78f);
    private static readonly Color CorPassoFeito = new Color(0.78f, 0.88f, 1f);

    private static readonly List<ListaMissoes> Ativas = new List<ListaMissoes>();

    private CategoriaMissao _categoria;
    private RectTransform _conteudo, _area, _lista;
    private ScrollRect _rolagem;
    private TMP_Text _relogioDiarias;
    private float _proximoRelogio;
    private bool _atualizando;
    private Vector3 _posicaoAnterior;

    /// <summary>Prepara o painel (uma vez) e mostra as missões da categoria.</summary>
    public static ListaMissoes Preparar(Image painel, CategoriaMissao categoria)
    {
        var l = painel.GetComponent<ListaMissoes>();
        if (l == null) l = painel.gameObject.AddComponent<ListaMissoes>();
        l.Montar(painel, categoria);
        return l;
    }

    /// <summary>
    /// O toque começou numa lista que ainda pode rolar para cima? Aí o arraste para baixo é rolagem,
    /// não "puxar para fechar".
    /// </summary>
    public static bool ToqueEmListaRolando(Vector2 posicaoTela)
    {
        foreach (var l in Ativas)
        {
            if (l._area == null || !l._area.gameObject.activeInHierarchy) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(l._area, posicaoTela, l.Camera())) continue;
            var cabe = l._lista.rect.height <= l._area.rect.height + 1f;
            if (!cabe && l._rolagem.verticalNormalizedPosition < 0.999f) return true;
        }
        return false;
    }

    private void Montar(Image painel, CategoriaMissao categoria)
    {
        _categoria = categoria;
        if (_conteudo != null) { Atualizar(); return; }

        var limpo = FigmaUI.S("missoes_fundo"); // a mesma imagem, sem os cartões de exemplo
        if (limpo != null) painel.sprite = limpo;

        // "Conteudo" tem o tamanho da imagem em px, escalado para caber no retângulo do painel (preserve aspect).
        var r = painel.rectTransform.rect;
        _conteudo = (RectTransform)new GameObject("Conteudo", typeof(RectTransform)).transform;
        _conteudo.SetParent(painel.transform, false);
        _conteudo.SetAsFirstSibling(); // as abas (botões da cena) ficam por cima
        _conteudo.anchorMin = _conteudo.anchorMax = new Vector2(0.5f, 0.5f);
        _conteudo.pivot = new Vector2(0.5f, 0.5f);
        _conteudo.anchoredPosition = r.center;
        _conteudo.sizeDelta = new Vector2(Largura, Altura);
        _conteudo.localScale = Vector3.one * Mathf.Min(r.width / Largura, r.height / Altura);

        _area = FigmaUI.Local(_conteudo, "Lista", ListaX, ListaY, CartaoW, 1300f);
        _lista = TelaStorage.Rolagem(_area);
        _rolagem = _area.GetComponent<ScrollRect>();
        var v = _lista.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 40f;
        v.padding = new RectOffset(0, 0, 0, 40);
        v.childControlHeight = false;
        v.childControlWidth = false;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = false;
        Atualizar();
    }

    private void OnEnable()
    {
        Ativas.Add(this);
        PerfilJogador.Mudou += Atualizar;
        if (_rolagem != null) _rolagem.verticalNormalizedPosition = 1f;
        Atualizar();
    }

    private void OnDisable()
    {
        Ativas.Remove(this);
        PerfilJogador.Mudou -= Atualizar;
    }

    private Camera Camera()
    {
        var c = GetComponentInParent<Canvas>();
        return c == null || c.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.rootCanvas.worldCamera;
    }

    private void LateUpdate()
    {
        AjustarAltura();
        if (_relogioDiarias == null || Time.unscaledTime < _proximoRelogio) return;
        _proximoRelogio = Time.unscaledTime + 1f;
        var falta = Missoes.TempoParaNovasDiarias();
        _relogioDiarias.text = $"New daily missions in {(int)falta.TotalHours}h {falta.Minutes:00}m";
    }

    /// <summary>
    /// O painel passa da borda de baixo da tela: a lista termina onde a tela termina, para dar para rolar até
    /// o último cartão. Só mede com o painel parado e aberto (não no meio da animação).
    /// </summary>
    private void AjustarAltura()
    {
        if (_area == null) return;
        var parado = _conteudo.position == _posicaoAnterior;
        _posicaoAnterior = _conteudo.position;
        if (!parado) return;
        var cam = Camera();
        var topo = RectTransformUtility.WorldToScreenPoint(cam, _area.position);
        if (topo.y < 0f || topo.y > Screen.height) return; // fechado (fora da tela)
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_conteudo,
                new Vector2(Screen.width / 2f, Screen.safeArea.yMin + 10f), cam, out var p)) return;
        var fundoTela = Altura / 2f - p.y; // em px da imagem, a partir do topo
        var altura = Mathf.Clamp(fundoTela - ListaY - 20f, 400f, Altura - ListaY - 20f);
        if (Mathf.Abs(_area.sizeDelta.y - altura) > 2f) _area.sizeDelta = new Vector2(CartaoW, altura);
    }

    private void Atualizar()
    {
        if (_atualizando || _lista == null || !isActiveAndEnabled) return; // as consultas podem salvar o perfil e chamar isto de novo
        _atualizando = true;
        try
        {
            foreach (Transform c in _lista) Destroy(c.gameObject);
            _relogioDiarias = null;
            switch (_categoria)
            {
                case CategoriaMissao.Historia: MontarHistoria(); break;
                case CategoriaMissao.Diaria: MontarDiarias(); break;
                default: MontarEspeciais(); break;
            }
        }
        finally { _atualizando = false; }
    }

    // ------------------------------------------------------------ Abas

    private void MontarHistoria()
    {
        var h = Missoes.HistoriaAtual();
        if (h == null)
        {
            Aviso("You've completed every chapter of the story.\nNew chapters coming soon!");
            return;
        }

        // Cartão do capítulo: título, objetivo atual e a trilha de passos (Figma: Frame 41).
        var cap = Caixa("Capitulo", 415f);
        var titulo = Texto(cap, h.Capitulo.Titulo, 40f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Center, 40f, 80f, CartaoW - 80f, 60f);
        AutoTamanho(titulo, 28f, 40f);
        var objetivo = Texto(cap, $"Step {h.Passo + 1} of {h.Capitulo.Passos.Length}: {h.Missao.Titulo}", 36f, FigmaUI.Peso.Normal,
            TextAlignmentOptions.Center, 40f, 140f, CartaoW - 80f, 50f);
        AutoTamanho(objetivo, 24f, 36f);

        var n = h.Capitulo.Passos.Length;
        const float tam = 92f, nosY = 255f;
        var passo = (CartaoW - 2f * 91f - tam) / Mathf.Max(1, n - 1);
        for (var i = 0; i < n; i++)
        {
            var x = 91f + i * passo;
            if (i < n - 1)
                FigmaUI.Imagem(FigmaUI.Local(cap, "Ligacao", x + tam + 12f, nosY + 49f, passo - tam - 24f, 3f), null, new Color(1, 1, 1, 0.85f));
            var estado = i < h.Passo ? 1 : i == h.Passo ? 0 : -1; // feito, atual, bloqueado
            var no = Icone(cap, x, nosY, tam, estado < 0 ? null : h.Capitulo.Passos[i],
                estado > 0 ? CorPassoFeito : estado < 0 ? CorPassoBloqueado : (Color?)null);
            if (estado == 0) no.localScale = Vector3.one * 1.12f; // passo atual em destaque
        }

        Cartao(h.Missao, h.Progresso, h.Capitulo.Titulo);
    }

    private void MontarDiarias()
    {
        foreach (var (def, prog) in Missoes.DeHoje()) Cartao(def, prog, null);
        var linha = Linha("Relogio", 80f);
        _relogioDiarias = Texto(linha, "", 34f, FigmaUI.Peso.Normal, TextAlignmentOptions.Center, 0f, 0f, CartaoW, 80f);
        _proximoRelogio = 0f;
    }

    private void MontarEspeciais()
    {
        var eventos = Missoes.EventosAtivos();
        if (eventos.Count == 0)
        {
            Aviso("No special event right now.\nCheck back soon!");
            return;
        }
        foreach (var e in eventos)
        {
            // Cabeçalho do evento (Figma: Frame 43): título amarelo, descrição e quanto falta.
            var caixa = Caixa("Evento", 300f);
            var t = Texto(caixa, e.Titulo, 40f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Center, 40f, 70f, CartaoW - 80f, 60f);
            t.color = CorAmarelo;
            AutoTamanho(t, 28f, 40f);
            AutoTamanho(Texto(caixa, e.Descricao, 36f, FigmaUI.Peso.Normal, TextAlignmentOptions.Center, 40f, 135f, CartaoW - 80f, 50f), 24f, 36f);
            var dias = Mathf.Max(0, (int)Math.Ceiling((e.Fim.Date.AddDays(1) - DateTime.Now).TotalDays));
            var fim = Texto(caixa, dias <= 1 ? "Ends today!" : $"Ends in {dias} days", 32f, FigmaUI.Peso.Normal,
                TextAlignmentOptions.Center, 40f, 205f, CartaoW - 80f, 50f);
            fim.color = new Color(1, 1, 1, 0.8f);

            foreach (var def in e.Missoes) Cartao(def, Missoes.ProgressoEspecial(e, def), null);
        }
    }

    // ------------------------------------------------------------ Cartão de missão

    /// <summary>
    /// Cartão do Figma: ícone + progresso, título (e subtítulo), linhas EXP e Money e os itens de recompensa.
    /// Concluída: fica mais apagada e o progresso vira "Concluída".
    /// </summary>
    private void Cartao(DefinicaoMissao def, ProgressoMissao prog, string subtitulo)
    {
        var temItens = def.RecompensaItens.Length > 0;
        var cartao = Caixa(def.Id, temItens ? 572f : 470f);
        if (prog.resgatada) cartao.gameObject.AddComponent<CanvasGroup>().alpha = 0.6f;

        Icone(cartao, 32f, 28f, 94f, def, null);
        var progresso = Texto(cartao, prog.resgatada ? "Completed" : Missoes.TextoProgresso(def, prog.progresso), 34f, FigmaUI.Peso.Normal,
            TextAlignmentOptions.Center, 0f, 140f, 158f, 44f);
        AutoTamanho(progresso, 22f, 34f);
        if (prog.resgatada) progresso.color = CorAmarelo;

        var semSub = string.IsNullOrEmpty(subtitulo);
        var titulo = Texto(cartao, def.Titulo, 40f, FigmaUI.Peso.Negrito, TextAlignmentOptions.MidlineLeft,
            183f, semSub ? 70f : 45f, CartaoW - 183f - 32f, 70f);
        titulo.overflowMode = TextOverflowModes.Ellipsis;
        if (!semSub)
        {
            var sub = Texto(cartao, subtitulo, 34f, FigmaUI.Peso.Normal, TextAlignmentOptions.MidlineLeft, 183f, 115f, CartaoW - 183f - 32f, 55f);
            sub.overflowMode = TextOverflowModes.Ellipsis;
        }

        LinhaDivisoria(cartao, 207f);
        Valor(cartao, 228f, "EXP", $"{def.RecompensaXp} EXP");
        LinhaDivisoria(cartao, 309f);
        Valor(cartao, 330f, "Money", $"{def.RecompensaBits} BIT");
        if (!temItens) return;
        LinhaDivisoria(cartao, 411f);
        var x = 35f;
        foreach (var r in def.RecompensaItens)
        {
            var janela = FigmaUI.Local(cartao, "Item", x, 448f, 88f, 88f);
            FigmaUI.Imagem(janela, FigmaUI.Arredondado(), new Color(0.85f, 0.85f, 0.85f)).type = Image.Type.Sliced;
            TelaBag.IconeRecortado(janela, r.Item);
            Cantos(cartao, x - 4f, 444f, 96f, 30f);
            Texto(cartao, $"{r.Quantidade}x", 32f, FigmaUI.Peso.Normal, TextAlignmentOptions.MidlineLeft, x + 90f, 470f, 80f, 44f);
            x += 200f;
        }
    }

    private static void Valor(RectTransform cartao, float y, string nome, string valor)
    {
        Texto(cartao, nome, 40f, FigmaUI.Peso.Negrito, TextAlignmentOptions.MidlineLeft, 35f, y, 400f, 60f);
        Texto(cartao, valor, 40f, FigmaUI.Peso.Negrito, TextAlignmentOptions.MidlineRight, CartaoW - 32f - 400f, y, 400f, 60f);
    }

    private static void LinhaDivisoria(RectTransform pai, float y) =>
        FigmaUI.Imagem(FigmaUI.Local(pai, "Linha", LinhaX, y, 875f, 3f), null, new Color(1, 1, 1, 0.85f));

    // ------------------------------------------------------------ Peças

    /// <summary>Octógono com borda branca e o ícone do tipo da missão. <paramref name="def"/> null = sem ícone.</summary>
    private static RectTransform Icone(RectTransform pai, float x, float y, float largura, DefinicaoMissao def, Color? cor)
    {
        var alto = largura * 104f / 94f;
        var rt = FigmaUI.Local(pai, "Icone", x, y, largura, alto);
        rt.pivot = new Vector2(0.5f, 0.5f); // para destacar com escala a partir do centro
        rt.anchoredPosition += new Vector2(largura / 2f, -alto / 2f);
        var fundo = FigmaUI.Imagem(rt, FigmaUI.S("hud_icone_missao_fundo"));
        if (cor.HasValue) fundo.color = cor.Value;
        if (def == null) return rt;
        var lado = largura * 0.52f;
        var glifo = FigmaUI.Local(rt, "Glifo", (largura - lado) / 2f, (alto - lado) / 2f, lado, lado);
        FigmaUI.Imagem(glifo, FigmaUI.S(NomeIcone(def.Tipo))).preserveAspect = true;
        return rt;
    }

    /// <summary>Ícone (Resources/UI/Figma) de cada tipo de missão.</summary>
    public static string NomeIcone(TipoMissao tipo)
    {
        switch (tipo)
        {
            case TipoMissao.AlimentarDigimon: return "icone_missao_comida";
            case TipoMissao.CapturarDigimons:
            case TipoMissao.CapturarEspecie: return "icone_missao_download";
            case TipoMissao.AndarMetros: return "icone_missao_passos";
            case TipoMissao.DecifrarDados: return "icone_missao_dados";
            case TipoMissao.TreinarDigimon: return "icone_missao_treino";
            case TipoMissao.Digivolver: return "icone_missao_digivolucao";
            case TipoMissao.EncontrarNoHabitat: return "icone_missao_busca";
            default: return "hud_icone_missao"; // espadas: batalhas
        }
    }

    /// <summary>Cantos brancos de mira em volta do ícone do item (coordenadas locais do cartão).</summary>
    private static void Cantos(RectTransform pai, float x, float y, float lado, float tamanho)
    {
        var s = FigmaUI.S("canto_1");
        void Canto(float cx, float cy, float rotacao)
        {
            var rt = FigmaUI.Local(pai, "Canto", cx, cy, tamanho, tamanho);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition += new Vector2(tamanho / 2f, -tamanho / 2f);
            rt.localEulerAngles = new Vector3(0, 0, rotacao);
            FigmaUI.Imagem(rt, s);
        }
        Canto(x, y, 0f);
        Canto(x + lado - tamanho, y, -90f);
        Canto(x + lado - tamanho, y + lado - tamanho, 180f);
        Canto(x, y + lado - tamanho, 90f);
    }

    /// <summary>Linha vazia da lista (sem fundo).</summary>
    private RectTransform Linha(string nome, float altura)
    {
        var rt = (RectTransform)new GameObject(nome, typeof(RectTransform)).transform;
        rt.SetParent(_lista, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(CartaoW, altura);
        return rt;
    }

    /// <summary>Linha da lista com o fundo dos cartões do Figma (azul com textura de pixels e borda branca).</summary>
    private RectTransform Caixa(string nome, float altura) => FigmaUI.PainelAzul(Linha(nome, altura));

    private void Aviso(string texto)
    {
        var caixa = Caixa("Aviso", 300f);
        var t = Texto(caixa, texto, 38f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Center, 40f, 0f, CartaoW - 80f, 300f);
        t.textWrappingMode = TextWrappingModes.Normal;
    }

    private static void AutoTamanho(TMP_Text t, float min, float max)
    {
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
    }

    /// <summary>Texto num retângulo local (x, y a partir do canto de cima à esquerda do pai).</summary>
    private static TMP_Text Texto(RectTransform pai, string texto, float tamanho, FigmaUI.Peso peso, TextAlignmentOptions alinhamento,
        float x, float y, float w, float h)
    {
        var t = FigmaUI.Texto(pai, texto, tamanho, peso, alinhamento);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return t;
    }
}
