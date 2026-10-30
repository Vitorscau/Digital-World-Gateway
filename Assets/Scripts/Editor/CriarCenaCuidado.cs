using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta a cena de cuidado (Care_Scene) a partir da AR_Battle_Scene: mesmo AR (sessão, câmera e malha do ambiente),
/// sem a parte da batalha, com o controlador CenaCuidado. Também dá o estado "eat" aos Animators do Digimon Links.
/// Menu: Tools > Digimon Nexus > Criar cena de cuidado.
/// </summary>
public static class CriarCenaCuidado
{
    private const string Origem = "Assets/Scenes/AR_Battle_Scene.unity";
    private const string Destino = "Assets/Scenes/Care_Scene.unity";

    [MenuItem("Tools/Digimon Nexus/Criar cena de cuidado")]
    public static void Executar()
    {
        var log = new StringBuilder();
        var cena = EditorSceneManager.OpenScene(Origem, OpenSceneMode.Single);
        foreach (var raiz in cena.GetRootGameObjects())
        {
            var batalha = raiz.name == "PrefabManager" || raiz.name == "Battle_Canvas" || raiz.name == "BattleSystem" ||
                          raiz.GetComponentInChildren<NavMeshHowTo>(true) != null ||
                          raiz.GetComponentInChildren<BattleManager>(true) != null;
            log.AppendLine((batalha ? "removido: " : "mantido: ") + raiz.name);
            if (batalha) Object.DestroyImmediate(raiz);
        }
        new GameObject("Cuidado").AddComponent<CenaCuidado>();
        EditorSceneManager.SaveScene(cena, Destino); // "salvar como": a AR_Battle_Scene não muda

        var cenas = EditorBuildSettings.scenes.ToList();
        if (!cenas.Any(s => s.path == Destino))
        {
            cenas.Add(new EditorBuildSettingsScene(Destino, true));
            EditorBuildSettings.scenes = cenas.ToArray();
            log.AppendLine("Care_Scene adicionada ao build");
        }

        log.AppendLine($"estado eat adicionado em {AdicionarEstadoComer()} Animators");
        AssetDatabase.SaveAssets();
        Debug.Log("[CriarCenaCuidado]\n" + log);
    }

    public static void ExecutarBatch()
    {
        Executar();
        EditorApplication.Exit(0);
    }

    /// <summary>Trigger "Eat" -> clipe "eat" (do mesmo FBX do idle), voltando ao idle no fim.</summary>
    private static int AdicionarEstadoComer()
    {
        var n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { ImportarDigimonLinks.Raiz }))
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
            if (ac == null || ac.parameters.Any(p => p.name == "Eat")) continue;
            var sm = ac.layers[0].stateMachine;
            var idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "idle");
            if (idle == null || idle.motion == null) continue;
            var fbx = AssetDatabase.GetAssetPath(idle.motion);
            var comer = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == "eat");
            if (comer == null) continue;
            ac.AddParameter("Eat", AnimatorControllerParameterType.Trigger);
            var estado = sm.AddState("eat", new Vector3(600, 360));
            estado.motion = comer;
            var ida = sm.AddAnyStateTransition(estado);
            ida.AddCondition(AnimatorConditionMode.If, 0, "Eat");
            ida.hasExitTime = false; ida.duration = 0.05f; ida.canTransitionToSelf = false;
            var volta = estado.AddTransition(idle);
            volta.hasExitTime = true; volta.exitTime = 0.95f; volta.duration = 0.1f;
            EditorUtility.SetDirty(ac);
            n++;
        }
        return n;
    }
}
