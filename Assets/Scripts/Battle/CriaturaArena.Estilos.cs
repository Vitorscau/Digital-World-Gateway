using System.Collections;
using UnityEngine;

/// <summary>
/// Os ataques especiais de cada Digimon na arena, pelo estilo da técnica (coluna "Estilo" do tecnicas.txt, escolhida
/// pela descrição real do ataque). Cada estilo tem preparação, partículas e um jeito de acertar — e quase todos dá
/// para evitar se mexendo: disparos voam em linha reta, raios e sopros acertam o que está na linha/cone, chuva e
/// pilar avisam no chão antes, ondas e tremores se espalham pelo chão.
/// </summary>
public partial class CriaturaArena
{
    /// <summary>Usa uma técnica agora (para testar e pré-visualizar os ataques no Editor).</summary>
    public void UsarTecnicaDeTeste(Tecnica t)
    {
        if (t == null || !Vivo || inimigo == null) return;
        StopCoroutine(nameof(Agir));
        _estado = Estado.Livre;
        StartCoroutine(Agir(t));
    }

    /// <summary>Estilos que terminam de perto do inimigo (o Digimon pula de volta depois).</summary>
    private static bool TerminaDePerto(EstiloTecnica e) =>
        e == EstiloTecnica.Golpe || e == EstiloTecnica.Corte || e == EstiloTecnica.Investida;

    private IEnumerator ExecutarEstilo(Tecnica t, Color cor)
    {
        switch (t.Estilo)
        {
            case EstiloTecnica.Projetil: yield return EstiloProjetil(t, cor); break;
            case EstiloTecnica.Rajada: yield return EstiloRajada(t, cor); break;
            case EstiloTecnica.EsferaGigante: yield return EstiloEsferaGigante(t, cor); break;
            case EstiloTecnica.Raio: yield return EstiloRaio(t, cor); break;
            case EstiloTecnica.Sopro: yield return EstiloSopro(t, cor); break;
            case EstiloTecnica.Relampago: yield return EstiloRelampago(t, cor); break;
            case EstiloTecnica.Bolhas: yield return EstiloBolhas(t, cor); break;
            case EstiloTecnica.Chuva: yield return EstiloChuva(t, cor); break;
            case EstiloTecnica.Tornado: yield return EstiloTornado(t, cor); break;
            case EstiloTecnica.Pilar: yield return EstiloPilar(t, cor); break;
            case EstiloTecnica.Onda: yield return EstiloOnda(t, cor); break;
            case EstiloTecnica.Corte: yield return EstiloCorte(t, cor); break;
            case EstiloTecnica.Investida: yield return EstiloInvestida(t, cor); break;
            case EstiloTecnica.Tremor: yield return EstiloTremor(t, cor); break;
            default: yield return EstiloGolpe(t, cor); break;
        }
    }

    // ------------------------------------------------------------ Disparos

