using System.Collections;
using UnityEngine;

/// <summary>
/// O parceiro "vivo" na cena de cuidado (V-Pet em AR). A IA olha as necessidades e o jeito dele:
/// - com fome, vem até o Tamer pedir comida; cansado, deita e dorme; triste, fica emburrado de costas;
/// - feliz e amigo, passeia, brinca e vem para perto do Tamer sozinho;
/// - quando chamado (toque no chão), obedece mais quanto maior a amizade;
/// - reage a carinho, comida, limpeza e treino (animação do modelo ou, se ele não tiver, pulinhos e balançadas).
/// </summary>
public class DigimonVirtual : MonoBehaviour
{
    public enum Estado { Livre, Andando, Dormindo, Ocupado }

    public Estado Atual { get; private set; } = Estado.Livre;
    public bool Dormindo => Atual == Estado.Dormindo;
    public float Altura { get; private set; }
    public DigimonSalvo Dados { get; private set; }

    private CenaCuidado _cena;
    private Animator _anim;
    private bool _temIsMoving;
    private readonly System.Collections.Generic.HashSet<string> _triggers = new System.Collections.Generic.HashSet<string>();
    private DigimonPersonalities _jeito;

    private float _chaoY;
    private Vector3 _casa;          // ponto em volta do qual ele vive
    private Vector3 _destino;
    private float _velocidade;
    private Vector3 _escalaBase;
    private float _proximaDecisao;
    private float _proximoPedido;
    private float _yawExtra, _pulo, _achata = 1f;
    private int _carinhosRecentes;
    private float _ultimoCarinho;

    public Vector3 Topo => transform.position + Vector3.up * Altura * 1.1f;

    public void Iniciar(CenaCuidado cena, DigimonSalvo dados, Vector3 chao, float altura)
    {
        _cena = cena;
        Dados = dados;
        _chaoY = chao.y;
        _casa = chao;
        Altura = altura;
        _escalaBase = transform.localScale;
        _velocidade = altura * 0.9f; // ~1 corpo por segundo
        var ficha = dados.Ficha;
        _jeito = ficha != null ? new Digimon(ficha, Mathf.Max(1, dados.nivel)).Personalidade : DigimonPersonalities.Lively;

        _anim = GetComponentInChildren<Animator>();
        if (_anim != null)
        {
            _anim.enabled = true;
            _anim.applyRootMotion = false;
            if (_anim.runtimeAnimatorController == null && ficha != null && ficha.Animator != null)
                _anim.runtimeAnimatorController = ficha.Animator.runtimeAnimatorController;
            if (_anim.runtimeAnimatorController != null)
                foreach (var p in _anim.parameters)
                {
                    if (p.name == "isMoving") _temIsMoving = true;
                    if (p.type == AnimatorControllerParameterType.Trigger) _triggers.Add(p.name);
                }
        }
        _proximaDecisao = Time.time + 1.5f;
        if (Dados.energia < 10f) StartCoroutine(Dormir());
    }

    // ------------------------------------------------------------ Laço

    private void Update()
    {
        if (Dados == null) return;
        if (Dormindo)
        {
            CuidadoDigimon.Mudar(ref Dados.energia, 2.5f * Time.deltaTime);
            CuidadoDigimon.Mudar(ref Dados.humor, 0.15f * Time.deltaTime);
            _achata = 0.94f + Mathf.Sin(Time.time * 2f) * 0.02f; // respiração
            if (Dados.energia >= 100f) Acordar(false);
            return;
        }

        if (Atual == Estado.Andando) Andar();
        else if (Atual == Estado.Livre)
        {
            OlharPara(_cena.PosicaoTamer, 120f);
            if (Time.time >= _proximaDecisao) Decidir();
        }
        if (_temIsMoving) _anim.SetBool("isMoving", Atual == Estado.Andando);
        _carinhosRecentes = Time.time - _ultimoCarinho > 20f ? 0 : _carinhosRecentes;
    }

    private void LateUpdate()
    {
        var p = transform.position;
        p.y = _chaoY + _pulo;
        transform.position = p;
        var largura = 1.5f - _achata * 0.5f; // achatou: fica mais largo
        transform.localScale = new Vector3(_escalaBase.x * largura, _escalaBase.y * _achata, _escalaBase.z * largura);
    }

