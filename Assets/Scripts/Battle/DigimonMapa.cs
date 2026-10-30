using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Toque num Digimon para batalhar (sistema antigo, por ID).
/// Digimons selvagens do spawner (com WildDigimon) são tratados pelo próprio WildDigimon.
/// </summary>
public class DigimonMapa : MonoBehaviour
{
    public string digimonID;
    public GameObject prefabParaBatalha;

    private void OnMouseDown()
    {
        if (GetComponent<WildDigimon>() != null) return; // o WildDigimon cuida do encontro

        BattleData.digimonClicadoID = digimonID;
        BattleData.digimonDoJogadorPrefab = prefabParaBatalha;
        SceneManager.LoadScene(Cenas.BatalhaAR);
    }
}
