using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.Maps;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Painel "Digivice" no mapa (botão no canto da tela), com três abas:
/// - Por perto: Digimons próximos, distância e tempo até sumir (tocar = batalhar).
/// - Coleção: Digimons capturados (tocar = escolher como parceiro).
/// - Missões: missões diárias e progresso.
/// No topo mostra nível, XP e bits do Tamer. Montado por código ao abrir a Map_Scene.
/// </summary>
public class PainelDigivice : MonoBehaviour
{
    private enum Aba { PorPerto, Colecao, Missoes }

    private static readonly Color CorFundo = new Color(0.06f, 0.16f, 0.2f, 0.95f);
    private static readonly Color CorBotao = new Color(0.35f, 0.82f, 0.6f);
    private static readonly Color CorBotaoInativo = new Color(0.2f, 0.42f, 0.4f);
    private static readonly Color CorLinha = new Color(1f, 1f, 1f, 0.07f);

    private GameObject _painel;
    private GameObject _botaoAbrir;
    private TMP_Text _cabecalho;
    private RectTransform _conteudo;
    private readonly Dictionary<Aba, Image> _abas = new Dictionary<Aba, Image>();
    private Aba _aba = Aba.PorPerto;
    private float _proximaAtualizacao;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Registrar()
    {
        SceneManager.sceneLoaded -= AoCarregar;
        SceneManager.sceneLoaded += AoCarregar;
        AoCarregar(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void AoCarregar(Scene cena, LoadSceneMode modo)
    {
        if (cena.name != Cenas.Mapa || FindFirstObjectByType<PainelDigivice>() != null) return;
        new GameObject("PainelDigivice").AddComponent<PainelDigivice>();
    }

    private void Start()
    {
        Montar();
        PerfilJogador.Mudou += AoMudarPerfil;
    }

    private void OnDestroy() => PerfilJogador.Mudou -= AoMudarPerfil;

    private void AoMudarPerfil()
    {
        if (_painel != null && _painel.activeSelf) Atualizar();
    }

    private void Update()
    {
        // "Por perto" muda o tempo todo (distâncias, tempo restante).
        if (_painel.activeSelf && _aba == Aba.PorPerto && Time.unscaledTime >= _proximaAtualizacao)
            Atualizar();
    }

    // ------------------------------------------------------------ Conteúdo

    private void Abrir(bool abrir)
    {
        _painel.SetActive(abrir);
        _botaoAbrir.SetActive(!abrir);
        if (abrir) Atualizar();
    }

    private void TrocarAba(Aba aba)
    {
        _aba = aba;
        Atualizar();
    }

    private void Atualizar()
    {
        _proximaAtualizacao = Time.unscaledTime + 1f;
        var p = PerfilJogador.Atual;
        _cabecalho.text = $"<b>{p.nome}</b>  nível {p.nivel}    XP {p.xp}/{PerfilJogador.XpParaProximoNivel(p.nivel)}    {p.bits} bits\n" +
                          $"<size=30>Vitórias {p.vitorias} · Derrotas {p.derrotas} · {p.metrosAndados / 1000:0.0} km andados</size>";
        foreach (var kv in _abas) kv.Value.color = kv.Key == _aba ? CorBotao : CorBotaoInativo;

        foreach (Transform c in _conteudo) Destroy(c.gameObject);
        switch (_aba)
        {
            case Aba.PorPerto: ListarPorPerto(); break;
            case Aba.Colecao: ListarColecao(p); break;
            case Aba.Missoes: ListarMissoes(); break;
        }
    }

    private void ListarPorPerto()
    {
        var spawner = _DigimonGOP.Instance;
        var mapa = spawner != null ? spawner.MapView : null;
        if (spawner == null || mapa == null) { Linha("Mapa ainda carregando...", null); return; }

        var lista = spawner.Visiveis
            .Select(w => (w, d: LightshipMapView.DistanceMeters(mapa.MapCenter, w.Local)))
            .OrderBy(x => x.d).ToList();
        if (lista.Count == 0) { Linha("Nenhum Digimon por perto. Ande um pouco!", null); return; }

        foreach (var (w, d) in lista)
        {
            var resta = w.TempoRestante;
            var perto = w.PodeEncontrar;
            var texto = $"<b>{w.NomeExibicao}</b>  nv. {w.Nivel}   <size=30>{HabitatTexto(w.Habitat)}</size>\n" +
                        $"<size=32>{d:0} m{(perto ? "  <color=#7CFFB2>· toque para batalhar</color>" : "")}" +
                        $"   · some em {Math.Max(0, (int)resta.TotalMinutes)}:{Math.Max(0, resta.Seconds):00}</size>";
            var alvo = w;
            Linha(texto, () => { Abrir(false); alvo.TentarEncontro(); });
        }
    }

    private void ListarColecao(PerfilJogador p)
    {
        foreach (var d in p.colecao.OrderByDescending(x => x.id == p.parceiroId).ThenByDescending(x => x.nivel))
        {
            var ficha = d.Ficha;
            var nome = ficha != null ? ficha.Name.Trim() : d.especie;
            var parceiro = d.id == p.parceiroId;
            var info = ficha != null ? $"{EstagioTexto(ficha.Stage)} · {AtributoTexto(ficha.Atribute)} · {ficha.Type}" : "";
            var texto = $"{(parceiro ? "<color=#FFE066>» </color>" : "")}<b>{nome}</b>  nv. {d.nivel}" +
                        $"   <size=30>XP {d.xp}/{PerfilJogador.XpDigimonParaProximoNivel(d.nivel)}</size>\n<size=30>{info}" +
                        $"{(parceiro ? "  · <color=#FFE066>parceiro</color>" : "  · toque para escolher como parceiro")}</size>";
            var alvo = d;
            Linha(texto, parceiro ? (Action)null : () => EscolherParceiro(alvo));
        }
    }

    private void ListarMissoes()
    {
        Linha("<b>Missões de hoje</b>  <size=30>(novas todo dia)</size>", null);
        foreach (var (def, prog) in Missoes.DeHoje())
        {
            var feita = prog.resgatada;
            var texto = $"{(feita ? "<color=#7CFFB2>[feita]</color> " : "")}<b>{def.Titulo}</b>\n" +
                        $"<size=30>{prog.progresso}/{def.Meta}   ·   recompensa: {def.RecompensaXp} XP, {def.RecompensaBits} bits</size>";
            Linha(texto, null);
        }
    }

    private void EscolherParceiro(DigimonSalvo d)
    {
        var p = PerfilJogador.Atual;
        p.DefinirParceiro(d);
        var controlador = FindFirstObjectByType<DigimonController>();
        if (controlador != null && d.Ficha != null) controlador.SetDigimon(d.Ficha);
        AvisoNaTela.Mostrar($"{(d.Ficha != null ? d.Ficha.Name.Trim() : d.especie)} agora é seu parceiro!");
    }

    private static string HabitatTexto(HabitatDigimon h) =>
        h == HabitatDigimon.Agua ? "água" : h == HabitatDigimon.Parque ? "parque" : "cidade";

    private static string EstagioTexto(DigimonStage s) =>
        s == DigimonStage.InTraining ? "Bebê II" : s == DigimonStage.Rookie ? "Criança" : s == DigimonStage.Champion ? "Adulto" : s.ToString();

    private static string AtributoTexto(DigimonAttribute a)
    {
        switch (a)
        {
            case DigimonAttribute.Fire: return "Fogo";
            case DigimonAttribute.Water: return "Água";
            case DigimonAttribute.Plant: return "Planta";
            case DigimonAttribute.Electric: return "Elétrico";
            case DigimonAttribute.Earth: return "Terra";
            case DigimonAttribute.Wind: return "Vento";
            case DigimonAttribute.Light: return "Luz";
            case DigimonAttribute.Dark: return "Trevas";
            default: return "Neutro";
        }
    }

    // ------------------------------------------------------------ Montagem da interface

    private void Montar()
    {
        var canvasGo = new GameObject("UI_Digivice", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        // Botão que abre o painel (lado esquerdo, abaixo do ícone de clima).
        var abrir = Botao(canvasGo.transform, "Digivice", CorBotao, () => Abrir(!_painel.activeSelf));
        _botaoAbrir = abrir;
        var art = (RectTransform)abrir.transform;
        art.anchorMin = art.anchorMax = new Vector2(0, 1);
        art.pivot = new Vector2(0, 1);
        art.anchoredPosition = new Vector2(30, -260);
        art.sizeDelta = new Vector2(230, 90);

        _painel = new GameObject("Painel", typeof(RectTransform), typeof(Image));
        _painel.transform.SetParent(canvasGo.transform, false);
        var prt = (RectTransform)_painel.transform;
        prt.anchorMin = new Vector2(0.04f, 0.12f);
        prt.anchorMax = new Vector2(0.96f, 0.86f);
        prt.offsetMin = prt.offsetMax = Vector2.zero;
        _painel.GetComponent<Image>().color = CorFundo;

        _cabecalho = Texto(_painel.transform, 38, TextAlignmentOptions.Left);
        Ancorar(_cabecalho.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -150), new Vector2(-130, -20));

        var fechar = Botao(_painel.transform, "X", new Color(0.85f, 0.35f, 0.35f), () => Abrir(false));
        Ancorar((RectTransform)fechar.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-110, -110), new Vector2(-20, -20));

        var nomes = new[] { (Aba.PorPerto, "Por perto"), (Aba.Colecao, "Coleção"), (Aba.Missoes, "Missões") };
        for (var i = 0; i < nomes.Length; i++)
        {
            var (aba, nome) = nomes[i];
            var b = Botao(_painel.transform, nome, CorBotaoInativo, () => TrocarAba(aba));
            var x0 = 0.03f + i * 0.32f;
            Ancorar((RectTransform)b.transform, new Vector2(x0, 1), new Vector2(x0 + 0.30f, 1), new Vector2(0, -260), new Vector2(0, -170));
            _abas[aba] = b.GetComponent<Image>();
        }

        // Lista com rolagem.
        var scroll = new GameObject("Lista", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(Mask));
        scroll.transform.SetParent(_painel.transform, false);
        Ancorar((RectTransform)scroll.transform, Vector2.zero, Vector2.one, new Vector2(20, 20), new Vector2(-20, -280));
        scroll.GetComponent<Image>().color = new Color(0, 0, 0, 0.15f);
        scroll.GetComponent<Mask>().showMaskGraphic = true;
        var conteudo = new GameObject("Conteudo", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        conteudo.transform.SetParent(scroll.transform, false);
        _conteudo = (RectTransform)conteudo.transform;
        _conteudo.anchorMin = new Vector2(0, 1);
        _conteudo.anchorMax = new Vector2(1, 1);
        _conteudo.pivot = new Vector2(0.5f, 1);
        _conteudo.offsetMin = _conteudo.offsetMax = Vector2.zero;
        var layout = conteudo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 12;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        conteudo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = scroll.GetComponent<ScrollRect>();
        sr.content = _conteudo;
        sr.horizontal = false;
        sr.scrollSensitivity = 30;

        _painel.SetActive(false);

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
    }

    private void Linha(string texto, Action aoTocar)
    {
        var go = new GameObject("Linha", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(_conteudo, false);
        go.GetComponent<Image>().color = CorLinha;
        go.GetComponent<LayoutElement>().minHeight = 120;
        if (aoTocar != null) go.AddComponent<Button>().onClick.AddListener(() => aoTocar());
        else go.GetComponent<Image>().raycastTarget = false;

        var t = Texto(go.transform, 38, TextAlignmentOptions.Left);
        t.text = texto;
        Ancorar(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 10), new Vector2(-24, -10));
        // A linha cresce conforme o texto.
        go.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(120, t.GetPreferredValues(texto, 900, 0).y + 30);
    }

    private static GameObject Botao(Transform pai, string rotulo, Color cor, Action aoTocar)
    {
        var go = new GameObject(rotulo, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(pai, false);
        go.GetComponent<Image>().color = cor;
        go.GetComponent<Button>().onClick.AddListener(() => aoTocar());
        var t = Texto(go.transform, 40, TextAlignmentOptions.Center);
        t.text = $"<b>{rotulo}</b>";
        t.color = new Color(0.04f, 0.14f, 0.12f);
        Ancorar(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return go;
    }

    private static TMP_Text Texto(Transform pai, float tamanho, TextAlignmentOptions alinhamento)
    {
        var go = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = tamanho;
        t.color = Color.white;
        t.alignment = alinhamento;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    private static void Ancorar(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }
}
