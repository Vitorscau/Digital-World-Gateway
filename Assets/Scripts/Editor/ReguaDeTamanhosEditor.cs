using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Régua de tamanhos: monta o prefab Assets/Prefabs/ReguaDeTamanhos.prefab com um grupo por estágio
/// (nível de digivolução); no grupo, todos ficam sobrepostos no mesmo ponto, com a lista dos nomes ao lado.
/// Selecione cada um pela Hierarchy (Estagio_Rookie > Agumon...).
/// Ajuste cada um no Inspector (campo Ajuste) ou com a ferramenta de escala e clique em "Salvar ajustes":
/// os valores vão para Resources/Digimons/ajuste_tamanho.txt, que o jogo usa no mapa, na batalha e na Care.
/// Menu: Tools > Digimon Nexus > Régua de tamanhos.
/// </summary>
public static class ReguaDeTamanhosMenu
{
    public const string CaminhoPrefab = "Assets/Prefabs/ReguaDeTamanhos.prefab";
    public const string CaminhoAjustes = "Assets/Resources/Digimons/ajuste_tamanho.txt";

    private static readonly DigimonStage[] Ordem =
    {
        DigimonStage.Fresh, DigimonStage.InTraining, DigimonStage.Rookie, DigimonStage.Champion,
        DigimonStage.Armor, DigimonStage.Ultimate, DigimonStage.Mega, DigimonStage.Hybrid,
        DigimonStage.BurstMode, DigimonStage.Fusion, DigimonStage.SuperUltimate
    };