    /// <summary>O que fazer agora, pelas necessidades e pelo jeito dele.</summary>
    private void Decidir()
    {
        _proximaDecisao = Time.time + Random.Range(2.5f, 5f);
        var d = Dados;

        if (d.energia < 12f) { StartCoroutine(Dormir()); return; }

        if (d.fome < 25f && Time.time > _proximoPedido)
        {
            _proximoPedido = Time.time + 12f;
            IrAte(PertoDoTamer());
            _cena.Balao(this, "Hungry!", Itens.Icone(Itens.Meat));
            return;
        }

        if (d.humor < 20f)
        {
            // Emburrado: fica parado de costas para o Tamer.
            StartCoroutine(Emburrar());
            return;
        }

        if (d.cocos > 0 && Random.value < 0.25f) _cena.Balao(this, "What a mess...", null);
        if (d.energia < 30f && Random.value < 0.3f) _cena.Balao(this, "*Yawn*...", null);

        // Livre: passear, vir para perto, brincar ou só olhar.
        var amizade = d.amizade / 100f;
        var passear = _jeito == DigimonPersonalities.Lively || _jeito == DigimonPersonalities.Nimble ? 0.5f : 0.3f;
        var vir = 0.1f + amizade * 0.4f;
        var brincar = d.humor > 70f ? 0.2f : 0.05f;
        var r = Random.value * (passear + vir + brincar + 0.3f);
        if (r < passear) IrAte(PontoAoRedor());
        else if (r < passear + vir) IrAte(PertoDoTamer());
        else if (r < passear + vir + brincar) StartCoroutine(Pular(2));
    }

    // ------------------------------------------------------------ Movimento

    private void IrAte(Vector3 ponto)
    {
        if (Dormindo) return;
        ponto.y = _chaoY;
        _destino = ponto;
        Atual = Estado.Andando;
    }

    private void Andar()
    {
        var para = _destino - transform.position;
        para.y = 0;
        if (para.magnitude < Altura * 0.15f)
        {
            Atual = Estado.Livre;
            _proximaDecisao = Mathf.Min(_proximaDecisao, Time.time + Random.Range(1f, 2.5f));
            return;
        }
        OlharPara(_destino, 360f);
        var vel = _velocidade * (_jeito == DigimonPersonalities.Nimble ? 1.3f : 1f) * (Dados.energia < 25f ? 0.6f : 1f);
        transform.position += para.normalized * Mathf.Min(para.magnitude, vel * Time.deltaTime);
        CuidadoDigimon.Mudar(ref Dados.energia, -0.05f * Time.deltaTime);
    }

