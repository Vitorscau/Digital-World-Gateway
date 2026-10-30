using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class BattleManager : MonoBehaviour
{
    [Header("Referências UI")]
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private GameObject defeatScreen;
    [SerializeField] private Button battleButton;

    [SerializeField] private GameObject battleCanvas;

    [Header("Contagem")]
    [SerializeField] private GameObject countdownCanvas;
    [SerializeField] private TMP_Text countdownText;

    [Header("Popup de Recompensa")]
    [SerializeField] private GameObject rewardPopup;
    [SerializeField] private Button okButton;

    [Header("Referências de Criaturas")]
    [SerializeField] private GameObject playerCreatureGO;
    [SerializeField] private GameObject enemyCreatureGO;
    private CriaturaArena playerCreature;
    private CriaturaArena enemyCreature;

    private NavMeshHowTo navMeshArena;
    private bool batalhaIniciada = false;
    private bool batalhaTerminada = false;

    private void Start()
    {
        navMeshArena = FindFirstObjectByType<NavMeshHowTo>();
        if (navMeshArena == null)
        {
            Debug.LogWarning("Nenhum objeto com NavMeshHowTo encontrado na cena.");
        }

        if (battleButton != null)
        {
            battleButton.onClick.AddListener(() => StartCoroutine(ContagemRegressiva()));
        }

        if (playerCreatureGO != null)
        {
            playerCreatureGO.SetActive(false);
            playerCreature = playerCreatureGO.GetComponent<CriaturaArena>();
        }

        if (enemyCreatureGO != null)
        {
            enemyCreatureGO.SetActive(false);
            enemyCreature = enemyCreatureGO.GetComponent<CriaturaArena>();
        }

        if (countdownCanvas != null)
        {
            countdownCanvas.SetActive(false);
        }

        if (rewardPopup != null)
        {
            rewardPopup.SetActive(false);
        }

        if (okButton != null)
        {
            okButton.onClick.AddListener(ReturnToMap);
        }
    }

    private void Update()
    {
        // Só checa até o fim da luta (antes chamava ShowVictory/ShowDefeat a cada frame).
        if (!batalhaIniciada || batalhaTerminada) return;

        if (playerCreature == null || playerCreature.gameObject == null)
        {
            ShowDefeat();
        }
        else if (enemyCreature == null || enemyCreature.gameObject == null)
        {
            ShowVictory();
        }
    }

    private IEnumerator ContagemRegressiva()
    {
        if (countdownCanvas != null) countdownCanvas.SetActive(true);

        string[] mensagens = { "1", "2", "3", "FIGHT!" };

        foreach (string msg in mensagens)
        {
            if (countdownText != null)
                countdownText.text = msg;

            yield return new WaitForSeconds(1f);
        }

        if (countdownCanvas != null) countdownCanvas.SetActive(false);

        if (playerCreatureGO != null)
        {
            playerCreatureGO.SetActive(true);
            var c = playerCreatureGO.GetComponent<CriaturaArena>();
            if (c != null) c.podeLutar = true;
        }

        if (enemyCreatureGO != null)
        {
            enemyCreatureGO.SetActive(true);
            var c = enemyCreatureGO.GetComponent<CriaturaArena>();
            if (c != null) c.podeLutar = true;
        }

        batalhaIniciada = true;

        if (navMeshArena != null)
        {
            navMeshArena.TravarArena();
        }
    }

    private void ShowVictory()
    {
        batalhaTerminada = true;
        if (victoryScreen != null)
            victoryScreen.SetActive(true);

        ShowRewardPopup();
    }

    private void ShowDefeat()
    {
        batalhaTerminada = true;
        if (defeatScreen != null)
            defeatScreen.SetActive(true);

        ShowRewardPopup();
    }

    private void ShowRewardPopup()
    {
        if (battleCanvas != null)
            battleCanvas.SetActive(false); // 👈 oculta o canvas da batalha
             Invoke(nameof(Popup), 1f);

       
    }
    private void Popup()
    {
                if (rewardPopup != null)
            rewardPopup.SetActive(true);   // 👈 mostra o canvas da recompensa
    }


    public void ReturnToMap()
    {
        SceneManager.LoadScene(Cenas.Mapa);
    }
}