    [MenuItem("Tools/Digimon Nexus/Régua de tamanhos")]
    public static void Montar()
    {
        // Sai do modo de edição de prefab (a régua pode estar aberta) antes de montar de novo.
        UnityEditor.SceneManagement.StageUtility.GoToMainStage();
        var ajustes = LerAjustes(out _);
        var fichas = Resources.LoadAll<DigimonBase>("Digimons").Where(f => f.Prefab != null)
            .OrderBy(f => System.Array.IndexOf(Ordem, f.Stage)).ThenBy(f => f.name).ToList();
        if (fichas.Count == 0)
        {
            EditorUtility.DisplayDialog("Régua de tamanhos", "Nenhuma ficha com prefab em Resources/Digimons.", "OK");
            return;
        }

        var raiz = new GameObject("ReguaDeTamanhos");
        raiz.AddComponent<ReguaDeTamanhos>();
        var fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        try
        {
            // 1) Cria todos e mede (no tamanho do prefab, sem ajuste).
            var itens = new List<(DigimonBase ficha, GameObject caixa, GameObject modelo, Bounds b)>();
            for (var i = 0; i < fichas.Count; i++)
            {
                var f = fichas[i];
                EditorUtility.DisplayProgressBar("Régua de tamanhos", f.name, (float)i / fichas.Count);
                var caixa = new GameObject(f.name);
                caixa.transform.SetParent(raiz.transform, false);
                var modelo = (GameObject)PrefabUtility.InstantiatePrefab(f.Prefab, caixa.transform);
                modelo.transform.localPosition = Vector3.zero;
                modelo.transform.localRotation = Quaternion.identity;
                // O anel do mapa (Ripple) atrapalha a comparação.
                foreach (var ps in modelo.GetComponentsInChildren<ParticleSystem>(true))
                    if (ps.name.StartsWith("Ripple")) ps.gameObject.SetActive(false);
                itens.Add((f, caixa, modelo, Limites(modelo)));
            }

            // 2) Um grupo por estágio (nível de digivolução): todos do grupo sobrepostos no mesmo ponto, para ver
            //    quem sai do tamanho dos outros. Os grupos ficam lado a lado em X, cada um com o nome do estágio em
            //    cima e a lista dos nomes ao lado (os nomes mostram o ajuste, ex.: "Patamon x0.7").
            var x = 0f;
            foreach (var grupo in itens.GroupBy(i => i.ficha.Stage))
            {
                var lista = grupo.ToList();
                var alturaGrupo = Mathf.Max(0.01f, lista.Max(i => i.b.size.y));
                var larguraMax = Mathf.Max(0.01f, lista.Max(i => Mathf.Max(i.b.size.x, i.b.size.z)));
                var linha = alturaGrupo * 0.045f;
                var larguraLista = linha * 9f; // espaço para os nomes ao lado

                var pasta = new GameObject("Estagio_" + grupo.Key);
                pasta.transform.SetParent(raiz.transform, false);
                pasta.transform.localPosition = new Vector3(x + larguraMax / 2f, 0f, 0f);

                var titulo = Rotulo(pasta.transform, fonte, $"{grupo.Key} ({lista.Count})", alturaGrupo * 0.09f);
                titulo.transform.localPosition = new Vector3(0f, alturaGrupo * 1.08f, 0f);

                for (var n = 0; n < lista.Count; n++)
                {
                    var (ficha, caixa, modelo, b) = lista[n];
                    caixa.transform.SetParent(pasta.transform, false);
                    caixa.transform.localPosition = Vector3.zero;
                    // Pés no chão e centralizado no ponto do grupo.
                    var bm = Limites(modelo);
                    var centro = caixa.transform.position;
                    var ajusteCentro = new Vector3(centro.x - bm.center.x, centro.y - bm.min.y, centro.z - bm.center.z);
                    if (DigimonVisual.Finito(bm) && bm.size.y > 0f) modelo.transform.position += ajusteCentro;
                    else Debug.LogWarning($"[Régua de tamanhos] {ficha.name}: modelo sem limites válidos (malha com NaN?). Confira o prefab.");

                    var rotulo = Rotulo(caixa.transform, fonte, ficha.name, linha * 0.8f);
                    rotulo.anchor = TextAnchor.UpperLeft;
                    rotulo.alignment = TextAlignment.Left;
                    rotulo.transform.localPosition = new Vector3(larguraMax / 2f + linha, alturaGrupo - n * linha * 1.15f, 0f);

                    var ajuste = caixa.AddComponent<AjusteTamanhoDigimon>();
                    ajuste.especie = ficha.name;
                    ajuste.modelo = modelo.transform;
                    ajuste.escalaBase = modelo.transform.localScale;
                    ajuste.rotulo = rotulo;
                    ajuste.ajuste = ajustes.TryGetValue(ficha.name, out var a) ? a : 1f;
                    ajuste.Aplicar();
                }
                x += larguraMax + larguraLista + larguraMax * 0.6f;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CaminhoPrefab));
            var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, CaminhoPrefab);
            AssetDatabase.OpenAsset(prefab); // abre no modo de edição de prefab
            Debug.Log($"[Régua de tamanhos] {itens.Count} Digimon em {CaminhoPrefab}. Ajuste e clique em \"Salvar ajustes\" na raiz.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Object.DestroyImmediate(raiz);
        }
    }

    private static TextMesh Rotulo(Transform pai, Font fonte, string texto, float altura)
    {
        var go = new GameObject("Nome");
        go.transform.SetParent(pai, false);
        var tm = go.AddComponent<TextMesh>();
        tm.font = fonte;
        go.GetComponent<MeshRenderer>().sharedMaterial = fonte.material;
        tm.text = texto;
        tm.fontSize = 64;
        tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.LowerCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        // Ajusta a escala para a linha de texto ter a altura pedida.
        var h = go.GetComponent<MeshRenderer>().bounds.size.y;
        if (h > 0.0001f) go.transform.localScale = Vector3.one * (altura / h);
        return tm;
    }

    private static Bounds Limites(GameObject go)
    {
        var ok = false;
        var b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!DigimonVisual.Finito(r.bounds)) continue; // malha com valores inválidos (NaN)
            if (!ok) { b = r.bounds; ok = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // ------------------------------------------------------------ Arquivo de ajustes

    public static Dictionary<string, float> LerAjustes(out List<string> cabecalho)
    {
        var d = new Dictionary<string, float>();
        cabecalho = new List<string>();
        if (!File.Exists(CaminhoAjustes)) return d;
        foreach (var linha in File.ReadAllLines(CaminhoAjustes))
        {
            var l = linha.Trim();
            if (l.StartsWith("#")) { cabecalho.Add(linha); continue; }
            var i = l.IndexOf('=');
            if (i <= 0) continue;
            if (float.TryParse(l.Substring(i + 1).Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var a))
                d[l.Substring(0, i).Trim()] = a;
        }
        return d;
    }

    public static void SalvarAjustes(IEnumerable<AjusteTamanhoDigimon> itens)
    {
        var d = LerAjustes(out var cabecalho);
        var mudou = 0;
        foreach (var it in itens)
        {
            if (string.IsNullOrEmpty(it.especie)) continue;
            var v = Mathf.Round(it.ajuste * 100f) / 100f;
            if (!d.TryGetValue(it.especie, out var antes) || !Mathf.Approximately(antes, v)) mudou++;
            d[it.especie] = v;
        }
        var linhas = new List<string>(cabecalho) { "" };
        linhas.AddRange(d.OrderBy(kv => kv.Key, System.StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"{kv.Key}={kv.Value.ToString("0.##", CultureInfo.InvariantCulture)}"));
        File.WriteAllText(CaminhoAjustes, string.Join("\n", linhas) + "\n");
        AssetDatabase.ImportAsset(CaminhoAjustes);
        Debug.Log($"[Régua de tamanhos] Ajustes salvos em {CaminhoAjustes} ({mudou} mudaram).");
    }
}

[CustomEditor(typeof(ReguaDeTamanhos))]
public class ReguaDeTamanhosEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Ajuste o tamanho de cada Digimon: selecione-o e mude o campo \"Ajuste\" (ou use a ferramenta de escala no modelo).\n" +
            "1 = tamanho normal · 1.5 = 50% maior · 0.5 = metade.\n" +
            "Depois clique em Salvar ajustes: vale no mapa, na batalha e na Care.", MessageType.Info);

        var itens = ((ReguaDeTamanhos)target).GetComponentsInChildren<AjusteTamanhoDigimon>(true);
        EditorGUILayout.LabelField("Digimon na régua", itens.Length.ToString());
        EditorGUILayout.LabelField("Com ajuste diferente de 1", itens.Count(i => !Mathf.Approximately(i.ajuste, 1f)).ToString());

        EditorGUILayout.Space();
        if (GUILayout.Button("Salvar ajustes", GUILayout.Height(32))) ReguaDeTamanhosMenu.SalvarAjustes(itens);
        if (GUILayout.Button("Recarregar do arquivo"))
        {
            var d = ReguaDeTamanhosMenu.LerAjustes(out _);
            foreach (var it in itens)
            {
                Undo.RecordObject(it, "Recarregar ajustes");
                it.ajuste = d.TryGetValue(it.especie, out var a) ? a : 1f;
                it.Aplicar();
            }
        }
        if (GUILayout.Button("Voltar todos para 1") &&
            EditorUtility.DisplayDialog("Régua de tamanhos", "Voltar todos os ajustes para 1? (só salva no arquivo se você clicar em Salvar ajustes)", "Voltar", "Cancelar"))
        {
            foreach (var it in itens)
            {
                Undo.RecordObject(it, "Voltar ajustes para 1");
                it.ajuste = 1f;
                it.Aplicar();
            }
        }
        if (GUILayout.Button("Montar a régua de novo (todos os Digimon)")) ReguaDeTamanhosMenu.Montar();
    }
}
