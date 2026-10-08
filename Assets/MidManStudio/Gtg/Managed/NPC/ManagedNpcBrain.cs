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
        [Tooltip("How a guard, enemy or boss reacts to a threat. Hostile attacks on sight, Retaliatory only after being attacked, Peaceful never fights and runs.")]
        [SerializeField] private NpcDisposition _disposition = NpcDisposition.Hostile;
        [Tooltip("The NPC's level. A companion refuses an order whose level is above this by more than the gap for that order type: raid 1, attack 3, follow and hold 5, deliver 6.")]
        [SerializeField] private int _level = 1;
        [Tooltip("Seconds after being hit by something with a source that the NPC counts as provoked.")]
        [SerializeField] private float _provokedMemorySeconds = 20f;

        [Header("Companion")]
        [Tooltip("What a companion follows and never sees as a threat. Normally the player.")]
        [SerializeField] private Transform _leader;
        [Range(0f, 1f)]
        [Tooltip("How trustworthy the companion is. A weight in its loyalty, which matters only for betrayal.")]
        [SerializeField] private float _trustworthiness = 0.5f;
        [Range(0f, 1f)]
        [Tooltip("How much the companion likes the player. A weight in its loyalty.")]
        [SerializeField] private float _affinity = 0.5f;
        [Range(0f, 1f)]
        [Tooltip("How satisfied the companion is with its pay. A weight in its loyalty.")]
        [SerializeField] private float _paySatisfaction = 0.5f;

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
        private NpcOrder _order = NpcOrder.None;
        private int _orderLevel;
        private float _provokedUntil = float.NegativeInfinity;
        private GameObject _lastAttacker;
        private bool _warnedNoLeader;
        private bool _betrayalOpportunity;
        private uint _noiseState;

        /// <summary>(this, old action, new action). Raised on the main thread inside the director tick.</summary>
        public event Action<ManagedNpcBrain, NpcAction, NpcAction> ActionChanged;

        /// <summary>(this, the order that was refused, its level). Raised when a companion declines an order. The order is cleared before this fires.</summary>
        public event Action<ManagedNpcBrain, NpcOrder, int> OrderRefused;

        /// <summary>
        /// Raised when a companion betrays the player. The opportunity is used up before this fires, so a
        /// handler that wants another chance has to set it again. What the betrayal does is up to the handler.
        /// </summary>
        public event Action<ManagedNpcBrain> Betrayed;

        /// <summary>Unique among live NPCs of this play session. Not stable between sessions.</summary>
        public ulong Id { get; internal set; }

        public NpcRole Role { get { return _role; } }
        public DecisionBackend Backend { get { return _backend; } }
        public bool CanMove { get { return _canMove; } }
        public LearningMode LearningMode { get { return _learningMode; } }
        public NpcDisposition Disposition { get { return _disposition; } set { _disposition = value; } }

        /// <summary>The NPC's level. Negative values count as zero.</summary>
        public int Level { get { return _level; } set { _level = Mathf.Max(0, value); } }

        /// <summary>What a companion follows and never sees as a threat.</summary>
        public Transform Leader { get { return _leader; } set { _leader = value; } }

        /// <summary>How trustworthy a companion is, from 0 to 1. Values outside that range are clamped.</summary>
        public float Trustworthiness { get { return _trustworthiness; } set { _trustworthiness = Mathf.Clamp01(value); } }

        /// <summary>How much a companion likes the player, from 0 to 1.</summary>
        public float Affinity { get { return _affinity; } set { _affinity = Mathf.Clamp01(value); } }

        /// <summary>How satisfied a companion is with its pay, from 0 to 1.</summary>
        public float PaySatisfaction { get { return _paySatisfaction; } set { _paySatisfaction = Mathf.Clamp01(value); } }

        /// <summary>
        /// True while the game lets the companion betray the player. The game decides when, for instance when
        /// the companion is alone with something valuable, and never sets it for a companion the story
        /// protects. A scripted betrayal does not use this: the story changes the companion directly.
        /// </summary>
        public bool BetrayalOpportunity { get { return _betrayalOpportunity; } set { _betrayalOpportunity = value; } }

        /// <summary>The weighted mix of trust, liking and pay, from 0 to 1. A companion at 0.5 or more never betrays.</summary>
        public float Loyalty
        {
            get
            {
                NPCNativeObservation mix = new NPCNativeObservation
                {
                    Trustworthiness = _trustworthiness,
                    Affinity = _affinity,
                    PaySatisfaction = _paySatisfaction
                };
                return NPCManagedFallback.Loyalty(mix);
            }
        }

        /// <summary>The standing order. Only a companion acts on it.</summary>
        public NpcOrder Order { get { return _order; } }

        /// <summary>How hard the order is, as a level.</summary>
        public int OrderLevel { get { return _orderLevel; } }

        /// <summary>True for a while after the NPC was hit by something with a source.</summary>
        public bool Provoked { get { return Time.time < _provokedUntil; } }

        /// <summary>What hit the NPC last, or null.</summary>
        public GameObject LastAttacker { get { return _lastAttacker; } }

        /// <summary>
        /// Gives a companion an order. The level is how hard the order is, 0 for one with no difficulty.
        /// The companion may refuse on the next decision, see OrderRefused. Other roles ignore orders.
        /// </summary>
        public void SetOrder(NpcOrder order, int orderLevel)
        {
            _order = order;
            _orderLevel = order == NpcOrder.None ? 0 : Mathf.Max(0, orderLevel);
        }

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
            if (_health != null)
            {
                _health.Damaged += OnDamaged;
            }

            ManagedNpcDirector.Register(this);
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
            }

            ManagedNpcDirector.Unregister(this);
        }

        // Damage with a source provokes. Damage with none, such as a hazard, does not. Hits from itself or
        // from its own leader do not either, so friendly fire does not turn a companion on the player.
        private void OnDamaged(ManagedHealth health, float amount, GameObject source)
        {
            if (source == null || IsSelf(source.transform) || IsLeader(source.transform))
            {
                return;
            }

            _lastAttacker = source;
            _provokedUntil = Time.time + _provokedMemorySeconds;
        }

        private void OnValidate()
        {
            _viewRange = Mathf.Max(0f, _viewRange);
            _loseSightDelay = Mathf.Max(0f, _loseSightDelay);
            _level = Mathf.Max(0, _level);
            _provokedMemorySeconds = Mathf.Max(0f, _provokedMemorySeconds);
            _trustworthiness = Mathf.Clamp01(_trustworthiness);
            _affinity = Mathf.Clamp01(_affinity);
            _paySatisfaction = Mathf.Clamp01(_paySatisfaction);
        }

        /// <summary>
        /// Looks for a threat. Each ray spends one from the budget, and an NPC that reaches
        /// the end of the budget keeps what it saw last tick. Returns false in that case.
        /// </summary>
        internal bool UpdateSight(float now, ref int rayBudget)
        {
            if (_role == NpcRole.Companion && _leader == null && !_warnedNoLeader)
            {
                _warnedNoLeader = true;
                Debug.LogWarning("[GTG NPC] " + name + " is a companion with no Leader, so it sees every threat source, the player included, as a threat. Set its Leader.");
            }

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
                if (!source.IsActive || IsSelf(source.transform) || IsLeader(source.transform))
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
                Reserved = 0,
                Disposition = (byte)_disposition,
                Order = (byte)_order,
                Provoked = Provoked ? (byte)1 : (byte)0,
                Level = ClampLevel(_level),
                OrderLevel = ClampLevel(_orderLevel),
                // The companion inputs are sent for companions only. Everyone else sends zeros, which Rust ignores.
                Trustworthiness = _role == NpcRole.Companion ? _trustworthiness : 0f,
                Affinity = _role == NpcRole.Companion ? _affinity : 0f,
                PaySatisfaction = _role == NpcRole.Companion ? _paySatisfaction : 0f,
                Noise = _role == NpcRole.Companion ? NextNoise() : 0u,
                BetrayalOpportunity = _role == NpcRole.Companion && _betrayalOpportunity ? (byte)1 : (byte)0
            };
        }

        // A random number for the next decision. Each brain has its own sequence, seeded from its id, so a run is
        // reproducible. xorshift32, never zero.
        private uint NextNoise()
        {
            uint x = _noiseState;
            if (x == 0u)
            {
                x = (uint)((Id * 0x9E3779B97F4A7C15UL) >> 32) | 1u;
            }

            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _noiseState = x;
            return x;
        }

        /// <summary>
        /// How an off-screen mission of this type would end for this companion, using its level. The player
        /// gets a notification and the reward when it is Success. A mission the player joins is played out
        /// instead and does not use this. False for an order that is not a mission.
        /// </summary>
        public bool TryResolveMission(NpcOrder order, int missionLevel, out NpcMissionOutcome outcome)
        {
            return ManagedMissionResolver.TryResolve(order, _level, missionLevel, NextNoise(), out outcome);
        }

        private static ushort ClampLevel(int level)
        {
            return (ushort)Mathf.Clamp(level, 0, ushort.MaxValue);
        }

        internal void ApplyAction(NpcAction action)
        {
            // A refusal always clears the order, even when the last action was a refusal too, otherwise a
            // second refused order given from a listener would stay in place.
            if (action == NpcAction.RefuseOrder)
            {
                NpcOrder refused = _order;
                int refusedLevel = _orderLevel;
                SetOrder(NpcOrder.None, 0);
                SetActionAndNotify(action);
                Action<ManagedNpcBrain, NpcOrder, int> declined = OrderRefused;
                if (declined != null && refused != NpcOrder.None)
                {
                    declined(this, refused, refusedLevel);
                }

                return;
            }

            if (action == NpcAction.Betray)
            {
                _betrayalOpportunity = false;
                SetActionAndNotify(action);
                Action<ManagedNpcBrain> betrayed = Betrayed;
                if (betrayed != null)
                {
                    betrayed(this);
                }

                return;
            }

            SetActionAndNotify(action);
        }

        private void SetActionAndNotify(NpcAction action)
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

        // A threat source on the leader or on one of its parents or children is never a threat to a companion.
        private bool IsLeader(Transform sourceTransform)
        {
            return _leader != null && (sourceTransform.IsChildOf(_leader) || _leader.IsChildOf(sourceTransform));
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
