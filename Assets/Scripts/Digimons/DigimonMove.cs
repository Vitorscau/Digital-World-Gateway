using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DigimonMove
{
    public MoveBase Base { get; set; }
    public int Cost { get; set; }

    public DigimonMove(MoveBase dBase)
    {
        Base = dBase;
        Cost = dBase.Cost;
    }
}
