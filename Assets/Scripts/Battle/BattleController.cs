using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleController : MonoBehaviour
{
    public static BattleController Instance { get; private set; }

    [SerializeField] private string battleSceneName = "AR_Battle_Scene";
    [SerializeField] private string mapSceneName = "Map_Scene";

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

    public void StartBattle() // Renamed from SetBattleActive
    {
        SceneManager.LoadScene(battleSceneName);
    }

    public void ReturnToMap()
    {
        Debug.Log("voltando para o mapa");
        SceneManager.LoadScene("Map_Scene");
        

    }
     public void ReturnToAR()
    {
        SceneManager.LoadScene("AR_Scene");
    }
}