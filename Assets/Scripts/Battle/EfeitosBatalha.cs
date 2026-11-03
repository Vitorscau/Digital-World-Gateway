using System.Collections;
using UnityEngine;

/// <summary>
/// Efeitos visuais dos golpes na arena, feitos com placas que sempre olham para a câmera (brilhos e anéis):
/// carga de energia antes do disparo, explosão no impacto, escudo da guarda, aura do suporte e poeira do passo.
/// Não usa a escala dos Digimon: cada efeito é um objeto solto na cena, com o tamanho em unidades do mundo.
/// </summary>
public class EfeitosBatalha : MonoBehaviour
{
    private static EfeitosBatalha _instancia;

    private static EfeitosBatalha I
    {
        get
        {
            if (_instancia == null) _instancia = new GameObject("EfeitosBatalha").AddComponent<EfeitosBatalha>();
            return _instancia;
        }
    }

    // ------------------------------------------------------------ Partículas e linhas (ataques especiais)

    private static Material _materialParticula;

    /// <summary>
    /// Material de partícula aditivo (brilho de energia), com o shader de partículas do URP — o mesmo do Ripple
    /// dos Digimon, então ele já entra no build. Usa a cor de cada partícula / linha.
    /// </summary>
    public static Material MaterialParticula()
    {
        if (_materialParticula != null) return _materialParticula;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
        if (shader == null) return _materialParticula = Formas.MaterialSemLuz(Color.white, Formas.Brilho().texture, true);
        var m = new Material(shader) { name = "ParticulaBatalha" };
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", Formas.Brilho().texture);
        m.mainTexture = Formas.Brilho().texture;
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f); // aditivo
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return _materialParticula = m;
    }

    /// <summary>Configuração de um sistema de partículas criado por código.</summary>
    public struct Particulas
    {
        public Color Cor;
        public float Tamanho;           // tamanho de cada partícula (mundo)
        public float Vida;              // segundos
        public float Velocidade;        // mundo/s
        public float Taxa;              // partículas por segundo (0 = só a rajada)
        public int Rajada;              // partículas de uma vez no começo
        public ParticleSystemShapeType Forma;
        public float Raio;              // raio da forma (esfera/círculo/cone)
        public float Angulo;            // abertura do cone (graus)
        public float Duracao;           // quanto tempo emite
        public float Gravidade;         // >0 cai, <0 sobe
        public bool Crescer;            // cresce em vez de encolher ao longo da vida
        public float Ruido;             // tremidinha (fogo, faíscas)
    }

    /// <summary>Cria, configura e solta um sistema de partículas (some sozinho quando termina).</summary>
    public static ParticleSystem Emitir(string nome, Vector3 pos, Quaternion rot, Particulas p, Transform seguir = null)
    {
        var go = new GameObject("Particulas_" + nome);
        go.transform.SetPositionAndRotation(pos, rot);
        if (seguir != null) go.transform.SetParent(seguir, true);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = Mathf.Max(0.05f, p.Duracao);
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(p.Vida * 0.6f, p.Vida);
        main.startSpeed = new ParticleSystem.MinMaxCurve(p.Velocidade * 0.5f, p.Velocidade);
        main.startSize = new ParticleSystem.MinMaxCurve(p.Tamanho * 0.6f, p.Tamanho);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var claro = Color.Lerp(p.Cor, Color.white, 0.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(p.Cor, claro);
        main.gravityModifier = p.Gravidade;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emissao = ps.emission;
        emissao.rateOverTime = p.Taxa;
        if (p.Rajada > 0) emissao.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)p.Rajada) });

        var forma = ps.shape;
        forma.shapeType = p.Forma;
        forma.radius = Mathf.Max(0.0001f, p.Raio);
        forma.angle = p.Angulo;

        var cor = ps.colorOverLifetime;
        cor.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        cor.color = g;

        var tamanho = ps.sizeOverLifetime;
        tamanho.enabled = true;
        tamanho.size = new ParticleSystem.MinMaxCurve(1f, p.Crescer ? AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f) : AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        if (p.Ruido > 0f)
        {
            var ruido = ps.noise;
            ruido.enabled = true;
            ruido.strength = p.Ruido;
            ruido.frequency = 1.5f;
        }

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = MaterialParticula();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        return ps;
    }

    /// <summary>Linha brilhante (raio, relâmpago, corte). Some sozinha depois de <paramref name="duracao"/>.</summary>
    public static LineRenderer Linha(string nome, Vector3[] pontos, float largura, Color cor, float duracao)
    {
        var go = new GameObject("Linha_" + nome);
        var l = go.AddComponent<LineRenderer>();
        l.sharedMaterial = MaterialParticula();
        l.positionCount = pontos.Length;
        l.SetPositions(pontos);
        l.widthMultiplier = largura;
        l.numCapVertices = 4;
        l.startColor = l.endColor = cor;
        l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        l.receiveShadows = false;
        l.alignment = LineAlignment.View;
        I.StartCoroutine(I.SumirLinha(l, largura, cor, duracao));
        return l;
    }

    private IEnumerator SumirLinha(LineRenderer l, float largura, Color cor, float duracao)
    {
        for (float k = 0; k < duracao && l != null; k += Time.deltaTime)
        {
            var p = k / duracao;
            l.widthMultiplier = largura * (p < 0.15f ? p / 0.15f : 1f - (p - 0.15f) / 0.85f * 0.7f);
            var c = cor;
            c.a = cor.a * (1f - p * p);
            l.startColor = l.endColor = c;
            yield return null;
        }
        if (l != null) Destroy(l.gameObject);
    }

    /// <summary>Roda uma rotina de efeito que não depende de nenhum Digimon (continua mesmo se ele sumir).</summary>
    public static void Rodar(IEnumerator rotina) => I.StartCoroutine(rotina);

    /// <summary>Placa (brilho, anel, raios) solta na cena, virada para a câmera.</summary>
    public static GameObject PlacaSolta(Sprite sprite, Color cor, Vector3 pos, float tamanho, bool aditivo = false) =>
        Placa("Placa", sprite, cor, pos, tamanho, aditivo);

    /// <summary>Faz uma placa abrir e sumir (onda, clarão).</summary>
    public static void AbrirESumirPlaca(GameObject go, float de, float para, float duracao) => I.StartCoroutine(I.AbrirESumir(go, de, para, duracao));

    // ------------------------------------------------------------ Efeitos

    /// <summary>Energia juntando no ponto do disparo (cresce enquanto o Digimon se prepara). Destrua ao disparar.</summary>
    public static GameObject Carga(Transform seguir, Vector3 deslocamento, float tamanho, Color cor, float duracao)
    {
        var brilho = Placa("Carga", Formas.Brilho(), cor, seguir.position + seguir.rotation * deslocamento, 0.01f, aditivo: true);
        I.StartCoroutine(I.Crescer(brilho.transform, seguir, deslocamento, tamanho, duracao));
        // Faíscas girando em volta.
        var anel = Placa("CargaAnel", Formas.Anel(), new Color(cor.r, cor.g, cor.b, 0.8f), brilho.transform.position, tamanho * 1.6f, aditivo: true);
        // Miolo branco no centro da carga.
        var miolo = Placa("CargaMiolo", Formas.Brilho(), new Color(1f, 1f, 1f, 0.9f), brilho.transform.position, tamanho * 0.5f, aditivo: true);
        miolo.transform.SetParent(brilho.transform, true);
        anel.transform.SetParent(brilho.transform, true);
        I.StartCoroutine(I.Encolher(anel.transform, tamanho * 1.6f, tamanho * 0.3f, duracao));
        return brilho;
    }

    /// <summary>Explosão no impacto: clarão + anel que se abre. Crítico e golpe forte são maiores.</summary>
    public static void Impacto(Vector3 ponto, float tamanho, Color cor, bool forte)
    {
        var f = forte ? 1.6f : 1f;
        var clarao = Placa("Clarao", Formas.Brilho(), Color.Lerp(cor, Color.white, 0.6f), ponto, tamanho * f, aditivo: true);
        I.StartCoroutine(I.AbrirESumir(clarao, tamanho * f, tamanho * 1.8f * f, 0.22f));
        var anel = Placa("Onda", Formas.Anel(), cor, ponto, tamanho * 0.4f, aditivo: true);
        I.StartCoroutine(I.AbrirESumir(anel, tamanho * 0.4f, tamanho * 2.6f * f, 0.35f));
        if (forte)
        {
            var raios = Placa("Raios", Formas.Raios(), new Color(1f, 1f, 1f, 0.9f), ponto, tamanho, aditivo: true);
            I.StartCoroutine(I.AbrirESumir(raios, tamanho, tamanho * 2.4f, 0.3f, girar: 220f));
        }
    }

    /// <summary>Bolha de escudo enquanto ele se defende.</summary>
    public static void Escudo(Transform seguir, Vector3 deslocamento, float tamanho, float duracao)
    {
        var escudo = Placa("Escudo", Formas.Anel(), new Color(0.55f, 0.85f, 1f, 0.9f), seguir.position + deslocamento, tamanho);
        var miolo = Placa("EscudoMiolo", Formas.Brilho(), new Color(0.55f, 0.85f, 1f, 0.35f), seguir.position + deslocamento, tamanho);
        miolo.transform.SetParent(escudo.transform, true);
        I.StartCoroutine(I.Seguir(escudo, seguir, deslocamento, tamanho, duracao));
    }

    /// <summary>Aura subindo em volta do Digimon (técnica de suporte, ex.: Attack Charge).</summary>
    public static void Aura(Transform seguir, float altura, Color cor)
    {
        for (var i = 0; i < 3; i++)
            I.StartCoroutine(I.AnelSubindo(seguir, altura, cor, i * 0.15f));
    }

    /// <summary>Poeira no chão (investida e passo rápido).</summary>
    public static void Poeira(Vector3 pe, float tamanho)
    {
        var p = Placa("Poeira", Formas.Brilho(), new Color(0.85f, 0.8f, 0.7f, 0.55f), pe + Vector3.up * tamanho * 0.2f, tamanho * 0.5f);
        I.StartCoroutine(I.AbrirESumir(p, tamanho * 0.5f, tamanho * 1.4f, 0.4f));
    }

    // ------------------------------------------------------------ Peças

    private static GameObject Placa(string nome, Sprite sprite, Color cor, Vector3 pos, float tamanho, bool aditivo = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Efeito_" + nome;
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        if (aditivo)
        {
            // Brilho de energia: soma luz (fica claro e "aceso" em vez de uma mancha de cor).
            var m = new Material(MaterialParticula());
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", sprite.texture);
            m.mainTexture = sprite.texture;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cor);
            m.color = cor;
            r.material = m;
        }
        else r.material = Formas.MaterialSemLuz(cor, sprite.texture, true);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * Mathf.Max(0.0001f, tamanho);
        go.AddComponent<OlharCamera>();
        return go;
    }

    private static void Alfa(GameObject go, float a)
    {
        var m = go.GetComponent<Renderer>().material;
        var c = m.color;
        c.a = a;
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
    }

    private IEnumerator Crescer(Transform t, Transform seguir, Vector3 deslocamento, float tamanho, float duracao)
    {
        for (float k = 0; k < duracao && t != null; k += Time.deltaTime)
        {
            if (seguir != null) t.position = seguir.position + seguir.rotation * deslocamento;
            var pulso = 1f + Mathf.Sin(Time.time * 40f) * 0.08f;
            t.localScale = Vector3.one * Mathf.Lerp(tamanho * 0.15f, tamanho, k / duracao) * pulso;
            yield return null;
        }
    }

    private IEnumerator Encolher(Transform t, float de, float para, float duracao)
    {
        for (float k = 0; k < duracao && t != null; k += Time.deltaTime)
        {
            t.localScale = Vector3.one * Mathf.Lerp(de, para, k / duracao);
            yield return null;
        }
    }

    private IEnumerator AbrirESumir(GameObject go, float de, float para, float duracao, float girar = 0f)
    {
        var a0 = go.GetComponent<Renderer>().material.color.a;
        for (float k = 0; k < duracao && go != null; k += Time.deltaTime)
        {
            var p = k / duracao;
            go.transform.localScale = Vector3.one * Mathf.Lerp(de, para, 1f - (1f - p) * (1f - p));
            if (girar != 0f) go.GetComponent<OlharCamera>().giro += girar * Time.deltaTime;
            Alfa(go, a0 * (1f - p));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private IEnumerator Seguir(GameObject go, Transform seguir, Vector3 deslocamento, float tamanho, float duracao)
    {
        for (float k = 0; k < duracao && go != null; k += Time.deltaTime)
        {
            if (seguir != null) go.transform.position = seguir.position + deslocamento;
            var entrada = Mathf.Clamp01(k / 0.1f);
            var saida = Mathf.Clamp01((duracao - k) / 0.15f);
            go.transform.localScale = Vector3.one * tamanho * Mathf.Lerp(0.7f, 1f, entrada);
            Alfa(go, 0.9f * Mathf.Min(entrada, saida));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private IEnumerator AnelSubindo(Transform seguir, float altura, Color cor, float atraso)
    {
        yield return new WaitForSeconds(atraso);
        if (seguir == null) yield break;
        var anel = Placa("Aura", Formas.Anel(), cor, seguir.position, altura * 0.9f);
        Destroy(anel.GetComponent<OlharCamera>());
        anel.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // deitado no chão, subindo
        const float duracao = 0.6f;
        for (float k = 0; k < duracao && anel != null; k += Time.deltaTime)
        {
            var p = k / duracao;
            if (seguir != null) anel.transform.position = seguir.position + Vector3.up * altura * p;
            anel.transform.localScale = Vector3.one * altura * Mathf.Lerp(0.9f, 0.5f, p);
            Alfa(anel, 0.85f * (1f - p));
            yield return null;
        }
        if (anel != null) Destroy(anel);
    }
}
