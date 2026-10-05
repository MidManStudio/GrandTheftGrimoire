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

        private IManagedDamageable _damageable;

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

        public int PointCount { get { return _visiblePoints != null ? _visiblePoints.Length : 0; } }

        public bool IsActive
        {
            get { return isActiveAndEnabled && (_health == null || _health.IsAlive); }
        }

        public Vector3 GetPoint(int index)
        {
            return transform.TransformPoint(_visiblePoints[index]);
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
