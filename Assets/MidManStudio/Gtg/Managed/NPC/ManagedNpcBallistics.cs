// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedNpcBallistics.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>
    /// The aiming maths of a shot that gravity bends. An arrow or a bolt leaves at a fixed speed and falls on the way,
    /// so pointing it straight at the target would drop it short. This works out the direction that makes the path
    /// pass through the target. Pure arithmetic, no scene access.
    /// </summary>
    internal static class ManagedNpcBallistics
    {
        // Below this horizontal distance there is no useful angle to solve for.
        private const float MinHorizontalMeters = 0.01f;

        /// <summary>
        /// The direction to launch from <paramref name="from"/> so that a shot of the given speed passes through
        /// <paramref name="to"/>. <paramref name="gravity"/> is how fast the shot accelerates downward in m/s squared
        /// (zero or less flies straight). Takes the flat of the two paths that reach the point, so a shot does not
        /// lob when it can fly level. Returns false when the target is out of reach at that speed, or straight above
        /// or below, and then <paramref name="direction"/> is the straight line to the target and the caller decides
        /// whether to fire it.
        /// </summary>
        internal static bool TryLaunchDirection(Vector3 from, Vector3 to, float speed, float gravity, out Vector3 direction)
        {
            Vector3 line = to - from;
            float length = line.magnitude;
            direction = length > 1e-6f ? line / length : new Vector3(0f, 0f, 1f);
            if (length <= 1e-6f || speed <= 0f)
            {
                return false;
            }

            if (!(gravity > 0f))
            {
                return true;
            }

            float dx = line.x;
            float dz = line.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < MinHorizontalMeters)
            {
                return false;
            }

            float h = line.y;
            float v2 = speed * speed;
            float discriminant = v2 * v2 - gravity * (gravity * d * d + 2f * h * v2);
            if (discriminant < 0f)
            {
                return false;
            }

            float tangent = (v2 - Mathf.Sqrt(discriminant)) / (gravity * d);
            float cos = 1f / Mathf.Sqrt(1f + tangent * tangent);
            float sin = tangent * cos;
            direction = new Vector3(dx / d * cos, sin, dz / d * cos);
            return true;
        }

        /// <summary>
        /// Bends a direction by up to <paramref name="spreadDegrees"/> in a random direction around it. A zero spread
        /// returns the direction unchanged and draws no random numbers. <paramref name="unitX"/> and
        /// <paramref name="unitY"/> are a point in the unit disc, one disc point per shot, so the offset is uniform
        /// over the cone.
        /// </summary>
        internal static Vector3 Spread(Vector3 direction, float spreadDegrees, float unitX, float unitY)
        {
            if (!(spreadDegrees > 0f))
            {
                return direction;
            }

            Vector3 forward = direction.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 1e-6f)
            {
                right = Vector3.Cross(Vector3.right, forward);
            }

            right = right.normalized;
            Vector3 up = Vector3.Cross(forward, right);
            float reach = Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
            return (forward + right * (unitX * reach) + up * (unitY * reach)).normalized;
        }
    }
}
