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

/// <summary>Um item da Bag e quantos o jogador tem.</summary>
[Serializable]
public class ItemSalvo
{
    public string id;
    public int quantidade;
}

/// <summary>Quanto dos dados de uma espécie o jogador já coletou batalhando (0 a 100%).</summary>
[Serializable]
public class ScanSalvo
{
    public string especie;
    public int porcentagem;
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
    public List<ItemSalvo> itens = new List<ItemSalvo>();
    public List<ScanSalvo> scans = new List<ScanSalvo>();
    public int nivelMostrado = 1;    // último nível já anunciado na tela "Level Up"
    public bool bandagemAtiva;       // a próxima batalha começa com vida extra
    public bool somLigado = true;
    public bool comecou;             // já recebeu os itens iniciais

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
        itens ??= new List<ItemSalvo>();
        scans ??= new List<ScanSalvo>();
        colecao.RemoveAll(d => d == null || string.IsNullOrEmpty(d.especie));
        if (colecao.Count == 0) AdicionarNaColecao("Gabumon", 5);
        if (Parceiro == null) parceiroId = colecao[0].id;
        if (nivelMostrado < 1) nivelMostrado = nivel;
        if (!comecou)
        {
            comecou = true;
            AdicionarItem("meat", 3);
            AdicionarItem("bandage", 3);
        }
    }

    // ------------------------------------------------------------ Bag

    public int Quantidade(string itemId) => itens.Find(i => i.id == itemId)?.quantidade ?? 0;

    public void AdicionarItem(string itemId, int quantidade = 1)
    {
        var i = itens.Find(x => x.id == itemId);
        if (i == null) itens.Add(i = new ItemSalvo { id = itemId });
        i.quantidade += quantidade;
    }

    public bool GastarItem(string itemId)
    {
        var i = itens.Find(x => x.id == itemId);
        if (i == null || i.quantidade <= 0) return false;
        i.quantidade--;
        if (i.quantidade == 0) itens.Remove(i);
        return true;
    }

    // ------------------------------------------------------------ Scan

    public int Scan(string especie) => scans.Find(s => CatalogoDigimon.Normalizar(s.especie) == CatalogoDigimon.Normalizar(especie))?.porcentagem ?? 0;

    /// <summary>Soma dados coletados de uma espécie (máximo 100%). Retorna o novo valor.</summary>
    public int AdicionarScan(string especie, int pontos)
    {
        var s = scans.Find(x => CatalogoDigimon.Normalizar(x.especie) == CatalogoDigimon.Normalizar(especie));
        if (s == null) scans.Add(s = new ScanSalvo { especie = especie });
        s.porcentagem = Mathf.Clamp(s.porcentagem + pontos, 0, 100);
        return s.porcentagem;
    }

    /// <summary>"Scan Data": com 100% dos dados, o Digimon é baixado para a coleção e o scan recomeça.</summary>
    public DigimonSalvo ConcluirScan(string especie)
    {
        var s = scans.Find(x => CatalogoDigimon.Normalizar(x.especie) == CatalogoDigimon.Normalizar(especie));
        if (s == null || s.porcentagem < 100) return null;
        s.porcentagem = 0;
        return AdicionarNaColecao(especie, 1);
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
