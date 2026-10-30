using UnityEngine;

namespace Niantic.Lightship.Maps
{
    /// <summary>
    /// Configuração da fonte dos tiles do mapa. Antes era o servidor da Niantic (desligado);
    /// agora são tiles raster no padrão {z}/{x}/{y} (OpenStreetMap por padrão).
    /// </summary>
    public enum MapaLivreStyle
    {
        [Tooltip("Mapa desenhado com os materiais do tema (estilo Pokémon GO).")]
        Vetorial,
        [Tooltip("Imagens prontas do OpenStreetMap (com nomes de ruas).")]
        Imagem
    }

    public class LightshipMapManager : MonoBehaviour
    {
        [SerializeField] private string _labelLanguage = "en";

        [Header("Estilo (MapaLivre)")]
        [SerializeField] private MapaLivreStyle _style = MapaLivreStyle.Vetorial;
        [Tooltip("Tema do mapa vetorial. Vazio = Resources/MapaLivre/TemaOriginal.")]
        [SerializeField] private MapaLivreTheme _theme;
        [SerializeField] private string _vectorTileJsonUrl = "https://tiles.openfreemap.org/planet";
        [Tooltip("Raio (metros) em volta do jogador onde o mapa vetorial é carregado.")]
        [SerializeField] private float _vectorLoadRadiusMeters = 700f;
        [SerializeField] private string _vectorAttribution = "© OpenFreeMap © OpenMapTiles © OpenStreetMap contributors";

        [Header("Imagem (MapaLivre)")]
        [Tooltip("URL no padrão {z}/{x}/{y}. {s} vira um subdomínio (a, b, c, d).")]
        [SerializeField] private string _tileUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
        [SerializeField] private string _subdomains = "";
        [Tooltip("Crédito exigido pela licença dos tiles.")]
        [SerializeField] private string _attribution = "© OpenStreetMap contributors";
        [SerializeField] private bool _showAttribution = true;
        [Tooltip("Zoom dos tiles: 18 = nítido de perto, 17 = mais leve.")]
        [Range(14, 19)] [SerializeField] private int _zoom = 18;
        [Tooltip("Raio (metros) em volta do jogador onde os tiles são carregados.")]
        [SerializeField] private float _loadRadiusMeters = 350f;
        [SerializeField] private Color _tint = Color.white;
        [SerializeField] private Material _tileMaterial;
        [SerializeField] private int _maxConcurrentDownloads = 4;
        [SerializeField] private bool _diskCache = true;

        public string LabelLanguage => _labelLanguage;
        public MapaLivreStyle Style => _style;
        public string VectorTileJsonUrl => _vectorTileJsonUrl;
        public float VectorLoadRadiusMeters => _vectorLoadRadiusMeters;

        public MapaLivreTheme Theme
        {
            get
            {
                if (_theme == null) _theme = Resources.Load<MapaLivreTheme>("MapaLivre/TemaOriginal");
                return _theme;
            }
        }
        public string TileUrl { get { MigrateOldUrl(); return _tileUrl; } }
        public string Subdomains => _subdomains;
        public string Attribution => _attribution;
        public bool ShowAttribution => _showAttribution;
        public int Zoom => _zoom;
        public float LoadRadiusMeters => _loadRadiusMeters;
        public Color Tint => _tint;
        public Material TileMaterial => _tileMaterial;
        public int MaxConcurrentDownloads => Mathf.Max(1, _maxConcurrentDownloads);
        public bool DiskCache => _diskCache;

        private void Awake() => MigrateOldUrl();
        private void OnValidate() => MigrateOldUrl();

        // A primeira versão usava os tiles do CARTO, que passaram a exigir chave.
        private void MigrateOldUrl()
        {
            if (string.IsNullOrEmpty(_tileUrl) || !_tileUrl.Contains("cartocdn.com")) return;
            _tileUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
            _subdomains = "";
            _attribution = "© OpenStreetMap contributors";
        }

        private void OnGUI()
        {
            var text = _style == MapaLivreStyle.Vetorial ? _vectorAttribution : _attribution;
            if (!_showAttribution || string.IsNullOrEmpty(text)) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(10, Screen.height / 90) };
            style.normal.textColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            var size = style.CalcSize(new GUIContent(text));
            GUI.Label(new Rect(6, Screen.height - size.y - 4, size.x, size.y), text, style);
        }
    }
}
