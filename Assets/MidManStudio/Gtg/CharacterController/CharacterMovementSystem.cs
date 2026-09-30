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
    /// Phase 0 kinematic movement. Ground check is a downward ray from just
    /// above the feet, movement is relative to the current yaw, and the entity
    /// pivot is the feet. Runs after <see cref="CharacterLookSystem"/>.
    /// </summary>
    [UpdateAfter(typeof(CharacterLookSystem))]
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
            CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            float3 up = new float3(0f, 1f, 0f);

            foreach (var (transform, verticalVelocity, ground, look, input, settings, entity) in
                     SystemAPI.Query<
                         RefRW<LocalTransform>,
                         RefRW<CharacterVerticalVelocity>,
                         RefRW<CharacterGroundState>,
                         RefRO<CharacterLook>,
                         RefRO<CharacterInput>,
                         RefRO<CharacterMoveSettings>>()
                         .WithAll<CharacterTag>()
                         .WithEntityAccess())
            {
                CharacterMoveSettings cfg = settings.ValueRO;
                CharacterInput inp = input.ValueRO;
                float3 position = transform.ValueRO.Position;
                float vSpeed = verticalVelocity.ValueRO.Value;

                // Ground is ignored while rising, otherwise the ray still sees the
                // floor for a few frames after takeoff and cancels the jump.
                bool grounded = false;
                float groundY = 0f;
                if (vSpeed <= 0f)
                {
                    // Reach far enough to cover this frame's fall so a fast fall
                    // cannot step through thin ground.
                    float fallStep = math.max(0f, -(vSpeed + cfg.Gravity * deltaTime) * deltaTime);
                    var rayInput = new RaycastInput
                    {
                        Start = position + up * cfg.GroundSkin,
                        End = position - up * (cfg.GroundCheckDistance + fallStep),
                        Filter = CollisionFilter.Default,
                    };

                    if (PhysicsRayUtility.CastRayIgnoring(collisionWorld, rayInput, entity, out RaycastHit hit))
                    {
                        grounded = true;
                        groundY = hit.Position.y;
                    }
                }

                bool jumped = false;
                if (grounded && inp.JumpPressed)
                {
                    vSpeed = cfg.JumpSpeed;
                    jumped = true;
                }
                else if (grounded)
                {
                    vSpeed = 0f;
                }
                else
                {
                    vSpeed += cfg.Gravity * deltaTime;
                }

                quaternion yawRotation = quaternion.RotateY(math.radians(look.ValueRO.Yaw));
                float3 horizontal = math.mul(yawRotation, new float3(inp.Move.x, 0f, inp.Move.y)) * cfg.MoveSpeed;
                float3 newPosition = position + (horizontal + up * vSpeed) * deltaTime;

                if (grounded && !jumped)
                {
                    newPosition.y = groundY;
                }

                verticalVelocity.ValueRW.Value = vSpeed;
                ground.ValueRW.IsGrounded = grounded && !jumped;
                transform.ValueRW.Position = newPosition;
            }
        }
    }
}
