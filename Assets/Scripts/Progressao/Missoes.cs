using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>As três abas da tela de missões.</summary>
public enum CategoriaMissao { Historia, Diaria, Especial }

/// <summary>Que tipo de ação conta para uma missão.</summary>
public enum TipoMissao
{
    VencerBatalhas,
    CapturarDigimons,    // baixar qualquer Digimon pelo Scan
    EncontrarNoHabitat,  // encontrar um Digimon num habitat específico
    AndarMetros,
    VencerAtributo,      // vencer um Digimon de um atributo (Fogo, Água...)
    DecifrarDados,       // decifrar Encrypted Data (os "PokéStops")
    VencerTipo,          // vencer um Digimon de um tipo (Vírus, Data, Vacina)
    AlimentarDigimon,
    TreinarDigimon,
    CapturarEspecie,     // baixar uma espécie específica pelo Scan ("Digi Convert AGUMON")
    Digivolver
}

/// <summary>Item dado como recompensa (id de <see cref="Itens"/>).</summary>
public struct RecompensaItem
{
    public string Item;
    public int Quantidade;
    public RecompensaItem(string item, int quantidade = 1) { Item = item; Quantidade = quantidade; }
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
    public DigimonType TipoDigimon;
    public string Especie;
    public int RecompensaXp;
    public int RecompensaBits;
    public RecompensaItem[] RecompensaItens = Array.Empty<RecompensaItem>();
}

/// <summary>Capítulo da história: passos feitos em ordem, um de cada vez.</summary>
public class CapituloHistoria
{
    public string Titulo;
    public DefinicaoMissao[] Passos;
}

/// <summary>Evento especial: missões que valem só entre <see cref="Inicio"/> e <see cref="Fim"/> (datas locais, fim incluso).</summary>
public class EventoEspecial
{
    public string Id;
    public string Titulo;
    public string Descricao;
    public DateTime Inicio;
    public DateTime Fim;
    public DefinicaoMissao[] Missoes;

    public bool Ativo(DateTime agora) => agora >= Inicio && agora < Fim.Date.AddDays(1);
}

/// <summary>Onde o jogador está na história.</summary>
public class EstadoHistoria
{
    public CapituloHistoria Capitulo;
    public int IndiceCapitulo;
    public int Passo;              // passo atual dentro do capítulo
    public DefinicaoMissao Missao; // missão do passo atual
    public ProgressoMissao Progresso;
}

/// <summary>
/// Missões em três categorias:
/// - História: capítulos com passos em ordem (o próximo libera quando o atual termina).
/// - Diárias: todo dia são sorteadas 3 missões (iguais para todos os jogadores naquele dia).
/// - Especiais: missões de eventos com data de início e fim.
/// Chame os métodos Registrar... quando algo acontecer no jogo; o progresso é salvo no perfil
/// e a recompensa é entregue sozinha quando a missão termina.
/// </summary>
public static class Missoes
{
    public static event Action<DefinicaoMissao> Concluida;

    private static RecompensaItem[] Meat(int n = 1) => new[] { new RecompensaItem(Itens.Meat, n) };
    private static RecompensaItem[] Bandage(int n = 1) => new[] { new RecompensaItem(Itens.Bandage, n) };

    // ------------------------------------------------------------ Diárias

