using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Textos que sobem por cima dos Digimon na luta: dano, "Miss", "Guard", "Critical!" e o nome da técnica.
/// Funciona em qualquer cena de batalha, sem configurar nada (cria o próprio Canvas).
/// </summary>
public class TextoDeBatalha : MonoBehaviour
{
    private static TextoDeBatalha _instancia;
    private Canvas _canvas;

    public static readonly Color CorDanoInimigo = new Color(1f, 0.85f, 0.2f);
    public static readonly Color CorDanoParceiro = new Color(1f, 0.4f, 0.4f);
    public static readonly Color CorTecnica = Color.white;
    public static readonly Color CorDefesa = new Color(0.55f, 0.85f, 1f);

    public static void Mostrar(Vector3 mundo, string texto, Color cor, float tamanho = 60f, float subida = 120f)
    {
        if (string.IsNullOrEmpty(texto)) return;
        if (_instancia == null)
        {
            var go = new GameObject("TextoDeBatalha");
            _instancia = go.AddComponent<TextoDeBatalha>();
            _instancia._canvas = go.AddComponent<Canvas>();
            _instancia._canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _instancia._canvas.sortingOrder = 150;
            UIResponsiva.ConfigurarEscala(go.AddComponent<UnityEngine.UI.CanvasScaler>(), new Vector2(1080, 1920));
        }
        _instancia.StartCoroutine(_instancia.Subir(mundo, texto, cor, tamanho, subida));
    }

    private IEnumerator Subir(Vector3 mundo, string texto, Color cor, float tamanho, float subida)
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        var go = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(_canvas.transform, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        var fonte = EstiloUI.Atual != null ? (EstiloUI.Atual.FontePesada != null ? EstiloUI.Atual.FontePesada : EstiloUI.Atual.FonteNegrito) : null;
        if (fonte != null) t.font = fonte;
        t.text = texto;
        t.fontSize = tamanho;
        t.fontStyle = FontStyles.Bold;
        t.color = cor;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(0, 0, 0, 200);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(700, 120);
        var escala = _canvas.transform.localScale.x;
        var lado = Random.Range(-30f, 30f);
        for (float k = 0; k < 1f; k += Time.deltaTime * 1.1f)
        {
            if (cam == null) break;
            var tela = cam.WorldToScreenPoint(mundo);
            go.SetActive(tela.z > 0);
            rt.position = tela + new Vector3(lado * escala, k * subida * escala, 0);
            // "Pulo" no começo e some no fim.
            rt.localScale = Vector3.one * (k < 0.12f ? Mathf.Lerp(1.4f, 1f, k / 0.12f) : 1f);
            t.alpha = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            yield return null;
        }
        Destroy(go);
    }
}
