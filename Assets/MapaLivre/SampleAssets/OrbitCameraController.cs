using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Niantic.Lightship.Maps.SampleAssets.Cameras.OrbitCamera
{
    [Serializable]
    public class GestureSettings
    {
        public bool _rotationEnabled = true;
        public bool _zoomEnabled = true;
        public bool _doubleTapZoomEnabled = true;
        [Tooltip("Graus por pixel arrastado.")]
        public float _rotationSensitivity = 0.25f;
        public float _pinchSensitivity = 0.01f;
        public float _scrollSensitivity = 0.1f;
    }

    /// <summary>
    /// Câmera estilo Pokémon GO: gira em volta do jogador arrastando o dedo/mouse,
    /// pinça ou roda do mouse para zoom (quanto mais perto, mais "deitada" a câmera).
    /// </summary>
    public class OrbitCameraController : MonoBehaviour
    {
        [Tooltip("Se ligado, a câmera gira junto com a direção do jogador.")]
        public bool SnapRotation;

        [SerializeField] private GameObject _focusObject;
        [SerializeField] private GestureSettings _gestureSettings = new GestureSettings();

        [Header("Distância (unidades de cena)")]
        [SerializeField] private float _minDistance = 60f;
        [SerializeField] private float _maxDistance = 260f;
        [Range(0, 1)] [SerializeField] private float _startZoom = 0.35f;
        [Header("Inclinação (graus)")]
        [SerializeField] private float _pitchClose = 35f;
        [SerializeField] private float _pitchFar = 65f;
        [SerializeField] private float _smoothing = 12f;

        private Camera _camera;
        private float _yaw, _zoom, _targetZoom;
        private Vector2 _lastPointer;
        private bool _dragging;
        private float _lastTapTime;

        public Camera Camera => _camera;
        public GameObject FocusObject { get => _focusObject; set => _focusObject = value; }

        private void Awake()
        {
            _camera = GetComponentInChildren<Camera>(true);
            _zoom = _targetZoom = _startZoom;
            _yaw = transform.eulerAngles.y;
            Apply(true);
        }

        /// <summary>Usado pelo botão-bússola da HUD: gira o mapa de volta com o norte para cima.</summary>
        public void OnToggleValueChanged(bool isOn) => ApontarParaNorte();

        /// <summary>Gira a câmera suavemente até o norte ficar para cima.</summary>
        public void ApontarParaNorte()
        {
            SnapRotation = false;
            _voltandoAoNorte = true;
        }

        private bool _voltandoAoNorte;

        private void LateUpdate()
        {
            HandleInput();
            if (_dragging) _voltandoAoNorte = false;
            if (_voltandoAoNorte)
            {
                _yaw = Mathf.LerpAngle(_yaw, 0f, 1f - Mathf.Exp(-8f * Time.deltaTime));
                if (Mathf.Abs(Mathf.DeltaAngle(_yaw, 0f)) < 0.1f) { _yaw = 0f; _voltandoAoNorte = false; }
            }
            if (SnapRotation && _focusObject != null)
                _yaw = Mathf.LerpAngle(_yaw, _focusObject.transform.eulerAngles.y, 1f - Mathf.Exp(-4f * Time.deltaTime));
            _zoom = Mathf.Lerp(_zoom, _targetZoom, 1f - Mathf.Exp(-_smoothing * Time.deltaTime));
            Apply(false);
        }

        private void Apply(bool instant)
        {
            if (_focusObject != null) transform.position = _focusObject.transform.position;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_camera == null) return;

            var dist = Mathf.Lerp(_minDistance, _maxDistance, _zoom);
            var pitch = Mathf.Lerp(_pitchClose, _pitchFar, _zoom) * Mathf.Deg2Rad;
            var local = new Vector3(0f, Mathf.Sin(pitch) * dist, -Mathf.Cos(pitch) * dist);
            var ct = _camera.transform;
            ct.localPosition = local;
            ct.localRotation = Quaternion.LookRotation(-local.normalized, Vector3.up);
            _camera.farClipPlane = Mathf.Max(_camera.farClipPlane, dist * 8f);
        }

        private static bool OverUI(int pointerId = -1) =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);

        private void HandleInput()
        {
            var g = _gestureSettings;

            if (Input.touchCount == 2)
            {
                _dragging = false;
                var a = Input.GetTouch(0);
                var b = Input.GetTouch(1);
                if (OverUI(a.fingerId) || OverUI(b.fingerId)) return;
                var prevDist = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude;
                var curDist = (a.position - b.position).magnitude;
                if (g._zoomEnabled) Zoom(-(curDist - prevDist) * g._pinchSensitivity);
                if (g._rotationEnabled)
                {
                    var prevAngle = Vector2.SignedAngle(Vector2.right, (a.position - a.deltaPosition) - (b.position - b.deltaPosition));
                    var curAngle = Vector2.SignedAngle(Vector2.right, a.position - b.position);
                    _yaw -= Mathf.DeltaAngle(prevAngle, curAngle);
                }
                return;
            }

            if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    _dragging = !OverUI(t.fingerId);
                    if (_dragging && g._doubleTapZoomEnabled && g._zoomEnabled && Time.unscaledTime - _lastTapTime < 0.3f)
                        _targetZoom = _targetZoom > 0.5f ? 0f : 1f;
                    _lastTapTime = Time.unscaledTime;
                }
                else if (_dragging && t.phase == TouchPhase.Moved && g._rotationEnabled)
                {
                    Rotate(t.position, t.deltaPosition);
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    _dragging = false;
                }
                return;
            }

            // Mouse (Editor / PC)
            if (Input.GetMouseButtonDown(0))
            {
                _dragging = !OverUI();
                _lastPointer = Input.mousePosition;
            }
            else if (Input.GetMouseButton(0) && _dragging && g._rotationEnabled)
            {
                Vector2 p = Input.mousePosition;
                Rotate(p, p - _lastPointer);
                _lastPointer = p;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _dragging = false;
            }

            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f && g._zoomEnabled && !OverUI()) Zoom(-scroll * g._scrollSensitivity);
        }

        private void Rotate(Vector2 position, Vector2 delta)
        {
            // Arrastar na metade de cima da tela gira ao contrário (sensação de "girar a mesa").
            var sign = position.y > Screen.height * 0.5f ? -1f : 1f;
            _yaw += delta.x * _gestureSettings._rotationSensitivity * sign;
        }

        private void Zoom(float amount) => _targetZoom = Mathf.Clamp01(_targetZoom + amount);
    }
}
