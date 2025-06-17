using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "Move", menuName = "Digimon/Create New Move")]
public class MoveBase : ScriptableObject
{
    [SerializeField] string name_;

    [TextArea]
    [SerializeField] string description_;

    [SerializeField] DigimonAttribute atribute_;

    [SerializeField] int power_;

    [SerializeField] int cost_;

    [SerializeField] MoveType type_;

    public string Name
    {
        get { return name_; }
    }
    public string Description
    {
        get { return description_; }
    }
    public DigimonAttribute Atribute
    {
        get { return atribute_; }
    }
    public int Power
    {
        get { return power_; }
    }
    public int Cost
    {
        get { return cost_; }
    }
    public MoveType Type
    {
        get { return type_; }
    }

}
public enum MoveType
{
    Direct,
    Fixed,
    Magic,
    Physical,
    Support
   
}
