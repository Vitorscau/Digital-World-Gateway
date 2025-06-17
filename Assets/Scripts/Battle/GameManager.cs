using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    public DigimonBase PlayerDigimon { get; private set; }
    public DigimonBase EnemyDigimon { get; private set; }
    public int EnemyLevel { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public void SetBattleData(DigimonBase playerDigimon, DigimonBase enemyDigimon, int enemyLevel)
    {
        PlayerDigimon = playerDigimon;
        EnemyDigimon = enemyDigimon;
        EnemyLevel = enemyLevel;
    }
}