using UnityEngine;
using UnityEngine.UI;

public class PlayClickSound : MonoBehaviour
{
    public AudioClip clickSound;
    private AudioSource audioSource;

    void Start()
    {
        // Pega o AudioSource da cena (pode ser do SoundManager)
        audioSource = FindObjectOfType<AudioSource>();

        // Adiciona o evento ao botão
        GetComponent<Button>().onClick.AddListener(TocarSom);
    }

    void TocarSom()
    {
        if (clickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(clickSound);
        }
        
    }
}
