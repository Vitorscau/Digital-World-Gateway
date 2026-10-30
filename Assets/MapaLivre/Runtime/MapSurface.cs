namespace Niantic.Lightship.Maps
{
    /// <summary>O que existe num ponto do mapa (usado, por exemplo, para escolher o habitat dos Digimons).</summary>
    public enum MapSurface
    {
        /// <summary>Os dados dessa área ainda não carregaram.</summary>
        Unknown,
        /// <summary>Chão comum: ruas, calçadas, terrenos.</summary>
        Ground,
        /// <summary>Parques, gramados, matas, areia.</summary>
        Park,
        /// <summary>Rios, lagos, mar.</summary>
        Water,
        /// <summary>Dentro de um prédio.</summary>
        Building
    }
}
