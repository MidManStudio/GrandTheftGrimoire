#if GTG_ECS
// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterMovementSystem.cs"
// ============================================================================

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Movement step. Turns the motor velocity into a position change by collide and
    /// slide: sweep the capsule along the motion, stop a skin width before the surface it
    /// hits, then continue along that surface. Runs last of the character modules.
    /// </summary>
    [UpdateAfter(typeof(CharacterGravitySystem))]
    [BurstCompile]
    public partial struct CharacterMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MoveJob
            {
                CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.Schedule();
        }

        [BurstCompile]
        public partial struct MoveJob : IJobEntity
        {
            [ReadOnly] public CollisionWorld CollisionWorld;
            public float DeltaTime;

            public void Execute(
                Entity entity,
                ref LocalTransform transform,
                ref CharacterMotor motor,
                in PhysicsCollider collider,
                in CharacterSettings settings)
            {
                float3 position = transform.Position;
                float3 displacement = (motor.HorizontalVelocity + new float3(0f, motor.VerticalVelocity, 0f)) * DeltaTime;
                float skin = settings.SkinWidth;

                for (int i = 0; i < settings.MaxSlideIterations; i++)
                {
                    float distance = math.length(displacement);
                    if (distance < 1e-5f)
                    {
                        break;
                    }

                    float3 direction = displacement / distance;
                    if (!PhysicsRayUtility.CastColliderIgnoring(
                            CollisionWorld, collider, position, transform.Rotation,
                            direction, distance + skin, entity, out ColliderCastHit hit))
                    {
                        position += displacement;
                        break;
                    }

                    // Back off along the motion so the gap along the surface normal is the skin width.
                    float3 normal = hit.SurfaceNormal;
                    float backoff = skin / math.max(0.1f, -math.dot(direction, normal));
                    float travel = math.max(0f, hit.Fraction * (distance + skin) - backoff);
                    position += direction * travel;

                    float3 remaining = direction * (distance - travel);
                    displacement = remaining - normal * math.dot(remaining, normal);

                    if (normal.y > 0.5f && motor.VerticalVelocity < 0f)
                    {
                        motor.VerticalVelocity = 0f;
                    }
                    else if (normal.y < -0.5f && motor.VerticalVelocity > 0f)
                    {
                        motor.VerticalVelocity = 0f;
                    }
                }

                transform.Position = position;
            }
        }
    }
}
#endif
