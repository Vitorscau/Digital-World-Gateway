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
    private const float RaioCarrossel = 1.7f;

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

    private void Start()
    {
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
        _cam.backgroundColor = new Color(0.04f, 0.32f, 0.62f);
        _cam.fieldOfView = 40f;
        _cam.transform.position = new Vector3(0f, 2.1f, -5.2f);
        _cam.transform.LookAt(new Vector3(0f, 0.15f, 0.9f));

        if (FindFirstObjectByType<Light>() == null)
        {
            var sol = new GameObject("Luz").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.intensity = 1.2f;
            sol.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        }
        RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.7f);

        // Ninho: um disco de grama e um anel de "palha" em volta dos ovos.
        var chao = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        chao.name = "Chao";
        chao.transform.position = new Vector3(0, -0.05f, RaioCarrossel);
        chao.transform.localScale = new Vector3(30f, 0.05f, 30f);
        chao.GetComponent<Renderer>().material = Material(new Color(0.42f, 0.78f, 0.45f));
        Destroy(chao.GetComponent<Collider>());

        _carrossel = new GameObject("Carrossel").transform;
        _carrossel.position = new Vector3(0, 0, RaioCarrossel);
        var n = Digitamas.Todos.Length;
        for (var i = 0; i < n; i++)
        {
            var apoio = new GameObject("Apoio_" + i).transform;
            apoio.SetParent(_carrossel, false);
            var ang = i * 360f / n;
            apoio.localPosition = Quaternion.Euler(0, ang, 0) * new Vector3(0, 0, -RaioCarrossel);
            // Ninho de cada ovo.
            var ninho = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ninho.GetComponent<Collider>());
            ninho.transform.SetParent(apoio, false);
            ninho.transform.localScale = new Vector3(0.75f, 0.06f, 0.75f);
            ninho.GetComponent<Renderer>().material = Material(new Color(0.72f, 0.55f, 0.3f));
            var ovo = Digitamas.Criar(Digitamas.Todos[i], apoio);
            ovo.transform.localScale = Vector3.one * 0.55f;
            ovo.transform.localPosition = Vector3.up * 0.04f;
            _ovos.Add(ovo.transform);
        }
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
        if (!_chocando) Toques();
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
        _descricao.text = Digitamas.Todos[_atual].Descricao;
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
        _descricao = FigmaUI.TextoEm(_controles.transform, 60f, 1480f, 909f, 110f, "", 44f, FigmaUI.Peso.Negrito);
        _descricao.textWrappingMode = TextWrappingModes.Normal;
        Seta(_controles.transform, 60f, "<", -1);
        Seta(_controles.transform, FigmaUI.Largura - 60f - 230f, ">", 1);
        FigmaUI.BotaoLargo(_controles.transform, 87f, 1800f, "Escolher este ovo", Confirmar);

        _confirmacao = new GameObject("Confirmacao", typeof(RectTransform));
        _confirmacao.transform.SetParent(_raiz, false);
        FigmaUI.Esticar((RectTransform)_confirmacao.transform);
        FigmaUI.TextoEm(_confirmacao.transform, 60f, 1500f, 909f, 100f, "Chocar este Digitama?", 52f, FigmaUI.Peso.Negrito);
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
        var b = FigmaUI.BotaoBack(pai, x, 1620f, () => Selecionar(_atual + passo), rotulo, 230f / 395f);
        b.GetComponentInChildren<TMP_Text>().fontSize = 60f;
    }

    public void Confirmar()
    {
        _controles.SetActive(false);
        _confirmacao.SetActive(true);
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
