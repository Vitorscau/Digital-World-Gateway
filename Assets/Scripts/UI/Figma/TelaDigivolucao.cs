using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sequência clássica de digivolução (estilo anime Adventure/02):
/// 1. o Digimon aparece e o fundo escurece: "X digivolve para...";
/// 2. fundo de digivolução: raios de luz girando na cor do elemento, colunas de código digital caindo,
///    anéis de dados saindo do Digimon e um brilho pulsando atrás da silhueta;
/// 3. as silhuetas da forma antiga e da nova se alternam cada vez mais rápido, crescendo;
/// 4. explosão branca com faíscas e o novo Digimon aparece com o nome em destaque.
/// Ao terminar, a espécie do parceiro já mudou.
/// </summary>
public class TelaDigivolucao : TelaFigma
{
    private DigimonSalvo _d;
    private DigimonBase _velha, _nova;
    private Action<DigimonBase> _aoTerminar;
    private RetratoVivo _retratoVelho, _retratoNovo;

    private RawImage _imagem;
    private Image _escuro, _raiosA, _raiosB, _brilho, _clarao;
    private RectTransform _efeitos;
    private TMP_Text _texto, _nome;
    private readonly List<(RectTransform rt, Image img, float atraso)> _aneis = new List<(RectTransform, Image, float)>();
    private readonly List<(RectTransform rt, TMP_Text txt, float vel)> _codigo = new List<(RectTransform, TMP_Text, float)>();
    private Color _corElemento = Color.cyan;
    private float _intensidade; // 0 = sem efeitos, 1 = digivolução a todo vapor
    private float _inicioEfeito;

    private const float CentroY = 780f; // centro do Digimon na tela (coordenadas do Figma)

    /// <summary>Só uma digivolução por vez (evita abrir duas se o perfil mudar no meio).</summary>
    public static bool Acontecendo { get; private set; }

    public static void Abrir(DigimonSalvo d, DigimonBase nova, Action<DigimonBase> aoTerminar = null)
    {
        if (d == null || nova == null || Acontecendo) return;
        var tela = Abrir<TelaDigivolucao>();
        tela._d = d;
        tela._velha = d.Ficha;
        tela._nova = nova;
        tela._aoTerminar = aoTerminar;
        tela._corElemento = CorDoElemento(nova.Atribute);
        Acontecendo = true;
        tela.StartCoroutine(tela.Animar());
    }

    protected override void Construir()
    {
        FigmaUI.Fundo(Raiz);
        _escuro = Tela("Escuro", new Color(0.02f, 0.03f, 0.12f, 0f));

        // Camada de efeitos (atrás do Digimon).
        _efeitos = FigmaUI.Ret(Raiz, "Efeitos", 0, 0, 1, 1);
        FigmaUI.Esticar(_efeitos);
        _raiosA = Centralizada("RaiosA", Formas.Raios(), 2400f, CentroY);
        _raiosB = Centralizada("RaiosB", Formas.Raios(), 1700f, CentroY);
        for (var i = 0; i < 26; i++) Coluna(i);
        for (var i = 0; i < 4; i++)
        {
            var anel = Centralizada("Anel", Formas.Anel(), 300f, CentroY);
            _aneis.Add((anel.rectTransform, anel, i * 0.35f));
        }
        _brilho = Centralizada("Brilho", Formas.Brilho(), 950f, CentroY);

        // Digimon.
        _imagem = FigmaUI.Ret(Raiz, "Digimon", 164.5f, CentroY - 350f, 700f, 700f).gameObject.AddComponent<RawImage>();
        _imagem.raycastTarget = false;
        _imagem.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _imagem.rectTransform.anchoredPosition += new Vector2(350f, -350f);

        FigmaUI.TextoEm(Raiz, 0, 140f, FigmaUI.Largura, 90f, "Digivolution", 80f, FigmaUI.Peso.Negrito);
        _texto = FigmaUI.TextoEm(Raiz, 60f, 1260f, 909f, 140f, "", 54f, FigmaUI.Peso.Negrito);
        _texto.textWrappingMode = TextWrappingModes.Normal;
        _nome = FigmaUI.TextoEm(Raiz, 0, 1400f, FigmaUI.Largura, 150f, "", 110f, FigmaUI.Peso.Negrito);
        _clarao = Tela("Clarao", new Color(1, 1, 1, 0));
        DefinirIntensidade(0f);
    }

    // ------------------------------------------------------------ Peças

