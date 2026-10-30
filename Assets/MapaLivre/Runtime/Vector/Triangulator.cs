using System;
using System.Collections.Generic;

namespace Niantic.Lightship.Maps.Vector
{
    /// <summary>
    /// Triangulação de polígonos simples por "ear clipping", com furos ligados ao contorno por pontes.
    /// Não depende da Unity, então pode rodar fora da thread principal.
    /// </summary>
    internal static class Triangulator
    {
        public struct P
        {
            public double X, Y;
            public P(double x, double y) { X = x; Y = y; }
        }

        /// <summary>Retorna índices (em trios) para a lista de pontos <paramref name="outPoints"/>.</summary>
        public static void Triangulate(List<P> outer, List<List<P>> holes, List<P> outPoints, List<int> outIndices)
        {
            if (outer == null || outer.Count < 3) return;
            var ring = Clean(outer);
            if (ring.Count < 3) return;
            if (SignedArea(ring) < 0) ring.Reverse(); // contorno em sentido anti-horário

            if (holes != null && holes.Count > 0)
            {
                var hs = new List<List<P>>();
                foreach (var h in holes)
                {
                    var c = Clean(h);
                    if (c.Count < 3) continue;
                    if (SignedArea(c) > 0) c.Reverse(); // furos em sentido horário
                    hs.Add(c);
                }
                // Liga primeiro os furos mais à direita.
                hs.Sort((a, b) => MaxX(b).CompareTo(MaxX(a)));
                foreach (var h in hs) ring = Bridge(ring, h);
            }

            EarClip(ring, outPoints, outIndices);
        }

        private static List<P> Clean(List<P> pts)
        {
            var r = new List<P>(pts.Count);
            foreach (var p in pts)
                if (r.Count == 0 || Math.Abs(r[r.Count - 1].X - p.X) > 1e-9 || Math.Abs(r[r.Count - 1].Y - p.Y) > 1e-9)
                    r.Add(p);
            if (r.Count > 1 && Math.Abs(r[0].X - r[r.Count - 1].X) < 1e-9 && Math.Abs(r[0].Y - r[r.Count - 1].Y) < 1e-9)
                r.RemoveAt(r.Count - 1);
            return r;
        }

        public static double SignedArea(List<P> r)
        {
            double a = 0;
            for (int i = 0, j = r.Count - 1; i < r.Count; j = i++)
                a += (r[j].X - r[i].X) * (r[j].Y + r[i].Y);
            return a * 0.5;
        }

        private static double MaxX(List<P> r)
        {
            var m = double.MinValue;
            foreach (var p in r) if (p.X > m) m = p.X;
            return m;
        }

        private static List<P> Bridge(List<P> outer, List<P> hole)
        {
            // Vértice mais à direita do furo.
            var hi = 0;
            for (var i = 1; i < hole.Count; i++) if (hole[i].X > hole[hi].X) hi = i;
            var h = hole[hi];

            // Vértice do contorno mais próximo cuja ligação não cruza nenhuma aresta.
            var order = new List<int>(outer.Count);
            for (var i = 0; i < outer.Count; i++) order.Add(i);
            order.Sort((a, b) => Dist2(outer[a], h).CompareTo(Dist2(outer[b], h)));

            var best = -1;
            var tries = Math.Min(order.Count, 64);
            for (var k = 0; k < tries && best < 0; k++)
            {
                var o = outer[order[k]];
                if (!Crosses(outer, h, o) && !Crosses(hole, h, o)) best = order[k];
            }
            if (best < 0) best = order[0];

            var result = new List<P>(outer.Count + hole.Count + 2);
            for (var i = 0; i <= best; i++) result.Add(outer[i]);
            for (var i = 0; i <= hole.Count; i++) result.Add(hole[(hi + i) % hole.Count]);
            for (var i = best; i < outer.Count; i++) result.Add(outer[i]);
            return result;
        }

        private static double Dist2(P a, P b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

        private static bool Crosses(List<P> ring, P a, P b)
        {
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                var c = ring[j];
                var d = ring[i];
                if (Same(c, a) || Same(c, b) || Same(d, a) || Same(d, b)) continue;
                if (SegmentsIntersect(a, b, c, d)) return true;
            }
            return false;
        }

        private static bool Same(P a, P b) => Math.Abs(a.X - b.X) < 1e-9 && Math.Abs(a.Y - b.Y) < 1e-9;

        private static double Cross(P o, P a, P b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

        private static bool SegmentsIntersect(P a, P b, P c, P d)
        {
            var d1 = Cross(c, d, a);
            var d2 = Cross(c, d, b);
            var d3 = Cross(a, b, c);
            var d4 = Cross(a, b, d);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static void EarClip(List<P> ring, List<P> outPoints, List<int> outIndices)
        {
            var n = ring.Count;
            var baseIndex = outPoints.Count;
            outPoints.AddRange(ring);

            var prev = new int[n];
            var next = new int[n];
            for (var i = 0; i < n; i++)
            {
                prev[i] = (i + n - 1) % n;
                next[i] = (i + 1) % n;
            }

            var remaining = n;
            var cur = 0;
            var stall = 0;
            while (remaining > 3 && stall < remaining)
            {
                var a = prev[cur];
                var b = cur;
                var c = next[cur];
                // Ponto alinhado com os vizinhos (comum na borda do tile): só remove, sem triângulo.
                var turn = Cross(ring[a], ring[b], ring[c]);
                var scale = Math.Max(Dist2(ring[a], ring[b]), Dist2(ring[b], ring[c]));
                if (Math.Abs(turn) <= 1e-10 * Math.Max(scale, 1e-12))
                {
                    next[a] = c;
                    prev[c] = a;
                    remaining--;
                    cur = c;
                    stall = 0;
                    continue;
                }
                if (IsEar(ring, prev, next, a, b, c, remaining))
                {
                    outIndices.Add(baseIndex + a);
                    outIndices.Add(baseIndex + b);
                    outIndices.Add(baseIndex + c);
                    next[a] = c;
                    prev[c] = a;
                    remaining--;
                    cur = c;
                    stall = 0;
                }
                else
                {
                    cur = next[cur];
                    stall++;
                }
            }

            if (remaining == 3)
            {
                outIndices.Add(baseIndex + prev[cur]);
                outIndices.Add(baseIndex + cur);
                outIndices.Add(baseIndex + next[cur]);
            }
            // Se sobrar um pedaço que não deu para triangular (polígono defeituoso), ele é descartado:
            // melhor um buraquinho do que um triângulo enorme cobrindo o mapa.
        }

        private static bool IsEar(List<P> r, int[] prev, int[] next, int a, int b, int c, int remaining)
        {
            var pa = r[a];
            var pb = r[b];
            var pc = r[c];
            var area = Cross(pa, pb, pc);
            if (area <= 0) return false; // vértice côncavo (ou colinear)

            // Nenhum outro vértice pode estar dentro do triângulo.
            var v = next[c];
            var guard = 0;
            while (v != a && guard++ < remaining)
            {
                var p = r[v];
                if (!Same(p, pa) && !Same(p, pb) && !Same(p, pc) &&
                    Cross(pa, pb, p) >= 0 && Cross(pb, pc, p) >= 0 && Cross(pc, pa, p) >= 0)
                    return false;
                v = next[v];
            }
            return true;
        }
    }
}
