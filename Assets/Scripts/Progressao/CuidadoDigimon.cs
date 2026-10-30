using System;
using UnityEngine;

public enum StatusTreino { Offense, Defense, Speed, Brains }

/// <summary>
/// Necessidades do Digimon no estilo V-Pet: fome, energia, humor, cocô e amizade.
/// Tudo muda com o tempo real, mesmo com o jogo fechado (a conta é feita ao abrir).
/// A amizade e o treino deixam o parceiro mais forte na batalha.
/// </summary>
public static class CuidadoDigimon
{
    // Por hora de tempo real.
    private const float FomePorHora = 9f;
    private const float EnergiaPorHoraDescansando = 18f;
    private const float HumorPorHora = 4f;
    private const float HumorPorCocoPorHora = 4f;
    private const int SegundosEntreCocos = 3 * 3600;
    public const int MaxCocos = 4;

    private static long Agora => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>Aplica o tempo que passou desde a última vez. Chame antes de mostrar ou usar as necessidades.</summary>
    public static void Atualizar(DigimonSalvo d)
    {
        if (d == null) return;
        var agora = Agora;
        if (d.ultimoCuidado <= 0 || d.ultimoCuidado > agora)
        {
            d.ultimoCuidado = agora;
            if (d.proximoCoco <= 0) d.proximoCoco = agora + SegundosEntreCocos;
            return;
        }
        var horas = (agora - d.ultimoCuidado) / 3600f;
        if (horas < 1f / 120f) return; // menos de 30 s: nada muda
        d.ultimoCuidado = agora;

        // Cocô: um a cada algumas horas (no máximo 4 acumulados).
        while (d.proximoCoco > 0 && d.proximoCoco <= agora && d.cocos < MaxCocos)
        {
            d.cocos++;
            d.proximoCoco += SegundosEntreCocos;
        }
        if (d.proximoCoco <= agora) d.proximoCoco = agora + SegundosEntreCocos;

        d.fome = Mathf.Clamp(d.fome - FomePorHora * horas, 0f, 100f);
        d.energia = Mathf.Clamp(d.energia + EnergiaPorHoraDescansando * horas, 0f, 100f);
        d.humor = Mathf.Clamp(d.humor - (HumorPorHora + d.cocos * HumorPorCocoPorHora + (d.fome < 20f ? 6f : 0f)) * horas, 0f, 100f);
        // Abandonado triste e com fome por muito tempo: a amizade cai.
        if (d.humor < 15f && d.fome < 15f) d.amizade = Mathf.Max(0, d.amizade - Mathf.FloorToInt(horas));
    }

    /// <summary>O parceiro precisa de atenção? (mostra um aviso na foto da HUD)</summary>
    public static bool PrecisaDeAtencao(DigimonSalvo d)
    {
        if (d == null) return false;
        Atualizar(d);
        return d.fome < 30f || d.humor < 30f || d.energia < 20f || d.cocos > 0;
    }

    public static void Amizade(DigimonSalvo d, int quanto) => d.amizade = Mathf.Clamp(d.amizade + quanto, 0, 100);

    public static void Mudar(ref float valor, float quanto) => valor = Mathf.Clamp(valor + quanto, 0f, 100f);

    // ------------------------------------------------------------ Ações

    public enum ResultadoComida { Comeu, Cheio, SemComida }

    /// <summary>Dá um Meat da Bag: mata a fome, deixa feliz e dá um pouco de EXP. O cocô vem logo depois.</summary>
    public static ResultadoComida Alimentar(PerfilJogador p, DigimonSalvo d)
    {
        Atualizar(d);
        if (d.fome >= 92f) return ResultadoComida.Cheio;
        if (!p.GastarItem(Itens.Meat)) return ResultadoComida.SemComida;
        var estavaComFome = d.fome < 60f;
        Mudar(ref d.fome, 35f);
        Mudar(ref d.humor, 6f);
        if (estavaComFome) Amizade(d, 1);
        PerfilJogador.GanharXp(d, 15);
        var agora = Agora;
        if (d.proximoCoco > agora + 1800) d.proximoCoco = agora + 1800; // comeu: cocô em até 30 min
        p.Salvar();
        return ResultadoComida.Comeu;
    }

