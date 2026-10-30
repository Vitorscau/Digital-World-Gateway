using System.Collections.Generic;
using Niantic.Lightship.Maps.Core.Coordinates;
using UnityEngine;

namespace Niantic.Lightship.Maps.MapLayers.Components
{
    /// <summary>Agrupa componentes que colocam coisas no mapa (Digimons, linhas...).</summary>
    public class MapLayer : MonoBehaviour
    {
        [SerializeField] private string _layerName;

        public string Name => _layerName;
        public LightshipMapView LightshipMapView { get; private set; }

        internal void Initialize(LightshipMapView view)
        {
            LightshipMapView = view;
            foreach (var c in GetComponentsInChildren<MapLayerComponent>(true))
                c.Initialize(view, this);
        }
    }

    public abstract class MapLayerComponent : MonoBehaviour
    {
        protected LightshipMapView MapView { get; private set; }
        protected MapLayer Layer { get; private set; }

        internal void Initialize(LightshipMapView view, MapLayer layer)
        {
            if (MapView != null) return;
            MapView = view;
            Layer = layer;
            MapView.MapCenterChanged += _ => Refresh();
            OnInitialized();
        }

        /// <summary>Acha o mapa sozinho se a camada não estiver na lista "Map Layers" do LightshipMapView.</summary>
        protected bool EnsureMapView()
        {
            if (MapView != null) return true;
            var layer = GetComponentInParent<MapLayer>();
            var view = layer != null && layer.LightshipMapView != null
                ? layer.LightshipMapView
                : FindFirstObjectByType<LightshipMapView>();
            if (view == null) return false;
            Initialize(view, layer);
            return true;
        }

        protected virtual void OnInitialized() { }
        protected abstract void Refresh();
    }
}
