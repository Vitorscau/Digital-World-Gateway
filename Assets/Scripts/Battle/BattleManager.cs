using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

/// <summary>
/// Controla a batalha da arena em AR (prefab Arena_agent): botão "Batalhar", contagem,
/// telas de vitória/derrota e popup de recompensa.
///
/// Os dois Digimons que vêm no prefab (Gabumon e Agumon Black) servem de molde: ao abrir,
/// são trocados pelo parceiro do jogador e pelo Digimon selvagem tocado no mapa, com a
/// vida e a força vindas das fichas. No fim, aplica XP, bits, captura e missões.
/// </summary>
public class BattleManager : MonoBehaviour
{
    [Header("Referências UI")]
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private GameObject defeatScreen;
    [SerializeField] private Button battleButton;

    [SerializeField] private GameObject battleCanvas;

    [Header("Contagem")]
    [SerializeField] private GameObject countdownCanvas;
    [SerializeField] private TMP_Text countdownText;

    [Header("Popup de Recompensa")]
    [SerializeField] private GameObject rewardPopup;
    [SerializeField] private Button okButton;

    [Header("Referências de Criaturas")]
    [SerializeField] private GameObject playerCreatureGO;
    [SerializeField] private GameObject enemyCreatureGO;
    private CriaturaArena playerCreature;
    private CriaturaArena enemyCreature;

    private NavMeshHowTo navMeshArena;
    private bool batalhaIniciada = false;
    private bool batalhaTerminada = false;
    private Encontro encontro;
    private ResultadoBatalha.Resumo resultado;

    private void Start()
    {
        navMeshArena = FindFirstObjectByType<NavMeshHowTo>();

        if (battleButton != null)
        {
            battleButton.onClick.AddListener(() =>
            {
                battleButton.interactable = false; // evita duas contagens ao tocar duas vezes
                StartCoroutine(ContagemRegressiva());
            });
        }

        PrepararDigimons();

        if (playerCreatureGO != null)
        {
            playerCreatureGO.SetActive(false);
            playerCreature = playerCreatureGO.GetComponent<CriaturaArena>();
        }

        if (enemyCreatureGO != null)
        {
            enemyCreatureGO.SetActive(false);
            enemyCreature = enemyCreatureGO.GetComponent<CriaturaArena>();
        }

        if (playerCreature != null) playerCreature.Derrotado += _ => ShowDefeat();
        if (enemyCreature != null) enemyCreature.Derrotado += _ => ShowVictory();

        if (countdownCanvas != null) countdownCanvas.SetActive(false);
        if (rewardPopup != null) rewardPopup.SetActive(false);
        if (okButton != null) okButton.onClick.AddListener(ReturnToMap);
    }

    /// <summary>Troca os Digimons de exemplo do prefab pelos do encontro atual.</summary>
    private void PrepararDigimons()
    {
        if (playerCreatureGO == null || enemyCreatureGO == null) return; // BattleManager sem arena (cena)

        encontro = Encontro.Atual ?? Encontro.Teste();
        if (encontro.FichaParceiro == null || encontro.FichaInimigo == null)
        {
            Debug.LogWarning("[Batalha AR] Falta ficha do parceiro ou do inimigo; usando os Digimons do prefab.");
            encontro = null;
            return;
        }

        var altura = MedirAltura(playerCreatureGO);
        var pai = playerCreatureGO.transform.parent;
        var novoJogador = DigimonVisual.Criar(encontro.FichaParceiro, null, playerCreatureGO.transform, altura, pai);
        var novoInimigo = DigimonVisual.Criar(encontro.FichaInimigo, encontro.ModeloInimigo, enemyCreatureGO.transform, altura, pai);
        if (novoJogador == null || novoInimigo == null)
        {
            if (novoJogador != null) Destroy(novoJogador.gameObject);
            if (novoInimigo != null) Destroy(novoInimigo.gameObject);
            encontro = null;
            return;
        }

        // Um olhando para o outro.
        novoJogador.transform.LookAt(new Vector3(novoInimigo.transform.position.x, novoJogador.transform.position.y, novoInimigo.transform.position.z));
        novoInimigo.transform.LookAt(new Vector3(novoJogador.transform.position.x, novoInimigo.transform.position.y, novoJogador.transform.position.z));

        // Ficha + nível de cada um, ligados às barras de vida da HUD da arena.
        var dadosJogador = new Digimon(encontro.FichaParceiro, Mathf.Max(1, encontro.Parceiro?.nivel ?? 5)).AplicarParceiro(encontro.Parceiro);
        var dadosInimigo = new Digimon(encontro.FichaInimigo, Mathf.Max(1, encontro.NivelInimigo));
        AcharHud("Enemy", out var hudInimigo, out var barraInimigo);
        AcharHud("Player", out var hudJogador, out var barraJogador);
        if (hudJogador != null) hudJogador.SetData(dadosJogador);
        if (hudInimigo != null) hudInimigo.SetData(dadosInimigo);
        novoJogador.Configurar(dadosJogador, novoInimigo, barraJogador);
        novoJogador.doJogador = true;
        novoInimigo.Configurar(dadosInimigo, novoJogador, barraInimigo);
        if (PerfilJogador.Atual.bandagemAtiva) novoJogador.AumentarVida(1.3f); // Bandage usada na Bag

        // Os de exemplo saem de cena.
        Destroy(playerCreatureGO);
        Destroy(enemyCreatureGO);
        playerCreatureGO = novoJogador.gameObject;
        enemyCreatureGO = novoInimigo.gameObject;
    }

