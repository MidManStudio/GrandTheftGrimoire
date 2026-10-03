// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCharacter.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.CharacterController
{
    /// <summary>
    /// Kinematic character on top of UnityEngine.CharacterController. Each frame it reads
    /// the input, then runs the look, walk, jump and gravity modules in that order and
    /// moves the capsule. Put it on an empty GameObject at the feet. Inside MidManStudio.Gtg
    /// namespaces the simple name CharacterController resolves to a namespace, so the Unity
    /// component is written in full.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.CharacterController))]
    public sealed class ManagedCharacter : MonoBehaviour
    {
        [Header("Modules")]
        [SerializeField] private ManagedCharacterFeature _features = ManagedCharacterFeature.All;

        [Header("Move")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _acceleration = 60f;
        [Range(0f, 1f)]
        [SerializeField] private float _airControl = 0.4f;

        [Header("Jump")]
        [SerializeField] private float _jumpSpeed = 7f;
        [SerializeField] private float _coyoteTime = 0.1f;

        [Header("Gravity")]
        [SerializeField] private float _gravity = -20f;
        [SerializeField] private float _gravityCap = 50f;
        [SerializeField] private float _groundStickSpeed = 2f;

        [Header("Collision")]
        [SerializeField] private float _maxSlopeDegrees = 50f;
        [SerializeField] private float _skinWidth = 0.03f;

        [Header("Look")]
        [SerializeField] private float _mouseDegreesPerPixel = 0.1f;
        [SerializeField] private float _stickDegreesPerSecond = 180f;
        [Tooltip("Look speed of the keyboard keys, J and L turn, I and K look up and down. Zero turns them off.")]
        [SerializeField] private float _keyboardLookDegreesPerSecond = 120f;
        [SerializeField] private float _minPitch = -80f;
        [SerializeField] private float _maxPitch = 80f;

        [Header("Body view")]
        [Tooltip("Adds a capsule mesh under the character when it has no renderer of its own.")]
        [SerializeField] private bool _createBodyView = true;

        private UnityEngine.CharacterController _controller;
        private ManagedCharacterInputFrame _input;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _timeSinceGrounded;
        private float _yaw;
        private float _pitch;
        private bool _grounded;

        /// <summary>View direction in degrees. A positive pitch looks down.</summary>
        public float Yaw { get { return _yaw; } }
        public float Pitch { get { return _pitch; } }
        public bool IsGrounded { get { return _grounded; } }
        public float VerticalVelocity { get { return _verticalVelocity; } }
        public ManagedCharacterInputFrame Input { get { return _input; } }

        /// <summary>Writable at runtime, so a stun or a cutscene only flips a flag.</summary>
        public ManagedCharacterFeature Features
        {
            get { return _features; }
            set { _features = value; }
        }

        public bool Has(ManagedCharacterFeature feature)
        {
            return (_features & feature) != 0;
        }

        // Shapes the capsule to a 2 m tall character with the pivot at the feet.
        private void Reset()
        {
            UnityEngine.CharacterController capsule = GetComponent<UnityEngine.CharacterController>();
            if (capsule != null)
            {
                capsule.height = 2f;
                capsule.radius = 0.5f;
                capsule.center = new Vector3(0f, 1f, 0f);
                capsule.stepOffset = 0.3f;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<UnityEngine.CharacterController>();
            _controller.slopeLimit = _maxSlopeDegrees;
            _controller.skinWidth = Mathf.Max(0.0001f, _skinWidth);
            _yaw = transform.eulerAngles.y;

            if (_createBodyView && GetComponentInChildren<Renderer>() == null)
            {
                CreateBodyView();
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _input = ManagedCharacterInput.Read(
                dt, _mouseDegreesPerPixel, _stickDegreesPerSecond, _keyboardLookDegreesPerSecond);

            ApplyLook();
            _timeSinceGrounded = _grounded ? 0f : _timeSinceGrounded + dt;
            ApplyWalk(dt);
            ApplyJump();
            ApplyGravity(dt);
            Step(dt);
        }

        private void ApplyLook()
        {
            if (!Has(ManagedCharacterFeature.Look))
            {
                return;
            }

            _yaw = (_yaw + _input.Look.x) % 360f;
            _pitch = Mathf.Clamp(_pitch - _input.Look.y, _minPitch, _maxPitch);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        // With the module off the target is zero, so the character glides to a stop.
        private void ApplyWalk(float dt)
        {
            Vector3 target = Vector3.zero;
            if (Has(ManagedCharacterFeature.Move))
            {
                Quaternion yaw = Quaternion.Euler(0f, _yaw, 0f);
                target = yaw * new Vector3(_input.Move.x, 0f, _input.Move.y) * _moveSpeed;
            }

            float acceleration = _acceleration * (_grounded ? 1f : _airControl);
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, target, acceleration * dt);
        }

        // The jump spends the coyote time, so there is no second jump in the air.
        private void ApplyJump()
        {
            if (!Has(ManagedCharacterFeature.Jump) || !_input.JumpPressed)
            {
                return;
            }

            bool canJump = _grounded || _timeSinceGrounded <= _coyoteTime;
            if (!canJump || _verticalVelocity > 0f)
            {
                return;
            }

            _verticalVelocity = _jumpSpeed;
            _grounded = false;
            _timeSinceGrounded = _coyoteTime + 1f;
        }

        // With the module off the vertical speed is left alone, which a flight module can use.
        private void ApplyGravity(float dt)
        {
            if (!Has(ManagedCharacterFeature.Gravity))
            {
                return;
            }

            if (_grounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = -_groundStickSpeed;
                return;
            }

            _verticalVelocity = Mathf.Max(_verticalVelocity + _gravity * dt, -_gravityCap);
        }

        // The ground state comes from the contact flags of this move. A rising character
        // reports no floor contact, which keeps the takeoff frame from cancelling the jump.
        private void Step(float dt)
        {
            Vector3 displacement = (_horizontalVelocity + Vector3.up * _verticalVelocity) * dt;
            CollisionFlags flags = _controller.Move(displacement);

            _grounded = (flags & CollisionFlags.Below) != 0;
            if (_grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = 0f;
            }
            else if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            {
                _verticalVelocity = 0f;
            }
        }

        private void CreateBodyView()
        {
            GameObject view = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            view.name = "BodyView";

            // Disabled first, because Destroy only runs at the end of the frame and the
            // controller would otherwise collide with its own body on the first move.
            Collider collider = view.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);

            view.transform.SetParent(transform, false);
            view.transform.localPosition = new Vector3(0f, 1f, 0f);
        }
    }
}
