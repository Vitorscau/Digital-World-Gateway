using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tela "Scan Digimon Data" (Figma: 2445).
/// Cada batalha contra uma espécie coleta dados dela. O QR code vai revelando o Digimon que está
/// "atrás" dele conforme a porcentagem sobe; o nome aparece em DigiCode até completar.
/// Com 100%, "Scan Data" baixa (invoca) o Digimon para a coleção e o scan daquela espécie recomeça.
/// Deslize para os lados (ou toque nas setas) para ver as outras espécies.
/// </summary>
public class TelaScan : TelaFigma
{
    private List<ScanSalvo> _scans = new List<ScanSalvo>();
    private int _indice;

    private Image _qr;
    private RawImage _digimon;
    private Image _nomeDigiCode;
    private TMP_Text _nome, _porcentagem, _dica;
    private RectTransform _balao, _barra, _cheio, _linha;
    private Button _botaoScan;
    private GameObject _setaEsq, _setaDir;
    private float _arrasteInicio;

    // Medidas do Figma
    private const float CaixaX = 163f, CaixaY = 639f, CaixaTam = 704f;
    private const float QrX = 250.5f, QrY = 727f, QrTam = 528f;
    private const float BarraX = 129f, BarraY = 1833f, BarraW = 772f;

    protected override void Construir()
    {
        FigmaUI.Fundo(Raiz);
        FigmaUI.TextoEm(Raiz, 0, 152f, FigmaUI.Largura, 58f, "Scan Digimon Data", 64f, FigmaUI.Peso.Negrito);

        // Caixa de vidro com os cantos de mira.
        FigmaUI.CaixaVidro(Raiz, CaixaX, CaixaY, CaixaTam, CaixaTam);
        FigmaUI.Cantos(Raiz, 201.7f, 671.9f, 625.6f, 638.1f);

        // QR (fundo branco) + Digimon por cima, que aparece conforme os dados.
        var qrFundo = FigmaUI.Ret(Raiz, "QR_Fundo", QrX, QrY, QrTam, QrTam);
        FigmaUI.Imagem(qrFundo, FigmaUI.Arredondado(), Color.white).type = Image.Type.Sliced;
        _qr = FigmaUI.Imagem(FigmaUI.Ret(Raiz, "QR", QrX, QrY, QrTam, QrTam), FigmaUI.S("scan_qr"));
        var foto = FigmaUI.Ret(Raiz, "Digimon", QrX + 14f, QrY + 14f, QrTam - 28f, QrTam - 28f);
        _digimon = foto.gameObject.AddComponent<RawImage>();
        _digimon.raycastTarget = false;

        // Linha verde de leitura (sobe e desce sobre o QR).
        _linha = FigmaUI.Ret(Raiz, "LinhaScan", 204f, 693f, 622f, 34f);
        FigmaUI.Imagem(_linha, FigmaUI.S("scan_linha"));

        // Nome: em DigiCode (imagem do Figma) até completar; depois em texto.
        _nomeDigiCode = FigmaUI.Imagem(FigmaUI.Ret(Raiz, "NomeDigiCode", CaixaX, 1397f, CaixaTam, 54f), FigmaUI.S("scan_nome_digicode"));
        _nomeDigiCode.preserveAspect = true;
        _nome = FigmaUI.TextoEm(Raiz, 0, 1390f, FigmaUI.Largura, 70f, "", 56f, FigmaUI.Peso.Negrito);

        _dica = FigmaUI.TextoEm(Raiz, 60f, 1500f, FigmaUI.Largura - 120f, 120f, "", 34f, FigmaUI.Peso.Normal);
        _dica.textWrappingMode = TextWrappingModes.Normal;

        // Balão "20%" (156 x 84, #3174A5) com a seta, acima da barra.
        _balao = FigmaUI.Ret(Raiz, "Balao", 249f, 1687f, 156f, 141f);
        var caixa = FigmaUI.Local(_balao, "Caixa", 0, 0, 156f, 84f);
        FigmaUI.Imagem(caixa, FigmaUI.Arredondado(), FigmaUI.AzulBalao).type = Image.Type.Sliced;
        _porcentagem = FigmaUI.Texto(caixa, "0%", 48f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Center, false);
        FigmaUI.Esticar(_porcentagem.rectTransform);
        var seta = FigmaUI.Local(_balao, "Seta", 49f, 69f, 57f, 57f);
        seta.pivot = new Vector2(0.5f, 0.5f);
        seta.anchoredPosition += new Vector2(28.5f, -28.5f);
        seta.localEulerAngles = new Vector3(0, 0, 180f);
        FigmaUI.Imagem(seta, FigmaUI.S("scan_seta_balao"));

        _barra = FigmaUI.Barra(Raiz, BarraX, BarraY, BarraW, 54f, 0f, out _cheio);

        _botaoScan = FigmaUI.BotaoLargo(Raiz, 87f, 1930f, "Scan Data", ScanData);
        FigmaUI.BotaoBack(Raiz, (FigmaUI.Largura - 316f) / 2f, 2080f, Fechar, "Back", 0.8f);

        // Setas para trocar de espécie.
        _setaEsq = FigmaUI.BotaoEm(Seta(40f, "<"), () => Trocar(-1)).gameObject;
        _setaDir = FigmaUI.BotaoEm(Seta(FigmaUI.Largura - 140f, ">"), () => Trocar(1)).gameObject;

        Recarregar();
    }

