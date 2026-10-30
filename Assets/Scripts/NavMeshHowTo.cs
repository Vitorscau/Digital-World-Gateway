using System.Collections;
using UnityEngine;
using Niantic.Lightship.AR.NavigationMesh;

public class NavMeshHowTo : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private LightshipNavMeshManager _navmeshManager;
    [SerializeField] private GameObject _agentPrefab;

    private GameObject _creature;
    private LightshipNavMeshAgent _agent;
    private Transform mainCameraTransform;

    public bool arenaTravada = false;
    public float pinchZoomSpeed = 0.01f;
    public float minScale = 0.3f;
    public float maxScale = 2f;

    // Controle de duplo toque/clique
    private float ultimoToque = 0f;
    private const float tempoEntreToques = 0.3f;

    private void Start()
    {
        mainCameraTransform = Camera.main.transform;
        transform.rotation = Quaternion.identity;
    }

    void Update()
    {
        if (!arenaTravada)
        {
            HandleDoubleTap();
            HandlePinch();
        }
    }

    public void TravarArena()
    {
        arenaTravada = true;
    }

    private void HandleDoubleTap()
    {
#if UNITY_EDITOR
        // Duplo clique com o mouse
        if (Input.GetMouseButtonDown(0))
        {
            float tempoDesdeUltimoClique = Time.time - ultimoToque;
            ultimoToque = Time.time;

            if (tempoDesdeUltimoClique < tempoEntreToques)
            {
                ProcessarToque(Input.mousePosition);
            }
        }
#else
        if (Input.touchCount == 1)
        {
            Touch toque = Input.GetTouch(0);

            if (toque.phase == TouchPhase.Began)
            {
                float tempoDesdeUltimoToque = Time.time - ultimoToque;
                ultimoToque = Time.time;

                if (tempoDesdeUltimoToque < tempoEntreToques)
                {
                    ProcessarToque(toque.position);
                }
            }
        }
#endif
    }

    private void ProcessarToque(Vector2 posicao)
    {
        Ray ray = _camera.ScreenPointToRay(posicao);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (_creature == null)
            {
                _creature = Instantiate(_agentPrefab);
                _creature.transform.position = hit.point;
                _agent = _creature.GetComponent<LightshipNavMeshAgent>();
            }
            else
            {
                _agent.transform.position = hit.point;
            }
        }
    }

    private void HandlePinch()
    {
        if (Input.touchCount == 2)
        {
            Touch t1 = Input.GetTouch(0);
            Touch t2 = Input.GetTouch(1);

            Vector2 t1Prev = t1.position - t1.deltaPosition;
            Vector2 t2Prev = t2.position - t2.deltaPosition;

            float prevMag = (t1Prev - t2Prev).magnitude;
            float currentMag = (t1.position - t2.position).magnitude;

            float diff = currentMag - prevMag;
            float newScale = Mathf.Clamp(transform.localScale.x + diff * pinchZoomSpeed, minScale, maxScale);

            transform.localScale = new Vector3(newScale, newScale, newScale);
        }
    }
}
