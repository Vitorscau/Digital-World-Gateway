using System.Collections.Generic;
using Niantic.Lightship.Maps.Core.Coordinates;
using UnityEngine;

namespace Niantic.Lightship.Maps.MapLayers.Components
{
    /// <summary>Coloca instâncias de um prefab em coordenadas geográficas.</summary>
    public class LayerGameObjectPlacement : MapLayerComponent
    {
        [SerializeField] private string _name;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private float _heightOffset;
        [SerializeField] private bool _scaleWithMapRadius;
        [SerializeField] private float _relativeScale = 1f;
        [SerializeField] private float _minimumScale;
        [SerializeField] private float _maximumScale = float.PositiveInfinity;

        private class Placed
        {
            public GameObject Go;
            public LatLng Location;
        }

        private readonly List<Placed> _instances = new List<Placed>();

        public string Name => _name;
        public GameObject Prefab => _prefab;

        public IEnumerable<GameObject> PlacedInstances
        {
            get
            {
                foreach (var p in _instances)
                    if (p.Go != null) yield return p.Go;
            }
        }

        public GameObject PlaceInstance(LatLng location, string instanceName = null) =>
            PlaceInstance(location, Quaternion.identity, instanceName);

        public GameObject PlaceInstance(LatLng location, Quaternion rotation, string instanceName = null) =>
            PlaceInstance(_prefab, location, rotation, instanceName);

        /// <summary>Coloca um prefab qualquer usando as regras desta camada (escala, altura, posição).</summary>
        public GameObject PlaceInstance(GameObject prefab, LatLng location, Quaternion rotation, string instanceName = null)
        {
            if (prefab == null || !EnsureMapView()) return null;

            var go = Instantiate(prefab, transform);
            go.name = string.IsNullOrEmpty(instanceName) ? $"{_name}_{_instances.Count}" : instanceName;
            go.transform.rotation = rotation * prefab.transform.rotation; // mantém a rotação própria do prefab
            go.transform.localScale = prefab.transform.localScale * CurrentScale();
            var placed = new Placed { Go = go, Location = location };
            _instances.Add(placed);
            Position(placed);
            return go;
        }

        public void RemoveInstance(GameObject instance)
        {
            for (var i = _instances.Count - 1; i >= 0; i--)
            {
                if (_instances[i].Go != instance) continue;
                if (instance != null) Destroy(instance);
                _instances.RemoveAt(i);
            }
        }

        public void ClearAllInstances()
        {
            foreach (var p in _instances)
                if (p.Go != null) Destroy(p.Go);
            _instances.Clear();
        }

        private float CurrentScale()
        {
            var s = _relativeScale;
            if (_scaleWithMapRadius && MapView != null) s *= (float)(MapView.MapRadius / 500.0);
            return Mathf.Clamp(s, _minimumScale, _maximumScale);
        }

        private void Position(Placed p)
        {
            var pos = MapView.LatLngToScene(p.Location);
            pos.y += _heightOffset;
            p.Go.transform.position = pos;
        }

        protected override void Refresh()
        {
            for (var i = _instances.Count - 1; i >= 0; i--)
            {
                if (_instances[i].Go == null) { _instances.RemoveAt(i); continue; }
                Position(_instances[i]);
            }
        }
    }
}
