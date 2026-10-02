#if GTG_ECS
// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/magic.md, section "FireballProjectileSystem.cs"
// ============================================================================

using MidManStudio.Gtg.CharacterController;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace MidManStudio.Gtg.Magic
{
    /// <summary>
    /// Moves fireballs in a straight line. Each step casts a ray from the old position to
    /// the new one, so a fast shot cannot pass through a thin collider. A hit destroys the
    /// fireball and creates a <see cref="SpellImpact"/> entity. Running out of life
    /// destroys it silently.
    /// </summary>
    [UpdateAfter(typeof(FireballCastSystem))]
    [BurstCompile]
    public partial struct FireballProjectileSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            new FlightJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
                Ecb = ecb.AsParallelWriter(),
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct FlightJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public CollisionWorld CollisionWorld;
            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute(
                [ChunkIndexInQuery] int sortKey,
                Entity entity,
                ref LocalTransform transform,
                ref FireballProjectile projectile)
            {
                float3 start = transform.Position;
                float3 end = start + projectile.Velocity * DeltaTime;

                var rayInput = new RaycastInput
                {
                    Start = start,
                    End = end,
                    Filter = CollisionFilter.Default,
                };

                if (PhysicsRayUtility.CastRayIgnoring(CollisionWorld, rayInput, projectile.Owner, out RaycastHit hit))
                {
                    Entity impact = Ecb.CreateEntity(sortKey);
                    Ecb.AddComponent(sortKey, impact, new SpellImpact
                    {
                        Position = hit.Position,
                        Normal = hit.SurfaceNormal,
                        Kind = SpellKind.Fireball,
                    });
                    Ecb.DestroyEntity(sortKey, entity);
                    return;
                }

                transform.Position = end;
                projectile.RemainingLife -= DeltaTime;
                if (projectile.RemainingLife <= 0f)
                {
                    Ecb.DestroyEntity(sortKey, entity);
                }
            }
        }
    }
}
#endif
