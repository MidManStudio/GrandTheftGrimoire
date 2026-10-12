// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "IManagedFlagSource.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Flags
{
    /// <summary>
    /// Answers one question: is this named condition true. A shop asks it to learn whether it
    /// is open, and it never learns who set the flag. <see cref="ManagedWorldFlags"/> is the plain
    /// answer today. A quest package can stand behind the same method later, with a flag name
    /// that is a quest id and an answer that means the quest is complete.
    /// </summary>
    public interface IManagedFlagSource
    {
        /// <summary>True when the flag is set. A null or empty name is never set.</summary>
        bool IsSet(string flag);
    }
}
