using UnityEngine;

public class TouchManager : MonoBehaviour
{
    [SerializeField] private BattleSystem battleSystem;

    public void OnBattleTrigger()
    {
        BattleController.Instance.StartBattle(); // Updated method call
        battleSystem.InitializeBattle(); // Updated method call
    }
}