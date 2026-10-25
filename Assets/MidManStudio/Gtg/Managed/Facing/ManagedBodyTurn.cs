// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedBodyTurn.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Facing
{
    /// <summary>
    /// The math of where the body faces. Yaw is in degrees, measured from the world forward axis
    /// toward the world right axis, the same as the yaw of the character. Static and free of scene
    /// state, so it is checked without a scene.
    /// </summary>
    public static class ManagedBodyTurn
    {
        /// <summary>
        /// The world yaw of a local move direction, for a character that looks along
        /// <paramref name="aimYaw"/>. Forward gives the aim yaw, right gives 90 degrees more, and
        /// back gives 180 degrees more.
        /// </summary>
        public static float MoveYaw(float aimYaw, Vector2 move)
        {
            return aimYaw + Mathf.Atan2(move.x, move.y) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The yaw the body turns toward. It is the aim when <paramref name="faceAim"/> is set. Otherwise
        /// it is the move direction, and the current body yaw when the move is no longer than the dead zone,
        /// so a body at rest stays where it is.
        /// </summary>
        public static float Target(float bodyYaw, float aimYaw, Vector2 move, bool faceAim, float moveDeadZone)
        {
            if (faceAim)
            {
                return aimYaw;
            }

            if (move.magnitude <= moveDeadZone)
            {
                return bodyYaw;
            }

            return MoveYaw(aimYaw, move);
        }

        /// <summary>
        /// Turns the body yaw toward the target by at most the speed times the step, the short way
        /// round. A speed of zero or less, or a step of zero or less, leaves the yaw as it is.
        /// </summary>
        public static float Step(float bodyYaw, float targetYaw, float degreesPerSecond, float seconds)
        {
            if (!(degreesPerSecond > 0f) || !(seconds > 0f))
            {
                return bodyYaw;
            }

            return Mathf.MoveTowardsAngle(bodyYaw, targetYaw, degreesPerSecond * seconds);
        }
    }
}