    private static float MedirAltura(GameObject molde)
    {
        // Renderers desligados não têm tamanho: liga só para medir.
        var estava = molde.activeSelf;
        molde.SetActive(true);
        var h = DigimonVisual.Altura(molde);
        molde.SetActive(estava);
        return h > 0 ? h : 0.5f;
    }

    private void AcharHud(string lado, out BattleHud hud, out HPBar barra)
    {
        hud = null;
        barra = null;
        foreach (var b in GetComponentsInChildren<HPBar>(true))
        {
            if (!b.name.Contains(lado)) continue;
            barra = b;
            hud = b.GetComponentInParent<BattleHud>(true);
            return;
        }
    }

    private void Update()
    {
        // Segurança: se algum Digimon sumir sem o evento (ex.: destruído por outro script).
        if (!batalhaIniciada || batalhaTerminada) return;
        if (playerCreature == null) ShowDefeat();
        else if (enemyCreature == null) ShowVictory();
    }

    private IEnumerator ContagemRegressiva()
    {
        if (countdownCanvas != null) countdownCanvas.SetActive(true);

        string[] mensagens = { "3", "2", "1", "FIGHT!" };
        foreach (string msg in mensagens)
        {
            if (countdownText != null) countdownText.text = msg;
            yield return new WaitForSeconds(1f);
        }

        if (countdownCanvas != null) countdownCanvas.SetActive(false);

        if (playerCreatureGO != null)
        {
            playerCreatureGO.SetActive(true);
            var c = playerCreatureGO.GetComponent<CriaturaArena>();
            if (c != null) c.podeLutar = true;
        }

        if (enemyCreatureGO != null)
        {
            enemyCreatureGO.SetActive(true);
            var c = enemyCreatureGO.GetComponent<CriaturaArena>();
            if (c != null) c.podeLutar = true;
        }

        batalhaIniciada = true;
        if (navMeshArena != null) navMeshArena.TravarArena();
    }

    private void ShowVictory()
    {
        if (batalhaTerminada) return;
        batalhaTerminada = true;
        if (victoryScreen != null) victoryScreen.SetActive(true);
        if (encontro != null) resultado = ResultadoBatalha.Aplicar(encontro, true);
        ShowRewardPopup();
    }

    private void ShowDefeat()
    {
        if (batalhaTerminada) return;
        batalhaTerminada = true;
        if (defeatScreen != null) defeatScreen.SetActive(true);
        if (encontro != null) resultado = ResultadoBatalha.Aplicar(encontro, false);
        ShowRewardPopup();
    }

    private void ShowRewardPopup()
    {
        if (playerCreature != null) playerCreature.podeLutar = false;
        if (enemyCreature != null) enemyCreature.podeLutar = false;
        if (battleCanvas != null) battleCanvas.SetActive(false);
        Invoke(nameof(Popup), 1f);
    }

