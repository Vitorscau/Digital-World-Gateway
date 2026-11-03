using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mensagem curta que aparece na parte de cima da tela e some sozinha
/// (ex.: "Chegue mais perto para batalhar"). Funciona em qualquer cena, sem configurar nada.
/// </summary>
public class AvisoNaTela : MonoBehaviour
{
    private static AvisoNaTela _instancia;
    private readonly Queue<string> _fila = new Queue<string>();
    private CanvasGroup _grupo;
    private TMP_Text _texto;
    private bool _mostrando;

    public static void Mostrar(string mensagem)
    {
        if (string.IsNullOrEmpty(mensagem)) return;
        Garantir();
        _instancia._fila.Enqueue(mensagem);
        if (!_instancia._mostrando) _instancia.StartCoroutine(_instancia.Rodar());
    }

    private static void Garantir()
    {
        if (_instancia != null) return;
        var go = new GameObject("AvisoNaTela");
        DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        UIResponsiva.ConfigurarEscala(go.AddComponent<CanvasScaler>(), new Vector2(1080, 1920));
        _instancia = go.AddComponent<AvisoNaTela>();
        _instancia.Montar(go.transform);
    }

    private void Montar(Transform raiz)
    {
        // Cartão azul com borda branca e texto em Lato (padrão das telas do jogo).
        var rt = UIPadrao.Cartao(raiz, "Painel");
        rt.anchorMin = new Vector2(0.06f, 1f);
        rt.anchorMax = new Vector2(0.94f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -170);
        rt.sizeDelta = new Vector2(0, 140);
        rt.GetComponent<Image>().raycastTarget = false;
        _grupo = rt.gameObject.AddComponent<CanvasGroup>();
        _grupo.alpha = 0;
        _grupo.blocksRaycasts = false;

        _texto = UIPadrao.Texto(rt, "", 40, true, TextAlignmentOptions.Center);
        UIPadrao.Esticar(_texto.rectTransform, 30, 10);
    }

    private IEnumerator Rodar()
    {
        _mostrando = true;
        while (_fila.Count > 0)
        {
            _texto.text = _fila.Dequeue();
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 6) { _grupo.alpha = t; yield return null; }
            _grupo.alpha = 1;
            yield return new WaitForSecondsRealtime(2.2f);
            for (float t = 1; t > 0; t -= Time.unscaledDeltaTime * 4) { _grupo.alpha = t; yield return null; }
            _grupo.alpha = 0;
        }
        _mostrando = false;
    }
}
