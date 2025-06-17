using UnityEngine;

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

    [Header("Vida")]
    public int vidaMaxima = 57;
    private int vidaAtual;

    [Header("HUD")]
    public string digimonName = "Digimon";
    public int level = 1;
    public string hpBarName = "Health_Bar_Player";
    private HPBar hpBar;

    [Header("Controle Manual de Combate")]
    public CriaturaArena inimigo;

    [HideInInspector] public bool podeLutar = false; // 👈 Novo controle

    private Transform alvo;
    private bool estaMovendo;
    private bool podeAtacar = true;

    void Awake()
    {
        if (rendererCriatura == null) rendererCriatura = GetComponentInChildren<Renderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.isKinematic = false;
        }
    }

    void Start()
    {
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

        if (inimigo != null)
        {
            alvo = inimigo.transform;
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} não tem inimigo atribuído!");
        }
    }

    void Update()
    {
        if (animator != null)
            animator.SetBool("isMoving", estaMovendo);
    }

    void FixedUpdate()
    {
        if (!podeLutar || alvo == null || rb == null || vidaAtual <= 0) return;

        Vector3 direcao = (alvo.position - transform.position).normalized;
        direcao.y = 0;
        float distancia = Vector3.Distance(transform.position, alvo.position);

        if (distancia > distanciaParada)
        {
            rb.velocity = direcao * velocidadeMovimento;
            estaMovendo = true;
            transform.LookAt(new Vector3(alvo.position.x, transform.position.y, alvo.position.z));
        }
        else
        {
            rb.velocity = Vector3.zero;
            estaMovendo = false;

            if (podeAtacar)
            {
                Atacar();
            }
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

        float sorteio = Random.value;
        string anim = (sorteio < chanceDeEspecial && !string.IsNullOrEmpty(nomeAnimacaoEspecial))
            ? nomeAnimacaoEspecial
            : nomeAnimacaoAtaque;

        if (animator != null)
        {
            animator.SetTrigger(anim);
        }

        int dano = (anim == nomeAnimacaoEspecial) ? 20 : 10;

        if (inimigo != null)
        {
            Debug.Log($"{gameObject.name} atacou {inimigo.gameObject.name} causando {dano} de dano");
            inimigo.ReceberDano(dano);
        }

        Invoke(nameof(ResetarAtaque), tempoEntreAtaques);
    }

    void ResetarAtaque()
    {
        podeAtacar = true;
    }

    public void ReceberDano(int dano)
    {
        vidaAtual -= dano;
        vidaAtual = Mathf.Max(0, vidaAtual);

        Debug.Log($"{gameObject.name} recebeu {dano} de dano. HP atual: {vidaAtual}");

        if (hpBar != null)
        {
            hpBar.SetHP(vidaAtual);
        }

        if (vidaAtual <= 0)
        {
            Morrer();
        }
    }

    void Morrer()
    {
        Debug.Log($"{gameObject.name} morreu!");
        Destroy(gameObject);
    }
}
