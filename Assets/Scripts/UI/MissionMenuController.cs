using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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

    private RectTransform currentMenu;
    private Vector2 dragStartPos;
    private bool isDragging = false;
    private float dragThreshold = 100f;

    void Start()
    {
        // Inicializa tudo escondido
        menuMissionsContainer.anchoredPosition = hiddenPos;
        historyMenu.anchoredPosition = hiddenPos;
        dailyMenu.anchoredPosition = hiddenPos;
        specialMenu.anchoredPosition = hiddenPos;

        menuMissionsContainer.gameObject.SetActive(false);
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
        if (Input.GetMouseButtonDown(0))
        {
            dragStartPos = Input.mousePosition;
            isDragging = true;
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
