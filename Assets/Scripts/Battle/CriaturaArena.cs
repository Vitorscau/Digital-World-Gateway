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
///
/// Com Configurar(...), usa a ficha do Digimon. Sem ficha, luta com os valores do Inspector.
/// </summary>
public class CriaturaArena : MonoBehaviour
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

    private enum Estado { Livre, Agindo, Defendendo, Atordoado, Caido }

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
        if (!_arenaDefinida) DefinirArena();
        Olhar(inimigo.transform.position);
        if (_estado != Estado.Livre) return;

        if (_plano == null || (_plano.CustoMP > _mp)) _plano = EscolherTecnica();
        var distancia = DistanciaAte(inimigo);
        var alvo = DistanciaDesejada(_plano);

        // Barra cheia e no alcance: age.
        if (_barraAcao >= 1f)
        {
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

    private void DefinirArena()
    {
        _arenaDefinida = true;
        _centroArena = (transform.position + inimigo.transform.position) * 0.5f;
        _raioArena = Mathf.Max(DistanciaAte(inimigo) * 0.9f, Perto * 4f);
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
        foreach (var t in _repertorio)
        {
            if (t.CustoMP > _mp) continue;
            float nota;
            if (t.Tipo == TipoTecnica.Suporte)
            {
                // Attack Charge e parecidos: vale no começo da luta, se ainda não está ativo.
                var ativo = Time.time < _fimBonusAtaque;
                nota = ativo || inimigo.vidaAtual < inimigo.vidaMaxima * 0.4f ? 0f : basico * 1.5f;
                if (Personalidade == DigimonPersonalities.Builder) nota *= 1.6f;
            }
            else
            {
                nota = CalculoDano.Estimar(Dados, inimigo.Dados, t, _bonusAtaque);
                // Acaba com o inimigo: prioridade máxima.
                if (nota >= inimigo.vidaAtual) nota *= 3f;
                // Poupa MP enquanto ele está baixo (a não ser que esteja perdendo).
                if (t.CustoMP > 0 && mpFracao < 0.4f && minhaVida > 0.35f) nota *= 0.6f;
                if (Personalidade == DigimonPersonalities.Fighter && !t.ADistancia) nota *= 1.25f;
                if (Personalidade == DigimonPersonalities.Brainy && t.ADistancia) nota *= 1.25f;
                if (preferirDistancia && t.ADistancia) nota *= 1.5f;
            }
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
        _mp -= t.CustoMP;

        if (!t.Basica)
        {
            TextoDeBatalha.Mostrar(Topo(), t.Nome, TextoDeBatalha.CorTecnica, 46f, 60f);
            UsouTecnica?.Invoke(this, t);
        }
        Animar(t.Tipo == TipoTecnica.Fisica ? nomeAnimacaoAtaque : nomeAnimacaoEspecial);
        if (t.Tipo != TipoTecnica.Suporte && inimigo != null) inimigo.VerGolpeVindo(this, t);

        yield return new WaitForSeconds(t.ADistancia ? 0.45f : 0.3f);
        if (_estado != Estado.Agindo || !Vivo) yield break; // levou um golpe e cambaleou

        switch (t.Tipo)
        {
            case TipoTecnica.Suporte:
                _bonusAtaque = 1.3f;
                _fimBonusAtaque = Time.time + 10f;
                _fimFirmeza = Time.time + 10f;
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

        yield return new WaitForSeconds(0.35f);
        if (_estado == Estado.Agindo) _estado = Estado.Livre;
    }

    /// <summary>Técnica de longe: uma bola de energia na cor do elemento voa até o alvo.</summary>
    private IEnumerator Disparo(Tecnica t, CriaturaArena alvo)
    {
        var bola = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(bola.GetComponent<Collider>());
        bola.name = "Disparo_" + t.Nome;
        _disparos.Add(bola);
        var tamanho = _altura * 0.22f;
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

        var inicio = Centro();
        bola.transform.position = inicio;
        var velocidade = Perto * 7f;
        while (alvo != null && alvo.Vivo)
        {
            var destino = alvo.Centro();
            bola.transform.position = Vector3.MoveTowards(bola.transform.position, destino, velocidade * Time.deltaTime);
            if ((bola.transform.position - destino).sqrMagnitude < (tamanho * 0.5f) * (tamanho * 0.5f)) break;
            yield return null;
        }
        if (alvo != null && alvo.Vivo && Vivo) alvo.ReceberGolpe(this, t);
        _disparos.Remove(bola);
        Destroy(bola);
    }

    // ------------------------------------------------------------ Defesa

    /// <summary>O inimigo começou um golpe contra mim: um Digimon esperto pode se defender a tempo.</summary>
    public void VerGolpeVindo(CriaturaArena atacante, Tecnica t)
    {
        if (!Vivo || !podeLutar || _estado != Estado.Livre) return;
        var chance = 0.05f + 0.3f * Esperteza;
        if (Personalidade == DigimonPersonalities.Defender) chance += 0.2f;
        if (vidaAtual < vidaMaxima * 0.3f) chance += 0.1f;
        if (!t.Basica) chance += 0.05f; // técnica grande é mais fácil de ver
        if (Dados != null) chance += Dados.Amizade / 400f; // confia no Tamer: fica mais atento
        if (Random.value > chance) return;
        _estado = Estado.Defendendo;
        _fimDefesa = Time.time + (t.ADistancia ? 1.4f : 0.9f);
        Animar("Guard");
    }

    /// <summary>Um golpe chegou: desvio, defesa, crítico e dano.</summary>
    public void ReceberGolpe(CriaturaArena atacante, Tecnica t)
    {
        if (!Vivo) return;
        var a = atacante != null ? atacante.Dados : null;

        if (a != null && Dados != null && _estado != Estado.Agindo && _estado != Estado.Atordoado &&
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

        var defendeu = _estado == Estado.Defendendo;
        if (defendeu)
        {
            dano = Mathf.Max(1, Mathf.RoundToInt(dano * 0.4f));
            TextoDeBatalha.Mostrar(Topo() + Vector3.up * _altura * 0.15f, "Guard", TextoDeBatalha.CorDefesa, 40f);
        }
        else if (critico) TextoDeBatalha.Mostrar(Topo() + Vector3.up * _altura * 0.15f, "Critical!", new Color(1f, 0.55f, 0.1f), 44f);

        ReceberDano(dano);
        if (!Vivo || defendeu) return;

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
            TextoDeBatalha.Mostrar(Topo() + Vector3.up * _altura * 0.3f, "Aguentou firme!", new Color(1f, 0.6f, 0.8f), 44f);
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
