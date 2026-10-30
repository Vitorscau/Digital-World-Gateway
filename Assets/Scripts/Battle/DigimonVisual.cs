using UnityEngine;

/// <summary>
/// Cria o modelo de um Digimon pronto para lutar: tira os scripts do mapa, coloca física e animação
/// e ajusta o tamanho para uma altura certa (os prefabs têm escalas muito diferentes entre si).
/// </summary>
public static class DigimonVisual
{
    public static CriaturaArena Criar(DigimonBase ficha, GameObject modelo, Transform ponto, float altura, Transform pai = null)
    {
        var origem = modelo != null ? modelo : ficha != null ? ficha.Prefab : null;
        if (origem == null) return null;

        var go = Object.Instantiate(origem, ponto.position, ponto.rotation, pai);
        go.name = (ficha != null ? ficha.Name.Trim() : origem.name) + "_Batalha";

        // Scripts que só fazem sentido no mapa.
        foreach (var c in go.GetComponents<WildDigimon>()) Object.Destroy(c);
        foreach (var c in go.GetComponents<DigimonMapa>()) Object.Destroy(c);
        foreach (var c in go.GetComponents<MapDigimon>()) Object.Destroy(c);

        // Animação: usa o controller da ficha se o modelo não tiver um.
        var animator = go.GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController == null && ficha != null && ficha.Animator != null)
            animator.runtimeAnimatorController = ficha.Animator.runtimeAnimatorController;
        if (animator != null) animator.applyRootMotion = false;

        // Física: cinemática (anda por script, não cai nem empurra o outro).
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        Normalizar(go, ponto.position, altura);

        var criatura = go.GetComponent<CriaturaArena>();
        if (criatura == null) criatura = go.AddComponent<CriaturaArena>();
        criatura.enabled = true; // alguns prefabs vêm com ela desligada
        criatura.rb = rb;
        criatura.animator = animator;
        if (animator != null) animator.enabled = true;
        criatura.podeLutar = false;
        criatura.velocidadeMovimento = altura * 1.2f / Mathf.Max(0.0001f, go.transform.lossyScale.x);
        criatura.distanciaParada = altura * 0.9f / Mathf.Max(0.0001f, go.transform.lossyScale.x);
        return criatura;
    }

    /// <summary>Altura (em unidades do mundo) de um modelo, contando só as malhas.</summary>
    public static float Altura(GameObject go)
    {
        var b = Limites(go, out var ok);
        return ok ? b.size.y : 0f;
    }

    private static void Normalizar(GameObject go, Vector3 chao, float altura)
    {
        var b = Limites(go, out var ok);
        if (!ok || b.size.y <= 0.00001f) return;
        go.transform.localScale *= altura / b.size.y;
        b = Limites(go, out _);
        // Pés no chão, centralizado no ponto.
        go.transform.position += new Vector3(chao.x - b.center.x, chao.y - b.min.y, chao.z - b.center.z);
    }

    private static Bounds Limites(GameObject go, out bool ok)
    {
        ok = false;
        var b = new Bounds();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue; // ignora partículas e linhas
            if (!ok) { b = r.bounds; ok = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
