// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterGroundSystem.cs"
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
    /// Ground module. Sweeps the character's capsule a short way down and records whether
    /// it stands on walkable ground. Ground is ignored while the character is rising, or
    /// the sweep would still see the floor after takeoff and cancel the jump.
    /// </summary>
    [UpdateAfter(typeof(CharacterLookSystem))]
    [BurstCompile]
    public partial struct CharacterGroundSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new GroundJob
            {
                CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.Schedule();
        }

        [BurstCompile]
        public partial struct GroundJob : IJobEntity
        {
            [ReadOnly] public CollisionWorld CollisionWorld;
            public float DeltaTime;

            public void Execute(
                Entity entity,
                ref CharacterMotor motor,
                in LocalTransform transform,
                in PhysicsCollider collider,
                in CharacterSettings settings)
            {
                bool grounded = false;
                float3 normal = new float3(0f, 1f, 0f);

                if (motor.VerticalVelocity <= 0.01f
                    && PhysicsRayUtility.CastColliderIgnoring(
                        CollisionWorld,
                        collider,
                        transform.Position,
                        transform.Rotation,
                        new float3(0f, -1f, 0f),
                        settings.GroundProbeDistance,
                        entity,
                        out ColliderCastHit hit)
                    && hit.SurfaceNormal.y >= settings.MaxSlopeDot)
                {
                    grounded = true;
                    normal = hit.SurfaceNormal;
                }

                motor.IsGrounded = grounded;
                motor.GroundNormal = normal;
                motor.TimeSinceGrounded = grounded ? 0f : motor.TimeSinceGrounded + DeltaTime;
            }
        }
    }
}
