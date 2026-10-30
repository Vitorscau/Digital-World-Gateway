using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Botão "Jogar" do menu: entra no mapa (usando o carregamento antecipado, se houver).</summary>
public class TouchInput : MonoBehaviour
{
    public void LoadMapScene()
    {
        if (LoadingManager.AtivarCenaPreCarregada()) return;
        SceneManager.LoadScene(Cenas.Inicio);
    }
}
