using UnityEngine;
using UnityEngine.SceneManagement;

public class DigimonMapa : MonoBehaviour
{
    public string digimonID;
    public GameObject prefabParaBatalha;

    private void OnMouseDown()
    {
        // Define qual digimon foi clicado
        BattleData.digimonClicadoID = digimonID;

        // Se quiser passar o prefab diretamente:
        BattleData.digimonDoJogadorPrefab = prefabParaBatalha;

        // Vai para a cena de batalha
        SceneManager.LoadScene("AR_Battle_Scene");
    }
}
