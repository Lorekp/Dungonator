using UnityEngine;
using UnityEngine.InputSystem;
using PixelPerfectCamera = UnityEngine.Rendering.Universal.PixelPerfectCamera;

namespace Debugging
{
    public class SimpleCameraController : MonoBehaviour
    {
        public float moveSpeed = 10f;
        public float dragSpeed = 2f;
        public float zoomSpeed = 0.1f; // Controls zoom factor change per scroll
        public float minZoomFactor = 0.5f; // Min PPU = defaultPPU * 0.5 (zoom out)
        public float maxZoomFactor = 2.0f; // Max PPU = defaultPPU * 2.0 (zoom in)

        private CameraControls _controls;
        private Vector3 _dragOrigin;
        private bool _dragging;
        private Camera _camera;
        private PixelPerfectCamera _pixelPerfectCamera;
        private int _defaultPPU;

        private void Start()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                return;
            }

            _pixelPerfectCamera = _camera.GetComponent<PixelPerfectCamera>();
            if (_pixelPerfectCamera == null)
            {
            }
            else
            {
                _defaultPPU = _pixelPerfectCamera.assetsPPU; // Store default PPU (e.g., 16)
            }
        }

        private void Awake()
        {
            _controls = new CameraControls();
            if (_controls == null)
            {
                return;
            }

            _controls.Camera.Drag.started += ctx => StartDrag();
            _controls.Camera.Drag.canceled += ctx => EndDrag();
        }

        private void OnEnable()
        {
            _controls.Enable();
        }

        private void OnDisable()
        {
            _controls.Disable();
        }

        void Update()
        {
            if (_camera == null || _controls == null) return;

            // Handle movement
            Vector2 moveInput = _controls.Camera.Move.ReadValue<Vector2>();
            if (moveInput != Vector2.zero)
            {
                transform.position += new Vector3(moveInput.x, moveInput.y, 0) * (moveSpeed * Time.deltaTime);
            }

            // Handle dragging
            if (_dragging)
            {
                Vector3 current = _camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                Vector3 delta = _dragOrigin - current;
                transform.position += delta * dragSpeed;
            }

            // Handle zoom
            float zoomDelta = 0f;
            try
            {
                zoomDelta = _controls.Camera.Zoom.ReadValue<float>();
            }
            catch (System.InvalidOperationException)
            {
                zoomDelta = Mouse.current.scroll.ReadValue().y * 0.01f; // Normalize raw scroll
            }

            if (Mathf.Abs(zoomDelta) > 0.01f && _pixelPerfectCamera != null)
            {
                // Calculate zoom factor (1.0 = default PPU)
                float currentZoomFactor = (float)_pixelPerfectCamera.assetsPPU / _defaultPPU;
                float targetZoomFactor = currentZoomFactor - zoomDelta * zoomSpeed;
                targetZoomFactor = Mathf.Clamp(targetZoomFactor, minZoomFactor, maxZoomFactor);

                // Apply new PPU
                int newPPU = Mathf.RoundToInt(_defaultPPU * targetZoomFactor);
                if (_pixelPerfectCamera.assetsPPU != newPPU)
                {
                    _pixelPerfectCamera.assetsPPU = newPPU;
                }
            }
        }

        void StartDrag()
        {
            _dragOrigin = _camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        }

        void EndDrag()
        {
            _dragging = false;
        }

        private void LateUpdate()
        {
            if (_controls.Camera.Drag.IsPressed())
            {
                _dragging = true;
            }
        }
    }
}