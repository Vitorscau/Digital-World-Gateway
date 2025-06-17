using UnityEngine;

public class ARBattleManager : MonoBehaviour
{
    public Transform spawnPlayer;
    public Transform spawnEnemy;

    public GameObject playerPrefab;
    public GameObject[] listaDeInimigos; // Digimons inimigos cadastrados com ID

    void Start()
    {
        // Instancia o jogador
        GameObject player = Instantiate(BattleData.digimonDoJogadorPrefab, spawnPlayer.position, Quaternion.identity);
        CriaturaArena arenaPlayer = player.GetComponent<CriaturaArena>();
        if (arenaPlayer != null) arenaPlayer.enabled = true;

        // Busca o inimigo pelo ID salvo
        GameObject prefabInimigo = BuscarInimigoPorID(BattleData.digimonClicadoID);
        GameObject inimigo = Instantiate(prefabInimigo, spawnEnemy.position, Quaternion.identity);
        CriaturaArena arenaInimigo = inimigo.GetComponent<CriaturaArena>();
        if (arenaInimigo != null) arenaInimigo.enabled = true;
    }

    GameObject BuscarInimigoPorID(string id)
    {
        foreach (GameObject digimon in listaDeInimigos)
        {
            DigimonIdentidade identidade = digimon.GetComponent<DigimonIdentidade>();
            if (identidade != null && identidade.digimonID == id)
            {
                return digimon;
            }
        }

        Debug.LogError("Inimigo com ID não encontrado: " + id);
        return null;
    }
}
