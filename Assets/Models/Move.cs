using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Move : MonoBehaviour
{
   
    public float velocidade = 5.0f; // Velocidade de movimento
    public float pontoDeVolta = 88f; // Ponto onde o objeto deve voltar para o início

    void Update()
    {
        // Mova o objeto ao longo do eixo Z
        transform.Translate(Vector3.forward * velocidade * Time.deltaTime);

        // Verifique se o objeto atingiu o ponto de volta
        if (transform.position.z >= pontoDeVolta)
        {
            // Se atingiu, volte para o início
            VoltarParaInicio();
        }
    }

    void VoltarParaInicio()
    {
        // Reposicione o objeto para o início
        transform.position = new Vector3(transform.position.x, transform.position.y, -16f);
    }
}