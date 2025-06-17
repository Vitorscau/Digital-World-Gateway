using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
            if (move.level <= Level)
                Moves.Add(new DigimonMove(move.movebase));

            if (Moves.Count >= 4)
                break;
        }
    }

    public int Attack
    {
        get { return Mathf.FloorToInt((Base.Attack * Level+(Base.Ability/4)) / 100f) + 5; }
    }
    public int Defense
    {
        get { return Mathf.FloorToInt((Base.Defense * Level + (Base.Ability / 4)) / 100f) + 5; }
    }
    public int MaxHp
    {
        get { return Mathf.FloorToInt((Base.MaxHp * Level + (Base.Ability / 4)) / 100f) + 10; }
    }
    public int Inteligence
    {
        get { return Mathf.FloorToInt((Base.Inteligence * Level + (Base.Ability / 4)) / 100f) + 5; }
    }
    public int SpecialPoints
    {
        get { return Mathf.FloorToInt((Base.SpecialPoints * Level + (Base.Ability / 4)) / 100f) + 5; }
    }
    public int SpecialDefense
    {
        get { return Mathf.FloorToInt((Base.SpecialDefense * Level + (Base.Ability / 4)) / 100f) + 5; }
    }
}