using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Um Digimon lutando sozinho na arena, no estilo Digimon World Championship: o jogador só assiste.
///
/// - Status: HP, MP, Offense, Defense, Speed e Brains (veja <see cref="Digimon"/>).
/// - Uma barra de ação enche com o tempo (mais rápido quem tem mais Speed). Cheia, ele usa uma técnica.
/// - Técnicas físicas são de perto; mágicas são disparos de longe que gastam MP (o MP volta devagar).
/// - Brains decide a qualidade das escolhas: técnica com vantagem de tipo, poupar MP, acabar com o inimigo,
///   ficar na distância certa e se defender quando vê um golpe vindo. Um Digimon pouco esperto age meio ao acaso.
/// - A personalidade dá o estilo: Fighter parte para cima, Defender se defende mais, Brainy prefere magia de longe,
///   Nimble desvia e circula, Durable aguenta golpes sem cambalear, Lively age mais vezes, Builder usa suporte,
///   Searcher acerta mais críticos.
/// - Táticas (quanto mais Brains, mais usa): espera o inimigo baixar a guarda, aproveita quando ele cambaleia,
///   finaliza com a técnica mais barata que resolve, guarda MP para o golpe final, recua quando está mal,
///   esquiva disparos para o lado e dá um passo para trás contra golpes de perto. Os disparos vão em linha reta
///   (mirando onde o alvo vai estar), então dá para escapar deles andando.
///
/// Com Configurar(...), usa a ficha do Digimon. Sem ficha, luta com os valores do Inspector.
/// </summary>
public partial class CriaturaArena : MonoBehaviour
{
    [Header("Configurações Básicas")]
    public float velocidadeMovimento = 3f;
    [Tooltip("Distância de um golpe corpo a corpo (unidades do modelo).")]
    public float distanciaParada = 1f;

    [Header("Componentes")]
    public Renderer rendererCriatura;
    public Animator animator;
    public Rigidbody rb;

    [Header("Modelo Visual")]
    public Transform modeloVisual;

    [Header("Animações (triggers do Animator)")]
    public string nomeAnimacaoAtaque = "Attack";
    public string nomeAnimacaoEspecial = "Special";
    [Range(0f, 1f)] public float chanceDeEspecial = 0.2f;
    [Tooltip("Tempo médio entre ações, com Speed igual ao do inimigo.")]
    public float tempoEntreAtaques = 1.6f;

    [Header("Vida (sem ficha)")]
    public int vidaMaxima = 57;
    private int vidaAtual;

    [Header("HUD")]
    public string digimonName = "Digimon";
    public int level = 1;
    public string hpBarName = "Health_Bar_Player";
    private HPBar hpBar;

    [Header("Combate")]
    public CriaturaArena inimigo;

    [HideInInspector] public bool podeLutar = false;
    /// <summary>É o parceiro do jogador (dano em vermelho; no selvagem, amarelo).</summary>
    [HideInInspector] public bool doJogador;

    /// <summary>Ficha e nível deste Digimon na batalha (null = valores fixos do Inspector).</summary>
    public Digimon Dados { get; private set; }
    public int VidaAtual => vidaAtual;
    public int VidaMaxima => vidaMaxima;
    public int MPAtual => Mathf.FloorToInt(_mp);
    public int MPMaximo => _mpMax;
    public bool Vivo => vidaAtual > 0;

    /// <summary>Disparado quando este Digimon é derrotado.</summary>
    public event Action<CriaturaArena> Derrotado;
    /// <summary>Disparado quando recebe dano (quem recebeu, quanto).</summary>
    public event Action<CriaturaArena, int> RecebeuDano;
    /// <summary>Disparado quando usa uma técnica com nome (quem usou, técnica).</summary>
    public event Action<CriaturaArena, Tecnica> UsouTecnica;

    private enum Estado { Livre, Agindo, Defendendo, Esquivando, Atordoado, Caido }

    private Estado _estado = Estado.Livre;
    private bool configurado;
    private bool temIsMoving;
    private readonly HashSet<string> _triggers = new HashSet<string>();

    private List<Tecnica> _repertorio;
    private Tecnica _plano;
    private float _mp;
    private int _mpMax = 10;
    private float _barraAcao;
    private float _tempoSemAlcance;
    private float _bonusAtaque = 1f, _fimBonusAtaque;
    private float _fimFirmeza; // não cambaleia até esse tempo
    private float _fimDefesa;
    private int _ladoCirculo = 1;
    private float _trocaLado;
    private Vector3 _centroArena;
    private float _raioArena;
    private bool _arenaDefinida;
    private bool estaMovendo;
    private float _altura = 1f;
    private readonly List<GameObject> _disparos = new List<GameObject>();
    private Tecnica _ultima;          // última técnica usada (evita repetir sempre a mesma)
    private float _esperandoGuarda;   // há quanto tempo segura o golpe esperando a guarda do inimigo cair
    private float _fugirAte;          // inimigo assustado com um golpe forte: foge até esse tempo
    private float _inicioFuga;
    private bool Fugindo => Time.time < _fugirAte;
    private bool _noAr;               // arremessado por um golpe forte (voando, quicando ou caído)
    private int _estadoBase;          // estado inicial do Animator (idle), para levantar depois de cair
    private float _chaoY;             // altura do chão da arena (onde ele cai depois de voar)
    private float _recuarAte;         // depois de atacar: recua e circula até esse tempo, depois volta a atacar
    private float _empurradoAte;      // sendo empurrado por um golpe: o empurrão manda no movimento
    private bool _andandoDeCostas;    // o Mover está levando ele para longe do inimigo (vira de costas)
    private Vector3 _direcaoAndando;
    private Vector3 _posAnterior;
    private Vector3 _velocidade;      // para quem atira mirar onde ele vai estar

    // ------------------------------------------------------------ Preparação

