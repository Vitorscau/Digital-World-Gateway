using System.Collections.Generic;
using System.Globalization;
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

        // O modelo fica dentro de um "suporte": a escala e o movimento vão no suporte, que nenhuma animação mexe.
        // (Clipes com curva de escala na raiz ou nos ossos sobrescreviam a escala aplicada no próprio modelo,
        // e o Digimon ficava gigante ou minúsculo assim que a animação começava.)
        var go = new GameObject((ficha != null ? ficha.Name.Trim() : origem.name) + "_Batalha");
        go.transform.SetPositionAndRotation(ponto.position, ponto.rotation);
        if (pai != null) go.transform.SetParent(pai, true);
        var corpo = Object.Instantiate(origem, go.transform);
        corpo.transform.localPosition = Vector3.zero;
        corpo.transform.localRotation = Quaternion.identity;

        // Scripts que só fazem sentido no mapa.
        foreach (var c in corpo.GetComponents<WildDigimon>()) Object.Destroy(c);
        foreach (var c in corpo.GetComponents<DigimonMapa>()) Object.Destroy(c);
        foreach (var c in corpo.GetComponents<MapDigimon>()) Object.Destroy(c);
        // O anel de partículas (Ripple) é do mapa: na batalha o modelo é ampliado e ele ficava gigante.
        foreach (var ps in corpo.GetComponentsInChildren<ParticleSystem>(true))
            if (ps.name.StartsWith("Ripple")) ps.gameObject.SetActive(false);
        // Física e luta ficam no suporte.
        foreach (var r in corpo.GetComponentsInChildren<Rigidbody>()) { r.isKinematic = true; Object.Destroy(r); }
        var original = corpo.GetComponent<CriaturaArena>(); // alguns prefabs trazem nomes de animação próprios

        // Animação: usa o controller da ficha se o modelo não tiver um.
        var animator = corpo.GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController == null && ficha != null && ficha.Animator != null)
            animator.runtimeAnimatorController = ficha.Animator.runtimeAnimatorController;
        if (animator != null)
        {
            TrocarAnimacoesQuebradas(animator);
            animator.applyRootMotion = false;
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f); // já na pose da animação: a medida abaixo é a do Digimon como ele aparece
        }
        // Limites que acompanham a animação (senão o Unity esconde partes ou mede errado).
        foreach (var s in corpo.GetComponentsInChildren<SkinnedMeshRenderer>()) s.updateWhenOffscreen = true;
        // A malha não encolhe/estica na troca de animação (bug de mistura do Mecanim em objetos que não são ossos).
        TravaEscala.Aplicar(corpo);

        // Física: cinemática (anda por script, não cai nem empurra o outro).
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        altura *= Ajuste(ficha);
        Normalizar(go, ponto.position, altura, animada: true);

        var criatura = go.AddComponent<CriaturaArena>();
        if (original != null)
        {
            criatura.nomeAnimacaoAtaque = original.nomeAnimacaoAtaque;
            criatura.nomeAnimacaoEspecial = original.nomeAnimacaoEspecial;
            criatura.chanceDeEspecial = original.chanceDeEspecial;
            criatura.tempoEntreAtaques = original.tempoEntreAtaques;
            Object.Destroy(original);
        }
        criatura.enabled = true;
        criatura.rb = rb;
        criatura.animator = animator;
        if (animator != null) animator.enabled = true;
        criatura.podeLutar = false;
        criatura.velocidadeMovimento = altura * 1.2f / Mathf.Max(0.0001f, go.transform.lossyScale.x);
        criatura.distanciaParada = altura * 0.9f / Mathf.Max(0.0001f, go.transform.lossyScale.x);
        return criatura;
    }

    // ------------------------------------------------------------ Ajuste de tamanho
    // Multiplicador por espécie em Resources/Digimons/ajuste_tamanho.txt (1 = tamanho normal; 1.5 = 50% maior;
    // 0.5 = metade). Vale no mapa (parceiro e selvagens), na batalha e na Care.

    private static Dictionary<string, float> _ajustes;

    /// <summary>Multiplicador de tamanho da espécie (ajuste_tamanho.txt). 1 se não estiver lá.</summary>
    public static float Ajuste(DigimonBase ficha)
    {
        if (ficha == null) return 1f;
        if (_ajustes == null) CarregarAjustes();
        return _ajustes.TryGetValue(CatalogoDigimon.Normalizar(ficha.name), out var a) ? a : 1f;
    }

    private static void CarregarAjustes()
    {
        _ajustes = new Dictionary<string, float>();
        var texto = Resources.Load<TextAsset>("Digimons/ajuste_tamanho");
        if (texto == null) return;
        foreach (var linha in texto.text.Split('\n'))
        {
            var l = linha.Trim();
            if (l.Length == 0 || l.StartsWith("#")) continue;
            var i = l.IndexOf('=');
            if (i <= 0) continue;
            if (float.TryParse(l.Substring(i + 1).Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && a > 0f)
                _ajustes[CatalogoDigimon.Normalizar(l.Substring(0, i))] = a;
        }
    }

    // ------------------------------------------------------------ Animações quebradas
    // Em muitos FBX o clipe attack01 tem as curvas dos ossos duplicadas (um jogo em metros e outro em centímetros).
    // O Mecanim mistura as duas e o esqueleto "explode": o Digimon sobe/some ou fica gigante no golpe físico
    // (ex.: Heavy Strike do Dorumon). Os controllers afetados estão em Resources/Digimons/animacoes_quebradas.txt
    // (gerado no editor); na batalha o attack01 deles vira o attack02 (ou o special01, se não houver).

    private static HashSet<string> _quebrados;

    private static void TrocarAnimacoesQuebradas(Animator animator)
    {
        var ctrl = animator.runtimeAnimatorController;
        if (ctrl == null) return;
        if (_quebrados == null)
        {
            _quebrados = new HashSet<string>();
            var texto = Resources.Load<TextAsset>("Digimons/animacoes_quebradas");
            if (texto != null)
                foreach (var linha in texto.text.Split('\n'))
                {
                    var l = linha.Trim();
                    if (l.Length > 0 && !l.StartsWith("#")) _quebrados.Add(l);
                }
        }
        var nome = ctrl is AnimatorOverrideController o && o.runtimeAnimatorController != null ? o.runtimeAnimatorController.name : ctrl.name;
        if (!_quebrados.Contains(nome)) return;

        AnimationClip quebrado = null, substituto = null, especial = null;
        foreach (var c in ctrl.animationClips)
        {
            if (c == null) continue;
            if (c.name == "attack01") quebrado = c;
            else if (c.name == "attack02") substituto = c;
            else if (c.name == "special01") especial = c;
        }
        substituto = substituto != null ? substituto : especial;
        if (quebrado == null || substituto == null) return;
        var troca = new AnimatorOverrideController(ctrl);
        troca[quebrado] = substituto;
        animator.runtimeAnimatorController = troca;
    }

    /// <summary>Altura (em unidades do mundo) de um modelo, contando só as malhas.</summary>
    public static float Altura(GameObject go)
    {
        var b = Limites(go, out var ok);
        return ok ? b.size.y : 0f;
    }

    private static void Normalizar(GameObject go, Vector3 chao, float altura, bool animada = false)
    {
        var b = animada ? LimitesAnimados(go, out var ok) : Limites(go, out ok);
        if (!ok || !Finito(b) || b.size.y <= 0.00001f) return;
        go.transform.localScale *= altura / b.size.y;
        b = animada ? LimitesAnimados(go, out _) : Limites(go, out _);
        // Pés no chão, centralizado no ponto.
        go.transform.position += new Vector3(chao.x - b.center.x, chao.y - b.min.y, chao.z - b.center.z);
    }

    /// <summary>
    /// Limites da malha na pose atual da animação (a malha deformada pelos ossos), não da pose de importação.
    /// Os limites comuns de um SkinnedMeshRenderer vêm do arquivo e não mudam com a escala dos ossos.
    /// </summary>
    private static Bounds LimitesAnimados(GameObject go, out bool ok)
    {
        ok = false;
        var b = new Bounds();
        var malha = new Mesh();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is SkinnedMeshRenderer s && s.sharedMesh != null)
            {
                s.BakeMesh(malha, true);
                var m = s.transform.localToWorldMatrix; // testado no Unity: BakeMesh(true) + matriz completa = limites reais
                foreach (var v in malha.vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    if (float.IsNaN(p.x + p.y + p.z) || float.IsInfinity(p.x + p.y + p.z)) continue;
                    if (!ok) { b = new Bounds(p, Vector3.zero); ok = true; }
                    else b.Encapsulate(p);
                }
            }
            else if (r is MeshRenderer && Finito(r.bounds))
            {
                if (!ok) { b = r.bounds; ok = true; }
                else b.Encapsulate(r.bounds);
            }
        }
        Object.Destroy(malha);
        return b;
    }

    private static Bounds Limites(GameObject go, out bool ok)
    {
        ok = false;
        var b = new Bounds();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue; // ignora partículas e linhas
            if (!Finito(r.bounds)) continue; // malha com valores inválidos: não entra na medida
            if (!ok) { b = r.bounds; ok = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    /// <summary>Limites válidos (alguns modelos têm malhas ou ossos com valores NaN/infinitos).</summary>
    public static bool Finito(Bounds b) =>
        !float.IsNaN(b.center.x + b.center.y + b.center.z + b.size.x + b.size.y + b.size.z) &&
        !float.IsInfinity(b.center.x + b.center.y + b.center.z + b.size.x + b.size.y + b.size.z);
}
