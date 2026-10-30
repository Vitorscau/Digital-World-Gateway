using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HUD do mapa (Figma: barra do parceiro + 5 botões).
/// - Liga os 5 botões que já estão na Map_Scene (imagens "Group 20" a "Group 24"):
///   Scan, Profile, Storage, Bag e Settings.
/// - Troca a barra do parceiro (imagem "Group 19", que tinha "30 Gabumon" desenhado) por uma versão
///   montada com as peças do Figma e os dados reais: retrato, nível, nome e EXP do parceiro.
/// - Mostra a tela "Level Up" quando o Tamer subiu de nível.
/// Montada por código ao abrir a Map_Scene.
/// </summary>
public class HUDParceiro : MonoBehaviour
{
    private static readonly (string sprite, Action abrir)[] Botoes =
    {
        ("Group 20", () => TelaFigma.Abrir<TelaScan>()),
        ("Group 21", () => TelaFigma.Abrir<TelaPerfil>()),
        ("Group 22", () => TelaFigma.Abrir<TelaStorage>()),
        ("Group 23", () => TelaFigma.Abrir<TelaBag>()),
        ("Group 24", () => TelaFigma.Abrir<TelaSettings>()),
    };

    private RectTransform _barra;
    private Image _retrato2D;
    private RawImage _retrato3D;
    private RetratoVivo _vivo;
    private GameObject _aviso;
    private float _proximoAviso;
    private TMP_Text _nivel, _nome;
    private RectTransform _xpFundo, _xp;
    private bool _levelUpVerificado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Registrar()
    {
        SceneManager.sceneLoaded -= AoCarregar;
        SceneManager.sceneLoaded += AoCarregar;
        AoCarregar(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void AoCarregar(Scene cena, LoadSceneMode modo)
    {
        if (cena.name != Cenas.Mapa || FindFirstObjectByType<HUDParceiro>() != null) return;
        if (IrParaDigitamaSePreciso()) return;
        new GameObject("HUDParceiro").AddComponent<HUDParceiro>();
    }

    private void Start()
    {
        TelaSettings.AplicarSom();
        var imagens = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var img in imagens)
        {
            if (img.sprite == null) continue;
            foreach (var (sprite, abrir) in Botoes)
            {
                if (img.sprite.name != sprite) continue;
                var b = img.GetComponent<Button>();
                if (b == null) b = img.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => abrir());
                img.raycastTarget = true;
            }
            if (img.sprite.name == "Group 19") MontarBarra(img);
        }
        PerfilJogador.Mudou += Atualizar;
        Atualizar();
    }

    private void OnDestroy()
    {
        PerfilJogador.Mudou -= Atualizar;
        if (_vivo != null) Destroy(_vivo.gameObject);
    }

    private void AtualizarAviso()
    {
        if (_aviso != null) _aviso.SetActive(CuidadoDigimon.PrecisaDeAtencao(PerfilJogador.Atual.Parceiro));
    }

    private float _proximaDigivolucao;

    private void Update()
    {
        if (Time.unscaledTime > _proximoAviso) { _proximoAviso = Time.unscaledTime + 30f; AtualizarAviso(); }
        // Espera a tela de aviso inicial ("Stay Aware") sair antes de anunciar o Level Up.
        if (_barra == null || !_barra.gameObject.activeInHierarchy || TelaFigma.AlgumaAberta) return;
        if (!_levelUpVerificado)
        {
            _levelUpVerificado = true;
            TelaLevelUp.MostrarSePreciso();
            return;
        }
        // Hora de digivolver? (nível ou idade, e o caminho pelas condições de cuidado)
        if (Time.unscaledTime < _proximaDigivolucao) return;
        _proximaDigivolucao = Time.unscaledTime + 5f;
        var parceiro = PerfilJogador.Atual.Parceiro;
        var nova = Digivolucao.Verificar(parceiro);
        if (nova != null)
            TelaDigivolucao.Abrir(parceiro, nova, f =>
            {
                var controlador = FindFirstObjectByType<DigimonController>();
                if (controlador != null) controlador.SetDigimon(f);
            });
    }

    /// <summary>Jogo novo (sem Digitama escolhido): vai direto para a escolha do ovo.</summary>
    private static bool IrParaDigitamaSePreciso()
    {
        if (PerfilJogador.Atual.inicioFeito) return false;
        SceneManager.LoadScene(Cenas.Digitama);
        return true;
    }

