using UnityEngine;

/// <summary>
/// Gira o objeto de acordo com uma fonte:
/// - Giroscopio: inclinação do celular (comportamento antigo).
/// - Bussola: norte magnético do celular.
/// - CameraDoMapa: para onde a câmera do mapa está virada (o ponteiro aponta para o norte do mapa,
///   como a bússola do Pokémon GO).
/// </summary>
public class FollowGyro : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    public enum Fonte
    {
        Giroscopio,
        Bussola,
        CameraDoMapa
    }

    [Header("Tweaks")]
    [SerializeField] private Fonte fonte = Fonte.Giroscopio;
    [SerializeField] private float rotationSpeed = 1.0f;
    [SerializeField] private float axisOffset = 0.0f;
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Z;
    [Tooltip("Deslocamento aplicado uma vez ao iniciar.")]
    [SerializeField] private Vector3 positionOffset;

    [Tooltip("Na tela do mapa, um ponteiro de bússola da HUD passa a mostrar o norte do mapa automaticamente.")]
    [SerializeField] private bool bussolaDoMapaAutomatica = true;

    void Start()
    {
        // Antes era somado a cada frame, e o objeto ia "fugindo" da tela.
        transform.position += positionOffset;

        if (bussolaDoMapaAutomatica && fonte == Fonte.Giroscopio && GetComponentInParent<Canvas>() != null &&
            FindFirstObjectByType<Niantic.Lightship.Maps.SampleAssets.Cameras.OrbitCamera.OrbitCameraController>() != null)
            fonte = Fonte.CameraDoMapa;

        if (fonte == Fonte.Giroscopio) GyroManager.Instance.EnanbleGyro();
        if (fonte == Fonte.Bussola) Input.compass.enabled = true;
    }

    void Update()
    {
        var angulo = LerAngulo();
        Quaternion alvo;
        switch (rotationAxis)
        {
            case RotationAxis.X: alvo = Quaternion.Euler(angulo + axisOffset, 0, 0); break;
            case RotationAxis.Y: alvo = Quaternion.Euler(0, -angulo - axisOffset, 0); break;
            default: alvo = Quaternion.Euler(0, 0, angulo + axisOffset); break;
        }

        // CameraDoMapa precisa acompanhar a câmera sem atraso; as outras fontes são suavizadas.
        transform.localRotation = fonte == Fonte.CameraDoMapa
            ? alvo
            : Quaternion.Slerp(transform.localRotation, alvo, rotationSpeed * Time.deltaTime);
    }

    private float LerAngulo()
    {
        switch (fonte)
        {
            case Fonte.Bussola:
                return Input.compass.magneticHeading;
            case Fonte.CameraDoMapa:
                var cam = Camera.main;
                return cam != null ? cam.transform.eulerAngles.y : 0f;
            default:
                var g = GyroManager.Instance.GetGyroRotation().eulerAngles;
                return rotationAxis == RotationAxis.X ? g.x : rotationAxis == RotationAxis.Y ? g.y : g.z;
        }
    }
}
