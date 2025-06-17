using UnityEngine;

public class BattleUnit : MonoBehaviour
{
    [Header("Unit Settings")]
    [SerializeField] private bool isPlayerUnit;
    [SerializeField] private Transform spawnPoint;

    public Digimon digimon { get; private set; }

    public void Initialize(DigimonBase digimonBase, int level)
    {
        digimon = new Digimon(digimonBase, level);
        SpawnDigimonModel();
    }

    private void SpawnDigimonModel()
    {
        if (digimon?.Base?.Prefab == null) return;

        var digimonInstance = Instantiate(
            digimon.Base.Prefab, 
            spawnPoint.position, 
            spawnPoint.rotation,
            transform
        );

        digimonInstance.transform.localScale = Vector3.one * 100f;

        var animator = digimonInstance.GetComponent<Animator>();
        if (animator != null && digimon.Base.Animator != null)
        {
            animator.runtimeAnimatorController = digimon.Base.Animator.runtimeAnimatorController;
        }

        gameObject.AddComponent<CriaturaArena>();
    }
}