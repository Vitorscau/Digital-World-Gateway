using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LoadingManager : MonoBehaviour
{
    public Slider loadingBar;
    public GameObject playButton;

    [Header("Logo do Jogo")]
    public RectTransform gameLogo; // Arraste o logo no Inspector
    public float logoScaleDuration = 1f;
    public Vector3 logoStartScale = new Vector3(0.1f, 0.1f, 0.1f);
    public Vector3 logoEndScale = Vector3.one;

    [Header("Tempo de Carregamento Aleatório")]
    public float minLoadTime = 2f;
    public float maxLoadTime = 5f;

    private float totalLoadTime;
    private float progress = 0f;
    private bool loadingComplete = false;

    void Start()
    {
        // Define o tempo total aleatório
        totalLoadTime = Random.Range(minLoadTime, maxLoadTime);

        loadingBar.value = 0f;
        playButton.SetActive(false);

        if (gameLogo != null)
        {
            gameLogo.localScale = logoStartScale;
            gameLogo.gameObject.SetActive(false);
        }

        StartCoroutine(SimulateIrregularLoading());
    }

    IEnumerator SimulateIrregularLoading()
    {
        float elapsedTime = 0f;

        while (elapsedTime < totalLoadTime)
        {
            float remainingTime = totalLoadTime - elapsedTime;
            float increment = Random.Range(0.05f, 0.2f);
            float targetProgress = Mathf.Clamp01(elapsedTime / totalLoadTime + increment * (remainingTime / totalLoadTime));

            while (progress < targetProgress && elapsedTime < totalLoadTime)
            {
                elapsedTime += Time.deltaTime;
                progress = Mathf.Clamp01(elapsedTime / totalLoadTime);
                loadingBar.value = progress;
                yield return null;
            }

            float delay = Random.Range(0.1f, 0.5f);
            yield return new WaitForSeconds(delay);
        }

        loadingBar.gameObject.SetActive(false);

        // Ativa o logo com animação
        if (gameLogo != null)
        {
            gameLogo.gameObject.SetActive(true);
            yield return StartCoroutine(AnimateLogoScale());
        }

        playButton.SetActive(true);
        loadingComplete = true;
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
