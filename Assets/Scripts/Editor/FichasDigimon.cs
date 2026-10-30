using System.IO;
using Niantic.Lightship.Maps.MapLayers.Components;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cria as fichas de batalha (DigimonBase) dos Digimons que aparecem no mapa e ainda não tinham uma,
/// e corrige a camada do Tsunomon (apontava para um prefab que não existe).
/// Menu: Tools > Digimon Nexus > Criar fichas que faltam.
/// </summary>
public static class FichasDigimon
{
    private const string Pasta = "Assets/Resources/Digimons";

    private struct Dados
    {
        public string Nome, Prefab, Descricao;
        public DigimonStage Estagio;
        public DigimonAttribute Atributo;
        public DigimonType Tipo;
        public int HP, ATK, DEF, INT, SP, SPD;
    }

    private static readonly Dados[] Novos =
    {
        new Dados { Nome = "Tsumemon", Prefab = "Characters/Tsumemon/Tsumemon.prefab", Estagio = DigimonStage.InTraining, Atributo = DigimonAttribute.Dark, Tipo = DigimonType.Virus, HP = 400, ATK = 32, DEF = 22, INT = 20, SP = 25, SPD = 30, Descricao = "Digimon bebê de garras afiadas. Gosta de lugares escuros e aparece à noite." },
        new Dados { Nome = "Tsunomon", Prefab = "Characters/Tsunomon/Tsunomon.prefab", Estagio = DigimonStage.InTraining, Atributo = DigimonAttribute.Neutral, Tipo = DigimonType.Data, HP = 410, ATK = 28, DEF = 30, INT = 22, SP = 25, SPD = 28, Descricao = "Digimon bebê com um chifre na cabeça. Evolui para Gabumon." },
        new Dados { Nome = "Tokomon", Prefab = "Characters/Tokomon/Tokomon.prefab", Estagio = DigimonStage.InTraining, Atributo = DigimonAttribute.Light, Tipo = DigimonType.Vaccine, HP = 420, ATK = 26, DEF = 28, INT = 24, SP = 25, SPD = 30, Descricao = "Digimon bebê de boca enorme e muito curioso. Evolui para Patamon." },
        new Dados { Nome = "Tanemon", Prefab = "Characters/Tanemon/Tanemon.prefab", Estagio = DigimonStage.InTraining, Atributo = DigimonAttribute.Plant, Tipo = DigimonType.Data, HP = 410, ATK = 25, DEF = 32, INT = 26, SP = 25, SPD = 24, Descricao = "Digimon bebê com uma folha na cabeça. Vive em parques e jardins." },
        new Dados { Nome = "Bukamon", Prefab = "Characters/Bukamon/Bukamon.prefab", Estagio = DigimonStage.InTraining, Atributo = DigimonAttribute.Water, Tipo = DigimonType.Vaccine, HP = 430, ATK = 27, DEF = 30, INT = 22, SP = 25, SPD = 28, Descricao = "Digimon bebê aquático e brincalhão. Evolui para Gomamon." },
        new Dados { Nome = "Patamon", Prefab = "Characters/Patamon Tsukaimon/Patamon.prefab", Estagio = DigimonStage.Rookie, Atributo = DigimonAttribute.Wind, Tipo = DigimonType.Vaccine, HP = 440, ATK = 45, DEF = 35, INT = 30, SP = 35, SPD = 45, Descricao = "Voa com as orelhas. Seu ataque Boom Bubble dispara um sopro de ar comprimido." },
        new Dados { Nome = "Tsukaimon", Prefab = "Characters/Patamon Tsukaimon/Tsukaimon.prefab", Estagio = DigimonStage.Rookie, Atributo = DigimonAttribute.Dark, Tipo = DigimonType.Virus, HP = 440, ATK = 50, DEF = 32, INT = 32, SP = 35, SPD = 46, Descricao = "Versão sombria de Patamon. Só aparece à noite." },
        new Dados { Nome = "Gomamon", Prefab = "Characters/Gomamon/Gomamon.prefab", Estagio = DigimonStage.Rookie, Atributo = DigimonAttribute.Water, Tipo = DigimonType.Vaccine, HP = 470, ATK = 48, DEF = 40, INT = 28, SP = 30, SPD = 38, Descricao = "Digimon marinho de pelo branco. Comanda cardumes de peixes." },
        new Dados { Nome = "Biyomon", Prefab = "Characters/Biyomon/Biyomon.prefab", Estagio = DigimonStage.Rookie, Atributo = DigimonAttribute.Wind, Tipo = DigimonType.Vaccine, HP = 445, ATK = 50, DEF = 34, INT = 30, SP = 35, SPD = 50, Descricao = "Digimon pássaro. Seu ataque Spiral Twister lança um redemoinho de fogo." },
        new Dados { Nome = "Palmon", Prefab = "Characters/Palmon/Palmon.prefab", Estagio = DigimonStage.Rookie, Atributo = DigimonAttribute.Plant, Tipo = DigimonType.Data, HP = 450, ATK = 44, DEF = 38, INT = 40, SP = 35, SPD = 36, Descricao = "Digimon planta. Prende os inimigos com as vinhas dos braços." },
    };

