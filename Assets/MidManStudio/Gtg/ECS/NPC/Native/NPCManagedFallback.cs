#if GTG_ECS
using MidManStudio.Gtg.NPC.Components;

namespace MidManStudio.Gtg.NPC.Native
{
    // Managed copy of the deterministic Rust decision (rust/npc/crates/npc-behavior decide()), used
    // only when the native library is unavailable. It has no Unity dependencies so the C# ABI test
    // (rust/npc/csharp-abi-test) can compare it with the real native library.
    // The native side rejects unknown roles, backends and non-boolean flags; this copy does not
    // validate, so callers must pass values from the enums in NPCEnums.cs.
    internal static class NPCManagedFallback
    {
        internal static NPCNativeDecision Decide(NPCNativeObservation o)
        {
            var role = (NpcRole)o.Role;
            bool threat = o.ThreatVisible != 0;
            bool mobile = o.CanMove != 0;
            float health = float.IsNaN(o.HealthFraction) || float.IsInfinity(o.HealthFraction)
                ? 1f
                : System.Math.Min(1f, System.Math.Max(0f, o.HealthFraction));
            NpcAction action;
            if (role == NpcRole.Merchant) action = threat ? NpcAction.Idle : NpcAction.Trade;
            else if (role == NpcRole.Civilian) action = threat && mobile ? NpcAction.Retreat : mobile ? NpcAction.Patrol : NpcAction.Idle;
            else if (!threat) action = mobile ? NpcAction.Patrol : NpcAction.Idle;
            else if (health < 0.2f && mobile) action = NpcAction.Retreat;
            else if ((DecisionBackend)o.Backend == DecisionBackend.StateMachine) action = NpcAction.Attack;
            else action = mobile && 1f - health > 0.6f + 0.4f * health ? NpcAction.Retreat : NpcAction.Attack;
            return new NPCNativeDecision { NpcId = o.NpcId, Action = (int)action };
        }
    }
}
#endif
