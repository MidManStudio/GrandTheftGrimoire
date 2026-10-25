// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedMeleeSwing.cs"
// ============================================================================

using System;

namespace MidManStudio.Gtg.Managed.Melee
{
    /// <summary>
    /// The clock of one swing. A swing starts, reaches its hit moment once, and ends. It holds plain
    /// values and knows nothing about Unity, so the timing is checked without a scene.
    /// </summary>
    public sealed class ManagedMeleeSwing
    {
        private float _elapsed;
        private float _hitAt;
        private float _length;
        private bool _hitDone;

        public bool IsSwinging { get; private set; }

        /// <summary>Seconds since the swing started, or 0 when none is under way.</summary>
        public float Elapsed { get { return IsSwinging ? _elapsed : 0f; } }

        /// <summary>How far through the swing it is, 0 through 1, or 0 when none is under way.</summary>
        public float Progress
        {
            get { return IsSwinging && _length > 0f ? Math.Min(1f, _elapsed / _length) : 0f; }
        }

        /// <summary>
        /// Starts a swing. Returns false, and changes nothing, while one is under way. A hit delay
        /// below zero counts as zero and a length below the hit delay counts as the hit delay.
        /// </summary>
        public bool Start(float hitDelaySeconds, float lengthSeconds)
        {
            if (IsSwinging)
            {
                return false;
            }

            _hitAt = Math.Max(0f, hitDelaySeconds);
            _length = Math.Max(_hitAt, lengthSeconds);
            _elapsed = 0f;
            _hitDone = false;
            IsSwinging = true;
            return true;
        }

        /// <summary>
        /// Moves the swing forward. Returns true on the one step that passes the hit moment, whatever
        /// the step size, and false on every other step. The swing ends on the step that passes its
        /// length, which can be the same step. A step of zero or less does nothing.
        /// </summary>
        public bool Tick(float seconds)
        {
            if (!IsSwinging || !(seconds > 0f))
            {
                return false;
            }

            _elapsed += seconds;
            bool hit = false;
            if (!_hitDone && _elapsed >= _hitAt)
            {
                _hitDone = true;
                hit = true;
            }

            if (_elapsed >= _length)
            {
                IsSwinging = false;
            }

            return hit;
        }

        /// <summary>Ends the swing without a hit. Does nothing when none is under way.</summary>
        public void Cancel()
        {
            IsSwinging = false;
            _elapsed = 0f;
            _hitDone = false;
        }
    }
}
