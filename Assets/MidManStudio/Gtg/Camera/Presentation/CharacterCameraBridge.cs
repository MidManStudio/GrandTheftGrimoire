// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/camera.md, section "CharacterCameraBridge.cs"
// ============================================================================

using MidManStudio.Gtg.CharacterController;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace MidManStudio.Gtg.Camera
{
    /// <summary>
    /// Copies the ECS character into GameObjects each frame: the visible body
    /// capsule and the Cinemachine tracking target. Also switches between the
    /// third person and first person cameras and owns cursor capture.
    /// Inside MidManStudio.Gtg namespaces write UnityEngine.Camera in full,
    /// because the simple name Camera resolves to this namespace.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class CharacterCameraBridge : MonoBehaviour
    {
        [Header("Scene references")]
        [Tooltip("Visible capsule outside the SubScene. Moved to the ECS character every frame.")]
        [SerializeField] private Transform _playerView;
        [Tooltip("Cinemachine tracking target. Sits at the eye point and carries yaw and pitch.")]
        [SerializeField] private Transform _cameraTarget;
        [SerializeField] private GameObject _thirdPersonCamera;
        [SerializeField] private GameObject _firstPersonCamera;
        [Tooltip("Body renderers that stop drawing in first person. Shadows stay on.")]
        [SerializeField] private Renderer[] _bodyRenderers;

        [Header("Offsets")]
        [Tooltip("Body capsule offset from the feet pivot. A default capsule is 2 m tall, so 1 m.")]
        [SerializeField] private Vector3 _viewOffset = new Vector3(0f, 1f, 0f);
        [Tooltip("Eye height above the feet pivot.")]
        [SerializeField] private float _eyeHeight = 1.6f;

        [Header("Behaviour")]
        [SerializeField] private bool _startInFirstPerson;
        [SerializeField] private bool _lockCursorOnStart = true;
        [SerializeField] private bool _showDebugOverlay = true;

        private World _world;
        private EntityQuery _query;
        private bool _firstPerson;
        private string _status = "waiting for ECS world";
        private Vector3 _position;
        private float _yaw;
        private float _pitch;
        private bool _grounded;
        private GUIStyle _style;

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

        private void Start()
        {
            _firstPerson = _startInFirstPerson;
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

        private void LateUpdate()
        {
            SyncFromEcs();

            // Runs after the ECS systems, so the click that recaptures the cursor
            // is not seen as a fire press this frame.
            Mouse mouse = Mouse.current;
            if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
            }
        }

        private void SyncFromEcs()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                _status = "ECS world missing";
                return;
            }

            if (world != _world)
            {
                _world = world;
                _query = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<CharacterTag>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<CharacterLook>(),
                    ComponentType.ReadOnly<CharacterGroundState>());
            }

            int count = _query.CalculateEntityCount();
            if (count != 1)
            {
                _status = count == 0
                    ? "no character entity (is CharacterAuthoring inside an open SubScene?)"
                    : "more than one character entity";
                return;
            }

            EntityManager entityManager = world.EntityManager;
            Entity player = _query.GetSingletonEntity();
            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(player);
            CharacterLook look = entityManager.GetComponentData<CharacterLook>(player);
            CharacterGroundState ground = entityManager.GetComponentData<CharacterGroundState>(player);

            _status = "character entity found";
            _position = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z);
            _yaw = look.Yaw;
            _pitch = look.Pitch;
            _grounded = ground.IsGrounded;

            if (_playerView != null)
            {
                _playerView.SetPositionAndRotation(_position + _viewOffset, Quaternion.Euler(0f, _yaw, 0f));
            }

            if (_cameraTarget != null)
            {
                _cameraTarget.SetPositionAndRotation(
                    _position + Vector3.up * _eyeHeight,
                    Quaternion.Euler(_pitch, _yaw, 0f));
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
            if (!_showDebugOverlay)
            {
                return;
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            }

            string text =
                "GTG dev overlay\n" +
                "View: " + (_firstPerson ? "first person" : "third person") + "  (V or right stick click)\n" +
                "ECS: " + _status + "\n" +
                "Pos: " + _position.ToString("F2") + "  Grounded: " + _grounded + "\n" +
                "Yaw: " + _yaw.ToString("F0") + "  Pitch: " + _pitch.ToString("F0") + "\n" +
                "Cursor: " + Cursor.lockState + "  (Esc releases, click captures)\n" +
                "Move WASD  Jump Space  Fireball LMB, F or right trigger";
            GUI.Label(new Rect(12f, 10f, 760f, 170f), text, _style);
        }
    }
}
