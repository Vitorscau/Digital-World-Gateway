using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Troca de cena sem travar: a cena nova é carregada em segundo plano (LoadSceneAsync) atrás de uma tela
/// com o logo, dicas e uma barra fina de progresso. A tela só sai depois que a cena nova já rodou os
/// primeiros quadros (Awake/Start pesados), então o "engasgo" fica escondido.
/// Use sempre <see cref="Ir"/> em vez de SceneManager.LoadScene.
/// </summary>
public class TelaCarregamento : MonoBehaviour
{
    private const float TempoMinimo = 0.8f;   // para dar tempo de ler a dica
    private const float TempoPorDica = 4f;

    private static readonly string[] Dicas =
    {
        "Decode Encrypted Data near you to earn items and EXP.",
        "Every 100 m you walk, you and your partner earn EXP.",
        "Digimon near water and in parks are different from those in the city.",
        "Feed, train and care for your partner to help it digivolve.",
        "Daily missions reset at midnight. Check the Daily tab!",
        "Collect 100% of a Digimon's data with Scan to download it.",
        "Use a Bandage before a battle to start with more HP.",
        "Tap your partner's face on the HUD to take care of it.",
        "Follow the story in the History tab to unlock new chapters.",
        "Stay aware of your surroundings while playing. Watch out for traffic!",
    };

    private static TelaCarregamento _instancia;

    private CanvasGroup _grupo;
    private RectTransform _barraFundo, _barraCheia;
    private TMP_Text _dica;
    private RectTransform _logo;
    private int _indiceDica;

    private string _pedida;
    private AsyncOperation _opPronta;
    private bool _rodando, _aceitaNova;

    /// <summary>true enquanto uma cena está sendo carregada (a tela está visível).</summary>
    public static bool Carregando => _instancia != null && _instancia._rodando;

    /// <summary>Vai para a cena mostrando a tela de carregamento. Toques repetidos durante o carregamento são ignorados.</summary>
    public static void Ir(string cena) => Pedir(cena, null);

    /// <summary>Entra numa cena que já foi pré-carregada (allowSceneActivation = false), com a tela por cima.</summary>
    public static void Ativar(AsyncOperation op) => Pedir(null, op);

    private static void Pedir(string cena, AsyncOperation op)
    {
        Garantir();
        var t = _instancia;
        if (t._rodando && !t._aceitaNova) return; // ainda carregando a anterior
        t._pedida = cena;
        t._opPronta = op;
        if (!t._rodando) t.StartCoroutine(t.Rodar());
    }

    private static void Garantir()
    {
        if (_instancia != null) return;
        var go = new GameObject("TelaCarregamento");
        DontDestroyOnLoad(go);
        _instancia = go.AddComponent<TelaCarregamento>();
        _instancia.Montar();
    }

    private void Montar()
    {
        // Abaixo dos avisos (500), acima das telas do jogo (até 200).
        var raiz = FigmaUI.Tela(transform, "Canvas", 450);
        _grupo = raiz.parent.gameObject.AddComponent<CanvasGroup>();
        _grupo.alpha = 0f;
        _grupo.blocksRaycasts = false;

        FigmaUI.Fundo(raiz);

        _logo = FigmaUI.Ret(raiz, "Logo", 64.5f, 560f, 900f, 900f);
        FigmaUI.Imagem(_logo, FigmaUI.S("logo_jogo")).preserveAspect = true;
        _logo.pivot = new Vector2(0.5f, 0.5f);
        _logo.anchoredPosition += new Vector2(450f, -450f);

        // Dica logo acima da barra (estilo Pokémon GO: texto curto, barra fina no pé da tela).
        _dica = FigmaUI.TextoEm(raiz, 50f, 1795f, 929f, 190f, "", 44f, FigmaUI.Peso.Normal);
        _dica.textWrappingMode = TextWrappingModes.Normal;
        _dica.alignment = TextAlignmentOptions.Bottom;

        const float barraW = 880f;
        _barraFundo = FigmaUI.Ret(raiz, "Barra", (FigmaUI.Largura - barraW) / 2f, 2024f, barraW, 16f);
        _barraCheia = BarraFina(_barraFundo);
    }

    // ------------------------------------------------------------ Peças usadas também no menu (LoadingManager)

    /// <summary>Próxima dica, nunca igual à anterior (<paramref name="indice"/> guarda a última mostrada).</summary>
    public static string ProximaDica(ref int indice)
    {
        indice = (indice + 1 + Random.Range(0, Dicas.Length - 1)) % Dicas.Length;
        return Dicas[indice];
    }

