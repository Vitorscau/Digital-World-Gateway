using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Cena de cuidado do parceiro: um V-Pet em 3D e AR.
/// - Toque no chão para chamar o Digimon (no AR, ele aparece no chão de verdade; sem AR, num chão virtual).
/// - Alimentar (Meat da Bag), Treinar (minigame), Dormir/Acordar, Limpar, e carinho tocando nele.
/// - Barras de Fome, Energia, Humor e Amizade. Tudo fica salvo no perfil.
/// Aberta ao tocar na foto do parceiro na HUD do mapa.
/// </summary>
public class CenaCuidado : MonoBehaviour
{
    [Tooltip("Altura (metros) de um Digimon Rookie no AR. Os outros estágios são proporcionais.")]
    [SerializeField] private float alturaRookieMetros = 0.4f;

    private PerfilJogador _perfil;
    private DigimonSalvo _parceiro;
    private DigimonBase _ficha;
    private Camera _cam;
    private DigimonVirtual _pet;
    private float _inicio;
    private readonly List<GameObject> _cocos = new List<GameObject>();

    // UI
    private RectTransform _raiz;
    private TMP_Text _dica, _titulo, _amizadeTxt, _rotuloDormir, _rotuloComida;
    private readonly Dictionary<string, (RectTransform fundo, RectTransform cheio, TMP_Text valor)> _barras =
        new Dictionary<string, (RectTransform, RectTransform, TMP_Text)>();
    private RectTransform _balao;
    private TMP_Text _balaoTexto;
    private Image _balaoIcone;
    private float _fimBalao;
    private float _proximoSalvar;
    private TreinoMinigame _treino;

    /// <summary>Onde o Tamer (a câmera) está, no chão.</summary>
    public Vector3 PosicaoTamer
    {
        get
        {
            var p = _cam != null ? _cam.transform.position : Vector3.zero;
            if (_pet != null) p.y = _pet.transform.position.y;
            return p;
        }
    }

    private void Start()
    {
        _inicio = Time.time;
        _cam = Camera.main;
        _perfil = PerfilJogador.Atual;
        _parceiro = _perfil.Parceiro;
        _ficha = _parceiro?.Ficha;
        CuidadoDigimon.Atualizar(_parceiro);
        TelaSettings.AplicarSom();
        MontarUI();
        if (_ficha == null) { _dica.text = "Nenhum parceiro escolhido."; return; }
        _dica.text = $"Aponte para o chão e toque para chamar o {Nome}.";
        StartCoroutine(VerificarAR());
    }

    private string Nome => _ficha != null ? _ficha.Name.Trim() : "Digimon";

