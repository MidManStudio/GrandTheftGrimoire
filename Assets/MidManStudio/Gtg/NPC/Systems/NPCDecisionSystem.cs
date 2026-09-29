using System;
using MidManStudio.Gtg.NPC.Components;
using MidManStudio.Gtg.NPC.Native;
using Unity.Collections;
using Unity.Entities;

namespace MidManStudio.Gtg.NPC.Systems
{
    // Runs on the main thread: pinning managed arrays and calling native code cannot be Burst compiled.
    // Other ECS systems populate NPCObservation and consume NPCDecision.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class NPCDecisionSystem : SystemBase
    {
        private EntityQuery query;
        private NPCNativeObservation[] observations = Array.Empty<NPCNativeObservation>();
        private NPCNativeDecision[] decisions = Array.Empty<NPCNativeDecision>();
        private Entity[] entities = Array.Empty<Entity>();
        private double nextDecisionTime;
        private int cursor;
        private const int BatchSize = 256;
        private const double IntervalSeconds = 0.1;
        private const int MaxAction = (int)NpcAction.Retreat;

        protected override void OnCreate()
        {
            query = GetEntityQuery(ComponentType.ReadOnly<NPCIdentity>(),
                ComponentType.ReadOnly<NPCObservation>(), ComponentType.ReadWrite<NPCDecision>());
            observations = new NPCNativeObservation[BatchSize];
            decisions = new NPCNativeDecision[BatchSize];
            entities = new Entity[BatchSize];
        }

        protected override void OnUpdate()
        {
            double now = SystemAPI.Time.ElapsedTime;
            if (now < nextDecisionTime) return;
            nextDecisionTime = now + IntervalSeconds;
            using var all = query.ToEntityArray(Allocator.Temp);
            if (all.Length == 0) { cursor = 0; return; }
            if (cursor >= all.Length) cursor = 0;
            // Process one capped batch per tick; rotate fairly when the population is large.
            int count = Math.Min(BatchSize, all.Length);
            for (int i = 0; i < count; i++)
            {
                var entity = all[(cursor + i) % all.Length];
                var identity = EntityManager.GetComponentData<NPCIdentity>(entity);
                var observation = EntityManager.GetComponentData<NPCObservation>(entity);
                entities[i] = entity;
                observations[i] = new NPCNativeObservation
                {
                    // Stable for this entity's lifetime; persisted NPC IDs should replace this later.
                    NpcId = identity.Id != 0 ? identity.Id : ((ulong)(uint)entity.Version << 32) | (uint)entity.Index,
                    Role = (uint)identity.Role,
                    Backend = (uint)identity.Backend,
                    ThreatVisible = observation.ThreatVisible != 0 ? 1u : 0u,
                    HealthFraction = float.IsNaN(observation.HealthFraction) ? 1f : Math.Min(1f, Math.Max(0f, observation.HealthFraction)),
                    CanMove = identity.CanMove != 0 ? 1u : 0u,
                    Reserved = 0
                };
            }
            cursor = (cursor + count) % all.Length;
            if (!NPCNativeLib.TryDecide(observations, decisions, count))
                for (int i = 0; i < count; i++)
                    decisions[i] = ManagedFallback(observations[i]);
            for (int i = 0; i < count; i++)
            {
                // Guard against a native result accidentally being assigned to the wrong entity.
                if (decisions[i].NpcId != observations[i].NpcId || decisions[i].Action < 0 || decisions[i].Action > MaxAction) continue;
                EntityManager.SetComponentData(entities[i], new NPCDecision { Action = (NpcAction)decisions[i].Action });
            }
        }

        // Mirrors the deterministic Rust decision logic; used only when the native library is unavailable.
        private static NPCNativeDecision ManagedFallback(NPCNativeObservation o)
        {
            NpcAction action;
            var role = (NpcRole)o.Role;
            bool threat = o.ThreatVisible != 0;
            bool mobile = o.CanMove != 0;
            float health = o.HealthFraction;
            if (role == NpcRole.Merchant) action = threat ? NpcAction.Idle : NpcAction.Trade;
            else if (role == NpcRole.Civilian) action = threat && mobile ? NpcAction.Retreat : mobile && !threat ? NpcAction.Patrol : NpcAction.Idle;
            else if (!threat) action = mobile ? NpcAction.Patrol : NpcAction.Idle;
            else if (health < 0.2f && mobile) action = NpcAction.Retreat;
            else if ((DecisionBackend)o.Backend == DecisionBackend.StateMachine) action = NpcAction.Attack;
            else action = mobile && 1f - health > 0.6f + 0.4f * health ? NpcAction.Retreat : NpcAction.Attack;
            return new NPCNativeDecision { NpcId = o.NpcId, Action = (int)action };
        }
    }
}
