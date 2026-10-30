using UnityEngine;

/// <summary>
/// Arena do AR: quando o jogador posiciona a arena (duplo toque), começa a batalha nos pontos dela.
/// A luta em si é feita pelo BatalhaDigimon, o mesmo sistema da Battle_Scene.
/// </summary>
public class ARBattleManager : MonoBehaviour
{
    public Transform spawnPlayer;
    public Transform spawnEnemy;

    [Tooltip("Usado só como referência de tamanho dos Digimons dentro da arena.")]
    public GameObject playerPrefab;

    [Tooltip("Não é mais usado: os inimigos vêm das fichas (Resources/Digimons). Mantido para não perder dados.")]
    public GameObject[] listaDeInimigos;

    [Tooltip("Altura dos Digimons se não houver prefab de referência (proporcional ao tamanho da arena).")]
    public float alturaPadrao = 0.3f;

    void Start()
    {
        var altura = MedirAltura();
        if (altura <= 0f) altura = alturaPadrao * transform.lossyScale.x;
        BatalhaDigimon.IniciarEm(spawnPlayer, spawnEnemy, altura, transform);
    }

    private float MedirAltura()
    {
        if (playerPrefab == null || spawnPlayer == null) return 0f;
        var tmp = Instantiate(playerPrefab, spawnPlayer.position, spawnPlayer.rotation, transform);
        var h = DigimonVisual.Altura(tmp);
        tmp.SetActive(false);
        Destroy(tmp);
        return h;
    }
}
