// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/magic.md, section "FireballCastSystem.cs"
// ============================================================================

using MidManStudio.Gtg.CharacterController;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MidManStudio.Gtg.Magic
{
    /// <summary>
    /// Cast module. Spawns a fireball entity when the fire button goes down, the cast
    /// feature is on and the cooldown has run out. The shot goes straight along the look
    /// direction.
    /// </summary>
    [UpdateAfter(typeof(CharacterMovementSystem))]
    [BurstCompile]
    public partial struct FireballCastSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            new CastJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = ecb.AsParallelWriter(),
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct CastJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute(
                [ChunkIndexInQuery] int sortKey,
                Entity entity,
                ref SpellCaster caster,
                in CharacterInput input,
                in CharacterLook look,
                in CharacterFeatures features,
                in LocalTransform transform)
            {
                caster.CooldownRemaining = math.max(0f, caster.CooldownRemaining - DeltaTime);

                if (!features.Has(CharacterFeature.Cast) || !input.FirePressed || caster.CooldownRemaining > 0f)
                {
                    return;
                }

                float yaw = math.radians(look.Yaw);
                float pitch = math.radians(look.Pitch);
                quaternion yawRotation = quaternion.RotateY(yaw);
                quaternion aimRotation = quaternion.Euler(pitch, yaw, 0f);

                float3 forward = math.mul(aimRotation, new float3(0f, 0f, 1f));
                float3 origin = transform.Position + math.mul(yawRotation, caster.MuzzleOffset);

                Entity projectile = Ecb.CreateEntity(sortKey);
                Ecb.AddComponent(sortKey, projectile, LocalTransform.FromPositionRotation(origin, aimRotation));
                Ecb.AddComponent(sortKey, projectile, new FireballProjectile
                {
                    Velocity = forward * caster.ProjectileSpeed,
                    RemainingLife = caster.ProjectileLifetime,
                    Owner = entity,
                });

                caster.CooldownRemaining = caster.Cooldown;
            }
        }
    }
}
