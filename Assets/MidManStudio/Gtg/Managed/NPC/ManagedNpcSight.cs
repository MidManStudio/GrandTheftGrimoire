// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedNpcSight.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>
    /// The two tests behind NPC sight. InView is cheap arithmetic and runs first. Only a
    /// point that passes it costs a physics ray.
    /// </summary>
    internal static class ManagedNpcSight
    {
        // How far past the aim point the ray reaches. A target point inside a collider is
        // found by the surface hit well before this, the margin only covers float error.
        private const float RayMargin = 0.05f;

        internal static float CosHalfFieldOfView(float fieldOfViewDegrees)
        {
            float clamped = Mathf.Clamp(fieldOfViewDegrees, 0f, 360f);
            return Mathf.Cos(clamped * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>
        /// True when the point is within range and inside the cone around forward. Forward
        /// must be normalized. A point at the eye counts as in view.
        /// </summary>
        internal static bool InView(Vector3 eye, Vector3 forward, Vector3 point, float range, float cosHalfFov)
        {
            Vector3 offset = point - eye;
            float sqrDistance = offset.sqrMagnitude;
            if (sqrDistance > range * range)
            {
                return false;
            }

            if (sqrDistance < 0.000001f)
            {
                return true;
            }

            float cosAngle = Vector3.Dot(forward, offset) / Mathf.Sqrt(sqrDistance);
            return cosAngle >= cosHalfFov;
        }

        /// <summary>
        /// True when the first thing a ray from the eye meets is the target, or nothing at
        /// all. Triggers are ignored. A ray that starts inside a collider does not hit it, so
        /// the NPC's own capsule does not block its own sight.
        /// </summary>
        internal static bool HasLineOfSight(Vector3 eye, Vector3 point, Transform target, int mask)
        {
            Vector3 offset = point - eye;
            float distance = offset.magnitude;
            if (distance < 0.0001f)
            {
                return true;
            }

            RaycastHit hit;
            if (Physics.Raycast(eye, offset / distance, out hit, distance + RayMargin, mask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.transform.IsChildOf(target);
            }

            return true;
        }
    }
}
