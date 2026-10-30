using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Configura a importação dos modelos do Digimon Links (Assets/Characters/DigimonLinks):
/// animação Generic com os clipes separados (idle, move, attack01...), sem câmeras e luzes.
/// </summary>
public class ImportarDigimonLinksPostprocessor : AssetPostprocessor
{
    private static bool EhDaPasta(string caminho) => caminho.Replace('\\', '/').StartsWith(ImportarDigimonLinks.Raiz + "/");

    private void OnPreprocessModel()
    {
        if (!EhDaPasta(assetPath)) return;
        var m = (ModelImporter)assetImporter;
        m.globalScale = 1f;
        m.useFileScale = true;
        m.importCameras = false;
        m.importLights = false;
        m.importVisibility = false;
        m.animationType = ModelImporterAnimationType.Generic;
        m.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        m.importAnimation = true;
        m.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        m.materialLocation = ModelImporterMaterialLocation.InPrefab;
        m.materialSearch = ModelImporterMaterialSearch.Local;
        m.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
    }

    private void OnPreprocessAnimation()
    {
        if (!EhDaPasta(assetPath)) return;
        var m = (ModelImporter)assetImporter;
        var clipes = m.defaultClipAnimations;
        foreach (var c in clipes)
        {
            c.name = ImportarDigimonLinks.NomeDoClipe(c.takeName);
            c.loopTime = c.loopPose = c.name == "idle" || c.name == "move";
            c.keepOriginalPositionY = true;
            c.keepOriginalPositionXZ = true;
            c.keepOriginalOrientation = true;
        }
        m.clipAnimations = clipes;
    }

    private void OnPreprocessTexture()
    {
        if (!EhDaPasta(assetPath)) return;
        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Default;
        t.sRGBTexture = true;
        t.mipmapEnabled = true;
        t.maxTextureSize = 1024;
        t.textureCompression = TextureImporterCompression.Compressed;
        t.alphaIsTransparency = true;
    }
}

/// <summary>
/// Monta os Digimon do pacote Digimon Links (extraídos em Assets/Characters/DigimonLinks, um por pasta,
/// listados em _manifesto.tsv: pasta, estágio e nomes): materiais OToon, Animator com os estados de batalha,
/// prefab no padrão dos outros Digimon (colisor + Ripple, tamanho pelo estágio) e ficha em Resources/Digimons.
/// Menu: Tools > Digimon Nexus > Importar Digimon Links.
/// </summary>
public static class ImportarDigimonLinks
{
    public const string Raiz = "Assets/Characters/DigimonLinks";
    private const string PastaFichas = "Assets/Resources/Digimons";
    private const string MaterialModelo = "Assets/Characters/Patamon Tsukaimon/Tsukaimon_Cell_Shading_Mat.mat";
    private const string PrefabReferencia = "Assets/Characters/Palmon/Palmon.prefab";
    private const string Ripple = "Assets/Particles/Ripple_PS.prefab";

    private static readonly StringBuilder Log = new StringBuilder();

