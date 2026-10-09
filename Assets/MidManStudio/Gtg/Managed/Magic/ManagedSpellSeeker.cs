// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSpellSeeker.cs"
// ============================================================================

using System;
using MidManStudio.Gtg.Managed.Health;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>
    /// Decides what a steered spell flies toward and remembers it for the life of the shot.
    /// A lock is either a living target found in a cone around the aim, or, when the cone is
    /// empty, the point the player aimed at. A shot keeps only the id of its lock, so flight
    /// stays free of references, and asks <see cref="TryGetPoint"/> where to turn each step.
    /// </summary>
    public sealed class ManagedSpellSeeker
    {
        private const int ScanBufferSize = 32;

        // A target closer than this is already in the caster's face, so the cone skips it.
        private const float MinTargetDistance = 0.75f;

        private struct Slot
        {
            public bool InUse;
            public Collider Collider;
            public IManagedDamageable Target;
            public Vector3 Point;

            // A point the shot has flown past. Once set it stays set, so a shot that missed
            // an open-air point flies on and does not loop back for it.
            public bool Passed;
        }

        private readonly Collider[] _scan = new Collider[ScanBufferSize];
        private Slot[] _slots;
        private int[] _free;
        private int _freeCount;
        private int _active;

        public ManagedSpellSeeker(int capacity)
        {
            int size = Math.Max(1, capacity);
            _slots = new Slot[size];
            _free = new int[size];
            for (int i = 0; i < size; i++)
            {
                _free[i] = size - 1 - i;
            }

            _freeCount = size;
        }

        /// <summary>Locks that are in use right now.</summary>
        public int ActiveCount { get { return _active; } }

        /// <summary>
        /// Takes a lock and returns its id, which is never 0. The cone is centered on
        /// <paramref name="aimDirection"/> from <paramref name="origin"/>. Among the living
        /// damageable targets inside it that the caster can see, the one nearest the aim line
        /// wins. With none, the lock is the fallback point.
        /// </summary>
        public int Lock(
            Vector3 origin, Vector3 aimDirection, Vector3 fallbackPoint,
            float range, float coneHalfAngleDegrees, int mask, Transform ignoreRoot)
        {
            Collider bestCollider = null;
            IManagedDamageable bestTarget = null;
            float bestAngle = float.MaxValue;
            float bestDistance = float.MaxValue;

            int found = Physics.OverlapSphereNonAlloc(origin, range, _scan, mask, QueryTriggerInteraction.Ignore);
            if (found > ScanBufferSize)
            {
                found = ScanBufferSize;
            }

            for (int i = 0; i < found; i++)
            {
                Collider collider = _scan[i];
                _scan[i] = null;
                if (collider == null || (ignoreRoot != null && collider.transform.IsChildOf(ignoreRoot)))
                {
                    continue;
                }

                IManagedDamageable target = collider.GetComponentInParent<IManagedDamageable>();
                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                Vector3 center = collider.bounds.center;
                Vector3 toCenter = center - origin;
                float distance = toCenter.magnitude;
                if (distance < MinTargetDistance || distance > range)
                {
                    continue;
                }

                float angle = Vector3.Angle(aimDirection, toCenter);
                if (angle > coneHalfAngleDegrees)
                {
                    continue;
                }

                bool better = angle < bestAngle || (angle == bestAngle && distance < bestDistance);
                if (!better || !CanSee(origin, center, target, mask, ignoreRoot))
                {
                    continue;
                }

                bestCollider = collider;
                bestTarget = target;
                bestAngle = angle;
                bestDistance = distance;
            }

            int id = Take();
            _slots[id - 1] = new Slot
            {
                InUse = true,
                Collider = bestCollider,
                Target = bestTarget,
                Point = bestCollider != null ? bestCollider.bounds.center : fallbackPoint,
            };

            return id;
        }

        /// <summary>
        /// The point a shot should turn toward this step. A living target is followed, and a
        /// shot that passes it turns back for it. A target that died or was destroyed leaves
        /// its last point behind, and so does a lock that never had a target. The shot turns
        /// toward such a point only until it has flown past it, judged from its position and
        /// heading. Returns false when there is nothing left to turn toward: an id that is
        /// not in use, or a point already passed.
        /// </summary>
        public bool TryGetPoint(int id, Vector3 position, Vector3 heading, out Vector3 point)
        {
            point = Vector3.zero;
            int index = id - 1;
            if (index < 0 || index >= _slots.Length || !_slots[index].InUse)
            {
                return false;
            }

            Slot slot = _slots[index];
            if (slot.Collider != null && slot.Target != null && slot.Target.IsAlive)
            {
                slot.Point = slot.Collider.bounds.center;
            }
            else
            {
                slot.Collider = null;
                slot.Target = null;
                if (!slot.Passed && Vector3.Dot(heading, slot.Point - position) < 0f)
                {
                    slot.Passed = true;
                }
            }

            _slots[index] = slot;
            if (slot.Passed)
            {
                return false;
            }

            point = slot.Point;
            return true;
        }

        /// <summary>True while the lock still follows a living target.</summary>
        public bool HasTarget(int id)
        {
            int index = id - 1;
            return index >= 0 && index < _slots.Length && _slots[index].InUse && _slots[index].Collider != null;
        }

        /// <summary>A short name for logs: the target's collider name, or the aim point.</summary>
        public string Describe(int id)
        {
            int index = id - 1;
            if (index < 0 || index >= _slots.Length || !_slots[index].InUse)
            {
                return "nothing";
            }

            return _slots[index].Collider != null ? "'" + _slots[index].Collider.name + "'" : "the aim point";
        }

        public void Release(int id)
        {
            int index = id - 1;
            if (index < 0 || index >= _slots.Length || !_slots[index].InUse)
            {
                return;
            }

            _slots[index] = default(Slot);
            _free[_freeCount++] = index;
            _active--;
        }

        private int Take()
        {
            if (_freeCount == 0)
            {
                Grow();
            }

            _active++;
            return _free[--_freeCount] + 1;
        }

        private void Grow()
        {
            int oldSize = _slots.Length;
            int size = oldSize * 2;
            Array.Resize(ref _slots, size);
            Array.Resize(ref _free, size);
            for (int i = size - 1; i >= oldSize; i--)
            {
                _free[_freeCount++] = i;
            }
        }

        // The first thing the line meets must be the target. The caster's own body is let
        // through, because the shot point sits near it.
        private static bool CanSee(Vector3 origin, Vector3 center, IManagedDamageable target, int mask, Transform ignoreRoot)
        {
            RaycastHit hit;
            if (!Physics.Linecast(origin, center, out hit, mask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot))
            {
                return true;
            }

            return ReferenceEquals(hit.collider.GetComponentInParent<IManagedDamageable>(), target);
        }
    }
}