    /// <summary>Sem AR (editor ou aparelho sem suporte): monta um chão virtual e coloca o Digimon na frente.</summary>
    private IEnumerator VerificarAR()
    {
        yield return new WaitForSeconds(2.5f);
        var semAR = ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.None ||
                    ARSession.state == ARSessionState.NeedsInstall;
#if UNITY_EDITOR
        semAR = semAR || ARSession.state < ARSessionState.SessionTracking;
#endif
        if (!semAR || _pet != null) yield break;
        var frente = Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up);
        if (frente.sqrMagnitude < 1e-4f) frente = Vector3.forward;
        var chao = _cam.transform.position + frente.normalized * 1.3f;
        chao.y = _cam.transform.position.y - 0.9f;
        ChaoVirtual(chao);
        Colocar(chao);
        // Sem AR a câmera não se move sozinha: olha para o Digimon.
        _cam.transform.LookAt(chao + Vector3.up * alturaRookieMetros * 0.6f);
    }

    private void ChaoVirtual(Vector3 centro)
    {
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.55f, 0.82f, 1f);
        var disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disco.name = "ChaoVirtual";
        disco.transform.position = centro - Vector3.up * 0.01f;
        disco.transform.localScale = new Vector3(4f, 0.01f, 4f);
        disco.GetComponent<Renderer>().material = Formas.MaterialSemLuz(new Color(0.47f, 0.85f, 0.55f));
    }

    // ------------------------------------------------------------ Colocar o Digimon

    private void Colocar(Vector3 chao)
    {
        var go = Instantiate(_ficha.Prefab);
        go.name = Nome + "_Cuidado";
        foreach (var c in go.GetComponents<WildDigimon>()) Destroy(c);
        foreach (var c in go.GetComponents<DigimonMapa>()) Destroy(c);
        foreach (var c in go.GetComponents<MapDigimon>()) Destroy(c);
        foreach (var c in go.GetComponents<CriaturaArena>()) Destroy(c);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
        if (go.GetComponentInChildren<Collider>() == null) go.AddComponent<CapsuleCollider>();

        // Tamanho real pelo estágio, pés no chão, olhando para o Tamer.
        var altura = alturaRookieMetros * Proporcao(_ficha.Stage);
        var h = DigimonVisual.Altura(go);
        if (h > 0.0001f) go.transform.localScale *= altura / h;
        go.transform.position = chao;
        var olhar = _cam.transform.position - chao;
        olhar.y = 0;
        if (olhar.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(olhar);

        // Sombra redonda no chão (deixa o modelo "pisando" no mundo real).
        var sombra = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(sombra.GetComponent<Collider>());
        sombra.name = "Sombra";
        sombra.transform.SetParent(go.transform, false);
        sombra.transform.localRotation = Quaternion.Euler(90, 0, 0);
        sombra.transform.localPosition = Vector3.up * 0.002f / Mathf.Max(0.0001f, go.transform.lossyScale.y);
        var tam = altura * 1.1f / Mathf.Max(0.0001f, go.transform.lossyScale.x);
        sombra.transform.localScale = new Vector3(tam, tam, 1f);
        sombra.GetComponent<Renderer>().material = Formas.MaterialSemLuz(Color.white, Formas.Sombra(), true);

        _pet = go.AddComponent<DigimonVirtual>();
        _pet.Iniciar(this, _parceiro, chao, altura);
        CriarCocos();
        _dica.text = "Toque nele para fazer carinho. Toque no chão para chamá-lo.";
        StartCoroutine(SumirDica(6f));
    }

    private static float Proporcao(DigimonStage e)
    {
        switch (e)
        {
            case DigimonStage.Fresh: return 0.55f;
            case DigimonStage.InTraining: return 0.7f;
            case DigimonStage.Rookie: return 1f;
            case DigimonStage.Champion: case DigimonStage.Armor: return 1.4f;
            case DigimonStage.Ultimate: return 1.8f;
            default: return 2.2f;
        }
    }

    private IEnumerator SumirDica(float depois)
    {
        yield return new WaitForSeconds(depois);
        if (_dica != null) _dica.text = "";
    }

    // ------------------------------------------------------------ Cocô (V-Pet!)

    private void CriarCocos()
    {
        foreach (var c in _cocos) if (c != null) Destroy(c);
        _cocos.Clear();
        if (_pet == null) return;
        for (var i = 0; i < _parceiro.cocos; i++)
        {
            var ang = (i * 97f + 40f) * Mathf.Deg2Rad;
            var pos = _pet.transform.position + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * _pet.Altura * (1.3f + i * 0.25f);
            _cocos.Add(Coco(pos, _pet.Altura * 0.28f));
        }
    }

    /// <summary>Montinho em espiral feito de três esferas marrons.</summary>
    private static GameObject Coco(Vector3 pos, float tamanho)
    {
        var raiz = new GameObject("Coco");
        raiz.transform.position = pos;
        var mat = Formas.MaterialSemLuz(new Color(0.45f, 0.28f, 0.15f));
        for (var i = 0; i < 3; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(raiz.transform, false);
            var k = 1f - i * 0.3f;
            s.transform.localScale = new Vector3(tamanho * k, tamanho * k * 0.55f, tamanho * k);
            s.transform.localPosition = Vector3.up * tamanho * (0.15f + i * 0.3f);
            s.GetComponent<Renderer>().material = mat;
        }
        var col = raiz.AddComponent<SphereCollider>();
        col.radius = tamanho * 0.7f;
        col.center = Vector3.up * tamanho * 0.4f;
        return raiz;
    }

    // ------------------------------------------------------------ Toques

    private float _ultimoEsfregar;

    private void Update()
    {
        if (_ficha == null) return;
        TocarOuEsfregar();
        Pinca();
        AtualizarUI();
        if (Time.time > _proximoSalvar)
        {
            _proximoSalvar = Time.time + 15f;
            _parceiro.ultimoCuidado = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _perfil.Salvar();
        }
    }

    private void TocarOuEsfregar()
    {
        if (_treino != null || _cam == null) return;
        Vector2 pos;
        bool comecou, segurando;
        if (Input.touchCount == 1)
        {
            var t = Input.GetTouch(0);
            pos = t.position;
            comecou = t.phase == TouchPhase.Began;
            segurando = t.phase == TouchPhase.Moved;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId)) return;
        }
        else if (Input.touchCount == 0)
        {
            pos = Input.mousePosition;
            comecou = Input.GetMouseButtonDown(0);
            segurando = Input.GetMouseButton(0) && !comecou;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        }
        else return;
        if (!comecou && !segurando) return;

        var raio = _cam.ScreenPointToRay(pos);
        var acerto = Physics.Raycast(raio, out var hit, 50f);

        // Tocar ou esfregar no Digimon = carinho.
        if (_pet != null && acerto && hit.collider.GetComponentInParent<DigimonVirtual>() == _pet)
        {
            if (comecou || Time.time - _ultimoEsfregar > 0.6f)
            {
                _ultimoEsfregar = Time.time;
                if (_pet.Carinho() > 0) AvisoNaTela.Mostrar($"Amizade +1 ({_parceiro.amizade})");
            }
            return;
        }
        if (!comecou) return;

        // Cocô: limpa um.
        if (acerto && hit.collider.name == "Coco")
        {
            _cocos.Remove(hit.collider.gameObject);
            Destroy(hit.collider.gameObject);
            _parceiro.cocos = Mathf.Max(0, _parceiro.cocos - 1);
            CuidadoDigimon.Mudar(ref _parceiro.humor, 5f);
            if (_parceiro.cocos == 0) { CuidadoDigimon.Amizade(_parceiro, 1); _pet?.Comemorar(); }
            return;
        }

        // Chão: na primeira vez coloca o Digimon; depois, chama ele até ali.
        var chao = acerto ? hit.point : PontoNoChaoVirtual(raio);
        if (chao == null) return;
        if (_pet == null) Colocar(chao.Value);
        else _pet.Chamar(chao.Value);
    }

    /// <summary>Sem malha do ambiente no ponto: usa um chão imaginário ~1,2 m abaixo do celular.</summary>
    private Vector3? PontoNoChaoVirtual(Ray raio)
    {
        var y = _pet != null ? _pet.transform.position.y : _cam.transform.position.y - 1.2f; // altura típica do celular
        var plano = new Plane(Vector3.up, new Vector3(0, y, 0));
        if (!plano.Raycast(raio, out var d) || d > 20f) return null;
        return raio.GetPoint(d);
    }

    /// <summary>Dois dedos: aumenta ou diminui o Digimon.</summary>
    private void Pinca()
    {
        if (_pet == null || Input.touchCount != 2) return;
        var a = Input.GetTouch(0);
        var b = Input.GetTouch(1);
        var antes = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude;
        var agora = (a.position - b.position).magnitude;
        if (antes < 1f) return;
        var fator = Mathf.Clamp(agora / antes, 0.9f, 1.1f);
        _pet.ReescalarBase(fator);
    }

    // ------------------------------------------------------------ Botões

    private void Alimentar()
    {
        if (!PetPronto()) return;
        if (_pet.Dormindo) { _pet.Acordar(true); return; }
        switch (CuidadoDigimon.Alimentar(_perfil, _parceiro))
        {
            case CuidadoDigimon.ResultadoComida.SemComida:
                AvisoNaTela.Mostrar("Sem Meat na Bag. Vença batalhas ou decifre Encrypted Data para conseguir mais.");
                break;
            case CuidadoDigimon.ResultadoComida.Cheio:
                _pet.RecusarComida();
                break;
            default:
                StartCoroutine(_pet.Comer());
                break;
        }
    }

    private void Treinar()
    {
        if (!PetPronto()) return;
        if (_pet.Dormindo) { _pet.Balao("Zzz..."); return; }
        var motivo = CuidadoDigimon.PodeTreinar(_parceiro);
        if (motivo != null)
        {
            AvisoNaTela.Mostrar($"{Nome} {motivo}.");
            return;
        }
        _treino = TreinoMinigame.Abrir(_raiz, _pet, _parceiro, Nome, () =>
        {
            _treino = null;
            _perfil.Salvar();
        });
    }

    private void DormirOuAcordar()
    {
        if (!PetPronto()) return;
        if (_pet.Dormindo) _pet.Acordar(true);
        else if (_parceiro.energia > 90f) _pet.Balao("Não estou com sono!");
        else StartCoroutine(_pet.Dormir());
    }

    private void Limpar()
    {
        if (!PetPronto()) return;
        var n = CuidadoDigimon.Limpar(_parceiro);
        CriarCocos();
        if (n > 0) { _pet.Comemorar(); AvisoNaTela.Mostrar("Tudo limpo!"); }
        else _pet.Balao("Já está limpinho!");
    }

    private bool PetPronto()
    {
        if (_pet != null && _treino == null) return true;
        if (_pet == null) AvisoNaTela.Mostrar($"Primeiro toque no chão para chamar o {Nome}.");
        return false;
    }

    private void Voltar()
    {
        _perfil.Salvar();
        SceneManager.LoadScene(Cenas.Mapa);
    }

    private void OnApplicationPause(bool pausa)
    {
        if (pausa && _perfil != null) _perfil.Salvar();
    }

    // ------------------------------------------------------------ Balão, corações

    public void Balao(DigimonVirtual quem, string texto, Sprite icone)
    {
        if (_balao == null) return;
        _balaoTexto.text = texto;
        _balaoIcone.sprite = icone;
        _balaoIcone.gameObject.SetActive(icone != null);
        _balaoTexto.rectTransform.offsetMin = new Vector2(icone != null ? 110f : 20f, 0);
        _fimBalao = Time.time + 2.8f;
        _balao.gameObject.SetActive(true);
    }

    public void Coracoes(Vector3 mundo, int quantos)
    {
        for (var i = 0; i < quantos; i++) StartCoroutine(Coracao(mundo, i * 0.12f));
    }

    private IEnumerator Coracao(Vector3 mundo, float atraso)
    {
        yield return new WaitForSeconds(atraso);
        var rt = FigmaUI.Ret(_raiz, "Coracao", 0, 0, 90, 90);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var img = FigmaUI.Imagem(rt, Formas.Coracao(), new Color(1f, 0.35f, 0.55f));
        var lado = Random.Range(-60f, 60f);
        for (float k = 0; k < 1f; k += Time.deltaTime * 0.9f)
        {
            if (_cam == null) break;
            var tela = _cam.WorldToScreenPoint(mundo);
            rt.position = tela + new Vector3(lado * k, k * 260f, 0) * _raiz.lossyScale.x;
            rt.localScale = Vector3.one * (0.6f + Mathf.Sin(k * Mathf.PI) * 0.6f);
            img.color = new Color(1f, 0.35f, 0.55f, 1f - k * k);
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    // ------------------------------------------------------------ Interface

    private void MontarUI()
    {
        _raiz = (RectTransform)FigmaUI.Canvas(transform, "UI_Cuidado", 100).transform;

        // Painel de cima: nome, nível, jeito e as quatro barras.
        FigmaUI.CaixaVidro(_raiz, 40f, 40f, 949f, 470f);
        _titulo = FigmaUI.TextoEm(_raiz, 80f, 60f, 870f, 70f, "", 50f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Left);
        _amizadeTxt = FigmaUI.TextoEm(_raiz, 80f, 125f, 870f, 50f, "", 32f, FigmaUI.Peso.Normal, TextAlignmentOptions.Left);
        Barra("Fome", 195f, new Color(1f, 0.6f, 0.25f));
        Barra("Energia", 270f, new Color(0.35f, 0.85f, 0.4f));
        Barra("Humor", 345f, new Color(1f, 0.85f, 0.3f));
        Barra("Amizade", 420f, new Color(1f, 0.45f, 0.65f));

        _dica = FigmaUI.TextoEm(_raiz, 60f, 540f, 909f, 140f, "", 38f, FigmaUI.Peso.Negrito);
        _dica.textWrappingMode = TextWrappingModes.Normal;

        // Balão de fala que segue a cabeça do Digimon.
        _balao = FigmaUI.Ret(_raiz, "Balao", 0, 0, 520f, 110f);
        _balao.pivot = new Vector2(0.5f, 0f);
        var fundo = FigmaUI.Imagem(_balao, FigmaUI.Arredondado(), new Color(1f, 1f, 1f, 0.92f));
        fundo.type = Image.Type.Sliced;
        _balaoIcone = FigmaUI.Imagem(FigmaUI.Local(_balao, "Icone", 15f, 10f, 90f, 90f), null);
        _balaoIcone.preserveAspect = true;
        _balaoTexto = FigmaUI.Texto(_balao, "", 38f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Center, false);
        FigmaUI.Esticar(_balaoTexto.rectTransform);
        _balaoTexto.color = new Color(0.1f, 0.2f, 0.35f);
        _balao.gameObject.SetActive(false);

        // Botões de baixo (padrão do Figma).
        var e = (FigmaUI.Largura - 2f * 430f - 30f) / 2f;
        _rotuloComida = Rotulo(FigmaUI.BotaoBack(_raiz, e, 1720f, Alimentar, "Alimentar", 430f / 395f));
        Rotulo(FigmaUI.BotaoBack(_raiz, e + 460f, 1720f, Treinar, "Treinar", 430f / 395f));
        _rotuloDormir = Rotulo(FigmaUI.BotaoBack(_raiz, e, 1880f, DormirOuAcordar, "Dormir", 430f / 395f));
        Rotulo(FigmaUI.BotaoBack(_raiz, e + 460f, 1880f, Limpar, "Limpar", 430f / 395f));
        FigmaUI.BotaoBack(_raiz, (FigmaUI.Largura - 300f) / 2f, 2050f, Voltar, "Voltar", 300f / 395f);
    }

    private static TMP_Text Rotulo(Button b) => b.GetComponentInChildren<TMP_Text>();

    private void Barra(string nome, float y, Color cor)
    {
        FigmaUI.TextoEm(_raiz, 80f, y, 220f, 50f, nome, 32f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Left);
        var fundo = FigmaUI.Barra(_raiz, 300f, y + 12f, 520f, 30f, 0f, out var cheio);
        cheio.GetComponent<Image>().color = cor;
        var valor = FigmaUI.TextoEm(_raiz, 835f, y, 120f, 50f, "", 32f, FigmaUI.Peso.Normal, TextAlignmentOptions.Right);
        _barras[nome] = (fundo, cheio, valor);
    }

    private void AtualizarUI()
    {
        if (_parceiro == null) return;
        _titulo.text = $"{Nome}  <size=36>Lv {_parceiro.nivel}</size>";
        var jeito = new Digimon(_ficha, Mathf.Max(1, _parceiro.nivel)).Personalidade;
        _amizadeTxt.text = $"{CuidadoDigimon.NivelAmizade(_parceiro.amizade)} · jeito {jeito}" + (_parceiro.cocos > 0 ? $" · sujeira: {_parceiro.cocos}" : "");
        void B(string n, float v)
        {
            var (fundo, cheio, valor) = _barras[n];
            FigmaUI.AtualizarBarra(fundo, cheio, v / 100f);
            valor.text = Mathf.RoundToInt(v).ToString();
        }
        B("Fome", _parceiro.fome);
        B("Energia", _parceiro.energia);
        B("Humor", _parceiro.humor);
        B("Amizade", _parceiro.amizade);
        _rotuloComida.text = $"Alimentar ({_perfil.Quantidade(Itens.Meat)})";
        _rotuloDormir.text = _pet != null && _pet.Dormindo ? "Acordar" : "Dormir";

        if (_balao.gameObject.activeSelf)
        {
            if (Time.time > _fimBalao || _pet == null) _balao.gameObject.SetActive(false);
            else
            {
                var tela = _cam.WorldToScreenPoint(_pet.Topo);
                _balao.gameObject.SetActive(tela.z > 0);
                _balao.position = tela + Vector3.up * 20f * _raiz.lossyScale.x;
            }
        }
    }
}

/// <summary>Atalho para falas simples sem ícone.</summary>
public static class DigimonVirtualExtensoes
{
    public static void Balao(this DigimonVirtual d, string texto)
    {
        var cena = Object.FindFirstObjectByType<CenaCuidado>();
        if (cena != null) cena.Balao(d, texto, null);
    }
}
