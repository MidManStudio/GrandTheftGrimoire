using MidManStudio.Gtg.NPC.Components;

namespace MidManStudio.Gtg.NPC.Native
{
    // Managed copy of the deterministic Rust decision (rust/npc/crates/npc-behavior decide()), used
    // only when the native library is unavailable. It has no Unity dependencies so the C# ABI test
    // (rust/npc/csharp-abi-test) can compare it with the real native library on thousands of NPCs.
    // The native side rejects unknown values and non-boolean flags; this copy does not validate, so callers
    // must pass values from the enums in NPCEnums.cs. Keep it in step with decide(): the refusal gaps, the
    // loyalty weights and the betrayal numbers are copies of the constants in npc-behavior, and every float
    // expression keeps the same order of operations so the results match bit for bit.
    internal static class NPCManagedFallback
    {
        // Same values as the constants in npc-behavior. Not const on purpose: the compiler would fold constant
        // float expressions in double precision, and Rust folds them in single precision.
        private static readonly float WeightTrust = 0.4f;
        private static readonly float WeightAffinity = 0.3f;
        private static readonly float WeightPay = 0.3f;
        private static readonly float LoyaltyCeiling = 0.5f;
        private static readonly float MaxBetrayChance = 0.5f;

        // How many levels above its own a companion still accepts an order of this type. Same as refuse_gap().
        internal static int RefuseGap(NpcOrder order)
        {
            switch (order)
            {
                case NpcOrder.Attack: return 3;
                case NpcOrder.Deliver: return 6;
                case NpcOrder.Raid: return 1;
                default: return 5;
            }
        }

        // A value from 0 to 1. Anything that is not finite counts as 1, the most loyal value.
        private static float Unit(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            return System.Math.Min(1f, System.Math.Max(0f, value));
        }

        internal static float Loyalty(NPCNativeObservation o)
        {
            float trust = (float)(WeightTrust * Unit(o.Trustworthiness));
            float affinity = (float)(WeightAffinity * Unit(o.Affinity));
            float pay = (float)(WeightPay * Unit(o.PaySatisfaction));
            return (float)((float)(trust + affinity) + pay);
        }

        internal static float BetrayalChance(float loyalty)
        {
            float fraction = (float)((float)(LoyaltyCeiling - loyalty) / LoyaltyCeiling);
            return (float)(System.Math.Min(1f, System.Math.Max(0f, fraction)) * MaxBetrayChance);
        }

        // A number from 0 up to but not including 1, from the top 24 bits so it is exact in a float.
        internal static float Roll(uint noise)
        {
            return (float)(noise >> 8) / 16777216f;
        }

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

        private static NpcAction MissionOrIdle(bool mobile)
        {
            return mobile ? NpcAction.Mission : NpcAction.Idle;
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
            if (o.BetrayalOpportunity != 0 && Roll(o.Noise) < BetrayalChance(Loyalty(o))) return NpcAction.Betray;
            var order = (NpcOrder)o.Order;
            if (order != NpcOrder.None && o.OrderLevel > o.Level + RefuseGap(order)) return NpcAction.RefuseOrder;
            switch (order)
            {
                case NpcOrder.Hold:
                    return NpcAction.Hold;
                case NpcOrder.Attack:
                    return threat ? Fight(o, mobile, health) : FollowOrIdle(mobile);
                case NpcOrder.Raid:
                    return threat ? Fight(o, mobile, health) : MissionOrIdle(mobile);
                case NpcOrder.Deliver:
                    return threat && o.Provoked != 0 ? Fight(o, mobile, health) : MissionOrIdle(mobile);
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

    // Managed copy of resolve_mission() in npc-behavior, for when the native library is unavailable. Same
    // numbers, same order of operations, and the same checks as gtg_npc_resolve_mission.
    internal static class NPCManagedMission
    {
        // Not const, for the same reason as in NPCManagedFallback.
        private static readonly float DeliverBase = 0.9f;
        private static readonly float DeliverPerLevel = 0.03f;
        private static readonly float DeliverMin = 0.1f;
        private static readonly float DeliverMax = 0.99f;
        private static readonly float DeliverFailed = 0.8f;
        private static readonly float DeliverCaught = 0.15f;
        private static readonly float RaidBase = 0.5f;
        private static readonly float RaidPerLevel = 0.06f;
        private static readonly float RaidMin = 0.05f;
        private static readonly float RaidMax = 0.95f;
        private static readonly float RaidFailed = 0.35f;
        private static readonly float RaidCaught = 0.4f;

        // False for an order that is not a mission, or a level outside 0 to 65535.
        internal static bool TryResolve(uint order, uint companionLevel, uint missionLevel, uint noise, out NpcMissionOutcome outcome)
        {
            outcome = NpcMissionOutcome.Failed;
            float baseChance, perLevel, min, max, failed, caught;
            if (order == (uint)NpcOrder.Deliver)
            {
                baseChance = DeliverBase; perLevel = DeliverPerLevel; min = DeliverMin; max = DeliverMax; failed = DeliverFailed; caught = DeliverCaught;
            }
            else if (order == (uint)NpcOrder.Raid)
            {
                baseChance = RaidBase; perLevel = RaidPerLevel; min = RaidMin; max = RaidMax; failed = RaidFailed; caught = RaidCaught;
            }
            else
            {
                return false;
            }

            if (companionLevel > 65535u || missionLevel > 65535u) return false;
            int difference = (int)companionLevel - (int)missionLevel;
            float chance = (float)(baseChance + (float)(perLevel * (float)difference));
            chance = System.Math.Min(max, System.Math.Max(min, chance));
            if (NPCManagedFallback.Roll(noise) < chance)
            {
                outcome = NpcMissionOutcome.Success;
                return true;
            }

            float split = (float)(noise & 0xFFu) / 256f;
            if (split < failed) outcome = NpcMissionOutcome.Failed;
            else if (split < (float)(failed + caught)) outcome = NpcMissionOutcome.Caught;
            else outcome = NpcMissionOutcome.Killed;
            return true;
        }
    }
}
