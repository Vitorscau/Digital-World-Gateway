using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Digivolução no estilo Digimon World.
///
/// - Fresh -> In-Training -> Rookie -> Champion -> Ultimate -> Mega, por nível (e, nos bebês, também pela idade).
/// - Cada Digimon tem caminhos possíveis. Os "bons" pedem treino no status certo, amizade, vitórias e poucos
///   erros de cuidado; vale o caminho que cumprir mais condições (é preciso cumprir a maioria, como no DW1).
/// - Caminhos "ruins" (Numemon, Sukamon, SkullGreymon...) aparecem quando o Digimon foi maltratado.
/// - Se nenhum caminho de Champion for cumprido, vira Numemon (ou Sukamon, se a sujeira e os erros foram demais).
/// - Os caminhos seguem as linhas conhecidas da franquia quando o modelo existe no jogo; quem não tem caminho
///   definido digivolve para um Digimon do estágio seguinte com o mesmo elemento.
/// </summary>
public static class Digivolucao
{
    private enum Tipo { Livre, Bom, Ruim }

    private class Caminho
    {
        public string Para;
        public Tipo Tipo;
        public StatusTreino? Foco;
    }

    private static Caminho L(string para, StatusTreino? foco = null) => new Caminho { Para = para, Tipo = Tipo.Livre, Foco = foco };
    private static Caminho B(string para, StatusTreino? foco = null) => new Caminho { Para = para, Tipo = Tipo.Bom, Foco = foco };
    private static Caminho R(string para) => new Caminho { Para = para, Tipo = Tipo.Ruim };

    private const StatusTreino Off = StatusTreino.Offense, Def = StatusTreino.Defense, Spd = StatusTreino.Speed, Bra = StatusTreino.Brains;

