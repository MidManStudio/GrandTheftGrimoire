// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedNpcActor.cs"
// ============================================================================

using System;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.NPC.Components;
using UnityEngine;
using UnityEngine.AI;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>
    /// Carries out the action the Rust decision chose. Patrol walks waypoints or wanders, Attack
    /// closes in on the threat, takes the place the target's engagement gives it, circles while it
    /// waits for a turn and hits in melee when it has one, Shoot does the same on the shooters' ring at
    /// the weapon's stand-off distance and fires from there, Kite backs away from a threat that is too close
    /// and fires on the way, Retreat runs away from it, Follow walks to
    /// the brain's Leader and stays near, and Idle, Trade, Hold, RefuseOrder, Mission and Betray stand
    /// still. An NPC that cannot move (a sentry, a shopkeeper behind a stall) stays at its post and strikes
    /// or fires from there when the target is within its reach. A mission and a betrayal are carried out by game code that listens to the brain.
    /// It moves the NPC with a NavMeshAgent, so the scene needs a baked NavMesh. It listens to the
    /// brain and never decides anything itself.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ManagedNpcBrain))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ManagedNpcActor : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField] private float _patrolSpeed = 1.8f;
        [SerializeField] private float _chaseSpeed = 3.8f;
        [SerializeField] private float _retreatSpeed = 4.2f;
        [SerializeField] private float _followSpeed = 3.4f;

        [Header("Patrol")]
        [Tooltip("Visited in order, then from the start. Empty means wandering around where the NPC began.")]
        [SerializeField] private Transform[] _waypoints;
        [SerializeField] private float _wanderRadius = 8f;
        [Tooltip("Seconds spent standing at each stop before the next one.")]
        [SerializeField] private float _pauseSeconds = 1.5f;

        [Header("Attack")]
        [SerializeField] private float _attackRange = 1.8f;
        [SerializeField] private float _attackDamage = 10f;
        [SerializeField] private float _attackCooldownSeconds = 1.2f;
        [SerializeField] private float _turnSpeedDegrees = 360f;
        [Tooltip("Seconds between refreshing the chase destination.")]
        [SerializeField] private float _repathIntervalSeconds = 0.25f;
        [Tooltip("A hit lands only when the target is within the attack range plus this, measured to where the target really is now and not to where it was last seen.")]
        [SerializeField] private float _hitLeewayMeters = 0.75f;

        [Header("Engagement")]
        [Tooltip("How far outside the melee ring the waiting ring lies. An attacker with no turn to strike stands and circles there.")]
        [SerializeField] private float _waitDistance = 2.5f;
        [Tooltip("Arc length kept free for each waiting attacker. Roughly twice the agent radius. A crowd of waiters widens the ring to keep it.")]
        [SerializeField] private float _ringSpacing = 1.2f;
        [Tooltip("How fast a waiting attacker circles the target, in meters per second. Zero makes it stand still on the ring.")]
        [SerializeField] private float _circleSpeed = 1.5f;
        [Tooltip("Seconds without moving toward its place before an attacker asks for another place on the ring.")]
        [SerializeField] private float _blockedSeconds = 1f;

        [Header("Retreat")]
        [SerializeField] private float _retreatDistance = 12f;

        [Header("Follow")]
        [Tooltip("A follower stops this close to its leader and starts walking again beyond this plus one meter.")]
        [SerializeField] private float _followDistance = 3f;

        [Header("Death")]
        [Tooltip("Seconds until a dead NPC is destroyed. Zero leaves the body in the scene.")]
        [SerializeField] private float _removeAfterDeathSeconds;

        private ManagedNpcBrain _brain;
        private NavMeshAgent _agent;
        private ManagedHealth _health;
        private ManagedNpcShooter _shooter;
        private NpcAction _mode = NpcAction.Idle;
        private Vector3 _home;
        private int _nextWaypoint;
        private bool _hasDestination;
        private float _resumeTime;
        private float _nextRepathTime;
        private float _nextAttackTime;
        private bool _stopped = true;
        private bool _configured;
        private bool _warnedNoNavMesh;
        private bool _warnedNoShooter;
        private bool _dead;

        // Where an attacker stands, see ManagedCombatEngagement.
        private ManagedCombatEngagement _engagement;
        private ManagedCombatEngagement.Member _member;
        private bool _autoRotation = true;
        private int _basePriority = 50;
        private int _priority = 50;
        private bool _inRange;
        private float _inRangeSince;
        private float _settleSeconds = SettleSeconds;
        private Vector3 _lastGoal;
        private float _progressCheckTime;
        private Vector3 _progressAnchor;
        private int _stuckSamples;

        // The melee ring sits at 70 percent of the attack range. The agent stops up to SlotStoppingDistance short of its
        // place, so the worst case is still inside the attack range with room to spare.
        private const float MeleeRingFraction = 0.7f;
        private const float SlotStoppingDistance = 0.25f;
        // A place further than this from the NavMesh counts as unreachable and the attacker asks for another one.
        private const float SlotSampleMeters = 1.5f;
        // An attacker this close to the waiting ring turns to face the target, see UpdateAttack.
        private const float EngagedMargin = 3f;
        // A holder strikes once it is this close to its place, or after SettleSeconds in range whatever the place. A shooter
        // comes into reach far from its place, so it is given the time to walk there on top of that.
        private const float SlotToleranceMeters = 0.6f;
        private const float SettleSeconds = 1.5f;
        // A waiter within this of its ring moves at the circling speed.
        private const float RingBandMeters = 1f;
        // A new place closer than this to the last one is not worth a new path.
        private const float GoalToleranceMeters = 0.4f;
        private const float ProgressSampleSeconds = 0.5f;
        private const float StuckMovementMeters = 0.1f;
        private const float StuckSlotDistanceMeters = 1f;
        // Holders push through, waiters give way.
        private const int PriorityStep = 10;

        /// <summary>(this, what was hit). Raised for every melee hit, for an animation or a sound.</summary>
        public event Action<ManagedNpcActor, IManagedDamageable> Attacked;

        public NpcAction Mode { get { return _mode; } }

        // Read-only state for the NPC debugger. Nothing in the game reads it.
        public Vector3 Home { get { return _home; } }
        public float AttackRange { get { return _attackRange; } }
        public float WanderRadius { get { return _wanderRadius; } }
        public float RetreatDistance { get { return _retreatDistance; } }
        public float FollowDistance { get { return _followDistance; } }
        public int WaypointCount { get { return _waypoints != null ? _waypoints.Length : 0; } }
        /// <summary>True while this attacker is part of its target's engagement. For debug tools.</summary>
        public bool IsEngaged { get { return _member != null; } }

        /// <summary>True while this attacker holds a place to strike or shoot from. For debug tools.</summary>
        public bool HasAttackToken { get { return _member != null && _member.HasToken; } }

        /// <summary>True while this attacker is in its target's engagement as a shooter. For debug tools.</summary>
        public bool IsShooter { get { return _member != null && _member.Ranged; } }

        /// <summary>The ranged weapon, or null for an NPC that has none. For debug tools.</summary>
        public ManagedNpcShooter Shooter { get { return _shooter; } }

        /// <summary>The place the engagement gave this attacker, or its own position when it has none. For debug tools.</summary>
        public Vector3 EngagementSlot { get { return _member != null ? _engagement.SlotPosition(_member) : transform.position; } }

        public int NextWaypointIndex { get { return WaypointCount > 0 ? _nextWaypoint % WaypointCount : 0; } }

        public Transform GetWaypoint(int index)
        {
            return _waypoints[index];
        }

        /// <summary>True while the agent is on the NavMesh and has a path to follow.</summary>
        public bool HasPath { get { return _agent != null && _agent.isOnNavMesh && _agent.hasPath; } }

        public Vector3 Destination { get { return _agent != null ? _agent.destination : transform.position; } }

        /// <summary>
        /// Copies the corners of the current path into the buffer and returns how many it wrote. Zero
        /// when there is no path. Reading the path allocates inside Unity, so only a debug view calls it.
        /// </summary>
        public int GetPathCorners(Vector3[] buffer)
        {
            return HasPath ? _agent.path.GetCornersNonAlloc(buffer) : 0;
        }

        private void Awake()
        {
            _brain = GetComponent<ManagedNpcBrain>();
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<ManagedHealth>();
            _shooter = GetComponent<ManagedNpcShooter>();
            _home = transform.position;
            _basePriority = _agent.avoidancePriority;
            _priority = _basePriority;
        }

        private void OnEnable()
        {
            _brain.ActionChanged += OnActionChanged;
            if (_health != null)
            {
                _health.Died += OnDied;
            }

            EnterMode(_brain.Action);
        }

        private void OnDisable()
        {
            _brain.ActionChanged -= OnActionChanged;
            if (_health != null)
            {
                _health.Died -= OnDied;
            }

            LeaveEngagement();
        }

        private void OnValidate()
        {
            _patrolSpeed = Mathf.Max(0f, _patrolSpeed);
            _chaseSpeed = Mathf.Max(0f, _chaseSpeed);
            _retreatSpeed = Mathf.Max(0f, _retreatSpeed);
            _followSpeed = Mathf.Max(0f, _followSpeed);
            _followDistance = Mathf.Max(0.5f, _followDistance);
            _wanderRadius = Mathf.Max(0f, _wanderRadius);
            _attackRange = Mathf.Max(0.1f, _attackRange);
            _attackCooldownSeconds = Mathf.Max(0f, _attackCooldownSeconds);
            _repathIntervalSeconds = Mathf.Max(0.05f, _repathIntervalSeconds);
            _hitLeewayMeters = Mathf.Max(0f, _hitLeewayMeters);
            _waitDistance = Mathf.Max(0f, _waitDistance);
            _ringSpacing = Mathf.Max(0.1f, _ringSpacing);
            _circleSpeed = Mathf.Max(0f, _circleSpeed);
            _blockedSeconds = Mathf.Max(ProgressSampleSeconds, _blockedSeconds);
        }

        private void OnActionChanged(ManagedNpcBrain brain, NpcAction previous, NpcAction next)
        {
            EnterMode(next);
        }

        private void OnDied(ManagedHealth health, GameObject source)
        {
            _dead = true;
            EnterMode(NpcAction.Idle);
            if (_removeAfterDeathSeconds > 0f)
            {
                Destroy(gameObject, _removeAfterDeathSeconds);
            }
        }

        private void EnterMode(NpcAction mode)
        {
            LeaveEngagement();
            _mode = _dead ? NpcAction.Idle : mode;
            _hasDestination = false;
            _nextRepathTime = 0f;
            _resumeTime = 0f;
            _configured = false;

            if (AgentReady())
            {
                Configure();
            }
        }

        // Speed and stopping distance for the current mode. Done as soon as the agent can take them,
        // which is later than EnterMode when the NPC is not on the NavMesh yet.
        private void Configure()
        {
            _configured = true;
            SetAutoRotation(true);
            switch (_mode)
            {
                case NpcAction.Patrol:
                    _agent.speed = _patrolSpeed;
                    _agent.stoppingDistance = 0.3f;
                    break;
                case NpcAction.Attack:
                    // The agent walks to a place on a ring, so it stops at that place and not at the melee range.
                    _agent.speed = _chaseSpeed;
                    _agent.stoppingDistance = SlotStoppingDistance;
                    break;
                case NpcAction.Shoot:
                    // Like Attack, the agent walks to a place on a ring, here the shooters' ring.
                    _agent.speed = _chaseSpeed;
                    _agent.stoppingDistance = SlotStoppingDistance;
                    break;
                case NpcAction.Kite:
                    _agent.speed = _retreatSpeed;
                    _agent.stoppingDistance = 0.3f;
                    break;
                case NpcAction.Retreat:
                    _agent.speed = _retreatSpeed;
                    _agent.stoppingDistance = 0.3f;
                    break;
                case NpcAction.Follow:
                    _agent.speed = _followSpeed;
                    _agent.stoppingDistance = _followDistance * 0.8f;
                    break;
                default:
                    Stop();
                    break;
            }
        }

        private void Update()
        {
            if (_dead || _mode == NpcAction.Idle || _mode == NpcAction.Trade || _mode == NpcAction.Hold || _mode == NpcAction.RefuseOrder || _mode == NpcAction.Mission || _mode == NpcAction.Betray)
            {
                if (!_stopped && AgentReady())
                {
                    Stop();
                }

                return;
            }

            if (!AgentReady())
            {
                return;
            }

            if (!_configured)
            {
                Configure();
            }

            float now = Time.time;
            switch (_mode)
            {
                case NpcAction.Patrol:
                    UpdatePatrol(now);
                    break;
                case NpcAction.Attack:
                    UpdateAttack(now);
                    break;
                case NpcAction.Shoot:
                    UpdateShoot(now);
                    break;
                case NpcAction.Kite:
                    UpdateKite(now);
                    break;
                case NpcAction.Retreat:
                    UpdateRetreat(now);
                    break;
                case NpcAction.Follow:
                    UpdateFollow(now);
                    break;
            }
        }

        private bool AgentReady()
        {
            if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
            {
                return true;
            }

            if (!_warnedNoNavMesh)
            {
                _warnedNoNavMesh = true;
                Debug.LogWarning("[GTG NPC] " + name + " is not on a NavMesh, so it cannot move. Bake a NavMesh for the scene and place the NPC on it.");
            }

            return false;
        }

        private void Stop()
        {
            _agent.isStopped = true;
            _stopped = true;
        }

        private void Go(Vector3 destination)
        {
            _agent.isStopped = false;
            _stopped = false;
            _agent.SetDestination(destination);
            _hasDestination = true;
        }

        private void UpdatePatrol(float now)
        {
            if (now < _resumeTime)
            {
                return;
            }

            bool arrived = _hasDestination && !_agent.pathPending
                && _agent.remainingDistance <= _agent.stoppingDistance + 0.2f;
            if (_hasDestination && !arrived)
            {
                return;
            }

            if (arrived)
            {
                _hasDestination = false;
                if (_pauseSeconds > 0f)
                {
                    Stop();
                    _resumeTime = now + _pauseSeconds;
                    return;
                }
            }

            Vector3 next;
            if (TryNextPatrolPoint(out next))
            {
                Go(next);
            }
            else if (!_stopped)
            {
                Stop();
            }
        }

        private bool TryNextPatrolPoint(out Vector3 point)
        {
            if (_waypoints != null && _waypoints.Length > 0)
            {
                for (int tries = 0; tries < _waypoints.Length; tries++)
                {
                    Transform waypoint = _waypoints[_nextWaypoint % _waypoints.Length];
                    _nextWaypoint = (_nextWaypoint + 1) % _waypoints.Length;
                    if (waypoint != null)
                    {
                        point = waypoint.position;
                        return true;
                    }
                }

                point = Vector3.zero;
                return false;
            }

            Vector2 offset = UnityEngine.Random.insideUnitCircle * _wanderRadius;
            return TrySample(_home + new Vector3(offset.x, 0f, offset.y), 4f, out point);
        }

        private void UpdateAttack(float now)
        {
            UpdateEngaged(now, false);
        }

        private void UpdateShoot(float now)
        {
            if (!HasShooter())
            {
                WarnNoShooter();
                if (!_stopped)
                {
                    Stop();
                }

                return;
            }

            UpdateEngaged(now, true);
        }

        // Attack and Shoot are the same fight on two rings. A melee attacker joins the target's engagement with the
        // melee ring and strikes when it holds a token and the target is in reach. A shooter joins with the ring at its
        // stand-off distance, among the other shooters, and fires from its place while the target is within the
        // weapon's range and in the line of fire. Everything else, the facing, the circling of a waiter and the
        // handling of a blocked place, is shared.
        private void UpdateEngaged(float now, bool ranged)
        {
            ManagedNpcThreatSource threat = _brain.Threat;
            if (threat == null || !threat.IsActive)
            {
                LeaveEngagement();

                // The target is gone. Walk to where it was last seen, then wait for the next decision.
                if (_hasDestination && (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance + 0.2f))
                {
                    return;
                }

                if (!_stopped)
                {
                    Stop();
                }

                return;
            }

            ManagedCombatEngagement engagement = threat.Engagement;
            if (_member == null || !ReferenceEquals(_engagement, engagement))
            {
                LeaveEngagement();
                _engagement = engagement;
                float ring = ranged ? _shooter.StandOffDistance : _attackRange * MeleeRingFraction;
                _member = engagement.Join(this, _brain, now, ring, _waitDistance, _ringSpacing, _circleSpeed, ranged);
                _hasDestination = false;
                _nextRepathTime = 0f;
            }

            engagement.Refresh(now);

            // An attacker that can move walks wherever its place is. One that cannot, a sentry with a bow or a shopkeeper
            // behind a stall, stays where it stands, and strikes or fires when the target is within its reach.
            bool mobile = _brain.CanMove;
            float reach = ranged ? _shooter.Range : _attackRange;

            // Where the threat was last seen, not where it is now, so hiding works.
            Vector3 targetPosition = _brain.LastKnownThreatPosition;
            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            // Close to the target the NPC keeps facing it. An agent turns to where it walks, so an NPC that circles or
            // flanks would turn its side to the target, leave its view cone, lose sight and drop out of the fight.
            bool close = distance <= _member.WaitRadius + EngagedMargin;
            SetAutoRotation(!close);
            if (close)
            {
                FaceTowards(toTarget);
            }

            SetPriority(_member.HasToken ? _basePriority - PriorityStep : _basePriority + PriorityStep);

            Vector3 slot = engagement.SlotPosition(_member);
            Vector3 toSlot = slot - transform.position;
            toSlot.y = 0f;
            float slotDistance = toSlot.magnitude;

            if (_member.HasToken && distance <= reach)
            {
                if (!_inRange)
                {
                    _inRange = true;
                    _inRangeSince = now;
                    _settleSeconds = ranged ? SettleSeconds + slotDistance / Mathf.Max(0.1f, _chaseSpeed) : SettleSeconds;
                }

                // Strike from the place the engagement gave. An NPC that cannot settle there within a moment strikes
                // from where it is, so a blocked place never leaves it standing idle in reach of the target.
                if (slotDistance <= SlotToleranceMeters || now - _inRangeSince >= _settleSeconds || !mobile)
                {
                    if (!_stopped)
                    {
                        Stop();
                    }

                    if (!ranged)
                    {
                        if (now >= _nextAttackTime)
                        {
                            _nextAttackTime = now + _attackCooldownSeconds;
                            Hit(threat);
                        }

                        return;
                    }

                    if (Shoot(threat, now))
                    {
                        return;
                    }

                    // Something stands in the line of fire. Ask for another place on the ring and go there, so the
                    // settling time starts again from the new place.
                    _inRange = false;
                    _member.Blocked = true;
                }
            }
            else
            {
                _inRange = false;
            }

            if (!mobile)
            {
                return;
            }

            // A waiter on its ring circles slowly. Everyone else closes in at the chase speed.
            bool onRing = !_member.HasToken && Mathf.Abs(distance - _member.Radius) <= RingBandMeters;
            float speed = onRing ? _circleSpeed : _chaseSpeed;
            if (_agent.speed != speed)
            {
                _agent.speed = speed;
            }

            bool needsToMove = slotDistance > StuckSlotDistanceMeters || (_member.HasToken && distance > reach);
            TrackProgress(now, needsToMove);

            if (!_hasDestination || now >= _nextRepathTime)
            {
                _nextRepathTime = now + _repathIntervalSeconds;
                Vector3 destination;
                if (TrySample(slot, SlotSampleMeters, out destination))
                {
                    Vector3 shift = destination - _lastGoal;
                    shift.y = 0f;
                    if (!_hasDestination || _stopped || shift.sqrMagnitude > GoalToleranceMeters * GoalToleranceMeters)
                    {
                        _lastGoal = destination;
                        Go(destination);
                    }
                }
                else
                {
                    // The place is off the NavMesh, for instance inside a wall. Ask for another one.
                    _member.Blocked = true;
                }
            }
        }

        // Looses a shot at where the threat was last seen, aimed at the body and not at the feet. False when something
        // stands in the line of fire. Being out of ammo or waiting on the cooldown is not a block.
        private bool Shoot(ManagedNpcThreatSource threat, float now)
        {
            Vector3 aim = _brain.LastKnownThreatPosition + (threat.AimPoint - threat.transform.position);
            return _shooter.TryFire(now, aim, threat.transform) != ManagedNpcFireResult.Blocked;
        }

        // A shooter that the threat has come too close to backs away to its stand-off distance and fires on the way. It
        // keeps facing the threat while it walks, so it stays in view. It takes no place in the engagement, since it is
        // not holding a position. Cornered, with no place to back into, it stands and fires.
        private void UpdateKite(float now)
        {
            if (!HasShooter())
            {
                WarnNoShooter();
                if (!_stopped)
                {
                    Stop();
                }

                return;
            }

            ManagedNpcThreatSource threat = _brain.Threat;
            if (threat == null || !threat.IsActive)
            {
                if (!_stopped)
                {
                    Stop();
                }

                return;
            }

            Vector3 targetPosition = _brain.LastKnownThreatPosition;
            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            SetAutoRotation(false);
            FaceTowards(toTarget);

            if (!_hasDestination || now >= _nextRepathTime)
            {
                _nextRepathTime = now + _repathIntervalSeconds;
                Vector3 away = -toTarget;
                if (away.sqrMagnitude < 0.0001f)
                {
                    away = -transform.forward;
                }

                Vector3 wanted = targetPosition + away.normalized * _shooter.StandOffDistance;
                wanted.y = transform.position.y;
                Vector3 destination;
                if (TrySample(wanted, 4f, out destination))
                {
                    Go(destination);
                }
                else if (!_stopped)
                {
                    Stop();
                }
            }

            if (distance <= _shooter.Range)
            {
                Shoot(threat, now);
            }
        }

        // Looked up again while it is missing, so a weapon added in code after Awake is still found.
        private bool HasShooter()
        {
            if (_shooter == null)
            {
                _shooter = GetComponent<ManagedNpcShooter>();
            }

            return _shooter != null;
        }

        private void WarnNoShooter()
        {
            if (_warnedNoShooter)
            {
                return;
            }

            _warnedNoShooter = true;
            Debug.LogWarning("[GTG NPC] " + name + " was told to shoot but has no ManagedNpcShooter, so it does nothing. Add the component, or set the attack style to Melee.");
        }

        // An attacker that is walking to its place and has not moved for _blockedSeconds is stuck, behind a neighbour or a
        // corner. It tells the engagement, which moves the place a little along the ring, and it plans a new path.
        private void TrackProgress(float now, bool needsToMove)
        {
            if (now < _progressCheckTime)
            {
                return;
            }

            _progressCheckTime = now + ProgressSampleSeconds;
            Vector3 moved = transform.position - _progressAnchor;
            moved.y = 0f;
            _progressAnchor = transform.position;

            bool trying = !_stopped && _agent.speed > 0.01f && needsToMove;
            if (!trying || moved.sqrMagnitude > StuckMovementMeters * StuckMovementMeters)
            {
                _stuckSamples = 0;
                return;
            }

            _stuckSamples++;
            if (_stuckSamples >= Mathf.RoundToInt(_blockedSeconds / ProgressSampleSeconds))
            {
                _stuckSamples = 0;
                _member.Blocked = true;
                _hasDestination = false;
            }
        }

        private void Hit(ManagedNpcThreatSource threat)
        {
            IManagedDamageable target = threat.Damageable;
            if (target == null || !target.IsAlive)
            {
                return;
            }

            // The attack aims at where the target was last seen, and the target may have stepped away since. The swing
            // misses when the target really is out of reach.
            Vector3 away = threat.transform.position - transform.position;
            away.y = 0f;
            if (away.magnitude > _attackRange + _hitLeewayMeters)
            {
                return;
            }

            target.TakeDamage(_attackDamage, gameObject);
            Action<ManagedNpcActor, IManagedDamageable> handler = Attacked;
            if (handler != null)
            {
                handler(this, target);
            }
        }

        // Gives up the place in the target's engagement and puts the agent back the way other modes expect it.
        private void LeaveEngagement()
        {
            if (_member != null)
            {
                _engagement.Leave(_member);
                _member = null;
                _engagement = null;
            }

            _inRange = false;
            _stuckSamples = 0;
            if (_agent != null)
            {
                SetAutoRotation(true);
                SetPriority(_basePriority);
            }
        }

        private void SetAutoRotation(bool on)
        {
            if (_autoRotation != on)
            {
                _autoRotation = on;
                _agent.updateRotation = on;
            }
        }

        private void SetPriority(int priority)
        {
            priority = Mathf.Clamp(priority, 0, 99);
            if (_priority != priority)
            {
                _priority = priority;
                _agent.avoidancePriority = priority;
            }
        }

        private void UpdateRetreat(float now)
        {
            if (_hasDestination && now < _nextRepathTime)
            {
                return;
            }

            _nextRepathTime = now + _repathIntervalSeconds * 4f;
            Vector3 away = transform.position - _brain.LastKnownThreatPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = -transform.forward;
            }

            Vector3 destination;
            if (TrySample(transform.position + away.normalized * _retreatDistance, 4f, out destination))
            {
                Go(destination);
            }
        }

        private void UpdateFollow(float now)
        {
            Transform leader = _brain.Leader;
            if (leader == null)
            {
                if (!_stopped)
                {
                    Stop();
                }

                return;
            }

            Vector3 toLeader = leader.position - transform.position;
            toLeader.y = 0f;
            float distance = toLeader.magnitude;

            // Stop inside the follow distance. Once stopped, wait until the leader is a meter farther, so a
            // follower does not shuffle back and forth at the edge.
            if (distance <= _followDistance || (_stopped && distance <= _followDistance + 1f))
            {
                if (!_stopped)
                {
                    Stop();
                }

                return;
            }

            if (!_hasDestination || _stopped || now >= _nextRepathTime)
            {
                _nextRepathTime = now + _repathIntervalSeconds;
                Vector3 destination;
                if (TrySample(leader.position, 3f, out destination))
                {
                    Go(destination);
                }
            }
        }

        private void FaceTowards(Vector3 flatDirection)
        {
            if (flatDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion look = Quaternion.LookRotation(flatDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _turnSpeedDegrees * Time.deltaTime);
        }

        private static bool TrySample(Vector3 wanted, float maxDistance, out Vector3 result)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(wanted, out hit, maxDistance, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = wanted;
            return false;
        }
    }
}