    [MenuItem("Tools/Digimon Nexus/Criar fichas que faltam")]
    public static string CriarFaltantes()
    {
        var log = new System.Text.StringBuilder();
        Directory.CreateDirectory(Pasta);
        foreach (var d in Novos)
        {
            var caminho = $"{Pasta}/{d.Nome}.asset";
            if (AssetDatabase.LoadAssetAtPath<DigimonBase>(caminho) != null) { log.AppendLine($"já existe: {d.Nome}"); continue; }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/" + d.Prefab);
            if (prefab == null) { log.AppendLine($"ERRO prefab não achado: {d.Prefab}"); continue; }

            var ficha = ScriptableObject.CreateInstance<DigimonBase>();
            AssetDatabase.CreateAsset(ficha, caminho);
            var so = new SerializedObject(ficha);
            so.FindProperty("name_").stringValue = d.Nome;
            so.FindProperty("description_").stringValue = d.Descricao;
            so.FindProperty("prefab_").objectReferenceValue = prefab;
            so.FindProperty("animator_").objectReferenceValue = prefab.GetComponentInChildren<Animator>();
            so.FindProperty("stage_").enumValueIndex = (int)d.Estagio;
            so.FindProperty("atribute_").enumValueIndex = (int)d.Atributo;
            so.FindProperty("type_").enumValueIndex = (int)d.Tipo;
            so.FindProperty("HP").intValue = d.HP;
            so.FindProperty("ATK").intValue = d.ATK;
            so.FindProperty("DEF").intValue = d.DEF;
            so.FindProperty("INT").intValue = d.INT;
            so.FindProperty("SP").intValue = d.SP;
            so.FindProperty("SPD").intValue = d.SPD;

            var golpes = so.FindProperty("learnableMoves_");
            golpes.arraySize = 0;
            AdicionarGolpe(golpes, GolpePorAtributo(d.Atributo), 1);
            AdicionarGolpe(golpes, "Attack Charge", 6);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ficha);
            log.AppendLine($"criada: {d.Nome}");
        }

        log.AppendLine(CorrigirCamadaTsunomon());
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    private static string GolpePorAtributo(DigimonAttribute a)
    {
        switch (a)
        {
            case DigimonAttribute.Water: return "Soap Flower";
            case DigimonAttribute.Plant: return "Crescent Leaf I";
            case DigimonAttribute.Wind: return "Wind Claw I";
            case DigimonAttribute.Fire: return "Burst Flame I";
            default: return "Heavy Strike I";
        }
    }

    private static void AdicionarGolpe(SerializedProperty lista, string nome, int nivel)
    {
        var golpe = AssetDatabase.LoadAssetAtPath<MoveBase>($"Assets/Resources/Moves/{nome}.asset");
        if (golpe == null) return;
        lista.arraySize++;
        var e = lista.GetArrayElementAtIndex(lista.arraySize - 1);
        e.FindPropertyRelative("movebase").objectReferenceValue = golpe;
        e.FindPropertyRelative("level").intValue = nivel;
    }

    private static string CorrigirCamadaTsunomon()
    {
        const string camada = "Assets/Lightship/Layers/Tsunomon_Layer.prefab";
        var raiz = PrefabUtility.LoadPrefabContents(camada);
        try
        {
            var p = raiz.GetComponent<LayerGameObjectPlacement>();
            if (p == null) return "Tsunomon_Layer sem LayerGameObjectPlacement";
            if (p.Prefab != null) return "Tsunomon_Layer já tem prefab";
            var so = new SerializedObject(p);
            so.FindProperty("_prefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Tsunomon/Tsunomon.prefab");
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, camada);
            return "Tsunomon_Layer corrigida";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }
}