    private static readonly DefinicaoMissao[] Banco =
    {
        new DefinicaoMissao { Id = "vencer3", Titulo = "Delete 3 Digimon", Tipo = TipoMissao.VencerBatalhas, Meta = 3, RecompensaXp = 150, RecompensaBits = 50, RecompensaItens = Bandage() },
        new DefinicaoMissao { Id = "vencer5", Titulo = "Delete 5 Digimon", Tipo = TipoMissao.VencerBatalhas, Meta = 5, RecompensaXp = 250, RecompensaBits = 80, RecompensaItens = Bandage(2) },
        new DefinicaoMissao { Id = "capturar2", Titulo = "Digi Convert 2 Digimon", Tipo = TipoMissao.CapturarDigimons, Meta = 2, RecompensaXp = 150, RecompensaBits = 50 },
        new DefinicaoMissao { Id = "agua", Titulo = "Find a Digimon near water", Tipo = TipoMissao.EncontrarNoHabitat, Habitat = HabitatDigimon.Agua, Meta = 1, RecompensaXp = 200, RecompensaBits = 60 },
        new DefinicaoMissao { Id = "parque", Titulo = "Find 2 Digimon in parks", Tipo = TipoMissao.EncontrarNoHabitat, Habitat = HabitatDigimon.Parque, Meta = 2, RecompensaXp = 150, RecompensaBits = 50 },
        new DefinicaoMissao { Id = "andar1k", Titulo = "Walk 1 km", Tipo = TipoMissao.AndarMetros, Meta = 1000, RecompensaXp = 200, RecompensaBits = 60, RecompensaItens = Meat() },
        new DefinicaoMissao { Id = "andar3k", Titulo = "Walk 3 km", Tipo = TipoMissao.AndarMetros, Meta = 3000, RecompensaXp = 400, RecompensaBits = 120, RecompensaItens = Meat(2) },
        new DefinicaoMissao { Id = "fogo", Titulo = "Delete a Fire Digimon", Tipo = TipoMissao.VencerAtributo, Atributo = DigimonAttribute.Fire, Meta = 1, RecompensaXp = 150, RecompensaBits = 50 },
        new DefinicaoMissao { Id = "planta", Titulo = "Delete a Plant Digimon", Tipo = TipoMissao.VencerAtributo, Atributo = DigimonAttribute.Plant, Meta = 1, RecompensaXp = 150, RecompensaBits = 50 },
        new DefinicaoMissao { Id = "virus", Titulo = "Delete a Virus type Digimon", Tipo = TipoMissao.VencerTipo, TipoDigimon = DigimonType.Virus, Meta = 1, RecompensaXp = 150, RecompensaBits = 50 },
        new DefinicaoMissao { Id = "dados3", Titulo = "Decode 3 Encrypted Data", Tipo = TipoMissao.DecifrarDados, Meta = 3, RecompensaXp = 150, RecompensaBits = 50 },
        new DefinicaoMissao { Id = "dados10", Titulo = "Decode 10 Encrypted Data", Tipo = TipoMissao.DecifrarDados, Meta = 10, RecompensaXp = 400, RecompensaBits = 120 },
        new DefinicaoMissao { Id = "alimentar2", Titulo = "Feed your partner 2 times", Tipo = TipoMissao.AlimentarDigimon, Meta = 2, RecompensaXp = 100, RecompensaBits = 40, RecompensaItens = Meat() },
        new DefinicaoMissao { Id = "treinar2", Titulo = "Train your partner 2 times", Tipo = TipoMissao.TreinarDigimon, Meta = 2, RecompensaXp = 150, RecompensaBits = 50, RecompensaItens = Meat() },
    };

    private const int MissoesPorDia = 3;

    // ------------------------------------------------------------ História

