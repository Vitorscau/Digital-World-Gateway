using UnityEngine;
using UnityEngine.UI;

public class BattleButton : MonoBehaviour
{
    private Button button;

    private void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(() => {
            BattleController.Instance.StartBattle(); // Updated method call
        });
    }
}