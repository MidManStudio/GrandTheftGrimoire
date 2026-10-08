// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedMissionResolver.cs"
// ============================================================================

using MidManStudio.Gtg.NPC.Components;
using MidManStudio.Gtg.NPC.Native;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>
    /// Decides how a companion mission that happens off screen ends. It asks the Rust library and falls
    /// back to a managed copy of the same rules when the library is not there. A mission the player joins
    /// is played out and does not go through here.
    /// </summary>
    public static class ManagedMissionResolver
    {
        /// <summary>
        /// Resolves a delivery or a raid. The noise is a random number from the caller, which keeps the result
        /// reproducible. Returns false for an order that is not a mission or a level outside 0 to 65535.
        /// </summary>
        public static bool TryResolve(NpcOrder order, int companionLevel, int missionLevel, uint noise, out NpcMissionOutcome outcome)
        {
            outcome = NpcMissionOutcome.Failed;
            if (order != NpcOrder.Deliver && order != NpcOrder.Raid)
            {
                return false;
            }

            if (companionLevel < 0 || missionLevel < 0 || companionLevel > 65535 || missionLevel > 65535)
            {
                return false;
            }

            int result;
            if (NPCNativeLib.TryResolveMission((uint)order, (uint)companionLevel, (uint)missionLevel, noise, out result))
            {
                outcome = (NpcMissionOutcome)result;
                return true;
            }

            return NPCManagedMission.TryResolve((uint)order, (uint)companionLevel, (uint)missionLevel, noise, out outcome);
        }
    }
}
