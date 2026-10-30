using UnityEngine;

/// <summary>
/// Rosto 3D animado de um Digimon, ao vivo, numa textura (para a foto do parceiro na HUD).
/// O modelo fica escondido bem longe do mapa, com uma câmera só dele enquadrando a cabeça.
/// </summary>
public class RetratoVivo : MonoBehaviour
{
    private const int Camada = 31;
    private static int _contador;

    public RenderTexture Textura { get; private set; }
    public DigimonBase Ficha { get; private set; }

    private Camera _cam;
    private Transform _suporte;
    private GameObject _modelo;
    private int _quadros;
    private bool _corpoInteiro;

    /// <summary>Cria o retrato. Com <paramref name="corpoInteiro"/>, enquadra o Digimon todo (não só o rosto).</summary>
    public static RetratoVivo Criar(DigimonBase ficha, int tamanho = 256, bool corpoInteiro = false)
    {
        if (ficha == null || ficha.Prefab == null) return null;
        var go = new GameObject("RetratoVivo_" + ficha.name);
        var r = go.AddComponent<RetratoVivo>();
        r._corpoInteiro = corpoInteiro;
        r.Montar(ficha, tamanho);
        return r;
    }

    private void Montar(DigimonBase ficha, int tamanho)
    {
        Ficha = ficha;
        // Cada retrato no seu canto, abaixo do mapa. Não muito longe: a precisão dos números cai
        // com a distância e os vértices começam a "pular" (modelo picotado).
        transform.position = new Vector3(40f * ++_contador, -1000f, 0f);

        _suporte = new GameObject("Suporte").transform;
        _suporte.SetParent(transform, false);
        _modelo = Instantiate(ficha.Prefab, _suporte, false);
        _modelo.transform.localPosition = Vector3.zero;
        foreach (var mb in _modelo.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        foreach (var c in _modelo.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var rb in _modelo.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
        foreach (var ps in _modelo.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
        var anim = _modelo.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.enabled = true;
            if (anim.runtimeAnimatorController == null && ficha.Animator != null)
                anim.runtimeAnimatorController = ficha.Animator.runtimeAnimatorController;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        _suporte.localRotation = Quaternion.Euler(0, RetratoDigimon.Giro, 0);
        foreach (var t in _modelo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Camada;

        // Tamanho padrão (1 unidade de altura): alguns modelos têm só 1 cm e ficariam imprecisos.
        var altura = AlturaModelo();
        if (altura > 1e-6f) _suporte.localScale = Vector3.one / altura;

        Textura = new RenderTexture(tamanho, tamanho, 24, RenderTextureFormat.ARGB32) { name = "RetratoVivo", antiAliasing = 2 };
        Textura.Create();
        _cam = new GameObject("Camera").AddComponent<Camera>();
        _cam.transform.SetParent(transform, false);
        _cam.cullingMask = 1 << Camada;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0, 0, 0, 0);
        _cam.fieldOfView = 24f;
        _cam.targetTexture = Textura;
        _cam.allowHDR = false;
        _cam.allowMSAA = true;
        // As outras câmeras não desenham a camada dos retratos.
        foreach (var c in Camera.allCameras)
            if (c != _cam && c.GetComponentInParent<RetratoVivo>() == null) // câmeras de outros retratos ficam como estão
                c.cullingMask &= ~(1 << Camada);
        Enquadrar();
    }

    private void LateUpdate()
    {
        // A pose da animação muda o tamanho nos primeiros quadros: enquadra de novo.
        if (_quadros < 3) { _quadros++; Enquadrar(); }
    }

    /// <summary>Câmera na frente da cabeça (parte de cima do modelo).</summary>
    private void Enquadrar()
    {
        if (_cam == null || _modelo == null) return;
        var ok = false;
        var b = new Bounds();
        foreach (var r in _modelo.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!ok) { b = r.bounds; ok = true; } else b.Encapsulate(r.bounds);
        }
        if (!ok) return;
        var h = b.size.y;
        var quadro = _corpoInteiro ? Mathf.Max(h, b.size.x) * 0.72f :Mathf.Max(h * 0.55f, Mathf.Min(b.size.x, h) * 0.6f);
        var alvo = _corpoInteiro ? b.center : new Vector3(b.center.x, b.max.y - quadro * 0.55f, b.center.z);
        var dist = quadro * 0.62f / Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        _cam.nearClipPlane = dist * 0.05f;
        _cam.farClipPlane = dist * 6f + b.size.magnitude;
        _cam.transform.position = alvo + new Vector3(0, quadro * 0.08f, -(dist + b.extents.z));
        _cam.transform.LookAt(alvo);
    }

    private float AlturaModelo()
    {
        var ok = false;
        var b = new Bounds();
        foreach (var r in _modelo.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!ok) { b = r.bounds; ok = true; } else b.Encapsulate(r.bounds);
        }
        return ok ? b.size.y : 0f;
    }

    private void OnDestroy()
    {
        if (Textura != null) { Textura.Release(); Destroy(Textura); }
    }
}
