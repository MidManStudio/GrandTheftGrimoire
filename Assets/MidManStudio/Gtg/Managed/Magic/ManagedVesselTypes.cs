// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedVesselTypes.cs"
// ============================================================================

using System;

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>What holds the SP and the bubble. A bottle is used up by one cast. An orb is not.</summary>
    public enum ManagedVesselKind : byte
    {
        Bottle = 0,
        Orb = 1,
    }

    /// <summary>
    /// What one cast takes out of a vessel. The SP is spent with the shot, so a shot that
    /// hits early does not give any back. <see cref="Capacity"/> is the SP of a full vessel
    /// of this kind, which the drive cost per meter is worked out from, so a half charged
    /// orb flies half as far.
    /// </summary>
    public struct ManagedVesselDraw
    {
        /// <summary>SP the shot starts with.</summary>
        public float Sp;

        /// <summary>SP of the vessel when full.</summary>
        public float Capacity;

        /// <summary>Load the vessel holds without extra cost, on the same scale as payload load.</summary>
        public float Rating;
    }

    /// <summary>
    /// One belt slot as authored in the Inspector: which spell, in what vessel. The SP and
    /// the rating of the vessel come from the spell's definition until crafted vessels exist.
    /// </summary>
    [Serializable]
    public sealed class ManagedLoadoutEntry
    {
        public ManagedSpellKind Spell;
        public ManagedVesselKind Vessel;

        /// <summary>Bottles in the slot. Only read for a bottle.</summary>
        public int Bottles = 5;

        /// <summary>Seconds an empty orb takes to charge fully. Only read for an orb.</summary>
        public float OrbRechargeSeconds = 45f;
    }
}
