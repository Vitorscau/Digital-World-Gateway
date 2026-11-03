using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleController : MonoBehaviour
{
    public static BattleController Instance { get; private set; }

    [SerializeField] private string battleSceneName = Cenas.BatalhaAR;
    [SerializeField] private string mapSceneName = Cenas.Mapa;

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
        TelaCarregamento.Ir(battleSceneName);
    }

    public void ReturnToMap()
    {
        Debug.Log("voltando para o mapa");
        TelaCarregamento.Ir(mapSceneName); // antes ignorava o campo
        

    }
     public void ReturnToAR()
    {
        TelaCarregamento.Ir(Cenas.AR);
    }
}