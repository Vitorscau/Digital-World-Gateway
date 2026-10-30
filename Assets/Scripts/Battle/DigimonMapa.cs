using UnityEngine;
using UnityEngine.SceneManagement;

public class DigimonMapa : MonoBehaviour
{
    public string digimonID;
    public GameObject prefabParaBatalha;

    private void OnMouseDown()
    {
        // Digimon selvagem do mapa: só batalha se o jogador estiver perto, e depois ele some.
        var selvagem = GetComponent<WildDigimon>();
        if (selvagem != null)
        {
            if (!selvagem.PodeEncontrar)
            {
                Debug.Log($"{selvagem.Especie} está longe demais. Chegue mais perto para batalhar.");
                return;
            }
            selvagem.RegistrarEncontro();
        }

        // Define qual digimon foi clicado
        BattleData.digimonClicadoID = digimonID;

        // Se quiser passar o prefab diretamente:
        BattleData.digimonDoJogadorPrefab = prefabParaBatalha;

        // Vai para a cena de batalha
        SceneManager.LoadScene("AR_Battle_Scene");
    }
}
