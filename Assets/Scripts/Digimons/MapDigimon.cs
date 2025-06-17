using UnityEngine;
using UnityEngine.SceneManagement;

public class MapDigimon : MonoBehaviour
{
    public DigimonBase digimonBase;
    public int level = 5; // Default level

    private void OnMouseDown()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetBattleData(
                GameManager.Instance.PlayerDigimon,
                digimonBase,
                level
            );
            SceneManager.LoadScene("BattleScene");
        }
    }
}