    void Awake()
    {
        if (rendererCriatura == null) rendererCriatura = GetComponentInChildren<Renderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null) rb.constraints = RigidbodyConstraints.FreezeRotation;
        LerParametrosAnimator();
        vidaAtual = vidaMaxima;
    }

    /// <summary>Prepara a criatura com a ficha do Digimon. Chame antes da luta começar.</summary>
    public void Configurar(Digimon dados, CriaturaArena oponente, HPBar barra)
    {
        Dados = dados;
        inimigo = oponente;
        hpBar = barra;
        configurado = true;
        if (dados != null)
        {
            vidaMaxima = dados.MaxHp;
            digimonName = dados.Base.Name.Trim();
            level = dados.Level;
            _mpMax = Mathf.Max(1, dados.MaxMp);
            _repertorio = Tecnica.Repertorio(dados);
        }
        vidaAtual = vidaMaxima;
        _mp = _mpMax;
        if (hpBar != null)
        {
            hpBar.Initialize(digimonName, level, vidaMaxima);
            hpBar.SetHP(vidaAtual);
        }
        LerParametrosAnimator();
        _altura = AlturaModelo();
        if (animator != null && animator.isActiveAndEnabled) _estadoBase = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
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
        var barObject = GameObject.Find(hpBarName);
        if (barObject != null)
        {
            hpBar = barObject.GetComponent<HPBar>();
            if (hpBar != null)
            {
                hpBar.Initialize(digimonName, level, vidaMaxima);
                hpBar.SetHP(vidaAtual);
            }
        }
        if (inimigo == null) Debug.LogWarning($"{gameObject.name} não tem inimigo atribuído!");
        _altura = AlturaModelo();
    }

    private void LerParametrosAnimator()
    {
        temIsMoving = false;
        _triggers.Clear();
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.name == "isMoving") temIsMoving = true;
            if (p.type == AnimatorControllerParameterType.Trigger) _triggers.Add(p.name);
        }
    }

    private void Animar(string trigger)
    {
        if (animator != null && animator.isActiveAndEnabled && _triggers.Contains(trigger)) animator.SetTrigger(trigger);
    }

    // ------------------------------------------------------------ Atalhos de status

    private float Escala => Mathf.Max(0.0001f, transform.lossyScale.x);
    /// <summary>Alcance de um golpe de perto, em unidades do mundo.</summary>
    private float Perto => distanciaParada * Escala;
    private float Esperteza => Dados != null ? Dados.Esperteza : 0.5f;
    private DigimonPersonalities Personalidade => Dados != null ? Dados.Personalidade : DigimonPersonalities.Fighter;
    private bool InimigoVivo => inimigo != null && inimigo.Vivo;

    private float Velocidade()
    {
        var rel = Dados != null && inimigo != null && inimigo.Dados != null
            ? CalculoDano.Relativo(Dados.Speed, inimigo.Dados.Speed) : 0.5f;
        var v = velocidadeMovimento * Escala * Mathf.Lerp(0.75f, 1.35f, rel);
        if (Personalidade == DigimonPersonalities.Nimble) v *= 1.15f;
        return v;
    }

    /// <summary>Segundos entre uma ação e outra: quem tem mais Speed age mais vezes.</summary>
    private float IntervaloAcao()
    {
        var t = tempoEntreAtaques;
        if (Dados != null && inimigo != null && inimigo.Dados != null)
            t *= Mathf.Clamp((inimigo.Dados.Speed + 10f) / (Dados.Speed + 10f), 0.6f, 1.6f);
        if (Personalidade == DigimonPersonalities.Lively) t *= 0.85f;
        return t;
    }

    // ------------------------------------------------------------ Laço principal

    void Update()
    {
        if (animator != null && temIsMoving) animator.SetBool("isMoving", estaMovendo && _estado == Estado.Livre);
        if (!podeLutar || !Vivo) return;

        // MP volta devagar (mais rápido para quem é esperto).
        _mp = Mathf.Min(_mpMax, _mp + _mpMax * (0.02f + 0.02f * Esperteza) * Time.deltaTime);
        if (_estado == Estado.Defendendo && Time.time > _fimDefesa) _estado = Estado.Livre;
        if (_estado == Estado.Livre && InimigoVivo) _barraAcao += Time.deltaTime / IntervaloAcao();
    }

    void FixedUpdate()
    {
        estaMovendo = false;
        if (!podeLutar || !Vivo || !InimigoVivo)
        {
            if (rb != null && !rb.isKinematic) rb.linearVelocity = Vector3.zero;
            return;
        }
        if (!_arenaDefinida) { DefinirArena(); _posAnterior = transform.position; }
        _velocidade = (transform.position - _posAnterior) / Time.fixedDeltaTime;
        _posAnterior = transform.position;
        if (_noAr || Time.time < _empurradoAte) return; // voando/caído/sendo empurrado: não anda por conta própria
        if (_estado == Estado.Livre && Fugindo)
        {
            Fugir();
            return;
        }
        // Andando para trás (recuando): vira de costas e olha para onde vai; senão, encara o inimigo.
        var deCostas = _andandoDeCostas && _estado == Estado.Livre;
        _andandoDeCostas = false; // o Mover deste quadro decide de novo
        Olhar(deCostas ? transform.position + _direcaoAndando : inimigo.transform.position);
        if (_estado != Estado.Livre) return;

        // Inimigo cambaleando: um Digimon esperto não espera a barra encher toda para aproveitar.
        if (inimigo._estado == Estado.Atordoado && _barraAcao >= 0.8f - 0.2f * Esperteza && Esperteza > 0.45f)
            _barraAcao = 1f;

        if (_plano == null || (_plano.CustoMP > _mp)) _plano = EscolherTecnica();
        var distancia = DistanciaAte(inimigo);
        var alvo = DistanciaDesejada(_plano);

        // Acabou de atacar: recua para uma distância segura e circula; só depois volta a atacar.
        if (Time.time < _recuarAte)
        {
            Mover(distancia, Mathf.Max(alvo, Perto * 3f));
            return;
        }

        // Barra cheia e no alcance: age.
        if (_barraAcao >= 1f)
        {
            // Inimigo em guarda: o esperto segura a técnica grande e circula até a guarda cair (no máximo um pouco).
            if (inimigo._estado == Estado.Defendendo && !_plano.Basica && _plano.Tipo != TipoTecnica.Suporte &&
                _esperandoGuarda < 0.4f + 1.2f * Esperteza && Esperteza > 0.35f)
            {
                _esperandoGuarda += Time.fixedDeltaTime;
                Mover(distancia, Mathf.Max(alvo, Perto * 1.6f));
                return;
            }
            _esperandoGuarda = 0f;
            var noAlcance = _plano.ADistancia || _plano.Tipo == TipoTecnica.Suporte
                ? distancia <= Perto * 6f
                : distancia <= Perto * 1.15f;
            if (noAlcance)
            {
                StartCoroutine(Agir(_plano));
                return;
            }
            // Demorando para chegar: um Digimon esperto troca por algo que alcance.
            _tempoSemAlcance += Time.fixedDeltaTime;
            if (_tempoSemAlcance > 1.2f + (1f - Esperteza) * 2f)
            {
                _tempoSemAlcance = 0f;
                _plano = EscolherTecnica(preferirDistancia: true);
            }
        }

        Mover(distancia, alvo);
    }

    /// <summary>
    /// Foge do inimigo depois de levar um golpe forte: corre para longe em zigue-zague e, na borda da arena,
    /// corre ao longo dela. Se a barra encher e tiver uma técnica de longe, ataca de costas para a parede.
    /// </summary>
    private void Fugir()
    {
        var distancia = DistanciaAte(inimigo);
        // Só contra-ataca depois de correr um pouco e abrir distância (senão a fuga nem aparece).
        if (_barraAcao >= 1f && Time.time - _inicioFuga > 0.9f && distancia >= Perto * 2.5f)
        {
            if (_plano == null || _plano.CustoMP > _mp || !_plano.ADistancia) _plano = EscolherTecnica(preferirDistancia: true);
            if (_plano.ADistancia && distancia <= Perto * 6f)
            {
                _fugirAte = 0f;
                Olhar(inimigo.transform.position);
                StartCoroutine(Agir(_plano));
                return;
            }
        }
        if (Time.time > _trocaLado)
        {
            _ladoCirculo = Random.value < 0.5f ? -1 : 1;
            _trocaLado = Time.time + Random.Range(0.5f, 1.1f); // zigue-zague mais curto na fuga
        }
        var deLa = transform.position - inimigo.transform.position;
        deLa.y = 0;
        deLa = deLa.sqrMagnitude > 1e-8f ? deLa.normalized : -transform.forward;
        var lateral = Vector3.Cross(Vector3.up, deLa) * _ladoCirculo;
        var movimento = deLa + lateral * 0.5f;
        var doCentro = transform.position - _centroArena;
        doCentro.y = 0;
        if (doCentro.magnitude > _raioArena * 0.85f) movimento = lateral + deLa * 0.15f; // encurralado: corre pela borda
        var passo = movimento.normalized * Velocidade() * 1.3f * Time.fixedDeltaTime;
        Olhar(transform.position + movimento);
        MoverPara(transform.position + passo);
        estaMovendo = true;
    }

    /// <summary>Inimigo levou um golpe forte: talvez fuja por um tempo (depende da personalidade e da vida).</summary>
    private void TalvezFugir(float peso)
    {
        if (doJogador || !Vivo) return;
        var chance = 0.65f + 0.3f * peso;
        switch (Personalidade)
        {
            case DigimonPersonalities.Fighter: chance *= 0.6f; break; // brigão foge menos, mas ainda foge
            case DigimonPersonalities.Durable: chance *= 0.7f; break;
            case DigimonPersonalities.Nimble:
            case DigimonPersonalities.Brainy: chance *= 1.25f; break;
        }
        if (vidaAtual < vidaMaxima * 0.35f) chance += 0.25f;
        if (Random.value > chance) return;
        if (!Fugindo) _inicioFuga = Time.time;
        _fugirAte =Mathf.Max(_fugirAte, Time.time + Random.Range(1.2f, 2.2f) * Mathf.Lerp(1f, 1.5f, peso));
        _plano = null;
    }

    private void DefinirArena()
    {
        _arenaDefinida = true;
        _chaoY = transform.position.y;
        _centroArena = (transform.position + inimigo.transform.position) * 0.5f;
        _raioArena = Mathf.Max(DistanciaAte(inimigo) * 0.9f, Perto * 5f); // espaço para voar longe nos golpes fortes
    }

    private float DistanciaAte(CriaturaArena outro)
    {
        var d = outro.transform.position - transform.position;
        d.y = 0;
        return d.magnitude;
    }

    private void Olhar(Vector3 ponto)
    {
        var d = ponto - transform.position;
        d.y = 0;
        if (d.sqrMagnitude < 1e-8f) return;
        var alvoRot = Quaternion.LookRotation(d);
        var nova = Quaternion.RotateTowards(transform.rotation, alvoRot, 540f * Time.fixedDeltaTime);
        if (rb != null && rb.isKinematic) rb.MoveRotation(nova); else transform.rotation = nova;
    }

    private float DistanciaDesejada(Tecnica t)
    {
        if (t == null || !t.ADistancia && t.Tipo != TipoTecnica.Suporte) return Perto * 0.85f;
        var d = Perto * 2.8f;
        if (Personalidade == DigimonPersonalities.Brainy) d *= 1.2f;
        // Com pouca vida, quem não é Fighter mantém mais distância.
        if (vidaAtual < vidaMaxima * 0.3f && Personalidade != DigimonPersonalities.Fighter) d *= 1.25f;
        return d;
    }

    /// <summary>Chega na distância da técnica planejada e, perto dela, anda em volta do inimigo.</summary>
    private void Mover(float distancia, float desejada)
    {
        var paraInimigo = inimigo.transform.position - transform.position;
        paraInimigo.y = 0;
        var dir = paraInimigo.sqrMagnitude > 1e-8f ? paraInimigo.normalized : transform.forward;
        var lateral = Vector3.Cross(Vector3.up, dir) * _ladoCirculo;

        if (Time.time > _trocaLado)
        {
            _ladoCirculo = Random.value < 0.5f ? -1 : 1;
            _trocaLado = Time.time + Random.Range(1f, 2.5f);
        }

        var erro = distancia - desejada;
        var movimento = Vector3.zero;
        if (Mathf.Abs(erro) > Perto * 0.25f) movimento += dir * Mathf.Sign(erro);
        // Circular: mais para quem é ágil ou esperto, e quando já está na distância certa.
        var circulo = Personalidade == DigimonPersonalities.Nimble ? 0.8f : 0.25f + 0.35f * Esperteza;
        if (Mathf.Abs(erro) < Perto * 0.8f) movimento += lateral * circulo;
        if (movimento.sqrMagnitude < 0.01f) return;
        // Se afastando do inimigo: no próximo quadro ele vira de costas (FixedUpdate).
        if (Vector3.Dot(movimento.normalized, dir) < -0.35f)
        {
            _andandoDeCostas = true;
            _direcaoAndando = movimento.normalized;
        }

        var passo = movimento.normalized * Velocidade() * Mathf.Min(1f, movimento.magnitude) * Time.fixedDeltaTime;
        var destino = transform.position + passo;
        // Não sai da arena.
        var doCentro = destino - _centroArena;
        doCentro.y = 0;
        if (doCentro.magnitude > _raioArena) destino = _centroArena + doCentro.normalized * _raioArena + Vector3.up * (destino.y - _centroArena.y);
        if (rb != null && rb.isKinematic) rb.MovePosition(destino);
        else transform.position = destino;
        estaMovendo = true;
    }

    // ------------------------------------------------------------ Escolha da técnica (a "IA")

    /// <summary>
    /// Dá uma nota para cada opção e escolhe. Com a Esperteza (Brains) alta, quase sempre a melhor;
    /// com ela baixa, muitas vezes uma qualquer.
    /// </summary>
    private Tecnica EscolherTecnica(bool preferirDistancia = false)
    {
        if (_repertorio == null || _repertorio.Count == 0) _repertorio = new List<Tecnica> { Tecnica.Basico(DigimonAttribute.Neutral) };
        if (Dados == null || inimigo == null || inimigo.Dados == null)
        {
            // Sem ficha: comportamento antigo (às vezes o especial).
            return Random.value < chanceDeEspecial && _repertorio.Count > 1 ? _repertorio[1] : _repertorio[0];
        }

        var opcoes = new List<(Tecnica t, float nota)>();
        var basico = CalculoDano.Estimar(Dados, inimigo.Dados, _repertorio[0], _bonusAtaque);
        var mpFracao = _mp / _mpMax;
        var minhaVida = vidaAtual / (float)Mathf.Max(1, vidaMaxima);
        var distancia = DistanciaAte(inimigo);
        var inimigoEmGuarda = inimigo._estado == Estado.Defendendo;

        // Finalizar: entre as técnicas que já derrubam o inimigo, a mais barata (poupa MP).
        Tecnica finalizadora = null;
        foreach (var t in _repertorio)
        {
            if (t.CustoMP > _mp || t.Tipo == TipoTecnica.Suporte) continue;
            var garantido = CalculoDano.Estimar(Dados, inimigo.Dados, t, _bonusAtaque) * 0.9f; // pior caso da sorte
            if (garantido < inimigo.vidaAtual) continue;
            if (finalizadora == null || t.CustoMP < finalizadora.CustoMP ||
                (t.CustoMP == finalizadora.CustoMP && !t.ADistancia && distancia <= Perto * 1.5f))
                finalizadora = t;
        }
        if (finalizadora != null && Random.value < 0.5f + 0.5f * Esperteza) return finalizadora;

        // Técnica forte que vale guardar MP para usar depois (o golpe final).
        var melhorCaro = 0;
        foreach (var t in _repertorio)
            if (t.CustoMP > melhorCaro && t.Tipo != TipoTecnica.Suporte &&
                CalculoDano.Estimar(Dados, inimigo.Dados, t, _bonusAtaque) >= inimigo.vidaAtual * 0.5f)
                melhorCaro = t.CustoMP;

        foreach (var t in _repertorio)
        {
            if (t.CustoMP > _mp) continue;
            float nota;
            if (t.Tipo == TipoTecnica.Suporte)
            {
                // Attack Charge e parecidos: vale quando a luta ainda vai demorar e ainda não está ativo.
                var ativo = Time.time < _fimBonusAtaque;
                var golpesParaVencer = inimigo.vidaAtual / Mathf.Max(1f, basico);
                nota = ativo || golpesParaVencer < 4f || minhaVida < 0.35f ? 0f : basico * 1.5f;
                if (Personalidade == DigimonPersonalities.Builder) nota *= 1.6f;
            }
            else
            {
                // Dano esperado: o que acerta de fato (o inimigo pode desviar) e o que passa da guarda.
                nota = CalculoDano.Estimar(Dados, inimigo.Dados, t, _bonusAtaque);
                nota *= 1f - CalculoDano.ChanceDesvio(Dados, inimigo.Dados, t) * Esperteza;
                if (inimigoEmGuarda) nota *= t.Basica ? 0.7f : 0.4f;
                // Golpe de perto com o inimigo longe: demora para chegar (e ele pode fugir).
                if (!t.ADistancia)
                    nota *= Mathf.Lerp(1f, 0.6f, Mathf.Clamp01((distancia - Perto * 1.2f) / (Perto * 5f)) * Esperteza);
                // Poupa MP enquanto ele está baixo (a não ser que esteja perdendo).
                if (t.CustoMP > 0 && mpFracao < 0.4f && minhaVida > 0.35f) nota *= 0.6f;
                // Não gasta o MP que vai faltar para o golpe final.
                if (t.CustoMP > 0 && melhorCaro > 0 && t.CustoMP < melhorCaro && _mp - t.CustoMP < melhorCaro)
                    nota *= 1f - 0.35f * Esperteza;
                // Com pouca vida, quem não é Fighter prefere atacar de longe.
                if (minhaVida < 0.3f && t.ADistancia && Personalidade != DigimonPersonalities.Fighter) nota *= 1.25f;
                if (Personalidade == DigimonPersonalities.Fighter && !t.ADistancia) nota *= 1.25f;
                if (Personalidade == DigimonPersonalities.Brainy && t.ADistancia) nota *= 1.25f;
                if (preferirDistancia && t.ADistancia) nota *= 1.5f;
            }
            // Variedade: repetir sempre a mesma técnica fica previsível.
            if (t == _ultima && !t.Basica) nota *= 0.85f;
            if (nota > 0f) opcoes.Add((t, nota));
        }
        if (opcoes.Count == 0) return _repertorio[0];

        opcoes.Sort((a, b) => b.nota.CompareTo(a.nota));
        if (Random.value < Esperteza) return opcoes[0].t;
        // Escolha "sem pensar": sorteio pelas notas, às vezes totalmente aleatório.
        if (Random.value < 0.4f) return opcoes[Random.Range(0, opcoes.Count)].t;
        var soma = 0f;
        foreach (var o in opcoes) soma += o.nota;
        var r = Random.value * soma;
        foreach (var o in opcoes)
        {
            r -= o.nota;
            if (r <= 0f) return o.t;
        }
        return opcoes[0].t;
    }

    // ------------------------------------------------------------ Ação

    private IEnumerator Agir(Tecnica t)
    {
        _estado = Estado.Agindo;
        _barraAcao = 0f;
        _tempoSemAlcance = 0f;
        _plano = null;
        _ultima = t;
        _mp -= t.CustoMP;

        if (!t.Basica)
        {
            TextoDeBatalha.Mostrar(Topo(), t.Nome, TextoDeBatalha.CorTecnica, 46f, 60f);
            UsouTecnica?.Invoke(this, t);
        }
        Animar(t.Tipo == TipoTecnica.Fisica ? nomeAnimacaoAtaque : nomeAnimacaoEspecial);
        if (t.Tipo != TipoTecnica.Suporte && inimigo != null) inimigo.VerGolpeVindo(this, t);

        var cor = t.Cor ?? CorDoElemento(t.Basica && Dados != null ? Dados.Base.Atribute : t.Atributo);

        // Ataque especial com estilo próprio (Terra Force, Blue Blaster, Super Shocker...): CriaturaArena.Estilos.cs.
        if (!t.Basica && t.Estilo != EstiloTecnica.Padrao && t.Estilo != EstiloTecnica.Aura && t.Tipo != TipoTecnica.Suporte)
        {
            yield return ExecutarEstilo(t, cor);
            if (Time.time > _fimBonusAtaque) _bonusAtaque = 1f;
            if (TerminaDePerto(t.Estilo) && Vivo && _estado == Estado.Agindo) yield return Recuo(0.25f);
            yield return new WaitForSeconds(0.2f);
            if (_estado == Estado.Agindo) _estado = Estado.Livre;
            ComecarRecuo();
            yield break;
        }

        GameObject carga = null;
        if (t.ADistancia)
        {
            // Energia juntando na frente do Digimon antes do disparo (maior quanto mais forte a técnica).
            carga = EfeitosBatalha.Carga(transform, PontoDisparoLocal(), _altura * (0.22f + 0.18f * Forca(t)), cor, 0.45f);
            yield return new WaitForSeconds(0.45f);
        }
        else if (t.Tipo == TipoTecnica.Fisica) yield return Investida(0.3f, t.Basica ? 0.5f : 0.75f);
        else yield return new WaitForSeconds(0.3f);
        if (carga != null) Destroy(carga);
        if (_estado != Estado.Agindo || !Vivo) yield break; // levou um golpe e cambaleou

        switch (t.Tipo)
        {
            case TipoTecnica.Suporte:
                _bonusAtaque = 1.3f;
                _fimBonusAtaque = Time.time + 10f;
                _fimFirmeza = Time.time + 10f;
                EfeitosBatalha.Aura(transform, _altura, cor);
                TextoDeBatalha.Mostrar(Topo(), "Offense ↑", TextoDeBatalha.CorDefesa, 44f);
                break;
            case TipoTecnica.Magica:
                if (InimigoVivo) StartCoroutine(Disparo(t, inimigo));
                break;
            default:
                if (InimigoVivo && DistanciaAte(inimigo) <= Perto * 1.5f) inimigo.ReceberGolpe(this, t);
                else TextoDeBatalha.Mostrar(Topo(), "Miss", TextoDeBatalha.CorDefesa, 44f);
                break;
        }
        if (Time.time > _fimBonusAtaque) _bonusAtaque = 1f;

        // Depois de um golpe de perto, volta um pouco (como um pulo para trás).
        if (t.Tipo == TipoTecnica.Fisica && Vivo) yield return Recuo(0.25f);
        yield return new WaitForSeconds(t.Tipo == TipoTecnica.Fisica ? 0.15f : 0.35f);
        if (_estado == Estado.Agindo) _estado = Estado.Livre;
        ComecarRecuo();
    }

    /// <summary>
    /// Ritmo de luta: ataca, recua até uma distância segura (andando em volta) e só então volta para atacar.
    /// O tempo recuado varia com a personalidade (Fighter volta logo; Brainy/Defender esperam mais).
    /// </summary>
    private void ComecarRecuo()
    {
        if (!Vivo) return;
        var tempo = Random.Range(0.6f, 1.1f);
        switch (Personalidade)
        {
            case DigimonPersonalities.Fighter: tempo *= 0.65f; break;
            case DigimonPersonalities.Brainy:
            case DigimonPersonalities.Defender: tempo *= 1.3f; break;
        }
        _recuarAte = Time.time + tempo;
    }

    /// <summary>0 = técnica fraca, 1 = técnica muito forte (pelo Poder).</summary>
    private static float Forca(Tecnica t) => t == null ? 0f : Mathf.Clamp01((t.Poder - 30f) / 100f);

    /// <summary>Ponto de onde sai o disparo (na frente do peito), no espaço do Digimon.</summary>
    private Vector3 PontoDisparoLocal() => new Vector3(0f, _altura * 0.55f, _altura * 0.35f);

    /// <summary>Avança rápido até encostar no inimigo enquanto prepara o golpe (com poeira nos pés).</summary>
    private IEnumerator Investida(float duracao, float forcaDoAvanco)
    {
        if (!InimigoVivo) { yield return new WaitForSeconds(duracao); yield break; }
        var falta = DistanciaAte(inimigo) - Perto * 0.7f;
        var passo = falta > 0f ? falta * forcaDoAvanco : 0f;
        if (passo > Perto * 0.1f) EfeitosBatalha.Poeira(transform.position, _altura * 0.6f);
        var dir = inimigo.transform.position - transform.position;
        dir.y = 0;
        dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : transform.forward;
        // Recua um pouquinho (pegando impulso) e avança.
        for (float k = 0; k < duracao && Vivo && _estado == Estado.Agindo; k += Time.deltaTime)
        {
            var p = k / duracao;
            var velocidade = p < 0.3f ? -0.4f : 1.4f; // impulso para trás, depois o bote
            // (levou um golpe no meio do bote: o empurrão ganha)
            if (Time.time >= _empurradoAte) MoverPara(transform.position + dir * passo * velocidade * Time.deltaTime / duracao);
            yield return null;
        }
    }

    /// <summary>Pulo para trás depois do golpe de perto.</summary>
    private IEnumerator Recuo(float duracao)
    {
        if (!InimigoVivo) yield break;
        var dir = transform.position - inimigo.transform.position;
        dir.y = 0;
        dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : -transform.forward;
        for (float k = 0; k < duracao && Vivo && _estado == Estado.Agindo; k += Time.deltaTime)
        {
            MoverPara(transform.position + dir * Perto * 1.1f * Time.deltaTime / duracao); // pulo para trás
            yield return null;
        }
    }

    /// <summary>Empurrão ao levar um golpe (mais forte no crítico e nos golpes que tiram muito).</summary>
    private IEnumerator Empurrao(CriaturaArena atacante, float forca)
    {
        var dir = atacante != null ? transform.position - atacante.transform.position : -transform.forward;
        dir.y = 0;
        dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : -transform.forward;
        var duracao = Mathf.Lerp(0.18f, 0.34f, Mathf.Clamp01(forca / 2f)); // empurrão forte arrasta por mais tempo
        _empurradoAte = Time.time + duracao;
        if (forca > 0.6f) EfeitosBatalha.Poeira(transform.position, _altura * 0.5f);
        for (float k = 0; k < duracao && Vivo; k += Time.deltaTime)
        {
            var p = 1f - k / duracao; // começa rápido e freia
            MoverPara(transform.position + dir * Perto * forca * 2f * p * Time.deltaTime / duracao);
            yield return null;
        }
        if (forca > 0.6f && Vivo) EfeitosBatalha.Poeira(transform.position, _altura * 0.6f); // arrasta no chão ao parar
    }

    /// <summary>
    /// Golpe forte: o Digimon voa para trás num arco, cai de costas, quica duas vezes, fica caído um instante e
    /// levanta (no estilo dos jogos de luta de Digimon). Se o golpe derrubou (ko), só voa e quica; a animação
    /// "Down" e o sumiço vêm do Morrer.
    /// </summary>
    private IEnumerator Arremesso(CriaturaArena atacante, float forca, float peso, bool ko)
    {
        _noAr = true;
        if (!ko)
        {
            StopCoroutine(nameof(Cambalear));
            _estado = Estado.Atordoado;
            _barraAcao *= 0.4f;
            _fugirAte = 0f;
            Animar("Damage");
        }
        var dir = atacante != null ? transform.position - atacante.transform.position : -transform.forward;
        dir.y = 0;
        dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : -transform.forward;
        var chao = _arenaDefinida ? _chaoY : transform.position.y; // (se já estava no ar, cai até o chão de verdade)
        var distancia = Perto * Mathf.Clamp(forca * 2f, 2.5f, 6f);
        var alturaVoo = _altura * Mathf.Clamp(0.45f + 0.3f * forca, 0.6f, 1.5f);

        // O corpo (modelo) tomba de costas enquanto voa; o "suporte" continua de pé (a física e a mira usam ele).
        var corpo = animator != null && animator.transform != transform && animator.transform.IsChildOf(transform) ? animator.transform : null;
        var giroInicial = corpo != null ? corpo.localRotation : Quaternion.identity;
        const float deitado = -80f;

        // Voo principal e dois quiques, cada um mais baixo e mais curto.
        float[] parte = { 0.62f, 0.26f, 0.12f };
        float[] altura = { 1f, 0.32f, 0.1f };
        float[] duracao = { 0.55f, 0.3f, 0.18f };
        var escalaTempo = Mathf.Lerp(0.85f, 1.25f, Mathf.Clamp01(forca / 3f));
        EfeitosBatalha.Poeira(transform.position, _altura * 0.5f);
        for (var fase = 0; fase < parte.Length; fase++)
        {
            var dur = duracao[fase] * escalaTempo;
            for (float k = 0; k < dur; k += Time.deltaTime)
            {
                var p = Mathf.Clamp01(k / dur);
                var pos = transform.position + dir * distancia * parte[fase] * Time.deltaTime / dur;
                pos.y = chao + alturaVoo * altura[fase] * 4f * p * (1f - p);
                MoverPara(pos);
                if (corpo != null && !ko)
                {
                    // Gira de costas no primeiro voo; nos quiques balança um pouco e volta a deitar.
                    var angulo = fase == 0 ? Mathf.Lerp(0f, deitado, Mathf.SmoothStep(0f, 1f, p)) : deitado + 18f * altura[fase] * Mathf.Sin(p * Mathf.PI);
                    corpo.localRotation = giroInicial * Quaternion.Euler(angulo, 0f, 0f);
                }
                yield return null;
            }
            // Bateu no chão.
            var noChao = transform.position;
            noChao.y = chao;
            MoverPara(noChao);
            EfeitosBatalha.Poeira(noChao, _altura * (fase == 0 ? 0.9f : 0.6f));
        }
        // Arrasta mais um pouco no chão.
        for (float k = 0; k < 0.15f; k += Time.deltaTime)
        {
            var pos = transform.position + dir * distancia * 0.06f * (1f - k / 0.15f) * Time.deltaTime / 0.075f;
            pos.y = chao;
            MoverPara(pos);
            yield return null;
        }
        if (ko || !Vivo) yield break; // derrotado: fica no chão

        // Caído um instante (mais tempo no golpe que tirou muito) e levanta.
        yield return new WaitForSeconds(Mathf.Lerp(0.45f, 0.9f, peso));
        if (!Vivo) yield break;
        if (animator != null && _estadoBase != 0)
        {
            foreach (var trigger in _triggers) animator.ResetTrigger(trigger);
            animator.CrossFadeInFixedTime(_estadoBase, 0.2f);
        }
        var giroDeitado = corpo != null ? corpo.localRotation : Quaternion.identity;
        const float levantar = 0.35f;
        for (float k = 0; k < levantar; k += Time.deltaTime)
        {
            var p = Mathf.SmoothStep(0f, 1f, k / levantar);
            if (corpo != null) corpo.localRotation = Quaternion.Slerp(giroDeitado, giroInicial, p);
            var pos = transform.position;
            pos.y = chao + _altura * 0.12f * Mathf.Sin(p * Mathf.PI); // pulinho ao levantar
            MoverPara(pos);
            yield return null;
        }
        if (corpo != null) corpo.localRotation = giroInicial;
        var fim = transform.position;
        fim.y = chao;
        MoverPara(fim);
        _noAr = false;
        _fimFirmeza = Time.time + 1.2f; // não cambaleia logo depois de levantar
        if (_estado == Estado.Atordoado) _estado = Estado.Livre;
        TalvezFugir(peso);
    }

    /// <summary>Técnica de longe: uma bola de energia na cor do elemento voa até o alvo.</summary>
    private IEnumerator Disparo(Tecnica t, CriaturaArena alvo)
    {
        var bola = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(bola.GetComponent<Collider>());
        bola.name = "Disparo_" + t.Nome;
        _disparos.Add(bola);
        var tamanho = _altura * (0.16f + 0.14f * Forca(t)); // técnica forte = disparo maior
        bola.transform.localScale = Vector3.one * tamanho;
        var r = bola.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            r.material = new Material(shader);
            var cor = CorDoElemento(t.Atributo);
            r.material.SetColor("_BaseColor", cor);
            r.material.color = cor;
        }
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var rastro = bola.AddComponent<TrailRenderer>();
        rastro.time = 0.2f;
        rastro.startWidth = tamanho * 0.8f;
        rastro.endWidth = 0f;
        rastro.material = r.material;

        var inicio = transform.TransformPoint(PontoDisparoLocal() / Escala);
        bola.transform.position = inicio;
        // Brilho em volta do disparo (vai junto com ele).
        var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(halo.GetComponent<Collider>());
        halo.name = "Halo";
        halo.GetComponent<Renderer>().material = Formas.MaterialSemLuz(CorDoElemento(t.Atributo), Formas.Brilho().texture, true);
        halo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        halo.AddComponent<OlharCamera>();
        halo.transform.SetParent(bola.transform, false);
        halo.transform.localScale = Vector3.one * 2.6f;
        var velocidade = Perto * 7f;
        // Vai em linha reta. Quem é esperto mira onde o alvo vai estar quando o disparo chegar.
        var tempoVoo = Vector3.Distance(inicio, alvo.Centro()) / Mathf.Max(0.0001f, velocidade);
        var mira = alvo.Centro() + new Vector3(alvo._velocidade.x, 0f, alvo._velocidade.z) * tempoVoo * Esperteza;
        var direcao = (mira - inicio).normalized;
        var alcance = Vector3.Distance(inicio, mira) + _altura;
        var raioAcerto = tamanho * 0.5f + alvo._altura * 0.35f;
        var acertou = false;
        for (var andado = 0f; andado < alcance && alvo != null && alvo.Vivo; andado += velocidade * Time.deltaTime)
        {
            bola.transform.position += direcao * velocidade * Time.deltaTime;
            var perto = alvo.Centro() - bola.transform.position;
            if (perto.sqrMagnitude < raioAcerto * raioAcerto) { acertou = true; break; }
            yield return null;
        }
        if (alvo != null && alvo.Vivo && Vivo)
        {
            if (acertou) alvo.ReceberGolpe(this, t);
            else TextoDeBatalha.Mostrar(alvo.Topo(), "Miss", TextoDeBatalha.CorDefesa, 48f);
        }
        _disparos.Remove(bola);
        Destroy(bola);
    }

    // ------------------------------------------------------------ Defesa

    /// <summary>
    /// O inimigo começou um golpe contra mim. Um Digimon esperto reage pelo perigo: disparo → esquiva para o lado
    /// (o disparo vai reto e passa); golpe de perto → passo para trás (sai do alcance) ou guarda;
    /// golpe fraco com a vida cheia → quase não se preocupa.
    /// </summary>
    public void VerGolpeVindo(CriaturaArena atacante, Tecnica t)
    {
        if (!Vivo || !podeLutar || _estado != Estado.Livre) return;

        // Quanto esse golpe me machuca (0 = nada, 1 = me derruba).
        var perigo = 0.5f;
        if (atacante != null && atacante.Dados != null && Dados != null)
            perigo = Mathf.Clamp01(CalculoDano.Estimar(atacante.Dados, Dados, t, atacante._bonusAtaque) / Mathf.Max(1, vidaAtual));
        var atencao = Mathf.Lerp(0.6f, 1.6f, Mathf.Clamp01(perigo * 3f)); // golpe perigoso chama mais atenção
        if (Dados != null) atencao += Dados.Amizade / 400f; // confia no Tamer: fica mais atento

        // Esquivar: depende de Speed, Brains e personalidade.
        var rapidez = Dados != null && atacante != null && atacante.Dados != null
            ? CalculoDano.Relativo(Dados.Speed, atacante.Dados.Speed) : 0.5f;
        var chanceEsquiva = (0.04f + 0.6f * Esperteza * rapidez) * atencao;
        if (Personalidade == DigimonPersonalities.Nimble) chanceEsquiva += 0.15f;
        if (!t.ADistancia) chanceEsquiva *= 0.6f; // golpe de perto é mais difícil de evitar
        if (Random.value < chanceEsquiva)
        {
            StartCoroutine(Esquivar(atacante, t.ADistancia));
            return;
        }

        // Defender.
        var chance = (0.05f + 0.3f * Esperteza) * atencao;
        if (Personalidade == DigimonPersonalities.Defender) chance += 0.2f;
        if (vidaAtual < vidaMaxima * 0.3f) chance += 0.1f;
        if (!t.Basica) chance += 0.05f; // técnica grande é mais fácil de ver
        if (Random.value > chance) return;
        _estado = Estado.Defendendo;
        _fimDefesa = Time.time + (t.ADistancia ? 1.4f : 0.9f);
        Animar("Guard");
        EfeitosBatalha.Escudo(transform, Vector3.up * _altura * 0.5f, _altura * 1.2f, _fimDefesa - Time.time);
    }

    /// <summary>Disparo vindo: corre para o lado. Golpe de perto: dá um passo para trás, saindo do alcance.</summary>
    private IEnumerator Esquivar(CriaturaArena atacante, bool deDisparo)
    {
        _estado = Estado.Esquivando;
        var deLa = atacante != null ? transform.position - atacante.transform.position : -transform.forward;
        deLa.y = 0;
        deLa = deLa.sqrMagnitude > 1e-8f ? deLa.normalized : -transform.forward;
        var dir = deDisparo ? Vector3.Cross(Vector3.up, deLa) * (Random.value < 0.5f ? -1 : 1) : deLa;
        var distancia = deDisparo ? Perto * 2.2f : Perto * 1.4f;
        const float duracao = 0.25f;
        EfeitosBatalha.Poeira(transform.position, _altura * 0.6f);
        for (float k = 0; k < duracao && Vivo; k += Time.deltaTime)
        {
            MoverPara(transform.position + dir * distancia * Time.deltaTime / duracao);
            yield return null;
        }
        yield return new WaitForSeconds(0.1f);
        if (_estado == Estado.Esquivando && Vivo) _estado = Estado.Livre;
    }

    /// <summary>Move até o ponto sem sair da arena.</summary>
    private void MoverPara(Vector3 destino)
    {
        if (_arenaDefinida)
        {
            var doCentro = destino - _centroArena;
            doCentro.y = 0;
            if (doCentro.magnitude > _raioArena)
                destino = _centroArena + doCentro.normalized * _raioArena + Vector3.up * (destino.y - _centroArena.y);
        }
        if (rb != null && rb.isKinematic) rb.MovePosition(destino); else transform.position = destino;
    }

    /// <summary>Um golpe chegou: desvio, defesa, crítico e dano.</summary>
    public void ReceberGolpe(CriaturaArena atacante, Tecnica t, float fracao = 1f)
    {
        if (!Vivo) return;
        var a = atacante != null ? atacante.Dados : null;

        if (a != null && Dados != null && _estado != Estado.Agindo && _estado != Estado.Atordoado && _estado != Estado.Esquivando &&
            Random.value < CalculoDano.ChanceDesvio(a, Dados, t))
        {
            TextoDeBatalha.Mostrar(Topo(), "Miss", TextoDeBatalha.CorDefesa, 48f);
            StartCoroutine(PassoDeLado());
            return;
        }

        int dano;
        var critico = false;
        if (a != null && Dados != null)
        {
            critico = Random.value < CalculoDano.ChanceCritico(a, Dados);
            dano = CalculoDano.Calcular(a, Dados, t, critico, atacante._bonusAtaque);
        }
        else dano = t != null && !t.Basica ? 20 : 10; // sem ficha: valores antigos
        if (fracao < 0.999f) dano = Mathf.Max(1, Mathf.RoundToInt(dano * fracao));

        var defendeu = _estado == Estado.Defendendo;
        var corGolpe = t != null && t.Cor.HasValue ? t.Cor.Value
            : CorDoElemento(t == null || t.Basica ? (a != null ? a.Base.Atribute : DigimonAttribute.Neutral) : t.Atributo);
        var forte = critico || (t != null && !t.Basica && Forca(t) > 0.5f);
        if (defendeu)
        {
            dano = Mathf.Max(1, Mathf.RoundToInt(dano * 0.4f));
            TextoDeBatalha.Mostrar(Topo() + Vector3.up * _altura * 0.15f, "Guard", TextoDeBatalha.CorDefesa, 40f);
            EfeitosBatalha.Impacto(Centro(), _altura * 0.45f, new Color(0.6f, 0.85f, 1f), false);
        }
        else
        {
            if (critico) TextoDeBatalha.Mostrar(Topo() + Vector3.up * _altura * 0.15f, "Critical!", new Color(1f, 0.55f, 0.1f), 44f);
            EfeitosBatalha.Impacto(Centro(), _altura * (0.5f + 0.3f * Forca(t)) * Mathf.Lerp(0.6f, 1f, fracao), corGolpe, forte);
            // Faíscas saindo do ponto do impacto.
            EfeitosBatalha.Emitir("Faiscas", Centro(), Quaternion.identity, new EfeitosBatalha.Particulas
            {
                Cor = corGolpe, Tamanho = _altura * 0.1f, Vida = 0.35f, Velocidade = _altura * 2.5f,
                Rajada = Mathf.RoundToInt((forte ? 30 : 14) * Mathf.Lerp(0.5f, 1f, fracao)),
                Forma = ParticleSystemShapeType.Sphere, Raio = _altura * 0.1f, Duracao = 0.1f, Gravidade = 0.5f
            });
        }

        // Empurrão: pequeno na guarda, maior no crítico e nos golpes que tiram muito.
        var peso = Mathf.Clamp01(dano / (float)Mathf.Max(1, vidaMaxima) * 4f);
        // (golpes de perto e técnicas fortes jogam mais longe; disparos fracos quase não empurram)
        // Todo golpe empurra (até o fraco e o de longe); na guarda, menos.
        var impulso = (defendeu ? 0.6f : 1.3f + 1.8f * peso + (critico ? 0.8f : 0f) + (forte ? 0.6f : 0f)) * Mathf.Lerp(0.6f, 1f, fracao);
        if (t != null && t.Tipo == TipoTecnica.Fisica) impulso *= 1.2f;
        // Golpe forte (ou o que derruba): voa longe, quica no chão, fica caído e levanta.
        var arremessa = !defendeu && fracao > 0.5f && (forte || critico || peso > 0.55f || dano >= vidaAtual);

        ReceberDano(dano);
        if (!Vivo)
        {
            if (arremessa || fracao > 0.5f) StartCoroutine(Arremesso(atacante, impulso, 0f, true));
            return;
        }
        if (_noAr) return; // já voando: o golpe só tira vida
        if (arremessa)
        {
            StartCoroutine(Arremesso(atacante, impulso, peso, false));
            return; // o resto (cambalear/fugir) acontece quando ele levanta
        }
        StartCoroutine(Empurrao(atacante, impulso));
        if (defendeu) return;
        if (forte || peso > 0.5f) TalvezFugir(peso);

        // Golpe forte faz cambalear (perde parte da barra de ação).
        var limite = vidaMaxima * (Personalidade == DigimonPersonalities.Durable ? 0.18f : 0.1f);
        if ((dano >= limite || critico) && Time.time > _fimFirmeza)
        {
            StopCoroutine(nameof(Cambalear));
            StartCoroutine(nameof(Cambalear));
        }
    }

    private IEnumerator Cambalear()
    {
        _estado = Estado.Atordoado;
        _barraAcao *= 0.5f;
        Animar("Damage");
        yield return new WaitForSeconds(0.45f);
        if (_estado == Estado.Atordoado && Vivo) _estado = Estado.Livre;
    }

    private IEnumerator PassoDeLado()
    {
        var lado = Vector3.Cross(Vector3.up, transform.forward) * (Random.value < 0.5f ? -1 : 1);
        for (float k = 0; k < 0.2f; k += Time.deltaTime)
        {
            var destino = transform.position + lado * Perto * 3f * Time.deltaTime;
            if (rb != null && rb.isKinematic) rb.MovePosition(destino); else transform.position = destino;
            yield return null;
        }
    }

    // ------------------------------------------------------------ Vida

    private bool _aguentou;

    public void ReceberDano(int dano)
    {
        if (vidaAtual <= 0) return;
        // Amizade alta: uma vez por luta, pode aguentar firme com 1 de vida.
        if (dano >= vidaAtual && !_aguentou && Dados != null && Dados.Amizade >= 50 && vidaAtual > 1 &&
            Random.value < Dados.Amizade / 250f)
        {
            _aguentou = true;
            dano = vidaAtual - 1;
            TextoDeBatalha.Mostrar(Topo() + Vector3.up * _altura * 0.3f, "Held on!", new Color(1f, 0.6f, 0.8f), 44f);
        }
        vidaAtual = Mathf.Max(0, vidaAtual - dano);
        if (hpBar != null) hpBar.SetHP(vidaAtual);
        TextoDeBatalha.Mostrar(Topo(), dano.ToString(), EhParceiro() ? TextoDeBatalha.CorDanoParceiro : TextoDeBatalha.CorDanoInimigo, 64f);
        RecebeuDano?.Invoke(this, dano);
        if (vidaAtual <= 0) Morrer();
    }

    private bool EhParceiro() => doJogador;

    void Morrer()
    {
        podeLutar = false;
        _estado = Estado.Caido;
        StopAllCoroutines();
        LimparDisparos();
        Derrotado?.Invoke(this);
        if (inimigo != null) inimigo.Vencer();
        if (_triggers.Contains("Down"))
        {
            Animar("Down");
            StartCoroutine(Sumir(1.6f));
        }
        else StartCoroutine(Sumir(0f));
    }

    /// <summary>O inimigo caiu: comemora.</summary>
    public void Vencer()
    {
        podeLutar = false;
        StopAllCoroutines();
        LimparDisparos();
        _estado = Estado.Livre;
        StartCoroutine(Comemorar());
    }

    private void LimparDisparos()
    {
        foreach (var d in _disparos) if (d != null) Destroy(d);
        _disparos.Clear();
    }

    private void OnDestroy() => LimparDisparos();

    private IEnumerator Comemorar()
    {
        yield return new WaitForSeconds(0.5f);
        Animar("Win");
    }

    private IEnumerator Sumir(float espera)
    {
        if (espera > 0f) yield return new WaitForSeconds(espera);
        var inicial = transform.localScale;
        for (float t = 0; t < 1f; t += Time.deltaTime * 2.5f)
        {
            transform.localScale = Vector3.Lerp(inicial, Vector3.zero, t);
            yield return null;
        }
        Destroy(gameObject);
    }

    // ------------------------------------------------------------ Posições

    void LateUpdate()
    {
        if (modeloVisual != null)
            modeloVisual.position = transform.position;
    }

    private float AlturaModelo()
    {
        var ok = false;
        var b = new Bounds();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!ok) { b = r.bounds; ok = true; } else b.Encapsulate(r.bounds);
        }
        return ok ? Mathf.Max(0.01f, b.size.y) : Perto;
    }

    private Vector3 Centro() => transform.position + Vector3.up * _altura * 0.5f;
    private Vector3 Topo() => transform.position + Vector3.up * _altura * 1.05f;

    private static Color CorDoElemento(DigimonAttribute a)
    {
        switch (a)
        {
            case DigimonAttribute.Fire: return new Color(1f, 0.45f, 0.1f);
            case DigimonAttribute.Water: return new Color(0.3f, 0.7f, 1f);
            case DigimonAttribute.Plant: return new Color(0.4f, 0.9f, 0.3f);
            case DigimonAttribute.Electric: return new Color(1f, 0.95f, 0.3f);
            case DigimonAttribute.Earth: return new Color(0.7f, 0.5f, 0.3f);
            case DigimonAttribute.Wind: return new Color(0.75f, 1f, 0.9f);
            case DigimonAttribute.Light: return new Color(1f, 1f, 0.8f);
            case DigimonAttribute.Dark: return new Color(0.55f, 0.2f, 0.8f);
            default: return Color.white;
        }
    }
}
