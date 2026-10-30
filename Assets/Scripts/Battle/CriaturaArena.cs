using System;
using UnityEngine;

/// <summary>
/// Um Digimon lutando na arena: anda até o inimigo e ataca sozinho.
/// Com Configurar(...), usa a ficha do Digimon (vida, ataque, defesa, tipo e atributo).
/// Sem ficha, usa os valores fixos do Inspector (comportamento antigo).
/// </summary>
public class CriaturaArena : MonoBehaviour
{
    [Header("Configurações Básicas")]
    public float velocidadeMovimento = 3f;
    public float distanciaParada = 1f;

    [Header("Componentes")]
    public Renderer rendererCriatura;
    public Animator animator;
    public Rigidbody rb;

    [Header("Modelo Visual")]
    public Transform modeloVisual;

    [Header("Ataque")]
    public string nomeAnimacaoAtaque = "Attack";
    public string nomeAnimacaoEspecial = "Special";
    [Range(0f, 1f)] public float chanceDeEspecial = 0.2f;
    public float tempoEntreAtaques = 1.5f;

    [Header("Vida (sem ficha)")]
    public int vidaMaxima = 57;
    private int vidaAtual;

    [Header("HUD")]
    public string digimonName = "Digimon";
    public int level = 1;
    public string hpBarName = "Health_Bar_Player";
    private HPBar hpBar;

    [Header("Controle Manual de Combate")]
    public CriaturaArena inimigo;

    [HideInInspector] public bool podeLutar = false;

    /// <summary>Ficha e nível deste Digimon na batalha (null = valores fixos do Inspector).</summary>
    public Digimon Dados { get; private set; }
    public int VidaAtual => vidaAtual;
    public int VidaMaxima => vidaMaxima;
    public bool Vivo => vidaAtual > 0;

    /// <summary>Disparado quando este Digimon é derrotado.</summary>
    public event Action<CriaturaArena> Derrotado;
    /// <summary>Disparado quando recebe dano (quem recebeu, quanto).</summary>
    public event Action<CriaturaArena, int> RecebeuDano;

    private Transform alvo;
    private bool estaMovendo;
    private bool podeAtacar = true;
    private bool configurado;
    private bool temIsMoving, temAtaque, temEspecial;

    void Awake()
    {
        if (rendererCriatura == null) rendererCriatura = GetComponentInChildren<Renderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
        LerParametrosAnimator();
        vidaAtual = vidaMaxima;
    }

    /// <summary>Prepara a criatura com a ficha do Digimon. Chame antes da luta começar.</summary>
    public void Configurar(Digimon dados, CriaturaArena oponente, HPBar barra)
    {
        Dados = dados;
        inimigo = oponente;
        alvo = oponente != null ? oponente.transform : null;
        hpBar = barra;
        configurado = true;
        if (dados != null)
        {
            vidaMaxima = dados.MaxHp;
            digimonName = dados.Base.Name.Trim();
            level = dados.Level;
        }
        vidaAtual = vidaMaxima;
        if (hpBar != null)
        {
            hpBar.Initialize(digimonName, level, vidaMaxima);
            hpBar.SetHP(vidaAtual);
        }
        LerParametrosAnimator();
    }

    /// <summary>Aumenta a vida máxima e atual (ex.: Bandage usada antes da batalha).</summary>
    public void AumentarVida(float fator)
    {
        vidaMaxima = Mathf.RoundToInt(vidaMaxima * fator);
        vidaAtual = vidaMaxima;
        if (hpBar != null)
        {
            hpBar.SetMaxHP(vidaMaxima);
            hpBar.SetHP(vidaAtual);
        }
    }

