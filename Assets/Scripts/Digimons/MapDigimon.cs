using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Toque num Digimon para batalhar (sistema antigo, via GameManager).
/// Digimons selvagens do spawner (com WildDigimon) são tratados pelo próprio WildDigimon.
/// </summary>
public class MapDigimon : MonoBehaviour
{
    public DigimonBase digimonBase;
    public int level = 5; // Default level

    private void OnMouseDown()
    {
        if (GetComponent<WildDigimon>() != null) return; // o WildDigimon cuida do encontro

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetBattleData(GameManager.Instance.PlayerDigimon, digimonBase, level);
            SceneManager.LoadScene(Cenas.Batalha); // antes: "BattleScene", que não existe
        }
    }
}
