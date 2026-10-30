using System;
using System.Collections.Generic;
using System.Globalization;
using Niantic.Lightship.Maps;
using Niantic.Lightship.Maps.Core;
using Niantic.Lightship.Maps.Core.Coordinates;
using Niantic.Lightship.Maps.MapLayers.Components;
using UnityEngine;

/// <summary>
/// Encrypted Data: os "PokéStops" do jogo.
///
/// - Ficam em lugares reais com nome (praças, igrejas, museus, lojas...), lidos do OpenStreetMap
///   junto com o mapa. Todo jogador vê os mesmos pontos nos mesmos lugares.
/// - Onde não há lugares com nome, aparecem alguns pontos extras em posições fixas.
/// - Tocar num ponto abre a tela para decifrar os dados: dá itens, EXP e às vezes dados de Scan.
/// - Depois de decifrado, o ponto fica roxo e recarrega por alguns minutos (salvo no perfil).
///
/// (O nome da classe ficou do SDK antigo, que usava as áreas de cobertura da Niantic; a cena continua ligada nela.)
/// </summary>
public class CoverageManager : MonoBehaviour
{
    public static CoverageManager Instancia { get; private set; }

    [SerializeField] private LightshipMapView _mapView;
    [Tooltip("Camada que coloca o modelo do Encrypted Data no mapa.")]
    [SerializeField] private LayerGameObjectPlacement _cubeGOP;

    [Header("Onde aparecem")]
    [Tooltip("Raio em volta do jogador em que os pontos aparecem no mapa.")]
    [SerializeField] private float raioVisivelMetros = 300f;
    [Tooltip("Dois pontos nunca ficam mais perto que isso (o lugar mais importante fica).")]
    [SerializeField] private float distanciaMinimaMetros = 60f;
    [Tooltip("Cria pontos extras onde não há lugares com nome no mapa.")]
    [SerializeField] private bool pontosExtras = true;
    [Tooltip("Tamanho da grade dos pontos extras (metros). Um ponto extra por célula, se não houver um lugar perto.")]
    [SerializeField] private float celulaExtrasMetros = 220f;
    [SerializeField] private int sementeDoMundo = 2024;

    [Header("Uso")]
    [Tooltip("Se ligado, só dá para decifrar perto do ponto.")]
    [SerializeField] private bool exigirDistancia = false;
    [SerializeField] private float raioUsoMetros = 40f;
    [Tooltip("Minutos até o ponto poder ser decifrado de novo.")]
    [SerializeField] private int recargaMinutos = 5;

    private class Candidato
    {
        public string Chave, Nome, Tipo;
        public LatLng Local;
        public int Rank;
        public double Mx, My;
    }

    // Lugares que não servem de ponto de jogo (como o Pokémon GO evita).
    private static readonly HashSet<string> TiposIgnorados = new HashSet<string>
    {
        "office", "company", "parking", "bicycle_parking", "motorcycle_parking", "toilets", "atm", "vending_machine",
        "bicycle_rental", "car_rental", "car", "car_repair", "car_wash", "post_box", "telephone", "recycling",
        "waste_basket", "waste_disposal", "bench", "shelter", "lawyer", "estate_agent", "insurance", "accountant",
        "dentist", "veterinary", "funeral_directors", "storage_rental", "laundry", "dry_cleaning", "hairdresser", "beauty"
    };

    private readonly Dictionary<string, PontoDados> _ativos = new Dictionary<string, PontoDados>();
    private readonly List<Candidato> _escolhidos = new List<Candidato>();
    private readonly List<MapPoi> _pois = new List<MapPoi>();
    private readonly HashSet<string> _vistos = new HashSet<string>();
    private readonly List<string> _remover = new List<string>();
    private int _versaoPois = -1;
    private float _proximaAtualizacao;

    public float RaioUsoMetros => raioUsoMetros;
    public int RecargaSegundos => Mathf.Max(1, recargaMinutos) * 60;
    public LightshipMapView MapView => _mapView;

