using UnityEngine;
using System.Collections;

public class AudioFadeIn : MonoBehaviour
{
    public AudioSource audioSource;
    public float fadeDuration = 2f; // Tempo para o fade in

    void Start()
    {
        if (audioSource != null)
        {
            StartCoroutine(FadeIn(audioSource, fadeDuration));
        }
    }

    IEnumerator FadeIn(AudioSource source, float duration)
    {
        source.volume = 0f;
        source.Play();

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Clamp01(elapsed / duration);
            yield return null;
            
        }
    }
}
