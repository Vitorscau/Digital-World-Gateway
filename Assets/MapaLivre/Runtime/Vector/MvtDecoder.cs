using System;
using System.Collections.Generic;
using System.Text;

namespace Niantic.Lightship.Maps.Vector
{
    /// <summary>Uma feição (rua, prédio, parque...) de um vector tile no formato Mapbox Vector Tile.</summary>
    internal sealed class MvtFeature
    {
        public int Type; // 1 = ponto, 2 = linha, 3 = polígono
        public uint[] Geometry;
        public Dictionary<string, object> Properties;

        public string GetString(string key) =>
            Properties != null && Properties.TryGetValue(key, out var v) ? v as string : null;

        public double GetNumber(string key, double fallback)
        {
            if (Properties == null || !Properties.TryGetValue(key, out var v) || v == null) return fallback;
            switch (v)
            {
                case double d: return d;
                case float f: return f;
                case long l: return l;
                case ulong u: return u;
                default: return fallback;
            }
        }
    }

    internal sealed class MvtLayer
    {
        public string Name;
        public int Extent = 4096;
        public readonly List<MvtFeature> Features = new List<MvtFeature>();
    }

    /// <summary>Leitor mínimo de protobuf para o formato MVT (spec 2.1).</summary>
    internal static class MvtDecoder
    {
        public static Dictionary<string, MvtLayer> Decode(byte[] data, ICollection<string> wantedLayers)
        {
            var result = new Dictionary<string, MvtLayer>();
            var r = new Reader(data, 0, data.Length);
            while (r.Next(out var field, out var wire))
            {
                if (field == 3 && wire == 2)
                {
                    r.ReadLengthDelimited(out var start, out var len);
                    var layer = DecodeLayer(data, start, len, wantedLayers);
                    if (layer != null) result[layer.Name] = layer;
                }
                else r.Skip(wire);
            }
            return result;
        }

        private static MvtLayer DecodeLayer(byte[] data, int start, int len, ICollection<string> wanted)
        {
            // Primeiro passe: nome, chaves, valores e extent. Features são lidas no segundo passe.
            var r = new Reader(data, start, start + len);
            string name = null;
            var keys = new List<string>();
            var values = new List<object>();
            var extent = 4096;
            var featureSpans = new List<(int, int)>();

            while (r.Next(out var field, out var wire))
            {
                switch (field)
                {
                    case 1 when wire == 2:
                        name = r.ReadString();
                        if (wanted != null && !wanted.Contains(name)) return null;
                        break;
                    case 2 when wire == 2:
                        r.ReadLengthDelimited(out var fs, out var fl);
                        featureSpans.Add((fs, fl));
                        break;
                    case 3 when wire == 2:
                        keys.Add(r.ReadString());
                        break;
                    case 4 when wire == 2:
                        r.ReadLengthDelimited(out var vs, out var vl);
                        values.Add(DecodeValue(data, vs, vl));
                        break;
                    case 5 when wire == 0:
                        extent = (int)r.ReadVarint();
                        break;
                    default:
                        r.Skip(wire);
                        break;
                }
            }

            if (name == null) return null;
            var layer = new MvtLayer { Name = name, Extent = extent };
            foreach (var (fs, fl) in featureSpans)
                layer.Features.Add(DecodeFeature(data, fs, fl, keys, values));
            return layer;
        }

        private static MvtFeature DecodeFeature(byte[] data, int start, int len, List<string> keys, List<object> values)
        {
            var r = new Reader(data, start, start + len);
            var f = new MvtFeature();
            while (r.Next(out var field, out var wire))
            {
                switch (field)
                {
                    case 2 when wire == 2:
                        var tags = r.ReadPackedUInt32();
                        f.Properties = new Dictionary<string, object>(tags.Length / 2);
                        for (var i = 0; i + 1 < tags.Length; i += 2)
                        {
                            var k = (int)tags[i];
                            var v = (int)tags[i + 1];
                            if (k < keys.Count && v < values.Count) f.Properties[keys[k]] = values[v];
                        }
                        break;
                    case 3 when wire == 0:
                        f.Type = (int)r.ReadVarint();
                        break;
                    case 4 when wire == 2:
                        f.Geometry = r.ReadPackedUInt32();
                        break;
                    default:
                        r.Skip(wire);
                        break;
                }
            }
            return f;
        }

