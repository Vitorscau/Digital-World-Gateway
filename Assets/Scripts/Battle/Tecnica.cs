using System.Collections.Generic;
using UnityEngine;

public enum TipoTecnica
{
    /// <summary>Golpe corpo a corpo: Offense contra Defense.</summary>
    Fisica,
    /// <summary>Disparo à distância: Brains contra Defense/Brains. Pode ser desviado.</summary>
    Magica,
    /// <summary>Melhora o próprio Digimon por um tempo (ex.: Attack Charge).</summary>
    Suporte
}

/// <summary>
/// Uma técnica usada na arena. Vem dos golpes da ficha (MoveBase) ou das técnicas do elemento do Digimon,
/// para todo Digimon ter um repertório de luta mesmo com poucos golpes cadastrados.
/// </summary>
public class Tecnica
{
    public string Nome;
    public int Poder;
    public int CustoMP;
    public TipoTecnica Tipo;
    public DigimonAttribute Atributo;
    /// <summary>Golpe básico (sem custo, sem nome na tela).</summary>
    public bool Basica;

    public bool ADistancia => Tipo == TipoTecnica.Magica;

    public static Tecnica Basico(DigimonAttribute atributo) =>
        new Tecnica { Nome = "Ataque", Poder = 30, CustoMP = 0, Tipo = TipoTecnica.Fisica, Atributo = DigimonAttribute.Neutral, Basica = true };

    public static Tecnica DeGolpe(MoveBase m)
    {
        if (m == null) return null;
        var nome = string.IsNullOrWhiteSpace(m.Name) ? m.name : m.Name.Trim();
        TipoTecnica tipo;
        switch (m.Type)
        {
            case MoveType.Support: tipo = TipoTecnica.Suporte; break;
            case MoveType.Magic: tipo = TipoTecnica.Magica; break;
            default: tipo = TipoTecnica.Fisica; break;
        }
        return new Tecnica { Nome = nome, Poder = m.Power, CustoMP = Mathf.Max(0, m.Cost), Tipo = tipo, Atributo = m.Atribute };
    }

    /// <summary>Técnicas do elemento: uma básica para todos e uma forte a partir do estágio Champion.</summary>
    public static IEnumerable<Tecnica> DoElemento(DigimonAttribute a, DigimonStage estagio)
    {
        var forte = estagio >= DigimonStage.Champion && estagio != DigimonStage.Fresh && estagio != DigimonStage.InTraining;
        switch (a)
        {
            case DigimonAttribute.Fire:
                yield return M("Fire Ball", 60, 4, a);
                if (forte) yield return M("Mega Flame", 95, 8, a);
                break;
            case DigimonAttribute.Water:
                yield return M("Bubble Shot", 55, 3, a);
                if (forte) yield return M("Ice Storm", 90, 8, a);
                break;
            case DigimonAttribute.Plant:
                yield return F("Poison Ivy", 65, 4, a);
                if (forte) yield return M("Thorn Whip", 90, 8, a);
                break;
            case DigimonAttribute.Electric:
                yield return M("Spark Wave", 60, 4, a);
                if (forte) yield return M("Thunder Justice", 95, 9, a);
                break;
            case DigimonAttribute.Earth:
                yield return F("Rock Crusher", 70, 4, a);
                if (forte) yield return F("Earth Quake", 95, 8, a);
                break;
            case DigimonAttribute.Wind:
                yield return M("Air Blade", 55, 3, a);
                if (forte) yield return M("Tornado", 90, 8, a);
                break;
            case DigimonAttribute.Light:
                yield return M("Holy Light", 65, 5, a);
                if (forte) yield return M("Heaven's Knuckle", 100, 9, a);
                break;
            case DigimonAttribute.Dark:
                yield return M("Dark Shot", 65, 5, a);
                if (forte) yield return M("Death Claw", 100, 9, a);
                break;
            default:
                yield return F("Heavy Strike", 60, 3, a);
                if (forte) yield return F("Power Crane", 90, 8, a);
                break;
        }
    }

    private static Tecnica M(string n, int p, int c, DigimonAttribute a) =>
        new Tecnica { Nome = n, Poder = p, CustoMP = c, Tipo = TipoTecnica.Magica, Atributo = a };

    private static Tecnica F(string n, int p, int c, DigimonAttribute a) =>
        new Tecnica { Nome = n, Poder = p, CustoMP = c, Tipo = TipoTecnica.Fisica, Atributo = a };

    /// <summary>Tudo o que o Digimon sabe usar na arena: golpe básico + golpes da ficha + técnicas do elemento.</summary>
    public static List<Tecnica> Repertorio(Digimon d)
    {
        var lista = new List<Tecnica> { Basico(d.Base.Atribute) };
        var nomes = new HashSet<string>();
        if (d.Moves != null)
            foreach (var m in d.Moves)
            {
                var t = DeGolpe(m?.Base);
                if (t == null || string.IsNullOrEmpty(t.Nome) || !nomes.Add(t.Nome)) continue;
                if (t.Tipo != TipoTecnica.Suporte && t.Poder <= 0) continue;
                lista.Add(t);
            }
        foreach (var t in DoElemento(d.Base.Atribute, d.Base.Stage))
            if (nomes.Add(t.Nome)) lista.Add(t);
        return lista;
    }
}
