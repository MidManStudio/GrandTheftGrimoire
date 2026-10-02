#if GTG_ECS
// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterGravitySystem.cs"
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Gravity module. Accelerates the vertical speed downward up to the fall cap, and
    /// holds a small downward speed while grounded so the character follows the floor.
    /// With the module off the vertical speed is left alone, which a flight module can use.
    /// </summary>
    [UpdateAfter(typeof(CharacterJumpSystem))]
    [BurstCompile]
    public partial struct CharacterGravitySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new GravityJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct GravityJob : IJobEntity
        {
            public float DeltaTime;

            public void Execute(
                ref CharacterMotor motor,
                in CharacterSettings settings,
                in CharacterFeatures features)
            {
                if (!features.Has(CharacterFeature.Gravity))
                {
                    return;
                }

                if (motor.IsGrounded && motor.VerticalVelocity <= 0f)
                {
                    motor.VerticalVelocity = -settings.GroundStickSpeed;
                    return;
                }

                motor.VerticalVelocity = math.max(
                    motor.VerticalVelocity + settings.Gravity * DeltaTime,
                    -settings.GravityCap);
            }
        }
    }
}
#endif
