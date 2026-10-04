// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHealth.cs"
// ============================================================================

using System;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Health
{
    /// <summary>
    /// Hit points as a component of its own. Put it on the player, an NPC or anything
    /// else that can be hurt. Systems read Fraction and IsAlive and subscribe to the
    /// events, they never write the value directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedHealth : MonoBehaviour, IManagedDamageable
    {
        [SerializeField] private float _maxHealth = 100f;
        [Tooltip("Damage is ignored while this is on.")]
        [SerializeField] private bool _invulnerable;

        private float _current;

        /// <summary>(this, amount actually removed, source). Raised before Died.</summary>
        public event Action<ManagedHealth, float, GameObject> Damaged;

        /// <summary>(this, source of the killing hit). Raised once per death.</summary>
        public event Action<ManagedHealth, GameObject> Died;

        public float Max { get { return _maxHealth; } }
        public float Current { get { return _current; } }
        public bool IsAlive { get { return _current > 0f; } }

        /// <summary>Current health from 0 to 1, the value the NPC observation carries.</summary>
        public float Fraction
        {
            get { return _maxHealth > 0f ? Mathf.Clamp01(_current / _maxHealth) : 0f; }
        }

        public bool Invulnerable
        {
            get { return _invulnerable; }
            set { _invulnerable = value; }
        }

        private void Awake()
        {
            _current = _maxHealth;
        }

        private void OnValidate()
        {
            _maxHealth = Mathf.Max(0.01f, _maxHealth);
        }

        public void TakeDamage(float amount, GameObject source = null)
        {
            // The negated comparison also rejects NaN.
            if (!IsAlive || _invulnerable || !(amount > 0f))
            {
                return;
            }

            float removed = Mathf.Min(amount, _current);
            _current -= removed;

            Action<ManagedHealth, float, GameObject> damaged = Damaged;
            if (damaged != null)
            {
                damaged(this, removed, source);
            }

            if (_current <= 0f)
            {
                _current = 0f;
                Action<ManagedHealth, GameObject> died = Died;
                if (died != null)
                {
                    died(this, source);
                }
            }
        }

        /// <summary>Adds health up to the maximum. Does nothing once dead, see ResetToFull.</summary>
        public void Heal(float amount)
        {
            if (!IsAlive || !(amount > 0f))
            {
                return;
            }

            _current = Mathf.Min(_maxHealth, _current + amount);
        }

        /// <summary>Restores full health, also from the dead state. Used for a respawn.</summary>
        public void ResetToFull()
        {
            _current = _maxHealth;
        }
    }
}
