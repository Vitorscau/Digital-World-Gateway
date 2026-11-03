using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Batalha única do jogo (substitui as três versões antigas):
/// cria o parceiro do jogador e o Digimon selvagem a partir das fichas, faz a contagem,
/// deixa os dois lutarem com dano calculado pelos atributos e, no fim, dá XP, bits,
/// captura o Digimon (na vitória) e avança as missões.
///
/// Na Battle_Scene (arena 3D, sem AR — o padrão do jogo) ele se monta sozinho ao abrir a cena, com a HUD
/// (HudBatalha), a câmera atrás do parceiro e um botão "AR" para levar esta batalha para o AR antes de começar.
/// No AR, a arena chama IniciarEm(...).
/// </summary>
public class BatalhaDigimon : MonoBehaviour
{
    private Transform _pontoJogador, _pontoInimigo, _pai;
    private float _altura = 1f;
    private Encontro _encontro;
    private CriaturaArena _jogador, _inimigo;
    private HPBar _barraJogador, _barraInimigo;
    private BattleHud _hudJogador, _hudInimigo;
    private bool _terminou;
    private bool _semAR;          // arena 3D comum (Battle_Scene)
    private bool _lutaComecou;
    private GameObject _botaoAR;

    private Canvas _canvas;
    private TMP_Text _textoGrande;
    private GameObject _painelResultado;

    // ------------------------------------------------------------ Montagem automática

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Registrar()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
        AoCarregarCena(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (cena.name != Cenas.Batalha || FindFirstObjectByType<BatalhaDigimon>() != null) return;
        var spawnJogador = GameObject.Find("SpawnPlayer");
        var spawnInimigo = GameObject.Find("SpawnEnemy");
        if (spawnJogador == null || spawnInimigo == null)
        {
            Debug.LogWarning("[Batalha] SpawnPlayer/SpawnEnemy não encontrados na Battle_Scene.");
            return;
        }

        // Os Digimons de exemplo que já estão na cena servem de referência de tamanho e saem de cena.
        var altura = 0f;
        var n = 0;
        foreach (var c in FindObjectsByType<CriaturaArena>(FindObjectsSortMode.None))
        {
            var h = DigimonVisual.Altura(c.gameObject);
            if (h > 0) { altura += h; n++; }
            c.gameObject.SetActive(false);
        }
        altura = n > 0 ? altura / n : 1.5f;

        IniciarEm(spawnJogador.transform, spawnInimigo.transform, altura, null)._semAR = true;
    }

    /// <summary>Começa a batalha usando estes pontos (usado pela arena do AR e pela Battle_Scene).</summary>
    public static BatalhaDigimon IniciarEm(Transform pontoJogador, Transform pontoInimigo, float altura, Transform pai)
    {
        var go = new GameObject("BatalhaDigimon");
        var b = go.AddComponent<BatalhaDigimon>();
        b._pontoJogador = pontoJogador;
        b._pontoInimigo = pontoInimigo;
        b._altura = altura;
        b._pai = pai;
        return b;
    }

    // ------------------------------------------------------------ Batalha

