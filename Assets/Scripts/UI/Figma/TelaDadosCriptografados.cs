using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tela de um Encrypted Data (o "PokéStop" do jogo), aberta ao tocar no ponto no mapa.
/// "Decrypt" dá itens, EXP do Tamer (bônus na primeira vez no lugar) e às vezes dados de Scan.
/// Segue o padrão das telas do Figma (fundo azul, moldura, cantos de mira e botões).
/// </summary>
public class TelaDadosCriptografados : TelaFigma
{
    private const int XpPorDecifrar = 50;
    private const int XpPrimeiraVez = 250;
    private const float ChanceScan = 0.35f;
    private const int PontosScan = 5;

    private PontoDados _ponto;
    private TMP_Text _estado;
    private Button _botao;
    private CanvasGroup _grupoBotao;
    private Image _qr;
    private float _proximoRelogio;

    public static TelaDadosCriptografados Abrir(PontoDados ponto)
    {
        var tela = Abrir<TelaDadosCriptografados>();
        tela.Preencher(ponto);
        return tela;
    }

    protected override void Construir() => MontarMolduraComTitulo("Encrypted Data");

    private void Preencher(PontoDados ponto)
    {
        _ponto = ponto;

        var nome = FigmaUI.TextoEm(Raiz, 87f, 225f, 855f, 130f, ponto.Nome, 56f, FigmaUI.Peso.Negrito);
        nome.textWrappingMode = TextWrappingModes.Normal;
        nome.overflowMode = TextOverflowModes.Ellipsis;
        nome.enableAutoSizing = true;
        nome.fontSizeMin = 34f;
        nome.fontSizeMax = 56f;
        FigmaUI.TextoEm(Raiz, 87f, 365f, 855f, 50f, TipoLegivel(ponto.Tipo), 34f, FigmaUI.Peso.Normal)
            .color = new Color(1, 1, 1, 0.8f);

        // Os dados "criptografados": o QR do Scan dentro dos cantos de mira.
        const float lado = 560f;
        var x = (FigmaUI.Largura - lado) / 2f;
        FigmaUI.Cantos(Raiz, x, 450f, lado, lado);
        _qr = FigmaUI.Imagem(FigmaUI.Ret(Raiz, "Dados", x + 70f, 520f, lado - 140f, lado - 140f), FigmaUI.S("scan_qr"));
        _qr.preserveAspect = true;

        _estado = FigmaUI.TextoEm(Raiz, 87f, 1045f, 855f, 60f, "", 36f, FigmaUI.Peso.Normal);
        _botao = FigmaUI.BotaoLargo(Raiz, 87f, 1125f, "Decrypt", Decifrar);
        _grupoBotao = _botao.gameObject.AddComponent<CanvasGroup>();

        FigmaUI.BotaoBack(Raiz, (FigmaUI.Largura - 395f) / 2f, 2061.5f, Voltar);
        AtualizarEstado();
    }

    private void Update()
    {
        if (_ponto == null || Time.unscaledTime < _proximoRelogio) return;
        _proximoRelogio = Time.unscaledTime + 0.5f;
        AtualizarEstado();
    }

    private void AtualizarEstado()
    {
        if (_ponto == null) return;
        var disponivel = _ponto.Disponivel;
        _botao.interactable = disponivel;
        _grupoBotao.alpha = disponivel ? 1f : 0.45f;
        _qr.color = disponivel ? Color.white : new Color(0.72f, 0.45f, 1f, 0.8f);

        var distancia = CoverageManager.Instancia != null ? CoverageManager.Instancia.DistanciaAte(_ponto) : 0;
        if (disponivel)
            _estado.text = $"{Distancia(distancia)} away";
        else
        {
            var r = _ponto.Recarga;
            _estado.text = $"Recharging: {(int)r.TotalMinutes}:{r.Seconds:00}";
        }
    }

    private static string Distancia(double metros) => metros >= 1000 ? $"{metros / 1000:0.0} km" : $"{metros:0} m";

    // ------------------------------------------------------------ Decifrar