    private void OlharPara(Vector3 ponto, float grausPorSegundo)
    {
        var d = ponto - transform.position;
        d.y = 0;
        if (d.sqrMagnitude < 1e-6f) return;
        var alvo = Quaternion.LookRotation(d) * Quaternion.Euler(0, _yawExtra, 0);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, alvo, grausPorSegundo * Time.deltaTime);
    }

    private Vector3 PontoAoRedor()
    {
        var c = Random.insideUnitCircle * Altura * 3f;
        return _casa + new Vector3(c.x, 0, c.y);
    }

    private Vector3 PertoDoTamer()
    {
        var t = _cena.PosicaoTamer;
        var dir = transform.position - t;
        dir.y = 0;
        var dist = Mathf.Max(Altura * 2.5f, 0.8f); // perto, mas ainda inteiro na tela
        return t + (dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward) * dist;
    }

    // ------------------------------------------------------------ Reações (chamadas pela cena)

    /// <summary>O Tamer tocou no chão: "vem cá!". Obedece mais com amizade alta.</summary>
    public void Chamar(Vector3 ponto)
    {
        if (Atual == Estado.Ocupado) return;
        if (Dormindo) { _cena.Balao(this, "Zzz...", null); return; }
        var obedece = 0.45f + Dados.amizade / 180f;
        if (Dados.humor < 20f) obedece *= 0.5f;
        if (Random.value < obedece)
        {
            IrAte(ponto);
            if (Random.value < 0.3f) _cena.Balao(this, "Coming!", null);
        }
        else
        {
            _cena.Balao(this, "Hmph.", null);
            StartCoroutine(Negar());
        }
    }

    /// <summary>Carinho (toque ou esfregar). Retorna quanto de amizade ganhou.</summary>
    public int Carinho()
    {
        if (Atual == Estado.Ocupado) return 0;
        if (Dormindo)
        {
            Acordar(true);
            return 0;
        }
        _ultimoCarinho = Time.time;
        var ganho = CuidadoDigimon.Carinho(Dados, _carinhosRecentes++);
        _cena.Coracoes(Topo, _carinhosRecentes <= 3 ? 3 : 1);
        if (_carinhosRecentes <= 3) StartCoroutine(Pular(1));
        else if (_carinhosRecentes > 6) _cena.Balao(this, "Okay, enough!", null);
        Atual = Atual == Estado.Andando ? Estado.Livre : Atual;
        return ganho;
    }

    public IEnumerator Comer()
    {
        Atual = Estado.Ocupado;
        _cena.Balao(this, "Yum!", Itens.Icone(Itens.Meat));
        if (_triggers.Contains("Eat"))
        {
            _anim.SetTrigger("Eat");
            yield return new WaitForSeconds(2.2f);
        }
        else
        {
            for (var i = 0; i < 5; i++)
            {
                _achata = 0.85f; yield return new WaitForSeconds(0.15f);
                _achata = 1f; yield return new WaitForSeconds(0.2f);
            }
        }
        Atual = Estado.Livre;
        StartCoroutine(Pular(1));
    }

    public void RecusarComida()
    {
        _cena.Balao(this, "I'm full!", null);
        StartCoroutine(Negar());
    }

    public void Comemorar()
    {
        if (_triggers.Contains("Win")) _anim.SetTrigger("Win");
        StartCoroutine(Pular(2));
    }

    /// <summary>Uma repetição do treino: o golpe que combina com o status treinado.</summary>
    public void AnimarTreino(StatusTreino s, bool acertou)
    {
        Atual = Estado.Ocupado;
        switch (s)
        {
            case StatusTreino.Offense: Gatilho("Attack", 1); break;
            case StatusTreino.Brains: Gatilho("Special", 1); break;
            case StatusTreino.Defense: Gatilho("Guard", 0); break;
            default: StartCoroutine(Corridinha()); break;
        }
        if (!acertou) StartCoroutine(Negar());
    }

    public void FimDoTreino() { if (Atual == Estado.Ocupado) Atual = Estado.Livre; }

    /// <summary>Pinça com dois dedos: muda o tamanho dele no AR.</summary>
    public void ReescalarBase(float fator)
    {
        var novaAltura = Mathf.Clamp(Altura * fator, 0.1f, 3f);
        fator = novaAltura / Mathf.Max(0.0001f, Altura);
        _escalaBase *= fator;
        Altura = novaAltura;
        _velocidade *= fator;
    }

    private void Gatilho(string trigger, int pulosSeNaoTiver)
    {
        if (_triggers.Contains(trigger)) _anim.SetTrigger(trigger);
        else StartCoroutine(Pular(pulosSeNaoTiver));
    }

    // ------------------------------------------------------------ Dormir

    public IEnumerator Dormir()
    {
        if (Dormindo) yield break;
        Atual = Estado.Dormindo;
        _cena.Balao(this, "Zzz...", null);
        if (_triggers.Contains("Down")) _anim.SetTrigger("Down");
        StartCoroutine(Roncar());
    }

    private IEnumerator Roncar()
    {
        while (Dormindo)
        {
            yield return new WaitForSeconds(4f);
            if (Dormindo) _cena.Balao(this, "Zzz...", null);
        }
    }

    /// <summary>Acorda. Se acordado à força (toque), fica de mau humor.</summary>
    public void Acordar(bool aForca)
    {
        if (!Dormindo) return;
        Atual = Estado.Livre;
        _achata = 1f;
        if (_anim != null && _anim.runtimeAnimatorController != null) _anim.Play("idle", 0, 0f);
        if (aForca)
        {
            CuidadoDigimon.Mudar(ref Dados.humor, -6f);
            _cena.Balao(this, "Hey! I was sleeping!", null);
            StartCoroutine(Negar());
        }
        else
        {
            _cena.Balao(this, "I slept well!", null);
            StartCoroutine(Pular(1));
        }
        _proximaDecisao = Time.time + 2f;
    }

    // ------------------------------------------------------------ Animações feitas por código

    private IEnumerator Pular(int vezes)
    {
        for (var i = 0; i < vezes; i++)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime * 3.2f)
            {
                _pulo = Mathf.Sin(t * Mathf.PI) * Altura * 0.25f;
                _achata = t < 0.15f ? 0.88f : 1.05f;
                yield return null;
            }
            _pulo = 0f;
            _achata = 1f;
        }
    }

    /// <summary>Balança a cabeça para os lados ("não").</summary>
    private IEnumerator Negar()
    {
        for (float t = 0; t < 1f; t += Time.deltaTime * 1.6f)
        {
            transform.rotation *= Quaternion.Euler(0, Mathf.Sin(t * Mathf.PI * 6f) * 120f * Time.deltaTime, 0);
            yield return null;
        }
    }

    private IEnumerator Emburrar()
    {
        Atual = Estado.Ocupado;
        _cena.Balao(this, "...", null);
        for (float t = 0; t < 3f; t += Time.deltaTime)
        {
            var costas = transform.position - (_cena.PosicaoTamer - transform.position);
            OlharPara(costas, 180f);
            yield return null;
        }
        Atual = Estado.Livre;
    }

    private IEnumerator Corridinha()
    {
        var inicio = transform.position;
        var lado = transform.right * Altura * 0.8f;
        for (float t = 0; t < 1f; t += Time.deltaTime * 2.5f)
        {
            transform.position = inicio + lado * Mathf.Sin(t * Mathf.PI * 2f);
            yield return null;
        }
        transform.position = inicio;
    }
}