    /// <summary>
    /// Árvore. Baseada nas linhas da franquia (Digimon World, Adventure, V-Pets). Onde o modelo não existe
    /// no jogo (Koromon, Tentomon, Keramon...), o caminho passa por um parente próximo.
    /// </summary>
    private static readonly Dictionary<string, Caminho[]> Arvore = new Dictionary<string, Caminho[]>(StringComparer.OrdinalIgnoreCase)
    {
        // Fresh -> In-Training (o ovo decide a linha)
        ["Botamon"] = new[] { L("Nyaromon") },               // sem Koromon no jogo: Nyaromon (linha do YukimiBotamon)
        ["Punimon"] = new[] { L("Tsunomon") },
        ["Poyomon"] = new[] { L("Tokomon") },
        ["Pabumon"] = new[] { L("Motimon") },
        ["Kuramon"] = new[] { L("Tsumemon"), R("Pagumon") },

        // In-Training -> Rookie (o treino escolhe)
        ["Nyaromon"] = new[] { L("Agumon", Off), L("Salamon", Bra), L("ToyAgumon", Def), L("Guilmon", Spd) },
        ["Tsunomon"] = new[] { L("Gabumon", Def), L("Gaomon", Off), L("Dorumon", Spd), L("Renamon", Bra) },
        ["Tokomon"] = new[] { L("Patamon", Bra), L("Lopmon", Def), L("Falcomon", Spd), L("Shoutmon", Off) },
        ["Motimon"] = new[] { L("Wormmon", Def), L("Hagurumon", Bra), L("Lalamon", Spd), L("Palmon", Off) },
        ["Tsumemon"] = new[] { L("DemiDevimon", Bra), L("Tsukaimon", Spd), L("Gazimon", Off), L("Goblimon", Def) },
        ["Pagumon"] = new[] { L("Gazimon"), L("Goblimon"), L("DemiDevimon") },
        ["Tanemon"] = new[] { L("Palmon", Bra), L("Lalamon", Spd), L("Wormmon", Def) },
        ["Bukamon"] = new[] { L("Gomamon", Def), L("Betamon", Off), L("Syakomon", Bra) },
        ["Yokomon"] = new[] { L("Biyomon", Spd), L("Hawkmon", Off), L("Palmon", Bra) },
        ["Wanyamon"] = new[] { L("Gaomon", Off), L("Kudamon", Bra), L("Zubamon", Spd) },

        // Rookie -> Champion
        ["Agumon"] = new[] { B("Greymon", Off), B("Tyrannomon", Def), B("Meramon", Spd), B("GeoGreymon", Bra), R("Numemon") },
        ["Agumon_black"] = new[] { B("BlackGreymon", Off), B("Tyrannomon", Def), R("Numemon") },
        ["Gabumon"] = new[] { B("Garurumon", Spd), B("Frigimon", Def), B("Leomon", Off), R("Ogremon") },
        ["Patamon"] = new[] { B("Angemon", Bra), B("Leomon", Off), B("Starmon", Spd), R("Bakemon") },
        ["Salamon"] = new[] { B("Gatomon", Spd), B("Sorcermon", Bra), R("BlackGatomon") },
        ["Palmon"] = new[] { B("Togemon", Off), B("Woodmon", Def), B("Sunflowmon", Bra), R("Vegiemon") },
        ["Biyomon"] = new[] { B("Birdramon", Off), B("Aquilamon", Spd), B("Peckmon", Bra), R("Numemon") },
        ["Gomamon"] = new[] { B("Ikkakumon", Def), B("Seadramon", Off), B("Gekomon", Bra), R("ShellNumemon") },
        ["Veemon"] = new[] { B("ExVeemon", Off), B("Veedramon", Def), B("Gargomon", Spd), R("Numemon") },
        ["Tsukaimon"] = new[] { B("Devimon", Bra), B("Bakemon", Def), R("BlackGatomon") },
        ["DemiDevimon"] = new[] { B("Devimon", Bra), B("Wizardmon", Spd), R("Bakemon") },
        ["Gazimon"] = new[] { B("Ogremon", Off), B("Kyubimon", Spd), R("Nanimon") },
        ["Goblimon"] = new[] { B("Ogremon", Off), B("Tyrannomon", Def), R("Nanimon") },
        ["Hagurumon"] = new[] { B("Guardromon", Def), B("Clockmon", Bra), R("GoldNumemon") },
        ["ToyAgumon"] = new[] { B("Guardromon", Def), B("Greymon", Off), R("Numemon") },
        ["Wormmon"] = new[] { B("Stingmon", Off), B("Kuwagamon", Def), B("Waspmon", Spd), R("Numemon") },
        ["Lalamon"] = new[] { B("Sunflowmon", Bra), B("Woodmon", Def), R("Vegiemon") },
        ["Renamon"] = new[] { B("Kyubimon", Bra), B("Reppamon", Spd), R("Bakemon") },
        ["Lopmon"] = new[] { B("Gargomon", Off), B("Frigimon", Def), R("Numemon") },
        ["Guilmon"] = new[] { B("Growlmon", Off), B("Tyrannomon", Def), R("BlackGreymon") },
        ["Gaomon"] = new[] { B("Gaogamon", Off), B("Leomon", Def), R("Ogremon") },
        ["Falcomon"] = new[] { B("Peckmon", Spd), B("Aquilamon", Off), R("Numemon") },
        ["Dorumon"] = new[] { B("Dorugamon", Off), B("Garurumon", Spd), R("Ogremon") },
        ["Dracomon"] = new[] { B("Coredramon Green", Off), B("Coredramon Blue", Spd), R("Numemon") },
        ["Ryudamon"] = new[] { B("Ginryumon", Def), R("Numemon") },
        ["Betamon"] = new[] { B("Seadramon", Off), B("Gekomon", Bra), R("ShellNumemon") },
        ["Hawkmon"] = new[] { B("Aquilamon", Spd), B("Birdramon", Off), R("Numemon") },
        ["Kudamon"] = new[] { B("Reppamon", Spd), B("Angemon", Bra), R("Numemon") },
        ["Zubamon"] = new[] { B("ZubaEagermon", Off), R("Numemon") },
        ["Vorvomon"] = new[] { B("Lavorvomon", Off), R("Numemon") },
        ["Jazamon"] = new[] { B("Jazardmon", Spd), R("Numemon") },
        ["Shoutmon"] = new[] { B("Greymon", Off), B("Starmon", Spd), R("Numemon") },
        ["Syakomon"] = new[] { B("Ikkakumon", Def), B("ShellNumemon", Bra) },
        ["Solarmon"] = new[] { B("Clockmon", Bra), B("Guardromon", Def), R("GoldNumemon") },

        // Champion -> Ultimate
        ["Greymon"] = new[] { B("MetalGreymon", Off), B("RizeGreymon", Spd), R("SkullGreymon") },
        ["GeoGreymon"] = new[] { B("RizeGreymon", Off), B("MetalGreymon", Def), R("SkullGreymon") },
        ["BlackGreymon"] = new[] { B("BlackMetalGreymon", Off), R("SkullGreymon") },
        ["Tyrannomon"] = new[] { B("MetalTyrannomon", Off), B("Mamemon", Def), R("SkullGreymon") },
        ["Garurumon"] = new[] { B("WereGarurumon", Off), B("MachGaogamon", Spd), R("ShadowWereGarurumon") },
        ["BlackGarurumon"] = new[] { B("ShadowWereGarurumon", Off) },
        ["Angemon"] = new[] { B("MagnaAngemon", Bra), B("Piximon", Spd), R("LadyDevimon") },
        ["Gatomon"] = new[] { B("Angewomon", Bra), B("Lilamon", Spd), R("LadyDevimon") },
        ["BlackGatomon"] = new[] { B("LadyDevimon", Bra) },
        ["Togemon"] = new[] { B("Lilamon", Bra), B("Cherrymon", Def) },
        ["Woodmon"] = new[] { B("Cherrymon", Def), R("SkullMeramon") },
        ["Sunflowmon"] = new[] { B("Lilamon", Bra) },
        ["Vegiemon"] = new[] { B("Cherrymon", Def) },
        ["Birdramon"] = new[] { B("Garudamon", Off), R("SkullMeramon") },
        ["Aquilamon"] = new[] { B("Garudamon", Spd) },
        ["Ikkakumon"] = new[] { B("Zudomon", Def), B("Whamon", Bra) },
        ["Seadramon"] = new[] { B("MegaSeadramon", Off), B("Whamon", Def) },
        ["Gekomon"] = new[] { B("ShogunGekomon", Bra) },
        ["ExVeemon"] = new[] { B("Paildramon", Off), B("AeroVeedramon", Spd) },
        ["Veedramon"] = new[] { B("AeroVeedramon", Def) },
        ["Devimon"] = new[] { B("LadyDevimon", Bra), B("Kimeramon", Off), B("SkullMeramon", Spd) },
        ["Meramon"] = new[] { B("SkullMeramon", Off) },
        ["Numemon"] = new[] { B("Monzaemon", Def), R("BlackKingNumemon") },
        ["Sukamon"] = new[] { B("Etemon", Off) },
        ["PlatinumSukamon"] = new[] { B("Etemon", Off) },
        ["Starmon"] = new[] { B("SuperStarmon", Spd) },
        ["Guardromon"] = new[] { B("Andromon", Def), B("MetalMamemon", Off) },
        ["Clockmon"] = new[] { B("Andromon", Bra), B("MetalMamemon", Def) },
        ["Stingmon"] = new[] { B("Okuwamon", Off) },
        ["Kuwagamon"] = new[] { B("Okuwamon", Off) },
        ["Kyubimon"] = new[] { B("Taomon", Bra) },
        ["Growlmon"] = new[] { B("WarGrowlmon", Off) },
        ["Gaogamon"] = new[] { B("MachGaogamon", Off) },
        ["Gargomon"] = new[] { B("Rapidmon", Spd) },
        ["Leomon"] = new[] { B("GrapLeomon", Off), B("Pandamon", Def) },
        ["Dorugamon"] = new[] { B("DoruGreymon", Off) },
        ["Coredramon Green"] = new[] { B("Groundramon", Off) },
        ["Coredramon Blue"] = new[] { B("Wingdramon", Spd) },
        ["Ginryumon"] = new[] { B("Hisyaryumon", Def) },
        ["Lavorvomon"] = new[] { B("Lavogaritamon", Off) },
        ["Jazardmon"] = new[] { B("Jazarichmon", Spd) },
        ["ZubaEagermon"] = new[] { B("Duramon", Off) },
        ["Meicoomon"] = new[] { B("Meicrackmon Vicious Mode", Off) },
        ["Wizardmon"] = new[] { B("Mamemon", Bra), B("Vademon", Spd) },
        ["Ogremon"] = new[] { B("Etemon", Off) },
        ["Bakemon"] = new[] { B("Vademon", Bra) },

        // Ultimate -> Mega
        ["MetalGreymon"] = new[] { B("WarGreymon", Off), B("BlitzGreymon", Spd) },
        ["BlackMetalGreymon"] = new[] { B("BlackWarGreymon", Off) },
        ["RizeGreymon"] = new[] { B("ShineGreymon", Off) },
        ["SkullGreymon"] = new[] { B("Machinedramon", Def) },
        ["WereGarurumon"] = new[] { B("MetalGarurumon", Spd), B("CresGarurumon", Off) },
        ["ShadowWereGarurumon"] = new[] { B("BlackMetalGarurumon", Spd) },
        ["MagnaAngemon"] = new[] { B("Seraphimon", Bra) },
        ["Angewomon"] = new[] { B("Ophanimon", Bra), B("Magnadramon", Spd) },
        ["LadyDevimon"] = new[] { B("Lilithmon", Bra) },
        ["Lilamon"] = new[] { B("Rosemon", Bra) },
        ["Cherrymon"] = new[] { B("Puppetmon", Bra) },
        ["Garudamon"] = new[] { B("Phoenixmon", Spd) },
        ["Zudomon"] = new[] { B("Vikemon", Def) },
        ["MegaSeadramon"] = new[] { B("MetalSeadramon", Off) },
        ["Paildramon"] = new[] { B("Imperialdramon Fighter Mode", Off) },
        ["AeroVeedramon"] = new[] { B("UlforceVeedramon", Spd) },
        ["Etemon"] = new[] { B("KingEtemon", Off), B("MetalEtemon", Def) },
        ["Monzaemon"] = new[] { B("BanchoLeomon", Off) },
        ["Andromon"] = new[] { B("HiAndromon", Def) },
        ["Okuwamon"] = new[] { B("GranKuwagamon", Off) },
        ["Taomon"] = new[] { B("Sakuyamon", Bra) },
        ["WarGrowlmon"] = new[] { B("Gallantmon", Off) },
        ["MachGaogamon"] = new[] { B("MirageGaogamon", Off) },
        ["GrapLeomon"] = new[] { B("BanchoLeomon", Off) },
        ["DoruGreymon"] = new[] { B("Alphamon", Off) },
        ["Groundramon"] = new[] { B("Breakdramon", Def) },
        ["Wingdramon"] = new[] { B("Slayerdramon", Spd) },
        ["Hisyaryumon"] = new[] { B("Ouryumon", Def) },
        ["Duramon"] = new[] { B("Durandamon", Off) },
        ["MetalTyrannomon"] = new[] { B("RustTyranomon", Off), B("Machinedramon", Def) },
        ["Megadramon"] = new[] { B("Machinedramon", Off) },
        ["Gigadramon"] = new[] { B("Machinedramon", Off) },
        ["Mamemon"] = new[] { B("PrinceMamemon", Off) },
        ["MetalMamemon"] = new[] { B("PrinceMamemon", Def) },
        ["Kimeramon"] = new[] { B("Millenniummon", Off) },
        ["SkullMeramon"] = new[] { B("Boltmon", Off) },
        ["Meicrackmon Vicious Mode"] = new[] { B("Raguelmon", Off) },
        ["ShogunGekomon"] = new[] { B("Vikemon", Def) },
    };

