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
/// Na Battle_Scene ele se monta sozinho ao abrir a cena. No AR, a arena chama IniciarEm(...).
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

        IniciarEm(spawnJogador.transform, spawnInimigo.transform, altura, null);
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
        AcharHuds();

        var parceiro = new Digimon(_encontro.FichaParceiro, Mathf.Max(1, _encontro.Parceiro?.nivel ?? 5));
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
        _inimigo.Configurar(selvagem, _jogador, _barraInimigo);
        _jogador.Derrotado += _ => Terminar(false);
        _inimigo.Derrotado += _ => Terminar(true);
        _jogador.RecebeuDano += (c, d) => MostrarDano(c, d);
        _inimigo.RecebeuDano += (c, d) => MostrarDano(c, d);

        // Contagem regressiva.
        foreach (var t in new[] { "3", "2", "1", "LUTE!" })
        {
            _textoGrande.text = t;
            yield return new WaitForSeconds(t == "LUTE!" ? 0.6f : 0.8f);
        }
        _textoGrande.text = "";
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

    private static T Campo<T>(object alvo, string nome) where T : class =>
        alvo.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(alvo) as T;

    private void Terminar(bool venceu)
    {
        if (_terminou) return;
        _terminou = true;
        if (_jogador != null) _jogador.podeLutar = false;
        if (_inimigo != null) _inimigo.podeLutar = false;

        var perfil = PerfilJogador.Atual;
        var nivelInimigo = Mathf.Max(1, _encontro.NivelInimigo);
        var parceiro = _encontro.Parceiro;
        var nomeParceiro = _encontro.FichaParceiro.Name.Trim();
        var nomeInimigo = _encontro.FichaInimigo.Name.Trim();
        string texto;

        if (venceu)
        {
            var xpDigimon = 10 + nivelInimigo * 6;
            var xpJogador = 50 + nivelInimigo * 10;
            var bits = 10 + nivelInimigo * 2;
            var subiuDigimon = PerfilJogador.GanharXp(parceiro, xpDigimon);
            var subiuJogador = perfil.GanharXp(xpJogador);
            perfil.bits += bits;
            perfil.vitorias++;
            perfil.AdicionarNaColecao(_encontro.EspecieInimigo, nivelInimigo);
            perfil.Salvar();
            Missoes.RegistrarVitoria(_encontro.FichaInimigo);
            Missoes.RegistrarCaptura();

            texto = $"<size=90><b>VITÓRIA!</b></size>\n\n" +
                    $"{nomeInimigo} (nv. {nivelInimigo}) entrou para a sua coleção!\n\n" +
                    $"{nomeParceiro}: +{xpDigimon} XP" + (subiuDigimon > 0 ? $"  <color=#FFE066>subiu para o nível {parceiro.nivel}!</color>" : "") + "\n" +
                    $"Você: +{xpJogador} XP" + (subiuJogador > 0 ? $"  <color=#FFE066>nível {perfil.nivel}!</color>" : "") + "\n" +
                    $"+{bits} bits";
        }
        else
        {
            var xpDigimon = 5 + nivelInimigo;
            PerfilJogador.GanharXp(parceiro, xpDigimon);
            perfil.derrotas++;
            perfil.Salvar();
            texto = $"<size=90><b>DERROTA</b></size>\n\n{nomeInimigo} fugiu.\n\n{nomeParceiro}: +{xpDigimon} XP";
        }

        StartCoroutine(MostrarResultado(texto));
    }

    private IEnumerator MostrarResultado(string texto)
    {
        yield return new WaitForSeconds(1.2f);
        _painelResultado.SetActive(true);
        _painelResultado.GetComponentInChildren<TMP_Text>().text = texto;
    }

    public void VoltarAoMapa()
    {
        Encontro.Limpar();
        SceneManager.LoadScene(Cenas.Mapa);
    }

    private void MostrarDano(CriaturaArena alvo, int dano)
    {
        if (alvo == null) return;
        StartCoroutine(NumeroDeDano(alvo.transform.position + Vector3.up * _altura * 1.1f, dano, alvo == _inimigo));
    }

    private IEnumerator NumeroDeDano(Vector3 posicao, int dano, bool noInimigo)
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        var go = new GameObject("Dano", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(_canvas.transform, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = dano.ToString();
        t.fontSize = 64;
        t.fontStyle = FontStyles.Bold;
        t.color = noInimigo ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.35f, 0.35f);
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(300, 100);
        for (float k = 0; k < 1f; k += Time.deltaTime * 1.2f)
        {
            var tela = cam.WorldToScreenPoint(posicao);
            rt.position = tela + new Vector3(0, k * 120f, 0);
            t.alpha = 1f - k;
            yield return null;
        }
        Destroy(go);
    }

    // ------------------------------------------------------------ Interface (criada por código)

    private void MontarUI()
    {
        var go = new GameObject("UI_Batalha", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        _textoGrande = Texto(go.transform, "Contagem", 220, Color.white);
        Esticar((RectTransform)_textoGrande.transform, Vector2.zero, Vector2.one);

        _painelResultado = new GameObject("Resultado", typeof(RectTransform), typeof(Image));
        _painelResultado.transform.SetParent(go.transform, false);
        var prt = (RectTransform)_painelResultado.transform;
        prt.anchorMin = new Vector2(0.08f, 0.25f);
        prt.anchorMax = new Vector2(0.92f, 0.75f);
        prt.offsetMin = prt.offsetMax = Vector2.zero;
        _painelResultado.GetComponent<Image>().color = new Color(0.07f, 0.18f, 0.22f, 0.94f);

        var corpo = Texto(_painelResultado.transform, "Texto", 44, Color.white);
        var crt = (RectTransform)corpo.transform;
        crt.anchorMin = new Vector2(0, 0.25f);
        crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(40, 0);
        crt.offsetMax = new Vector2(-40, -40);

        var botao = new GameObject("Voltar", typeof(RectTransform), typeof(Image), typeof(Button));
        botao.transform.SetParent(_painelResultado.transform, false);
        var brt = (RectTransform)botao.transform;
        brt.anchorMin = new Vector2(0.2f, 0.06f);
        brt.anchorMax = new Vector2(0.8f, 0.2f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        botao.GetComponent<Image>().color = new Color(0.35f, 0.82f, 0.6f);
        botao.GetComponent<Button>().onClick.AddListener(VoltarAoMapa);
        var rotulo = Texto(botao.transform, "Rotulo", 48, new Color(0.05f, 0.15f, 0.12f));
        rotulo.text = "Voltar ao mapa";
        Esticar((RectTransform)rotulo.transform, Vector2.zero, Vector2.one);

        _painelResultado.SetActive(false);

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
    }

    private static TMP_Text Texto(Transform pai, string nome, float tamanho, Color cor)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);
        var t = go.GetComponent<TextMeshProUGUI>();
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
