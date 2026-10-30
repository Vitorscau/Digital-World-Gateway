using UnityEngine;
using System.Collections;

public class UIAnimator : MonoBehaviour
{
    public float animationDuration = 0.5f;
    public float startYOffset = -800f;

    private RectTransform rectTransform;
    private Vector2 targetPosition;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        targetPosition = rectTransform.anchoredPosition;

        // Começa fora da tela
        rectTransform.anchoredPosition = targetPosition + new Vector2(0, startYOffset);
        gameObject.SetActive(false); // Começa desativado
    }

    public void AparecerComAnimacao()
    {
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(AnimarEntrada());
    }

    private IEnumerator AnimarEntrada()
    {
        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 endPos = targetPosition;

        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            float t = elapsed / animationDuration;
            // Ease-out: mais suave no final
            t = 1f - Mathf.Pow(1f - t, 3);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rectTransform.anchoredPosition = endPos;
        
    }
}
