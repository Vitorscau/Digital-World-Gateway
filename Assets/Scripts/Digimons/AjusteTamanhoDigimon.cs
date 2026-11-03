using UnityEngine;

/// <summary>
/// Um Digimon da régua de tamanhos (prefab "ReguaDeTamanhos"). Mude o Ajuste aqui no Inspector
/// (ou escale o modelo com a ferramenta de escala) e clique em "Salvar ajustes" na raiz da régua.
/// O ajuste é um multiplicador sobre o tamanho normal do Digimon (1 = normal).
/// </summary>
[ExecuteAlways]
public class AjusteTamanhoDigimon : MonoBehaviour
{
    [Tooltip("Nome da ficha em Resources/Digimons (o mesmo do ajuste_tamanho.txt).")]
    public string especie;

    [Tooltip("1 = tamanho normal · 1.5 = 50% maior · 0.5 = metade.")]
    [Range(0.05f, 5f)] public float ajuste = 1f;

    [HideInInspector] public Transform modelo;
    [HideInInspector] public Vector3 escalaBase = Vector3.one;
    [HideInInspector] public TextMesh rotulo;

    private float _aplicado = -1f;

    private void OnValidate() => Aplicar();

    private void Update()
    {
        if (modelo == null) return;
        // Escalou o modelo com a ferramenta de escala? O ajuste acompanha.
        if (!Mathf.Approximately(ajuste, _aplicado))
        {
            Aplicar();
            return;
        }
        var lido = escalaBase.x != 0f ? modelo.localScale.x / escalaBase.x : 1f;
        if (Mathf.Abs(lido - ajuste) > 0.001f)
        {
            ajuste = Mathf.Round(lido * 100f) / 100f;
            _aplicado = ajuste;
            Rotular();
        }
    }

    public void Aplicar()
    {
        if (modelo == null) return;
        modelo.localScale = escalaBase * ajuste;
        _aplicado = ajuste;
        Rotular();
    }

    private void Rotular()
    {
        if (rotulo != null) rotulo.text = Mathf.Approximately(ajuste, 1f) ? especie : $"{especie}  x{ajuste:0.##}";
    }
}
