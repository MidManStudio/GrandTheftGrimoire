// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/camera.md, section "CharacterCameraBridge.cs"
// ============================================================================

using System;
using System.Text;
using MidManStudio.Gtg.CharacterController;
using MidManStudio.Gtg.Magic;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace MidManStudio.Gtg.Camera
{
    /// <summary>
    /// Copies the ECS character into GameObjects each frame: the visible body capsule and
    /// the camera target that Cinemachine follows. Switches between the third person and
    /// first person cameras, owns cursor capture, and draws a debug overlay that reports
    /// the health of the ECS world and lets each character module be switched on and off.
    /// Inside MidManStudio.Gtg namespaces write UnityEngine.Camera in full, because the
    /// simple name Camera resolves to this namespace.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class CharacterCameraBridge : MonoBehaviour
    {
        [Header("Scene references")]
        [Tooltip("Visible capsule outside the SubScene. Moved to the ECS character every frame.")]
        [SerializeField] private Transform _playerView;
        [Tooltip("Cinemachine Follow target. Sits at the eye point and carries yaw and pitch.")]
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

        private static readonly Type[] CharacterSystems =
        {
            typeof(CharacterInputSystem),
            typeof(CharacterLookSystem),
            typeof(CharacterGroundSystem),
            typeof(CharacterWalkSystem),
            typeof(CharacterJumpSystem),
            typeof(CharacterGravitySystem),
            typeof(CharacterMovementSystem),
            typeof(FireballCastSystem),
            typeof(FireballProjectileSystem),
        };

        private World _world;
        private EntityQuery _characterQuery;
        private EntityQuery _physicsQuery;
        private EntityQuery _commandBufferQuery;
        private Entity _player;
        private bool _hasPlayer;
        private bool _firstPerson;
        private bool _physicsReady;
        private bool _commandBufferReady;
        private string _missingSystems = string.Empty;
        private int _characterCount;
        private bool _hasCollider;
        private CharacterFeatures _features;
        private Vector3 _position;
        private float _verticalSpeed;
        private float _yaw;
        private float _pitch;
        private bool _grounded;
        private GUIStyle _style;
        private readonly StringBuilder _text = new StringBuilder(512);

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
                _world = null;
                _hasPlayer = false;
                return;
            }

            EntityManager entityManager = world.EntityManager;
            if (world != _world)
            {
                _world = world;
                _characterQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<CharacterTag>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<CharacterLook>(),
                    ComponentType.ReadOnly<CharacterMotor>());
                _physicsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
                _commandBufferQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<EndSimulationEntityCommandBufferSystem.Singleton>());
            }

            _physicsReady = _physicsQuery.CalculateEntityCount() > 0;
            _commandBufferReady = _commandBufferQuery.CalculateEntityCount() > 0;
            _missingSystems = FindMissingSystems(world);

            _characterCount = _characterQuery.CalculateEntityCount();
            _hasPlayer = _characterCount == 1;
            if (!_hasPlayer)
            {
                return;
            }

            _player = _characterQuery.GetSingletonEntity();
            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(_player);
            CharacterLook look = entityManager.GetComponentData<CharacterLook>(_player);
            CharacterMotor motor = entityManager.GetComponentData<CharacterMotor>(_player);
            _hasCollider = entityManager.HasComponent<PhysicsCollider>(_player);
            _features = entityManager.HasComponent<CharacterFeatures>(_player)
                ? entityManager.GetComponentData<CharacterFeatures>(_player)
                : default;

            _position = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z);
            _verticalSpeed = motor.VerticalVelocity;
            _yaw = look.Yaw;
            _pitch = look.Pitch;
            _grounded = motor.IsGrounded;

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

        private static string FindMissingSystems(World world)
        {
            string missing = string.Empty;
            for (int i = 0; i < CharacterSystems.Length; i++)
            {
                if (world.GetExistingSystem(CharacterSystems[i]).Equals(SystemHandle.Null))
                {
                    string name = CharacterSystems[i].Name.Replace("System", string.Empty);
                    missing = missing.Length == 0 ? name : missing + ", " + name;
                }
            }

            return missing;
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
                _style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            }

            _text.Length = 0;
            _text.Append("GTG dev overlay\n");
            _text.Append("View: ").Append(_firstPerson ? "first person" : "third person").Append("  (V or right stick click)\n");
            _text.Append("ECS world: ").Append(_world != null ? "ok" : "MISSING").Append('\n');
            _text.Append("Physics world: ").Append(_physicsReady ? "ok" : "MISSING (physics systems did not start)").Append('\n');
            _text.Append("Command buffer: ").Append(_commandBufferReady ? "ok" : "MISSING (ECB systems did not start)").Append('\n');
            _text.Append("Character systems: ")
                .Append(_missingSystems.Length == 0 ? "all created" : "NOT CREATED: " + _missingSystems).Append('\n');

            if (_hasPlayer)
            {
                _text.Append("Character: found, collider ").Append(_hasCollider ? "ok" : "MISSING (add a CapsuleCollider)").Append('\n');
                _text.Append("Pos ").Append(_position.ToString("F2")).Append("  Grounded ").Append(_grounded)
                    .Append("  vy ").Append(_verticalSpeed.ToString("F1")).Append('\n');
                _text.Append("Yaw ").Append(_yaw.ToString("F0")).Append("  Pitch ").Append(_pitch.ToString("F0")).Append('\n');
            }
            else
            {
                _text.Append(_characterCount == 0
                    ? "Character: NONE (is CharacterAuthoring inside an open SubScene?)\n"
                    : "Character: more than one entity\n");
            }

            _text.Append("Cursor ").Append(Cursor.lockState).Append("  (Esc releases, click captures)\n");
            _text.Append("Move WASD  Jump Space  Fireball LMB, F or right trigger");

            GUI.Label(new Rect(12f, 10f, 900f, 220f), _text.ToString(), _style);

            if (_hasPlayer)
            {
                DrawFeatureToggles();
            }
        }

        private void DrawFeatureToggles()
        {
            GUILayout.BeginArea(new Rect(12f, 232f, 700f, 30f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Modules:", GUILayout.Width(70f));

            CharacterFeature enabled = _features.Enabled;
            enabled = Toggle(enabled, CharacterFeature.Look, "Look");
            enabled = Toggle(enabled, CharacterFeature.Move, "Move");
            enabled = Toggle(enabled, CharacterFeature.Jump, "Jump");
            enabled = Toggle(enabled, CharacterFeature.Gravity, "Gravity");
            enabled = Toggle(enabled, CharacterFeature.Cast, "Cast");

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (enabled != _features.Enabled && _world != null && _world.IsCreated)
            {
                _world.EntityManager.SetComponentData(_player, new CharacterFeatures { Enabled = enabled });
            }
        }

        private static CharacterFeature Toggle(CharacterFeature current, CharacterFeature feature, string label)
        {
            bool on = GUILayout.Toggle((current & feature) != 0, label, GUILayout.Width(80f));
            return on ? current | feature : current & ~feature;
        }
    }
}
