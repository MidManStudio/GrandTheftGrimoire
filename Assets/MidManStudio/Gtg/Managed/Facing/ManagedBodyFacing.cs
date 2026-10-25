// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedBodyFacing.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Camera;
using MidManStudio.Gtg.Managed.CharacterController;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Facing
{
    /// <summary>
    /// Turns the character body apart from the aim. The character root carries the aim yaw, which
    /// the camera follows, so the body is a child that turns on its own. In third person it turns
    /// toward the way the character walks and holds still at rest. It faces the aim in first person,
    /// and for a moment after a fire press, so a cast or a swing goes where the crosshair points. The
    /// camera and the aim never read the body. Put it on the same GameObject as ManagedCharacter.
    /// </summary>
    [DefaultExecutionOrder(-35)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ManagedCharacter))]
    public sealed class ManagedBodyFacing : MonoBehaviour
    {
        // Frames between searches for a camera rig that is not there yet.
        private const int SearchInterval = 30;

        [Tooltip("The child that turns, the character model or the created body view. Found under the character when empty. It cannot be the character root, which carries the aim.")]
        [SerializeField] private Transform _body;
        [SerializeField] private float _turnDegreesPerSecond = 720f;
        [Tooltip("Seconds the body keeps facing the aim after a fire press.")]
        [SerializeField] private float _aimHoldSeconds = 0.6f;
        [Tooltip("A move input no longer than this does not turn the body.")]
        [SerializeField] private float _moveDeadZone = 0.1f;

        private ManagedCharacter _character;
        private ManagedCameraRig _rig;
        private float _bodyYaw;
        private float _aimHold;

        /// <summary>The yaw the body faces now, in degrees.</summary>
        public float BodyYaw { get { return _bodyYaw; } }

        private void Awake()
        {
            _character = GetComponent<ManagedCharacter>();
        }

        private void Start()
        {
            if (_body == transform)
            {
                Debug.LogWarning("[GTG Facing] Body is the character root, which carries the aim. Assign a child.", this);
                _body = null;
            }

            if (_body == null)
            {
                _body = transform.Find("BodyView");
            }

            if (_body == null)
            {
                Debug.Log("[GTG Facing] No child to turn under the character, so facing is idle. Assign Body when the character model is a child.", this);
                enabled = false;
                return;
            }

            _bodyYaw = _character.Yaw;
            _rig = FindFirstObjectByType<ManagedCameraRig>();
        }

        private void Update()
        {
            if (_rig == null && Time.frameCount % SearchInterval == 0)
            {
                _rig = FindFirstObjectByType<ManagedCameraRig>();
            }

            float dt = Time.deltaTime;
            if (_character.FireWasPressed && _character.Has(ManagedCharacterFeature.Cast))
            {
                _aimHold = _aimHoldSeconds;
            }
            else
            {
                _aimHold = Mathf.Max(0f, _aimHold - dt);
            }

            bool firstPerson = _rig != null && _rig.IsFirstPerson;
            Vector2 move = _character.Has(ManagedCharacterFeature.Move) ? _character.Input.Move : Vector2.zero;
            float target = ManagedBodyTurn.Target(_bodyYaw, _character.Yaw, move, firstPerson || _aimHold > 0f, _moveDeadZone);

            // In first person the body is not seen, only its shadow, so it follows the aim at once.
            _bodyYaw = firstPerson ? target : ManagedBodyTurn.Step(_bodyYaw, target, _turnDegreesPerSecond, dt) % 360f;
            _body.rotation = Quaternion.Euler(0f, _bodyYaw, 0f);
        }
    }
}