    public static readonly CapituloHistoria[] Historia =
    {
        new CapituloHistoria
        {
            Titulo = "Prologue 1: Discovering a new World",
            Passos = new[]
            {
                new DefinicaoMissao { Id = "h1_virus", Titulo = "Delete a Virus type Digimon", Tipo = TipoMissao.VencerTipo, TipoDigimon = DigimonType.Virus, Meta = 1, RecompensaXp = 100, RecompensaBits = 30, RecompensaItens = Meat() },
                new DefinicaoMissao { Id = "h1_dados", Titulo = "Decode an Encrypted Data", Tipo = TipoMissao.DecifrarDados, Meta = 1, RecompensaXp = 100, RecompensaBits = 30 },
                new DefinicaoMissao { Id = "h1_alimentar", Titulo = "Feed your partner", Tipo = TipoMissao.AlimentarDigimon, Meta = 1, RecompensaXp = 100, RecompensaBits = 30, RecompensaItens = Meat() },
                new DefinicaoMissao { Id = "h1_andar", Titulo = "Walk 500 m with your partner", Tipo = TipoMissao.AndarMetros, Meta = 500, RecompensaXp = 200, RecompensaBits = 60, RecompensaItens = Bandage() },
            }
        },
        new CapituloHistoria
        {
            Titulo = "Prologue 2: The Bond with your Partner",
            Passos = new[]
            {
                new DefinicaoMissao { Id = "h2_treinar", Titulo = "Train your partner 2 times", Tipo = TipoMissao.TreinarDigimon, Meta = 2, RecompensaXp = 150, RecompensaBits = 40 },
                new DefinicaoMissao { Id = "h2_vencer", Titulo = "Delete 3 Digimon", Tipo = TipoMissao.VencerBatalhas, Meta = 3, RecompensaXp = 150, RecompensaBits = 40, RecompensaItens = Bandage() },
                new DefinicaoMissao { Id = "h2_parque", Titulo = "Find a Digimon in a park", Tipo = TipoMissao.EncontrarNoHabitat, Habitat = HabitatDigimon.Parque, Meta = 1, RecompensaXp = 150, RecompensaBits = 40 },
                new DefinicaoMissao { Id = "h2_scan", Titulo = "Digi Convert a Digimon", Tipo = TipoMissao.CapturarDigimons, Meta = 1, RecompensaXp = 300, RecompensaBits = 80, RecompensaItens = Meat(2) },
            }
        },
        new CapituloHistoria
        {
            Titulo = "Chapter 1: Signs of Digivolution",
            Passos = new[]
            {
                new DefinicaoMissao { Id = "c1_fogo", Titulo = "Delete a Fire Digimon", Tipo = TipoMissao.VencerAtributo, Atributo = DigimonAttribute.Fire, Meta = 1, RecompensaXp = 200, RecompensaBits = 60 },
                new DefinicaoMissao { Id = "c1_dados", Titulo = "Decode 5 Encrypted Data", Tipo = TipoMissao.DecifrarDados, Meta = 5, RecompensaXp = 250, RecompensaBits = 80, RecompensaItens = Meat() },
                new DefinicaoMissao { Id = "c1_andar", Titulo = "Walk 2 km", Tipo = TipoMissao.AndarMetros, Meta = 2000, RecompensaXp = 300, RecompensaBits = 100, RecompensaItens = Bandage(2) },
                new DefinicaoMissao { Id = "c1_digivolver", Titulo = "Digivolve your partner", Tipo = TipoMissao.Digivolver, Meta = 1, RecompensaXp = 500, RecompensaBits = 200, RecompensaItens = Meat(3) },
            }
        },
    };

    // ------------------------------------------------------------ Especiais

    public static readonly EventoEspecial[] Eventos =
    {
        new EventoEspecial
        {
            Id = "aventura2026",
            Titulo = "Adventure Begins Special Event",
            Descricao = "Take your first steps into a new digital world",
            Inicio = new DateTime(2026, 11, 1),
            Fim = new DateTime(2027, 1, 31),
            Missoes = new[]
            {
                new DefinicaoMissao { Id = "agumon", Titulo = "Digi Convert AGUMON", Tipo = TipoMissao.CapturarEspecie, Especie = "Agumon", Meta = 1, RecompensaXp = 100, RecompensaBits = 120, RecompensaItens = Meat() },
                new DefinicaoMissao { Id = "vencer10", Titulo = "Delete 10 Digimon", Tipo = TipoMissao.VencerBatalhas, Meta = 10, RecompensaXp = 300, RecompensaBits = 150, RecompensaItens = Bandage(2) },
                new DefinicaoMissao { Id = "andar5k", Titulo = "Walk 5 km", Tipo = TipoMissao.AndarMetros, Meta = 5000, RecompensaXp = 400, RecompensaBits = 200, RecompensaItens = Meat(3) },
            }
        },
    };

    // ------------------------------------------------------------ Consultas

