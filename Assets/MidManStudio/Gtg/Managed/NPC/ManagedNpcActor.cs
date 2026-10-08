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
    /// chases the threat and hits it in melee, Retreat runs away from it, Follow walks to the brain's
    /// Leader and stays near, and Idle, Trade, Hold, RefuseOrder, Mission and Betray stand still. A mission and a
    /// betrayal are carried out by game code that listens to the brain.
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
        private bool _dead;

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
            _home = transform.position;
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
            switch (_mode)
            {
                case NpcAction.Patrol:
                    _agent.speed = _patrolSpeed;
                    _agent.stoppingDistance = 0.3f;
                    break;
                case NpcAction.Attack:
                    _agent.speed = _chaseSpeed;
                    _agent.stoppingDistance = _attackRange * 0.8f;
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
            ManagedNpcThreatSource threat = _brain.Threat;
            if (threat == null)
            {
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

            // Where the threat was last seen, not where it is now, so hiding works.
            Vector3 targetPosition = _brain.LastKnownThreatPosition;
            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (distance <= _attackRange)
            {
                if (!_stopped)
                {
                    Stop();
                }

                FaceTowards(toTarget);
                if (now >= _nextAttackTime)
                {
                    _nextAttackTime = now + _attackCooldownSeconds;
                    Hit(threat);
                }

                return;
            }

            if (!_hasDestination || now >= _nextRepathTime)
            {
                _nextRepathTime = now + _repathIntervalSeconds;
                Vector3 destination;
                if (TrySample(targetPosition, 2f, out destination))
                {
                    Go(destination);
                }
            }
        }

        private void Hit(ManagedNpcThreatSource threat)
        {
            IManagedDamageable target = threat.Damageable;
            if (target == null || !target.IsAlive)
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
