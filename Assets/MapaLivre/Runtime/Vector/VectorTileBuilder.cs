using System;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.Lightship.Maps.Vector
{
    internal enum MapCategory
    {
        Ground, Special, Sand, Forest, Park, Water, Path, RoadCasing, MinorRoad, MajorRoad, BuildingTop, BuildingWall
    }

    internal sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<int> Triangles = new List<int>();
    }

    /// <summary>Parâmetros copiados do tema na thread principal (materiais não podem ser lidos fora dela).</summary>
    internal sealed class BuildSettings
    {
        public float MajorRoadWidth, MinorRoadWidth, PathWidth, WaterwayWidth;
        public float RoadOutlineWidth; // 0 = sem borda
        public bool ShowBuildings;
        public float BuildingHeightScale, BuildingMinHeight, BuildingMaxHeight;
        public float TextureRepeatMeters = 8f;
        public readonly Dictionary<MapCategory, Vector2> Tiling = new Dictionary<MapCategory, Vector2>();
    }

    /// <summary>Transforma um vector tile em malhas, separadas por categoria. Roda fora da thread principal.</summary>
    internal static class VectorTileBuilder
    {
        private static readonly string[] Layers =
            { "water", "waterway", "landcover", "landuse", "park", "transportation", "building" };

        // Altura (metros) de cada camada, para não brigarem na tela.
        public static float LayerHeight(MapCategory c)
        {
            switch (c)
            {
                case MapCategory.Ground: return -0.9f;
                case MapCategory.Special: return -0.8f;
                case MapCategory.Sand: return -0.7f;
                case MapCategory.Forest: return -0.6f;
                case MapCategory.Park: return -0.5f;
                case MapCategory.Water: return -0.35f;
                case MapCategory.Path: return -0.3f;
                case MapCategory.RoadCasing: return -0.22f;
                case MapCategory.MinorRoad: return -0.15f;
                case MapCategory.MajorRoad: return -0.05f;
                default: return 0f;
            }
        }

        private sealed class Ctx
        {
            public BuildSettings S;
            public double Size;      // tamanho do tile em unidades Mercator
            public double K;         // metros reais por unidade Mercator
            public double OffU, OffV; // deslocamento da textura (em repetições) para emendar tiles vizinhos
            public float Extent;
            public Dictionary<MapCategory, MeshData> Out;
        }

        public static Dictionary<MapCategory, MeshData> Build(byte[] pbf, double west, double north, double size, double k, BuildSettings s)
        {
            var ctx = new Ctx
            {
                S = s,
                Size = size,
                K = k,
                Out = new Dictionary<MapCategory, MeshData>()
            };
            var rep = Math.Max(0.5, s.TextureRepeatMeters);
            ctx.OffU = Frac(west * k / rep);
            ctx.OffV = Frac(north * k / rep);

            // Chão: um quadrado cobrindo o tile inteiro.
            var ground = new List<Triangulator.P>
            {
                new Triangulator.P(0, 0), new Triangulator.P(size, 0),
                new Triangulator.P(size, -size), new Triangulator.P(0, -size)
            };
            AddPolygon(ctx, MapCategory.Ground, ground, null);

            var layers = MvtDecoder.Decode(pbf, Layers);
            foreach (var name in Layers)
            {
                if (!layers.TryGetValue(name, out var layer)) continue;
                ctx.Extent = layer.Extent;
                foreach (var f in layer.Features) Classify(ctx, name, f);
            }
            return ctx.Out;
        }

        private static double Frac(double v) => v - Math.Floor(v);

        private static void Classify(Ctx ctx, string layer, MvtFeature f)
        {
            var cls = f.GetString("class");
            if (f.GetString("brunnel") == "tunnel") return;

            switch (layer)
            {
                case "water":
                    if (f.Type == 3) AddPolygons(ctx, MapCategory.Water, f);
                    break;
                case "waterway":
                    if (f.Type == 2) AddLines(ctx, MapCategory.Water, f, ctx.S.WaterwayWidth * (cls == "river" ? 2f : 1f));
                    break;
                case "landcover":
                    if (f.Type != 3) break;
                    switch (cls)
                    {
                        case "wood": AddPolygons(ctx, MapCategory.Forest, f); break;
                        case "grass":
                        case "farmland":
                        case "wetland": AddPolygons(ctx, MapCategory.Park, f); break;
                        case "sand": AddPolygons(ctx, MapCategory.Sand, f); break;
                        case "ice":
                        case "rock": AddPolygons(ctx, MapCategory.Special, f); break;
                    }
                    break;
                case "park":
                    if (f.Type == 3) AddPolygons(ctx, MapCategory.Park, f);
                    break;
                case "landuse":
                    if (f.Type != 3) break;
                    switch (cls)
                    {
                        case "pitch":
                        case "playground":
                        case "cemetery":
                        case "zoo":
                        case "theme_park":
                        case "stadium":
                            AddPolygons(ctx, MapCategory.Park, f);
                            break;
                        case "school":
                        case "university":
                        case "college":
                        case "kindergarten":
                        case "hospital":
                        case "library":
                            AddPolygons(ctx, MapCategory.Special, f);
                            break;
                    }
                    break;
                case "transportation":
                    if (f.Type != 2) break;
                    switch (cls)
                    {
                        case "motorway":
                        case "trunk":
                        case "primary":
                        case "secondary":
                            AddRoad(ctx, MapCategory.MajorRoad, f, ctx.S.MajorRoadWidth);
                            break;
                        case "tertiary":
                        case "minor":
                        case "busway":
                        case "raceway":
                            AddRoad(ctx, MapCategory.MinorRoad, f, ctx.S.MinorRoadWidth);
                            break;
                        case "service":
                            AddRoad(ctx, MapCategory.MinorRoad, f, ctx.S.MinorRoadWidth * 0.6f);
                            break;
                        case "track":
                        case "path":
                            AddLines(ctx, MapCategory.Path, f, ctx.S.PathWidth);
                            break;
                    }
                    break;
                case "building":
                    if (f.Type == 3 && ctx.S.ShowBuildings) AddBuildings(ctx, f);
                    break;
            }
        }

        // ---------- Geometria ----------

        private static Triangulator.P ToP(Ctx ctx, (float x, float y) p) =>
            new Triangulator.P(p.x / ctx.Extent * ctx.Size, -p.y / ctx.Extent * ctx.Size);

        /// <summary>Agrupa anéis do MVT em polígonos (contorno + furos).</summary>
        private static List<(List<Triangulator.P> outer, List<List<Triangulator.P>> holes)> Polygons(Ctx ctx, MvtFeature f)
        {
            var result = new List<(List<Triangulator.P>, List<List<Triangulator.P>>)>();
            foreach (var ring in MvtDecoder.DecodeGeometry(f.Geometry))
            {
                if (ring.Count < 3) continue;
                var pts = new List<Triangulator.P>(ring.Count);
                foreach (var p in ring) pts.Add(ToP(ctx, p));
                // No MVT o contorno externo tem área positiva com Y para baixo; com o Y invertido fica negativa.
                if (Triangulator.SignedArea(pts) < 0 || result.Count == 0)
                    result.Add((pts, new List<List<Triangulator.P>>()));
                else
                    result[result.Count - 1].Item2.Add(pts);
            }
            return result;
        }

        private static void AddPolygons(Ctx ctx, MapCategory cat, MvtFeature f)
        {
            foreach (var (outer, holes) in Polygons(ctx, f)) AddPolygon(ctx, cat, outer, holes);
        }

        private static MeshData Data(Ctx ctx, MapCategory cat)
        {
            if (!ctx.Out.TryGetValue(cat, out var d)) ctx.Out[cat] = d = new MeshData();
            return d;
        }

        private static Vector2 Uv(Ctx ctx, MapCategory cat, double x, double y)
        {
            var rep = Math.Max(0.5, ctx.S.TextureRepeatMeters);
            var tiling = ctx.S.Tiling.TryGetValue(cat, out var t) ? t : Vector2.one;
            if (tiling.x == 0) tiling.x = 1;
            if (tiling.y == 0) tiling.y = 1;
            var u = x * ctx.K / rep + ctx.OffU;
            var v = y * ctx.K / rep + ctx.OffV;
            return new Vector2((float)(u / tiling.x), (float)(v / tiling.y));
        }

        private static void AddPolygon(Ctx ctx, MapCategory cat, List<Triangulator.P> outer, List<List<Triangulator.P>> holes, float height = float.NaN)
        {
            var pts = new List<Triangulator.P>();
            var idx = new List<int>();
            Triangulator.Triangulate(outer, holes, pts, idx);
            if (idx.Count == 0) return;

            var d = Data(ctx, cat);
            var y = float.IsNaN(height) ? LayerHeight(cat) : height;
            var start = d.Vertices.Count;
            foreach (var p in pts)
            {
                d.Vertices.Add(new Vector3((float)p.X, y, (float)p.Y));
                d.Uvs.Add(Uv(ctx, cat, p.X, p.Y));
            }
            for (var i = 0; i < idx.Count; i += 3)
                AddTriUp(d, start + idx[i], start + idx[i + 1], start + idx[i + 2]);
        }

        /// <summary>Adiciona um triângulo virado para cima (visível de cima).</summary>
        private static void AddTriUp(MeshData d, int a, int b, int c)
        {
            var va = d.Vertices[a];
            var vb = d.Vertices[b];
            var vc = d.Vertices[c];
            var crossY = (vb.z - va.z) * (vc.x - va.x) - (vb.x - va.x) * (vc.z - va.z);
            d.Triangles.Add(a);
            if (crossY >= 0) { d.Triangles.Add(b); d.Triangles.Add(c); }
            else { d.Triangles.Add(c); d.Triangles.Add(b); }
        }

        private static void AddLines(Ctx ctx, MapCategory cat, MvtFeature f, float widthMeters)
        {
            var halfMerc = widthMeters * 0.5 / ctx.K;
            var d = Data(ctx, cat);
            var y = LayerHeight(cat);
            foreach (var line in MvtDecoder.DecodeGeometry(f.Geometry))
            {
                if (line.Count < 2) continue;
                var pts = new List<Triangulator.P>(line.Count);
                foreach (var p in line) pts.Add(ToP(ctx, p));

                for (var i = 0; i < pts.Count - 1; i++)
                {
                    var a = pts[i];
                    var b = pts[i + 1];
                    var dx = b.X - a.X;
                    var dy = b.Y - a.Y;
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    if (len < 1e-9) continue;
                    var nx = -dy / len * halfMerc;
                    var ny = dx / len * halfMerc;
                    var s = d.Vertices.Count;
                    AddVert(ctx, d, cat, a.X + nx, a.Y + ny, y);
                    AddVert(ctx, d, cat, a.X - nx, a.Y - ny, y);
                    AddVert(ctx, d, cat, b.X - nx, b.Y - ny, y);
                    AddVert(ctx, d, cat, b.X + nx, b.Y + ny, y);
                    AddTriUp(d, s, s + 1, s + 2);
                    AddTriUp(d, s, s + 2, s + 3);
                }

                // Juntas e pontas arredondadas.
                foreach (var p in pts) AddDisc(ctx, d, cat, p, halfMerc, y);
            }
        }

        /// <summary>Rua com borda: uma faixa mais larga por baixo, na cor da borda.</summary>
        private static void AddRoad(Ctx ctx, MapCategory cat, MvtFeature f, float width)
        {
            if (ctx.S.RoadOutlineWidth > 0) AddLines(ctx, MapCategory.RoadCasing, f, width + 2 * ctx.S.RoadOutlineWidth);
            AddLines(ctx, cat, f, width);
        }

        private static void AddDisc(Ctx ctx, MeshData d, MapCategory cat, Triangulator.P c, double r, float y)
        {
            const int seg = 8;
            var center = d.Vertices.Count;
            AddVert(ctx, d, cat, c.X, c.Y, y);
            for (var i = 0; i < seg; i++)
            {
                var ang = i * Math.PI * 2 / seg;
                AddVert(ctx, d, cat, c.X + Math.Cos(ang) * r, c.Y + Math.Sin(ang) * r, y);
            }
            for (var i = 0; i < seg; i++)
                AddTriUp(d, center, center + 1 + i, center + 1 + (i + 1) % seg);
        }

        private static void AddVert(Ctx ctx, MeshData d, MapCategory cat, double x, double z, float y)
        {
            d.Vertices.Add(new Vector3((float)x, y, (float)z));
            d.Uvs.Add(Uv(ctx, cat, x, z));
        }

        private static void AddBuildings(Ctx ctx, MvtFeature f)
        {
            var real = f.GetNumber("render_height", 8);
            var h = (float)Math.Max(ctx.S.BuildingMinHeight,
                Math.Min(ctx.S.BuildingMaxHeight, real * ctx.S.BuildingHeightScale));

            foreach (var (outer, holes) in Polygons(ctx, f))
            {
                AddPolygon(ctx, MapCategory.BuildingTop, outer, holes, h);

                // Paredes (dos dois lados, para não depender da ordem dos vértices).
                var d = Data(ctx, MapCategory.BuildingWall);
                for (int i = 0, j = outer.Count - 1; i < outer.Count; j = i++)
                {
                    var a = outer[j];
                    var b = outer[i];
                    var segLen = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)) * ctx.K;
                    var s = d.Vertices.Count;
                    d.Vertices.Add(new Vector3((float)a.X, 0, (float)a.Y));
                    d.Vertices.Add(new Vector3((float)b.X, 0, (float)b.Y));
                    d.Vertices.Add(new Vector3((float)b.X, h, (float)b.Y));
                    d.Vertices.Add(new Vector3((float)a.X, h, (float)a.Y));
                    var rep = Math.Max(0.5, ctx.S.TextureRepeatMeters);
                    var u1 = (float)(segLen / rep);
                    var v1 = (float)(h / rep);
                    d.Uvs.Add(new Vector2(0, 0));
                    d.Uvs.Add(new Vector2(u1, 0));
                    d.Uvs.Add(new Vector2(u1, v1));
                    d.Uvs.Add(new Vector2(0, v1));
                    d.Triangles.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3, s, s + 2, s + 1, s, s + 3, s + 2 });
                }
            }
        }
    }
}
