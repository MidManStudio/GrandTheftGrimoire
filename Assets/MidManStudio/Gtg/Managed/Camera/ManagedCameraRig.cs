// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCameraRig.cs"
// ============================================================================

using System.Text;
using MidManStudio.Gtg.Managed.CharacterController;
using MidManStudio.Gtg.Managed.Magic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace MidManStudio.Gtg.Managed.Camera
{
    /// <summary>
    /// Camera and cursor owner for the managed character. Switches between a third person
    /// and a first person view, captures the cursor and draws a debug overlay with the
    /// module toggles and a crosshair at the screen center. With Cinemachine cameras assigned it only moves the follow target
    /// and switches the cameras on and off. With none assigned it drives the main camera
    /// itself. Inside MidManStudio.Gtg namespaces write UnityEngine.Camera in full, because
    /// the simple name Camera resolves to a namespace.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class ManagedCameraRig : MonoBehaviour
    {
        [Header("Scene references (all optional)")]
        [Tooltip("Found automatically when empty.")]
        [SerializeField] private ManagedCharacter _player;
        [Tooltip("Cinemachine Follow target. Created when empty. Carries yaw and pitch at the eye point.")]
        [SerializeField] private Transform _cameraTarget;
        [SerializeField] private GameObject _thirdPersonCamera;
        [SerializeField] private GameObject _firstPersonCamera;
        [Tooltip("Body renderers that stop drawing in first person. Found under the player when empty.")]
        [SerializeField] private Renderer[] _bodyRenderers;

        [Header("Offsets")]
        [SerializeField] private float _eyeHeight = 1.6f;
        [Tooltip("Used only by the built-in camera.")]
        [SerializeField] private float _thirdPersonDistance = 3.5f;

        [Header("Behaviour")]
        [SerializeField] private bool _startInFirstPerson;
        [SerializeField] private bool _lockCursorOnStart = true;
        [SerializeField] private bool _showDebugOverlay = true;
        [SerializeField] private bool _showCrosshair = true;

        private const float WallPadding = 0.15f;

        private UnityEngine.Camera _builtInCamera;
        private ManagedSpellCaster _caster;
        private bool _firstPerson;
        private GUIStyle _style;
        private readonly StringBuilder _text = new StringBuilder(384);

        /// <summary>Public so a UI Button can call it from its onClick list.</summary>
        public void ToggleFirstPerson()
        {
            SetFirstPerson(!_firstPerson);
        }

        public void SetFirstPerson(bool firstPerson)
        {
            _firstPerson = firstPerson;
            ApplyMode();
        }

        private void Awake()
        {
            if (_cameraTarget == null)
            {
                _cameraTarget = new GameObject("GTG Camera Target").transform;
            }

            if (_thirdPersonCamera == null && _firstPersonCamera == null)
            {
                _builtInCamera = UnityEngine.Camera.main;
                if (_builtInCamera == null)
                {
                    var go = new GameObject("GTG Camera") { tag = "MainCamera" };
                    _builtInCamera = go.AddComponent<UnityEngine.Camera>();
                }
            }
        }

        private void Start()
        {
            _firstPerson = _startInFirstPerson;
            FindPlayer();
            ApplyMode();
            if (_lockCursorOnStart)
            {
                SetCursorLocked(true);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            bool togglePressed =
                (keyboard != null && keyboard.vKey.wasPressedThisFrame) ||
                (gamepad != null && gamepad.rightStickButton.wasPressedThisFrame);
            if (togglePressed)
            {
                ToggleFirstPerson();
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }
        }

        // Runs after the character, so the click that recaptures the cursor
        // is not seen as a fire press this frame.
        private void LateUpdate()
        {
            if (_player == null && Time.frameCount % 30 == 0)
            {
                FindPlayer();
            }

            if (_player != null)
            {
                Vector3 eye = _player.transform.position + Vector3.up * _eyeHeight;
                Quaternion aim = Quaternion.Euler(_player.Pitch, _player.Yaw, 0f);
                _cameraTarget.SetPositionAndRotation(eye, aim);

                if (_builtInCamera != null)
                {
                    PlaceBuiltInCamera(eye, aim);
                }
            }

            Mouse mouse = Mouse.current;
            if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
            }
        }

        private void PlaceBuiltInCamera(Vector3 eye, Quaternion aim)
        {
            Vector3 position = eye;
            if (!_firstPerson)
            {
                position = eye + aim * Vector3.back * _thirdPersonDistance;

                // Pull the camera in front of any wall between the eye and the wanted spot.
                RaycastHit hit;
                if (Physics.Linecast(eye, position, out hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    position = hit.point + hit.normal * WallPadding;
                }
            }

            _builtInCamera.transform.SetPositionAndRotation(position, aim);
        }

        private void FindPlayer()
        {
            _player = FindFirstObjectByType<ManagedCharacter>();
            _caster = FindFirstObjectByType<ManagedSpellCaster>();
            if (_player != null && (_bodyRenderers == null || _bodyRenderers.Length == 0))
            {
                _bodyRenderers = _player.GetComponentsInChildren<Renderer>();
                ApplyMode();
            }
        }

        private void ApplyMode()
        {
            if (_thirdPersonCamera != null)
            {
                _thirdPersonCamera.SetActive(!_firstPerson);
            }

            if (_firstPersonCamera != null)
            {
                _firstPersonCamera.SetActive(_firstPerson);
            }

            if (_bodyRenderers != null)
            {
                ShadowCastingMode mode = _firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                for (int i = 0; i < _bodyRenderers.Length; i++)
                {
                    if (_bodyRenderers[i] != null)
                    {
                        _bodyRenderers[i].shadowCastingMode = mode;
                    }
                }
            }
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnGUI()
        {
            if (_showCrosshair && _player != null)
            {
                DrawCrosshair();
            }

            if (!_showDebugOverlay)
            {
                return;
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            }

            _text.Length = 0;
            _text.Append("GTG dev overlay (managed stack)\n");
            _text.Append("View: ").Append(_firstPerson ? "first person" : "third person").Append("  (V or right stick click)\n");

            if (_player != null)
            {
                Vector3 position = _player.transform.position;
                _text.Append("Pos ").Append(position.ToString("F2"))
                    .Append("  Grounded ").Append(_player.IsGrounded)
                    .Append("  vy ").Append(_player.VerticalVelocity.ToString("F1")).Append('\n');
                _text.Append("Yaw ").Append(_player.Yaw.ToString("F0"))
                    .Append("  Pitch ").Append(_player.Pitch.ToString("F0")).Append('\n');
            }
            else
            {
                _text.Append("Character: NONE (add ManagedCharacter to a GameObject)\n");
            }

            if (_caster != null)
            {
                _text.Append("Spell: ").Append(_caster.SelectedSpell.DisplayName)
                    .Append(" (").Append(_caster.SelectedVesselText).Append(')')
                    .Append("  (1, 2, 3, Tab or d-pad)  Shots in flight ").Append(_caster.ShotCount).Append('\n');
            }

            _text.Append("Cursor ").Append(Cursor.lockState).Append("  (Esc releases, click captures)\n");
            _text.Append("Move WASD or arrows  Jump Space  Cast LMB, F or right trigger");

            GUI.Label(new Rect(12f, 10f, 900f, 160f), _text.ToString(), _style);

            if (_player != null)
            {
                DrawFeatureToggles();
            }
        }

        private void DrawCrosshair()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 9f, 2f, 7f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy + 2f, 2f, 7f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 9f, cy - 1f, 7f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2f, cy - 1f, 7f, 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawFeatureToggles()
        {
            GUILayout.BeginArea(new Rect(12f, 150f, 700f, 30f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Modules:", GUILayout.Width(70f));

            ManagedCharacterFeature enabled = _player.Features;
            enabled = Toggle(enabled, ManagedCharacterFeature.Look, "Look");
            enabled = Toggle(enabled, ManagedCharacterFeature.Move, "Move");
            enabled = Toggle(enabled, ManagedCharacterFeature.Jump, "Jump");
            enabled = Toggle(enabled, ManagedCharacterFeature.Gravity, "Gravity");
            enabled = Toggle(enabled, ManagedCharacterFeature.Cast, "Cast");

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            _player.Features = enabled;
        }

        private static ManagedCharacterFeature Toggle(
            ManagedCharacterFeature current, ManagedCharacterFeature feature, string label)
        {
            bool on = GUILayout.Toggle((current & feature) != 0, label, GUILayout.Width(80f));
            return on ? current | feature : current & ~feature;
        }
    }
}