    private Image Tela(string nome, Color cor)
    {
        var rt = FigmaUI.Ret(Raiz, nome, 0, 0, 1, 1);
        FigmaUI.TelaCheia(rt);
        return FigmaUI.Imagem(rt, null, cor);
    }

    private Image Centralizada(string nome, Sprite s, float lado, float cy)
    {
        var rt = FigmaUI.Ret(_efeitos, nome, 0, 0, lado, lado);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -cy);
        return FigmaUI.Imagem(rt, s);
    }

    private const string Glifos = "0123456789ABCDEF<>/\\|=+*#";

    private void Coluna(int i)
    {
        var x = (i + 0.5f) * FigmaUI.Largura / 26f - 20f;
        var t = FigmaUI.TextoEm(_efeitos, x, -400f, 40f, 1400f, TextoAleatorio(18), 34f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Top, false);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.lineSpacing = -20f;
        _codigo.Add((t.rectTransform, t, UnityEngine.Random.Range(250f, 700f)));
    }

    private static string TextoAleatorio(int n)
    {
        var c = new char[n * 2];
        for (var i = 0; i < n; i++)
        {
            c[i * 2] = Glifos[UnityEngine.Random.Range(0, Glifos.Length)];
            c[i * 2 + 1] = '\n';
        }
        return new string(c);
    }

    private void DefinirIntensidade(float v)
    {
        _intensidade = v;
        _efeitos.gameObject.SetActive(v > 0.001f);
    }

    // ------------------------------------------------------------ Movimento dos efeitos

    private void Update()
    {
        if (_intensidade <= 0.001f) return;
        var t = Time.unscaledTime - _inicioEfeito;
        var dt = Time.unscaledDeltaTime;
        var cor = _corElemento;

        _raiosA.rectTransform.localEulerAngles = new Vector3(0, 0, t * 25f);
        _raiosB.rectTransform.localEulerAngles = new Vector3(0, 0, -t * 40f);
        _raiosA.color = new Color(cor.r, cor.g, cor.b, 0.55f * _intensidade);
        _raiosB.color = new Color(Mathf.Lerp(cor.r, 1f, 0.6f), Mathf.Lerp(cor.g, 1f, 0.6f), Mathf.Lerp(cor.b, 1f, 0.6f), 0.35f * _intensidade);

        // Brilho pulsando atrás da silhueta.
        var pulso = 1f + Mathf.Sin(t * 9f) * 0.08f;
        _brilho.rectTransform.localScale = Vector3.one * pulso * (0.8f + _intensidade * 0.4f);
        _brilho.color = new Color(1f, 1f, 1f, 0.9f * _intensidade);

        // Anéis de dados saindo do centro, um atrás do outro.
        foreach (var (rt, img, atraso) in _aneis)
        {
            var k = Mathf.Repeat((t - atraso) * 0.7f, 1f);
            rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 5f, k);
            img.color = new Color(Mathf.Lerp(cor.r, 1f, 0.5f), Mathf.Lerp(cor.g, 1f, 0.5f), Mathf.Lerp(cor.b, 1f, 0.5f), (1f - k) * _intensidade);
        }

        // Código digital caindo.
        foreach (var (rt, txt, vel) in _codigo)
        {
            var p = rt.anchoredPosition;
            p.y -= vel * dt * (0.6f + _intensidade);
            if (p.y < -2600f) { p.y = 500f; txt.text = TextoAleatorio(18); }
            rt.anchoredPosition = p;
            txt.color = new Color(Mathf.Lerp(cor.r, 0.6f, 0.4f), Mathf.Lerp(cor.g, 1f, 0.4f), Mathf.Lerp(cor.b, 1f, 0.4f), 0.35f * _intensidade);
        }
    }

    // ------------------------------------------------------------ Sequência

    private static string Nome(DigimonBase f) => f != null ? f.Name.Trim() : "?";

    private IEnumerator Animar()
    {
        _retratoVelho = RetratoVivo.Criar(_velha, 512, true);
        _retratoNovo = RetratoVivo.Criar(_nova, 512, true);
        yield return null; // um quadro para as câmeras enquadrarem

        // 1. Começo: o Digimon brilha e o fundo escurece.
        Mostrar(_retratoVelho, Color.white, 1f);
        _texto.text = $"{Nome(_velha)} is digivolving into...";
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 1.2f)
        {
            _escuro.color = new Color(0.02f, 0.03f, 0.12f, t * 0.92f);
            yield return null;
        }

        // 2. Fundo de digivolução liga.
        _inicioEfeito = Time.unscaledTime;
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.6f)
        {
            DefinirIntensidade(t);
            _clarao.color = new Color(1, 1, 1, (1f - t) * 0.6f);
            yield return null;
        }
        DefinirIntensidade(1f);
        _clarao.color = new Color(1, 1, 1, 0);

        // 3. Silhuetas alternando, cada vez mais rápido e maiores.
        var intervalo = 0.4f;
        for (var i = 0; i < 20; i++)
        {
            var nova = i % 2 == 0;
            var escala = 0.9f + i * 0.015f;
            Mostrar(nova ? _retratoNovo : _retratoVelho, Color.black, escala);
            yield return new WaitForSecondsRealtime(intervalo);
            intervalo = Mathf.Max(0.04f, intervalo * 0.84f);
        }

        // 4. Explosão branca: no auge, a espécie muda.
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime * 5f)
        {
            _clarao.color = new Color(1, 1, 1, t);
            yield return null;
        }
        Digivolucao.Aplicar(_d, _nova);
        Mostrar(_retratoNovo, Color.white, 1.1f);
        _texto.text = "";
        Faiscas(28);
        for (float t = 1; t > 0f; t -= Time.unscaledDeltaTime * 1.2f)
        {
            _clarao.color = new Color(1, 1, 1, t);
            _imagem.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.25f, t);
            yield return null;
        }
        _clarao.color = new Color(1, 1, 1, 0);

        // O nome "bate" na tela, como no anime.
        _nome.text = $"{Nome(_nova)}!";
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime * 4f)
        {
            _nome.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, t);
            _nome.alpha = t;
            yield return null;
        }
        _nome.rectTransform.localScale = Vector3.one;
        _texto.text = $"{Nome(_velha)} digivolved into";
        yield return new WaitForSecondsRealtime(0.6f);

        // Os efeitos acalmam e aparece o OK.
        for (float t = 1; t > 0.35f; t -= Time.unscaledDeltaTime)
        {
            DefinirIntensidade(t);
            yield return null;
        }
        FigmaUI.BotaoBack(Raiz, (FigmaUI.Largura - 395f) / 2f, 1720f, Concluir, "OK");
    }

    private void Mostrar(RetratoVivo r, Color cor, float escala)
    {
        _imagem.texture = r != null ? r.Textura : null;
        _imagem.color = cor;
        _imagem.rectTransform.localScale = Vector3.one * escala;
    }

    /// <summary>Faíscas brancas voando para fora do centro.</summary>
    private void Faiscas(int quantas)
    {
        for (var i = 0; i < quantas; i++) StartCoroutine(Faisca());
    }

    private IEnumerator Faisca()
    {
        var rt = FigmaUI.Ret(Raiz, "Faisca", 0, 0, 30f, 30f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -CentroY);
        var img = FigmaUI.Imagem(rt, Formas.Brilho(), Color.white);
        var dir = UnityEngine.Random.insideUnitCircle.normalized;
        var vel = UnityEngine.Random.Range(700f, 1600f);
        var tam = UnityEngine.Random.Range(20f, 70f);
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime * 1.4f)
        {
            rt.anchoredPosition = new Vector2(0f, -CentroY) + dir * vel * t;
            rt.sizeDelta = Vector2.one * tam * (1f - t * 0.5f);
            img.color = new Color(1f, 1f, Mathf.Lerp(1f, 0.6f, t), 1f - t);
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    private static Color CorDoElemento(DigimonAttribute a)
    {
        switch (a)
        {
            case DigimonAttribute.Fire: return new Color(1f, 0.45f, 0.1f);
            case DigimonAttribute.Water: return new Color(0.25f, 0.65f, 1f);
            case DigimonAttribute.Plant: return new Color(0.35f, 0.9f, 0.3f);
            case DigimonAttribute.Electric: return new Color(1f, 0.9f, 0.2f);
            case DigimonAttribute.Earth: return new Color(0.85f, 0.6f, 0.3f);
            case DigimonAttribute.Wind: return new Color(0.5f, 1f, 0.85f);
            case DigimonAttribute.Light: return new Color(1f, 0.95f, 0.6f);
            case DigimonAttribute.Dark: return new Color(0.6f, 0.25f, 0.9f);
            default: return new Color(0.3f, 0.85f, 1f);
        }
    }

    private void Concluir()
    {
        var nova = _nova;
        var aoTerminar = _aoTerminar;
        Fechar();
        aoTerminar?.Invoke(nova);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Acontecendo = false;
        if (_retratoVelho != null) Destroy(_retratoVelho.gameObject);
        if (_retratoNovo != null) Destroy(_retratoNovo.gameObject);
    }
}
