#if GTG_ECS
// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterLookSystem.cs"
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Look module. Adds the frame's look change to yaw and pitch, clamps pitch, and
    /// turns the entity to the yaw. Pitch is only stored, the body stays upright.
    /// </summary>
    [UpdateAfter(typeof(CharacterInputSystem))]
    [BurstCompile]
    public partial struct CharacterLookSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new LookJob().ScheduleParallel();
        }

        [BurstCompile]
        public partial struct LookJob : IJobEntity
        {
            public void Execute(
                ref CharacterLook look,
                ref LocalTransform transform,
                in CharacterInput input,
                in CharacterSettings settings,
                in CharacterFeatures features)
            {
                if (!features.Has(CharacterFeature.Look))
                {
                    return;
                }

                float yaw = math.fmod(look.Yaw + input.Look.x, 360f);
                look.Yaw = yaw;
                look.Pitch = math.clamp(look.Pitch - input.Look.y, settings.MinPitch, settings.MaxPitch);
                transform.Rotation = quaternion.RotateY(math.radians(yaw));
            }
        }
    }
}
#endif