    /// <summary>
    /// Transforma <paramref name="trilho"/> (já posicionado, ~10 de altura) numa barra fina de pontas redondas.
    /// Retorna a parte cheia; use <see cref="AtualizarBarraFina"/> para mudar o progresso.
    /// </summary>
    public static RectTransform BarraFina(RectTransform trilho)
    {
        var h = trilho.sizeDelta.y;
        // Cores das barras do Figma (EXP): trilho azul-escuro e parte cheia azul-claro.
        var fundo = FigmaUI.Imagem(trilho, FigmaUI.Arredondado(), FigmaUI.AzulBarraFundo);
        trilho.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -3f);
        fundo.type = Image.Type.Sliced;
        fundo.pixelsPerUnitMultiplier = 11f / (h / 2f); // borda de 11 px = meia altura: pontas redondas
        var cheia = FigmaUI.Local(trilho, "Cheia", 0f, 0f, h, h);
        var img = FigmaUI.Imagem(cheia, FigmaUI.Arredondado(), FigmaUI.AzulBarra);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = fundo.pixelsPerUnitMultiplier;
        return cheia;
    }

    public static void AtualizarBarraFina(RectTransform trilho, RectTransform cheia, float p)
    {
        var h = trilho.sizeDelta.y;
        cheia.sizeDelta = new Vector2(Mathf.Lerp(h, trilho.sizeDelta.x, Mathf.Clamp01(p)), h);
    }

    private IEnumerator Rodar()
    {
        _rodando = true;
        _aceitaNova = false;
        _grupo.blocksRaycasts = true; // bloqueia toques na cena que está saindo
        NovaDica();
        Progresso(0f);
        yield return Fade(1f, 0.15f);

        var mostrado = 0f;
        var tempoDica = 0f;
        while (_pedida != null || _opPronta != null)
        {
            var cena = _pedida;
            var op = _opPronta ?? SceneManager.LoadSceneAsync(cena);
            _pedida = null;
            _opPronta = null;
            _aceitaNova = false;
            if (op == null)
            {
                Debug.LogError($"[TelaCarregamento] Não consegui carregar a cena \"{cena}\" (está no Build Settings?).");
                break;
            }
            op.allowSceneActivation = false;

            // 1) Carrega em segundo plano. O Unity para em 0.9 até a cena poder ser ativada.
            var tempo = 0f;
            while (true)
            {
                var dt = Time.unscaledDeltaTime;
                tempo += dt;
                tempoDica = Animar(tempoDica, dt);
                var real = Mathf.Clamp01(op.progress / 0.9f) * 0.9f;
                mostrado = Mathf.MoveTowards(mostrado, real, dt * 1.2f);
                Progresso(mostrado);
                if (op.progress >= 0.9f && mostrado >= 0.89f && tempo >= TempoMinimo) break;
                yield return null;
            }

            // 2) Ativa a cena nova (o quadro pesado acontece aqui, escondido pela tela).
            _aceitaNova = true; // a cena nova pode pedir outra (ex.: jogo novo vai para o Digitama)
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // 3) Espera os primeiros quadros da cena (Start, montagem da HUD, mapa) e completa a barra.
            for (var i = 0; i < 3; i++) yield return null;
            while (mostrado < 1f)
            {
                var dt = Time.unscaledDeltaTime;
                tempoDica = Animar(tempoDica, dt);
                mostrado = Mathf.MoveTowards(mostrado, 1f, dt * 2.5f);
                Progresso(mostrado);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.15f);
            if (_pedida != null || _opPronta != null) { mostrado = 0f; Progresso(0f); }
        }

        _aceitaNova = false;
        yield return Fade(0f, 0.25f);
        _grupo.blocksRaycasts = false;
        _rodando = false;
    }

    /// <summary>Logo "respirando" e troca de dica a cada poucos segundos. Retorna o novo tempo da dica.</summary>
    private float Animar(float tempoDica, float dt)
    {
        _logo.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.025f);
        tempoDica += dt;
        if (tempoDica < TempoPorDica) return tempoDica;
        NovaDica();
        return 0f;
    }

    private void NovaDica() => _dica.text = ProximaDica(ref _indiceDica);

    private void Progresso(float p) => AtualizarBarraFina(_barraFundo, _barraCheia, p);

    private IEnumerator Fade(float alvo, float duracao)
    {
        var inicio = _grupo.alpha;
        for (var t = 0f; t < duracao; t += Time.unscaledDeltaTime)
        {
            _grupo.alpha = Mathf.Lerp(inicio, alvo, t / duracao);
            yield return null;
        }
        _grupo.alpha = alvo;
    }
}
