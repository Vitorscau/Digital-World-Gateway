using System;
using System.Collections.Generic;
using System.Text;
using Niantic.Lightship.Maps;
using Niantic.Lightship.Maps.Core;
using Niantic.Lightship.Maps.Core.Coordinates;
using Niantic.Lightship.Maps.MapLayers.Components;
using UnityEngine;

/// <summary>
/// Spawn de Digimons no estilo Pokémon GO.
///
/// - O mundo é dividido numa grade fixa. Cada célula tem alguns pontos de spawn em lugares fixos,
///   calculados a partir das coordenadas (iguais para todo mundo, sem servidor).
/// - Cada ponto fica ativo por alguns minutos a cada ciclo (ex.: 30 min por hora), num horário próprio.
/// - O Digimon de cada ponto é sorteado pelo ponto + ciclo: quem estiver ali na mesma hora vê o mesmo Digimon.
/// - O sorteio respeita o habitat do lugar (água, parque, cidade), o período do dia e o peso de cada Digimon.
/// - Só aparecem os pontos dentro do raio de visão. Depois de um encontro, o Digimon some para este jogador.
/// </summary>
public class _DigimonGOP : MonoBehaviour
{
    public static _DigimonGOP Instance { get; private set; }

    [SerializeField] private LightshipMapView lightshipMapView;
    public Camera _mapCamera;

    [Header("Tabela de spawn")]
    [SerializeField] private List<GameObjectProbability> digimonLayers = new List<GameObjectProbability>();

    [Header("Pontos de spawn")]
    [Tooltip("Tamanho da célula da grade (metros no equador; ~70 m no sul do Brasil).")]
    [SerializeField] private float tamanhoCelula = 80f;
    [Tooltip("Quantos pontos de spawn uma célula pode ter, no máximo.")]
    [Range(1, 6)] [SerializeField] private int pontosPorCelula = 3;
    [Tooltip("Chance de cada um desses pontos existir. Controla a densidade de spawns.")]
    [Range(0f, 1f)] [SerializeField] private float chanceDePonto = 0.35f;
    [Tooltip("Muda todos os pontos e sorteios do mundo. Todos os jogadores precisam usar o mesmo valor.")]
    [SerializeField] private int sementeDoMundo = 2024;

    [Header("Tempo")]
    [Tooltip("De quantos em quantos minutos cada ponto volta a gerar um Digimon.")]
    [SerializeField] private int cicloMinutos = 60;
    [Tooltip("Por quantos minutos o Digimon fica no mapa.")]
    [SerializeField] private int duracaoMinutos = 30;
    [SerializeField] private int horaInicioDoDia = 6;
    [SerializeField] private int horaFimDoDia = 18;

    [Header("Distâncias")]
    [Tooltip("Raio em volta do jogador em que os Digimons aparecem no mapa.")]
    [SerializeField] private float raioVisivelMetros = 200f;
    [Tooltip("Se ligado, só dá para batalhar com Digimons dentro do raio de encontro. Desligado: qualquer Digimon visível.")]
    [SerializeField] private bool exigirDistanciaParaBatalhar = false;
    [Tooltip("Distância máxima para tocar no Digimon e começar o encontro (se a opção acima estiver ligada).")]
    [SerializeField] private float raioEncontroMetros = 40f;
    [SerializeField] private float intervaloAtualizacao = 1f;

    private class Ativo
    {
        public WildDigimon Wild;
        public LayerGameObjectPlacement Layer;
        public DateTime Fim;
        public bool Visto;
    }

    private const string PrefsVistos = "DigimonNexus.SpawnsVistos";
    private readonly Dictionary<string, Ativo> _ativos = new Dictionary<string, Ativo>();
    private readonly Dictionary<string, long> _vistos = new Dictionary<string, long>();
    private readonly List<string> _remover = new List<string>();
    private readonly List<GameObjectProbability> _candidatos = new List<GameObjectProbability>();
    private readonly List<float> _pesos = new List<float>();
    private float _proximaAtualizacao;

    public float RaioEncontroMetros => raioEncontroMetros;
    public LightshipMapView MapView => lightshipMapView;

