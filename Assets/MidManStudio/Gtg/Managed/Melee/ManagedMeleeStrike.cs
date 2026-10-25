// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedMeleeStrike.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Health;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Melee
{
    /// <summary>
    /// The hit check of one swing. It looks at everything within reach of the chest, keeps the
    /// targets whose nearest point lies inside the cone, and hurts each of them once. The cone is
    /// flat on the ground, so aiming up or down does not change it, and reach is measured in 3D.
    /// </summary>
    public static class ManagedMeleeStrike
    {
        private const int MaxOverlaps = 32;

        private static readonly Collider[] Overlaps = new Collider[MaxOverlaps];
        private static readonly IManagedDamageable[] Targets = new IManagedDamageable[MaxOverlaps];

        /// <summary>
        /// Hurts every IManagedDamageable inside the cone once, with the damage of the weapon. A body
        /// with several colliders is hurt once, and anything under <paramref name="source"/> is
        /// skipped. Returns how many targets were hurt. A weapon with no damage or no reach hurts
        /// nothing.
        /// </summary>
        public static int Strike(Vector3 origin, Vector3 forward, ManagedMeleeWeapon weapon, GameObject source, LayerMask mask)
        {
            if (weapon == null || !(weapon.Damage > 0f) || !(weapon.Reach > 0f))
            {
                return 0;
            }

            int found = Physics.OverlapSphereNonAlloc(origin, weapon.Reach, Overlaps, mask, QueryTriggerInteraction.Ignore);
            if (found > MaxOverlaps)
            {
                found = MaxOverlaps;
            }

            int targetCount = 0;
            for (int i = 0; i < found; i++)
            {
                Collider collider = Overlaps[i];
                Overlaps[i] = null;
                if (collider == null)
                {
                    continue;
                }

                IManagedDamageable target = collider.GetComponentInParent<IManagedDamageable>();
                if (target == null || IsSource(target, source))
                {
                    continue;
                }

                if (!InCone(origin, forward, collider.bounds.ClosestPoint(origin), weapon.ArcDegrees))
                {
                    continue;
                }

                bool seen = false;
                for (int t = 0; t < targetCount; t++)
                {
                    if (ReferenceEquals(Targets[t], target))
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                {
                    Targets[targetCount++] = target;
                }
            }

            int hurt = 0;
            for (int t = 0; t < targetCount; t++)
            {
                IManagedDamageable target = Targets[t];
                Targets[t] = null;
                if (target.IsAlive)
                {
                    target.TakeDamage(weapon.Damage, source);
                    hurt++;
                }
            }

            return hurt;
        }

        /// <summary>
        /// True when the point lies inside the cone that opens from the origin along the forward
        /// direction, measured on the ground plane. A point straight above or below the origin counts
        /// as inside, a forward direction with no horizontal part has no cone, and 360 degrees covers
        /// everything.
        /// </summary>
        public static bool InCone(Vector3 origin, Vector3 forward, Vector3 point, float arcDegrees)
        {
            Vector3 flat = new Vector3(point.x - origin.x, 0f, point.z - origin.z);
            if (flat.sqrMagnitude < 1e-6f)
            {
                return true;
            }

            if (arcDegrees >= 360f)
            {
                return true;
            }

            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
            if (flatForward.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            return Vector3.Angle(flatForward, flat) <= arcDegrees * 0.5f;
        }

        private static bool IsSource(IManagedDamageable target, GameObject source)
        {
            Component component = target as Component;
            return source != null && component != null && component.transform.IsChildOf(source.transform);
        }
    }
}
