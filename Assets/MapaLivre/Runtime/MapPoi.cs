using Niantic.Lightship.Maps.Core.Coordinates;

namespace Niantic.Lightship.Maps
{
    /// <summary>
    /// Um lugar com nome no mapa (praça, igreja, museu, loja...), vindo do OpenStreetMap.
    /// Bom para pontos fixos de jogo, como os PokéStops.
    /// </summary>
    public struct MapPoi
    {
        public LatLng Location;
        public string Name;
        /// <summary>Tipo do lugar no esquema OpenMapTiles (ex.: "place_of_worship", "park", "shop").</summary>
        public string Class;
        public string Subclass;
        /// <summary>Importância: quanto menor, mais conhecido o lugar.</summary>
        public int Rank;
    }
}