    void Start()
    {
        if (configurado) return; // já preparado por Configurar(...)

        vidaAtual = vidaMaxima;
        GameObject barObject = GameObject.Find(hpBarName);
        if (barObject != null)
        {
            hpBar = barObject.GetComponent<HPBar>();
            if (hpBar != null)
            {
                hpBar.Initialize(digimonName, level, vidaMaxima);
                hpBar.SetHP(vidaAtual);
            }
        }

        if (inimigo != null) alvo = inimigo.transform;
        else Debug.LogWarning($"{gameObject.name} não tem inimigo atribuído!");
    }

    private void LerParametrosAnimator()
    {
        temIsMoving = temAtaque = temEspecial = false;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.name == "isMoving") temIsMoving = true;
            if (p.name == nomeAnimacaoAtaque) temAtaque = true;
            if (p.name == nomeAnimacaoEspecial) temEspecial = true;
        }
    }

    void Update()
    {
        if (animator != null && temIsMoving)
            animator.SetBool("isMoving", estaMovendo);
    }

    void FixedUpdate()
    {
        var inimigoVivo = inimigo != null && inimigo.Vivo;
        if (!podeLutar || alvo == null || !inimigoVivo || vidaAtual <= 0)
        {
            estaMovendo = false;
            if (rb != null && !rb.isKinematic) rb.linearVelocity = Vector3.zero;
            return;
        }

        Vector3 direcao = alvo.position - transform.position;
        direcao.y = 0;
        float distancia = direcao.magnitude;
        direcao = distancia > 0.0001f ? direcao / distancia : Vector3.zero;
        var escala = transform.lossyScale.x; // distâncias acompanham o tamanho da arena (AR)

        if (distancia > distanciaParada * escala)
        {
            var passo = direcao * velocidadeMovimento * escala;
            if (rb != null && rb.isKinematic) rb.MovePosition(rb.position + passo * Time.fixedDeltaTime);
            else if (rb != null) rb.linearVelocity = passo;
            else transform.position += passo * Time.fixedDeltaTime;
            estaMovendo = true;
            transform.LookAt(new Vector3(alvo.position.x, transform.position.y, alvo.position.z));
        }
        else
        {
            if (rb != null && !rb.isKinematic) rb.linearVelocity = Vector3.zero;
            estaMovendo = false;
            if (podeAtacar) Atacar();
        }
    }

    void LateUpdate()
    {
        if (modeloVisual != null)
            modeloVisual.position = transform.position;
    }

    void Atacar()
    {
        podeAtacar = false;

        var especial = UnityEngine.Random.value < chanceDeEspecial && !string.IsNullOrEmpty(nomeAnimacaoEspecial);
        var anim = especial ? nomeAnimacaoEspecial : nomeAnimacaoAtaque;
        if (animator != null && (especial ? temEspecial : temAtaque))
            animator.SetTrigger(anim);

        int dano = Dados != null && inimigo != null && inimigo.Dados != null
            ? CalculoDano.Calcular(Dados, inimigo.Dados, especial)
            : (especial ? 20 : 10);

        if (inimigo != null) inimigo.ReceberDano(dano);

        Invoke(nameof(ResetarAtaque), tempoEntreAtaques);
    }

    void ResetarAtaque()
    {
        podeAtacar = true;
    }

    public void ReceberDano(int dano)
    {
        if (vidaAtual <= 0) return;
        vidaAtual = Mathf.Max(0, vidaAtual - dano);
        if (hpBar != null) hpBar.SetHP(vidaAtual);
        RecebeuDano?.Invoke(this, dano);
        if (vidaAtual <= 0) Morrer();
    }

    void Morrer()
    {
        podeLutar = false;
        Derrotado?.Invoke(this);
        // Some com uma pequena animação de encolher (antes era destruído na hora).
        StartCoroutine(Sumir());
    }

    private System.Collections.IEnumerator Sumir()
    {
        var inicial = transform.localScale;
        for (float t = 0; t < 1f; t += Time.deltaTime * 2.5f)
        {
            transform.localScale = Vector3.Lerp(inicial, Vector3.zero, t);
            yield return null;
        }
        Destroy(gameObject);
    }
}
