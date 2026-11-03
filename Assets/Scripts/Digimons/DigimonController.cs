using UnityEngine;
using Niantic.Lightship.Maps.SampleAssets.Player;

public class DigimonController : MonoBehaviour
{
    [SerializeField]
    private DigimonBase digimon;

    private Animator animator;

    [SerializeField]
    private Transform spawnPoint;

    [Tooltip("Usa o parceiro salvo no perfil do jogador (em vez do Digimon fixo acima).")]
    [SerializeField]
    private bool usarParceiroDoPerfil = true;

    private void Start()
    {
        animator = GetComponent<Animator>();

        if (usarParceiroDoPerfil)
        {
            var ficha = PerfilJogador.Atual.Parceiro?.Ficha;
            if (ficha != null) digimon = ficha;
        }

        if (digimon != null)
        {
            SetDigimonController();
        }
    }
    public DigimonBase GetDigimon()
    {
        return digimon;
    }
    [Tooltip("Altura do parceiro no mapa (metros). Os modelos têm escalas diferentes; todos são ajustados para esta altura.")]
    [SerializeField]
    private float alturaNoMapa = 14f;

    private GameObject instanciaAtual;
    private bool iniciado;

    private void SetDigimonController()
    {
        iniciado = true;
        if (instanciaAtual != null) Destroy(instanciaAtual); // troca de parceiro

        if (digimon.Prefab != null)
        {
            // Instanciar o GameObject do DigimonBase
            GameObject digimonInstance = Instantiate(digimon.Prefab, spawnPoint.position, spawnPoint.rotation);
            instanciaAtual = digimonInstance;

            // Scripts de mapa/batalha que vêm em alguns prefabs não servem para o parceiro.
            foreach (var c in digimonInstance.GetComponents<CriaturaArena>()) Destroy(c);
            foreach (var c in digimonInstance.GetComponents<DigimonMapa>()) Destroy(c);
            foreach (var c in digimonInstance.GetComponents<WildDigimon>()) Destroy(c);

            // Tornar o DigimonInstance um filho do DigimonController
            digimonInstance.transform.parent = transform;
            digimonInstance.transform.localScale = Vector3.one;
            AjustarAltura(digimonInstance);

            // Obtém o Animator do GameObject instanciado
            Animator digimonAnimator = digimonInstance.GetComponentInChildren<Animator>();

            // Verifica se o GameObject instanciado possui um Animator
            if (digimonAnimator != null)
            {
                // Configurar o Animator do GameObject instanciado
                if (digimon.Animator != null)
                    digimonAnimator.runtimeAnimatorController = digimon.Animator.runtimeAnimatorController;
                digimonAnimator.enabled = true;

                // Acessar o componente PlayerModel no mesmo GameObject
                PlayerModel playerModel = GetComponent<PlayerModel>();
                if (playerModel == null) playerModel = FindFirstObjectByType<PlayerModel>();

                 //Verificar se o componente PlayerModel foi encontrado
                if (playerModel != null)
                {
                    // Atribuir o Animator do GameObject instanciado ao parâmetro _animator no PlayerModel
                    playerModel.SetAnimator(digimonAnimator);
                }
                else
                {
                    Debug.LogWarning("O componente PlayerModel não foi encontrado.");
                }
            }
            else
            {
                Debug.LogWarning("O GameObject instanciado não possui um Animator.");
            }
        }
        else
        {
            Debug.LogWarning("DigimonBase não possui um Prefab associado.");
        }
    }

    public void SetDigimon(DigimonBase newDigimon)
    {
        digimon = newDigimon;

        if (iniciado && digimon != null)
        {
            SetDigimonController();
        }
    }

    /// <summary>Deixa o modelo com a altura definida, com os pés no ponto de spawn.</summary>
    private void AjustarAltura(GameObject go)
    {
        if (alturaNoMapa <= 0f) return;
        var h = DigimonVisual.Altura(go);
        if (h <= 0.0001f) return;
        // A altura vale para um Rookie; bebês ficam menores e estágios fortes, maiores.
        go.transform.localScale *= alturaNoMapa * Proporcao(digimon != null ? digimon.Stage : DigimonStage.Rookie) / h
                                   * DigimonVisual.Ajuste(digimon);
    }

    public static float Proporcao(DigimonStage e)
    {
        switch (e)
        {
            case DigimonStage.Fresh: return 0.5f;
            case DigimonStage.InTraining: return 0.65f;
            case DigimonStage.Rookie: return 1f;
            case DigimonStage.Champion:
            case DigimonStage.Armor: return 1.25f;
            case DigimonStage.Ultimate: return 1.45f;
            default: return 1.6f;
        }
    }
}
