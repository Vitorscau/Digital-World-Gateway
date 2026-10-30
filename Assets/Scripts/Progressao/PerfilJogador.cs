using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Um Digimon da coleção do jogador.</summary>
[Serializable]
public class DigimonSalvo
{
    public string id;          // identificador único na coleção
    public string especie;     // nome da ficha (DigimonBase), ex.: "Gabumon"
    public int nivel = 1;
    public int xp;
    public long capturadoEm;   // data (Unix, segundos)

    public DigimonBase Ficha => CatalogoDigimon.Buscar(especie);
}

/// <summary>Progresso de uma missão.</summary>
[Serializable]
public class ProgressoMissao
{
    public string id;
    public int progresso;
    public bool resgatada;
}

/// <summary>
/// Tudo o que o jogo precisa lembrar do jogador. Salvo como JSON no aparelho
/// (Application.persistentDataPath/perfil.json).
/// </summary>
[Serializable]
public class PerfilJogador
{
    public const int NivelMaximo = 50;
    public const int NivelMaximoDigimon = 99;

    public string nome = "Tamer";
    public int nivel = 1;
    public int xp;
    public int bits;                 // moeda
    public string parceiroId;        // Digimon que anda com o jogador
    public List<DigimonSalvo> colecao = new List<DigimonSalvo>();
    public int vitorias;
    public int derrotas;
    public double metrosAndados;
    public string diaMissoes;        // data (yyyy-MM-dd) das missões diárias atuais
    public List<ProgressoMissao> missoes = new List<ProgressoMissao>();

    // ------------------------------------------------------------ Acesso global

    private static PerfilJogador _atual;
    private static string Caminho => Path.Combine(Application.persistentDataPath, "perfil.json");

    /// <summary>Disparado quando o perfil muda (nível, XP, coleção...). Bom para atualizar a HUD.</summary>
    public static event Action Mudou;

    public static PerfilJogador Atual
    {
        get
        {
            if (_atual == null) _atual = Carregar();
            return _atual;
        }
    }

    private static PerfilJogador Carregar()
    {
        try
        {
            if (File.Exists(Caminho))
            {
                var p = JsonUtility.FromJson<PerfilJogador>(File.ReadAllText(Caminho));
                if (p != null)
                {
                    p.Corrigir();
                    return p;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Perfil] Não consegui ler o save, começando um novo: {e.Message}");
        }

        var novo = new PerfilJogador();
        novo.Corrigir();
        novo.Salvar();
        return novo;
    }

    /// <summary>Garante um parceiro inicial e dados válidos (save antigo ou novo).</summary>
    private void Corrigir()
    {
        colecao ??= new List<DigimonSalvo>();
        missoes ??= new List<ProgressoMissao>();
        colecao.RemoveAll(d => d == null || string.IsNullOrEmpty(d.especie));
        if (colecao.Count == 0) AdicionarNaColecao("Gabumon", 5);
        if (Parceiro == null) parceiroId = colecao[0].id;
    }

    public void Salvar()
    {
        try
        {
            File.WriteAllText(Caminho, JsonUtility.ToJson(this, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Perfil] Não consegui salvar: {e.Message}");
        }
        Mudou?.Invoke();
    }

    /// <summary>Apaga o progresso (útil para testes).</summary>
    public static void Resetar()
    {
        if (File.Exists(Caminho)) File.Delete(Caminho);
        _atual = null;
        Mudou?.Invoke();
    }

    // ------------------------------------------------------------ Coleção

    public DigimonSalvo Parceiro => colecao.Find(d => d.id == parceiroId);

    public DigimonSalvo AdicionarNaColecao(string especie, int nivelDigimon)
    {
        var d = new DigimonSalvo
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 12),
            especie = especie,
            nivel = Mathf.Clamp(nivelDigimon, 1, NivelMaximoDigimon),
            capturadoEm = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        colecao.Add(d);
        return d;
    }

    public void DefinirParceiro(DigimonSalvo d)
    {
        if (d == null || !colecao.Contains(d)) return;
        parceiroId = d.id;
        Salvar();
    }

    // ------------------------------------------------------------ Experiência

    /// <summary>XP necessária para passar do nível <paramref name="n"/> para o próximo (jogador).</summary>
    public static int XpParaProximoNivel(int n) => 100 + (n - 1) * 75;

    /// <summary>XP necessária para o Digimon passar do nível <paramref name="n"/> para o próximo.</summary>
    public static int XpDigimonParaProximoNivel(int n) => 20 + n * n * 3;

    /// <summary>Dá XP ao jogador. Retorna quantos níveis subiu.</summary>
    public int GanharXp(int quantidade)
    {
        var subiu = 0;
        xp += Mathf.Max(0, quantidade);
        while (nivel < NivelMaximo && xp >= XpParaProximoNivel(nivel))
        {
            xp -= XpParaProximoNivel(nivel);
            nivel++;
            subiu++;
        }
        return subiu;
    }

    /// <summary>Dá XP a um Digimon. Retorna quantos níveis ele subiu.</summary>
    public static int GanharXp(DigimonSalvo d, int quantidade)
    {
        if (d == null) return 0;
        var subiu = 0;
        d.xp += Mathf.Max(0, quantidade);
        while (d.nivel < NivelMaximoDigimon && d.xp >= XpDigimonParaProximoNivel(d.nivel))
        {
            d.xp -= XpDigimonParaProximoNivel(d.nivel);
            d.nivel++;
            subiu++;
        }
        return subiu;
    }
}
