#if GTG_ECS
// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "PhysicsRayUtility.cs"
// ============================================================================

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>Collision world queries shared by the character, spell and projectile systems.</summary>
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

        /// <summary>
        /// Sweeps the character's own collider along <paramref name="direction"/> and returns
        /// the closest surface that blocks the motion. Hits on <paramref name="ignore"/> and
        /// surfaces the sweep is moving away from or along are skipped.
        /// </summary>
        public static bool CastColliderIgnoring(
            CollisionWorld world,
            PhysicsCollider collider,
            float3 start,
            quaternion rotation,
            float3 direction,
            float distance,
            Entity ignore,
            out ColliderCastHit closest)
        {
            closest = default;
            bool found = false;
            var input = new ColliderCastInput(collider.Value, start, start + direction * distance, rotation);
            var hits = new NativeList<ColliderCastHit>(8, Allocator.Temp);

            if (world.CastCollider(input, ref hits))
            {
                float best = float.MaxValue;
                for (int i = 0; i < hits.Length; i++)
                {
                    ColliderCastHit hit = hits[i];
                    if (hit.Entity == ignore)
                    {
                        continue;
                    }

                    if (math.dot(hit.SurfaceNormal, direction) >= -1e-4f)
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
#endif
