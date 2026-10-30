using System.Collections.Generic;
using Niantic.Lightship.Maps.Core.Coordinates;
using UnityEngine;

namespace Niantic.Lightship.Maps.MapLayers.Components
{
    /// <summary>Desenha linhas/contornos em coordenadas geográficas.</summary>
    public class LayerLineRenderer : MapLayerComponent
    {
        [SerializeField] private string _name;
        [SerializeField] private Material _material;
        [SerializeField] private float _lineWidth = 2f;
        [SerializeField] private float _heightOffset = 0.2f;

        private class Line
        {
            public LineRenderer Renderer;
            public LatLng[] Points;
        }

        private readonly Dictionary<string, Line> _lines = new Dictionary<string, Line>();

        public string Name => _name;

        public void DrawLoop(LatLng[] points, string id = null) => Draw(points, id, true);
        public void DrawLine(LatLng[] points, string id = null) => Draw(points, id, false);

        private void Draw(LatLng[] points, string id, bool loop)
        {
            if (points == null || points.Length < 2 || !EnsureMapView()) return;
            id ??= $"line_{_lines.Count}";
            if (!_lines.TryGetValue(id, out var line))
            {
                var go = new GameObject($"{_name}_{id}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.sharedMaterial = _material;
                lr.widthMultiplier = _lineWidth;
                line = new Line { Renderer = lr };
                _lines[id] = line;
            }

            line.Points = points;
            line.Renderer.loop = loop;
            Apply(line);
        }

        public void Clear(string id)
        {
            if (!_lines.TryGetValue(id, out var line)) return;
            if (line.Renderer != null) Destroy(line.Renderer.gameObject);
            _lines.Remove(id);
        }

        public void ClearAll()
        {
            foreach (var l in _lines.Values)
                if (l.Renderer != null) Destroy(l.Renderer.gameObject);
            _lines.Clear();
        }

        private void Apply(Line line)
        {
            line.Renderer.positionCount = line.Points.Length;
            for (var i = 0; i < line.Points.Length; i++)
            {
                var p = MapView.LatLngToScene(line.Points[i]);
                p.y += _heightOffset;
                line.Renderer.SetPosition(i, p);
            }
        }

        protected override void Refresh()
        {
            foreach (var l in _lines.Values)
                if (l.Renderer != null) Apply(l);
        }
    }
}