    private void Decifrar()
    {
        if (_ponto == null || !_ponto.Disponivel) return;
        var gerente = CoverageManager.Instancia;
        if (gerente != null && !gerente.PodeUsar(_ponto)) return;

        var perfil = PerfilJogador.Atual;
        var rnd = new System.Random();

        // Itens: 3 (às vezes 4), mais Meat do que Bandage.
        var itens = new Dictionary<string, int>();
        var quantos = rnd.NextDouble() < 0.15 ? 4 : 3;
        for (var i = 0; i < quantos; i++)
        {
            var id = rnd.NextDouble() < 0.6 ? Itens.Meat : Itens.Bandage;
            itens[id] = itens.TryGetValue(id, out var n) ? n + 1 : 1;
            perfil.AdicionarItem(id);
        }

        var primeiraVez = perfil.MarcarPontoUsado(_ponto.Chave, gerente != null ? gerente.RecargaSegundos : 300);
        var xp = XpPorDecifrar + (primeiraVez ? XpPrimeiraVez : 0);
        perfil.GanharXp(xp);

        // Às vezes os dados são de um Digimon: soma no Scan dele.
        string linhaScan = null;
        if (rnd.NextDouble() < ChanceScan)
        {
            var especies = (_DigimonGOP.Instance != null ? _DigimonGOP.Instance.Especies : CatalogoDigimon.Todos)
                .Distinct().ToList();
            if (especies.Count > 0)
            {
                var ficha = especies[rnd.Next(especies.Count)];
                var total = perfil.AdicionarScan(ficha.name, PontosScan);
                linhaScan = total >= 100
                    ? $"{ficha.Name.Trim()} data complete! Open Scan."
                    : $"+{PontosScan}% {ficha.Name.Trim()} data ({total}%)";
            }
        }

        perfil.Salvar();
        Missoes.RegistrarDecifrar();
        _ponto.AtualizarVisual();
        AtualizarEstado();
        StartCoroutine(MostrarPremio(itens, xp, primeiraVez, linhaScan));
    }

    private IEnumerator MostrarPremio(Dictionary<string, int> itens, int xp, bool primeiraVez, string linhaScan)
    {
        // Pequena "decodificação": o QR pisca antes de revelar o prêmio.
        for (var i = 0; i < 6; i++)
        {
            _qr.color = new Color(1, 1, 1, i % 2 == 0 ? 0.25f : 1f);
            yield return new WaitForSecondsRealtime(0.08f);
        }
        AtualizarEstado();

        FigmaUI.CaixaVidro(Raiz, 87f, 1315f, 855f, 560f); // criada antes dos textos: fica atrás deles
        FigmaUI.TextoEm(Raiz, 87f, 1340f, 855f, 60f, "Data decoded!", 44f, FigmaUI.Peso.Negrito);

        // Ícones dos itens lado a lado, com a quantidade.
        const float lado = 150f, espaco = 60f;
        var largura = itens.Count * lado + (itens.Count - 1) * espaco;
        var x = (FigmaUI.Largura - largura) / 2f;
        foreach (var kv in itens)
        {
            TelaBag.IconeRecortado(FigmaUI.Ret(Raiz, "Item", x, 1425f, lado, lado), kv.Key);
            FigmaUI.Cantos(Raiz, x - 12f, 1413f, lado + 24f, lado + 24f, 40f);
            FigmaUI.TextoEm(Raiz, x, 1580f, lado, 50f, $"{kv.Value}x {Itens.Buscar(kv.Key)?.Nome}", 32f, FigmaUI.Peso.Negrito);
            x += lado + espaco;
        }

        var linhas = $"+{xp} EXP" + (primeiraVez ? "  (first time here!)" : "");
        if (linhaScan != null) linhas += "\n" + linhaScan;
        var t = FigmaUI.TextoEm(Raiz, 107f, 1660f, 815f, 190f, linhas, 36f, FigmaUI.Peso.Normal);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.enableAutoSizing = true;
        t.fontSizeMin = 26f;
        t.fontSizeMax = 36f;
    }

    private void Voltar()
    {
        Fechar();
        TelaLevelUp.MostrarSePreciso();
    }

    /// <summary>Nome em português do tipo de lugar (esquema OpenMapTiles).</summary>
    private static string TipoLegivel(string tipo)
    {
        switch (tipo)
        {
            case null: case "": return "Data lost on the map";
            case "place_of_worship": case "church": case "christian": return "Temple";
            case "park": case "garden": case "playground": return "Park";
            case "monument": case "memorial": case "statue": case "artwork": return "Monument";
            case "museum": return "Museum";
            case "art_gallery": case "gallery": return "Gallery";
            case "theatre": return "Theater";
            case "cinema": return "Cinema";
            case "library": return "Library";
            case "attraction": case "viewpoint": return "Attraction";
            case "school": case "college": case "university": case "kindergarten": return "School";
            case "hospital": case "clinic": case "doctors": return "Health";
            case "pharmacy": case "chemist": return "Pharmacy";
            case "town_hall": case "townhall": case "courthouse": return "Public building";
            case "restaurant": return "Restaurant";
            case "cafe": return "Cafe";
            case "bar": case "pub": case "beer": return "Bar";
            case "fast_food": case "ice_cream": return "Snack bar";
            case "bakery": return "Bakery";
            case "grocery": case "supermarket": case "convenience": return "Market";
            case "shop": case "clothes": case "clothing_store": case "mall": return "Shop";
            case "bank": case "atm": return "Bank";
            case "fuel": return "Gas station";
            case "lodging": case "hotel": return "Lodging";
            case "stadium": case "sports_centre": case "pitch": case "sport": return "Sports";
            case "bus": case "bus_stop": case "station": case "railway": return "Station";
            default:
                var s = tipo.Replace("_", " ");
                return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