    private void Awake()
    {
        // A cena tem um segundo CoverageManager sem camada; só o que coloca os pontos responde.
        if (_cubeGOP != null || Instancia == null) Instancia = this;
        if (_mapView == null) _mapView = FindFirstObjectByType<LightshipMapView>();
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    private void Update()
    {
        if (_mapView == null || _cubeGOP == null || Time.unscaledTime < _proximaAtualizacao) return;
        _proximaAtualizacao = Time.unscaledTime + 1f;

        // A lista de lugares só é refeita quando o mapa carrega ou descarta uma área.
        if (_versaoPois != _mapView.PoisVersion)
        {
            _versaoPois = _mapView.PoisVersion;
            EscolherLugares();
        }
        Atualizar();
    }

    // ------------------------------------------------------------ Escolha dos pontos

    /// <summary>
    /// De todos os lugares carregados, fica com os que não têm outro mais importante por perto.
    /// A regra só depende dos vizinhos, então o mesmo lugar é escolhido não importa de onde o jogador venha.
    /// </summary>
    private void EscolherLugares()
    {
        _pois.Clear();
        _mapView.GetPois(_pois);
        _escolhidos.Clear();

        var todos = new List<Candidato>(_pois.Count);
        foreach (var p in _pois)
        {
            if (TiposIgnorados.Contains(p.Class ?? "") || TiposIgnorados.Contains(p.Subclass ?? "")) continue;
            todos.Add(new Candidato
            {
                Chave = Chave(p.Location),
                Nome = p.Name,
                Tipo = string.IsNullOrEmpty(p.Subclass) ? p.Class : p.Subclass,
                Local = p.Location,
                Rank = p.Rank,
                Mx = WebMercator.ToMercX(p.Location.Longitude),
                My = WebMercator.ToMercY(p.Location.Latitude)
            });
        }

        // Ordena pela importância; empate decidido pela chave (igual para todos os jogadores).
        todos.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : string.CompareOrdinal(a.Chave, b.Chave));

        // Grade para achar vizinhos rápido.
        var cos = Math.Cos(_mapView.MapCenter.Latitude * Math.PI / 180.0);
        var minMerc = distanciaMinimaMetros / Math.Max(0.05, cos);
        var grade = new Dictionary<long, List<Candidato>>();
        foreach (var c in todos)
        {
            var gx = (long)Math.Floor(c.Mx / minMerc);
            var gy = (long)Math.Floor(c.My / minMerc);
            var livre = true;
            for (var dx = -1; dx <= 1 && livre; dx++)
            for (var dy = -1; dy <= 1 && livre; dy++)
            {
                if (!grade.TryGetValue(((gx + dx) << 32) ^ (gy + dy), out var lista)) continue;
                foreach (var o in lista)
                {
                    var ex = o.Mx - c.Mx;
                    var ey = o.My - c.My;
                    if (ex * ex + ey * ey < minMerc * minMerc) { livre = false; break; }
                }
            }
            if (!livre) continue;
            var k = (gx << 32) ^ gy;
            if (!grade.TryGetValue(k, out var celula)) grade[k] = celula = new List<Candidato>();
            celula.Add(c);
            _escolhidos.Add(c);
        }
    }

    private void Atualizar()
    {
        var centro = _mapView.MapCenter;
        _vistos.Clear();

        foreach (var c in _escolhidos)
            if (LightshipMapView.DistanceMeters(centro, c.Local) <= raioVisivelMetros) Mostrar(c);

        if (pontosExtras) ColocarExtras(centro);

        _remover.Clear();
        foreach (var kv in _ativos)
            if (kv.Value == null || !_vistos.Contains(kv.Key)) _remover.Add(kv.Key);
        foreach (var chave in _remover)
        {
            if (_ativos.TryGetValue(chave, out var p) && p != null) _cubeGOP.RemoveInstance(p.gameObject);
            _ativos.Remove(chave);
        }
    }