    private void Popup()
    {
        if (rewardPopup == null) return;
        rewardPopup.SetActive(true);
        if (resultado != null) EscreverRecompensa(resultado);
    }

    // Cor de fundo do painel de recompensa (imagem "Group 11"), usada para cobrir os valores desenhados.
    private static readonly Color FundoPainel = new Color32(0x00, 0xCF, 0xCC, 0xFF);

    /// <summary>
    /// O painel de recompensa é uma imagem com "10 EXP / 10 EXP / 10 BIT" e dois itens desenhados.
    /// Cobre esses valores com a cor do fundo e escreve os valores reais no mesmo lugar.
    /// Posições em fração da imagem (0 = embaixo/esquerda, 1 = em cima/direita).
    /// </summary>
    private void EscreverRecompensa(ResultadoBatalha.Resumo r)
    {
        var painel = rewardPopup.transform.Find("Image") as RectTransform;
        if (painel == null || painel.Find("ValoresReais") != null) return;
        var raiz = new GameObject("ValoresReais", typeof(RectTransform)).GetComponent<RectTransform>();
        raiz.SetParent(painel, false);
        raiz.anchorMin = Vector2.zero;
        raiz.anchorMax = Vector2.one;
        raiz.offsetMin = raiz.offsetMax = Vector2.zero;

        Valor(raiz, 0.851f, $"{r.XpJogador} EXP");   // Acquired Tamer EXP
        Valor(raiz, 0.751f, $"{r.XpDigimon} EXP");   // Acquired Digimon EXP
        Valor(raiz, 0.646f, $"{r.Bits} BIT");        // Acquired Money

        // Itens: a imagem já mostra Bandage 1x e Meat 1x, que é o que a vitória dá.
        // Na derrota não há itens: cobre os desenhados.
        if (r.Itens.Count == 0)
        {
            Remendo(raiz, new Vector2(0.09f, 0.335f), new Vector2(0.64f, 0.48f));
            Rotulo(raiz, new Vector2(0.12f, 0.36f), new Vector2(0.64f, 0.46f), "No items", TextAlignmentOptions.Left, 0.85f);
        }

        // Dados de Scan coletados (e subida de nível do parceiro).
        var linha = r.LinhaScan;
        if (r.NivelParceiroNovo > 0) linha += $"\n{r.NomeParceiro} reached level {r.NivelParceiroNovo}!";
        Rotulo(raiz, new Vector2(0.08f, 0.21f), new Vector2(0.92f, 0.33f), linha, TextAlignmentOptions.Center, 1f);
    }

    private static void Valor(RectTransform raiz, float centroY, string texto)
    {
        Remendo(raiz, new Vector2(0.66f, centroY - 0.034f), new Vector2(0.93f, centroY + 0.034f));
        Rotulo(raiz, new Vector2(0.62f, centroY - 0.032f), new Vector2(0.885f, centroY + 0.032f), texto, TextAlignmentOptions.Right, 1f);
    }

    private static void Remendo(RectTransform raiz, Vector2 min, Vector2 max)
    {
        var go = new GameObject("Remendo", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(raiz, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = FundoPainel;
        img.raycastTarget = false;
    }

    private static void Rotulo(RectTransform raiz, Vector2 min, Vector2 max, string texto, TextAlignmentOptions alinhamento, float escalaFonte)
    {
        var go = new GameObject("Valor", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(raiz, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.GetComponent<TextMeshProUGUI>();
        // Mesma fonte dos rótulos desenhados ("Acquired Tamer EXP"...): Lato.
        if (EstiloUI.Atual.FonteNegrito != null) t.font = EstiloUI.Atual.FonteNegrito;
        else t.fontStyle = FontStyles.Bold;
        t.text = texto;
        t.alignment = alinhamento;
        t.color = Color.white;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.5f;
        t.fontSizeMax = 4.2f * escalaFonte; // o painel tem 100 unidades de altura
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
    }

    public void ReturnToMap()
    {
        Encontro.Limpar();
        TelaCarregamento.Ir(Cenas.Mapa);
    }
}
