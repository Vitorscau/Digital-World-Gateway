using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configura as imagens baixadas do Figma (Assets/Resources/UI/Figma) para uso na UI:
/// PNG viram Sprite; SVG viram sprite texturizado (rasterizado pelo importador de SVG do Unity).
/// Menu: Tools > Digimon Nexus > Configurar imagens do Figma.
/// </summary>
public static class FigmaAssets
{
    public const string Pasta = "Assets/Resources/UI/Figma";

    [MenuItem("Tools/Digimon Nexus/Configurar imagens do Figma")]
    public static string Configurar()
    {
        var log = new System.Text.StringBuilder();
        foreach (var caminho in Directory.GetFiles(Pasta).Where(f => !f.EndsWith(".meta")).Select(f => f.Replace('\\', '/')))
        {
            var imp = AssetImporter.GetAtPath(caminho);
            if (imp is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
                log.AppendLine($"png: {Path.GetFileName(caminho)}");
            }
            else if (imp != null && imp.GetType().Name == "SVGImporter")
            {
                var so = new SerializedObject(imp);
                // m_SvgType: Default | VectorSprite | TexturedSprite | Texture2D | VectorImage | UISVGImage
                var tipo = so.FindProperty("m_SvgType");
                if (tipo != null) tipo.enumValueIndex = System.Array.IndexOf(tipo.enumNames, "TexturedSprite");
                var tam = so.FindProperty("m_TextureSize");
                if (tam != null) tam.intValue = 1024;
                var viewport = so.FindProperty("m_PreserveViewport");
                if (viewport != null) viewport.boolValue = true; // mantém as margens do desenho (contornos não cortam)
                var aspecto = so.FindProperty("m_KeepTextureAspectRatio");
                if (aspecto != null) aspecto.boolValue = true;
                var aa = so.FindProperty("m_SampleCount");
                if (aa != null) aa.intValue = 4;
                so.ApplyModifiedPropertiesWithoutUndo();
                imp.SaveAndReimport();
                var sprite = AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Sprite>().FirstOrDefault();
                log.AppendLine($"svg: {Path.GetFileName(caminho)} -> {(sprite != null ? $"sprite {sprite.rect.width}x{sprite.rect.height}" : "SEM SPRITE")}");
            }
        }
        return log.ToString();
    }
}
