using UnityEngine;

/// <summary>Fórmulas de combate: dano, vantagem de tipo e de atributo, crítico, desvio.</summary>
public static class CalculoDano
{
    /// <summary>Poder de um ataque normal e de um especial (usado pela fórmula antiga).</summary>
    public const float PoderNormal = 30f;
    public const float PoderEspecial = 65f;

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
        return vence ? 1.3f : 0.85f;
    }

    /// <summary>Atributos: Fogo > Planta > Água > Fogo; Luz e Trevas se anulam com mais força.</summary>
    public static float VantagemAtributo(DigimonAttribute atacante, DigimonAttribute defensor)
    {
        bool V(DigimonAttribute a, DigimonAttribute d) => atacante == a && defensor == d;
        if (V(DigimonAttribute.Fire, DigimonAttribute.Plant) || V(DigimonAttribute.Plant, DigimonAttribute.Water) ||
            V(DigimonAttribute.Water, DigimonAttribute.Fire) || V(DigimonAttribute.Electric, DigimonAttribute.Water) ||
            V(DigimonAttribute.Wind, DigimonAttribute.Plant) || V(DigimonAttribute.Earth, DigimonAttribute.Electric) ||
            V(DigimonAttribute.Light, DigimonAttribute.Dark) || V(DigimonAttribute.Dark, DigimonAttribute.Light))
            return 1.15f;
        if (V(DigimonAttribute.Plant, DigimonAttribute.Fire) || V(DigimonAttribute.Water, DigimonAttribute.Plant) ||
            V(DigimonAttribute.Fire, DigimonAttribute.Water) || V(DigimonAttribute.Water, DigimonAttribute.Electric) ||
            V(DigimonAttribute.Electric, DigimonAttribute.Earth))
            return 0.9f;
        return 1f;
    }

    /// <summary>
    /// Dano médio de uma técnica (sem sorte nem crítico). Usado pela IA para comparar opções.
    /// Física: Offense contra Defense. Mágica: Brains contra a média de Defense e Brains do alvo.
    /// </summary>
    public static float Estimar(Digimon atacante, Digimon defensor, Tecnica t, float bonusAtaque = 1f)
    {
        if (t == null || t.Tipo == TipoTecnica.Suporte) return 0f;
        float atk, def;
        if (t.Tipo == TipoTecnica.Magica)
        {
            atk = atacante.Brains;
            def = (defensor.Defense + defensor.Brains) * 0.5f;
        }
        else
        {
            atk = atacante.Offense * bonusAtaque;
            def = defensor.Defense;
        }
        var razao = Mathf.Pow(Mathf.Max(1f, atk) / Mathf.Max(1f, def), 0.75f);
        var nivel = (2f + atacante.Level) / 7f;
        var bruto = t.Poder * 0.075f * razao * nivel;
        // Técnica do mesmo elemento do Digimon é mais forte.
        if (!t.Basica && t.Atributo == atacante.Base.Atribute && t.Atributo != DigimonAttribute.Neutral) bruto *= 1.2f;
        var atributo = t.Basica ? atacante.Base.Atribute : t.Atributo;
        return bruto * VantagemTipo(atacante.Base.Type, defensor.Base.Type) * VantagemAtributo(atributo, defensor.Base.Atribute);
    }

    public static int Calcular(Digimon atacante, Digimon defensor, Tecnica t, bool critico, float bonusAtaque = 1f)
    {
        var dano = Estimar(atacante, defensor, t, bonusAtaque) * Random.Range(0.9f, 1.1f) * (critico ? 1.5f : 1f);
        return Mathf.Max(1, Mathf.RoundToInt(dano));
    }

    /// <summary>Fórmula antiga (ataque normal ou especial), mantida para quem ainda usa.</summary>
    public static int Calcular(Digimon atacante, Digimon defensor, bool especial)
    {
        var t = especial
            ? new Tecnica { Nome = "Special", Poder = (int)PoderEspecial, Tipo = TipoTecnica.Magica, Atributo = atacante.Base.Atribute }
            : Tecnica.Basico(atacante.Base.Atribute);
        return Calcular(atacante, defensor, t, false);
    }

    /// <summary>Chance de crítico: um pouco maior para quem é mais esperto e mais rápido.</summary>
    public static float ChanceCritico(Digimon a, Digimon d)
    {
        var c = 0.05f + 0.06f * Relativo(a.Speed, d.Speed) + 0.04f * a.Esperteza;
        if (a.Personalidade == DigimonPersonalities.Searcher) c += 0.07f;
        c += a.Amizade * 0.0008f; // parceiro amigo acerta mais em cheio
        return Mathf.Clamp(c, 0.03f, 0.25f);
    }

    /// <summary>Chance de desviar: depende de quanto o defensor é mais rápido que o atacante.</summary>
    public static float ChanceDesvio(Digimon atacante, Digimon defensor, Tecnica t)
    {
        var c = 0.05f + 0.3f * Mathf.Max(0f, Relativo(defensor.Speed, atacante.Speed) - 0.5f) * 2f;
        if (defensor.Personalidade == DigimonPersonalities.Nimble) c += 0.08f;
        if (t != null && t.ADistancia) c += 0.05f; // dá para ver o disparo vindo
        return Mathf.Clamp(c, 0.02f, 0.35f);
    }

    /// <summary>a / (a + b): 0,5 quando iguais.</summary>
    public static float Relativo(float a, float b) => a / Mathf.Max(1f, a + b);
}
