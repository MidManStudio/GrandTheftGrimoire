// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedNpcBrain.cs"
// ============================================================================

using System;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.NPC.Components;
using MidManStudio.Gtg.NPC.Native;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>
    /// One NPC. It holds the settings the Rust decision needs, works out what the NPC can
    /// see, and stores the action that came back. It does not move, fight or trade, other
    /// components read Action and do that. The ManagedNpcDirector drives it in batches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedNpcBrain : MonoBehaviour
    {
        [Header("Decision")]
        [SerializeField] private NpcRole _role = NpcRole.Enemy;
        [SerializeField] private DecisionBackend _backend = DecisionBackend.Utility;
        [Tooltip("A merchant that never leaves its stall stays off.")]
        [SerializeField] private bool _canMove = true;
        [Tooltip("Stored for the archetype data. Nothing reads it yet.")]
        [SerializeField] private LearningMode _learningMode = LearningMode.Disabled;

        [Header("Sight")]
        [SerializeField] private float _viewRange = 20f;
        [Range(0f, 360f)]
        [SerializeField] private float _fieldOfViewDegrees = 110f;
        [Tooltip("Where the eye is, in local space. The default is 1.6 m up from the feet.")]
        [SerializeField] private Vector3 _eyeOffset = new Vector3(0f, 1.6f, 0f);
        [Tooltip("Layers a sight ray can hit. Leave the NPC's own colliders out, or put the eye outside them, otherwise they block the view.")]
        [SerializeField] private LayerMask _sightMask = ~0;
        [Tooltip("Seconds a threat stays visible after the last ray that saw it. Zero turns the lingering off.")]
        [SerializeField] private float _loseSightDelay = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool _logActionChanges;

        private ManagedHealth _health;
        private float _lastSeenTime = float.NegativeInfinity;
        private bool _threatVisible;
        private ManagedNpcThreatSource _threat;
        private Vector3 _lastKnownThreatPosition;
        private NpcAction _action = NpcAction.Idle;

        /// <summary>(this, old action, new action). Raised on the main thread inside the director tick.</summary>
        public event Action<ManagedNpcBrain, NpcAction, NpcAction> ActionChanged;

        /// <summary>Unique among live NPCs of this play session. Not stable between sessions.</summary>
        public ulong Id { get; internal set; }

        public NpcRole Role { get { return _role; } }
        public DecisionBackend Backend { get { return _backend; } }
        public bool CanMove { get { return _canMove; } }
        public LearningMode LearningMode { get { return _learningMode; } }

        /// <summary>The last decision. Idle until the first tick, and again once dead.</summary>
        public NpcAction Action { get { return _action; } }

        public bool ThreatVisible { get { return _threatVisible; } }

        /// <summary>
        /// The threat seen last, or null when none has been seen or the memory has run out. It stays
        /// set while the lingering time runs, so an NPC keeps chasing a target that just stepped
        /// behind cover.
        /// </summary>
        public ManagedNpcThreatSource Threat { get { return _threat; } }

        /// <summary>Where the threat was when it was last seen. Only meaningful while Threat is set.</summary>
        public Vector3 LastKnownThreatPosition { get { return _lastKnownThreatPosition; } }

        public bool IsAlive { get { return _health == null || _health.IsAlive; } }

        public float HealthFraction { get { return _health != null ? _health.Fraction : 1f; } }

        public Vector3 EyePosition { get { return transform.TransformPoint(_eyeOffset); } }

        public float ViewRange { get { return _viewRange; } }
        public float FieldOfViewDegrees { get { return _fieldOfViewDegrees; } }

        private void Awake()
        {
            _health = GetComponent<ManagedHealth>();
        }

        private void OnEnable()
        {
            ManagedNpcDirector.Register(this);
        }

        private void OnDisable()
        {
            ManagedNpcDirector.Unregister(this);
        }

        private void OnValidate()
        {
            _viewRange = Mathf.Max(0f, _viewRange);
            _loseSightDelay = Mathf.Max(0f, _loseSightDelay);
        }

        /// <summary>
        /// Looks for a threat. Each ray spends one from the budget, and an NPC that reaches
        /// the end of the budget keeps what it saw last tick. Returns false in that case.
        /// </summary>
        internal bool UpdateSight(float now, ref int rayBudget)
        {
            Vector3 eye = EyePosition;
            Vector3 forward = transform.forward;
            float cosHalfFov = ManagedNpcSight.CosHalfFieldOfView(_fieldOfViewDegrees);
            int mask = _sightMask;
            bool seen = false;
            bool outOfBudget = false;
            ManagedNpcThreatSource seenSource = null;

            for (int s = 0; s < ManagedNpcThreatSource.Count && !seen && !outOfBudget; s++)
            {
                ManagedNpcThreatSource source = ManagedNpcThreatSource.Get(s);
                if (!source.IsActive || IsSelf(source.transform))
                {
                    continue;
                }

                for (int p = 0; p < source.PointCount; p++)
                {
                    Vector3 point = source.GetPoint(p);
                    if (!ManagedNpcSight.InView(eye, forward, point, _viewRange, cosHalfFov))
                    {
                        continue;
                    }

                    if (rayBudget <= 0)
                    {
                        outOfBudget = true;
                        break;
                    }

                    rayBudget--;
                    if (ManagedNpcSight.HasLineOfSight(eye, point, source.transform, mask))
                    {
                        seen = true;
                        seenSource = source;
                        break;
                    }
                }
            }

            if (seen)
            {
                _lastSeenTime = now;
                _threatVisible = true;
                _threat = seenSource;
                _lastKnownThreatPosition = seenSource.transform.position;
            }
            else if (!outOfBudget)
            {
                _threatVisible = now - _lastSeenTime <= _loseSightDelay;
                if (!_threatVisible || (_threat != null && !_threat.IsActive))
                {
                    _threat = null;
                }
            }

            return !outOfBudget;
        }

        internal NPCNativeObservation BuildObservation()
        {
            return new NPCNativeObservation
            {
                NpcId = Id,
                Role = (uint)_role,
                Backend = (uint)_backend,
                ThreatVisible = _threatVisible ? 1u : 0u,
                HealthFraction = HealthFraction,
                CanMove = _canMove ? 1u : 0u,
                Reserved = 0
            };
        }

        internal void ApplyAction(NpcAction action)
        {
            if (action == _action)
            {
                return;
            }

            NpcAction previous = _action;
            _action = action;
            if (_logActionChanges)
            {
                Debug.Log("[NPC " + name + "] " + previous + " -> " + action);
            }

            Action<ManagedNpcBrain, NpcAction, NpcAction> changed = ActionChanged;
            if (changed != null)
            {
                changed(this, previous, action);
            }
        }

        // A threat source on this NPC or on one of its parents is not a threat to itself.
        private bool IsSelf(Transform sourceTransform)
        {
            return transform.IsChildOf(sourceTransform) || sourceTransform.IsChildOf(transform);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 eye = EyePosition;
            Gizmos.color = _threatVisible ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(eye, 0.15f);

            float half = Mathf.Clamp(_fieldOfViewDegrees, 0f, 360f) * 0.5f;
            Vector3 left = Quaternion.AngleAxis(-half, Vector3.up) * transform.forward;
            Vector3 right = Quaternion.AngleAxis(half, Vector3.up) * transform.forward;
            Gizmos.DrawRay(eye, left * _viewRange);
            Gizmos.DrawRay(eye, right * _viewRange);
            Gizmos.DrawRay(eye, transform.forward * _viewRange);
        }
    }
}
