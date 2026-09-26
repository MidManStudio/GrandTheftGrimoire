// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterMovementSystem.cs"
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Phase 0 kinematic movement: ground check via a downward raycast
    /// against the Unity Physics collision world, then gravity/jump on the
    /// vertical axis and a direct position move on the horizontal plane.
    ///
    /// Movement is world-axis-relative for now, not camera-relative — that's
    /// a presentation-layer decision that depends on the Cinemachine rig,
    /// which doesn't exist yet at this phase.
    ///
    /// Runs after <see cref="CharacterInputSystem"/> so it always sees this
    /// frame's input, not last frame's.
    /// </summary>
    [UpdateAfter(typeof(CharacterInputSystem))]
    [BurstCompile]
    public partial struct CharacterMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld.CollisionWorld;

            foreach (var (transform, verticalVelocity, ground, input, settings) in
                     SystemAPI.Query<
                         RefRW<LocalTransform>,
                         RefRW<CharacterVerticalVelocity>,
                         RefRW<CharacterGroundState>,
                         RefRO<CharacterInput>,
                         RefRO<CharacterMoveSettings>>()
                         .WithAll<CharacterTag>())
            {
                float3 position = transform.ValueRO.Position;

                var rayInput = new RaycastInput
                {
                    Start = position,
                    End = position + new float3(0f, -settings.ValueRO.GroundCheckDistance, 0f),
                    Filter = CollisionFilter.Default,
                };
                bool isGrounded = collisionWorld.CastRay(rayInput, out _);
                ground.ValueRW.IsGrounded = isGrounded;

                float vSpeed = verticalVelocity.ValueRO.Value;
                if (isGrounded)
                {
                    vSpeed = input.ValueRO.JumpPressed ? settings.ValueRO.JumpSpeed : 0f;
                }
                else
                {
                    vSpeed += settings.ValueRO.Gravity * deltaTime;
                }
                verticalVelocity.ValueRW.Value = vSpeed;

                float3 horizontal = new float3(input.ValueRO.Move.x, 0f, input.ValueRO.Move.y) * settings.ValueRO.MoveSpeed;
                float3 displacement = (horizontal + new float3(0f, vSpeed, 0f)) * deltaTime;

                transform.ValueRW.Position = position + displacement;
            }
        }
    }
}
