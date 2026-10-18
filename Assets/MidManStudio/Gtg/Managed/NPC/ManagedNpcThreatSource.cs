// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedNpcThreatSource.cs"
// ============================================================================

using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Health;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>
    /// Marks an object that NPCs can see as a threat. Put it on the player. An NPC sees the
    /// source when one of its visible points is inside the view cone and a ray from the eye
    /// reaches it. Every registered source is a threat to every NPC, factions are not modelled yet.
    /// It is also the target of a fight: it holds the ManagedCombatEngagement that spaces its attackers
    /// around it, and the settings that say how many may strike at once, melee attackers and shooters each
    /// with a limit of their own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedNpcThreatSource : MonoBehaviour
    {
        private static readonly List<ManagedNpcThreatSource> Sources = new List<ManagedNpcThreatSource>();

        [Tooltip("Points on the body that count as visible, in local space. The default is head and chest of a 2 m character with the pivot at the feet. Seeing any one of them is enough, so a half hidden target is still seen.")]
        [SerializeField] private Vector3[] _visiblePoints =
        {
            new Vector3(0f, 1.6f, 0f),
            new Vector3(0f, 0.9f, 0f),
        };

        [Tooltip("Optional. A dead source is not seen. Left empty, the component on this object is used.")]
        [SerializeField] private ManagedHealth _health;

        [Header("Engagement")]
        [Tooltip("Most NPCs that may strike this target in melee at the same time. The rest circle at a distance and take a turn when a holder has kept its place for the rotation time. Zero lifts the limit and every attacker closes in.")]
        [SerializeField] private int _maxMeleeAttackers = 3;
        [Tooltip("Seconds an attacker keeps its place while another NPC is waiting for it. Zero means a holder keeps it until it stops attacking.")]
        [SerializeField] private float _attackerRotationSeconds = 5f;
        [Tooltip("Most shooters that may fire at this target at the same time. A separate pool from the melee limit, so archers never keep the swordsmen from the target. The rest wait further out and take a turn when a holder has kept its place for the rotation time. Zero lifts the limit.")]
        [SerializeField] private int _maxRangedAttackers = 4;
        [Tooltip("Seconds a shooter keeps its place while another shooter waits. Zero means a holder keeps it until it stops attacking.")]
        [SerializeField] private float _rangedRotationSeconds = 5f;

        private IManagedDamageable _damageable;
        private ManagedCombatEngagement _engagement;

        public static int Count { get { return Sources.Count; } }

        public static ManagedNpcThreatSource Get(int index)
        {
            return Sources[index];
        }

        /// <summary>
        /// What an NPC melee attack hurts. Found on this object or a parent, null when the
        /// source cannot take damage.
        /// </summary>
        public IManagedDamageable Damageable { get { return _damageable; } }

        /// <summary>Most NPCs that may strike this target in melee at once. Zero means no limit.</summary>
        public int MaxMeleeAttackers { get { return _maxMeleeAttackers; } set { _maxMeleeAttackers = Mathf.Max(0, value); } }

        /// <summary>Seconds a holder keeps its place while another attacker waits. Zero means it never rotates out.</summary>
        public float AttackerRotationSeconds { get { return _attackerRotationSeconds; } set { _attackerRotationSeconds = Mathf.Max(0f, value); } }

        /// <summary>Most shooters that may fire at this target at once. Zero means no limit.</summary>
        public int MaxRangedAttackers { get { return _maxRangedAttackers; } set { _maxRangedAttackers = Mathf.Max(0, value); } }

        /// <summary>Seconds a shooter keeps its place while another shooter waits. Zero means it never rotates out.</summary>
        public float RangedRotationSeconds { get { return _rangedRotationSeconds; } set { _rangedRotationSeconds = Mathf.Max(0f, value); } }

        /// <summary>NPCs attacking this target right now, melee and shooters together. For debug tools.</summary>
        public int AttackerCount { get { return _engagement != null ? _engagement.MemberCount : 0; } }

        /// <summary>Melee attackers that hold a place to strike from. Shooters are counted by <see cref="ShootingCount"/>. For debug tools.</summary>
        public int StrikingCount { get { return _engagement != null ? _engagement.TokenHolderCount : 0; } }

        /// <summary>Shooters attacking this target, with or without a place to fire from. For debug tools.</summary>
        public int ShooterCount { get { return _engagement != null ? _engagement.ShooterCount : 0; } }

        /// <summary>Shooters that hold a place to fire from. For debug tools.</summary>
        public int ShootingCount { get { return _engagement != null ? _engagement.ShooterTokenHolderCount : 0; } }

        // Made on first use and kept, so a fight starting costs no allocation after the first.
        internal ManagedCombatEngagement Engagement
        {
            get
            {
                if (_engagement == null)
                {
                    _engagement = new ManagedCombatEngagement(this);
                }

                return _engagement;
            }
        }

        public int PointCount { get { return _visiblePoints != null ? _visiblePoints.Length : 0; } }

        public bool IsActive
        {
            get { return isActiveAndEnabled && (_health == null || _health.IsAlive); }
        }

        public Vector3 GetPoint(int index)
        {
            return transform.TransformPoint(_visiblePoints[index]);
        }

        /// <summary>
        /// Where a shooter aims: the middle of the visible points, which is the body and not the feet. The transform
        /// position when there are no points.
        /// </summary>
        public Vector3 AimPoint
        {
            get
            {
                int count = PointCount;
                if (count == 0)
                {
                    return transform.position;
                }

                Vector3 sum = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    sum = sum + GetPoint(i);
                }

                return sum / count;
            }
        }

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Sources.Clear();
        }

        private void Awake()
        {
            if (_health == null)
            {
                _health = GetComponent<ManagedHealth>();
            }

            _damageable = GetComponentInParent<IManagedDamageable>();
        }

        private void OnValidate()
        {
            _maxMeleeAttackers = Mathf.Max(0, _maxMeleeAttackers);
            _attackerRotationSeconds = Mathf.Max(0f, _attackerRotationSeconds);
            _maxRangedAttackers = Mathf.Max(0, _maxRangedAttackers);
            _rangedRotationSeconds = Mathf.Max(0f, _rangedRotationSeconds);
        }

        private void OnEnable()
        {
            Sources.Add(this);
        }

        private void OnDisable()
        {
            Sources.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            if (_visiblePoints == null)
            {
                return;
            }

            Gizmos.color = Color.red;
            for (int i = 0; i < _visiblePoints.Length; i++)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(_visiblePoints[i]), 0.1f);
            }
        }
    }
}