    /// <summary>
    /// Nome do clipe a partir do take:
    /// Digimon Links: "Armature|idle|Base Layer" -> "idle".
    /// Cyber Sleuth / ReArise usam códigos da Bandai ("chr516_bn01_rd", "chr322_ba01"):
    /// bn01 = parado, br01 = correr, ba01/ba02 = ataques, bs01 = técnica, bd01 = dano, bd03/bd02 = cair,
    /// bg01 = defesa, bv01 = vitória, fe01 = emoção (usada como "comer").
    /// </summary>
    public static string NomeDoClipe(string take)
    {
        var partes = take.Split('|');
        foreach (var p in partes)
        {
            var m = Regex.Match(p, @"_(b[a-z]\d\d|fe\d\d)(?:_|$)", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            switch (m.Groups[1].Value.ToLowerInvariant())
            {
                case "bn01": return "idle";
                case "br01": return "move";
                case "ba01": return "attack01";
                case "ba02": return "attack02";
                case "bs01": return "skill";
                case "bd01": return "damage";
                case "bd02": return "damage2";
                case "bd03": return "down";
                case "bg01": return "guard";
                case "bv01": return "win";
                case "fe01": return "eat";
                default: return m.Groups[1].Value.ToLowerInvariant();
            }
        }
        return (partes.Length >= 2 ? partes[1] : take).Trim().ToLowerInvariant();
    }

    [MenuItem("Tools/Digimon Nexus/Importar Digimon Links")]
    public static void Executar() => Executar(Raiz + "/_manifesto.tsv");

    /// <summary>Só as pastas listadas em _novos.tsv (Digimon adicionados depois).</summary>
    [MenuItem("Tools/Digimon Nexus/Importar Digimon novos")]
    public static void ExecutarNovos() => Executar(Raiz + "/_novos.tsv");

    /// <summary>Resumo da última importação.</summary>
    public static string UltimoLog => Log.ToString();

    public static void Executar(string manifesto)
    {
        Log.Clear();
        if (!File.Exists(manifesto)) { Debug.LogError("Sem " + manifesto); return; }

        // Garante que os modelos passaram pelo postprocessor (clipes separados).
        AssetDatabase.Refresh();
        ReimportarSemClipes();

        var modelo = AssetDatabase.LoadAssetAtPath<Material>(MaterialModelo);
        var alturaRef = Altura(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabReferencia), out var escalaRef);
        Log.AppendLine($"altura de referência (Palmon) = {alturaRef:0.###}");
        Directory.CreateDirectory(PastaFichas);

        var criados = new List<(string nome, GameObject prefab)>();
        foreach (var linha in File.ReadAllLines(manifesto))
        {
            if (string.IsNullOrWhiteSpace(linha)) continue;
            var c = linha.Split('\t');
            try { criados.AddRange(MontarPasta(c[0], c[1], c[2].Split(';'), modelo, alturaRef)); }
            catch (Exception e) { Log.AppendLine($"ERRO {c[0]}: {e}"); }
        }
        AssetDatabase.SaveAssets();
        Log.AppendLine($"prefabs criados: {criados.Count}");

        var saida = Environment.GetEnvironmentVariable("DIGIMON_LINKS_SAIDA");
        if (!string.IsNullOrEmpty(saida))
        {
            Directory.CreateDirectory(saida);
            Folhas(criados, saida);
            File.WriteAllText(Path.Combine(saida, "log.txt"), Log.ToString());
        }
        Debug.Log("[DigimonLinks]\n" + Log);
    }

    /// <summary>Para rodar em batchmode (-executeMethod).</summary>
    public static void ExecutarBatch()
    {
        try { Executar(); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        EditorApplication.Exit(0);
    }

    private static void ReimportarSemClipes()
    {
        var caminhos = AssetDatabase.FindAssets("t:Model", new[] { Raiz }).Select(AssetDatabase.GUIDToAssetPath).ToList();
        var refazer = caminhos.Where(p =>
        {
            var m = AssetImporter.GetAtPath(p) as ModelImporter;
            return m != null && (m.animationType != ModelImporterAnimationType.Generic ||
                                 (m.importedTakeInfos.Length > 0 && m.clipAnimations.Length == 0));
        }).ToList();
        if (refazer.Count == 0) return;
        Log.AppendLine($"reimportando {refazer.Count} modelos");
        AssetDatabase.StartAssetEditing();
        try { foreach (var p in refazer) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate); }
        finally { AssetDatabase.StopAssetEditing(); }
    }

    // ------------------------------------------------------------ Uma pasta (um zip)

    private static IEnumerable<(string, GameObject)> MontarPasta(string pasta, string estagioTxt, string[] nomes, Material modelo, float alturaRef)
    {
        var dir = $"{Raiz}/{pasta}";
        var fbxs = Directory.GetFiles(dir, "*.fbx").Select(f => f.Replace('\\', '/')).OrderBy(f => f).ToList();
        var pngs = Directory.GetFiles(dir, "*.png").Select(f => f.Replace('\\', '/')).ToList();
        nomes = NomesCompletos(nomes);
        var estagio = Estagio(estagioTxt);
        var resultado = new List<(string, GameObject)>();
        var todosUsados = new HashSet<string>();
        var usadosDoUltimo = new HashSet<string>();

        for (var i = 0; i < fbxs.Count; i++)
        {
            var nome = i < nomes.Length ? nomes[i] : $"{nomes[0]} {i + 1}";
            usadosDoUltimo = new HashSet<string>();
            var r = MontarModelo(dir, fbxs[i], nome, estagio, pngs, null, modelo, alturaRef, usadosDoUltimo);
            todosUsados.UnionWith(usadosDoUltimo);
            if (r != null) resultado.Add((nome, r));
        }

        // Sobraram nomes: são a última malha com outra textura (ex.: Greymon -> BlackGreymon).
        if (nomes.Length > fbxs.Count && fbxs.Count > 0)
        {
            var numerosUsados = new HashSet<string>(todosUsados.Select(NumeroChr).Where(n => n != null));
            var sobra = pngs.Where(p => !todosUsados.Contains(p) && !EhEfeito(p) && !numerosUsados.Contains(NumeroChr(p))).ToList();
            var grupos = sobra.GroupBy(p => NumeroChr(p)).Where(g => g.Key != null).OrderBy(g => g.Key).ToList();
            for (var k = fbxs.Count; k < nomes.Length; k++)
            {
                var g = k - fbxs.Count;
                if (g >= grupos.Count) { Log.AppendLine($"pulado (sem textura alternativa): {nomes[k]}"); continue; }
                var troca = TrocaDeTexturas(usadosDoUltimo, grupos[g].ToList());
                if (troca.Count == 0) { Log.AppendLine($"pulado (texturas não combinam): {nomes[k]}"); continue; }
                var r = MontarModelo(dir, fbxs[fbxs.Count - 1], nomes[k], estagio, pngs, troca, modelo, alturaRef, new HashSet<string>());
                if (r != null) resultado.Add((nomes[k], r));
            }
        }
        return resultado;
    }

