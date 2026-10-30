using System;
using System.Collections.Generic;
using Niantic.Lightship.Maps.Coordinates;
using Niantic.Lightship.Maps.Core;
using Niantic.Lightship.Maps.Core.Coordinates;
using Niantic.Lightship.Maps.MapLayers.Components;
using UnityEngine;

namespace Niantic.Lightship.Maps
{
    /// <summary>
    /// Mapa centrado num LatLng. Uma unidade de cena = 1 metro (vezes UnitsPerMeter).
    /// Com "Center Map At Origin", o centro do mapa fica sempre na origem do mundo (onde está o jogador)
    /// e é o mapa que se move quando o centro muda.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LightshipMapView : MonoBehaviour
    {
        [SerializeField] private LightshipMapManager _lightshipMapManager;
        [SerializeField] private bool _centerMapAtOrigin = true;
        [SerializeField] private double _maximumMapOffset = 1000;
#pragma warning disable CS0414 // campo do SDK antigo, mantido para não perder dados da cena
        [SerializeField] private int _tileBatchCount = 32;
#pragma warning restore CS0414
        [Tooltip("Tema do SDK antigo (não é mais usado).")]
        [SerializeField] private UnityEngine.Object _mapTheme;
        [SerializeField] private double _viewableRadius = 500;
        [SerializeField] private bool _startAtDefaultLocation = true;
        [SerializeField] private SerializableLatLng _defaultLocation = new SerializableLatLng(-8.0476, -34.8770);
        [SerializeField] private List<MapLayer> _mapLayers = new List<MapLayer>();

        [Header("MapaLivre")]
        [Tooltip("Unidades de cena por metro real.")]
        [SerializeField] private float _unitsPerMeter = 1f;

        private LatLng _center;
        private double _centerMercX, _centerMercY, _metersPerMerc = 1;
        private bool _initialized;
        private Vector.VectorMapRenderer _vector;

        /// <summary>Disparado sempre que o centro do mapa muda.</summary>
        public event Action<LatLng> MapCenterChanged;

        public LatLng MapCenter { get { EnsureInitialized(); return _center; } }
        public double MapRadius => _viewableRadius;
        public float UnitsPerMeter => _unitsPerMeter;
        public bool IsMapCenteredAtOrigin => _centerMapAtOrigin;
        public LightshipMapManager MapManager => _lightshipMapManager;
        public IReadOnlyList<MapLayer> MapLayers => _mapLayers;

        /// <summary>Ponto do mundo onde fica o centro do mapa.</summary>
        public Vector3 SceneOrigin => _centerMapAtOrigin ? Vector3.zero : transform.position;

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            if (_lightshipMapManager == null) _lightshipMapManager = GetComponent<LightshipMapManager>();
            if (_lightshipMapManager == null) _lightshipMapManager = gameObject.AddComponent<LightshipMapManager>();

            SetCenterInternal(_defaultLocation);

            foreach (var layer in _mapLayers)
                if (layer != null) layer.Initialize(this);

            if (Application.isPlaying)
            {
                var go = new GameObject("MapTiles");
                go.transform.SetParent(transform, false);
                var theme = _lightshipMapManager.Theme;
                if (_lightshipMapManager.Style == MapaLivreStyle.Vetorial && theme != null)
                {
                    _vector = go.AddComponent<Vector.VectorMapRenderer>();
                    _vector.Initialize(this, _lightshipMapManager, theme);
                }
                else
                {
                    if (_lightshipMapManager.Style == MapaLivreStyle.Vetorial)
                        Debug.LogWarning("[MapaLivre] Nenhum tema encontrado; usando o mapa de imagens.");
                    go.AddComponent<MapTileRenderer>().Initialize(this, _lightshipMapManager);
                }
            }
        }

        public void SetMapCenter(LatLng center)
        {
            EnsureInitialized();
            SetCenterInternal(center);
            MapCenterChanged?.Invoke(_center);
        }

        /// <summary>Move o centro do mapa por um deslocamento em unidades de cena (X = leste, Z = norte).</summary>
        public void OffsetMapCenter(Vector3 sceneOffset) => SetMapCenter(SceneToLatLng(SceneOrigin + sceneOffset));

        public void SetMapRadius(double radiusMeters) => _viewableRadius = Math.Max(50, radiusMeters);

        private void SetCenterInternal(LatLng center)
        {
            _center = center;
            _centerMercX = WebMercator.ToMercX(center.Longitude);
            _centerMercY = WebMercator.ToMercY(center.Latitude);
            // Na projeção Mercator, 1 unidade "merc" vale cos(lat) metros reais.
            _metersPerMerc = Math.Cos(center.Latitude * Math.PI / 180.0);
        }

        public Vector3 LatLngToScene(LatLng location)
        {
            EnsureInitialized();
            return MercToScene(WebMercator.ToMercX(location.Longitude), WebMercator.ToMercY(location.Latitude));
        }

        public Vector3 LatLngToScene(SerializableLatLng location) => LatLngToScene((LatLng)location);

        /// <summary>
        /// O que existe num ponto do mapa: água, parque, prédio ou chão comum.
        /// Retorna Unknown enquanto os dados daquela área não carregaram.
        /// No estilo "Imagem" (sem dados vetoriais) sempre retorna Ground.
        /// </summary>
        public MapSurface GetSurface(LatLng location)
        {
            EnsureInitialized();
            if (_vector == null) return MapSurface.Ground;
            return _vector.GetSurface(WebMercator.ToMercX(location.Longitude), WebMercator.ToMercY(location.Latitude));
        }

        public LatLng SceneToLatLng(Vector3 scenePosition)
        {
            EnsureInitialized();
            var local = scenePosition - SceneOrigin;
            var k = _metersPerMerc * _unitsPerMeter;
            var mx = _centerMercX + local.x / k;
            var my = _centerMercY + local.z / k;
            return new LatLng(WebMercator.ToLat(my), WebMercator.ToLng(mx));
        }

        public Vector3 MercToScene(double mercX, double mercY)
        {
            EnsureInitialized();
            var k = _metersPerMerc * _unitsPerMeter;
            return SceneOrigin + new Vector3((float)((mercX - _centerMercX) * k), 0f, (float)((mercY - _centerMercY) * k));
        }

        public double CenterMercX { get { EnsureInitialized(); return _centerMercX; } }
        public double CenterMercY { get { EnsureInitialized(); return _centerMercY; } }
        public double MetersPerMerc { get { EnsureInitialized(); return _metersPerMerc; } }

        /// <summary>Distância em metros entre dois pontos (fórmula de haversine).</summary>
        public static double DistanceMeters(LatLng a, LatLng b)
        {
            const double r = 6371000.0;
            var dLat = (b.Latitude - a.Latitude) * Math.PI / 180.0;
            var dLng = (b.Longitude - a.Longitude) * Math.PI / 180.0;
            var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(a.Latitude * Math.PI / 180.0) * Math.Cos(b.Latitude * Math.PI / 180.0) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return 2 * r * Math.Asin(Math.Min(1, Math.Sqrt(h)));
        }
    }
}