    private IEnumerator Start()
    {
        _encontro = Encontro.Atual ?? Encontro.Teste();
        if (_encontro.FichaParceiro == null || _encontro.FichaInimigo == null)
        {
            Debug.LogError("[Batalha] Falta a ficha do parceiro ou do inimigo em Resources/Digimons.");
            yield break;
        }

        MontarUI();
        if (_semAR) DesligarSistemaAntigo();
        else AcharHuds();

        var parceiro = new Digimon(_encontro.FichaParceiro, Mathf.Max(1, _encontro.Parceiro?.nivel ?? 5)).AplicarParceiro(_encontro.Parceiro);
        var selvagem = new Digimon(_encontro.FichaInimigo, Mathf.Max(1, _encontro.NivelInimigo));

        _jogador = DigimonVisual.Criar(_encontro.FichaParceiro, null, _pontoJogador, _altura, _pai);
        _inimigo = DigimonVisual.Criar(_encontro.FichaInimigo, _encontro.ModeloInimigo, _pontoInimigo, _altura, _pai);
        if (_jogador == null || _inimigo == null)
        {
            Debug.LogError("[Batalha] Não consegui criar os modelos dos Digimons.");
            yield break;
        }

        // Um olhando para o outro.
        _jogador.transform.LookAt(new Vector3(_inimigo.transform.position.x, _jogador.transform.position.y, _inimigo.transform.position.z));
        _inimigo.transform.LookAt(new Vector3(_jogador.transform.position.x, _inimigo.transform.position.y, _jogador.transform.position.z));

        if (_hudJogador != null) _hudJogador.SetData(parceiro);
        if (_hudInimigo != null) _hudInimigo.SetData(selvagem);
        _jogador.Configurar(parceiro, _inimigo, _barraJogador);
        _jogador.doJogador = true;
        _inimigo.Configurar(selvagem, _jogador, _barraInimigo);
        if (PerfilJogador.Atual.bandagemAtiva) _jogador.AumentarVida(1.3f); // Bandage usada na Bag
        _jogador.Derrotado += _ => Terminar(false);
        _inimigo.Derrotado += _ => Terminar(true);

        // Sem HUD na cena (arena 3D): monta a do Figma, com HP e MP.
        if (_hudJogador == null && _hudInimigo == null) HudBatalha.Criar(_canvas.transform, _jogador, _inimigo);
        if (_semAR)
        {
            EnquadrarCamera();
            MontarBotaoAR();
        }

        // Contagem regressiva (dá tempo de trocar para o AR).
        foreach (var t in new[] { "3", "2", "1", "FIGHT!" })
        {
            _textoGrande.text = t;
            yield return new WaitForSeconds(t == "FIGHT!" ? 0.6f : 0.8f);
        }
        _textoGrande.text = "";
        _lutaComecou = true;
        if (_botaoAR != null) _botaoAR.SetActive(false); // depois de começar não troca (não dá para fugir de uma luta perdida)
        _jogador.podeLutar = true;
        _inimigo.podeLutar = true;

        // Trava a arena do AR (não deixa mais mover/escalar).
        var arena = FindFirstObjectByType<NavMeshHowTo>();
        if (arena != null) arena.TravarArena();
    }

    private void AcharHuds()
    {
        // Usa a HUD da cena (BattleSystem.playerHud/enemyHud), se existir.
        var sistema = FindFirstObjectByType<BattleSystem>();
        if (sistema == null) return;
        _hudJogador = Campo<BattleHud>(sistema, "playerHud");
        _hudInimigo = Campo<BattleHud>(sistema, "enemyHud");
        _barraJogador = _hudJogador != null ? Campo<HPBar>(_hudJogador, "hpBar") : null;
        _barraInimigo = _hudInimigo != null ? Campo<HPBar>(_hudInimigo, "hpBar") : null;
    }

    /// <summary>
    /// Na arena 3D, a Battle_Scene ainda tem o BattleSystem antigo (por turnos), com HUD e botões "Battle"/"Back"
    /// preenchidos com dados de teste: desliga tudo isso e usa só a HudBatalha.
    /// </summary>
    private void DesligarSistemaAntigo()
    {
        foreach (var sistema in FindObjectsByType<BattleSystem>(FindObjectsSortMode.None))
        {
            sistema.enabled = false;
            foreach (var c in sistema.GetComponentsInChildren<Canvas>(true))
                if (c.isRootCanvas) c.gameObject.SetActive(false);
        }
        foreach (var hud in FindObjectsByType<BattleHud>(FindObjectsSortMode.None))
        {
            var raiz = hud.GetComponentInParent<Canvas>()?.rootCanvas;
            if (raiz != null && raiz != _canvas) raiz.gameObject.SetActive(false);
        }
    }

    private static T Campo<T>(object alvo, string nome) where T : class =>
        alvo.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(alvo) as T;

