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
            // Digimon selvagem do mapa: só batalha se o jogador estiver perto, e depois ele some.
            var selvagem = GetComponent<WildDigimon>();
            if (selvagem != null)
            {
                if (!selvagem.PodeEncontrar)
                {
                    Debug.Log($"{selvagem.Especie} está longe demais. Chegue mais perto para batalhar.");
                    return;
                }
                level = selvagem.Nivel;
                selvagem.RegistrarEncontro();
            }

            GameManager.Instance.SetBattleData(
                GameManager.Instance.PlayerDigimon,
                digimonBase,
                level
            );
            SceneManager.LoadScene("BattleScene");
        }
    }
}