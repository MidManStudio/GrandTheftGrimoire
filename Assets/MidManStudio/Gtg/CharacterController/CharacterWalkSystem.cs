// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterWalkSystem.cs"
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Walk module. Turns the move input into a horizontal velocity relative to the yaw,
    /// with acceleration and reduced air control. With the module off the target is zero,
    /// so the character glides to a stop.
    /// </summary>
    [UpdateAfter(typeof(CharacterGroundSystem))]
    [BurstCompile]
    public partial struct CharacterWalkSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new WalkJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct WalkJob : IJobEntity
        {
            public float DeltaTime;

            public void Execute(
                ref CharacterMotor motor,
                in CharacterInput input,
                in CharacterLook look,
                in CharacterSettings settings,
                in CharacterFeatures features)
            {
                float3 target = float3.zero;
                if (features.Has(CharacterFeature.Move))
                {
                    quaternion yaw = quaternion.RotateY(math.radians(look.Yaw));
                    target = math.mul(yaw, new float3(input.Move.x, 0f, input.Move.y)) * settings.MoveSpeed;
                }

                float acceleration = settings.Acceleration * (motor.IsGrounded ? 1f : settings.AirControl);
                motor.HorizontalVelocity = MoveTowards(motor.HorizontalVelocity, target, acceleration * DeltaTime);
            }

            private static float3 MoveTowards(float3 current, float3 target, float maxDelta)
            {
                float3 delta = target - current;
                float length = math.length(delta);
                if (length <= maxDelta || length < 1e-6f)
                {
                    return target;
                }

                return current + delta / length * maxDelta;
            }
        }
    }
}