    /// <summary>Carinho. Rende menos se repetido em seguida (<paramref name="repeticao"/> = carinhos recentes).</summary>
    public static int Carinho(DigimonSalvo d, int repeticao)
    {
        Atualizar(d);
        var fator = Mathf.Max(0f, 1f - repeticao * 0.25f);
        Mudar(ref d.humor, 8f * fator);
        var ganho = repeticao < 2 && d.humor > 30f ? 1 : 0;
        Amizade(d, ganho);
        return ganho;
    }

    public static int Limpar(DigimonSalvo d)
    {
        Atualizar(d);
        var havia = d.cocos;
        if (havia == 0) return 0;
        d.cocos = 0;
        Mudar(ref d.humor, 5f * havia);
        Amizade(d, 1);
        return havia;
    }

    /// <summary>Pode treinar? Retorna o motivo se não puder.</summary>
    public static string PodeTreinar(DigimonSalvo d)
    {
        Atualizar(d);
        if (d.energia < 20f) return "está cansado demais para treinar";
        if (d.fome < 15f) return "está com fome demais para treinar";
        if (d.humor < 15f) return "não quer treinar agora (está triste)";
        return null;
    }

    /// <summary>Fim de um treino: soma os pontos no status escolhido, gasta energia e fome, dá EXP.</summary>
    public static int Treinar(DigimonSalvo d, StatusTreino s, int pontos)
    {
        Atualizar(d);
        Mudar(ref d.energia, -20f);
        Mudar(ref d.fome, -10f);
        var limite = Mathf.Clamp(20 + d.nivel * 2, 20, 100); // não passa muito do nível
        int Somar(ref int v) { var antes = v; v = Mathf.Clamp(v + pontos, 0, limite); return v - antes; }
        int ganho;
        switch (s)
        {
            case StatusTreino.Offense: ganho = Somar(ref d.treinoOffense); break;
            case StatusTreino.Defense: ganho = Somar(ref d.treinoDefense); break;
            case StatusTreino.Speed: ganho = Somar(ref d.treinoSpeed); break;
            default: ganho = Somar(ref d.treinoBrains); break;
        }
        if (pontos >= 6) { Mudar(ref d.humor, 5f); Amizade(d, 1); }
        else if (pontos <= 1) Mudar(ref d.humor, -4f);
        PerfilJogador.GanharXp(d, 5 + pontos * 2);
        return ganho;
    }

    public static int Treino(DigimonSalvo d, StatusTreino s)
    {
        switch (s)
        {
            case StatusTreino.Offense: return d.treinoOffense;
            case StatusTreino.Defense: return d.treinoDefense;
            case StatusTreino.Speed: return d.treinoSpeed;
            default: return d.treinoBrains;
        }
    }

    /// <summary>Batalha cansa e dá fome; vencer alegra, perder entristece.</summary>
    public static void DepoisDaBatalha(DigimonSalvo d, bool venceu)
    {
        if (d == null) return;
        Atualizar(d);
        Mudar(ref d.energia, -12f);
        Mudar(ref d.fome, -8f);
        Mudar(ref d.humor, venceu ? 8f : -6f);
        if (venceu) Amizade(d, 1);
    }

    // ------------------------------------------------------------ Textos

    public static string Nome(StatusTreino s)
    {
        switch (s)
        {
            case StatusTreino.Offense: return "Offense";
            case StatusTreino.Defense: return "Defense";
            case StatusTreino.Speed: return "Speed";
            default: return "Brains";
        }
    }

    public static string NivelAmizade(int a) =>
        a >= 90 ? "Melhores amigos" : a >= 60 ? "Muito próximos" : a >= 30 ? "Amigos" : a >= 10 ? "Se conhecendo" : "Desconfiado";
}
