using TMPro;
using UnityEngine;

public class BattleHud : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private HPBar hpBar;

    public void SetData(Digimon digimon)
    {
        nameText.text = digimon.Base.Name;
        levelText.text = "Lv." + digimon.Level;
        hpBar.SetMaxHP(digimon.MaxHp);
        hpBar.SetHP(digimon.HP);
    }
}