    private static string[] NomesCompletos(string[] nomes)
    {
        var baseNome = nomes[0].Split(' ')[0];
        return nomes.Select(n =>
        {
            n = n.Trim().Replace("BlackGarurmon", "BlackGarurumon");
            if (n.EndsWith("Mode") && !n.Contains(baseNome)) n = baseNome + " " + n;
            return n;
        }).ToArray();
    }

    private static string NumeroChr(string png)
    {
        var m = Regex.Match(Path.GetFileNameWithoutExtension(png), @"^chr(\d+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string SufixoChr(string png) =>
        Regex.Replace(Path.GetFileNameWithoutExtension(png), @"^chr\d+", "", RegexOptions.IgnoreCase).ToLowerInvariant();

    private static bool EhEfeito(string png)
    {
        var n = Path.GetFileNameWithoutExtension(png).ToLowerInvariant();
        return n.StartsWith("eff") || n.Contains("_eff");
    }

    /// <summary>Textura original -> textura alternativa, pelo mesmo final de nome (a01, a02...).</summary>
    private static Dictionary<string, string> TrocaDeTexturas(HashSet<string> originais, List<string> grupo)
    {
        var troca = new Dictionary<string, string>();
        var orig = originais.Where(o => NumeroChr(o) != null).ToList();
        foreach (var o in orig)
        {
            var alt = grupo.FirstOrDefault(g => SufixoChr(g) == SufixoChr(o));
            if (alt != null) troca[o] = alt;
        }
        if (troca.Count > 0) return troca;
        // Nomes diferentes (a01 x _m1): pareia pela ordem, se a quantidade bate (ou se há uma só).
        var alts = grupo.Where(g => !Path.GetFileNameWithoutExtension(g).EndsWith("s")).OrderBy(g => g).ToList();
        var origs = orig.OrderBy(o => o).ToList();
        if (origs.Count == 1 && alts.Count >= 1) troca[origs[0]] = alts.OrderBy(a => a.Length).First();
        else if (origs.Count == alts.Count) for (var i = 0; i < origs.Count; i++) troca[origs[i]] = alts[i];
        return troca;
    }

    // ------------------------------------------------------------ Um modelo

    private static GameObject MontarModelo(string dir, string fbx, string nome, DigimonStage estagio, List<string> pngs,
        Dictionary<string, string> troca, Material modelo, float alturaRef, HashSet<string> usados)
    {
        var fonte = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (fonte == null) { Log.AppendLine($"ERRO não carregou {fbx}"); return null; }
        var arquivo = Arquivo(nome);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(fonte);
        try
        {
            go.name = nome;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            // Materiais OToon com a textura certa.
            Directory.CreateDirectory($"{dir}/Materials");
            var cyberSleuth = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && Regex.IsMatch(mats[i].name, @"_mat_\d+$")) cyberSleuth = true;
                    var png = TexturaDe(mats[i], pngs);
                    if (png == null) { Log.AppendLine($"aviso {nome}: sem textura para '{mats[i]?.name}'"); continue; }
                    usados.Add(png);
                    if (troca != null && troca.TryGetValue(png, out var alt)) png = alt;
                    mats[i] = MaterialPara(dir, png, modelo);
                }
                r.sharedMaterials = mats;
            }

            // Modelos do Cyber Sleuth vêm de cabeça para baixo (outro eixo "para cima"). As peças vão para
            // dentro de um "Corpo" girado 180° — com o Animator nele, os caminhos da animação continuam iguais.
            var corpo = go.transform;
            if (cyberSleuth)
            {
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var original = go.GetComponent<Animator>();
                var avatar = original != null ? original.avatar : null;
                if (original != null) UnityEngine.Object.DestroyImmediate(original);
                var c = new GameObject("Corpo").transform;
                c.SetParent(go.transform, false);
                foreach (var filho in go.transform.Cast<Transform>().Where(t => t != c).ToList()) filho.SetParent(c, true);
                c.localRotation = Quaternion.Euler(90f, 0f, 0f); // desfaz a conversão de eixo: de pé e de frente como os outros
                c.gameObject.AddComponent<Animator>().avatar = avatar;
                corpo = c;
            }

            // Animação.
            var clipes = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToList();
            var animator = corpo.GetComponent<Animator>();
            if (clipes.Count > 0)
            {
                if (animator == null) animator = corpo.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = Controller(dir, Path.GetFileNameWithoutExtension(fbx), clipes);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var idle = clipes.FirstOrDefault(c => c.name == "idle");
                if (idle != null) idle.SampleAnimation(corpo.gameObject, 0f); // pose de pé para medir
            }
            else Log.AppendLine($"sem animação: {nome}");

            // Tamanho pelo estágio, como os Digimon que já existem.
            var altura = AlturaMalhas(go, out var limites);
            if (altura <= 0f) { Log.AppendLine($"ERRO {nome}: sem malha"); return null; }
            if (cyberSleuth)
            {
                // Depois de virar, os pés ficam abaixo da origem: sobe o corpo até o chão.
                corpo.localPosition += Vector3.up * -limites.min.y;
                altura = AlturaMalhas(go, out limites);
            }
            var escala = alturaRef * Multiplicador(estagio) / altura;
            go.transform.localScale = Vector3.one * escala;

            // Colisor para tocar no mapa (em coordenadas locais).
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(limites.center.x, limites.center.y, limites.center.z);
            col.height = limites.size.y;
            col.radius = Mathf.Max(limites.size.x, limites.size.z) * 0.5f;
            col.direction = 1;

            // Ondinha no chão (igual aos outros Digimon do mapa).
            var ripple = AssetDatabase.LoadAssetAtPath<GameObject>(Ripple);
            if (ripple != null)
            {
                var rp = (GameObject)PrefabUtility.InstantiatePrefab(ripple, go.transform);
                rp.transform.localPosition = new Vector3(limites.center.x, limites.min.y + limites.size.y * 0.02f, limites.center.z);
                rp.transform.localRotation = Quaternion.identity;
                rp.transform.localScale = ripple.transform.localScale * (escala / 1346.933f);
            }

            var caminho = $"{dir}/{arquivo}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, caminho);
            Ficha(nome, prefab, estagio);
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static string Arquivo(string nome) => Regex.Replace(nome, @"[^A-Za-z0-9]+", "_").Trim('_');

    private static string TexturaDe(Material original, List<string> pngs)
    {
        if (original == null) return pngs.Count == 1 ? pngs[0] : null;
        if (original.mainTexture != null)
        {
            var p = AssetDatabase.GetAssetPath(original.mainTexture);
            if (!string.IsNullOrEmpty(p) && pngs.Contains(p)) return p;
            var porNome = pngs.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(original.mainTexture.name, StringComparison.OrdinalIgnoreCase));
            if (porNome != null) return porNome;
        }
        var n = Regex.Replace(original.name, @"\.\d+$", "").Trim();
        return pngs.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(n, StringComparison.OrdinalIgnoreCase))
               ?? pngs.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).StartsWith(n, StringComparison.OrdinalIgnoreCase))
               ?? pngs.FirstOrDefault(x => n.StartsWith(Path.GetFileNameWithoutExtension(x), StringComparison.OrdinalIgnoreCase))
               ?? (pngs.Count(x => !EhEfeito(x)) == 1 ? pngs.First(x => !EhEfeito(x)) : null)
               ?? PorPosicao(n, pngs);
    }

    /// <summary>
    /// Cyber Sleuth: materiais com nome genérico ("chr322_mat_001") usam as texturas de cor pela ordem
    /// (a01, b01/a02...). As de brilho ("s", "s_a") ficam de fora.
    /// </summary>
    private static string PorPosicao(string material, List<string> pngs)
    {
        var m = Regex.Match(material, @"_mat_(\d+)$", RegexOptions.IgnoreCase);
        var cores = pngs.Where(x => !EhEfeito(x) && !Regex.IsMatch(Path.GetFileNameWithoutExtension(x), @"s(_a)?$", RegexOptions.IgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        if (cores.Count == 0) return null;
        var i = m.Success ? int.Parse(m.Groups[1].Value) : 0;
        return cores[Mathf.Clamp(i, 0, cores.Count - 1)];
    }

    private static Material MaterialPara(string dir, string png, Material modelo)
    {
        var caminho = $"{dir}/Materials/{Arquivo(Path.GetFileNameWithoutExtension(png))}.mat";
        var existente = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (existente != null) return existente;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        var m = new Material(modelo) { name = Path.GetFileNameWithoutExtension(caminho) };
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (EhEfeito(png))
        {
            // Planos de efeito (brilho, fogo): recorta pelo alfa em vez de virar um quadrado.
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.3f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
        }
        AssetDatabase.CreateAsset(m, caminho);
        return m;
    }

    // ------------------------------------------------------------ Animator

    private static AnimatorController Controller(string dir, string fbxNome, List<AnimationClip> clipes)
    {
        var caminho = $"{dir}/{Arquivo(fbxNome)}_Animator.controller";
        var existente = AssetDatabase.LoadAssetAtPath<AnimatorController>(caminho);
        if (existente != null) return existente;

        var ac = AnimatorController.CreateAnimatorControllerAtPath(caminho);
        ac.AddParameter("isMoving", AnimatorControllerParameterType.Bool);
        foreach (var t in new[] { "Attack", "Special", "Damage", "Guard", "Win", "Down", "Eat" })
            ac.AddParameter(t, AnimatorControllerParameterType.Trigger);

        var sm = ac.layers[0].stateMachine;
        AnimationClip C(string n) => clipes.FirstOrDefault(c => c.name == n);

        var idle = sm.AddState("idle", new Vector3(300, 0));
        idle.motion = C("idle") ?? C("bn02") ?? clipes[0];
        sm.defaultState = idle;

        var move = sm.AddState("move", new Vector3(300, 120));
        move.motion = C("move") ?? C("br02") ?? idle.motion;
        var ida = idle.AddTransition(move);
        ida.AddCondition(AnimatorConditionMode.If, 0, "isMoving");
        ida.hasExitTime = false; ida.duration = 0.1f;
        var volta = move.AddTransition(idle);
        volta.AddCondition(AnimatorConditionMode.IfNot, 0, "isMoving");
        volta.hasExitTime = false; volta.duration = 0.1f;

        // Ações disparadas por trigger de qualquer estado; voltam ao idle no fim (menos "down").
        void Acao(string trigger, string clipe, Vector3 pos, bool voltaAoIdle)
        {
            // Modelos de outros jogos às vezes não têm o clipe: usa um parecido.
            var clip = C(clipe) ?? (clipe == "attack02" ? C("skill") : clipe == "down" ? C("damage2") : null);
            if (clip == null) return;
            var s = sm.AddState(clipe, pos);
            s.motion = clip;
            var t = sm.AddAnyStateTransition(s);
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
            t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false;
            if (!voltaAoIdle) return;
            var fim = s.AddTransition(idle);
            fim.hasExitTime = true; fim.exitTime = 0.95f; fim.duration = 0.1f;
        }
        Acao("Attack", "attack01", new Vector3(600, -120), true);
        Acao("Special", "attack02", new Vector3(600, -40), true);
        Acao("Damage", "damage", new Vector3(600, 40), true);
        Acao("Guard", "guard", new Vector3(600, 120), true);
        Acao("Win", "win", new Vector3(600, 200), true);
        Acao("Down", "down", new Vector3(600, 280), false);
        Acao("Eat", "eat", new Vector3(600, 360), true);

        EditorUtility.SetDirty(ac);
        return ac;
    }

    // ------------------------------------------------------------ Ficha

    private static void Ficha(string nome, GameObject prefab, DigimonStage estagio)
    {
        var caminho = $"{PastaFichas}/{Arquivo(nome)}.asset";
        var ficha = AssetDatabase.LoadAssetAtPath<DigimonBase>(caminho);
        if (ficha == null)
        {
            ficha = ScriptableObject.CreateInstance<DigimonBase>();
            AssetDatabase.CreateAsset(ficha, caminho);
        }
        var atributo = Atributo(nome);
        var tipo = Tipo(nome, atributo);
        var (hp, atk, def, inte, sp, spd) = Atributos(estagio, nome);

        var so = new SerializedObject(ficha);
        so.FindProperty("name_").stringValue = nome;
        so.FindProperty("description_").stringValue = $"{nome}, Digimon {EstagioPt(estagio)} do tipo {tipo}.";
        so.FindProperty("prefab_").objectReferenceValue = prefab;
        so.FindProperty("animator_").objectReferenceValue = prefab.GetComponentInChildren<Animator>();
        so.FindProperty("stage_").enumValueIndex = (int)estagio;
        so.FindProperty("atribute_").enumValueIndex = (int)atributo;
        so.FindProperty("type_").enumValueIndex = (int)tipo;
        so.FindProperty("HP").intValue = hp;
        so.FindProperty("ATK").intValue = atk;
        so.FindProperty("DEF").intValue = def;
        so.FindProperty("INT").intValue = inte;
        so.FindProperty("SP").intValue = sp;
        so.FindProperty("SPD").intValue = spd;
        var golpes = so.FindProperty("learnableMoves_");
        golpes.arraySize = 0;
        Golpe(golpes, GolpePorAtributo(atributo), 1);
        Golpe(golpes, "Attack Charge", 6);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ficha);
    }

    private static void Golpe(SerializedProperty lista, string nome, int nivel)
    {
        var golpe = AssetDatabase.LoadAssetAtPath<MoveBase>($"Assets/Resources/Moves/{nome}.asset");
        if (golpe == null) return;
        lista.arraySize++;
        var e = lista.GetArrayElementAtIndex(lista.arraySize - 1);
        e.FindPropertyRelative("movebase").objectReferenceValue = golpe;
        e.FindPropertyRelative("level").intValue = nivel;
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

    private static DigimonStage Estagio(string s)
    {
        switch (s.Trim())
        {
            case "Fresh": return DigimonStage.Fresh;
            case "In-Training": return DigimonStage.InTraining;
            case "Rookie": return DigimonStage.Rookie;
            case "Champion": return DigimonStage.Champion;
            case "Ultimate": return DigimonStage.Ultimate;
            case "Armor": return DigimonStage.Armor;
            default: return DigimonStage.Mega;
        }
    }

    private static string EstagioPt(DigimonStage e)
    {
        switch (e)
        {
            case DigimonStage.Fresh: return "bebê";
            case DigimonStage.InTraining: return "em treinamento";
            case DigimonStage.Rookie: return "criança";
            case DigimonStage.Champion: return "adulto";
            case DigimonStage.Ultimate: return "perfeito";
            case DigimonStage.Armor: return "armor";
            default: return "supremo";
        }
    }

    private static float Multiplicador(DigimonStage e)
    {
        switch (e)
        {
            case DigimonStage.Fresh: return 0.55f;
            case DigimonStage.InTraining: return 0.7f;
            case DigimonStage.Rookie: return 1f;
            case DigimonStage.Champion:
            case DigimonStage.Armor: return 1.3f;
            case DigimonStage.Ultimate: return 1.6f;
            default: return 1.9f;
        }
    }

    private static (int, int, int, int, int, int) Atributos(DigimonStage e, string nome)
    {
        int hp, atk, def, inte, sp, spd;
        switch (e)
        {
            case DigimonStage.Fresh: hp = 350; atk = 20; def = 20; inte = 18; sp = 20; spd = 24; break;
            case DigimonStage.InTraining: hp = 410; atk = 28; def = 28; inte = 24; sp = 25; spd = 28; break;
            case DigimonStage.Rookie: hp = 450; atk = 46; def = 36; inte = 32; sp = 35; spd = 42; break;
            case DigimonStage.Champion:
            case DigimonStage.Armor: hp = 600; atk = 68; def = 55; inte = 48; sp = 50; spd = 55; break;
            case DigimonStage.Ultimate: hp = 800; atk = 92; def = 74; inte = 66; sp = 70; spd = 70; break;
            default: hp = 1000; atk = 120; def = 95; inte = 88; sp = 90; spd = 85; break;
        }
        // Variação fixa por nome (±10%), para cada um ter seu jeito.
        var h = (uint)nome.GetHashCode();
        int V(int v, int k) => Mathf.RoundToInt(v * (0.9f + ((h >> (k * 4)) & 15) / 75f));
        return (V(hp, 0), V(atk, 1), V(def, 2), V(inte, 3), V(sp, 4), V(spd, 5));
    }

    private static readonly (DigimonAttribute a, string[] chaves)[] Elementos =
    {
        (DigimonAttribute.Dark, new[] { "black", "chaos", "shadow", "ruin", "devi", "bake", "lucemon", "beelze", "daemon", "barbamon", "lilith", "belphe", "apocaly", "millenni", "piedmon", "myotis", "kuzuha", "sorcer", "raguel", "armagedd", "diaboro", "zwart", "ogremon", "kuramon", "pagumon", "mastemon", "leviamon", "nanimon", "tsukaimon", "gazimon", "impmon", "dorumon", "dorugamon", "skull" } ),
        (DigimonAttribute.Water, new[] { "ice", "frigi", "seadra", "ikkaku", "zudo", "vikemon", "plesio", "marine", "whamon", "gekomon", "shellnume", "bukamon", "gomamon", "neptune", "aqua", "crab", "dolphin", "coral" } ),
        (DigimonAttribute.Fire, new[] { "mera", "birdra", "greymon", "growlmon", "volcanic", "flame", "phoenix", "shine", "apollo", "kyubi", "tyranno", "coredramon", "agumon", "wargrey", "blitzgrey", "guilmon", "gallantmon", "flamedra", "salamon", "rust" } ),
        (DigimonAttribute.Plant, new[] { "togemon", "lillymon", "rosemon", "woodmon", "vegie", "sunflow", "lotus", "jurei", "blossom", "tanemon", "palmon", "yokomon", "floramon", "mushroom", "cherry" } ),
        (DigimonAttribute.Electric, new[] { "kabuteri", "andromon", "guardromon", "clockmon", "chaosdromon", "machinedra", "metallicdra", "mekanori", "hagurumon", "metalgrey", "metalgaru", "metalsea", "megadramon", "gigadramon", "locomon", "granloco", "volt", "thunder", "kuwagamon", "okuwamon", "stingmon", "waspmon", "tentomon", "motimon" } ),
        (DigimonAttribute.Wind, new[] { "aquila", "peckmon", "garuda", "hippogri", "ravemon", "silphy", "valkyri", "biyomon", "zubaeager", "falcomon", "hawkmon", "patamon", "angemon", "wind" } ),
        (DigimonAttribute.Light, new[] { "seraphi", "ophani", "magnamon", "gatomon", "angewo", "holy", "sakuya", "omnimon", "alphamon", "jesmon", "crusader", "kentauros", "magnadra", "slayer", "ulforce", "imperialdra", "dynasmon", "leopardmon", "craniamon", "gankoo", "sleipmon", "titamon", "tokomon", "nyaromon", "susanoo", "lucemon chaos" } ),
        (DigimonAttribute.Earth, new[] { "gargomon", "rapidmon", "leomon", "golem", "starmon", "monochro", "gaogamon", "garurumon", "numemon", "sukamon", "terrier", "gomamon", "cyclone", "grapleo", "vorvomon", "lavorvo", "jazard", "ginryu", "ryudamon", "hisyaryu", "renamon", "taomon", "reppamon", "gabumon" } ),
    };

    private static DigimonAttribute Atributo(string nome)
    {
        var n = nome.ToLowerInvariant().Replace(" ", "");
        foreach (var (a, chaves) in Elementos)
            if (chaves.Any(k => n.Contains(k.Replace(" ", "")))) return a;
        return DigimonAttribute.Neutral;
    }

    private static readonly string[] Vacinas =
    {
        "greymon", "garurumon", "angemon", "leomon", "gatomon", "ikkakumon", "birdramon", "kabuterimon", "togemon", "frigimon",
        "omnimon", "seraphimon", "ophanimon", "magnamon", "rapidmon", "gargomon", "magnadramon", "slayerdramon", "ulforce",
        "dynasmon", "craniamon", "sleipmon", "crusader", "jesmon", "gallantmon", "sakuyamon", "imperialdramon", "aquilamon", "zudomon", "vikemon"
    };

    private static DigimonType Tipo(string nome, DigimonAttribute a)
    {
        var n = nome.ToLowerInvariant().Replace(" ", "");
        if (a == DigimonAttribute.Dark) return DigimonType.Virus;
        if (n.StartsWith("black") || n.StartsWith("chaos")) return DigimonType.Virus;
        if (a == DigimonAttribute.Light || Vacinas.Any(v => n.Contains(v))) return DigimonType.Vaccine;
        return DigimonType.Data;
    }

    // ------------------------------------------------------------ Medidas

    private static float Altura(GameObject prefab, out float escala)
    {
        escala = 1f;
        if (prefab == null) return 1f;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            escala = go.transform.localScale.x;
            return AlturaMalhas(go, out _) is var h && h > 0 ? h : 1f;
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    /// <summary>Altura das malhas em coordenadas do mundo; <paramref name="local"/> recebe os limites no espaço do objeto raiz.</summary>
    private static float AlturaMalhas(GameObject go, out Bounds local)
    {
        var ok = false;
        var mundo = new Bounds();
        local = new Bounds();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            Bounds b;
            if (r is SkinnedMeshRenderer sk && sk.sharedMesh != null)
            {
                // Bounds do skinned mesh na pose atual.
                var malha = new Mesh();
                sk.BakeMesh(malha, true);
                b = Transformar(malha.bounds, sk.transform.localToWorldMatrix);
                UnityEngine.Object.DestroyImmediate(malha);
            }
            else b = r.bounds;
            if (!ok) { mundo = b; ok = true; }
            else mundo.Encapsulate(b);
        }
        if (!ok) return 0f;
        local = Transformar(mundo, go.transform.worldToLocalMatrix);
        return mundo.size.y;
    }

    private static Bounds Transformar(Bounds b, Matrix4x4 m)
    {
        var r = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
        for (var i = 0; i < 8; i++)
            r.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        return r;
    }

    // ------------------------------------------------------------ Folhas de conferência

    /// <summary>Fotos de todos os prefabs, 10 por linha, para conferir de uma vez (só com placa de vídeo).</summary>
    private static void Folhas(List<(string nome, GameObject prefab)> lista, string saida)
    {
        const int lado = 192, colunas = 10, porFolha = 60;
        var nomes = new StringBuilder();
        for (var f = 0; f * porFolha < lista.Count; f++)
        {
            var parte = lista.Skip(f * porFolha).Take(porFolha).ToList();
            var linhas = Mathf.CeilToInt(parte.Count / (float)colunas);
            var folha = new Texture2D(colunas * lado, linhas * lado, TextureFormat.RGBA32, false);
            folha.SetPixels32(Enumerable.Repeat(new Color32(40, 40, 48, 255), folha.width * folha.height).ToArray());
            nomes.AppendLine($"--- folha {f + 1}");
            for (var i = 0; i < parte.Count; i++)
            {
                nomes.AppendLine($"{i / colunas + 1}.{i % colunas + 1} {parte[i].nome}");
                var foto = Foto(parte[i].prefab, lado);
                if (foto == null) continue;
                var x = (i % colunas) * lado;
                var y = folha.height - (i / colunas + 1) * lado;
                folha.SetPixels(x, y, lado, lado, foto.GetPixels());
                UnityEngine.Object.DestroyImmediate(foto);
            }
            folha.Apply();
            File.WriteAllBytes(Path.Combine(saida, $"folha_{f + 1}.png"), folha.EncodeToPNG());
        }
        File.WriteAllText(Path.Combine(saida, "folhas.txt"), nomes.ToString());
    }

    private static Texture2D Foto(GameObject prefab, int lado)
    {
        var suporte = new GameObject("Suporte");
        suporte.transform.position = new Vector3(0, -5000, 0);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, suporte.transform);
        go.transform.localPosition = Vector3.zero;
        try
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            var anim = go.GetComponent<Animator>();
            var ctrl = anim != null ? anim.runtimeAnimatorController : null;
            var idle = ctrl != null ? ctrl.animationClips.FirstOrDefault(c => c.name == "idle") : null;
            if (idle != null) idle.SampleAnimation(go, 0f);
            suporte.transform.rotation = Quaternion.Euler(0, 200f, 0);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;

            var h = AlturaMalhas(go, out _);
            var b = new Bounds();
            var ok = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!ok) { b = r.bounds; ok = true; } else b.Encapsulate(r.bounds);
            }
            if (!ok) return null;
            var tam = Mathf.Max(b.size.y, b.size.x, b.size.z * 0.6f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << 31;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.16f, 0.19f, 1f);
            cam.fieldOfView = 26f;
            var dist = tam * 0.62f / Mathf.Tan(13f * Mathf.Deg2Rad);
            cam.nearClipPlane = dist * 0.01f;
            cam.farClipPlane = dist * 10f;
            cam.transform.position = b.center + new Vector3(0, tam * 0.08f, -dist);
            cam.transform.LookAt(b.center);
            var luz = new GameObject("Luz").AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.cullingMask = 1 << 31;
            luz.transform.rotation = Quaternion.Euler(40, 20, 0);
            var rt = RenderTexture.GetTemporary(lado, lado, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, lado, lado), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(cam.gameObject);
            UnityEngine.Object.DestroyImmediate(luz.gameObject);
            return tex;
        }
        finally { UnityEngine.Object.DestroyImmediate(suporte); }
    }
}
