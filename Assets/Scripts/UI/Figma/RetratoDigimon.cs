using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera o retrato de um Digimon (foto do modelo 3D com fundo transparente) para as telas
/// Profile, Storage e Scan. Cada espécie é fotografada uma vez e guardada.
/// </summary>
public static class RetratoDigimon
{
    private const int Camada = 31; // camada só para as fotos
    private static readonly Dictionary<string, RenderTexture> _cache = new Dictionary<string, RenderTexture>();

    /// <summary>Giro (graus) aplicado ao modelo para ele ficar de frente para a câmera, levemente de lado.</summary>
    public static float Giro = 200f;

    public static Texture Obter(DigimonBase ficha, int tamanho = 512)
    {
        if (ficha == null) return null;
        var chave = ficha.name + "_" + tamanho;
        if (_cache.TryGetValue(chave, out var pronta) && pronta != null && pronta.IsCreated()) return pronta;

        var origem = ficha.Prefab;
        if (origem == null) return null;

        // Um objeto pai segura o giro: a animação de pose pode reescrever a rotação do próprio modelo.
        var suporte = new GameObject("RetratoSuporte");
        suporte.transform.position = new Vector3(0, -1000, 0); // não muito longe: precisão dos vértices
        var go = Object.Instantiate(origem, suporte.transform, false);
        go.transform.localPosition = Vector3.zero;
        try
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.enabled = true;
                if (anim.runtimeAnimatorController == null && ficha.Animator != null)
                    anim.runtimeAnimatorController = ficha.Animator.runtimeAnimatorController;
                anim.applyRootMotion = false;
                anim.Update(0f); // pose parada
            }
            suporte.transform.rotation = Quaternion.Euler(0, Giro, 0);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Camada;

            // Tamanho padrão (1 unidade de altura): modelos de 1 cm ficariam picotados.
            var bruto = Limites(go, out var temMalha);
            if (temMalha && bruto.size.y > 1e-6f) suporte.transform.localScale = Vector3.one / bruto.size.y;

            // Enquadramento pelo tamanho real do modelo.
            var limites = Limites(go, out var ok);
            if (!ok) return null;
            var altura = Mathf.Max(limites.size.y, limites.size.x * 0.9f);
            var camGo = new GameObject("CameraRetrato");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << Camada;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = altura * 50f;
            var distancia = altura * 0.6f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var alvo = limites.center + Vector3.up * altura * 0.02f;
            camGo.transform.position = alvo + new Vector3(0, altura * 0.08f, -distancia);
            camGo.transform.LookAt(alvo);

            var rt = new RenderTexture(tamanho, tamanho, 24, RenderTextureFormat.ARGB32) { name = "Retrato_" + ficha.name, antiAliasing = 4 };
            rt.Create();
            cam.targetTexture = rt;

            var neblina = RenderSettings.fog;
            RenderSettings.fog = false; // o mapa usa névoa; a foto não
            cam.Render();
            RenderSettings.fog = neblina;

            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            _cache[chave] = rt;
            return rt;
        }
        finally
        {
            Object.DestroyImmediate(suporte); // leva o modelo junto
        }
    }

    private static Bounds Limites(GameObject go, out bool ok)
    {
        ok = false;
        var b = new Bounds();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))) continue;
            if (!ok) { b = r.bounds; ok = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
