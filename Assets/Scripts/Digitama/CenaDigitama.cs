using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Início do jogo no estilo Digimon World: o Tamer escolhe um entre vários Digitamas (cada ovo choca um
/// Digimon Fresh diferente), o ovo choca e o bebê vira o primeiro parceiro. Depois, vai para o mapa.
/// A cena é montada toda por código (câmera, luz, ninho, ovos e interface).
/// </summary>
public class CenaDigitama : MonoBehaviour
{
    private const float RaioCarrossel = 2.3f;

    private Camera _cam;
    private Transform _carrossel;
    private readonly List<Transform> _ovos = new List<Transform>();
    private int _atual;
    private float _anguloAlvo, _angulo;
    private bool _chocando;

    private RectTransform _raiz;
    private TMP_Text _titulo, _subtitulo, _descricao;
    private GameObject _controles, _confirmacao, _final;
    private Image _clarao;

    [Tooltip("Música de fundo (a mesma do mapa).")]
    [SerializeField] private AudioClip musica;
    [SerializeField, Range(0f, 1f)] private float volumeMusica = 0.5f;

    private Transform _anelLuz;
    private TMP_Text _contador, _textoConfirmacao;
    private bool _sorteando;

    // Paleta do mapa (tema "Original" do MapaLivre).
    private static readonly Color CorCeu = Hex("#3D6878");
    private static readonly Color CorChao = Hex("#78F898");
    private static readonly Color CorParque = Hex("#08A888");
    private static readonly Color CorRua = Hex("#58A090");
    private static readonly Color CorBorda = Hex("#F0F8C0");
    private static readonly Color CorPredio = Hex("#D8D0F8");

    private static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

    private void Start()
    {
        TelaSettings.AplicarSom();
        if (musica != null)
        {
            var som = gameObject.AddComponent<AudioSource>();
            som.clip = musica;
            som.loop = true;
            som.volume = volumeMusica;
            som.Play();
        }
        MontarMundo();
        MontarUI();
        Selecionar(0, true);
    }

    // ------------------------------------------------------------ Mundo 3D

    private void MontarMundo()
    {
        _cam = Camera.main;
        if (_cam == null)
        {
            _cam = new GameObject("Main Camera").AddComponent<Camera>();
            _cam.tag = "MainCamera";
        }
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = CorCeu;
        _cam.fieldOfView = 40f;
        _cam.transform.position = new Vector3(0f, 2.1f, -5.2f);
        _cam.transform.LookAt(new Vector3(0f, 0.15f, 0.9f));
        FundoDegrade();

        if (FindFirstObjectByType<Light>() == null)
        {
            var sol = new GameObject("Luz").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.intensity = 1.0f;
            sol.color = new Color(0.9f, 0.95f, 1f);
            sol.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }
        RenderSettings.ambientLight = new Color(0.6f, 0.66f, 0.7f);
        // Névoa na cor do céu do mapa: o chão some suavemente no horizonte.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = CorCeu;
        RenderSettings.fogStartDistance = 6f;
        RenderSettings.fogEndDistance = 16f;

        // Chão: o mesmo material do chão do mapa (verde com circuito).
        var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
        chao.name = "Chao";
        Destroy(chao.GetComponent<Collider>());
        chao.transform.position = new Vector3(0, 0f, RaioCarrossel);
        chao.transform.localScale = Vector3.one * 4f;
        chao.GetComponent<Renderer>().material = MaterialChaoDoMapa();

        // Rotatória como as do mapa: rua verde-petróleo com bordas creme e um parque no meio.
        var centro = new Vector3(0f, 0f, RaioCarrossel);
        Disco("Borda", centro, RaioCarrossel + 0.75f, 0f, 0.004f, CorBorda);
        Disco("Rua", centro, RaioCarrossel + 0.65f, RaioCarrossel - 0.65f, 0.008f, CorRua);
        Disco("BordaParque", centro, RaioCarrossel - 0.55f, 0f, 0.006f, CorBorda);
        Disco("Parque", centro, RaioCarrossel - 0.65f, 0f, 0.01f, CorParque);

        // Luz de destaque no ovo da frente e um anel de luz girando no chão embaixo dele.
        var foco = new GameObject("Foco").AddComponent<Light>();
        foco.type = LightType.Spot;
        foco.color = new Color(1f, 0.95f, 0.8f);
        foco.intensity = 2.5f; // fraco: com o cel shading, luz forte vira um círculo no ovo
        foco.range = 6f;
        foco.spotAngle = 40f;
        foco.transform.position = new Vector3(0f, 3.2f, -0.6f);
        foco.transform.LookAt(new Vector3(0f, 0.3f, 0f));
        var anel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(anel.GetComponent<Collider>());
        anel.name = "AnelDeLuz";
        anel.transform.position = new Vector3(0f, 0.012f, 0f);
        anel.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        anel.transform.localScale = Vector3.one * 1.25f;
        anel.GetComponent<Renderer>().material = Formas.MaterialSemLuz(new Color(CorBorda.r, CorBorda.g, CorBorda.b, 0.95f), Formas.Anel().texture, true);
        _anelLuz = anel.transform;

        // Dados flutuando (partículas subindo devagar).
        Particulas();

        _carrossel = new GameObject("Carrossel").transform;
        _carrossel.position = new Vector3(0, 0, RaioCarrossel);
        var n = Digitamas.Todos.Length;
        var malhaNinho = Ninho();
        var matNinho = Material(new Color(0.55f, 0.38f, 0.2f));
        for (var i = 0; i < n; i++)
        {
            var apoio = new GameObject("Apoio_" + i).transform;
            apoio.SetParent(_carrossel, false);
            var ang = i * 360f / n;
            apoio.localPosition = Quaternion.Euler(0, ang, 0) * new Vector3(0, 0, -RaioCarrossel);
            // Ninho em anel; o ovo fica apoiado dentro dele.
            var ninho = new GameObject("Ninho");
            ninho.transform.SetParent(apoio, false);
            ninho.AddComponent<MeshFilter>().sharedMesh = malhaNinho;
            ninho.AddComponent<MeshRenderer>().sharedMaterial = matNinho;
            var ovo = Digitamas.Criar(Digitamas.Todos[i], apoio);
            ovo.transform.localScale = Vector3.one * 0.55f;
            ovo.transform.localPosition = Vector3.up * 0.04f;
            _ovos.Add(ovo.transform);
        }
    }

    /// <summary>Fundo em degradê na cor do céu do mapa (mais escuro no alto) atrás de tudo.</summary>
    private void FundoDegrade()
    {
        var go = new GameObject("Fundo", typeof(Canvas));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceCamera;
        c.worldCamera = _cam;
        c.planeDistance = _cam.farClipPlane * 0.95f;
        c.sortingOrder = -100;
        var img = new GameObject("Degrade", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        img.transform.SetParent(go.transform, false);
        FigmaUI.Esticar(img.rectTransform);
        var t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (var y = 0; y < 64; y++)
            t.SetPixel(0, y, Color.Lerp(CorCeu, new Color(CorCeu.r * 0.55f, CorCeu.g * 0.55f, CorCeu.b * 0.55f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(56f, 63f, y))));
        t.Apply();
        img.texture = t;
        img.raycastTarget = false;
    }

    /// <summary>Cópia do material do chão do mapa, com o circuito num tamanho que aparece bem de perto.</summary>
    private static Material MaterialChaoDoMapa()
    {
        var tema = Resources.Load<Niantic.Lightship.Maps.MapaLivreTheme>("MapaLivre/TemaOriginal");
        if (tema == null || tema.Ground == null) return Material(CorChao);
        var m = new Material(tema.Ground);
        var escala = new Vector2(6f, 6f);
        if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", escala);
        m.mainTextureScale = escala;
        return m;
    }

    /// <summary>Disco (ou anel, se raioDentro > 0) achatado no chão.</summary>
    private static void Disco(string nome, Vector3 centro, float raioFora, float raioDentro, float altura, Color cor)
    {
        const int lados = 96;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (var i = 0; i <= lados; i++)
        {
            var a = i / (float)lados * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            verts.Add(d * raioDentro);
            verts.Add(d * raioFora);
        }
        for (var i = 0; i < lados; i++)
        {
            var p = i * 2;
            tris.Add(p); tris.Add(p + 3); tris.Add(p + 1);
            tris.Add(p); tris.Add(p + 2); tris.Add(p + 3);
        }
        var m = new Mesh { name = nome };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        var go = new GameObject(nome);
        go.transform.position = centro + Vector3.up * altura;
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>().sharedMaterial = Material(cor);
    }

    /// <summary>Ninho em forma de anel (toro), com a parte de cima mais grossa, como palha trançada.</summary>
    private static Mesh Ninho()
    {
        const int voltas = 32, gomos = 12;
        const float raio = 0.36f, grossura = 0.07f;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (var i = 0; i <= voltas; i++)
        {
            var a = i / (float)voltas * Mathf.PI * 2f;
            var centro = new Vector3(Mathf.Cos(a) * raio, grossura, Mathf.Sin(a) * raio);
            var fora = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (var j = 0; j <= gomos; j++)
            {
                var b = j / (float)gomos * Mathf.PI * 2f;
                // Um pouco irregular, como galhos.
                var g = grossura * (1f + 0.12f * Mathf.Sin(a * 9f + b * 3f));
                verts.Add(centro + fora * Mathf.Cos(b) * g + Vector3.up * Mathf.Sin(b) * g);
            }
        }
        for (var i = 0; i < voltas; i++)
        for (var j = 0; j < gomos; j++)
        {
            var p = i * (gomos + 1) + j;
            var q = p + gomos + 1;
            tris.Add(p); tris.Add(p + 1); tris.Add(q);
            tris.Add(p + 1); tris.Add(q + 1); tris.Add(q);
        }
        var m = new Mesh { name = "Ninho" };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Pontinhos de luz subindo devagar (dados do Digital World).</summary>
    private void Particulas()
    {
        var go = new GameObject("Dados");
        go.transform.position = new Vector3(0f, 0f, RaioCarrossel);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 6f;
        main.startSpeed = 0.25f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(CorBorda.r, CorBorda.g, CorBorda.b, 0.85f), new Color(CorPredio.r, CorPredio.g, CorPredio.b, 0.85f));
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emissao = ps.emission;
        emissao.rateOverTime = 14f;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Circle;
        forma.radius = 5f;
        forma.rotation = new Vector3(-90f, 0f, 0f);
        var cor = ps.colorOverLifetime;
        cor.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        cor.color = g;
        go.GetComponent<ParticleSystemRenderer>().material = Formas.MaterialSemLuz(Color.white, Formas.Brilho().texture, true);
    }

    private static Material Material(Color cor)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cor);
        m.color = cor;
        return m;
    }

    private void Update()
    {
        if (_anelLuz != null)
        {
            _anelLuz.rotation = Quaternion.Euler(90f, Time.time * 40f, 0f);
            _anelLuz.localScale = Vector3.one * (1.2f + Mathf.Sin(Time.time * 3f) * 0.06f);
        }
        // Carrossel gira até o ovo escolhido ficar na frente.
        _angulo = Mathf.LerpAngle(_angulo, _anguloAlvo, Time.deltaTime * 6f);
        _carrossel.rotation = Quaternion.Euler(0, _angulo, 0);
        for (var i = 0; i < _ovos.Count; i++)
        {
            if (_ovos[i] == null) continue;
            var escolhido = i == _atual;
            var alvo = escolhido ? 0.62f : 0.48f;
            var s = Mathf.Lerp(_ovos[i].localScale.x, alvo, Time.deltaTime * 6f);
            _ovos[i].localScale = Vector3.one * s;
            if (!_chocando)
            {
                // Balança de leve (o escolhido balança mais) e gira para mostrar a estampa.
                var balanco = Mathf.Sin(Time.time * (escolhido ? 3f : 1.5f) + i) * (escolhido ? 6f : 2f);
                _ovos[i].localRotation = Quaternion.Euler(0, Time.time * 20f + i * 70f, balanco);
            }
        }
        if (!_chocando && !_sorteando) Toques();
    }

    private Vector2 _inicioArraste;
    private bool _arrastando;

    private void Toques()
    {
        var sobreUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (Input.touchCount > 0 && EventSystem.current != null) sobreUI = EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        if (Input.GetMouseButtonDown(0) && !sobreUI) { _inicioArraste = Input.mousePosition; _arrastando = true; }
        if (!Input.GetMouseButtonUp(0) || !_arrastando) return;
        _arrastando = false;
        var delta = (Vector2)Input.mousePosition - _inicioArraste;
        if (Mathf.Abs(delta.x) > Screen.width * 0.08f)
        {
            Selecionar(_atual + (delta.x < 0 ? 1 : -1));
            return;
        }
        // Toque num ovo: escolhe ele.
        if (Physics.Raycast(_cam.ScreenPointToRay(Input.mousePosition), out var hit, 50f))
        {
            var i = _ovos.IndexOf(hit.collider.transform);
            if (i >= 0) Selecionar(i);
        }
    }

    public void Selecionar(int i, bool imediato = false)
    {
        var n = Digitamas.Todos.Length;
        _atual = (i % n + n) % n;
        // Ângulo que traz o ovo para a frente (ângulo contínuo, sem dar a volta inteira).
        var alvo = -_atual * 360f / n;
        _anguloAlvo = _angulo + Mathf.DeltaAngle(_angulo, alvo);
        if (imediato) _angulo = _anguloAlvo;
        _descricao.text = NomeDoFresh(Digitamas.Todos[_atual].Fresh);
        if (_contador != null) _contador.text = $"Digitama {_atual + 1} de {n}";
    }

    private static string NomeDoFresh(string especie)
    {
        var ficha = CatalogoDigimon.Buscar(especie);
        return ficha != null && !string.IsNullOrWhiteSpace(ficha.Name) ? ficha.Name.Trim() : especie;
    }

    // ------------------------------------------------------------ Interface

    private void MontarUI()
    {
        _raiz = (RectTransform)FigmaUI.Canvas(transform, "UI_Digitama", 100).transform;
        _titulo = FigmaUI.TextoEm(_raiz, 0, 140f, FigmaUI.Largura, 100f, "Escolha seu Digitama", 76f, FigmaUI.Peso.Negrito);
        _subtitulo = FigmaUI.TextoEm(_raiz, 60f, 250f, 909f, 120f, "Cada ovo guarda um Digimon diferente.\nEle será o seu primeiro parceiro.", 36f, FigmaUI.Peso.Normal);
        _subtitulo.textWrappingMode = TextWrappingModes.Normal;

        _controles = new GameObject("Controles", typeof(RectTransform));
        _controles.transform.SetParent(_raiz, false);
        FigmaUI.Esticar((RectTransform)_controles.transform);
        // Painel de vidro com o nome do Digimon do ovo, o contador e as setas.
        var vidro = FigmaUI.CaixaVidro(_controles.transform, 50f, 1430f, 929f, 330f);
        // Fundo escuro (azul-petróleo do céu do mapa) para o texto aparecer sobre o chão verde.
        var fundoPainel = FigmaUI.Imagem(FigmaUI.Ret(_controles.transform, "FundoPainel", 50f, 1430f, 929f, 330f), FigmaUI.Arredondado(), new Color(CorCeu.r * 0.55f, CorCeu.g * 0.55f, CorCeu.b * 0.55f, 0.85f));
        fundoPainel.type = Image.Type.Sliced;
        fundoPainel.raycastTarget = false;
        vidro.SetAsLastSibling();
        _contador = FigmaUI.TextoEm(_controles.transform, 50f, 1455f, 929f, 50f, "", 34f, FigmaUI.Peso.Normal);
        _contador.color = new Color(1f, 1f, 1f, 0.8f);
        _descricao = FigmaUI.TextoEm(_controles.transform, 300f, 1530f, 429f, 160f, "", 58f, FigmaUI.Peso.Negrito);
        _descricao.textWrappingMode = TextWrappingModes.NoWrap;
        _descricao.enableAutoSizing = true;
        _descricao.fontSizeMin = 30f;
        _descricao.fontSizeMax = 58f;
        Seta(_controles.transform, 70f, "<", -1);
        Seta(_controles.transform, FigmaUI.Largura - 70f - 230f, ">", 1);
        FigmaUI.BotaoLargo(_controles.transform, 87f, 1800f, "Escolher este ovo", Confirmar);
        // Destino: a roleta gira e para num ovo qualquer.
        FigmaUI.BotaoBack(_controles.transform, (FigmaUI.Largura - 395f) / 2f, 1965f, () => StartCoroutine(Destino()), "Destino");

        _confirmacao = new GameObject("Confirmacao", typeof(RectTransform));
        _confirmacao.transform.SetParent(_raiz, false);
        FigmaUI.Esticar((RectTransform)_confirmacao.transform);
        var fundoConfirmacao = FigmaUI.Imagem(FigmaUI.Ret(_confirmacao.transform, "FundoPainel", 50f, 1430f, 929f, 420f), FigmaUI.Arredondado(), new Color(CorCeu.r * 0.55f, CorCeu.g * 0.55f, CorCeu.b * 0.55f, 0.85f));
        fundoConfirmacao.type = Image.Type.Sliced;
        fundoConfirmacao.raycastTarget = false;
        _textoConfirmacao = FigmaUI.TextoEm(_confirmacao.transform, 60f, 1460f, 909f, 180f, "", 52f, FigmaUI.Peso.Negrito);
        _textoConfirmacao.textWrappingMode = TextWrappingModes.Normal;
        FigmaUI.BotaoBack(_confirmacao.transform, 107f, 1680f, () => StartCoroutine(Chocar()), "Sim");
        FigmaUI.BotaoBack(_confirmacao.transform, 527f, 1680f, () => { _confirmacao.SetActive(false); _controles.SetActive(true); }, "Não");
        _confirmacao.SetActive(false);

        _final = new GameObject("Final", typeof(RectTransform));
        _final.transform.SetParent(_raiz, false);
        FigmaUI.Esticar((RectTransform)_final.transform);
        _final.SetActive(false);

        var clarao = FigmaUI.Ret(_raiz, "Clarao", 0, 0, 1, 1);
        FigmaUI.Esticar(clarao);
        _clarao = FigmaUI.Imagem(clarao, null, new Color(1, 1, 1, 0));
    }

    private void Seta(Transform pai, float x, string rotulo, int passo)
    {
        var b = FigmaUI.BotaoBack(pai, x, 1545f, () => Selecionar(_atual + passo), rotulo, 230f / 395f);
        b.GetComponentInChildren<TMP_Text>().fontSize = 60f;
    }

    public void Confirmar() => Confirmar("Chocar este Digitama?");

    private void Confirmar(string pergunta)
    {
        _textoConfirmacao.text = pergunta;
        _controles.SetActive(false);
        _confirmacao.SetActive(true);
    }

    /// <summary>Roleta: os ovos passam cada vez mais devagar até parar num ovo sorteado.</summary>
    public IEnumerator Destino()
    {
        if (_sorteando || _chocando) yield break;
        _sorteando = true;
        _controles.SetActive(false);
        var n = Digitamas.Todos.Length;
        var sorteado = Random.Range(0, n);
        // Pelo menos duas voltas inteiras antes de parar no sorteado.
        var passos = n * 2 + ((sorteado - _atual) % n + n) % n;
        for (var i = 1; i <= passos; i++)
        {
            Selecionar(_atual + 1);
            var t = i / (float)passos;
            yield return new WaitForSeconds(Mathf.Lerp(0.05f, 0.42f, t * t * t));
        }
        yield return new WaitForSeconds(0.6f);
        _sorteando = false;
        Confirmar($"O destino escolheu {NomeDoFresh(Digitamas.Todos[_atual].Fresh)}!\nChocar este Digitama?");
    }

    // ------------------------------------------------------------ Chocar

    public IEnumerator Chocar()
    {
        _chocando = true;
        _confirmacao.SetActive(false);
        _titulo.text = "";
        _subtitulo.gameObject.SetActive(false);
        var tipo = Digitamas.Todos[_atual];
        var ovo = _ovos[_atual];

        // Os outros ovos somem; o escolhido fica no centro.
        for (var i = 0; i < _ovos.Count; i++)
            if (i != _atual) _ovos[i].parent.gameObject.SetActive(false);
        ovo.localRotation = Quaternion.identity;

        // Balança cada vez mais forte (três vezes), como nos V-Pets.
        for (var vez = 1; vez <= 3; vez++)
        {
            for (float t = 0; t < 0.9f; t += Time.deltaTime)
            {
                ovo.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 30f) * 7f * vez);
                yield return null;
            }
            ovo.localRotation = Quaternion.identity;
            yield return new WaitForSeconds(0.5f);
        }

        // Estica, achata e... clarão!
        var escala = ovo.localScale;
        for (float t = 0; t < 1f; t += Time.deltaTime * 3f)
        {
            ovo.localScale = new Vector3(escala.x * (1f + t * 0.25f), escala.y * (1f - t * 0.2f), escala.z * (1f + t * 0.25f));
            _clarao.color = new Color(1, 1, 1, t);
            yield return null;
        }
        _clarao.color = Color.white;
        var lugar = ovo.parent.position;
        ovo.gameObject.SetActive(false);

        // Nasce o Digimon Fresh.
        var ficha = CatalogoDigimon.Buscar(tipo.Fresh);
        GameObject bebe = null;
        if (ficha != null && ficha.Prefab != null)
        {
            bebe = Instantiate(ficha.Prefab);
            foreach (var c in bebe.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var ps in bebe.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            var h = DigimonVisual.Altura(bebe);
            if (h > 0.0001f) bebe.transform.localScale *= 0.75f / h;
            bebe.transform.position = lugar + Vector3.up * 0.05f;
            var olhar = _cam.transform.position - lugar;
            olhar.y = 0;
            bebe.transform.rotation = Quaternion.LookRotation(olhar);
            var anim = bebe.GetComponentInChildren<Animator>();
            if (anim != null) { anim.enabled = true; anim.applyRootMotion = false; }
        }
        yield return new WaitForSeconds(0.25f);
        for (float t = 1; t > 0f; t -= Time.deltaTime * 1.2f)
        {
            _clarao.color = new Color(1, 1, 1, t);
            yield return null;
        }
        _clarao.color = new Color(1, 1, 1, 0);

        // Pulinho de alegria.
        if (bebe != null)
        {
            var base0 = bebe.transform.position;
            for (var p = 0; p < 2; p++)
            for (float t = 0; t < 1f; t += Time.deltaTime * 3f)
            {
                bebe.transform.position = base0 + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.25f;
                yield return null;
            }
            bebe.transform.position = base0;
        }

        Registrar(tipo.Fresh);
        var nome = ficha != null ? ficha.Name.Trim() : tipo.Fresh;
        _final.SetActive(true);
        FigmaUI.TextoEm(_final.transform, 0, 160f, FigmaUI.Largura, 100f, $"{nome} nasceu!", 80f, FigmaUI.Peso.Negrito);
        FigmaUI.TextoEm(_final.transform, 60f, 1480f, 909f, 220f,
            "Este é o seu novo parceiro.\nAlimente, treine e dê carinho: o jeito como você cuida decide em quem ele vai digivolver.",
            38f, FigmaUI.Peso.Normal).textWrappingMode = TextWrappingModes.Normal;
        FigmaUI.BotaoLargo(_final.transform, 87f, 1800f, "Começar a aventura", () => SceneManager.LoadScene(Cenas.Mapa));
    }

    /// <summary>O bebê entra na coleção como parceiro e o início fica marcado como feito.</summary>
    private static void Registrar(string especie)
    {
        var p = PerfilJogador.Atual;
        var d = p.AdicionarNaColecao(especie, 1);
        d.fome = 60f;
        d.humor = 80f;
        d.energia = 100f;
        p.parceiroId = d.id;
        p.inicioFeito = true;
        p.Salvar();
    }
}
