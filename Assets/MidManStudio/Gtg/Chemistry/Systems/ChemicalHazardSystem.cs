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
    [UpdateAfter(typeof(ChemistryReactionSystem))]
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
            double now = SystemAPI.Time.ElapsedTime;
            EntityCommandBuffer ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (hazard, entity) in SystemAPI.Query<RefRW<ChemicalHazard>>().WithEntityAccess())
            {
                ChemicalHazard value = hazard.ValueRO;
                float age = (float)(now - value.SpawnTime);
                if (age >= value.Duration)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                float grown = value.GrowDuration <= 0f ? 1f : math.saturate(age / value.GrowDuration);
                hazard.ValueRW.CurrentRadius = value.MaxRadius * grown;
            }
        }
    }
}
