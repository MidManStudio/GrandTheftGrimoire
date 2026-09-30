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
    /// Integrates look input into yaw and pitch, and turns the entity to face
    /// the yaw. Pitch is clamped and only stored, since the body stays upright.
    /// </summary>
    [UpdateAfter(typeof(CharacterInputSystem))]
    [BurstCompile]
    public partial struct CharacterLookSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (look, transform, input, settings) in
                     SystemAPI.Query<
                         RefRW<CharacterLook>,
                         RefRW<LocalTransform>,
                         RefRO<CharacterInput>,
                         RefRO<CharacterMoveSettings>>()
                         .WithAll<CharacterTag>())
            {
                float2 delta = input.ValueRO.Look;
                float yaw = math.fmod(look.ValueRO.Yaw + delta.x, 360f);
                float pitch = math.clamp(
                    look.ValueRO.Pitch - delta.y,
                    settings.ValueRO.MinPitch,
                    settings.ValueRO.MaxPitch);

                look.ValueRW.Yaw = yaw;
                look.ValueRW.Pitch = pitch;
                transform.ValueRW.Rotation = quaternion.RotateY(math.radians(yaw));
            }
        }
    }
}
