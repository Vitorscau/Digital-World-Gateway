using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Teste automático da cena de cuidado (roda em batchmode): entra em Play, coloca o Digimon,
/// alimenta, faz carinho, limpa, abre o treino e põe para dormir, registrando erros e tirando fotos.
/// O save do jogador é guardado antes e devolvido no fim.
/// </summary>
[InitializeOnLoad]
public static class TesteCuidado
{
    private const string Chave = "TesteCuidado.Ativo";
    private static readonly StringBuilder Log = new StringBuilder();
    private static int _passo;
    private static int _erros;

    static TesteCuidado()
    {
        if (!SessionState.GetBool(Chave, false)) return;
        Application.logMessageReceived += (msg, pilha, tipo) =>
        {
            if (tipo == LogType.Exception || tipo == LogType.Error)
            {
                if (msg.Contains("Lightship") || msg.Contains("MonoBehaviourEventDispatcher") || pilha.Contains("Niantic")) return;
                _erros++;
                File.AppendAllText(Saida("erros.txt"), msg + "\n" + pilha + "\n");
            }
        };
        EditorApplication.update += Passo;
    }

    private static string Saida(string arquivo)
    {
        var dir = Environment.GetEnvironmentVariable("DIGIMON_LINKS_SAIDA") ?? Path.GetTempPath();
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, arquivo);
    }

    private static string Perfil => Path.Combine(Application.persistentDataPath, "perfil.json");

    public static void ExecutarBatch()
    {
        if (File.Exists(Perfil)) File.Copy(Perfil, Perfil + ".bak_teste", true);
        // Começa com fome, triste e sujo para ver a IA reagir.
        PerfilJogador.Resetar();
        var p = PerfilJogador.Atual;
        var d = p.Parceiro;
        d.fome = 20f; d.humor = 40f; d.energia = 60f; d.cocos = 2;
        d.ultimoCuidado = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        p.Salvar();
        File.WriteAllText(Saida("erros.txt"), "");
        SessionState.SetBool(Chave, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Care_Scene.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Chamar(object alvo, string metodo)
    {
        var m = alvo.GetType().GetMethod(metodo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        m?.Invoke(alvo, null);
        Registrar($"chamou {metodo}");
    }

    private static void Registrar(string s)
    {
        var d = PerfilJogador.Atual.Parceiro;
        File.AppendAllText(Saida("teste_cuidado.txt"),
            $"[{Time.time:0.0}s] {s} | fome {d.fome:0} energia {d.energia:0} humor {d.humor:0} amizade {d.amizade} cocos {d.cocos} meat {PerfilJogador.Atual.Quantidade(Itens.Meat)}\n");
    }

    private static void Foto(string nome)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var rt = new RenderTexture(540, 960, 24);
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
        RenderTexture.active = rt;
        var t = new Texture2D(540, 960, TextureFormat.RGB24, false);
        t.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
        t.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Saida(nome), t.EncodeToPNG());
        rt.Release();
    }

    private static void Passo()
    {
        if (!EditorApplication.isPlaying) return;
        var cena = UnityEngine.Object.FindFirstObjectByType<CenaCuidado>();
        var pet = UnityEngine.Object.FindFirstObjectByType<DigimonVirtual>();
        var t = Time.time;
        switch (_passo)
        {
            case 0 when t > 4f:
                File.WriteAllText(Saida("teste_cuidado.txt"), "");
                Registrar(pet != null ? $"Digimon colocado: {pet.name} altura {pet.Altura:0.00} m em {pet.transform.position}" : "ERRO: Digimon não apareceu");
                Foto("cuidado_1.png");
                _passo++; break;
            case 1 when t > 6f:
                Chamar(cena, "Alimentar"); _passo++; break;
            case 2 when t > 9f:
                if (pet != null) { pet.Carinho(); pet.Carinho(); }
                Registrar("2 carinhos");
                Foto("cuidado_2.png");
                _passo++; break;
            case 3 when t > 11f:
                Chamar(cena, "Limpar"); _passo++; break;
            case 4 when t > 13f:
                Chamar(cena, "Treinar");
                _passo++; break;
            case 5 when t > 15f:
                var treino = UnityEngine.Object.FindFirstObjectByType<TreinoMinigame>();
                Registrar(treino != null ? "treino aberto" : "treino NÃO abriu");
                if (treino != null) Chamar(treino, "Fechar");
                _passo++; break;
            case 6 when t > 16f:
                Chamar(cena, "DormirOuAcordar"); _passo++; break;
            case 7 when t > 19f:
                Registrar(pet != null ? $"dormindo: {pet.Dormindo}" : "sem pet");
                Foto("cuidado_3.png");
                Chamar(cena, "DormirOuAcordar");
                _passo++; break;
            case 8 when t > 21f:
                Registrar($"fim. erros: {_erros}");
                _passo++;
                EditorApplication.update -= Passo;
                SessionState.SetBool(Chave, false);
                EditorApplication.ExitPlaymode();
                EditorApplication.delayCall += () =>
                {
                    if (File.Exists(Perfil + ".bak_teste")) { File.Copy(Perfil + ".bak_teste", Perfil, true); File.Delete(Perfil + ".bak_teste"); }
                    EditorApplication.Exit(0);
                };
                break;
        }
    }
}