    // ------------------------------------------------------------ Regras

    /// <summary>Nível que o Digimon precisa ter para sair do estágio atual (0 = não digivolve mais).</summary>
    public static int NivelParaDigivolver(DigimonStage atual)
    {
        switch (atual)
        {
            case DigimonStage.Fresh: return 3;
            case DigimonStage.InTraining: return 6;
            case DigimonStage.Rookie: return 14;
            case DigimonStage.Champion:
            case DigimonStage.Armor: return 26;
            case DigimonStage.Ultimate: return 40;
            default: return 0;
        }
    }

    private static DigimonStage? Proximo(DigimonStage e)
    {
        switch (e)
        {
            case DigimonStage.Fresh: return DigimonStage.InTraining;
            case DigimonStage.InTraining: return DigimonStage.Rookie;
            case DigimonStage.Rookie: return DigimonStage.Champion;
            case DigimonStage.Champion:
            case DigimonStage.Armor: return DigimonStage.Ultimate;
            case DigimonStage.Ultimate: return DigimonStage.Mega;
            default: return null;
        }
    }

    // Exigências dos caminhos "bons", pelo estágio de destino.
    private static int TreinoMin(DigimonStage e) => e == DigimonStage.Champion ? 15 : e == DigimonStage.Ultimate ? 35 : 60;
    private static int AmizadeMin(DigimonStage e) => e == DigimonStage.Champion ? 20 : e == DigimonStage.Ultimate ? 45 : 75;
    private static int VitoriasMin(DigimonStage e) => e == DigimonStage.Champion ? 3 : e == DigimonStage.Ultimate ? 10 : 25;
    private static int ErrosMax(DigimonStage e) => e == DigimonStage.Champion ? 3 : e == DigimonStage.Ultimate ? 5 : 8;
    private const int ErrosParaRuim = 5;

