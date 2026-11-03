using UnityEngine;

/// <summary>Mantém a placa virada para a câmera (com um giro opcional em volta do próprio eixo).</summary>
public class OlharCamera : MonoBehaviour
{
    public float giro;
    private Camera _cam;

    private void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position, _cam.transform.up) *
                             Quaternion.Euler(0f, 0f, giro);
    }
}
