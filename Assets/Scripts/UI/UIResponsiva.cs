using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Deixa a UI igual em qualquer celular (16:9, 19.5:9, 20:9, 21:9, tablets, com ou sem notch):
/// - Todos os canvas escalam com a tela no modo "Expand": a área desenhada sempre cabe inteira, nada corta nas laterais.
/// - A área desenhada (Figma ou cena) é encaixada dentro da área segura (fora do notch e da barra de gestos).
/// - A HUD do mapa é posicionada pelas medidas do Figma em HUDParceiro.
/// - Fundos e véus continuam cobrindo a tela toda.
/// Cenas antigas com "Constant Pixel Size" são corrigidas ao carregar.
/// </summary>
public static class UIResponsiva
{
    /// <summary>Resolução em que as cenas (Mapa, Menu, AR) foram montadas.</summary>
    public static readonly Vector2 ReferenciaCenas = new Vector2(1080f, 2400f);

    /// <summary>Grupos de cada cena (filhos diretos do canvas) encaixados inteiros na área segura.</summary>
    private static readonly Dictionary<string, string[]> GruposCentrados = new Dictionary<string, string[]>
    {
        { Cenas.Mapa, new[] { "Stay" } },
    };

    public static void ConfigurarEscala(CanvasScaler s, Vector2 referencia)
    {
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = referencia;
        s.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Registrar()
    {
        SceneManager.sceneLoaded -= AoCarregar;
        SceneManager.sceneLoaded += AoCarregar;
        AoCarregar(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void AoCarregar(Scene cena, LoadSceneMode modo)
    {
        GruposCentrados.TryGetValue(cena.name, out var centrados);
        foreach (var raiz in cena.GetRootGameObjects())
        foreach (var canvas in raiz.GetComponentsInChildren<Canvas>(true))
        {
            if (!canvas.isRootCanvas) continue;
            var s = canvas.GetComponent<CanvasScaler>();
            if (s != null && s.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize) ConfigurarEscala(s, ReferenciaCenas);
            var referencia = s != null ? s.referenceResolution : ReferenciaCenas;
            foreach (Transform filho in canvas.transform)
            {
                if (centrados != null && Array.IndexOf(centrados, filho.name) >= 0 && filho.GetComponent<AreaDeDesenho>() == null)
                    filho.gameObject.AddComponent<AreaDeDesenho>().Tamanho = referencia;
            }
        }
    }

    /// <summary>
    /// Área segura da tela (Screen.safeArea) no espaço local de <paramref name="pai"/>.
    /// <paramref name="deslocamento"/>: fração da altura da tela para baixo (para acompanhar uma tela deslizando).
    /// </summary>
    public static Rect AreaSeguraLocal(RectTransform pai, Canvas canvas, bool telaToda = false, float deslocamento = 0f)
    {
        var area = telaToda ? new Rect(0, 0, Screen.width, Screen.height) : Screen.safeArea;
        area.y -= deslocamento * Screen.height;
        var cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pai, area.min, cam, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pai, area.max, cam, out var max);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}

/// <summary>
/// Encaixa um retângulo de tamanho fixo (a "prancheta" do desenho) dentro da área segura da tela,
/// centralizado e com a maior escala que cabe. Os filhos são posicionados nas medidas do desenho.
/// <see cref="Deslocamento"/> empurra a prancheta para baixo (1 = uma tela inteira), para animar entrada e saída.
/// </summary>
[DisallowMultipleComponent]
public class AreaDeDesenho : MonoBehaviour
{
    public Vector2 Tamanho = new Vector2(FigmaUI.Largura, FigmaUI.Altura);

    /// <summary>Fração da altura da tela para baixo (0 = no lugar, 1 = fora da tela, embaixo).</summary>
    public float Deslocamento { get; set; }

    private Vector3 _escalaOriginal;
    private Vector2Int _tela;
    private Rect _seguro;
    private Vector2 _posicaoBase;
    private float _alturaPai;
    private bool _ajustada;

    private void Awake() => _escalaOriginal = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;

    private void OnEnable() => _tela = Vector2Int.zero; // força recalcular

    private void LateUpdate()
    {
        var tela = new Vector2Int(Screen.width, Screen.height);
        if (tela != _tela || Screen.safeArea != _seguro)
            if (Ajustar()) { _tela = tela; _seguro = Screen.safeArea; }
        if (_ajustada) ((RectTransform)transform).anchoredPosition = _posicaoBase + new Vector2(0f, -Deslocamento * _alturaPai);
    }

    private bool Ajustar()
    {
        var rt = (RectTransform)transform;
        var pai = rt.parent as RectTransform;
        var canvas = GetComponentInParent<Canvas>();
        if (pai == null || canvas == null) return false;
        Canvas.ForceUpdateCanvases(); // garante a escala do CanvasScaler deste quadro
        var area = UIResponsiva.AreaSeguraLocal(pai, canvas);
        if (area.width <= 1f || area.height <= 1f) return false;

        var k = Mathf.Min(area.width / Tamanho.x, area.height / Tamanho.y);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Tamanho;
        rt.localScale = _escalaOriginal * k;
        _posicaoBase = area.center - pai.rect.center;
        _alturaPai = pai.rect.height;
        _ajustada = true;
        return true;
    }
}

/// <summary>
/// Faz um retângulo cobrir a tela inteira mesmo estando dentro de uma <see cref="AreaDeDesenho"/>
/// (fundos, véus e clarões não deixam faixas nas bordas). Se a área estiver deslizando, o fundo desliza junto.
/// </summary>
[DisallowMultipleComponent]
public class CobrirTela : MonoBehaviour
{
    /// <summary>true: cobre a tela mantendo a proporção da imagem (corta o que sobrar, sem esticar a arte).</summary>
    public bool ManterProporcao;

    private Vector3 _ultimaEscala;
    private Vector3 _ultimaPosicao;
    private Vector2Int _tela;
    private AreaDeDesenho _area;

    private void Awake() => _area = GetComponentInParent<AreaDeDesenho>();

    private void LateUpdate()
    {
        var rt = (RectTransform)transform;
        var pai = rt.parent as RectTransform;
        if (pai == null) return;
        var tela = new Vector2Int(Screen.width, Screen.height);
        if (tela == _tela && pai.lossyScale == _ultimaEscala && pai.position == _ultimaPosicao) return;
        _tela = tela;
        _ultimaEscala = pai.lossyScale;
        _ultimaPosicao = pai.position;

        var canvas = GetComponentInParent<Canvas>();
        var area = UIResponsiva.AreaSeguraLocal(pai, canvas, telaToda: true, _area != null ? _area.Deslocamento : 0f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var tamanho = area.size + Vector2.one * 4f; // folga contra arredondamento
        var img = ManterProporcao ? GetComponent<UnityEngine.UI.Image>() : null;
        if (img != null && img.sprite != null)
        {
            img.preserveAspect = false; // o retângulo já tem a proporção da imagem
            var proporcao = img.sprite.rect.width / img.sprite.rect.height;
            if (tamanho.x / tamanho.y > proporcao) tamanho.y = tamanho.x / proporcao;
            else tamanho.x = tamanho.y * proporcao;
        }
        rt.localScale = Vector3.one;
        rt.sizeDelta = tamanho;
        rt.anchoredPosition = area.center - pai.rect.center;
    }
}
