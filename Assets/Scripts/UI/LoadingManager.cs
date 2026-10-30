using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Tela de carregamento do menu. Enquanto a barra anda, a cena do mapa é carregada de verdade
/// em segundo plano; o botão "Jogar" só aparece quando ela estiver pronta (e depois de um tempo mínimo,
/// para a animação do logo não ficar corrida).
/// </summary>
public class LoadingManager : MonoBehaviour
{
    public Slider loadingBar;
    public GameObject playButton;

    [Header("Logo do Jogo")]
    public RectTransform gameLogo; // Arraste o logo no Inspector
    public float logoScaleDuration = 1f;
    public Vector3 logoStartScale = new Vector3(0.1f, 0.1f, 0.1f);
    public Vector3 logoEndScale = Vector3.one;

    [Header("Tempo mínimo de carregamento")]
    public float minLoadTime = 2f;
    public float maxLoadTime = 5f;

    [Header("Cena carregada em segundo plano")]
    public string cenaParaCarregar = Cenas.Mapa;

    private static AsyncOperation _preCarregamento;

    /// <summary>Entra na cena que já foi carregada em segundo plano. Retorna false se não houver.</summary>
    public static bool AtivarCenaPreCarregada()
    {
        if (_preCarregamento == null) return false;
        _preCarregamento.allowSceneActivation = true;
        _preCarregamento = null;
        return true;
    }

    void Start()
    {
        loadingBar.value = 0f;
        playButton.SetActive(false);

        if (gameLogo != null)
        {
            gameLogo.localScale = logoStartScale;
            gameLogo.gameObject.SetActive(false);
        }

        StartCoroutine(Carregar());
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
        while (true)
        {
            tempo += Time.deltaTime;
            // O carregamento do Unity para em 0.9 enquanto espera a ativação.
            var real = _preCarregamento != null ? Mathf.Clamp01(_preCarregamento.progress / 0.9f) : 1f;
            var alvo = Mathf.Min(real, tempo / tempoMinimo);
            mostrado = Mathf.MoveTowards(mostrado, alvo, Time.deltaTime * 1.5f);
            loadingBar.value = mostrado;
            if (mostrado >= 0.999f) break;
            yield return null;
        }

        loadingBar.gameObject.SetActive(false);

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
