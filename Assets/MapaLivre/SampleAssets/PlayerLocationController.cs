using System.Collections;
using Niantic.Lightship.Maps.Core.Coordinates;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Niantic.Lightship.Maps.SampleAssets.Player
{
    /// <summary>
    /// Move o jogador pelo mapa usando o GPS do celular.
    /// No Editor (ou sem GPS), anda com WASD / setas, relativo à câmera.
    /// O jogador fica parado na origem; quem anda é o mapa (o centro do mapa segue o jogador).
    /// </summary>
    public class PlayerLocationController : MonoBehaviour
    {
        [SerializeField] private LightshipMapView _lightshipMapView;
        [Tooltip("Velocidade no Editor, em metros por segundo.")]
        [SerializeField] private float _editorMovementSpeed = 60f;
        [SerializeField] private Camera _camera;
        [SerializeField] private PlayerModel _model;

        [Header("GPS")]
        [SerializeField] private float _gpsAccuracyMeters = 5f;
        [SerializeField] private float _gpsUpdateDistanceMeters = 1f;
        [Tooltip("Acima dessa distância o jogador 'teleporta' em vez de andar até o ponto.")]
        [SerializeField] private float _teleportDistanceMeters = 300f;
        [SerializeField] private float _turnSpeed = 10f;

        private LatLng? _gpsTarget;
        private bool _gpsRunning;

        public bool IsGpsActive => _gpsRunning;

        private void Start()
        {
            if (_lightshipMapView == null) _lightshipMapView = FindFirstObjectByType<LightshipMapView>();
            if (_camera == null) _camera = Camera.main;
            if (_model == null) _model = GetComponentInChildren<PlayerModel>();
            if (!Application.isEditor) StartCoroutine(StartGps());
        }

        private IEnumerator StartGps()
        {
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Permission.RequestUserPermission(Permission.FineLocation);
                var wait = 0f;
                while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) && wait < 20f)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
#endif
            if (!Input.location.isEnabledByUser)
            {
                Debug.LogWarning("[MapaLivre] Localização desligada pelo usuário; usando o local padrão.");
                yield break;
            }

            Input.location.Start(_gpsAccuracyMeters, _gpsUpdateDistanceMeters);
            var timeout = 20f;
            while (Input.location.status == LocationServiceStatus.Initializing && timeout > 0)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarning($"[MapaLivre] GPS indisponível ({Input.location.status}).");
                yield break;
            }

            _gpsRunning = true;
            var first = Input.location.lastData;
            _lightshipMapView.SetMapCenter(new LatLng(first.latitude, first.longitude));
        }

        private void OnDestroy()
        {
            if (_gpsRunning) Input.location.Stop();
        }

        private void Update()
        {
            if (_lightshipMapView == null) return;

            var move = Vector3.zero;

            if (_gpsRunning)
            {
                var d = Input.location.lastData;
                _gpsTarget = new LatLng(d.latitude, d.longitude);
            }

            if (_gpsTarget.HasValue)
            {
                // Anda suavemente até a última posição do GPS.
                var target = _lightshipMapView.LatLngToScene(_gpsTarget.Value) - _lightshipMapView.SceneOrigin;
                target.y = 0f;
                var meters = target.magnitude / Mathf.Max(0.0001f, _lightshipMapView.UnitsPerMeter);
                if (meters > _teleportDistanceMeters)
                {
                    _lightshipMapView.SetMapCenter(_gpsTarget.Value);
                }
                else if (meters > 0.05f)
                {
                    var speed = Mathf.Max(1.5f, meters * 1.5f) * _lightshipMapView.UnitsPerMeter;
                    move = Vector3.ClampMagnitude(target, speed * Time.deltaTime);
                }
            }

            if (!_gpsRunning)
            {
                var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
                if (input.sqrMagnitude > 0.01f)
                {
                    _gpsTarget = null;
                    var basis = _camera != null ? _camera.transform : transform;
                    var fwd = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
                    if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(basis.up, Vector3.up).normalized;
                    var right = Vector3.Cross(Vector3.up, fwd);
                    move = (fwd * input.z + right * input.x).normalized *
                           (_editorMovementSpeed * _lightshipMapView.UnitsPerMeter * Time.deltaTime);
                }
            }

            var moving = move.sqrMagnitude > 1e-8f;
            if (moving)
            {
                _lightshipMapView.OffsetMapCenter(move);
                var look = Quaternion.LookRotation(new Vector3(move.x, 0f, move.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-_turnSpeed * Time.deltaTime));
            }

            if (_model != null)
                _model.UpdatePlayerState(moving, moving ? move.magnitude / Mathf.Max(Time.deltaTime, 1e-4f) : 0f);
        }
    }
}
