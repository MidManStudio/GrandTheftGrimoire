// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/magic.md, section "FireballProjectileSystem.cs"
// ============================================================================

using MidManStudio.Gtg.CharacterController;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace MidManStudio.Gtg.Magic
{
    /// <summary>
    /// Moves fireballs in a straight line. Each step casts a ray from the old
    /// position to the new one, so a fast shot cannot pass through a thin
    /// collider. A hit destroys the fireball and records a
    /// <see cref="SpellImpactEvent"/>. Running out of life destroys it silently.
    /// </summary>
    [UpdateAfter(typeof(FireballCastSystem))]
    [BurstCompile]
    public partial struct FireballProjectileSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            Entity impactEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<SpellImpactEvent>(impactEntity);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            EntityCommandBuffer ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            DynamicBuffer<SpellImpactEvent> impacts = SystemAPI.GetSingletonBuffer<SpellImpactEvent>();
            impacts.Clear();

            foreach (var (transform, projectile, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<FireballProjectile>>()
                         .WithEntityAccess())
            {
                float3 start = transform.ValueRO.Position;
                float3 end = start + projectile.ValueRO.Velocity * deltaTime;

                var rayInput = new RaycastInput
                {
                    Start = start,
                    End = end,
                    Filter = CollisionFilter.Default,
                };

                if (PhysicsRayUtility.CastRayIgnoring(collisionWorld, rayInput, projectile.ValueRO.Owner, out RaycastHit hit))
                {
                    impacts.Add(new SpellImpactEvent
                    {
                        Position = hit.Position,
                        Normal = hit.SurfaceNormal,
                        Kind = SpellKind.Fireball,
                    });
                    ecb.DestroyEntity(entity);
                    continue;
                }

                transform.ValueRW.Position = end;
                projectile.ValueRW.RemainingLife -= deltaTime;
                if (projectile.ValueRO.RemainingLife <= 0f)
                {
                    ecb.DestroyEntity(entity);
                }
            }
        }
    }
}
