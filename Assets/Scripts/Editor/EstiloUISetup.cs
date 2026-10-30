using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gera os sprites de cantos cortados no padrão das telas (borda branca, azul do jogo)
/// e monta o asset Resources/UI/EstiloUI com fontes e sprites que já existem no projeto.
/// Menu: Tools > Digimon Nexus > Criar estilo de UI.
/// </summary>
public static class EstiloUISetup
{
    private const string PastaGerados = "Assets/UI/Gerado";
    private const string CaminhoEstilo = "Assets/Resources/UI/EstiloUI.asset";

    [MenuItem("Tools/Digimon Nexus/Criar estilo de UI")]
    public static string Criar()
    {
        Directory.CreateDirectory(PastaGerados);
        var azul = new Color32(0x00, 0xA8, 0xF3, 0xFF);
        var azulEscuro = new Color32(0x2E, 0x74, 0xCA, 0xFF);
        var azulCartao = new Color32(0x00, 0xB6, 0xFF, 0xFF);

        // Botão (como "Daily"): canto cortado grande, borda branca grossa.
        var botao = Gerar("Botao", 128, 30, 7, azul, Color.white);
        var botaoSel = Gerar("BotaoSelecionado", 128, 30, 7, azulEscuro, Color.white);
        // Cartão (como o cartão de missão): canto cortado menor, borda branca fina.
        var cartao = Gerar("Cartao", 96, 16, 3, azulCartao, Color.white);
        // Moldura do painel: só a borda.
        var moldura = Gerar("Moldura", 160, 40, 6, new Color32(0, 0, 0, 0), Color.white);

        Directory.CreateDirectory(Path.GetDirectoryName(CaminhoEstilo));
        var e = AssetDatabase.LoadAssetAtPath<EstiloUI>(CaminhoEstilo);
        if (e == null)
        {
            e = ScriptableObject.CreateInstance<EstiloUI>();
            AssetDatabase.CreateAsset(e, CaminhoEstilo);
        }

        e.FonteNegrito = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/Lato-Bold SDF.asset");
        e.FonteNormal = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/Lato-Regular SDF.asset");
        e.FontePesada = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/Lato-Black SDF.asset");
        e.Fundo = Sprite("Assets/UI/Telas/Stay/Rectangle 59.png");
        e.IconeDigivice = Sprite("Assets/UI/Telas/Group 20.png");
        e.BotaoBatalha = Sprite("Assets/UI/Prefabs/Resources/battle_button_.png");
        e.BotaoOkBatalha = Sprite("Assets/UI/Group 10.png");
        e.Botao = botao;
        e.BotaoSelecionado = botaoSel;
        e.Cartao = cartao;
        e.Moldura = moldura;
        EditorUtility.SetDirty(e);
        AssetDatabase.SaveAssets();

        return $"Estilo criado: fontes {(e.FonteNegrito ? "ok" : "FALTANDO")}, fundo {(e.Fundo ? "ok" : "FALTANDO")}, " +
               $"digivice {(e.IconeDigivice ? "ok" : "FALTANDO")}, ok-batalha {(e.BotaoOkBatalha ? "ok" : "FALTANDO")}";
    }

    private static Sprite Sprite(string caminho)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        if (s != null) return s;
        // Algumas imagens podem não estar importadas como sprite.
        var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
        if (imp == null) return null;
        imp.textureType = TextureImporterType.Sprite;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
    }

    /// <summary>Octógono (retângulo de cantos cortados) com borda, suavizado, pronto para 9-slice.</summary>
    private static Sprite Gerar(string nome, int tamanho, int corte, int borda, Color32 preenchimento, Color32 corBorda)
    {
        var px = new Color32[tamanho * tamanho];
        const int ss = 4; // supersampling para bordas suaves
        for (var y = 0; y < tamanho; y++)
        for (var x = 0; x < tamanho; x++)
        {
            float dentro = 0, naBorda = 0;
            for (var sy = 0; sy < ss; sy++)
            for (var sx = 0; sx < ss; sx++)
            {
                var fx = x + (sx + 0.5f) / ss;
                var fy = y + (sy + 0.5f) / ss;
                var d = Distancia(fx, fy, tamanho, corte);       // distância até a borda externa (positiva = dentro)
                if (d < 0) continue;
                if (d < borda) naBorda++;
                else dentro++;
            }
            var total = (float)(ss * ss);
            Color f = preenchimento, c = corBorda;
            var pesoBorda = naBorda / total * c.a;
            var pesoDentro = dentro / total * f.a;
            var alfa = pesoBorda + pesoDentro;
            px[y * tamanho + x] = alfa > 0.0001f
                ? new Color((c.r * pesoBorda + f.r * pesoDentro) / alfa, (c.g * pesoBorda + f.g * pesoDentro) / alfa,
                    (c.b * pesoBorda + f.b * pesoDentro) / alfa, alfa)
                : new Color(0, 0, 0, 0);
        }

        var tex = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        var caminho = $"{PastaGerados}/{nome}.png";
        File.WriteAllBytes(caminho, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);

        var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        var b = corte + borda + 2;
        imp.spriteBorder = new Vector4(b, b, b, b);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
    }

    /// <summary>Distância (em pixels) de um ponto até a borda de um quadrado com cantos cortados a 45°.</summary>
    private static float Distancia(float x, float y, int tamanho, int corte)
    {
        var dx = Mathf.Min(x, tamanho - x);
        var dy = Mathf.Min(y, tamanho - y);
        var reto = Mathf.Min(dx, dy);
        var diagonal = (dx + dy - corte) / Mathf.Sqrt(2f);
        return Mathf.Min(reto, diagonal);
    }
}