    /// <summary>As missões de hoje (sorteia novas quando o dia muda).</summary>
    public static List<(DefinicaoMissao def, ProgressoMissao prog)> DeHoje()
    {
        var perfil = PerfilJogador.Atual;
        var hoje = DateTime.Now.ToString("yyyy-MM-dd");
        if (perfil.diaMissoes != hoje)
        {
            perfil.diaMissoes = hoje;
            perfil.missoes.Clear();
            var rnd = new System.Random(SementeDoDia(hoje));
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

    /// <summary>Semente fixa por data (string.GetHashCode pode mudar entre execuções).</summary>
    private static int SementeDoDia(string dia)
    {
        unchecked
        {
            var h = 0x5eed;
            foreach (var c in dia) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }

    /// <summary>Quanto falta para as diárias trocarem (meia-noite local).</summary>
    public static TimeSpan TempoParaNovasDiarias() => DateTime.Today.AddDays(1) - DateTime.Now;

    /// <summary>Passo atual da história, ou null se o jogador já terminou todos os capítulos.</summary>
    public static EstadoHistoria HistoriaAtual()
    {
        var perfil = PerfilJogador.Atual;
        var passo = Mathf.Max(0, perfil.historiaPasso);
        for (var c = 0; c < Historia.Length; c++)
        {
            var cap = Historia[c];
            if (passo < cap.Passos.Length)
            {
                var def = cap.Passos[passo];
                if (perfil.historia == null || perfil.historia.id != def.Id)
                    perfil.historia = new ProgressoMissao { id = def.Id }; // passo novo (ou save antigo)
                return new EstadoHistoria { Capitulo = cap, IndiceCapitulo = c, Passo = passo, Missao = def, Progresso = perfil.historia };
            }
            passo -= cap.Passos.Length;
        }
        return null;
    }

    /// <summary>Quantos passos da história existem antes do capítulo <paramref name="capitulo"/>.</summary>
    private static int PassosAntes(int capitulo)
    {
        var n = 0;
        for (var c = 0; c < capitulo && c < Historia.Length; c++) n += Historia[c].Passos.Length;
        return n;
    }

    public static List<EventoEspecial> EventosAtivos()
    {
        var agora = DateTime.Now;
        return new List<EventoEspecial>(Array.FindAll(Eventos, e => e.Ativo(agora)));
    }

    /// <summary>Progresso de uma missão de evento (cria se ainda não existe).</summary>
    public static ProgressoMissao ProgressoEspecial(EventoEspecial evento, DefinicaoMissao def)
    {
        var perfil = PerfilJogador.Atual;
        var id = evento.Id + "/" + def.Id;
        var p = perfil.missoesEspeciais.Find(x => x.id == id);
        if (p == null) perfil.missoesEspeciais.Add(p = new ProgressoMissao { id = id });
        return p;
    }

    /// <summary>
    /// A missão mostrada no painel da HUD: o passo atual da história; se acabou, a primeira diária pendente;
    /// se não houver, a primeira especial pendente. <paramref name="subtitulo"/> diz de onde ela é.
    /// </summary>
    public static (DefinicaoMissao def, ProgressoMissao prog)? Acompanhada(out string subtitulo)
    {
        var h = HistoriaAtual();
        if (h != null) { subtitulo = h.Capitulo.Titulo; return (h.Missao, h.Progresso); }
        foreach (var (def, prog) in DeHoje())
            if (!prog.resgatada) { subtitulo = "Daily mission"; return (def, prog); }
        foreach (var e in EventosAtivos())
        foreach (var def in e.Missoes)
        {
            var prog = ProgressoEspecial(e, def);
            if (!prog.resgatada) { subtitulo = e.Titulo; return (def, prog); }
        }
        subtitulo = "All missions completed!";
        return null;
    }

    /// <summary>"3/5" ou, para caminhada, "0,4/1 km".</summary>
    public static string TextoProgresso(DefinicaoMissao def, int progresso)
    {
        if (def.Tipo != TipoMissao.AndarMetros) return $"{progresso}/{def.Meta}";
        return $"{progresso / 1000f:0.#}/{def.Meta / 1000f:0.#} km";
    }

    // ------------------------------------------------------------ Registro de ações

    public static void RegistrarVitoria(DigimonBase inimigo) =>
        Avancar(d => d.Tipo == TipoMissao.VencerBatalhas ||
                     (d.Tipo == TipoMissao.VencerAtributo && inimigo != null && inimigo.Atribute == d.Atributo) ||
                     (d.Tipo == TipoMissao.VencerTipo && inimigo != null && inimigo.Type == d.TipoDigimon), 1);

    public static void RegistrarCaptura(string especie = null) =>
        Avancar(d => d.Tipo == TipoMissao.CapturarDigimons ||
                     (d.Tipo == TipoMissao.CapturarEspecie && MesmaEspecie(especie, d.Especie)), 1);

    public static void RegistrarEncontro(HabitatDigimon habitat) =>
        Avancar(d => d.Tipo == TipoMissao.EncontrarNoHabitat && (d.Habitat & habitat) != 0, 1);

    public static void RegistrarDecifrar() => Avancar(d => d.Tipo == TipoMissao.DecifrarDados, 1);

    public static void RegistrarCaminhada(int metros) => Avancar(d => d.Tipo == TipoMissao.AndarMetros, metros);

    public static void RegistrarAlimentar() => Avancar(d => d.Tipo == TipoMissao.AlimentarDigimon, 1);

    public static void RegistrarTreino() => Avancar(d => d.Tipo == TipoMissao.TreinarDigimon, 1);

    public static void RegistrarDigivolucao() => Avancar(d => d.Tipo == TipoMissao.Digivolver, 1);

    private static bool MesmaEspecie(string a, string b) =>
        !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && CatalogoDigimon.Normalizar(a) == CatalogoDigimon.Normalizar(b);

    private static void Avancar(Predicate<DefinicaoMissao> conta, int quanto)
    {
        if (quanto <= 0) return;
        var perfil = PerfilJogador.Atual;
        var mudou = false;

        foreach (var (def, prog) in DeHoje())
            mudou |= Somar(def, prog, conta, quanto);

        foreach (var e in EventosAtivos())
        foreach (var def in e.Missoes)
            mudou |= Somar(def, ProgressoEspecial(e, def), conta, quanto);

        // História: só o passo atual conta. Ao terminar, o próximo passo é liberado (começando do zero).
        var h = HistoriaAtual();
        if (h != null && Somar(h.Missao, h.Progresso, conta, quanto))
        {
            mudou = true;
            if (h.Progresso.resgatada)
            {
                perfil.historiaPasso = PassosAntes(h.IndiceCapitulo) + h.Passo + 1;
                var proximo = HistoriaAtual();
                if (proximo == null) AvisoNaTela.Mostrar("Story complete! New chapters coming soon.");
                else if (proximo.IndiceCapitulo != h.IndiceCapitulo) AvisoNaTela.Mostrar($"New chapter: {proximo.Capitulo.Titulo}");
            }
        }

        if (mudou) perfil.Salvar();
    }

    /// <summary>Soma progresso numa missão que conta para a ação. Retorna true se mudou algo.</summary>
    private static bool Somar(DefinicaoMissao def, ProgressoMissao prog, Predicate<DefinicaoMissao> conta, int quanto)
    {
        if (prog.resgatada || !conta(def)) return false;
        prog.progresso = Mathf.Min(def.Meta, prog.progresso + quanto);
        if (prog.progresso >= def.Meta) Concluir(def, prog);
        return true;
    }

    /// <summary>Entrega a recompensa (automática ao concluir).</summary>
    private static void Concluir(DefinicaoMissao def, ProgressoMissao prog)
    {
        prog.resgatada = true;
        var perfil = PerfilJogador.Atual;
        perfil.GanharXp(def.RecompensaXp);
        var xpDigimon = def.RecompensaXp / 2; // o parceiro também ganha (linha "Acquired Digimon EXP" do popup)
        PerfilJogador.GanharXp(perfil.Parceiro, xpDigimon);
        perfil.bits += def.RecompensaBits;
        foreach (var r in def.RecompensaItens) perfil.AdicionarItem(r.Item, r.Quantidade);
        AvisoNaTela.Mostrar($"Mission complete: {def.Titulo}!");
        // Popup com as recompensas (Figma "2444"): aparece no mapa assim que não houver outra tela aberta.
        TelaRecompensa.Enfileirar(new TelaRecompensa.Recompensa
        {
            XpTamer = def.RecompensaXp, XpDigimon = xpDigimon, Bits = def.RecompensaBits, Itens = def.RecompensaItens
        });
        Concluida?.Invoke(def);
    }
}
