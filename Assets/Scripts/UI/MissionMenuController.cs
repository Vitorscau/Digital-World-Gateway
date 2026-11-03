using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Menu de missões da Map_Scene: o painel sobe de baixo para cima ao tocar na aba de missões da HUD,
/// as abas History / Daily / Special trocam deslizando, e arrastar para baixo (ou tocar na aba já selecionada) fecha.
/// Cada painel é preenchido com as missões de verdade por <see cref="ListaMissoes"/>.
/// </summary>
public class MissionMenuController : MonoBehaviour
{
    [Header("Menus de Missão")]
    public RectTransform menuMissionsContainer;
    public RectTransform historyMenu;
    public RectTransform dailyMenu;
    public RectTransform specialMenu;

    [Header("Velocidade da animação")]
    public float animationDuration = 0.5f;

    [Header("Posições")]
    public Vector2 hiddenPos = new Vector2(0, -2000);
    public Vector2 visiblePos = new Vector2(0, 0);

    // Topo de cada painel na tela do Figma (Page 2, 1029 x 2228): "2446" History e "2447" Daily.
    private const float FigmaLargura = 1029f, FigmaAltura = 2228f;
    private const float TopoHistory = 759f, TopoDaily = 650f, TopoSpecial = 650f;
    private const float LarguraBarraHUD = 981f, LarguraVisivelPainel = 991f;

    private RectTransform currentMenu;
    private Vector2 dragStartPos;
    private bool isDragging = false;
    private float dragThreshold = 100f;

    void Start()
    {
        // Painéis presos na borda de baixo da tela, na largura dela, como no Figma.
        Esticar(menuMissionsContainer);
        Esticar(historyMenu);
        Esticar(dailyMenu);
        Esticar(specialMenu);
        PosicionarPainel(historyMenu, TopoHistory);
        PosicionarPainel(dailyMenu, TopoDaily);
        PosicionarPainel(specialMenu, TopoSpecial);

        // Inicializa tudo escondido
        menuMissionsContainer.anchoredPosition = hiddenPos;
        historyMenu.anchoredPosition = hiddenPos;
        dailyMenu.anchoredPosition = hiddenPos;
        specialMenu.anchoredPosition = hiddenPos;

        Preparar(historyMenu, CategoriaMissao.Historia);
        Preparar(dailyMenu, CategoriaMissao.Diaria);
        Preparar(specialMenu, CategoriaMissao.Especial);

        menuMissionsContainer.gameObject.SetActive(false);
    }

    /// <summary>O painel (imagem) dentro de cada menu recebe a lista de missões.</summary>
    private static void Preparar(RectTransform menu, CategoriaMissao categoria)
    {
        var painel = Painel(menu);
        if (painel != null) ListaMissoes.Preparar(painel, categoria);
    }

    private static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>Largura da tela e topo na altura do Figma (o resto do painel passa da borda de baixo).</summary>
    private void PosicionarPainel(RectTransform menu, float topoFigma)
    {
        var img = Painel(menu);
        var tela = menuMissionsContainer.parent as RectTransform;
        if (img == null || img.sprite == null || tela == null) return;
        var s = tela.rect.width / FigmaLargura;
        var rt = img.rectTransform;
        var r = rt.rect;
        // Proporção da imagem pelo sprite; tamanho real pelo Figma (o Unity reduz a textura para 2048 de altura,
        // então sprite.rect não é o tamanho do desenho).
        var sw = img.sprite.rect.width;
        var sh = img.sprite.rect.height;
        const float larguraFigma = 1001f, alturaFigma = 2227f;
        var exibida = img.preserveAspect ? Mathf.Min(r.width / sw, r.height / sh) * sw : r.width;
        if (exibida <= 0f) return;
        // A borda branca do painel (x 5 a 995 da imagem) fica com a largura da barra da HUD (981 no Figma).
        var largura = larguraFigma * LarguraBarraHUD / LarguraVisivelPainel;
        var k = s * largura / exibida;
        rt.localScale = new Vector3(k, k, 1f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        var centroY = (FigmaAltura - topoFigma) * s - alturaFigma * (largura / larguraFigma) * s / 2f;
        rt.anchoredPosition = new Vector2(0f, centroY) - r.center * k;
    }

    private static Image Painel(RectTransform menu)
    {
        foreach (var img in menu.GetComponentsInChildren<Image>(true))
            if (img.GetComponent<Button>() == null && img.transform.parent == menu) return img;
        return null;
    }

    // Chamar no botão para abrir o container
    public void AbrirMenuMissoes()
    {
        menuMissionsContainer.gameObject.SetActive(true);
        StartCoroutine(Animar(menuMissionsContainer, visiblePos));
        TrocarMenu(historyMenu);
    }

    // Botões dentro do popup
    public void SelecionarHistory() => TrocarMenu(historyMenu);
    public void SelecionarDaily() => TrocarMenu(dailyMenu);
    public void SelecionarSpecial() => TrocarMenu(specialMenu);

    void TrocarMenu(RectTransform novoMenu)
    {
        if (currentMenu == novoMenu) return;

        StartCoroutine(TrocarMenusAnimado(currentMenu, novoMenu));
        currentMenu = novoMenu;
    }

    IEnumerator TrocarMenusAnimado(RectTransform antigo, RectTransform novo)
    {
        if (antigo != null)
            yield return StartCoroutine(Animar(antigo, hiddenPos));

        novo.gameObject.SetActive(true);
        novo.anchoredPosition = hiddenPos;
        yield return StartCoroutine(Animar(novo, visiblePos));
    }

    IEnumerator Animar(RectTransform target, Vector2 destino)
    {
        Vector2 inicio = target.anchoredPosition;
        float t = 0f;

        while (t < animationDuration)
        {
            t += Time.deltaTime;
            float progresso = t / animationDuration;
            target.anchoredPosition = Vector2.Lerp(inicio, destino, progresso);
            yield return null;
        }

        target.anchoredPosition = destino;

        // Desativa após descer
        if (destino == hiddenPos)
            target.gameObject.SetActive(false);
    }

    void Update()
    {
        DetectarArraste();
    }

    void DetectarArraste()
    {
        // Só fecha arrastando para baixo se o menu estiver aberto e o arraste começar em cima do painel
        // (e não numa lista que ainda pode rolar para cima: aí o arraste é rolagem).
        if (Input.GetMouseButtonDown(0))
        {
            dragStartPos = Input.mousePosition;
            var painel = currentMenu != null ? Painel(currentMenu) : null;
            var canvas = menuMissionsContainer.GetComponentInParent<Canvas>();
            var cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            isDragging = menuMissionsContainer.gameObject.activeInHierarchy && painel != null &&
                RectTransformUtility.RectangleContainsScreenPoint(painel.rectTransform, dragStartPos, cam) &&
                !ListaMissoes.ToqueEmListaRolando(dragStartPos);
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            float dragDelta = dragStartPos.y - Input.mousePosition.y;
            if (dragDelta > dragThreshold)
            {
                FecharMenuMissoes();
            }

            isDragging = false;
        }
    }

    public void FecharMenuMissoes()
    {
        StartCoroutine(Animar(menuMissionsContainer, hiddenPos));
        if (currentMenu != null)
        {
            StartCoroutine(Animar(currentMenu, hiddenPos));
            currentMenu = null;
        }
    }
}
