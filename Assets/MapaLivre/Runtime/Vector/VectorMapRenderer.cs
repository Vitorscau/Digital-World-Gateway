using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Niantic.Lightship.Maps.Core;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace Niantic.Lightship.Maps.Vector
{
    /// <summary>
    /// Mapa vetorial estilo Pokémon GO: baixa os vector tiles (OpenFreeMap / OpenStreetMap),
    /// monta malhas de chão, parques, água, ruas e prédios e pinta com os materiais do tema.
    /// </summary>
    [AddComponentMenu("")]
    internal class VectorMapRenderer : MonoBehaviour
    {
        public const int Zoom = 14; // nível máximo de detalhe do OpenFreeMap

        private class VTile
        {
            public int X, Y;
            public double West, North, Size;
            public GameObject Go;
            public bool Loading;
            public int Attempts;
            public SurfaceIndex Surface;
            public List<MapPoi> Pois;
        }

        private LightshipMapView _view;
        private LightshipMapManager _settings;
        private MapaLivreTheme _theme;
        private BuildSettings _build;
        private readonly Dictionary<long, VTile> _tiles = new Dictionary<long, VTile>();
        private readonly List<long> _toRemove = new List<long>();
        private string _template;
        private bool _resolvingTemplate;
        private int _active;
        private string _cacheDir;

        private bool _fogWas;
        private FogMode _fogModeWas;
        private Color _fogColorWas;
        private float _fogStartWas, _fogEndWas;
        private bool _fogApplied;

        public void Initialize(LightshipMapView view, LightshipMapManager settings, MapaLivreTheme theme)
        {
            _view = view;
            _settings = settings;
            _theme = theme;
            _cacheDir = Path.Combine(Application.persistentDataPath, "MapTilesVector");
            _build = CreateBuildSettings(theme);
            ApplySky();
        }

        internal static BuildSettings CreateBuildSettings(MapaLivreTheme t)
        {
            var b = new BuildSettings
            {
                MajorRoadWidth = t.MajorRoadWidth,
                MinorRoadWidth = t.MinorRoadWidth,
                PathWidth = t.PathWidth,
                WaterwayWidth = t.WaterwayWidth,
                RoadOutlineWidth = t.RoadOutline != null ? t.RoadOutlineWidth : 0f,
                ShowBuildings = t.ShowBuildings,
                BuildingHeightScale = t.BuildingHeightScale,
                BuildingMinHeight = t.BuildingMinHeight,
                BuildingMaxHeight = t.BuildingMaxHeight,
                TextureRepeatMeters = t.TextureRepeatMeters
            };
            foreach (MapCategory c in Enum.GetValues(typeof(MapCategory)))
            {
                var m = MaterialFor(t, c);
                b.Tiling[c] = m != null && m.HasProperty("_BaseMap") ? m.GetTextureScale("_BaseMap") : Vector2.one;
            }
            return b;
        }

        internal static Material MaterialFor(MapaLivreTheme t, MapCategory c)
        {
            switch (c)
            {
                case MapCategory.Ground: return t.Ground;
                case MapCategory.Special: return t.Special != null ? t.Special : t.Ground;
                case MapCategory.Sand: return t.Sand != null ? t.Sand : t.Ground;
                case MapCategory.Forest: return t.Forest != null ? t.Forest : t.Park;
                case MapCategory.Park: return t.Park;
                case MapCategory.Water: return t.Water;
                case MapCategory.Path: return t.Path != null ? t.Path : t.MinorRoad;
                case MapCategory.RoadCasing: return t.RoadOutline;
                case MapCategory.MinorRoad: return t.MinorRoad;
                case MapCategory.MajorRoad: return t.MajorRoad != null ? t.MajorRoad : t.MinorRoad;
                case MapCategory.BuildingTop: return t.BuildingTop;
                case MapCategory.BuildingWall: return t.BuildingWall != null ? t.BuildingWall : t.BuildingTop;
                default: return null;
            }
        }

        private void ApplySky()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = _theme.SkyColor;
            }
            if (!_theme.UseFog) return;
            _fogWas = RenderSettings.fog;
            _fogModeWas = RenderSettings.fogMode;
            _fogColorWas = RenderSettings.fogColor;
            _fogStartWas = RenderSettings.fogStartDistance;
            _fogEndWas = RenderSettings.fogEndDistance;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = _theme.SkyColor;
            RenderSettings.fogStartDistance = _theme.FogStart;
            RenderSettings.fogEndDistance = _theme.FogEnd;
            _fogApplied = true;
        }

        private void LateUpdate()
        {
            if (_view == null) return;
            if (_template == null)
            {
                if (!_resolvingTemplate) StartCoroutine(ResolveTemplate());
                return;
            }

            var tileMerc = WebMercator.TileSizeMerc(Zoom);
            var radiusMerc = _settings.VectorLoadRadiusMeters / Math.Max(0.01, _view.MetersPerMerc);
            var minX = (int)Math.Floor((_view.CenterMercX - radiusMerc + WebMercator.HalfCircumference) / tileMerc);
            var maxX = (int)Math.Floor((_view.CenterMercX + radiusMerc + WebMercator.HalfCircumference) / tileMerc);
            var minY = (int)Math.Floor((WebMercator.HalfCircumference - (_view.CenterMercY + radiusMerc)) / tileMerc);
            var maxY = (int)Math.Floor((WebMercator.HalfCircumference - (_view.CenterMercY - radiusMerc)) / tileMerc);

            _toRemove.Clear();
            foreach (var kv in _tiles)
            {
                var t = kv.Value;
                if (t.X < minX - 1 || t.X > maxX + 1 || t.Y < minY - 1 || t.Y > maxY + 1) _toRemove.Add(kv.Key);
            }
            foreach (var key in _toRemove)
            {
                DestroyTile(_tiles[key]);
                _tiles.Remove(key);
            }

            var k = (float)(_view.MetersPerMerc * _view.UnitsPerMeter);
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var key = ((long)x << 32) | (uint)y;
                if (!_tiles.TryGetValue(key, out var tile))
                {
                    tile = new VTile
                    {
                        X = x,
                        Y = y,
                        Size = tileMerc,
                        West = x * tileMerc - WebMercator.HalfCircumference,
                        North = WebMercator.HalfCircumference - y * tileMerc,
                        Go = new GameObject($"VectorTile_{Zoom}_{x}_{y}")
                    };
                    tile.Go.transform.SetParent(transform, false);
                    _tiles[key] = tile;
                }

                tile.Go.transform.position = _view.MercToScene(tile.West, tile.North);
                tile.Go.transform.localScale = new Vector3(k, _view.UnitsPerMeter, k);

                if (!tile.Loading && tile.Attempts < 3 && tile.Go.transform.childCount == 0 && _active < _settings.MaxConcurrentDownloads)
                    StartCoroutine(Load(tile));
            }
        }

        private IEnumerator ResolveTemplate()
        {
            _resolvingTemplate = true;
            const string prefKey = "MapaLivre.VectorTemplate";
            var url = _settings.VectorTileJsonUrl;
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var m = Regex.Match(req.downloadHandler.text, "\"tiles\"\\s*:\\s*\\[\\s*\"([^\"]+)\"");
                    if (m.Success)
                    {
                        _template = m.Groups[1].Value.Replace("\\/", "/");
                        PlayerPrefs.SetString(prefKey, _template);
                    }
                }
            }

            // Sem internet: usa o último endereço conhecido (os tiles podem estar no cache).
            if (_template == null && PlayerPrefs.HasKey(prefKey)) _template = PlayerPrefs.GetString(prefKey);
            if (_template == null)
            {
                Debug.LogWarning("[MapaLivre] Não consegui ler o TileJSON do mapa vetorial; tentando de novo em 10 s.");
                yield return new WaitForSeconds(10f);
            }
            _resolvingTemplate = false;
        }

        private IEnumerator Load(VTile tile)
        {
            if (_template == null) yield break;
            tile.Loading = true;
            tile.Attempts++;
            _active++;
            byte[] bytes = null;
            try
            {
                var path = Path.Combine(_cacheDir, Zoom.ToString(), tile.X.ToString(), tile.Y + ".pbf");
                if (_settings.DiskCache && File.Exists(path) &&
                    (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalDays < 30)
                {
                    try { bytes = File.ReadAllBytes(path); } catch (Exception) { bytes = null; }
                }

                if (bytes == null)
                {
                    var url = _template.Replace("{z}", Zoom.ToString()).Replace("{x}", tile.X.ToString()).Replace("{y}", tile.Y.ToString());
                    using (var req = UnityWebRequest.Get(url))
                    {
                        req.timeout = 30;
                        yield return req.SendWebRequest();
                        if (req.result != UnityWebRequest.Result.Success)
                        {
                            Debug.LogWarning($"[MapaLivre] Falha ao baixar {url}: {req.error}");
                            if (req.responseCode == 404) _template = null; // versão do mapa mudou
                            yield return new WaitForSeconds(3f);
                            yield break;
                        }
                        bytes = Decompress(req.downloadHandler.data);
                    }

                    if (_settings.DiskCache)
                    {
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                            File.WriteAllBytes(path, bytes);
                        }
                        catch (Exception e) { Debug.LogWarning($"[MapaLivre] Cache: {e.Message}"); }
                    }
                }

                // Monta a geometria fora da thread principal.
                var k = Math.Cos(WebMercator.ToLat(tile.North - tile.Size / 2) * Math.PI / 180.0);
                var data = bytes;
                var task = Task.Run(() =>
                {
                    var built = VectorTileBuilder.Build(data, tile.West, tile.North, tile.Size, k, _build);
                    return (built, SurfaceIndex.From(built), PoiReader.Read(data, tile.West, tile.North, tile.Size));
                });
                while (!task.IsCompleted) yield return null;
                if (task.IsFaulted)
                {
                    Debug.LogError($"[MapaLivre] Erro montando o tile {tile.X},{tile.Y}: {task.Exception?.GetBaseException()}");
                    yield break;
                }
                if (tile.Go == null) yield break;
                CreateMeshes(tile.Go.transform, task.Result.built, _theme);
                tile.Surface = task.Result.Item2;
                tile.Pois = task.Result.Item3;
                PoisVersion++;
            }
            finally
            {
                _active--;
                tile.Loading = false;
            }
        }

        /// <summary>O que existe num ponto (coordenadas Mercator). Unknown se o tile ainda não carregou.</summary>
        public MapSurface GetSurface(double mercX, double mercY)
        {
            var tileMerc = WebMercator.TileSizeMerc(Zoom);
            var x = (int)Math.Floor((mercX + WebMercator.HalfCircumference) / tileMerc);
            var y = (int)Math.Floor((WebMercator.HalfCircumference - mercY) / tileMerc);
            var key = ((long)x << 32) | (uint)y;
            if (!_tiles.TryGetValue(key, out var tile) || tile.Surface == null) return MapSurface.Unknown;
            return tile.Surface.Query((float)(mercX - tile.West), (float)(mercY - tile.North));
        }

        /// <summary>Muda sempre que lugares entram ou saem (tiles carregados ou descartados).</summary>
        public int PoisVersion { get; private set; }

        /// <summary>Junta em <paramref name="output"/> os lugares com nome de todos os tiles carregados.</summary>
        public void GetPois(List<MapPoi> output)
        {
            foreach (var t in _tiles.Values)
                if (t.Pois != null) output.AddRange(t.Pois);
        }

        internal static byte[] Decompress(byte[] data)
        {
            if (data == null || data.Length < 2 || data[0] != 0x1f || data[1] != 0x8b) return data;
            using (var input = new MemoryStream(data))
            using (var gz = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gz.CopyTo(output);
                return output.ToArray();
            }
        }

        internal static void CreateMeshes(Transform parent, Dictionary<MapCategory, MeshData> meshes, MapaLivreTheme theme)
        {
            foreach (var kv in meshes)
            {
                var mat = MaterialFor(theme, kv.Key);
                if (mat == null || kv.Value.Triangles.Count == 0) continue;
                var mesh = new Mesh { name = kv.Key.ToString(), indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(kv.Value.Vertices);
                mesh.SetUVs(0, kv.Value.Uvs);
                mesh.SetTriangles(kv.Value.Triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject(kv.Key.ToString());
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = kv.Key == MapCategory.Ground;
            }
        }

        private void DestroyTile(VTile t)
        {
            if (t.Pois != null) { t.Pois = null; PoisVersion++; }
            if (t.Go == null) return;
            foreach (var mf in t.Go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
            Destroy(t.Go);
            t.Go = null;
        }

        private void OnDestroy()
        {
            foreach (var t in _tiles.Values) DestroyTile(t);
            if (_fogApplied)
            {
                RenderSettings.fog = _fogWas;
                RenderSettings.fogMode = _fogModeWas;
                RenderSettings.fogColor = _fogColorWas;
                RenderSettings.fogStartDistance = _fogStartWas;
                RenderSettings.fogEndDistance = _fogEndWas;
            }
        }
    }
}
