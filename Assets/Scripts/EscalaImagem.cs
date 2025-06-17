using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class EscalaImagem : MonoBehaviour
{
    public float escalaMinima = 0.5f;
    public float escalaMaxima = 2.0f;
    public float velocidadeDeAlteracao = 1.0f;

    private bool aumentando = true;

    void Update()
    {
        // Altera a escala da imagem
        AlterarEscala();

        // Altera a direção da alteração de escala quando atinge os limites
        VerificarLimites();
    }

    void AlterarEscala()
    {
        float escalaAtual = transform.localScale.x;

        if (aumentando)
        {
            escalaAtual += velocidadeDeAlteracao * Time.deltaTime;
        }
        else
        {
            escalaAtual -= velocidadeDeAlteracao * Time.deltaTime;
        }

        // Limita a escala dentro dos valores desejados
        escalaAtual = Mathf.Clamp(escalaAtual, escalaMinima, escalaMaxima);

        // Aplica a nova escala à imagem
        transform.localScale = new Vector3(escalaAtual, escalaAtual, 1f);
    }

    void VerificarLimites()
    {
        if (transform.localScale.x >= escalaMaxima || transform.localScale.x <= escalaMinima)
        {
            aumentando = !aumentando;
        }
    }
}
