using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Um Digimon com nível (ficha + nível). Status no estilo Digimon World:
/// HP, MP, Offense (ataque físico), Defense, Speed (velocidade) e Brains (inteligência:
/// técnicas mágicas e decisões na luta).
/// </summary>
public class Digimon
{
    public DigimonBase Base { get; set; }
    public int Level { get; set; }

    public int HP { get; set; }

    public List<DigimonMove> Moves { get; set; }

    public Digimon(DigimonBase dBase, int dLevel)
    {
        Base = dBase;
        Level = dLevel;
        HP = MaxHp;

        Moves = new List<DigimonMove>();
        foreach (var move in Base.LearnableMoves)
        {
            if (move.movebase == null) continue;
            if (move.level <= Level)
                Moves.Add(new DigimonMove(move.movebase));

            if (Moves.Count >= 4)
                break;
        }
    }

    private int Escala(int baseStat, int extra) => Mathf.FloorToInt((baseStat * Level + (Base.Ability / 4)) / 100f) + extra;

    // ------------------------------------------------------------ Status (estilo Digimon World)

    public int MaxHp => Escala(Base.MaxHp, 10);
    /// <summary>Pontos para usar técnicas. Vem do SP da ficha.</summary>
    public int MaxMp => Escala(Base.SpecialPoints, 8);
    /// <summary>Força dos golpes físicos.</summary>
    public int Offense => Escala(Base.Attack, 5);
    public int Defense => Escala(Base.Defense, 5);
    /// <summary>Velocidade: agir mais vezes, andar mais rápido e desviar.</summary>
    public int Speed => Escala(Base.SpecialDefense, 5); // o campo "SPD" da ficha
    /// <summary>Inteligência: força das técnicas mágicas e qualidade das decisões na luta.</summary>
    public int Brains => Escala(Base.Inteligence, 5);

    /// <summary>
    /// Quão bem ele escolhe o que fazer (0 a 1), pela inteligência da espécie.
    /// Um Digimon "burro" erra a técnica, gasta MP à toa e quase não se defende.
    /// </summary>
    public float Esperteza
    {
        get
        {
            var e = 0.25f + Base.Inteligence / 130f;
            if (Personalidade == DigimonPersonalities.Brainy) e += 0.1f;
            return Mathf.Clamp(e, 0.3f, 0.95f);
        }
    }

    /// <summary>
    /// Jeito de lutar. Se a ficha não tiver um definido (Durable é o padrão), vem do status mais forte da espécie.
    /// </summary>
    public DigimonPersonalities Personalidade
    {
        get
        {
            if (Base.Personalities != DigimonPersonalities.Durable) return Base.Personalities;
            // Compara cada status com o valor típico (Offense costuma ser o maior de todos).
            var atk = Base.Attack / 46f;
            var def = Base.Defense / 36f;
            var inte = Base.Inteligence / 32f;
            var spd = Base.SpecialDefense / 42f;
            var hp = Base.MaxHp / 450f;
            var maior = Mathf.Max(atk, def, inte, spd, hp);
            var menor = Mathf.Min(atk, def, inte, spd, hp);
            if (maior - menor < 0.08f)
            {
                // Equilibrado: um jeito próprio da espécie, sempre o mesmo.
                var h = (uint)(Base.name ?? "").GetHashCode() % 3;
                return h == 0 ? DigimonPersonalities.Lively : h == 1 ? DigimonPersonalities.Builder : DigimonPersonalities.Searcher;
            }
            if (maior == atk) return DigimonPersonalities.Fighter;
            if (maior == inte) return DigimonPersonalities.Brainy;
            if (maior == spd) return DigimonPersonalities.Nimble;
            if (maior == def) return DigimonPersonalities.Defender;
            return DigimonPersonalities.Durable;
        }
    }

    // ------------------------------------------------------------ Nomes antigos (usados por outras telas)

    public int Attack => Offense;
    public int Inteligence => Brains;
    public int SpecialPoints => MaxMp;
    public int SpecialDefense => Mathf.FloorToInt((Defense + Brains) / 2f);
}
