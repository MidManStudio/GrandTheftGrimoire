// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedBuildRules.cs"
// ============================================================================

using System;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>Numbers every placement check shares. Serialized on <see cref="ManagedBuildMode"/>.</summary>
    [Serializable]
    public sealed class ManagedBuildRules
    {
        [Tooltip("Side of one grid cell in meters. Pieces snap to this grid.")]
        public float CellSize = 0.5f;

        [Tooltip("Farthest a piece can be placed from the player, measured on the ground plane. Zero or less turns the limit off.")]
        public float MaxReach = 8f;

        [Tooltip("Largest height difference allowed between the five ground samples under a footprint.")]
        public float MaxGroundDelta = 0.15f;

        [Tooltip("The blocker box starts this far above the ground, so the ground itself never counts as a blocker.")]
        public float BlockerSkin = 0.1f;

        [Tooltip("The ground samples and the blocker box are pulled in by this much from the footprint edge, so a neighbor that touches the edge does not block.")]
        public float EdgeInset = 0.05f;
    }
}
