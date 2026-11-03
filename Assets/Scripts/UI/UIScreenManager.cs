using UnityEngine;

public class UIScreenManager : MonoBehaviour
{
    [Header("Referências de UI")]
    public GameObject telaInicialBackground;
    public RectTransform popupInicial;
    public GameObject HUD;

    [Header("Animação")]
    public float tempoAnimacao = 0.5f;
    public Vector2 offsetEntrada = new Vector2(0, -800f); // de onde vem o popup

    private Vector2 posicaoFinal;

    /// <summary>O aviso "Stay Aware" aparece só uma vez por abertura do jogo (não ao voltar da batalha, Care...).</summary>
    private static bool _jaMostrada;

    void Start()
    {
        if (_jaMostrada)
        {
            telaInicialBackground.SetActive(false);
            popupInicial.gameObject.SetActive(false);
            HUD.SetActive(true);
            return;
        }
        _jaMostrada = true;

        // O fundo cobre a tela toda em qualquer proporção (o grupo "Stay" encolhe para caber na área segura).
        var cobrir = telaInicialBackground.GetComponent<CobrirTela>();
        if (cobrir == null) cobrir = telaInicialBackground.AddComponent<CobrirTela>();
        cobrir.ManterProporcao = true;

        // Centralizado na tela (o grupo "Stay" já fica centralizado na área segura).
        posicaoFinal = Vector2.zero;

        // Ativa o background e move popup para fora da tela
        telaInicialBackground.SetActive(true);
        HUD.SetActive(false);
        popupInicial.anchoredPosition = posicaoFinal + offsetEntrada;

        // Anima o popup para entrar
        StartCoroutine(AnimarEntrada(popupInicial, posicaoFinal));
    }

    public void FecharTelaInicial()
    {
        // Anima para sair e troca UI ao final
        StartCoroutine(AnimarSaida(popupInicial, () =>
        {
            telaInicialBackground.SetActive(false);
            popupInicial.gameObject.SetActive(false); // some depois de sair
            HUD.SetActive(true);
        }));
    }

    System.Collections.IEnumerator AnimarEntrada(RectTransform target, Vector2 destino)
    {
        float tempo = 0;
        Vector2 origem = target.anchoredPosition;

        while (tempo < tempoAnimacao)
        {
            tempo += Time.deltaTime;
            float t = Mathf.Clamp01(tempo / tempoAnimacao);
            target.anchoredPosition = Vector2.Lerp(origem, destino, EaseOut(t));
            yield return null;
        }

        target.anchoredPosition = destino;
    }

    System.Collections.IEnumerator AnimarSaida(RectTransform target, System.Action aoFinalizar)
    {
        float tempo = 0;
        Vector2 origem = target.anchoredPosition;
        Vector2 destino = posicaoFinal + offsetEntrada;

        while (tempo < tempoAnimacao)
        {
            tempo += Time.deltaTime;
            float t = Mathf.Clamp01(tempo / tempoAnimacao);
            target.anchoredPosition = Vector2.Lerp(origem, destino, EaseIn(t));
            yield return null;
        }

        target.anchoredPosition = destino;

        aoFinalizar?.Invoke();
    }

    float EaseOut(float t) => 1 - Mathf.Pow(1 - t, 3);
    float EaseIn(float t) => Mathf.Pow(t, 3);
}
