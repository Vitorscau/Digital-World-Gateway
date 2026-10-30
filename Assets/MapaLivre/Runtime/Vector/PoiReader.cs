using System.Collections.Generic;
using Niantic.Lightship.Maps.Core;
using Niantic.Lightship.Maps.Core.Coordinates;

namespace Niantic.Lightship.Maps.Vector
{
    /// <summary>Lê os lugares com nome (camada "poi") de um vector tile. Roda fora da thread principal.</summary>
    internal static class PoiReader
    {
        private static readonly string[] Wanted = { "poi" };

        public static List<MapPoi> Read(byte[] pbf, double west, double north, double size)
        {
            var list = new List<MapPoi>();
            var layers = MvtDecoder.Decode(pbf, Wanted);
            if (!layers.TryGetValue("poi", out var layer)) return list;

            foreach (var f in layer.Features)
            {
                if (f.Type != 1) continue;
                var name = f.GetString("name");
                if (string.IsNullOrWhiteSpace(name)) name = f.GetString("name:latin");
                if (string.IsNullOrWhiteSpace(name)) continue;

                foreach (var part in MvtDecoder.DecodeGeometry(f.Geometry))
                foreach (var (x, y) in part)
                {
                    // Pontos na borda extra do tile pertencem ao vizinho (evita duplicar).
                    if (x < 0 || y < 0 || x >= layer.Extent || y >= layer.Extent) continue;
                    var mx = west + x / (double)layer.Extent * size;
                    var my = north - y / (double)layer.Extent * size;
                    list.Add(new MapPoi
                    {
                        Location = new LatLng(WebMercator.ToLat(my), WebMercator.ToLng(mx)),
                        Name = name.Trim(),
                        Class = f.GetString("class"),
                        Subclass = f.GetString("subclass"),
                        Rank = (int)f.GetNumber("rank", 99)
                    });
                }
            }
            return list;
        }
    }
}
