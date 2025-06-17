using UnityEngine;

public class Rotator : MonoBehaviour
{
    // Escolha do eixo de rotação no Inspector
    public Vector3 rotationAxis = Vector3.up;

    // Velocidade de rotação ajustável no Inspector
    public float rotationSpeed = 5f;

    void Update()
    {
        // Rotaciona o objeto em torno do eixo escolhido
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
    }
}
