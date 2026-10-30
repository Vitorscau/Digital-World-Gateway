using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Teste do rosto 3D da HUD (roda em batchmode): abre o mapa em Play e salva 8 quadros seguidos
/// da textura do RetratoVivo numa tira, para ver falhas de imagem.
/// </summary>
[InitializeOnLoad]
public static class TesteRetrato
{
    private const string Chave = "TesteRetrato.Ativo";
    private static Texture2D _tira;
    private static int _quadro;
    private static float _inicio = -1f;

    static TesteRetrato()
    {
        if (!SessionState.GetBool(Chave, false)) return;
        EditorApplication.update += Passo;
    }

    private static string Saida(string arquivo)
    {
        var dir = Environment.GetEnvironmentVariable("DIGIMON_LINKS_SAIDA") ?? Path.GetTempPath();
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, arquivo);
    }

    public static void ExecutarBatch()
    {
        SessionState.SetBool(Chave, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Map_Scene.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Passo()
    {
        if (!EditorApplication.isPlaying) return;
        if (_inicio < 0) _inicio = Time.realtimeSinceStartup;
        if (Time.realtimeSinceStartup - _inicio < 10f) return;

        var vivo = UnityEngine.Object.FindFirstObjectByType<RetratoVivo>();
        if (vivo == null || vivo.Textura == null)
        {
            File.WriteAllText(Saida("retrato_teste.txt"), "RetratoVivo não encontrado");
            Terminar();
            return;
        }
        var rt = vivo.Textura;
        if (_tira == null) _tira = new Texture2D(rt.width * 8, rt.height, TextureFormat.RGBA32, false);
        var cam = vivo.GetComponentInChildren<Camera>();
        if (cam != null) cam.Render();
        RenderTexture.active = rt;
        _tira.ReadPixels(new Rect(0, 0, rt.width, rt.height), _quadro * rt.width, 0);
        RenderTexture.active = null;
        _quadro++;
        if (_quadro < 8) return;

        // Fundo xadrez para ver a transparência.
        var px = _tira.GetPixels();
        for (var i = 0; i < px.Length; i++)
        {
            var x = i % _tira.width; var y = i / _tira.width;
            var xadrez = ((x / 16 + y / 16) % 2 == 0) ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.6f, 0.6f, 0.6f);
            px[i] = Color.Lerp(xadrez, new Color(px[i].r, px[i].g, px[i].b), px[i].a);
            px[i].a = 1f;
        }
        _tira.SetPixels(px);
        _tira.Apply();
        File.WriteAllBytes(Saida("retrato_tira.png"), _tira.EncodeToPNG());
        File.WriteAllText(Saida("retrato_teste.txt"),
            $"ficha {vivo.Ficha?.name} rt {rt.width}x{rt.height} aa {rt.antiAliasing} pos {vivo.transform.position} cam near {cam?.nearClipPlane} far {cam?.farClipPlane} msaa {cam?.allowMSAA}");
        Terminar();
    }

    private static void Terminar()
    {
        EditorApplication.update -= Passo;
        SessionState.SetBool(Chave, false);
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(0);
    }
}