    /// <summary>As espécies da tabela de spawn (as que aparecem no mapa).</summary>
    public IEnumerable<DigimonBase> Especies
    {
        get
        {
            foreach (var e in _tabela.Count > 0 ? _tabela : digimonLayers)
            {
                if (e == null) continue;
                var ficha = e.digimon != null ? e.digimon : e.gameObjectPlacement != null ? CatalogoDigimon.Buscar(e.gameObjectPlacement.Name) : null;
                if (ficha != null) yield return ficha;
            }
        }
    }

    /// <summary>Digimons visíveis agora (para uma lista de "por perto", por exemplo).</summary>
    public IEnumerable<WildDigimon> Visiveis
    {
        get
        {
            foreach (var a in _ativos.Values)
                if (a.Wild != null) yield return a.Wild;
        }
    }

    private void Awake()
    {
        Instance = this;
        if (lightshipMapView == null) lightshipMapView = FindFirstObjectByType<LightshipMapView>();
        CarregarVistos();
        MontarTabela();
    }

    // ------------------------------------------------------------------ Tabela (cena + fichas)

    [Header("Todos os Digimon com ficha")]
    [Tooltip("Também sorteia os Digimon que têm ficha em Resources/Digimons mas não estão na tabela acima " +
             "(raridade, habitat, horário e nível vêm do estágio e do elemento de cada um).")]
    [SerializeField] private bool incluirTodasAsFichas = true;
    [Tooltip("Multiplica a chance de todos esses Digimon extras juntos.")]
    [SerializeField] private float pesoFichasExtras = 1f;

    private readonly List<GameObjectProbability> _tabela = new List<GameObjectProbability>();

