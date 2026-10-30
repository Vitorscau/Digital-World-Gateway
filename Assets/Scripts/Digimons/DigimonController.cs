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
    private void SetDigimonController()
    {
        if (digimon.Prefab != null)
        {
            // Instanciar o GameObject do DigimonBase
            GameObject digimonInstance = Instantiate(digimon.Prefab, spawnPoint.position, spawnPoint.rotation);

            // Tornar o DigimonInstance um filho do DigimonController
            digimonInstance.transform.parent = transform;

            // Definir a escala do GameObject instanciado para (1, 1, 1)
            digimonInstance.transform.localScale = Vector3.one;

            // Obtém o Animator do GameObject instanciado
            Animator digimonAnimator = digimonInstance.GetComponent<Animator>();

            // Verifica se o GameObject instanciado possui um Animator
            if (digimonAnimator != null)
            {
                // Configurar o Animator do GameObject instanciado
                digimonAnimator.runtimeAnimatorController = digimon.Animator.runtimeAnimatorController;

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

        if (animator != null)
        {
            SetDigimonController();
        }
    }
}