    private RectTransform Seta(float x, string simbolo)
    {
        var rt = FigmaUI.Ret(Raiz, "Seta" + simbolo, x, 930f, 100f, 140f);
        var t = FigmaUI.Texto(rt, simbolo, 110f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Center);
        FigmaUI.Esticar(t.rectTransform);
        return rt;
    }

    protected override void AoMudarPerfil() => Recarregar();

    private void Recarregar()
    {
        var atual = _scans.Count > 0 && _indice < _scans.Count ? _scans[_indice].especie : null;
        _scans = PerfilJogador.Atual.scans.Where(s => s.porcentagem > 0)
            .OrderByDescending(s => s.porcentagem).ToList();
        _indice = atual != null ? Mathf.Max(0, _scans.FindIndex(s => s.especie == atual)) : 0;
        Mostrar();
    }

    private void Trocar(int direcao)
    {
        if (_scans.Count < 2) return;
        _indice = (_indice + direcao + _scans.Count) % _scans.Count;
        Mostrar();
    }

    private void Mostrar()
    {
        _setaEsq.SetActive(_scans.Count > 1);
        _setaDir.SetActive(_scans.Count > 1);

        if (_scans.Count == 0)
        {
            _digimon.color = new Color(1, 1, 1, 0);
            _qr.color = Color.white;
            _nomeDigiCode.gameObject.SetActive(true);
            _nome.text = "";
            _dica.text = "Battle Digimon on the map to collect their data.";
            Progresso(0);
            _botaoScan.interactable = false;
            return;
        }

        var s = _scans[_indice];
        var ficha = CatalogoDigimon.Buscar(s.especie);
        var p = s.porcentagem / 100f;
        _digimon.texture = RetratoDigimon.Obter(ficha);
        // O Digimon vai aparecendo e o QR vai sumindo conforme os dados chegam.
        _digimon.color = new Color(1, 1, 1, Mathf.Lerp(0.08f, 1f, p));
        _qr.color = new Color(1, 1, 1, Mathf.Lerp(1f, 0.12f, p));
        var completo = s.porcentagem >= 100;
        _nomeDigiCode.gameObject.SetActive(!completo);
        _nome.text = completo ? (ficha != null ? ficha.Name.Trim() : s.especie) : "";
        _dica.text = completo
            ? "Data complete! Tap Scan Data to download."
            : _scans.Count > 1 ? $"{_indice + 1} of {_scans.Count}  ·  swipe to see others" : "";
        Progresso(p);
        _botaoScan.interactable = completo;
    }

    private void Progresso(float p)
    {
        _porcentagem.text = $"{Mathf.RoundToInt(p * 100)}%";
        FigmaUI.AtualizarBarra(_barra, _cheio, p);
        // O balão acompanha o fim da parte cheia da barra.
        var x = BarraX + BarraW * Mathf.Clamp01(p) - 78f;
        FigmaUI.Posicionar(_balao, Mathf.Clamp(x, BarraX - 40f, BarraX + BarraW - 116f), 1687f, 156f, 141f);
    }

    private void ScanData()
    {
        if (_scans.Count == 0) return;
        var s = _scans[_indice];
        var p = PerfilJogador.Atual;
        var novo = p.ConcluirScan(s.especie);
        if (novo == null)
        {
            AvisoNaTela.Mostrar($"Not enough data ({s.porcentagem}%). Keep battling!");
            return;
        }
        p.Salvar();
        Missoes.RegistrarCaptura(novo.especie);
        var nome = novo.Ficha != null ? novo.Ficha.Name.Trim() : novo.especie;
        AvisoNaTela.Mostrar($"Download complete! {nome} joined your Storage.");
    }

    private void Update()
    {
        // Linha de leitura passando sobre o QR.
        if (_linha != null)
        {
            var t = Mathf.PingPong(Time.unscaledTime * 0.35f, 1f);
            FigmaUI.Posicionar(_linha, 204f, Mathf.Lerp(693f, QrY + QrTam - 20f, t), 622f, 34f);
        }

        // Deslizar para trocar de espécie.
        if (Input.GetMouseButtonDown(0)) _arrasteInicio = Input.mousePosition.x;
        if (Input.GetMouseButtonUp(0))
        {
            var dx = Input.mousePosition.x - _arrasteInicio;
            if (Mathf.Abs(dx) > Screen.width * 0.15f) Trocar(dx < 0 ? 1 : -1);
        }
    }
}