    /// <summary>Uma bola de energia (Pepper Breath, Pyro Sphere...).</summary>
    private IEnumerator EstiloProjetil(Tecnica t, Color cor)
    {
        var carga = EfeitosBatalha.Carga(transform, PontoDisparoLocal(), _altura * (0.22f + 0.18f * Forca(t)), cor, 0.4f);
        yield return new WaitForSeconds(0.4f);
        if (carga != null) Destroy(carga);
        if (!Pronto()) yield break;
        StartCoroutine(Voar(t, inimigo, Boca(), _altura * (0.18f + 0.14f * Forca(t)), Perto * 7f, cor, 1f));
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>Vários disparos pequenos em sequência (mísseis, agulhas, penas, flechas...).</summary>
    private IEnumerator EstiloRajada(Tecnica t, Color cor)
    {
        var carga = EfeitosBatalha.Carga(transform, PontoDisparoLocal(), _altura * 0.2f, cor, 0.3f);
        yield return new WaitForSeconds(0.3f);
        if (carga != null) Destroy(carga);
        const int tiros = 5;
        for (var i = 0; i < tiros && Pronto(); i++)
        {
            var lado = transform.right * (Random.Range(-0.25f, 0.25f) * _altura) + Vector3.up * (Random.Range(-0.1f, 0.15f) * _altura);
            StartCoroutine(Voar(t, inimigo, Boca() + lado, _altura * 0.08f, Perto * 9f, cor, 0.25f, desvioMira: _altura * 0.25f));
            yield return new WaitForSeconds(0.09f);
        }
        yield return new WaitForSeconds(0.15f);
    }

    /// <summary>Esfera enorme carregada acima da cabeça e arremessada (Terra Force, Gaia Force...).</summary>
    private IEnumerator EstiloEsferaGigante(Tecnica t, Color cor)
    {
        var acima = new Vector3(0f, _altura * 1.4f, _altura * 0.1f);
        var carga = EfeitosBatalha.Carga(transform, acima, _altura * 0.75f, cor, 0.9f);
        // Energia sendo puxada de volta para a esfera.
        EfeitosBatalha.Emitir("Juntando", transform.position + transform.rotation * acima, Quaternion.identity, new EfeitosBatalha.Particulas
        {
            Cor = cor, Tamanho = _altura * 0.1f, Vida = 0.5f, Velocidade = -_altura * 2.2f, Taxa = 90f,
            Forma = ParticleSystemShapeType.Sphere, Raio = _altura * 1.1f, Duracao = 0.8f
        });
        yield return new WaitForSeconds(0.9f);
        if (carga != null) Destroy(carga);
        if (!Pronto()) yield break;
        StartCoroutine(Voar(t, inimigo, transform.position + transform.rotation * acima, _altura * 0.55f, Perto * 4.5f, cor, 1f,
            raioExtra: _altura * 0.35f, explosao: true));
        yield return new WaitForSeconds(0.3f);
    }

    /// <summary>Bolhas lentas e balançando (bebês).</summary>
    private IEnumerator EstiloBolhas(Tecnica t, Color cor)
    {
        yield return new WaitForSeconds(0.25f);
        var corBolha = new Color(Mathf.Lerp(cor.r, 0.7f, 0.5f), Mathf.Lerp(cor.g, 0.9f, 0.5f), 1f, 0.8f);
        for (var i = 0; i < 5 && Pronto(); i++)
        {
            StartCoroutine(Voar(t, inimigo, Boca(), _altura * Random.Range(0.1f, 0.16f), Perto * 3.5f, corBolha, 0.22f,
                ondulacao: _altura * 0.25f, desvioMira: _altura * 0.2f, bolha: true));
            yield return new WaitForSeconds(0.12f);
        }
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>
    /// Um disparo que vai em linha reta (mirando onde o alvo vai estar, pela Esperteza). Acerta se encostar no alvo.
    /// </summary>
    private IEnumerator Voar(Tecnica t, CriaturaArena alvo, Vector3 inicio, float tamanho, float velocidade, Color cor, float fracao,
        float ondulacao = 0f, float desvioMira = 0f, float raioExtra = 0f, bool explosao = false, bool bolha = false)
    {
        if (alvo == null || !alvo.Vivo) yield break;
        GameObject bola;
        if (bolha)
        {
            // Bolha: esfera translúcida com brilho suave.
            bola = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(bola.GetComponent<Collider>());
            bola.transform.localScale = Vector3.one * tamanho;
            var r = bola.GetComponent<Renderer>();
            r.material = Formas.MaterialSemLuz(new Color(cor.r, cor.g, cor.b, 0.45f), null, true);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var halo = EfeitosBatalha.PlacaSolta(Formas.Brilho(), new Color(cor.r, cor.g, cor.b, 0.4f), inicio, tamanho * 2.2f);
            halo.transform.SetParent(bola.transform, true);
        }
        else
        {
            // Energia: miolo quase branco + brilho na cor do ataque (aditivos, "acesos").
            bola = new GameObject();
            var brilho = EfeitosBatalha.PlacaSolta(Formas.Brilho(), new Color(cor.r, cor.g, cor.b, 0.95f), inicio, tamanho * 3f, aditivo: true);
            brilho.transform.SetParent(bola.transform, true);
            var miolo = EfeitosBatalha.PlacaSolta(Formas.Brilho(), new Color(1f, 1f, 1f, 0.95f), inicio, tamanho * 1.3f, aditivo: true);
            miolo.transform.SetParent(bola.transform, true);
        }
        bola.name = "Disparo_" + t.Nome;
        _disparos.Add(bola);
        if (!bolha)
            EfeitosBatalha.Emitir("Rastro", inicio, Quaternion.identity, new EfeitosBatalha.Particulas
            {
                Cor = cor, Tamanho = tamanho * 0.7f, Vida = 0.3f, Velocidade = tamanho * 0.5f, Taxa = 70f,
                Forma = ParticleSystemShapeType.Sphere, Raio = tamanho * 0.3f, Duracao = 3f, Ruido = tamanho * 0.5f
            }, bola.transform);

        bola.transform.position = inicio;
        var tempoVoo = Vector3.Distance(inicio, alvo.Centro()) / Mathf.Max(0.0001f, velocidade);
        var mira = alvo.Centro() + new Vector3(alvo._velocidade.x, 0f, alvo._velocidade.z) * tempoVoo * Esperteza
                   + Random.insideUnitSphere * desvioMira;
        var pulso = bola.transform.localScale;
        var direcao = (mira - inicio).normalized;
        var lado = Vector3.Cross(Vector3.up, direcao).normalized;
        var alcance = Vector3.Distance(inicio, mira) + _altura;
        var raioAcerto = tamanho * 0.5f + alvo._altura * 0.35f + raioExtra;
        var acertou = false;
        var andado = 0f;
        while (andado < alcance && alvo != null && alvo.Vivo && bola != null)
        {
            andado += velocidade * Time.deltaTime;
            var onda = ondulacao > 0f ? lado * Mathf.Sin(andado / Mathf.Max(0.0001f, ondulacao) * 1.5f) * ondulacao * 0.6f : Vector3.zero;
            bola.transform.position = inicio + direcao * andado + onda;
            if (!bolha) bola.transform.localScale = pulso * (1f + Mathf.Sin(Time.time * 35f) * 0.12f); // energia pulsando
            var perto = alvo.Centro() - bola.transform.position;
            if (perto.sqrMagnitude < raioAcerto * raioAcerto) { acertou = true; break; }
            yield return null;
        }
        var fim = bola != null ? bola.transform.position : mira;
        if (bola != null) { _disparos.Remove(bola); Destroy(bola); }
        if (explosao)
        {
            Explosao(fim, _altura * 1.1f, cor);
            AnelNoChao(new Vector3(fim.x, transform.position.y, fim.z), _altura * 0.3f, Perto * 4f, 0.45f, cor);
        }
        if (acertou) Acertar(alvo, t, fracao);
        else if (fracao >= 0.99f) Errou(alvo);
    }

    // ------------------------------------------------------------ Linha / cone

    /// <summary>Feixe contínuo em linha reta (Supreme Cannon, Hand of Fate...). Acerta quem estiver na linha.</summary>
    private IEnumerator EstiloRaio(Tecnica t, Color cor)
    {
        var carga = EfeitosBatalha.Carga(transform, PontoDisparoLocal(), _altura * 0.35f, cor, 0.45f);
        yield return new WaitForSeconds(0.45f);
        if (carga != null) Destroy(carga);
        if (!Pronto()) yield break;
        var a = Boca();
        var mira = inimigo.Centro() + new Vector3(inimigo._velocidade.x, 0f, inimigo._velocidade.z) * 0.1f * Esperteza;
        var dir = (mira - a).normalized;
        var b = mira + dir * _altura;
        var largura = _altura * (0.18f + 0.12f * Forca(t));
        EfeitosBatalha.Linha("Raio", new[] { a, b }, largura, cor, 0.5f);
        EfeitosBatalha.Linha("RaioMiolo", new[] { a, b }, largura * 0.35f, new Color(1f, 1f, 1f, 0.95f), 0.5f);
        EfeitosBatalha.Emitir("RaioFaiscas", b, Quaternion.identity, new EfeitosBatalha.Particulas
        {
            Cor = cor, Tamanho = _altura * 0.12f, Vida = 0.4f, Velocidade = _altura * 2.5f, Taxa = 120f, Rajada = 15,
            Forma = ParticleSystemShapeType.Sphere, Raio = _altura * 0.1f, Duracao = 0.4f
        });
        yield return new WaitForSeconds(0.08f);
        if (InimigoVivo && DistanciaAoSegmento(inimigo.Centro(), a, b) <= largura * 0.5f + inimigo._altura * 0.35f) Acertar(inimigo, t, 1f);
        else Errou(inimigo);
        yield return new WaitForSeconds(0.35f);
    }

    /// <summary>Jato em cone (Blue Blaster, sopros de fogo, gelo, gás). Acerta quem estiver dentro do cone.</summary>
    private IEnumerator EstiloSopro(Tecnica t, Color cor)
    {
        yield return new WaitForSeconds(0.25f);
        if (!Pronto()) yield break;
        var origem = Boca();
        var dir = inimigo.Centro() - origem;
        var alcance = Mathf.Max(dir.magnitude + _altura, Perto * 3f);
        dir.Normalize();
        var velocidade = alcance / 0.45f;
        EfeitosBatalha.Emitir("Sopro", origem, Quaternion.LookRotation(dir), new EfeitosBatalha.Particulas
        {
            Cor = cor, Tamanho = _altura * 0.28f, Vida = 0.45f, Velocidade = velocidade, Taxa = 180f,
            Forma = ParticleSystemShapeType.Cone, Angulo = 12f, Raio = _altura * 0.05f, Duracao = 0.65f,
            Crescer = true, Ruido = _altura * 0.25f
        });
        yield return new WaitForSeconds(0.35f);
        if (InimigoVivo)
        {
            var paraAlvo = inimigo.Centro() - origem;
            var dentro = paraAlvo.magnitude <= alcance && Vector3.Angle(dir, paraAlvo) <= 22f;
            if (dentro) Acertar(inimigo, t, 1f); else Errou(inimigo);
        }
        yield return new WaitForSeconds(0.35f);
    }

    /// <summary>Descarga elétrica em zigue-zague (Super Shocker, ataques de trovão).</summary>
    private IEnumerator EstiloRelampago(Tecnica t, Color cor)
    {
        EfeitosBatalha.Emitir("Estalos", Centro(), Quaternion.identity, new EfeitosBatalha.Particulas
        {
            Cor = cor, Tamanho = _altura * 0.08f, Vida = 0.2f, Velocidade = _altura * 1.5f, Taxa = 120f,
            Forma = ParticleSystemShapeType.Sphere, Raio = _altura * 0.55f, Duracao = 0.35f, Ruido = _altura
        });
        yield return new WaitForSeconds(0.35f);
        if (!Pronto()) yield break;
        var alvoPonto = inimigo.Centro();
        for (var i = 0; i < 2; i++)
        {
            var pontos = Ziguezague(Boca(), alvoPonto, 9, _altura * 0.28f);
            EfeitosBatalha.Linha("Relampago", pontos, _altura * 0.1f, cor, 0.22f);
            EfeitosBatalha.Linha("RelampagoMiolo", pontos, _altura * 0.035f, Color.white, 0.22f);
            yield return new WaitForSeconds(0.07f);
        }
        if (InimigoVivo && Vector3.Distance(inimigo.Centro(), alvoPonto) <= Perto * 1.2f) Acertar(inimigo, t, 1f);
        else Errou(inimigo);
        yield return new WaitForSeconds(0.25f);
    }

    // ------------------------------------------------------------ Área

    /// <summary>Coisas caindo do céu na área do alvo (meteoros, flechas, estrelas). Avisa no chão antes.</summary>
    private IEnumerator EstiloChuva(Tecnica t, Color cor)
    {
        EfeitosBatalha.Aura(transform, _altura, cor);
        if (!InimigoVivo) yield break;
        var centro = Chao(inimigo.transform.position + new Vector3(inimigo._velocidade.x, 0f, inimigo._velocidade.z) * 0.5f * Esperteza);
        var raio = Perto * 1.6f;
        AnelNoChao(centro, raio * 2f, raio * 2f, 0.8f, new Color(1f, 0.3f, 0.2f, 0.8f)); // aviso
        yield return new WaitForSeconds(0.6f);
        for (var i = 0; i < 6 && Vivo; i++)
        {
            var destino = centro + Random.insideUnitSphere.Plano() * raio;
            EfeitosBatalha.Rodar(Cair(t, destino, cor, 0.22f));
            yield return new WaitForSeconds(0.1f);
        }
        yield return new WaitForSeconds(0.4f);
    }

    private IEnumerator Cair(Tecnica t, Vector3 destino, Color cor, float fracao)
    {
        var tamanho = _altura * 0.18f;
        var inicio = destino + Vector3.up * _altura * 5f + transform.forward * _altura;
        var meteoro = EfeitosBatalha.PlacaSolta(Formas.Brilho(), Color.Lerp(cor, Color.white, 0.4f), inicio, tamanho * 2f);
        EfeitosBatalha.Emitir("Cauda", inicio, Quaternion.identity, new EfeitosBatalha.Particulas
        {
            Cor = cor, Tamanho = tamanho, Vida = 0.25f, Velocidade = 0f, Taxa = 80f,
            Forma = ParticleSystemShapeType.Sphere, Raio = tamanho * 0.2f, Duracao = 2f
        }, meteoro.transform);
        const float duracao = 0.35f;
        for (float k = 0; k < duracao && meteoro != null; k += Time.deltaTime)
        {
            meteoro.transform.position = Vector3.Lerp(inicio, destino, k / duracao);
            yield return null;
        }
        if (meteoro != null) Destroy(meteoro);
        Explosao(destino + Vector3.up * tamanho * 0.5f, _altura * 0.5f, cor);
        if (this != null && InimigoVivo && Dentro(inimigo, destino, Perto * 0.9f)) Acertar(inimigo, t, fracao);
    }

    /// <summary>Redemoinho que vai até o alvo, curvando um pouco atrás dele.</summary>
    private IEnumerator EstiloTornado(Tecnica t, Color cor)
    {
        yield return new WaitForSeconds(0.3f);
        if (!Pronto()) yield break;
        var tornado = new GameObject("Tornado_" + t.Nome);
        _disparos.Add(tornado);
        tornado.transform.position = Chao(transform.position + transform.forward * Perto);
        EfeitosBatalha.Emitir("Tornado", tornado.transform.position, Quaternion.Euler(-90f, 0f, 0f), new EfeitosBatalha.Particulas
        {
            Cor = cor, Tamanho = _altura * 0.3f, Vida = 0.6f, Velocidade = _altura * 2.2f, Taxa = 140f,
            Forma = ParticleSystemShapeType.Cone, Angulo = 25f, Raio = _altura * 0.25f, Duracao = 2.4f, Ruido = _altura * 0.6f, Crescer = true
        }, tornado.transform);
        var velocidade = Perto * 4.5f;
        var acertou = false;
        for (float k = 0; k < 2.5f && InimigoVivo && tornado != null; k += Time.deltaTime)
        {
            var para = Chao(inimigo.transform.position) - tornado.transform.position;
            para.y = 0;
            var dir = Vector3.RotateTowards(transform.forward, para.normalized, 1f, 0f);
            tornado.transform.position += (k < 0.1f ? dir : para.normalized * 0.6f + dir * 0.4f).normalized * velocidade * Time.deltaTime;
            if (Dentro(inimigo, tornado.transform.position, _altura * 0.45f)) { acertou = true; break; }
            yield return null;
        }
        if (tornado != null) { _disparos.Remove(tornado); Destroy(tornado, 0.6f); }
        if (acertou) Acertar(inimigo, t, 1f); else Errou(inimigo);
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>Coluna que explode do chão embaixo do alvo (luz, fogo, trevas, gelo). Avisa no chão antes.</summary>
    private IEnumerator EstiloPilar(Tecnica t, Color cor)
    {
        EfeitosBatalha.Aura(transform, _altura, cor);
        if (!InimigoVivo) yield break;
        var centro = Chao(inimigo.transform.position + new Vector3(inimigo._velocidade.x, 0f, inimigo._velocidade.z) * 0.4f * Esperteza);
        var raio = Perto * 1.1f;
        AnelNoChao(centro, raio * 2f, raio * 1.6f, 0.55f, cor);
        yield return new WaitForSeconds(0.55f);
        if (!Vivo) yield break;
        var topo = centro + Vector3.up * _altura * 5f;
        EfeitosBatalha.Linha("Pilar", new[] { centro, topo }, raio * 1.6f, cor, 0.6f);
        EfeitosBatalha.Linha("PilarMiolo", new[] { centro, topo }, raio * 0.6f, new Color(1f, 1f, 1f, 0.9f), 0.6f);
        EfeitosBatalha.Emitir("PilarSubindo", centro, Quaternion.Euler(-90f, 0f, 0f), new EfeitosBatalha.Particulas
        {
            Cor = cor, Tamanho = _altura * 0.3f, Vida = 0.6f, Velocidade = _altura * 7f, Taxa = 240f, Rajada = 30,
            Forma = ParticleSystemShapeType.Cone, Angulo = 6f, Raio = raio * 0.6f, Duracao = 0.5f
        });
        AnelNoChao(centro, raio, raio * 3f, 0.4f, cor);
        if (InimigoVivo && Dentro(inimigo, centro, raio)) Acertar(inimigo, t, 1f); else Errou(inimigo);
        yield return new WaitForSeconds(0.4f);
    }

    /// <summary>Onda de choque que se espalha pelo chão a partir do Digimon (rugidos, ondas sonoras, energia).</summary>
    private IEnumerator EstiloOnda(Tecnica t, Color cor)
    {
        yield return new WaitForSeconds(0.25f);
        if (!Pronto()) yield break;
        var centro = Chao(transform.position);
        var distancia = DistanciaAte(inimigo);
        var alcance = Mathf.Max(distancia + Perto, Perto * 3f);
        const float duracao = 0.45f;
        for (var i = 0; i < 3; i++) AnelNoChao(centro, _altura * 0.3f, alcance * 2f, duracao + i * 0.1f, cor);
        EfeitosBatalha.Emitir("Onda", centro + Vector3.up * _altura * 0.15f, Quaternion.Euler(90f, 0f, 0f), new EfeitosBatalha.Particulas
        {
            Cor = cor, Tamanho = _altura * 0.15f, Vida = duracao, Velocidade = alcance / duracao, Rajada = 60,
            Forma = ParticleSystemShapeType.Circle, Raio = _altura * 0.2f, Duracao = 0.1f
        });
        yield return new WaitForSeconds(duracao * Mathf.Clamp01(distancia / alcance));
        if (InimigoVivo && inimigo._estado != Estado.Esquivando && DistanciaAte(inimigo) <= alcance) Acertar(inimigo, t, 1f);
        else Errou(inimigo);
        yield return new WaitForSeconds(0.25f);
    }

    // ------------------------------------------------------------ De perto

    /// <summary>Soco, chute, cabeçada, mordida: o bote com impacto forte.</summary>
    private IEnumerator EstiloGolpe(Tecnica t, Color cor)
    {
        yield return Investida(0.3f, 0.8f);
        if (!Pronto()) yield break;
        if (DistanciaAte(inimigo) <= Perto * 1.5f)
        {
            EfeitosBatalha.Impacto(inimigo.Centro(), _altura * 0.6f, cor, true);
            Acertar(inimigo, t, 1f);
        }
        else Errou(inimigo);
    }

    /// <summary>Garra, espada, lâmina: o bote com cortes brilhando no alvo.</summary>
    private IEnumerator EstiloCorte(Tecnica t, Color cor)
    {
        yield return Investida(0.25f, 0.85f);
        if (!Pronto()) yield break;
        var alvo = inimigo.Centro();
        var cortes = t.Poder >= 90 ? 3 : 2;
        var acertou = DistanciaAte(inimigo) <= Perto * 1.5f;
        for (var i = 0; i < cortes; i++)
        {
            // Traço largo na cor do ataque + miolo branco (testado no Unity: só o fino quase não aparecia).
            var arco = Arco(alvo, _altura * 0.6f, i * 55f - 30f);
            EfeitosBatalha.Linha("Corte", arco, _altura * 0.16f, new Color(cor.r, cor.g, cor.b, 0.9f), 0.3f);
            EfeitosBatalha.Linha("CorteMiolo", arco, _altura * 0.06f, Color.white, 0.3f);
            EfeitosBatalha.Impacto(arco[arco.Length / 2], _altura * 0.35f, cor, false);
            yield return new WaitForSeconds(0.07f);
        }
        if (acertou && InimigoVivo) Acertar(inimigo, t, 1f); else Errou(inimigo);
    }

    /// <summary>O corpo todo avança atravessando o alvo (chifradas, broca giratória).</summary>
    private IEnumerator EstiloInvestida(Tecnica t, Color cor)
    {
        EfeitosBatalha.Emitir("Impulso", transform.position, Quaternion.Euler(-90f, 0f, 0f), new EfeitosBatalha.Particulas
        {
            Cor = cor, Tamanho = _altura * 0.15f, Vida = 0.35f, Velocidade = _altura * 1.5f, Taxa = 90f,
            Forma = ParticleSystemShapeType.Circle, Raio = _altura * 0.5f, Duracao = 0.3f
        });
        yield return new WaitForSeconds(0.3f);
        if (!Pronto()) yield break;
        var dir = inimigo.transform.position - transform.position;
        dir.y = 0;
        var distancia = dir.magnitude + Perto * 1.2f;
        dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : transform.forward;
        EfeitosBatalha.Emitir("Rastro", Centro(), Quaternion.identity, new EfeitosBatalha.Particulas
        {
            Cor = cor, Tamanho = _altura * 0.35f, Vida = 0.3f, Velocidade = _altura * 0.3f, Taxa = 160f,
            Forma = ParticleSystemShapeType.Sphere, Raio = _altura * 0.3f, Duracao = 0.4f
        }, transform);
        EfeitosBatalha.Poeira(transform.position, _altura * 0.7f);
        var velocidade = Perto * 14f;
        var acertou = false;
        for (var andado = 0f; andado < distancia && Vivo && _estado == Estado.Agindo; andado += velocidade * Time.deltaTime)
        {
            MoverPara(transform.position + dir * velocidade * Time.deltaTime);
            if (!acertou && InimigoVivo && DistanciaAte(inimigo) <= Perto * 0.9f) { acertou = true; Acertar(inimigo, t, 1f); }
            yield return null;
        }
        if (!acertou) Errou(inimigo);
    }

    /// <summary>Bate no chão: o chão treme em volta (quem estiver esquivando no alto escapa).</summary>
    private IEnumerator EstiloTremor(Tecnica t, Color cor)
    {
        yield return new WaitForSeconds(0.35f);
        if (!Pronto()) yield break;
        var centro = Chao(transform.position + transform.forward * Perto * 0.6f);
        var raio = Perto * 4.5f;
        AnelNoChao(centro, _altura * 0.3f, raio * 2f, 0.4f, cor);
        AnelNoChao(centro, _altura * 0.3f, raio * 1.4f, 0.3f, new Color(0.75f, 0.6f, 0.4f, 0.9f));
        EfeitosBatalha.Emitir("Pedras", centro, Quaternion.Euler(-90f, 0f, 0f), new EfeitosBatalha.Particulas
        {
            Cor = new Color(0.6f, 0.45f, 0.3f), Tamanho = _altura * 0.14f, Vida = 0.8f, Velocidade = _altura * 3f, Rajada = 40,
            Forma = ParticleSystemShapeType.Cone, Angulo = 40f, Raio = raio * 0.4f, Duracao = 0.1f, Gravidade = 1.5f
        });
        EfeitosBatalha.Poeira(centro, raio * 0.6f);
        yield return new WaitForSeconds(0.12f);
        if (InimigoVivo && inimigo._estado != Estado.Esquivando && Dentro(inimigo, centro, raio)) Acertar(inimigo, t, 1f);
        else Errou(inimigo);
        yield return new WaitForSeconds(0.25f);
    }

    // ------------------------------------------------------------ Peças

    /// <summary>Ainda dá para continuar o ataque? (não cambaleou, não caiu, o inimigo está vivo)</summary>
    private bool Pronto() => Vivo && _estado == Estado.Agindo && InimigoVivo;

    private Vector3 Boca() => transform.TransformPoint(PontoDisparoLocal() / Escala);

    private Vector3 Chao(Vector3 p) => new Vector3(p.x, transform.position.y, p.z);

    private void Acertar(CriaturaArena alvo, Tecnica t, float fracao)
    {
        if (alvo != null && alvo.Vivo && Vivo) alvo.ReceberGolpe(this, t, fracao);
    }

    private static void Errou(CriaturaArena alvo)
    {
        if (alvo != null && alvo.Vivo) TextoDeBatalha.Mostrar(alvo.Topo(), "Miss", TextoDeBatalha.CorDefesa, 48f);
    }

    private static bool Dentro(CriaturaArena alvo, Vector3 ponto, float raio)
    {
        if (alvo == null || !alvo.Vivo) return false;
        var d = alvo.transform.position - ponto;
        d.y = 0;
        return d.magnitude <= raio + alvo._altura * 0.25f;
    }

    private static float DistanciaAoSegmento(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
        return Vector3.Distance(p, a + ab * t);
    }

    private static Vector3[] Ziguezague(Vector3 a, Vector3 b, int partes, float amplitude)
    {
        var pontos = new Vector3[partes + 1];
        var dir = (b - a).normalized;
        var lado = Vector3.Cross(dir, Vector3.up).normalized;
        var cima = Vector3.Cross(lado, dir).normalized;
        for (var i = 0; i <= partes; i++)
        {
            var p = Vector3.Lerp(a, b, i / (float)partes);
            if (i > 0 && i < partes) p += lado * Random.Range(-amplitude, amplitude) + cima * Random.Range(-amplitude, amplitude) * 0.6f;
            pontos[i] = p;
        }
        return pontos;
    }

    /// <summary>Pontos de um corte em arco na frente do alvo (inclinado em <paramref name="angulo"/> graus).</summary>
    private Vector3[] Arco(Vector3 centro, float raio, float angulo)
    {
        var pontos = new Vector3[12];
        var giro = Quaternion.AngleAxis(angulo, transform.forward);
        for (var i = 0; i < pontos.Length; i++)
        {
            var a = Mathf.Lerp(-70f, 70f, i / (pontos.Length - 1f)) * Mathf.Deg2Rad;
            var local = transform.right * Mathf.Sin(a) * raio + Vector3.up * Mathf.Cos(a) * raio * 0.6f - transform.forward * raio * 0.3f;
            pontos[i] = centro + giro * local;
        }
        return pontos;
    }

    /// <summary>Explosão com partículas (esfera gigante, meteoros).</summary>
    private static void Explosao(Vector3 ponto, float tamanho, Color cor)
    {
        EfeitosBatalha.Impacto(ponto, tamanho, cor, true);
        EfeitosBatalha.Emitir("Explosao", ponto, Quaternion.identity, new EfeitosBatalha.Particulas
        {
            Cor = cor, Tamanho = tamanho * 0.3f, Vida = 0.5f, Velocidade = tamanho * 3f, Rajada = 45,
            Forma = ParticleSystemShapeType.Sphere, Raio = tamanho * 0.1f, Duracao = 0.1f, Ruido = tamanho * 0.3f
        });
    }

    /// <summary>Anel deitado no chão que se abre e some (onda, aviso de área).</summary>
    private static void AnelNoChao(Vector3 centro, float de, float para, float duracao, Color cor) =>
        EfeitosBatalha.Rodar(AbrirAnel(centro, de, para, duracao, cor));

    private static IEnumerator AbrirAnel(Vector3 centro, float de, float para, float duracao, Color cor)
    {
        var anel = EfeitosBatalha.PlacaSolta(Formas.Anel(), cor, centro + Vector3.up * 0.01f, de);
        Destroy(anel.GetComponent<OlharCamera>());
        anel.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var m = anel.GetComponent<Renderer>().material;
        for (float k = 0; k < duracao && anel != null; k += Time.deltaTime)
        {
            var p = k / duracao;
            anel.transform.localScale = Vector3.one * Mathf.Lerp(de, para, 1f - (1f - p) * (1f - p));
            var c = cor;
            c.a = cor.a * (1f - p);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            yield return null;
        }
        if (anel != null) Destroy(anel);
    }
}

/// <summary>Atalhos de vetor usados pelos estilos de ataque.</summary>
internal static class VetorExtensoes
{
    /// <summary>O vetor sem a altura (no plano do chão).</summary>
    public static Vector3 Plano(this Vector3 v) => new Vector3(v.x, 0f, v.z);
}
