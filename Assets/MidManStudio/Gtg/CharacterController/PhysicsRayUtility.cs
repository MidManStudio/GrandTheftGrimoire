// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "PhysicsRayUtility.cs"
// ============================================================================

using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>Collision world queries shared by the movement and spell systems.</summary>
    public static class PhysicsRayUtility
    {
        /// <summary>
        /// Casts a ray and returns the closest hit that does not belong to
        /// <paramref name="ignore"/>. Pass <see cref="Entity.Null"/> to ignore nothing.
        /// </summary>
        public static bool CastRayIgnoring(
            CollisionWorld world, RaycastInput input, Entity ignore, out RaycastHit closest)
        {
            closest = default;
            bool found = false;
            var hits = new NativeList<RaycastHit>(8, Allocator.Temp);

            if (world.CastRay(input, ref hits))
            {
                float best = float.MaxValue;
                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];
                    if (hit.Entity == ignore)
                    {
                        continue;
                    }

                    if (hit.Fraction < best)
                    {
                        best = hit.Fraction;
                        closest = hit;
                        found = true;
                    }
                }
            }

            hits.Dispose();
            return found;
        }
    }
}
