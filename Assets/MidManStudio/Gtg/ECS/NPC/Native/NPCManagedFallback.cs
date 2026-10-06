using MidManStudio.Gtg.NPC.Components;

namespace MidManStudio.Gtg.NPC.Native
{
    // Managed copy of the deterministic Rust decision (rust/npc/crates/npc-behavior decide()), used
    // only when the native library is unavailable. It has no Unity dependencies so the C# ABI test
    // (rust/npc/csharp-abi-test) can compare it with the real native library on thousands of NPCs.
    // The native side rejects unknown values and non-boolean flags; this copy does not validate, so callers
    // must pass values from the enums in NPCEnums.cs. Keep it in step with decide(), including RefuseLevelGap.
    internal static class NPCManagedFallback
    {
        // Same value as REFUSE_LEVEL_GAP in npc-behavior.
        internal const int RefuseLevelGap = 5;

        internal static NPCNativeDecision Decide(NPCNativeObservation o)
        {
            var role = (NpcRole)o.Role;
            bool threat = o.ThreatVisible != 0;
            bool mobile = o.CanMove != 0;
            float health = float.IsNaN(o.HealthFraction) || float.IsInfinity(o.HealthFraction)
                ? 1f
                : System.Math.Min(1f, System.Math.Max(0f, o.HealthFraction));
            NpcAction action;
            switch (role)
            {
                case NpcRole.Merchant:
                    action = threat ? NpcAction.Idle : NpcAction.Trade;
                    break;
                case NpcRole.Civilian:
                    action = threat && mobile ? NpcAction.Retreat : Wander(mobile);
                    break;
                case NpcRole.Companion:
                    action = Companion(o, threat, mobile, health);
                    break;
                default:
                    action = Combatant(o, threat, mobile, health);
                    break;
            }

            return new NPCNativeDecision { NpcId = o.NpcId, Action = (int)action };
        }

        private static NpcAction Wander(bool mobile)
        {
            return mobile ? NpcAction.Patrol : NpcAction.Idle;
        }

        private static NpcAction FollowOrIdle(bool mobile)
        {
            return mobile ? NpcAction.Follow : NpcAction.Idle;
        }

        private static NpcAction Combatant(NPCNativeObservation o, bool threat, bool mobile, float health)
        {
            if (!threat) return Wander(mobile);
            var disposition = (NpcDisposition)o.Disposition;
            if (disposition == NpcDisposition.Peaceful) return mobile ? NpcAction.Retreat : NpcAction.Idle;
            if (disposition == NpcDisposition.Retaliatory && o.Provoked == 0) return Wander(mobile);
            return Fight(o, mobile, health);
        }

        private static NpcAction Companion(NPCNativeObservation o, bool threat, bool mobile, float health)
        {
            var order = (NpcOrder)o.Order;
            if (order != NpcOrder.None && o.OrderLevel > o.Level + RefuseLevelGap) return NpcAction.RefuseOrder;
            switch (order)
            {
                case NpcOrder.Hold:
                    return NpcAction.Hold;
                case NpcOrder.Attack:
                    return threat ? Fight(o, mobile, health) : FollowOrIdle(mobile);
                default:
                    return threat && o.Provoked != 0 ? Fight(o, mobile, health) : FollowOrIdle(mobile);
            }
        }

        private static NpcAction Fight(NPCNativeObservation o, bool mobile, float health)
        {
            if (health < 0.2f && mobile) return NpcAction.Retreat;
            if ((DecisionBackend)o.Backend == DecisionBackend.StateMachine) return NpcAction.Attack;
            return mobile && 1f - health > 0.6f + 0.4f * health ? NpcAction.Retreat : NpcAction.Attack;
        }
    }
}
