using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Niantic.Lightship.Maps.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Niantic.Lightship.Maps
{
    /// <summary>Baixa, guarda em cache e desenha os tiles raster em volta do centro do mapa.</summary>
    [AddComponentMenu("")]
    internal class MapTileRenderer : MonoBehaviour
    {
        private class Tile
        {
            public int X, Y;
            public GameObject Go;
            public MeshRenderer Renderer;
            public Texture2D Texture;
            public bool Loading, Failed;
        }

        private LightshipMapView _view;
        private LightshipMapManager _settings;
        private Material _material;
        private Mesh _quad;
        private readonly Dictionary<long, Tile> _tiles = new Dictionary<long, Tile>();
        private readonly Queue<Tile> _pending = new Queue<Tile>();
        private readonly List<long> _toRemove = new List<long>();
        private int _activeDownloads;
        private string _cacheDir;
        private MaterialPropertyBlock _mpb;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public void Initialize(LightshipMapView view, LightshipMapManager settings)
        {
            _view = view;
            _settings = settings;
            _mpb = new MaterialPropertyBlock();
            _cacheDir = Path.Combine(Application.persistentDataPath, "MapTiles");

            _material = settings.TileMaterial != null ? settings.TileMaterial : Resources.Load<Material>("MapaLivre/MapTile");
            if (_material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
                _material = new Material(shader);
            }

            _quad = BuildQuad();
        }

        private static Mesh BuildQuad()
        {
            // Quadrado 1x1 deitado no plano XZ, com o canto (0,0) no sudoeste.
            var m = new Mesh { name = "MapTileQuad" };
            m.vertices = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.RecalculateBounds();
            return m;
        }

        private void LateUpdate()
        {
            if (_view == null) return;

            var zoom = _settings.Zoom;
            var tileMerc = WebMercator.TileSizeMerc(zoom);
            var radiusMerc = Math.Min(_view.MapRadius, _settings.LoadRadiusMeters) / Math.Max(0.01, _view.MetersPerMerc);
            var minX = (int)Math.Floor((_view.CenterMercX - radiusMerc + WebMercator.HalfCircumference) / tileMerc);
            var maxX = (int)Math.Floor((_view.CenterMercX + radiusMerc + WebMercator.HalfCircumference) / tileMerc);
            var minY = (int)Math.Floor((WebMercator.HalfCircumference - (_view.CenterMercY + radiusMerc)) / tileMerc);
            var maxY = (int)Math.Floor((WebMercator.HalfCircumference - (_view.CenterMercY - radiusMerc)) / tileMerc);
            var n = 1 << zoom;

            // Remove os tiles que saíram da área visível (com uma margem).
            _toRemove.Clear();
            foreach (var kv in _tiles)
            {
                var t = kv.Value;
                if (t.X < minX - 1 || t.X > maxX + 1 || t.Y < minY - 1 || t.Y > maxY + 1) _toRemove.Add(kv.Key);
            }
            foreach (var key in _toRemove)
            {
                var t = _tiles[key];
                if (t.Texture != null) Destroy(t.Texture);
                Destroy(t.Go);
                _tiles.Remove(key);
            }

            var scale = (float)(tileMerc * _view.MetersPerMerc * _view.UnitsPerMeter);
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                if (y < 0 || y >= n) continue;
                var key = ((long)x << 32) | (uint)y;
                if (!_tiles.TryGetValue(key, out var tile))
                {
                    tile = CreateTile(x, y);
                    _tiles[key] = tile;
                    tile.X = x;
                    tile.Y = y;
                    tile.Loading = true;
                    _pending.Enqueue(tile);
                }

                // Canto sudoeste do tile em coordenadas Mercator.
                var west = x * tileMerc - WebMercator.HalfCircumference;
                var south = WebMercator.HalfCircumference - (y + 1) * tileMerc;
                var pos = _view.MercToScene(west, south);
                tile.Go.transform.SetPositionAndRotation(pos, Quaternion.identity);
                tile.Go.transform.localScale = new Vector3(scale, 1f, scale);
            }

            while (_activeDownloads < _settings.MaxConcurrentDownloads && _pending.Count > 0)
            {
                var t = _pending.Dequeue();
                if (t.Go == null) continue;
                StartCoroutine(Load(t, zoom));
            }
        }

        private Tile CreateTile(int x, int y)
        {
            var go = new GameObject($"Tile_{x}_{y}");
            go.transform.SetParent(transform, true);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.enabled = false; // só aparece quando a textura chegar
            return new Tile { Go = go, Renderer = r };
        }

        private IEnumerator Load(Tile tile, int zoom)
        {
            _activeDownloads++;
            try
            {
                var n = 1 << zoom;
                var tx = ((tile.X % n) + n) % n;
                var path = Path.Combine(_cacheDir, zoom.ToString(), tx.ToString(), tile.Y + ".png");
                byte[] bytes = null;

                if (_settings.DiskCache && File.Exists(path))
                {
                    try { bytes = File.ReadAllBytes(path); } catch (Exception) { bytes = null; }
                }

                if (bytes == null)
                {
                    var url = BuildUrl(zoom, tx, tile.Y);
                    using (var req = UnityWebRequest.Get(url))
                    {
                        req.timeout = 20;
#if !UNITY_WEBGL
                        req.SetRequestHeader("User-Agent", $"{Application.productName}/{Application.version} (Unity)");
#endif
                        yield return req.SendWebRequest();
                        if (req.result != UnityWebRequest.Result.Success)
                        {
                            Debug.LogWarning($"[MapaLivre] Falha ao baixar tile {url}: {req.error}");
                            tile.Failed = true;
                            yield break;
                        }
                        bytes = req.downloadHandler.data;
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

                if (tile.Go == null) yield break; // tile removido enquanto baixava

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4,
                    name = tile.Go.name
                };
                if (!tex.LoadImage(bytes, true))
                {
                    Destroy(tex);
                    tile.Failed = true;
                    yield break;
                }

                tile.Texture = tex;
                _mpb.Clear();
                _mpb.SetTexture(BaseMap, tex);
                _mpb.SetTexture(MainTex, tex);
                _mpb.SetColor(BaseColor, _settings.Tint);
                _mpb.SetColor(ColorId, _settings.Tint);
                tile.Renderer.SetPropertyBlock(_mpb);
                tile.Renderer.enabled = true;
            }
            finally
            {
                tile.Loading = false;
                _activeDownloads--;
            }
        }

        private string BuildUrl(int z, int x, int y)
        {
            var url = _settings.TileUrl
                .Replace("{z}", z.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString());
            var subs = _settings.Subdomains;
            if (!string.IsNullOrEmpty(subs))
                url = url.Replace("{s}", subs[Math.Abs(x + y) % subs.Length].ToString());
            return url;
        }

        private void OnDestroy()
        {
            foreach (var t in _tiles.Values)
                if (t.Texture != null) Destroy(t.Texture);
            if (_quad != null) Destroy(_quad);
        }
    }
}
