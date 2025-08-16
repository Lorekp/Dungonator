using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // NEW INPUT SYSTEM

namespace Dungonator.Client
{
    /// <summary>
    /// Persistent FPS HUD (TextMeshPro) that auto-spawns once and survives scene loads.
    /// Toggle on/off via Input System (default: <Keyboard>/f3). Updates use unscaledDeltaTime.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class GlobalFPSHud : MonoBehaviour
    {
        private static GlobalFPSHud _instance;

        [Header("Input (New Input System)")]
        [Tooltip("Optional: reference an action (e.g., 'UI/FPSHudToggle'). If not set, a default <Keyboard>/f3 binding is created at runtime.")]
        [SerializeField] private InputActionReference toggleActionRef;
        [Tooltip("Used only if Toggle Action Ref is null.")]
        [SerializeField] private string defaultToggleBinding = "<Keyboard>/f3";
        [Tooltip("Additional optional binding (e.g., <Gamepad>/start). Leave empty to skip.")]
        [SerializeField] private string extraToggleBinding = "";

        private InputAction _toggleAction;
        private bool _ownsToggleAction; // true if we created it ourselves

        [Header("Behavior")]
        [SerializeField] private float updatesPerSecond = 4f;
        [Range(0f, 1f)] [SerializeField] private float smoothing = 0.1f;

        [Header("Display")]
        [SerializeField] private bool showMilliseconds = true;
        [SerializeField] private int fpsDecimalPlaces = 0;
        [SerializeField] private int msDecimalPlaces = 1;

        [Header("Color thresholds (by FPS)")]
        [SerializeField] private int okFps = 60;
        [SerializeField] private int goodFps = 144;
        [SerializeField] private Color badColor  = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color okColor   = new Color(1f, 0.95f, 0.4f, 1f);
        [SerializeField] private Color goodColor = new Color(0.4f, 1f, 0.4f, 1f);

        [Header("Layout")]
        [SerializeField] private Vector2 anchoredPos = new Vector2(8f, -8f); // top-left padding
        [SerializeField] private int fontSize = 24;
        [SerializeField] private FontStyles fontStyle = FontStyles.Bold;
        [SerializeField] private bool dropShadow = true;
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private Vector2 shadowOffset = new Vector2(1.5f, -1.5f);

        private TextMeshProUGUI _label;
        private float _deltaTime;
        private float _nextUpdateAt;
        private bool _visible = true;

        // --- Auto-spawn once before any scene loads ---
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            if (_instance != null) return;

            var go = new GameObject("FPS HUD (Global)");
            _instance = go.AddComponent<GlobalFPSHud>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            CreateHudIfNeeded();
        }

        private void OnEnable()
        {
            // Wire input
            if (toggleActionRef != null && toggleActionRef.action != null)
            {
                _toggleAction = toggleActionRef.action;
                _ownsToggleAction = false;
            }
            else
            {
                _toggleAction = new InputAction("ToggleFPSHud");
                if (!string.IsNullOrWhiteSpace(defaultToggleBinding))
                    _toggleAction.AddBinding(defaultToggleBinding);
                if (!string.IsNullOrWhiteSpace(extraToggleBinding))
                    _toggleAction.AddBinding(extraToggleBinding);

                _ownsToggleAction = true;
            }

            _toggleAction.performed += OnTogglePerformed;
            if (!_toggleAction.enabled) _toggleAction.Enable();
        }

        private void OnDisable()
        {
            if (_toggleAction != null)
            {
                _toggleAction.performed -= OnTogglePerformed;
                if (_ownsToggleAction)
                {
                    _toggleAction.Disable();
                    _toggleAction.Dispose();
                }
                _toggleAction = null;
            }
        }

        private void OnTogglePerformed(InputAction.CallbackContext ctx)
        {
            _visible = !_visible;
            if (_label) _label.gameObject.SetActive(_visible);
        }

        private void Update()
        {
            if (!_visible || _label == null) return;

            // Smooth unscaled delta (so pausing/slow-mo doesn't skew)
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            _deltaTime += (dt - _deltaTime) * Mathf.Clamp01(smoothing);

            // Throttle text updates
            if (Time.unscaledTime >= _nextUpdateAt)
            {
                _nextUpdateAt = Time.unscaledTime + (1f / Mathf.Max(1e-3f, updatesPerSecond));
                UpdateText();
            }
        }

        private void UpdateText()
        {
            float msec = _deltaTime * 1000.0f;
            float fps  = 1.0f / _deltaTime;

            // Build text
            if (showMilliseconds)
                _label.text = $"{msec.ToString($"0.{new string('0', msDecimalPlaces)}")} ms  ({fps.ToString($"0.{new string('0', fpsDecimalPlaces)}")} fps)";
            else
                _label.text = $"{fps.ToString($"0.{new string('0', fpsDecimalPlaces)}")} fps";

            // Color by thresholds
            if (fps >= goodFps)      _label.color = goodColor;
            else if (fps >= okFps)   _label.color = okColor;
            else                     _label.color = badColor;
        }

        private void CreateHudIfNeeded()
        {
            // Create overlay canvas (on top of everything)
            var canvasGO = new GameObject("FPS HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            DontDestroyOnLoad(canvasGO);

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue; // top-most

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            // Text label
            var textGO = new GameObject("FPS Label", typeof(TextMeshProUGUI));
            textGO.transform.SetParent(canvasGO.transform, false);
            _label = textGO.GetComponent<TextMeshProUGUI>();

            var rect = _label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f); // top-left
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot     = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;

            _label.text = "…";
            _label.raycastTarget = false;
            _label.alignment = TextAlignmentOptions.TopLeft;
            _label.fontSize = fontSize;
            _label.fontStyle = fontStyle;
            _label.textWrappingMode = TextWrappingModes.NoWrap;

            if (dropShadow)
            {
                var shadow = textGO.AddComponent<Shadow>();
                shadow.effectColor = shadowColor;
                shadow.effectDistance = shadowOffset;
                shadow.useGraphicAlpha = true;
            }
        }
    }
}
