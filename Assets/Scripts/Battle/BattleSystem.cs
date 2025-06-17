using UnityEngine;
using UnityEngine.SceneManagement;
public class BattleSystem : MonoBehaviour
{
    [SerializeField] private BattleUnit playerUnit;
    [SerializeField] private BattleHud playerHud;
    [SerializeField] private BattleUnit enemyUnit;
    [SerializeField] private BattleHud enemyHud;

    private void Start()
    {
        InitializeBattle(); // Changed from SetupBattle
    }

    public void InitializeBattle() // Made public
    {
        if (GameManager.Instance == null) return;

        playerUnit.Initialize(GameManager.Instance.PlayerDigimon, 5);
        enemyUnit.Initialize(GameManager.Instance.EnemyDigimon, GameManager.Instance.EnemyLevel);

        playerHud.SetData(playerUnit.digimon);
        enemyHud.SetData(enemyUnit.digimon);
    }

    public void LoadMapScene()
    {
        SceneManager.LoadScene("Map_Scene");
    }
}