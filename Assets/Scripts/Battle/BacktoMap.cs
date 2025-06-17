using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BacktoMap : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
     private void OnMouseDown()
    {
       Debug.Log("voltando para o mapa");
        SceneManager.LoadScene("Map_Scene");
    }
}
