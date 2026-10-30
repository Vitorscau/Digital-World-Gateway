using UnityEngine;
using UnityEngine.SceneManagement;

public class TouchInput : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        // Verifica se h� toque na tela
        //if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        //{
            // Carrega uma nova cena chamada "NomeDaSuaCena"
          //  SceneManager.LoadScene("Map_Scene");
        //}
    }

    public void LoadMapScene()
    {
          SceneManager.LoadScene("Map_Scene");
    }
}
