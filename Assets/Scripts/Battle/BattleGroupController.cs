using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleGroupController : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Invoke("Alternate", 1f);
    }
    void Alternate()
    {
        gameObject.SetActive(false);
    }
   
}