        private static object DecodeValue(byte[] data, int start, int len)
        {
            var r = new Reader(data, start, start + len);
            object value = null;
            while (r.Next(out var field, out var wire))
            {
                switch (field)
                {
                    case 1: value = r.ReadString(); break;
                    case 2: value = (double)BitConverter.ToSingle(data, r.Advance(4)); break;
                    case 3: value = BitConverter.ToDouble(data, r.Advance(8)); break;
                    case 4: value = (long)r.ReadVarint(); break;
                    case 5: value = r.ReadVarint(); break;
                    case 6:
                        var z = r.ReadVarint();
                        value = (long)(z >> 1) ^ -(long)(z & 1);
                        break;
                    case 7: value = r.ReadVarint() != 0; break;
                    default: r.Skip(wire); break;
                }
            }
            return value;
        }

        /// <summary>
        /// Converte os comandos de geometria em listas de pontos (coordenadas do tile, Y para baixo).
        /// Para linhas: uma lista por trecho. Para polígonos: um anel por lista.
        /// </summary>
        public static List<List<(float x, float y)>> DecodeGeometry(uint[] geom)
        {
            var parts = new List<List<(float, float)>>();
            if (geom == null) return parts;
            List<(float, float)> current = null;
            int x = 0, y = 0, i = 0;
            while (i < geom.Length)
            {
                var cmd = geom[i] & 0x7;
                var count = (int)(geom[i] >> 3);
                i++;
                if (cmd == 1 || cmd == 2)
                {
                    for (var c = 0; c < count && i + 1 < geom.Length; c++)
                    {
                        x += ZigZag(geom[i++]);
                        y += ZigZag(geom[i++]);
                        if (cmd == 1)
                        {
                            current = new List<(float, float)>();
                            parts.Add(current);
                        }
                        current?.Add((x, y));
                    }
                }
                else if (cmd != 7)
                {
                    break; // comando inválido
                }
            }
            return parts;
        }

        private static int ZigZag(uint n) => (int)(n >> 1) ^ -(int)(n & 1);

        private struct Reader
        {
            private readonly byte[] _b;
            private int _pos;
            private readonly int _end;

            public Reader(byte[] b, int start, int end)
            {
                _b = b;
                _pos = start;
                _end = end;
            }

            public bool Next(out int field, out int wire)
            {
                if (_pos >= _end)
                {
                    field = wire = 0;
                    return false;
                }
                var key = ReadVarint();
                field = (int)(key >> 3);
                wire = (int)(key & 7);
                return true;
            }

            public ulong ReadVarint()
            {
                ulong result = 0;
                var shift = 0;
                while (_pos < _end)
                {
                    var b = _b[_pos++];
                    result |= (ulong)(b & 0x7F) << shift;
                    if ((b & 0x80) == 0) break;
                    shift += 7;
                }
                return result;
            }

            public int Advance(int n)
            {
                var p = _pos;
                _pos += n;
                return p;
            }

            public void ReadLengthDelimited(out int start, out int len)
            {
                len = (int)ReadVarint();
                start = _pos;
                _pos += len;
            }

            public string ReadString()
            {
                ReadLengthDelimited(out var s, out var l);
                return Encoding.UTF8.GetString(_b, s, l);
            }

            public uint[] ReadPackedUInt32()
            {
                ReadLengthDelimited(out var s, out var l);
                var sub = new Reader(_b, s, s + l);
                var list = new List<uint>(l);
                while (sub._pos < sub._end) list.Add((uint)sub.ReadVarint());
                return list.ToArray();
            }

            public void Skip(int wire)
            {
                switch (wire)
                {
                    case 0: ReadVarint(); break;
                    case 1: _pos += 8; break;
                    case 2: ReadLengthDelimited(out _, out _); break;
                    case 5: _pos += 4; break;
                    default: _pos = _end; break;
                }
            }
        }
    }
}
