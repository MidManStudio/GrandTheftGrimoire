// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCharacterFeature.cs"
// ============================================================================

using System;

namespace MidManStudio.Gtg.Managed.CharacterController
{
    /// <summary>One switch per character module. A module does nothing when its flag is off.</summary>
    [Flags]
    public enum ManagedCharacterFeature
    {
        None = 0,
        Look = 1 << 0,
        Move = 1 << 1,
        Jump = 1 << 2,
        Gravity = 1 << 3,
        Cast = 1 << 4,
        All = Look | Move | Jump | Gravity | Cast,
    }
}
