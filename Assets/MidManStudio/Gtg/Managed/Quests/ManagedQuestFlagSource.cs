// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedQuestFlagSource.cs"
// ============================================================================

using System;
using MidManStudio.Gtg.Managed.Flags;
using MidManStudio.Questly.Core;
using MidManStudio.Questly.Generated;

namespace MidManStudio.Gtg.Managed.Quests
{
    /// <summary>
    /// Answers a flag question from Questly: a flag name is a quest id, and the flag is set while that
    /// quest is COMPLETED. A shop that is given this source opens when its quest is done. It reads the
    /// <see cref="QuestRuntime"/> on every call and keeps nothing of its own.
    /// </summary>
    public sealed class ManagedQuestFlagSource : IManagedFlagSource
    {
        private readonly QuestRuntime _runtime;

        public ManagedQuestFlagSource(QuestRuntime runtime)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException("runtime");
            }

            _runtime = runtime;
        }

        /// <summary>True when the quest is COMPLETED. A null or empty name, and an id Questly does not know, are never set.</summary>
        public bool IsSet(string flag)
        {
            return !string.IsNullOrEmpty(flag) && _runtime.GetQuestState(flag) == QuestState.COMPLETED;
        }
    }
}
