#if GTG_ECS
// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/chemistry.md, section "ChemicalHazardSystem.cs"
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace MidManStudio.Gtg.Chemistry
{
    /// <summary>Grows each hazard to its full radius, then removes it when its duration ends.</summary>
    [BurstCompile]
    public partial struct ChemicalHazardSystem : ISystem
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

            new HazardJob
            {
                Now = SystemAPI.Time.ElapsedTime,
                Ecb = ecb.AsParallelWriter(),
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct HazardJob : IJobEntity
        {
            public double Now;
            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute([ChunkIndexInQuery] int sortKey, Entity entity, ref ChemicalHazard hazard)
            {
                float age = (float)(Now - hazard.SpawnTime);
                if (age >= hazard.Duration)
                {
                    Ecb.DestroyEntity(sortKey, entity);
                    return;
                }

                float grown = hazard.GrowDuration <= 0f ? 1f : math.saturate(age / hazard.GrowDuration);
                hazard.CurrentRadius = hazard.MaxRadius * grown;
            }
        }
    }
}
#endif