    /// <summary>
    /// Monta a barra do parceiro por cima da imagem antiga, nas medidas do Figma.
    /// A imagem "Group 19" tem 998 x 259; as peças são posicionadas na mesma escala.
    /// </summary>
    private void MontarBarra(Image antiga)
    {
        var antigaRt = (RectTransform)antiga.transform;
        antiga.color = new Color(1, 1, 1, 0); // some com o "30 Gabumon" desenhado, mas continua ocupando o lugar

        // A imagem antiga é desenhada "preservando a proporção" dentro de um retângulo maior:
        // a barra fica centralizada nele. As peças vão nessa área visível.
        var r = antigaRt.rect;
        var k = antiga.preserveAspect ? Mathf.Min(r.width / 998f, r.height / 259f) : r.width / 998f;
        _barra = new GameObject("BarraParceiro", typeof(RectTransform)).GetComponent<RectTransform>();
        _barra.SetParent(antigaRt, false);
        _barra.anchorMin = _barra.anchorMax = new Vector2(0.5f, 0.5f);
        _barra.pivot = new Vector2(0.5f, 0.5f);
        _barra.anchoredPosition = Vector2.zero; // âncora no centro do retângulo antigo
        _barra.sizeDelta = new Vector2(998f * k, 259f * k);

        RectTransform Peca(string nome, float x, float y, float w, float h)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_barra, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x * k, -y * k);
            rt.sizeDelta = new Vector2(w * k, h * k);
            return rt;
        }

        var painel = Peca("Painel", 1f, 41f, 998f, 222f);
        FigmaUI.Imagem(painel, FigmaUI.S("hud_painel_parceiro"));

        // Retrato: arte 2D do Gabumon (Figma) ou foto 3D dos outros parceiros.
        var retrato = Peca("Retrato", 8f, 4f, 217f, 228f);
        _retrato2D = FigmaUI.Imagem(retrato, FigmaUI.S("hud_retrato_parceiro"));
        _retrato2D.preserveAspect = true;
        var foto = Peca("Foto", 0f, -10f, 235f, 235f);
        _retrato3D = foto.gameObject.AddComponent<RawImage>();
        _retrato3D.raycastTarget = true;
        // Tocar no rosto do parceiro abre a cena de cuidado (V-Pet em AR).
        var botao = foto.gameObject.AddComponent<Button>();
        botao.targetGraphic = _retrato3D;
        botao.onClick.AddListener(() => { if (!TelaFigma.AlgumaAberta) SceneManager.LoadScene(Cenas.Cuidado); });

        // Aviso "!" quando ele precisa de atenção (fome, tristeza, cansaço, sujeira).
        var aviso = Peca("Aviso", 170f, 0f, 62f, 62f);
        FigmaUI.Imagem(aviso, FigmaUI.Arredondado(), new Color(1f, 0.25f, 0.3f)).type = Image.Type.Sliced;
        FigmaUI.Esticar(FigmaUI.Texto(aviso, "!", 46f * k, FigmaUI.Peso.Negrito, TMPro.TextAlignmentOptions.Center).rectTransform);
        _aviso = aviso.gameObject;
        _aviso.SetActive(false);

        _nivel = FigmaUI.Texto(Peca("Nivel", 160f, 139f, 105f, 84f), "", 70f * k, FigmaUI.Peso.Negrito, TextAlignmentOptions.Right);
        _nivel.characterSpacing = 6f;
        FigmaUI.Esticar(_nivel.rectTransform);
        _nome = FigmaUI.Texto(Peca("Nome", 270f, 169f, 400f, 48f), "", 40f * k, FigmaUI.Peso.Normal, TextAlignmentOptions.Left);
        FigmaUI.Esticar(_nome.rectTransform);

        _xpFundo = Peca("BarraFundo", 213f, 225f, 762.5f, 18f);
        FigmaUI.Imagem(_xpFundo, FigmaUI.S("hud_barra_fundo"));
        _xp = Peca("BarraEXP", 220f, 229f, 755f, 10f);
        var xpImg = FigmaUI.Imagem(_xp, FigmaUI.S("hud_barra_xp"));
        xpImg.type = Image.Type.Filled;
        xpImg.fillMethod = Image.FillMethod.Horizontal;
    }

    private void Atualizar()
    {
        if (_barra == null) return;
        var d = PerfilJogador.Atual.Parceiro;
        var ficha = d?.Ficha;
        var nome = ficha != null ? ficha.Name.Trim() : d?.especie ?? "";
        _nivel.text = d != null ? d.nivel.ToString() : "";
        _nome.text = nome;
        _xp.GetComponent<Image>().fillAmount = d != null ? Mathf.Clamp01((float)d.xp / PerfilJogador.XpDigimonParaProximoNivel(d.nivel)) : 0f;

        // Rosto 3D animado do parceiro (no lugar da arte 2D do Gabumon).
        if (_vivo == null || _vivo.Ficha != ficha)
        {
            if (_vivo != null) Destroy(_vivo.gameObject);
            _vivo = RetratoVivo.Criar(ficha);
        }
        var temVivo = _vivo != null;
        _retrato2D.gameObject.SetActive(!temVivo);
        _retrato3D.texture = temVivo ? _vivo.Textura : RetratoDigimon.Obter(ficha, 256);
        _retrato3D.color = _retrato3D.texture != null ? Color.white : new Color(1, 1, 1, 0);
        AtualizarAviso();
    }
}