    /// <summary>Um ponto extra por célula da grade, numa posição fixa, se não houver um lugar de verdade perto.</summary>
    private void ColocarExtras(LatLng centro)
    {
        var cos = Math.Cos(centro.Latitude * Math.PI / 180.0);
        var celMerc = Math.Max(50.0, celulaExtrasMetros) / Math.Max(0.05, cos);
        var cx = WebMercator.ToMercX(centro.Longitude);
        var cy = WebMercator.ToMercY(centro.Latitude);
        var raioMerc = raioVisivelMetros / Math.Max(0.05, cos);
        var perto = celMerc * 0.5;

        for (var ix = (long)Math.Floor((cx - raioMerc) / celMerc); ix <= (long)Math.Floor((cx + raioMerc) / celMerc); ix++)
        for (var iy = (long)Math.Floor((cy - raioMerc) / celMerc); iy <= (long)Math.Floor((cy + raioMerc) / celMerc); iy++)
        {
            var h = Hash(ix, iy);
            var mx = (ix + 0.15 + 0.7 * U(h)) * celMerc;
            var my = (iy + 0.15 + 0.7 * U(Hash(iy, ix))) * celMerc;
            var local = new LatLng(WebMercator.ToLat(my), WebMercator.ToLng(mx));
            if (LightshipMapView.DistanceMeters(centro, local) > raioVisivelMetros) continue;

            var chave = $"x{ix}_{iy}";
            if (!_ativos.ContainsKey(chave))
            {
                // Só onde dá para chegar a pé, e só depois que o mapa daquela área carregou.
                var sup = _mapView.GetSurface(local);
                if (sup != MapSurface.Ground && sup != MapSurface.Park) continue;
                var temLugar = false;
                foreach (var c in _escolhidos)
                {
                    var ex = c.Mx - mx;
                    var ey = c.My - my;
                    if (ex * ex + ey * ey < perto * perto) { temLugar = true; break; }
                }
                if (temLugar) continue;
            }
            Mostrar(new Candidato { Chave = chave, Nome = "Encrypted Data", Tipo = null, Local = local, Rank = 999, Mx = mx, My = my });
        }
    }

    private void Mostrar(Candidato c)
    {
        _vistos.Add(c.Chave);
        if (_ativos.TryGetValue(c.Chave, out var existente) && existente != null)
        {
            existente.AtualizarVisual();
            return;
        }
        var go = _cubeGOP.PlaceInstance(c.Local, "Encrypted_Data_" + c.Chave);
        if (go == null) return;
        var ponto = go.AddComponent<PontoDados>();
        ponto.Configurar(this, c.Chave, c.Nome, c.Tipo, c.Local);
        _ativos[c.Chave] = ponto;
    }

    // ------------------------------------------------------------ Uso

    public bool PodeUsar(PontoDados p) =>
        p != null && (!exigirDistancia || DistanciaAte(p) <= raioUsoMetros);

    public double DistanciaAte(PontoDados p) => LightshipMapView.DistanceMeters(_mapView.MapCenter, p.Local);

    public void Tocar(PontoDados p)
    {
        if (p == null || TelaFigma.AlgumaAberta) return;
        if (!PodeUsar(p))
        {
            AvisoNaTela.Mostrar($"Longe demais. Chegue a {raioUsoMetros:0} m para decifrar os dados.");
            return;
        }
        TelaDadosCriptografados.Abrir(p);
    }

    // ------------------------------------------------------------ Ajudantes

    private static string Chave(LatLng l) =>
        l.Latitude.ToString("F5", CultureInfo.InvariantCulture) + "_" + l.Longitude.ToString("F5", CultureInfo.InvariantCulture);

    private ulong Hash(long x, long y)
    {
        unchecked
        {
            var z = (ulong)sementeDoMundo * 0x9E3779B97F4A7C15UL ^ (ulong)x * 0xBF58476D1CE4E5B9UL ^ (ulong)y * 0x94D049BB133111EBUL ^ 0xD47AUL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    private static double U(ulong h) => (h >> 11) * (1.0 / (1UL << 53));
}