    /// <summary>Hora de digivolver? Nível (ou idade, nos bebês).</summary>
    public static bool ChegouAHora(DigimonSalvo d)
    {
        var ficha = d?.Ficha;
        if (ficha == null) return false;
        var nivel = NivelParaDigivolver(ficha.Stage);
        if (nivel <= 0) return false;
        if (d.nivel >= nivel) return true;
        // Bebês crescem com o tempo, como num V-Pet.
        var idadeMin = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (d.nasceuEm > 0 ? d.nasceuEm : d.capturadoEm)) / 60f;
        return ficha.Stage == DigimonStage.Fresh && idadeMin >= 15f ||
               ficha.Stage == DigimonStage.InTraining && idadeMin >= 120f;
    }

    /// <summary>Para quem o Digimon digivolve agora, ou null se ainda não é hora / nenhum caminho foi cumprido.</summary>
    public static DigimonBase Verificar(DigimonSalvo d)
    {
        if (!ChegouAHora(d)) return null;
        return Escolher(d, out _);
    }

    /// <summary>Escolhe o caminho pelas condições (o que cumprir mais vence; a maioria precisa ser cumprida).</summary>
    private static DigimonBase Escolher(DigimonSalvo d, out string motivo)
    {
        motivo = null;
        var ficha = d.Ficha;
        var alvo = Proximo(ficha.Stage);
        if (alvo == null) return null;

        DigimonBase melhor = null;
        var melhorNota = -1f;
        foreach (var c in Caminhos(ficha, alvo.Value))
        {
            var destino = CatalogoDigimon.Buscar(c.Para);
            if (destino == null || destino.Prefab == null) continue;
            var nota = Nota(d, c, destino.Stage);
            if (nota > melhorNota) { melhorNota = nota; melhor = destino; }
        }

        if (melhor != null && melhorNota >= 0.66f) return melhor;

        // Nenhum caminho cumprido.
        if (alvo == DigimonStage.Champion)
        {
            var falha = d.errosCuidado >= 8 ? CatalogoDigimon.Buscar("Sukamon") : CatalogoDigimon.Buscar("Numemon");
            if (falha != null) return falha;
        }
        motivo = "precisa de mais treino, amizade ou vitórias";
        return null;
    }

    private static float Nota(DigimonSalvo d, Caminho c, DigimonStage destino)
    {
        switch (c.Tipo)
        {
            case Tipo.Livre:
                // Sempre possível; o status mais treinado escolhe entre as opções.
                return 0.7f + (c.Foco.HasValue ? CuidadoDigimon.Treino(d, c.Foco.Value) / 1000f : 0f);
            case Tipo.Ruim:
                return d.errosCuidado >= ErrosParaRuim ? 0.8f : 0f;
            default:
                var total = 3;
                var ok = 0;
                if (d.amizade >= AmizadeMin(destino)) ok++;
                if (d.vitorias >= VitoriasMin(destino)) ok++;
                if (d.errosCuidado <= ErrosMax(destino)) ok++;
                if (c.Foco.HasValue)
                {
                    total++;
                    if (CuidadoDigimon.Treino(d, c.Foco.Value) >= TreinoMin(destino)) ok++;
                }
                // Desempate: quem tem mais treino no status do caminho.
                return ok / (float)total + (c.Foco.HasValue ? CuidadoDigimon.Treino(d, c.Foco.Value) / 10000f : 0f);
        }
    }

    /// <summary>Caminhos da árvore; sem entrada, os Digimon do próximo estágio com o mesmo elemento.</summary>
    private static IEnumerable<Caminho> Caminhos(DigimonBase ficha, DigimonStage alvo)
    {
        if (Arvore.TryGetValue(ficha.name, out var lista) || Arvore.TryGetValue(ficha.Name ?? "", out lista))
            return lista;

        var mesmos = CatalogoDigimon.Todos.Distinct()
            .Where(f => f != null && f.Prefab != null && f.Stage == alvo && f.Atribute == ficha.Atribute)
            .OrderBy(f => f.name, StringComparer.Ordinal).ToList();
        if (mesmos.Count == 0)
            mesmos = CatalogoDigimon.Todos.Distinct().Where(f => f != null && f.Prefab != null && f.Stage == alvo)
                .OrderBy(f => f.name, StringComparer.Ordinal).ToList();
        if (mesmos.Count == 0) return Array.Empty<Caminho>();
        // Até 3 opções, sempre as mesmas para cada espécie; cada uma pede um status diferente.
        var inicio = (int)((uint)ficha.name.GetHashCode() % (uint)mesmos.Count);
        var focos = new[] { Off, Spd, Bra, Def };
        var tipo = alvo <= DigimonStage.Rookie ? Tipo.Livre : Tipo.Bom;
        return Enumerable.Range(0, Mathf.Min(3, mesmos.Count))
            .Select(i => new Caminho { Para = mesmos[(inicio + i) % mesmos.Count].name, Tipo = tipo, Foco = focos[i % focos.Length] });
    }

    /// <summary>Dica para a tela de cuidado: quando e como vai digivolver.</summary>
    public static string Dica(DigimonSalvo d)
    {
        var ficha = d?.Ficha;
        if (ficha == null) return "";
        var nivel = NivelParaDigivolver(ficha.Stage);
        if (nivel <= 0) return "Estágio final";
        if (!ChegouAHora(d)) return $"Digivolui no nível {nivel}";
        return Escolher(d, out var motivo) != null ? "Pronto para digivolver!" : $"Nível certo, mas {motivo}";
    }

    /// <summary>Aplica a digivolução: a espécie muda, o resto (nível, treino, amizade) continua.</summary>
    public static void Aplicar(DigimonSalvo d, DigimonBase nova)
    {
        if (d == null || nova == null) return;
        d.especie = nova.name;
        d.nasceuEm = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        CuidadoDigimon.Mudar(ref d.humor, 20f);
        CuidadoDigimon.Mudar(ref d.energia, 30f);
        PerfilJogador.Atual.Salvar();
    }
}