    /// <summary>A tabela da cena mais uma entrada para cada ficha que ainda não está nela.</summary>
    private void MontarTabela()
    {
        _tabela.Clear();
        _tabela.AddRange(digimonLayers);
        if (!incluirTodasAsFichas) return;

        // Uma camada da cena serve de "anfitriã" para colocar os modelos no mapa.
        LayerGameObjectPlacement anfitria = null;
        var naTabela = new HashSet<string>();
        foreach (var e in digimonLayers)
        {
            if (e?.gameObjectPlacement == null) continue;
            if (anfitria == null) anfitria = e.gameObjectPlacement;
            naTabela.Add(CatalogoDigimon.Normalizar(e.gameObjectPlacement.Name));
            if (e.digimon != null) naTabela.Add(CatalogoDigimon.Normalizar(e.digimon.name));
        }
        if (anfitria == null) return;

        var fichas = new List<DigimonBase>(CatalogoDigimon.Todos);
        fichas.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); // ordem fixa: o sorteio é igual para todos
        var vistas = new HashSet<DigimonBase>();
        foreach (var f in fichas)
        {
            if (f == null || f.Prefab == null || !vistas.Add(f)) continue;
            if (naTabela.Contains(CatalogoDigimon.Normalizar(f.name)) || naTabela.Contains(CatalogoDigimon.Normalizar(f.Name ?? ""))) continue;
            _tabela.Add(EntradaDaFicha(f, anfitria));
        }
    }

    private GameObjectProbability EntradaDaFicha(DigimonBase f, LayerGameObjectPlacement anfitria)
    {
        var e = new GameObjectProbability
        {
            gameObjectPlacement = anfitria,
            digimon = f,
            modeloExtra = f.Prefab,
            bonusHabitat = 2f
        };
        switch (f.Stage)
        {
            case DigimonStage.Fresh: e.probability = 0.02f; e.nivel = new Vector2Int(1, 3); break;
            case DigimonStage.InTraining: e.probability = 0.025f; e.nivel = new Vector2Int(1, 5); break;
            case DigimonStage.Rookie: e.probability = 0.02f; e.nivel = new Vector2Int(3, 12); break;
            case DigimonStage.Champion:
            case DigimonStage.Armor: e.probability = 0.008f; e.nivel = new Vector2Int(10, 22); e.tamerMinimo = 3; break;
            case DigimonStage.Ultimate: e.probability = 0.003f; e.nivel = new Vector2Int(20, 35); e.tamerMinimo = 8; break;
            default: e.probability = 0.001f; e.nivel = new Vector2Int(35, 50); e.tamerMinimo = 15; break;
        }
        e.probability *= Mathf.Max(0f, pesoFichasExtras);
        switch (f.Atribute)
        {
            case DigimonAttribute.Water: e.habitats = HabitatDigimon.Agua; break;
            case DigimonAttribute.Plant: e.habitats = HabitatDigimon.Parque; break;
            case DigimonAttribute.Earth:
            case DigimonAttribute.Wind: e.habitats = HabitatDigimon.Parque | HabitatDigimon.Urbano; break;
            case DigimonAttribute.Fire:
            case DigimonAttribute.Electric: e.habitats = HabitatDigimon.Urbano; break;
            default: e.habitats = HabitatDigimon.Todos; break;
        }
        e.periodo = f.Atribute == DigimonAttribute.Dark ? PeriodoDoDia.Noite
            : f.Atribute == DigimonAttribute.Light ? PeriodoDoDia.Dia
            : PeriodoDoDia.Sempre;
        return e;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (lightshipMapView == null || Time.unscaledTime < _proximaAtualizacao) return;
        _proximaAtualizacao = Time.unscaledTime + intervaloAtualizacao;
        Atualizar(DateTime.UtcNow);
    }

    // ------------------------------------------------------------------ Spawn

    private LatLng? _ultimaPosicao;
    private float _metrosPendentes;

    /// <summary>Conta a distância andada (para missões). Saltos grandes (GPS pulando) não contam.</summary>
    private void ContarCaminhada(LatLng centro)
    {
        if (_ultimaPosicao.HasValue)
        {
            var d = (float)LightshipMapView.DistanceMeters(_ultimaPosicao.Value, centro);
            if (d < 80f * intervaloAtualizacao) _metrosPendentes += d;
        }
        _ultimaPosicao = centro;
        if (_metrosPendentes < 25f) return;
        var metros = Mathf.FloorToInt(_metrosPendentes);
        _metrosPendentes -= metros;
        PerfilJogador.Atual.metrosAndados += metros;
        Missoes.RegistrarCaminhada(metros); // já salva o perfil
    }

    private void Atualizar(DateTime agoraUtc)
    {
        foreach (var a in _ativos.Values) a.Visto = false;

        var centro = lightshipMapView.MapCenter;
        ContarCaminhada(centro);
        var cx = WebMercator.ToMercX(centro.Longitude);
        var cy = WebMercator.ToMercY(centro.Latitude);
        var cosLat = Math.Cos(centro.Latitude * Math.PI / 180.0);
        var raioMerc = raioVisivelMetros / Math.Max(0.05, cosLat);
        var cel = Math.Max(10.0, tamanhoCelula);
        var minutoAgora = (long)Math.Floor((agoraUtc - DateTime.UnixEpoch).TotalMinutes);
        var ehDia = EhDia(DateTime.Now.Hour);
        var ciclo = Math.Max(1, cicloMinutos);
        var duracao = Mathf.Clamp(duracaoMinutos, 1, ciclo);

        var ix0 = (long)Math.Floor((cx - raioMerc) / cel);
        var ix1 = (long)Math.Floor((cx + raioMerc) / cel);
        var iy0 = (long)Math.Floor((cy - raioMerc) / cel);
        var iy1 = (long)Math.Floor((cy + raioMerc) / cel);

        for (var ix = ix0; ix <= ix1; ix++)
        for (var iy = iy0; iy <= iy1; iy++)
        for (var s = 0; s < pontosPorCelula; s++)
        {
            // 1) O ponto existe? Onde fica? (fixo para sempre)
            if (U(Hash(ix, iy, s, 0)) >= chanceDePonto) continue;
            var mx = (ix + U(Hash(ix, iy, s, 1))) * cel;
            var my = (iy + U(Hash(ix, iy, s, 2))) * cel;
            var local = new LatLng(WebMercator.ToLat(my), WebMercator.ToLng(mx));
            if (LightshipMapView.DistanceMeters(centro, local) > raioVisivelMetros) continue;

            // 2) Está ativo agora? Cada ponto tem seu próprio minuto de início dentro do ciclo.
            var inicio = (long)(Hash(ix, iy, s, 3) % (ulong)ciclo);
            var t = minutoAgora - inicio;
            var cicloAtual = FloorDiv(t, ciclo);
            if (t - cicloAtual * ciclo >= duracao) continue;

            var id = $"{ix}_{iy}_{s}_{cicloAtual}";
            if (_vistos.ContainsKey(id)) continue;
            if (_ativos.TryGetValue(id, out var existente))
            {
                existente.Visto = true;
                continue;
            }

            // 3) Que Digimon? Depende do lugar (habitat), do período e do sorteio deste ciclo.
            var superficie = lightshipMapView.GetSurface(local);
            if (superficie == MapSurface.Unknown) continue; // mapa dessa área ainda carregando
            var habitat = superficie == MapSurface.Water ? HabitatDigimon.Agua
                : superficie == MapSurface.Park ? HabitatDigimon.Parque
                : HabitatDigimon.Urbano;

            var entrada = Sortear(habitat, ehDia, U(Hash(ix, iy, s, 4, cicloAtual)));
            if (entrada == null) continue;

            var nivelMin = Mathf.Min(entrada.nivel.x, entrada.nivel.y);
            var nivelMax = Mathf.Max(entrada.nivel.x, entrada.nivel.y);
            var nivel = nivelMin + (int)(Hash(ix, iy, s, 5, cicloAtual) % (ulong)(nivelMax - nivelMin + 1));
            var giro = Quaternion.Euler(0f, (float)(U(Hash(ix, iy, s, 6, cicloAtual)) * 360.0), 0f);
            var fim = DateTime.UnixEpoch.AddMinutes(cicloAtual * ciclo + inicio + duracao);

            // Entradas das fichas trazem o próprio modelo; as da cena usam o da camada.
            var modelo = entrada.modeloExtra != null ? entrada.modeloExtra : entrada.gameObjectPlacement.Prefab;
            var especie = entrada.modeloExtra != null && entrada.digimon != null ? entrada.digimon.name : entrada.gameObjectPlacement.Name;
            var go = entrada.gameObjectPlacement.PlaceInstance(modelo, local, giro, $"{especie}_{id}");
            if (go == null) continue;
            var wild = go.GetComponent<WildDigimon>();
            if (wild == null) wild = go.AddComponent<WildDigimon>();
            var ficha = entrada.digimon != null ? entrada.digimon : CatalogoDigimon.Buscar(especie);
            wild.Configurar(this, id, especie, nivel, fim, local, habitat, ficha, modelo);
            _ativos[id] = new Ativo { Wild = wild, Layer = entrada.gameObjectPlacement, Fim = fim, Visto = true };
        }

        // 4) Some com o que saiu do raio, acabou o tempo ou já foi encontrado.
        _remover.Clear();
        foreach (var kv in _ativos)
            if (!kv.Value.Visto || kv.Value.Fim <= agoraUtc || kv.Value.Wild == null || _vistos.ContainsKey(kv.Key))
                _remover.Add(kv.Key);
        foreach (var id in _remover) Remover(id);
    }

    /// <summary>Para testes: sorteia como no mapa (sorteio de 0 a 1).</summary>
    public GameObjectProbability SortearParaTeste(HabitatDigimon habitat, bool ehDia, double sorteio) => Sortear(habitat, ehDia, sorteio);

    private GameObjectProbability Sortear(HabitatDigimon habitat, bool ehDia, double sorteio)
    {
        _candidatos.Clear();
        _pesos.Clear();
        var total = 0f;
        var nivelTamer = PerfilJogador.Atual.nivel;
        if (_tabela.Count == 0) MontarTabela();
        foreach (var e in _tabela)
        {
            if (e == null || e.gameObjectPlacement == null || e.probability <= 0f) continue;
            if ((e.habitats & habitat) == 0) continue;
            if (e.periodo == PeriodoDoDia.Dia && !ehDia) continue;
            if (e.periodo == PeriodoDoDia.Noite && ehDia) continue;
            var peso = e.probability;
            // Especialistas (que não vivem em todo lugar) aparecem mais no habitat deles.
            if (e.habitats != HabitatDigimon.Todos) peso *= Mathf.Max(1f, e.bonusHabitat);
            // Estágios fortes vão aparecendo conforme o Tamer sobe de nível (quase nunca antes disso).
            if (e.tamerMinimo > 0) peso *= Mathf.Clamp((nivelTamer - e.tamerMinimo + 1) / 3f, 0.02f, 1f);
            _candidatos.Add(e);
            _pesos.Add(peso);
            total += peso;
        }
        if (total <= 0f) return null;

        var alvo = sorteio * total;
        for (var i = 0; i < _candidatos.Count; i++)
        {
            alvo -= _pesos[i];
            if (alvo < 0) return _candidatos[i];
        }
        return _candidatos[_candidatos.Count - 1];
    }

    private void Remover(string id)
    {
        if (!_ativos.TryGetValue(id, out var a)) return;
        if (a.Wild != null) a.Layer.RemoveInstance(a.Wild.gameObject);
        _ativos.Remove(id);
    }

    private bool EhDia(int hora) =>
        horaInicioDoDia <= horaFimDoDia
            ? hora >= horaInicioDoDia && hora < horaFimDoDia
            : hora >= horaInicioDoDia || hora < horaFimDoDia;

    // ------------------------------------------------------------------ Encontros

    /// <summary>O jogador está perto o bastante para começar o encontro?</summary>
    public bool PodeEncontrar(WildDigimon wild) =>
        wild != null && !exigirDistanciaParaBatalhar ||
        wild != null && lightshipMapView != null &&
        LightshipMapView.DistanceMeters(lightshipMapView.MapCenter, wild.Local) <= raioEncontroMetros;

    /// <summary>Marca o Digimon como encontrado: ele some para este jogador até o fim do tempo dele.</summary>
    public void RegistrarEncontro(WildDigimon wild)
    {
        if (wild == null || string.IsNullOrEmpty(wild.SpawnId)) return;
        _vistos[wild.SpawnId] = new DateTimeOffset(wild.DespawnUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        SalvarVistos();
    }

    private void CarregarVistos()
    {
        _vistos.Clear();
        var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var item in PlayerPrefs.GetString(PrefsVistos, "").Split(';'))
        {
            var partes = item.Split('|');
            if (partes.Length == 2 && long.TryParse(partes[1], out var fim) && fim > agora)
                _vistos[partes[0]] = fim;
        }
    }

    private void SalvarVistos()
    {
        var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sb = new StringBuilder();
        foreach (var kv in _vistos)
        {
            if (kv.Value <= agora) continue;
            if (sb.Length > 0) sb.Append(';');
            sb.Append(kv.Key).Append('|').Append(kv.Value);
        }
        PlayerPrefs.SetString(PrefsVistos, sb.ToString());
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------------ Sorteio determinístico

    private ulong Hash(long a, long b, long c, long d, long e = 0)
    {
        var h = Mix((ulong)(uint)sementeDoMundo);
        h = Mix(h ^ (ulong)a);
        h = Mix(h ^ (ulong)b);
        h = Mix(h ^ (ulong)c);
        h = Mix(h ^ (ulong)d);
        return Mix(h ^ (ulong)e);
    }

    // SplitMix64: mesmo resultado em qualquer aparelho.
    private static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static double U(ulong h) => (h >> 11) * (1.0 / (1UL << 53));

    private static long FloorDiv(long a, long b)
    {
        var q = a / b;
        if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
        return q;
    }

    private void OnValidate()
    {
        duracaoMinutos = Mathf.Clamp(duracaoMinutos, 1, Mathf.Max(1, cicloMinutos));
        raioEncontroMetros = Mathf.Min(raioEncontroMetros, raioVisivelMetros);
    }
}
