using UnityEngine;

namespace Niantic.Lightship.Maps
{
    /// <summary>Aparência do mapa vetorial: material e largura de cada tipo de coisa.</summary>
    [CreateAssetMenu(menuName = "MapaLivre/Tema do mapa", fileName = "TemaMapa")]
    public class MapaLivreTheme : ScriptableObject
    {
        [Header("Áreas")]
        public Material Ground;
        public Material Park;
        public Material Forest;
        public Material Sand;
        public Material Water;
        public Material Special;

        [Header("Ruas")]
        public Material MajorRoad;
        public Material MinorRoad;
        public Material Path;
        public float MajorRoadWidth = 14f;
        public float MinorRoadWidth = 9f;
        public float PathWidth = 3.5f;
        public float WaterwayWidth = 5f;
        [Tooltip("Borda escura das ruas (vazio = sem borda).")]
        public Material RoadOutline;
        [Tooltip("Espessura da borda em metros, de cada lado.")]
        public float RoadOutlineWidth = 1.5f;

        [Header("Prédios")]
        public bool ShowBuildings = true;
        public Material BuildingTop;
        public Material BuildingWall;
        [Tooltip("Multiplica a altura real dos prédios (0 = prédios planos).")]
        public float BuildingHeightScale = 0.3f;
        public float BuildingMinHeight = 1.5f;
        public float BuildingMaxHeight = 12f;

        [Header("Texturas")]
        [Tooltip("De quantos em quantos metros a textura se repete.")]
        public float TextureRepeatMeters = 8f;

        [Header("Fundo")]
        public Color SkyColor = new Color(0.62f, 0.85f, 0.95f);
        [Tooltip("Névoa na cor do céu para esconder a borda do mapa.")]
        public bool UseFog = true;
        public float FogStart = 350f;
        public float FogEnd = 650f;
    }
}
