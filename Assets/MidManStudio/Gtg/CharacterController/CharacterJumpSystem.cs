// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterJumpSystem.cs"
// ============================================================================

using Unity.Burst;
using Unity.Entities;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Jump module. Jumps when the button went down and the character is grounded or
    /// left the ground within the coyote time. The coyote time is spent by the jump, so
    /// there is no second jump in the air.
    /// </summary>
    [UpdateAfter(typeof(CharacterWalkSystem))]
    [BurstCompile]
    public partial struct CharacterJumpSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new JumpJob().ScheduleParallel();
        }

        [BurstCompile]
        public partial struct JumpJob : IJobEntity
        {
            public void Execute(
                ref CharacterMotor motor,
                in CharacterInput input,
                in CharacterSettings settings,
                in CharacterFeatures features)
            {
                if (!features.Has(CharacterFeature.Jump) || !input.JumpPressed)
                {
                    return;
                }

                bool canJump = motor.IsGrounded || motor.TimeSinceGrounded <= settings.CoyoteTime;
                if (!canJump || motor.VerticalVelocity > 0f)
                {
                    return;
                }

                motor.VerticalVelocity = settings.JumpSpeed;
                motor.IsGrounded = false;
                motor.TimeSinceGrounded = settings.CoyoteTime + 1f;
            }
        }
    }
}
