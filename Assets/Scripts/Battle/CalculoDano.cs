using UnityEngine;

/// <summary>Fórmulas de combate: dano, vantagem de tipo e de atributo.</summary>
public static class CalculoDano
{
    /// <summary>Poder de um ataque normal e de um especial (antes eram 10 e 20 fixos).</summary>
    public const float PoderNormal = 4f;
    public const float PoderEspecial = 8f;

    /// <summary>
    /// Triângulo de tipos de Digimon: Vacina vence Vírus, Vírus vence Data, Data vence Vacina.
    /// Free é neutro.
    /// </summary>
    public static float VantagemTipo(DigimonType atacante, DigimonType defensor)
    {
        if (atacante == DigimonType.Free || defensor == DigimonType.Free || atacante == defensor) return 1f;
        var vence = (atacante == DigimonType.Vaccine && defensor == DigimonType.Virus) ||
                    (atacante == DigimonType.Virus && defensor == DigimonType.Data) ||
                    (atacante == DigimonType.Data && defensor == DigimonType.Vaccine);
        return vence ? 1.5f : 0.75f;
    }

    /// <summary>Atributos: Fogo > Planta > Água > Fogo; Luz e Trevas se anulam com mais força.</summary>
    public static float VantagemAtributo(DigimonAttribute atacante, DigimonAttribute defensor)
    {
        bool V(DigimonAttribute a, DigimonAttribute d) => atacante == a && defensor == d;
        if (V(DigimonAttribute.Fire, DigimonAttribute.Plant) || V(DigimonAttribute.Plant, DigimonAttribute.Water) ||
            V(DigimonAttribute.Water, DigimonAttribute.Fire) || V(DigimonAttribute.Electric, DigimonAttribute.Water) ||
            V(DigimonAttribute.Wind, DigimonAttribute.Plant) || V(DigimonAttribute.Earth, DigimonAttribute.Electric) ||
            V(DigimonAttribute.Light, DigimonAttribute.Dark) || V(DigimonAttribute.Dark, DigimonAttribute.Light))
            return 1.25f;
        if (V(DigimonAttribute.Plant, DigimonAttribute.Fire) || V(DigimonAttribute.Water, DigimonAttribute.Plant) ||
            V(DigimonAttribute.Fire, DigimonAttribute.Water) || V(DigimonAttribute.Water, DigimonAttribute.Electric) ||
            V(DigimonAttribute.Electric, DigimonAttribute.Earth))
            return 0.8f;
        return 1f;
    }

    public static int Calcular(Digimon atacante, Digimon defensor, bool especial)
    {
        var poder = especial ? PoderEspecial : PoderNormal;
        float atk = especial ? atacante.Inteligence : atacante.Attack;
        float def = especial ? defensor.SpecialDefense : defensor.Defense;
        var bruto = poder * (atk / Mathf.Max(1f, def)) * (1f + atacante.Level * 0.05f);
        var mult = VantagemTipo(atacante.Base.Type, defensor.Base.Type) *
                   VantagemAtributo(atacante.Base.Atribute, defensor.Base.Atribute) *
                   Random.Range(0.85f, 1.15f);
        return Mathf.Max(1, Mathf.RoundToInt(bruto * mult));
    }
}
