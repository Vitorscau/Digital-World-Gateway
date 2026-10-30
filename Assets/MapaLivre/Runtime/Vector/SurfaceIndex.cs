using System.Collections.Generic;
using UnityEngine;

namespace Niantic.Lightship.Maps.Vector
{
    /// <summary>
    /// Guarda os triângulos de água, parque e prédios de um tile para responder
    /// "o que existe neste ponto?". Coordenadas em unidades Mercator relativas ao canto noroeste do tile.
    /// </summary>
    internal sealed class SurfaceIndex
    {
        private sealed class Layer
        {
            public Vector2[] Points;
            public int[] Triangles;
            public Rect Bounds;
        }

        private readonly List<(MapSurface surface, Layer layer)> _layers = new List<(MapSurface, Layer)>();

        public static SurfaceIndex From(Dictionary<MapCategory, MeshData> meshes)
        {
            var index = new SurfaceIndex();
            // Ordem = prioridade: água ganha de prédio, que ganha de parque.
            index.Add(meshes, MapCategory.Water, MapSurface.Water);
            index.Add(meshes, MapCategory.BuildingTop, MapSurface.Building);
            index.Add(meshes, MapCategory.Park, MapSurface.Park);
            index.Add(meshes, MapCategory.Forest, MapSurface.Park);
            index.Add(meshes, MapCategory.Sand, MapSurface.Park);
            return index;
        }

        private void Add(Dictionary<MapCategory, MeshData> meshes, MapCategory cat, MapSurface surface)
        {
            if (!meshes.TryGetValue(cat, out var data) || data.Triangles.Count == 0) return;
            var pts = new Vector2[data.Vertices.Count];
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (var i = 0; i < pts.Length; i++)
            {
                var v = data.Vertices[i];
                pts[i] = new Vector2(v.x, v.z);
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.z < minY) minY = v.z;
                if (v.z > maxY) maxY = v.z;
            }
            _layers.Add((surface, new Layer
            {
                Points = pts,
                Triangles = data.Triangles.ToArray(),
                Bounds = Rect.MinMaxRect(minX, minY, maxX, maxY)
            }));
        }

        public MapSurface Query(float x, float z)
        {
            var p = new Vector2(x, z);
            foreach (var (surface, layer) in _layers)
            {
                if (!layer.Bounds.Contains(p)) continue;
                var t = layer.Triangles;
                var v = layer.Points;
                for (var i = 0; i + 2 < t.Length; i += 3)
                    if (Inside(p, v[t[i]], v[t[i + 1]], v[t[i + 2]])) return surface;
            }
            return MapSurface.Ground;
        }

        private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            var d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            var d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            var neg = d1 < 0 || d2 < 0 || d3 < 0;
            var pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
    }
}
