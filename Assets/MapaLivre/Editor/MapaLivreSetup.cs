using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Niantic.Lightship.Maps;
using Niantic.Lightship.Maps.Core;
using Niantic.Lightship.Maps.SampleAssets.Cameras.OrbitCamera;
using Niantic.Lightship.Maps.Vector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

/// <summary>Configuração e teste do MapaLivre (substituto do Lightship Maps).</summary>
public static class MapaLivreSetup
{
    private const string MaterialPath = "Assets/MapaLivre/Resources/MapaLivre/MapTile.mat";
    private const string ThemeFolder = "Assets/MapaLivre/Resources/MapaLivre";
    private const string MaterialFolder = "Assets/MapaLivre/Temas";
    private const string TexFolder = "Assets/Materials/Textures/";

    // ---------------------------------------------------------------- Materiais e temas

    [MenuItem("Tools/MapaLivre/Criar material dos tiles")]
    public static void EnsureMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
        var mat = new Material(UnlitShader()) { name = "MapTile", enableInstancing = true };
        AssetDatabase.CreateAsset(mat, MaterialPath);
        AssetDatabase.SaveAssets();
    }

    private static Shader UnlitShader() => Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");

    private static Material Mat(string folder, string name, Color color, string texture = null)
    {
        var dir = $"{MaterialFolder}/{folder}";
        Directory.CreateDirectory(dir);
        var path = $"{dir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(UnlitShader()) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        Texture2D tex = null;
        if (texture != null)
        {
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + texture + ".png");
            if (tex == null) Debug.LogWarning($"[MapaLivre] Textura não encontrada: {texture}");
        }
        m.SetTexture("_BaseMap", tex);
        m.SetTextureScale("_BaseMap", Vector2.one);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static MapaLivreTheme Theme(string name)
    {
        Directory.CreateDirectory(ThemeFolder);
        var path = $"{ThemeFolder}/{name}.asset";
        var t = AssetDatabase.LoadAssetAtPath<MapaLivreTheme>(path);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<MapaLivreTheme>();
            AssetDatabase.CreateAsset(t, path);
        }
        return t;
    }

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

    [MenuItem("Tools/MapaLivre/Criar temas do mapa")]
    public static void CreateThemes()
    {
        // Tema "Original": a paleta do tema padrão do Lightship Maps que o jogo usava
        // (cores tiradas de um print do jogo): chão verde com circuito clarinho, prédios lilás,
        // ruas verde-petróleo com laterais amarelo-claras, água azul.
        var circuito = CircuitTexture.Create($"{MaterialFolder}/Original/Circuito.png",
            Hex("#78F898"), Color.Lerp(Hex("#78F898"), Color.black, 0.02f)); // linhas 2% mais escuras que o verde
        var o = Theme("TemaOriginal");
        o.Ground = Mat("Original", "Chao", Color.white);
        o.Ground.SetTexture("_BaseMap", circuito);
        o.Special = o.Ground; // mesmo chão com circuito
        o.Park = Mat("Original", "Parque", Hex("#08A888"));
        o.Forest = Mat("Original", "Mata", Hex("#08A888"));
        o.Sand = Mat("Original", "Areia", Hex("#F0F8C0"));
        o.Water = Mat("Original", "Agua", Hex("#1888D0"));
        o.MajorRoad = Mat("Original", "RuaPrincipal", Hex("#58A090"));
        o.MinorRoad = Mat("Original", "Rua", Hex("#58A090"));
        o.Path = Mat("Original", "Trilha", Hex("#F0F8C0"));
        o.RoadOutline = Mat("Original", "BordaRua", Hex("#F0F8C0"));
        o.RoadOutlineWidth = 1.6f;
        o.MajorRoadWidth = 12f;
        o.MinorRoadWidth = 6f;
        o.PathWidth = 2f;
        o.ShowBuildings = true;
        o.BuildingTop = Mat("Original", "PredioTopo", Hex("#D8D0F8"));
        o.BuildingWall = Mat("Original", "PredioParede", Hex("#B8B0E8"));
        o.BuildingHeightScale = 0f;
        o.BuildingMinHeight = 0.3f;
        o.BuildingMaxHeight = 0.3f;
        o.TextureRepeatMeters = 90f; // circuito 50% maior que os 60 m originais
        o.SkyColor = Hex("#3D6878");
        o.FogStart = 380f;
        o.FogEnd = 700f;
        EditorUtility.SetDirty(o);

        // Tema "Pixel": as texturas pixel-art da pasta Materials/Textures.
        var a = Theme("TemaPixel");
        a.Ground = Mat("Pixel", "Chao", Color.white, "farm_base_tex_grass_05");
        a.Park = Mat("Pixel", "Parque", Color.white, "farm_base_tex_flower_02");
        a.Forest = Mat("Pixel", "Mata", Color.white, "farm_base_tex_grass_06");
        a.Special = Mat("Pixel", "Especial", Color.white, "farm_base_tex_grass_04");
        a.Sand = Mat("Pixel", "Areia", Color.white, "farm_base_tex_sand_02");
        a.Water = Mat("Pixel", "Agua", Color.white, "farm_base_tex_water_02");
        a.MajorRoad = Mat("Pixel", "RuaPrincipal", Color.white, "farm_base_tex_sand_04");
        a.MinorRoad = Mat("Pixel", "Rua", Color.white, "farm_base_tex_sand_03");
        a.Path = Mat("Pixel", "Trilha", Color.white, "farm_base_tex_sand_07");
        a.BuildingTop = Mat("Pixel", "PredioTopo", Hex("#E9DCC0"));
        a.BuildingWall = Mat("Pixel", "PredioParede", Hex("#BFA987"));
        a.BuildingHeightScale = 0.12f;
        a.BuildingMaxHeight = 3f;
        a.BuildingMinHeight = 1f;
        a.TextureRepeatMeters = 12f;
        a.SkyColor = Hex("#9ED8F2");
        EditorUtility.SetDirty(a);

        // Tema "Pokémon GO": cores chapadas, ruas claras, parques mais escuros.
        var p = Theme("TemaPokemonGO");
        p.Ground = Mat("PokemonGO", "Chao", Hex("#8FD68A"));
        p.Park = Mat("PokemonGO", "Parque", Hex("#5DBE63"));
        p.Forest = Mat("PokemonGO", "Mata", Hex("#4AA857"));
        p.Special = Mat("PokemonGO", "Especial", Hex("#A9DE8F"));
        p.Sand = Mat("PokemonGO", "Areia", Hex("#F1E3A6"));
        p.Water = Mat("PokemonGO", "Agua", Hex("#4FC3F0"));
        p.MajorRoad = Mat("PokemonGO", "RuaPrincipal", Hex("#F6F4C8"));
        p.MinorRoad = Mat("PokemonGO", "Rua", Hex("#D9F2EC"));
        p.Path = Mat("PokemonGO", "Trilha", Hex("#C9E8B5"));
        p.BuildingTop = Mat("PokemonGO", "PredioTopo", Hex("#D5EEDD"));
        p.BuildingWall = Mat("PokemonGO", "PredioParede", Hex("#A5CFB4"));
        p.ShowBuildings = false; // no Pokémon GO os quarteirões são lisos
        p.BuildingHeightScale = 0.15f;
        p.BuildingMaxHeight = 6f;
        p.SkyColor = Hex("#8FD3F5");
        EditorUtility.SetDirty(p);

        AssetDatabase.SaveAssets();
        Debug.Log("[MapaLivre] Temas criados: TemaOriginal, TemaPixel e TemaPokemonGO");
    }

    // ---------------------------------------------------------------- Prévia

    [MenuItem("Tools/MapaLivre/Gerar prévias da Map_Scene")]
    public static void PreviewMenu() => PreviewAll(Path.Combine(Application.dataPath, "../Logs"));

    /// <summary>Linha de comando: -executeMethod MapaLivreSetup.BatchPreview -previewOut pasta</summary>
    public static void BatchPreview()
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, "-previewOut");
        var output = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.dataPath, "../Logs");
        try { PreviewAll(output); }
        catch (Exception e) { Debug.LogError("[MapaLivre] " + e); EditorApplication.Exit(1); }
        EditorApplication.Exit(0);
    }

    public static void PreviewAll(string folder)
    {
        EnsureMaterial();
        CreateThemes();
        Directory.CreateDirectory(folder);
        Preview(Path.Combine(folder, "mapa_original_perto.png"), "TemaOriginal", 0.2f);
        Preview(Path.Combine(folder, "mapa_original_longe.png"), "TemaOriginal", 1f);
        Preview(Path.Combine(folder, "mapa_pixel_longe.png"), "TemaPixel", 1f);
        Preview(Path.Combine(folder, "mapa_pokemongo_perto.png"), "TemaPokemonGO", 0.2f);
        Preview(Path.Combine(folder, "mapa_pokemongo_longe.png"), "TemaPokemonGO", 1f);
    }

    private static readonly Dictionary<string, byte[]> Downloads = new Dictionary<string, byte[]>();

    private static byte[] Download(string url)
    {
        if (Downloads.TryGetValue(url, out var cached)) return cached;
        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("User-Agent", "DigimonNexus-Preview/1.0 (Unity)");
        var op = req.SendWebRequest();
        var start = DateTime.Now;
        while (!op.isDone && (DateTime.Now - start).TotalSeconds < 30) System.Threading.Thread.Sleep(20);
        if (req.result != UnityWebRequest.Result.Success) throw new Exception($"{url}: {req.error}");
        return Downloads[url] = req.downloadHandler.data;
    }

    private static void Preview(string output, string themeName, float zoom)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Map_Scene.unity", OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (n > 0) Debug.LogWarning($"[MapaLivre] Script faltando ({n}) em: {GetPath(t)}");
        }

        var view = Object.FindFirstObjectByType<LightshipMapView>();
        var orbit = Object.FindFirstObjectByType<OrbitCameraController>();
        var mgr = view.MapManager;
        var theme = AssetDatabase.LoadAssetAtPath<MapaLivreTheme>($"{ThemeFolder}/{themeName}.asset");

        // Mapa vetorial (download síncrono, só para a prévia).
        var json = System.Text.Encoding.UTF8.GetString(Download(mgr.VectorTileJsonUrl));
        var template = Regex.Match(json, "\"tiles\"\\s*:\\s*\\[\\s*\"([^\"]+)\"").Groups[1].Value;
        var z = VectorMapRenderer.Zoom;
        var tileMerc = WebMercator.TileSizeMerc(z);
        var radius = mgr.VectorLoadRadiusMeters / view.MetersPerMerc;
        int x0 = (int)Math.Floor((view.CenterMercX - radius + WebMercator.HalfCircumference) / tileMerc);
        int x1 = (int)Math.Floor((view.CenterMercX + radius + WebMercator.HalfCircumference) / tileMerc);
        int y0 = (int)Math.Floor((WebMercator.HalfCircumference - (view.CenterMercY + radius)) / tileMerc);
        int y1 = (int)Math.Floor((WebMercator.HalfCircumference - (view.CenterMercY - radius)) / tileMerc);
        var build = VectorMapRenderer.CreateBuildSettings(theme);
        var k = (float)(view.MetersPerMerc * view.UnitsPerMeter);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int tris = 0;
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
        {
            var bytes = VectorMapRenderer.Decompress(Download(template.Replace("{z}", z.ToString()).Replace("{x}", x.ToString()).Replace("{y}", y.ToString())));
            var west = x * tileMerc - WebMercator.HalfCircumference;
            var north = WebMercator.HalfCircumference - y * tileMerc;
            var lat = Math.Cos(WebMercator.ToLat(north - tileMerc / 2) * Math.PI / 180.0);
            var meshes = VectorTileBuilder.Build(bytes, west, north, tileMerc, lat, build);
            tris += meshes.Values.Sum(m => m.Triangles.Count / 3);
            var go = new GameObject($"VectorTile_{x}_{y}");
            go.transform.position = view.MercToScene(west, north);
            go.transform.localScale = new Vector3(k, view.UnitsPerMeter, k);
            VectorMapRenderer.CreateMeshes(go.transform, meshes, theme);
        }
        Debug.Log($"[MapaLivre] {themeName}: tiles {(x1 - x0 + 1) * (y1 - y0 + 1)}, {tris} triângulos, {watch.ElapsedMilliseconds} ms");

        // Jogador e Digimons selvagens (rodando os Start() na mão).
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            var typeName = mb.GetType().Name;
            if (typeName == "DigimonController" || typeName == "_DigimonGOP") Call(mb, "Start");
            if (typeName == "_DigimonGOP") Call(mb, "PlaceInstances");
        }

        // Câmera, céu e névoa.
        if (orbit != null)
        {
            typeof(OrbitCameraController).GetField("_startZoom", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(orbit, zoom);
            Call(orbit, "Awake");
            Call(orbit, "Apply", true);
        }
        var cam = orbit != null ? orbit.Camera : Camera.main;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = theme.SkyColor;
        RenderSettings.fog = theme.UseFog;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = theme.SkyColor;
        RenderSettings.fogStartDistance = theme.FogStart;
        RenderSettings.fogEndDistance = theme.FogEnd;

        var rt = new RenderTexture(720, 1280, 24);
        cam.targetTexture = rt;
        var asyncShaders = EditorSettings.asyncShaderCompilation;
        EditorSettings.asyncShaderCompilation = false;
        cam.Render();
        cam.Render();
        EditorSettings.asyncShaderCompilation = asyncShaders;
        RenderTexture.active = rt;
        var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        shot.Apply();
        File.WriteAllBytes(output, shot.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        Debug.Log($"[MapaLivre] Prévia salva em {output}");
    }

    public static string Diagnose(double lat, double lng)
    {
        var sb = new System.Text.StringBuilder();
        var json = System.Text.Encoding.UTF8.GetString(Download("https://tiles.openfreemap.org/planet"));
        var template = Regex.Match(json, "\"tiles\"\\s*:\\s*\\[\\s*\"([^\"]+)\"").Groups[1].Value;
        WebMercator.ToTile(new Niantic.Lightship.Maps.Core.Coordinates.LatLng(lat, lng), 14, out var x, out var y);
        var bytes = VectorMapRenderer.Decompress(Download(template.Replace("{z}", "14").Replace("{x}", x.ToString()).Replace("{y}", y.ToString())));
        sb.AppendLine($"tile {x},{y} bytes {bytes.Length}");
        var layers = MvtDecoder.Decode(bytes, null);
        foreach (var l in layers.Values) sb.AppendLine($"layer {l.Name} extent {l.Extent} features {l.Features.Count}");
        if (layers.TryGetValue("water", out var water))
            foreach (var f in water.Features.Take(10))
            {
                var rings = MvtDecoder.DecodeGeometry(f.Geometry);
                sb.Append($"  water type {f.Type} class {f.GetString("class")} rings {rings.Count}:");
                foreach (var r in rings.Take(6))
                {
                    double a = 0;
                    for (int i = 0, j = r.Count - 1; i < r.Count; j = i++) a += (double)r[j].x * r[i].y - (double)r[i].x * r[j].y;
                    sb.Append($" [{r.Count} pts, area {a / 2:F0}]");
                }
                sb.AppendLine();
            }
        var tileMerc = WebMercator.TileSizeMerc(14);
        var meshes = VectorTileBuilder.Build(bytes, x * tileMerc - WebMercator.HalfCircumference, WebMercator.HalfCircumference - y * tileMerc, tileMerc, 0.67, VectorMapRenderer.CreateBuildSettings(AssetDatabase.LoadAssetAtPath<MapaLivreTheme>($"{ThemeFolder}/TemaPokemonGO.asset")));
        var westT = x * tileMerc - WebMercator.HalfCircumference;
        var northT = WebMercator.HalfCircumference - y * tileMerc;
        var px = WebMercator.ToMercX(lng) - westT;
        var pz = WebMercator.ToMercY(lat) - northT;
        sb.AppendLine($"ponto no tile: x {px:F1} z {pz:F1} (tile {tileMerc:F1})");
        foreach (var kv in meshes)
        {
            var hits = 0; var tt = kv.Value.Triangles; var vs = kv.Value.Vertices;
            for (var i = 0; i + 2 < tt.Count; i += 3)
            {
                var a = vs[tt[i]]; var b2 = vs[tt[i + 1]]; var c = vs[tt[i + 2]];
                double d1 = (px - b2.x) * (a.z - b2.z) - (a.x - b2.x) * (pz - b2.z);
                double d2 = (px - c.x) * (b2.z - c.z) - (b2.x - c.x) * (pz - c.z);
                double d3 = (px - a.x) * (c.z - a.z) - (c.x - a.x) * (pz - a.z);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                if (!(neg && pos)) hits++;
            }
            if (hits > 0) sb.AppendLine($"  sob o jogador: {kv.Key} ({hits})");
        }
        foreach (var kv in meshes)
        {
            var b = new Bounds(kv.Value.Vertices.Count > 0 ? kv.Value.Vertices[0] : Vector3.zero, Vector3.zero);
            foreach (var v in kv.Value.Vertices) b.Encapsulate(v);
            double area = 0, maxTri = 0;
            var t = kv.Value.Triangles; var vv = kv.Value.Vertices;
            for (var i = 0; i + 2 < t.Count; i += 3)
            {
                var a = vv[t[i]]; var bb = vv[t[i + 1]]; var c = vv[t[i + 2]];
                var ar = System.Math.Abs((bb.x - a.x) * (c.z - a.z) - (c.x - a.x) * (bb.z - a.z)) / 2.0;
                area += ar; if (ar > maxTri) maxTri = ar;
            }
            sb.AppendLine($"cat {kv.Key}: verts {vv.Count} tris {t.Count / 3} area {area / (tileMerc * tileMerc):P1} maiorTri {maxTri / (tileMerc * tileMerc):P2}");
        }
        return sb.ToString();
    }

    private static void Call(object target, string method, params object[] args)
    {
        var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        try { m?.Invoke(target, args.Length == 0 ? null : args); }
        catch (Exception e) { Debug.LogWarning($"[MapaLivre] {target.GetType().Name}.{method}: {e.InnerException?.Message ?? e.Message}"); }
    }

    private static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;
}
