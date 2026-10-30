using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Que tipo de ação conta para uma missão.</summary>
public enum TipoMissao
{
    VencerBatalhas,
    CapturarDigimons,
    EncontrarNoHabitat,  // encontrar um Digimon num habitat específico
    AndarMetros,
    VencerAtributo       // vencer um Digimon de um atributo (Fogo, Água...)
}

/// <summary>Definição de uma missão (fixa no código; o progresso fica no PerfilJogador).</summary>
public class DefinicaoMissao
{
    public string Id;
    public string Titulo;
    public TipoMissao Tipo;
    public int Meta;
    public HabitatDigimon Habitat;
    public DigimonAttribute Atributo;
    public int RecompensaXp;
    public int RecompensaBits;
}

/// <summary>
/// Missões diárias: todo dia são sorteadas 3 missões (iguais para todos os jogadores naquele dia).
/// Chame os métodos Registrar... quando algo acontecer no jogo; o progresso é salvo no perfil.
/// </summary>
public static class Missoes
{
    public static event Action<DefinicaoMissao> Concluida;

    private static readonly DefinicaoMissao[] Banco =
    {
        new DefinicaoMissao { Id = "vencer3", Titulo = "Vença 3 batalhas", Tipo = TipoMissao.VencerBatalhas, Meta = 3, RecompensaXp = 150, RecompensaBits = 50 },
        new DefinicaoMissao { Id = "vencer5", Titulo = "Vença 5 batalhas", Tipo = TipoMissao.VencerBatalhas, Meta = 5, RecompensaXp = 250, RecompensaBits = 80 },
        new DefinicaoMissao { Id = "capturar2", Titulo = "Capture 2 Digimons", Tipo = TipoMissao.CapturarDigimons, Meta = 2, RecompensaXp = 150, RecompensaBits = 50 },
        new DefinicaoMissao { Id = "agua", Titulo = "Encontre um Digimon perto da água", Tipo = TipoMissao.EncontrarNoHabitat, Habitat = HabitatDigimon.Agua, Meta = 1, RecompensaXp = 200, RecompensaBits = 60 },
        new DefinicaoMissao { Id = "parque", Titulo = "Encontre 2 Digimons em parques", Tipo = TipoMissao.EncontrarNoHabitat, Habitat = HabitatDigimon.Parque, Meta = 2, RecompensaXp = 150, RecompensaBits = 50 },
        new DefinicaoMissao { Id = "andar1k", Titulo = "Ande 1 km", Tipo = TipoMissao.AndarMetros, Meta = 1000, RecompensaXp = 200, RecompensaBits = 60 },
        new DefinicaoMissao { Id = "andar3k", Titulo = "Ande 3 km", Tipo = TipoMissao.AndarMetros, Meta = 3000, RecompensaXp = 400, RecompensaBits = 120 },
        new DefinicaoMissao { Id = "fogo", Titulo = "Vença um Digimon de Fogo", Tipo = TipoMissao.VencerAtributo, Atributo = DigimonAttribute.Fire, Meta = 1, RecompensaXp = 150, RecompensaBits = 50 },
        new DefinicaoMissao { Id = "planta", Titulo = "Vença um Digimon de Planta", Tipo = TipoMissao.VencerAtributo, Atributo = DigimonAttribute.Plant, Meta = 1, RecompensaXp = 150, RecompensaBits = 50 },
    };

    private const int MissoesPorDia = 3;

    /// <summary>As missões de hoje (sorteia novas quando o dia muda).</summary>
    public static List<(DefinicaoMissao def, ProgressoMissao prog)> DeHoje()
    {
        var perfil = PerfilJogador.Atual;
        var hoje = DateTime.Now.ToString("yyyy-MM-dd");
        if (perfil.diaMissoes != hoje)
        {
            perfil.diaMissoes = hoje;
            perfil.missoes.Clear();
            var rnd = new System.Random(hoje.GetHashCode() & 0x7fffffff ^ 0x5eed);
            var ids = new List<DefinicaoMissao>(Banco);
            for (var i = 0; i < MissoesPorDia && ids.Count > 0; i++)
            {
                var k = rnd.Next(ids.Count);
                perfil.missoes.Add(new ProgressoMissao { id = ids[k].Id });
                ids.RemoveAt(k);
            }
            perfil.Salvar();
        }

        var lista = new List<(DefinicaoMissao, ProgressoMissao)>();
        foreach (var p in perfil.missoes)
        {
            var d = Array.Find(Banco, b => b.Id == p.id);
            if (d != null) lista.Add((d, p));
        }
        return lista;
    }

    public static void RegistrarVitoria(DigimonBase inimigo) =>
        Avancar(d => d.Tipo == TipoMissao.VencerBatalhas ||
                     (d.Tipo == TipoMissao.VencerAtributo && inimigo != null && inimigo.Atribute == d.Atributo), 1);

    public static void RegistrarCaptura() => Avancar(d => d.Tipo == TipoMissao.CapturarDigimons, 1);

    public static void RegistrarEncontro(HabitatDigimon habitat) =>
        Avancar(d => d.Tipo == TipoMissao.EncontrarNoHabitat && (d.Habitat & habitat) != 0, 1);

    public static void RegistrarCaminhada(int metros) => Avancar(d => d.Tipo == TipoMissao.AndarMetros, metros);

    private static void Avancar(Predicate<DefinicaoMissao> conta, int quanto)
    {
        if (quanto <= 0) return;
        var mudou = false;
        foreach (var (def, prog) in DeHoje())
        {
            if (prog.resgatada || !conta(def)) continue;
            prog.progresso = Mathf.Min(def.Meta, prog.progresso + quanto);
            mudou = true;
            if (prog.progresso >= def.Meta)
            {
                // Recompensa automática ao concluir.
                prog.resgatada = true;
                var perfil = PerfilJogador.Atual;
                perfil.GanharXp(def.RecompensaXp);
                perfil.bits += def.RecompensaBits;
                AvisoNaTela.Mostrar($"Missão concluída: {def.Titulo}!  +{def.RecompensaXp} XP  +{def.RecompensaBits} bits");
                Concluida?.Invoke(def);
            }
        }
        if (mudou) PerfilJogador.Atual.Salvar();
    }
}
