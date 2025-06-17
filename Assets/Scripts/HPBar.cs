using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HPBar : MonoBehaviour
{
    [Header("Componentes UI")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text hpText;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponent<Slider>();
    }

    public void Initialize(string digimonName, int level, int maxHP)
    {
        if (nameText != null)
            nameText.text = digimonName;

        if (levelText != null)
            levelText.text = $"Lv.{level}";

        SetMaxHP(maxHP);
    }

    public void SetMaxHP(int maxHP)
    {
        if (slider != null)
        {
            slider.maxValue = maxHP;
            slider.value = maxHP;
        }

        UpdateHPText();
    }

    public void SetHP(int currentHP)
    {
        if (slider != null)
        {
            slider.value = currentHP;
        }

        UpdateHPText();
    }

    private void UpdateHPText()
    {
        if (hpText != null && slider != null)
        {
            hpText.text = $"{slider.value}/{slider.maxValue}";
        }
    }
}
