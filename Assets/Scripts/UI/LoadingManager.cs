using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

/// <summary>
/// Tela de carregamento do menu (logo no meio e a cena 3D rodando atrás). Enquanto a cena do mapa é carregada
/// de verdade em segundo plano, aparece uma barra fina no pé da tela com dicas em cima (estilo Pokémon GO).
/// O botão "Tap to Start" só aparece quando o mapa estiver pronto. Ao tocar, a troca acontece por trás da
/// <see cref="TelaCarregamento"/>, sem travar.
/// </summary>
public class LoadingManager : MonoBehaviour
{
    [Tooltip("Barra antiga (fica escondida; a barra fina é montada por código).")]
    public Slider loadingBar;
    public GameObject playButton;

    [Header("Logo do Jogo")]
    public RectTransform gameLogo; // Arraste o logo no Inspector
    public float logoScaleDuration = 1f;
    public Vector3 logoStartScale = new Vector3(0.1f, 0.1f, 0.1f);
    public Vector3 logoEndScale = Vector3.one;

    [Header("Tempo mínimo na tela (para dar tempo de ler uma dica)")]
    public float minLoadTime = 4f;
    public float maxLoadTime = 4f;

    [Header("Cena carregada em segundo plano")]
    public string cenaParaCarregar = Cenas.Mapa;

    [Header("Barra fina e dicas (medidas na resolução de referência, a partir do centro)")]
    public float barraY = -1000f;
    public float barraLargura = 880f;
    public float barraAltura = 16f;
    public float tamanhoDica = 46f;
    public float tempoPorDica = 4f;

    private static AsyncOperation _preCarregamento;

    private CanvasGroup _grupo;
    private RectTransform _trilho, _cheia;
    private TMP_Text _dica;
    private int _indiceDica;

    /// <summary>
    /// Entra na cena que já foi carregada em segundo plano. Direto, sem a TelaCarregamento: o próprio menu
    /// já foi a tela de loading. Retorna false se não houver cena pré-carregada.
    /// </summary>
    public static bool AtivarCenaPreCarregada()
    {
        if (_preCarregamento == null) return false;
        _preCarregamento.allowSceneActivation = true;
        _preCarregamento = null;
        return true;
    }

    void Start()
    {
        if (loadingBar != null)
        {
            loadingBar.value = 0f;
            loadingBar.gameObject.SetActive(false);
        }
        playButton.SetActive(false);

        if (gameLogo != null)
        {
            gameLogo.localScale = logoStartScale;
            gameLogo.gameObject.SetActive(false);
        }

        MontarBarra();
        StartCoroutine(Carregar());
    }

    /// <summary>Barra fina + dica, dentro da área segura (não fica embaixo da barra de gestos).</summary>
    private void MontarBarra()
    {
        var canvas = GetComponentInParent<Canvas>();
        var escala = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
        var area = (RectTransform)new GameObject("Carregamento", typeof(RectTransform)).transform;
        area.SetParent(canvas != null ? canvas.transform : transform, false);
        area.gameObject.AddComponent<AreaDeDesenho>().Tamanho = escala != null ? escala.referenceResolution : UIResponsiva.ReferenciaCenas;
        _grupo = area.gameObject.AddComponent<CanvasGroup>();
        _grupo.blocksRaycasts = false;

        _trilho = (RectTransform)new GameObject("Barra", typeof(RectTransform)).transform;
        _trilho.SetParent(area, false);
        _trilho.anchoredPosition = new Vector2(0f, barraY);
        _trilho.sizeDelta = new Vector2(barraLargura, barraAltura);
        _cheia = TelaCarregamento.BarraFina(_trilho);

        _dica = FigmaUI.Texto(area, TelaCarregamento.ProximaDica(ref _indiceDica), tamanhoDica, FigmaUI.Peso.Normal, TextAlignmentOptions.Bottom);
        _dica.textWrappingMode = TextWrappingModes.Normal;
        var rt = _dica.rectTransform;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, barraY + 36f);
        rt.sizeDelta = new Vector2(960f, 200f);
    }

    IEnumerator Carregar()
    {
        var tempoMinimo = Random.Range(minLoadTime, maxLoadTime);
        if (!string.IsNullOrEmpty(cenaParaCarregar) && _preCarregamento == null)
        {
            _preCarregamento = SceneManager.LoadSceneAsync(cenaParaCarregar == Cenas.Mapa ? Cenas.Inicio : cenaParaCarregar);
            if (_preCarregamento != null) _preCarregamento.allowSceneActivation = false;
        }

        float tempo = 0f;
        float mostrado = 0f;
        float tempoDica = 0f;
        while (true)
        {
            tempo += Time.deltaTime;
            tempoDica += Time.deltaTime;
            if (tempoDica >= tempoPorDica)
            {
                tempoDica = 0f;
                _dica.text = TelaCarregamento.ProximaDica(ref _indiceDica);
            }
            // O carregamento do Unity para em 0.9 enquanto espera a ativação.
            var real = _preCarregamento != null ? Mathf.Clamp01(_preCarregamento.progress / 0.9f) : 1f;
            var alvo = Mathf.Min(real, tempo / Mathf.Max(0.01f, tempoMinimo));
            mostrado = Mathf.MoveTowards(mostrado, alvo, Time.deltaTime * 1.5f);
            TelaCarregamento.AtualizarBarraFina(_trilho, _cheia, mostrado);
            if (loadingBar != null) loadingBar.value = mostrado;
            if (mostrado >= 0.999f) break;
            yield return null;
        }

        // Barra e dica somem e o logo entra.
        for (var t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            _grupo.alpha = 1f - t / 0.3f;
            yield return null;
        }
        _grupo.gameObject.SetActive(false);

        if (gameLogo != null)
        {
            gameLogo.gameObject.SetActive(true);
            yield return StartCoroutine(AnimateLogoScale());
        }

        playButton.SetActive(true);
    }

    IEnumerator AnimateLogoScale()
    {
        float elapsed = 0f;
        while (elapsed < logoScaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / logoScaleDuration);
            gameLogo.localScale = Vector3.Lerp(logoStartScale, logoEndScale, t);
            yield return null;
        }
    }
}
