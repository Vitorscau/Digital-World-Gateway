using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Na batalha em AR (AR_Battle_Scene), mostra um botão "No AR" até a luta começar: leva esta mesma batalha para a
/// arena 3D comum (útil quando a câmera não acha o chão ou o celular não suporta AR). Some quando a luta começa.
/// A arena comum tem o botão inverso ("AR"), em BatalhaDigimon. O padrão fica em Settings ("AR Battles").
/// </summary>
public class TrocaModoBatalha : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Registrar()
    {
        SceneManager.sceneLoaded -= AoCarregar;
        SceneManager.sceneLoaded += AoCarregar;
        AoCarregar(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void AoCarregar(Scene cena, LoadSceneMode modo)
    {
        if (cena.name != Cenas.BatalhaAR || FindFirstObjectByType<TrocaModoBatalha>() != null) return;
        new GameObject("TrocaModoBatalha").AddComponent<TrocaModoBatalha>();
    }

    private GameObject _botao;
    private float _proximaVerificacao;

    private void Start()
    {
        var canvas = UIPadrao.Canvas(transform, "UI_SemAR", 150);
        var b = UIPadrao.Botao(canvas.transform, "No AR", () =>
        {
            if (LutaComecou()) return;
            TelaCarregamento.Ir(Cenas.Batalha);
        }, false, 40f);
        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40f, -110f);
        rt.sizeDelta = new Vector2(260f, 110f);
        _botao = b.gameObject;
    }

    private void Update()
    {
        if (_botao == null || Time.unscaledTime < _proximaVerificacao) return;
        _proximaVerificacao = Time.unscaledTime + 0.25f;
        if (LutaComecou()) Destroy(gameObject); // depois de começar não troca (não dá para fugir de uma luta perdida)
    }

    private static bool LutaComecou()
    {
        foreach (var c in FindObjectsByType<CriaturaArena>(FindObjectsSortMode.None))
            if (c.podeLutar) return true;
        return false;
    }
}
