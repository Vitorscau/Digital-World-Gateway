// MapaLivre: substituto do Niantic Lightship Maps SDK (descontinuado em out/2025).
// Mantém os mesmos namespaces e nomes de classes para que cenas e prefabs antigos continuem funcionando.
using System;
using UnityEngine;

namespace Niantic.Lightship.Maps.Core.Coordinates
{
    [Serializable]
    public readonly struct LatLng : IEquatable<LatLng>
    {
        public double Latitude { get; }
        public double Longitude { get; }

        public LatLng(double latitude, double longitude)
        {
            Latitude = Math.Max(-85.05112878, Math.Min(85.05112878, latitude));
            Longitude = longitude;
        }

        public bool Equals(LatLng other) => Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude);
        public override bool Equals(object obj) => obj is LatLng other && Equals(other);
        public override int GetHashCode() => (Latitude.GetHashCode() * 397) ^ Longitude.GetHashCode();
        public override string ToString() => $"({Latitude:F6}, {Longitude:F6})";
    }
}

namespace Niantic.Lightship.Maps.Coordinates
{
    using Niantic.Lightship.Maps.Core.Coordinates;

    /// <summary>LatLng que aparece no Inspector (mesmos campos do SDK original).</summary>
    [Serializable]
    public struct SerializableLatLng
    {
        [SerializeField] private double _latitude;
        [SerializeField] private double _longitude;

        public SerializableLatLng(double latitude, double longitude)
        {
            _latitude = latitude;
            _longitude = longitude;
        }

        public double Latitude => _latitude;
        public double Longitude => _longitude;

        public static implicit operator LatLng(SerializableLatLng s) => new LatLng(s._latitude, s._longitude);
        public static implicit operator SerializableLatLng(LatLng l) => new SerializableLatLng(l.Latitude, l.Longitude);
    }
}

namespace Niantic.Lightship.Maps.Core
{
    using Niantic.Lightship.Maps.Core.Coordinates;

    /// <summary>Matemática Web Mercator (EPSG:3857), a mesma dos tiles do OpenStreetMap.</summary>
    public static class WebMercator
    {
        public const double EarthRadius = 6378137.0;
        public const double HalfCircumference = Math.PI * EarthRadius;

        public static double ToMercX(double lng) => EarthRadius * lng * Math.PI / 180.0;

        public static double ToMercY(double lat)
        {
            var rad = lat * Math.PI / 180.0;
            return EarthRadius * Math.Log(Math.Tan(Math.PI / 4.0 + rad / 2.0));
        }

        public static double ToLng(double mercX) => mercX / EarthRadius * 180.0 / Math.PI;

        public static double ToLat(double mercY) =>
            (2.0 * Math.Atan(Math.Exp(mercY / EarthRadius)) - Math.PI / 2.0) * 180.0 / Math.PI;

        public static double TileSizeMerc(int zoom) => 2.0 * HalfCircumference / (1 << zoom);

        public static void ToTile(LatLng p, int zoom, out int x, out int y)
        {
            var size = TileSizeMerc(zoom);
            x = (int)Math.Floor((ToMercX(p.Longitude) + HalfCircumference) / size);
            y = (int)Math.Floor((HalfCircumference - ToMercY(p.Latitude)) / size);
        }
    }
}
