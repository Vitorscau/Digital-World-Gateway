using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.Maps;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Painel "Digivice" no mapa (botão com o ícone do Digivice), com três abas:
/// - Por perto: Digimons próximos, distância e tempo até sumir (tocar = batalhar).
/// - Coleção: Digimons capturados (tocar = escolher como parceiro).
/// - Missões: missões diárias e progresso.
/// No topo mostra nível, XP e bits do Tamer. Visual no padrão das telas do jogo (EstiloUI).
/// Montado por código ao abrir a Map_Scene.
/// </summary>
public class PainelDigivice : MonoBehaviour
{
    private enum Aba { PorPerto, Colecao, Missoes }

    private RectTransform _painel;
    private GameObject _botaoAbrir;
    private TMP_Text _titulo, _cabecalho;
    private RectTransform _conteudo;
    private readonly Dictionary<Aba, Button> _abas = new Dictionary<Aba, Button>();
    private Aba _aba = Aba.PorPerto;
    private float _proximaAtualizacao;

    private static EstiloUI E => EstiloUI.Atual;

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
        if (_painel != null && _painel.gameObject.activeSelf) Atualizar();
    }

    private GameObject _hud;

    private void Update()
    {
        // O botão só aparece junto com a HUD do jogo (some na tela de aviso inicial, por exemplo).
        if (_hud == null)
        {
            var telas = FindFirstObjectByType<UIScreenManager>();
            if (telas != null) _hud = telas.HUD;
        }
        if (!_painel.gameObject.activeSelf)
        {
            var mostrar = _hud == null || _hud.activeInHierarchy;
            if (_botaoAbrir.activeSelf != mostrar) _botaoAbrir.SetActive(mostrar);
        }

        // "Por perto" muda o tempo todo (distâncias, tempo restante).
        if (_painel.gameObject.activeSelf && _aba == Aba.PorPerto && Time.unscaledTime >= _proximaAtualizacao)
            Atualizar();
    }

    // ------------------------------------------------------------ Conteúdo

    private void Abrir(bool abrir)
    {
        _painel.gameObject.SetActive(abrir);
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
        _cabecalho.text = $"<b>{p.nome}</b>   Lv.{p.nivel}   <size=80%>EXP {p.xp}/{PerfilJogador.XpParaProximoNivel(p.nivel)}   ·   {p.bits} BIT</size>\n" +
                          $"<size=70%>Vitórias {p.vitorias} · Derrotas {p.derrotas} · {p.metrosAndados / 1000:0.0} km</size>";
        foreach (var kv in _abas) UIPadrao.Selecionar(kv.Value, kv.Key == _aba);

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
        if (spawner == null || mapa == null) { Cartao("Mapa ainda carregando...", null, null); return; }

        var lista = spawner.Visiveis
            .Select(w => (w, d: LightshipMapView.DistanceMeters(mapa.MapCenter, w.Local)))
            .OrderBy(x => x.d).ToList();
        if (lista.Count == 0) { Cartao("Nenhum Digimon por perto", "Ande um pouco para encontrar mais.", null); return; }

        foreach (var (w, d) in lista)
        {
            var resta = w.TempoRestante;
            var titulo = $"{w.NomeExibicao}  <size=80%>Lv.{w.Nivel}</size>";
            var detalhe = $"{d:0} m  ·  {HabitatTexto(w.Habitat)}  ·  some em {Math.Max(0, (int)resta.TotalMinutes)}:{Math.Max(0, resta.Seconds):00}";
            var alvo = w;
            Cartao(titulo, detalhe, () => { Abrir(false); alvo.TentarEncontro(); }, "Batalhar");
        }
    }

    private void ListarColecao(PerfilJogador p)
    {
        foreach (var d in p.colecao.OrderByDescending(x => x.id == p.parceiroId).ThenByDescending(x => x.nivel))
        {
            var ficha = d.Ficha;
            var nome = ficha != null ? ficha.Name.Trim() : d.especie;
            var parceiro = d.id == p.parceiroId;
            var titulo = $"{nome}  <size=80%>Lv.{d.nivel}</size>" + (parceiro ? $"  <color=#{ColorUtility.ToHtmlStringRGB(E.Destaque)}><size=70%>PARCEIRO</size></color>" : "");
            var info = ficha != null ? $"{EstagioTexto(ficha.Stage)} · {AtributoTexto(ficha.Atribute)} · {ficha.Type}" : "";
            var detalhe = $"{info}  ·  EXP {d.xp}/{PerfilJogador.XpDigimonParaProximoNivel(d.nivel)}";
            var alvo = d;
            Cartao(titulo, detalhe, parceiro ? (Action)null : () => EscolherParceiro(alvo), parceiro ? null : "Escolher");
        }
    }

    private void ListarMissoes()
    {
        foreach (var (def, prog) in Missoes.DeHoje())
        {
            var feita = prog.resgatada;
            var titulo = def.Titulo + (feita ? $"  <color=#{ColorUtility.ToHtmlStringRGB(E.Destaque)}><size=70%>CONCLUÍDA</size></color>" : "");
            var detalhe = $"{prog.progresso}/{def.Meta}   ·   EXP {def.RecompensaXp}   ·   {def.RecompensaBits} BIT";
            Cartao(titulo, detalhe, null);
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
        var canvas = UIPadrao.Canvas(transform, "UI_Digivice", 50);

        // Botão com o ícone do Digivice (mesmo desenho dos botões da HUD), abaixo do ícone de clima.
        var abrirRt = UIPadrao.Imagem(canvas.transform, "Digivice", E.IconeDigivice, Color.white, false);
        abrirRt.anchorMin = abrirRt.anchorMax = new Vector2(0, 1);
        abrirRt.pivot = new Vector2(0, 1);
        abrirRt.anchoredPosition = new Vector2(36, -250);
        abrirRt.sizeDelta = new Vector2(140, 140);
        abrirRt.gameObject.AddComponent<Button>().onClick.AddListener(() => Abrir(true));
        if (E.IconeDigivice == null)
        {
            var t = UIPadrao.Texto(abrirRt, "Digivice", 30, true, TextAlignmentOptions.Center);
            UIPadrao.Esticar(t.rectTransform);
        }
        _botaoAbrir = abrirRt.gameObject;

        // Painel (como a tela de Missões).
        _painel = UIPadrao.Painel(canvas.transform, "Painel");
        // Termina acima do banner de missão da HUD (parte de baixo da tela).
        UIPadrao.Ancorar(_painel, new Vector2(0.04f, 0.125f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);

        _titulo = UIPadrao.Texto(_painel, "Digivice", 46, true, TextAlignmentOptions.Left);
        UIPadrao.Ancorar(_titulo.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(44, -100), new Vector2(-150, -30));

        var fechar = UIPadrao.Botao(_painel, "X", () => Abrir(false), false, 44);
        UIPadrao.Ancorar((RectTransform)fechar.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-128, -118), new Vector2(-36, -30));

        var abas = new[] { (Aba.PorPerto, "Por perto"), (Aba.Colecao, "Coleção"), (Aba.Missoes, "Missões") };
        for (var i = 0; i < abas.Length; i++)
        {
            var (aba, nome) = abas[i];
            var b = UIPadrao.Botao(_painel, nome, () => TrocarAba(aba), false, 38);
            var x0 = 0.04f + i * 0.31f;
            UIPadrao.Ancorar((RectTransform)b.transform, new Vector2(x0, 1), new Vector2(x0 + 0.29f, 1), new Vector2(0, -220), new Vector2(0, -130));
            _abas[aba] = b;
        }

        // Cartão do Tamer.
        var tamer = UIPadrao.Cartao(_painel, "Tamer");
        UIPadrao.Ancorar(tamer, new Vector2(0, 1), new Vector2(1, 1), new Vector2(36, -360), new Vector2(-36, -240));
        _cabecalho = UIPadrao.Texto(tamer, "", 38, true, TextAlignmentOptions.Left);
        UIPadrao.Esticar(_cabecalho.rectTransform, 28, 10);

        // Lista com rolagem.
        var scroll = new GameObject("Lista", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        scroll.transform.SetParent(_painel, false);
        UIPadrao.Ancorar((RectTransform)scroll.transform, Vector2.zero, Vector2.one, new Vector2(30, 36), new Vector2(-30, -380));
        scroll.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        var conteudo = new GameObject("Conteudo", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        conteudo.transform.SetParent(scroll.transform, false);
        _conteudo = (RectTransform)conteudo.transform;
        _conteudo.anchorMin = new Vector2(0, 1);
        _conteudo.anchorMax = new Vector2(1, 1);
        _conteudo.pivot = new Vector2(0.5f, 1);
        _conteudo.offsetMin = _conteudo.offsetMax = Vector2.zero;
        var layout = conteudo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 16;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        conteudo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = scroll.GetComponent<ScrollRect>();
        sr.content = _conteudo;
        sr.horizontal = false;
        sr.scrollSensitivity = 30;

        _painel.gameObject.SetActive(false);
    }

    /// <summary>Cartão no padrão do cartão de missão: título em negrito, detalhe embaixo e botão opcional.</summary>
    private void Cartao(string titulo, string detalhe, Action aoTocar, string rotuloBotao = null)
    {
        var rt = UIPadrao.Cartao(_conteudo, "Cartao");
        rt.gameObject.AddComponent<LayoutElement>().minHeight = 150;
        var margemDireita = aoTocar != null && rotuloBotao != null ? -250f : -28f;

        var t = UIPadrao.Texto(rt, titulo, 40, true, TextAlignmentOptions.TopLeft);
        UIPadrao.Ancorar(t.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(30, 0), new Vector2(margemDireita, -18));
        if (!string.IsNullOrEmpty(detalhe))
        {
            var d = UIPadrao.Texto(rt, detalhe, 30, false, TextAlignmentOptions.BottomLeft);
            UIPadrao.Ancorar(d.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(30, 18), new Vector2(margemDireita, 0));
        }

        if (aoTocar != null && rotuloBotao != null)
        {
            var b = UIPadrao.Botao(rt, rotuloBotao, aoTocar, false, 32);
            UIPadrao.Ancorar((RectTransform)b.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-230, -42), new Vector2(-24, 42));
        }
    }
}
