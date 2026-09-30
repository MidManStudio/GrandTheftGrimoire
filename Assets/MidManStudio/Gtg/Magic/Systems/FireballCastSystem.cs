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
    /// Spawns a fireball entity when the fire button goes down and the
    /// cooldown has run out. The shot goes straight along the look direction.
    /// </summary>
    [UpdateAfter(typeof(CharacterMovementSystem))]
    [BurstCompile]
    public partial struct FireballCastSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SpellCaster>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            EntityCommandBuffer ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (caster, input, look, transform, entity) in
                     SystemAPI.Query<
                         RefRW<SpellCaster>,
                         RefRO<CharacterInput>,
                         RefRO<CharacterLook>,
                         RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                SpellCaster cfg = caster.ValueRO;
                cfg.CooldownRemaining = math.max(0f, cfg.CooldownRemaining - deltaTime);

                if (input.ValueRO.FirePressed && cfg.CooldownRemaining <= 0f)
                {
                    float yaw = math.radians(look.ValueRO.Yaw);
                    float pitch = math.radians(look.ValueRO.Pitch);
                    quaternion yawRotation = quaternion.RotateY(yaw);
                    quaternion aimRotation = quaternion.Euler(pitch, yaw, 0f);

                    float3 forward = math.mul(aimRotation, new float3(0f, 0f, 1f));
                    float3 origin = transform.ValueRO.Position + math.mul(yawRotation, cfg.MuzzleOffset);

                    Entity projectile = ecb.CreateEntity();
                    ecb.AddComponent(projectile, LocalTransform.FromPositionRotation(origin, aimRotation));
                    ecb.AddComponent(projectile, new FireballProjectile
                    {
                        Velocity = forward * cfg.ProjectileSpeed,
                        RemainingLife = cfg.ProjectileLifetime,
                        Owner = entity,
                    });

                    cfg.CooldownRemaining = cfg.Cooldown;
                }

                caster.ValueRW = cfg;
            }
        }
    }
}