    private void Terminar(bool venceu)
    {
        if (_terminou) return;
        _terminou = true;
        if (_jogador != null) _jogador.podeLutar = false;
        if (_inimigo != null) _inimigo.podeLutar = false;

        StartCoroutine(MostrarResultado(ResultadoBatalha.Aplicar(_encontro, venceu).Texto));
    }

    private IEnumerator MostrarResultado(string texto)
    {
        yield return new WaitForSeconds(1.2f);
        _painelResultado.SetActive(true);
        _painelResultado.GetComponentInChildren<TMP_Text>().text = texto;
    }

    /// <summary>Câmera atrás e um pouco acima do parceiro, olhando para o meio da luta (o inimigo de frente).</summary>
    private void EnquadrarCamera()
    {
        var cam = Camera.main;
        if (cam == null || _jogador == null || _inimigo == null) return;
        CameraArena.Seguir(cam, _jogador.transform, _inimigo.transform, _altura);
    }

    /// <summary>Botão "AR": leva esta mesma batalha para o AR (só antes de a luta começar).</summary>
    private void MontarBotaoAR()
    {
        var b = UIPadrao.Botao(_canvas.transform, "AR", () =>
        {
            if (_lutaComecou) return;
            TelaCarregamento.Ir(Cenas.BatalhaAR);
        }, false, 44f);
        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40f, -110f);
        rt.sizeDelta = new Vector2(220f, 110f);
        _botaoAR = b.gameObject;
    }

    public void VoltarAoMapa()
    {
        Encontro.Limpar();
        TelaCarregamento.Ir(Cenas.Mapa);
    }

    // ------------------------------------------------------------ Interface (criada por código)

    private void MontarUI()
    {
        var go = new GameObject("UI_Batalha", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        UIResponsiva.ConfigurarEscala(go.GetComponent<CanvasScaler>(), new Vector2(1080, 1920));

        _textoGrande = Texto(go.transform, "Contagem", 220, Color.white);
        Esticar((RectTransform)_textoGrande.transform, Vector2.zero, Vector2.one);

        // Painel no padrão da arena do AR: verde-água, borda branca, Lato e o botão OK original.
        var estilo = EstiloUI.Atual;
        var prt = UIPadrao.Imagem(go.transform, "Resultado", null, estilo.VerdeBatalha, false);
        prt.anchorMin = new Vector2(0.06f, 0.28f);
        prt.anchorMax = new Vector2(0.94f, 0.72f);
        prt.offsetMin = prt.offsetMax = Vector2.zero;
        if (estilo.Moldura != null)
        {
            var moldura = UIPadrao.Imagem(prt, "Moldura", estilo.Moldura, Color.white, true);
            UIPadrao.Esticar(moldura);
            moldura.GetComponent<Image>().raycastTarget = false;
        }
        _painelResultado = prt.gameObject;

        var corpo = Texto(_painelResultado.transform, "Texto", 44, Color.white);
        var crt = (RectTransform)corpo.transform;
        crt.anchorMin = new Vector2(0, 0.28f);
        crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(50, 0);
        crt.offsetMax = new Vector2(-50, -50);

        var brt = UIPadrao.Imagem(_painelResultado.transform, "OK", estilo.BotaoOkBatalha, Color.white, false);
        brt.anchorMin = new Vector2(0.27f, 0.06f);
        brt.anchorMax = new Vector2(0.73f, 0.22f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        brt.GetComponent<Image>().preserveAspect = true;
        brt.gameObject.AddComponent<Button>().onClick.AddListener(VoltarAoMapa);
        if (estilo.BotaoOkBatalha == null)
        {
            var rotulo = Texto(brt, "Rotulo", 48, Color.white);
            rotulo.text = "OK";
            Esticar((RectTransform)rotulo.transform, Vector2.zero, Vector2.one);
        }

        _painelResultado.SetActive(false);

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
    }

    private static TMP_Text Texto(Transform pai, string nome, float tamanho, Color cor)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (EstiloUI.Atual.FonteNegrito != null) t.font = EstiloUI.Atual.FonteNegrito; // Lato, como no resto do jogo
        t.fontSize = tamanho;
        t.color = cor;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    private static void Esticar(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
