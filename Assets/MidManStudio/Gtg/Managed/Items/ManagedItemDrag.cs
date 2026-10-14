// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedItemDrag.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>Where a dragged item came from.</summary>
    public enum ManagedItemDragOrigin
    {
        /// <summary>Anything that is not listed. The drop target treats the item as new.</summary>
        Other = 0,

        /// <summary>A slot of a bag. The source index is the bag slot.</summary>
        Bag = 1,

        /// <summary>A quick slot. The source index is the quick slot.</summary>
        QuickSlot = 2,
    }

    /// <summary>What a drag carries.</summary>
    public readonly struct ManagedItemPayload
    {
        public readonly int ItemId;
        public readonly int Count;
        public readonly ManagedItemDragOrigin Origin;
        public readonly int SourceIndex;

        public ManagedItemPayload(int itemId, int count, ManagedItemDragOrigin origin, int sourceIndex)
        {
            ItemId = itemId;
            Count = count < 1 ? 1 : count;
            Origin = origin;
            SourceIndex = sourceIndex;
        }
    }

    /// <summary>
    /// The one place a dragged item waits between the thing it was picked up from and the thing it
    /// is dropped on. A source calls <see cref="Begin"/>, a target that takes it calls
    /// <see cref="Accept"/>, and the source calls <see cref="End"/> to learn whether anything took
    /// it. Sources and targets never reference each other, so a quick slot, a bag window and a
    /// trade window can all be either one. It holds plain values and no UI.
    /// </summary>
    public static class ManagedItemDrag
    {
        private static bool _active;
        private static bool _accepted;
        private static ManagedItemPayload _payload;

        public static bool Active { get { return _active; } }

        /// <summary>The payload of the drag in progress. Meaningless while <see cref="Active"/> is false.</summary>
        public static ManagedItemPayload Payload { get { return _payload; } }

        /// <summary>True once a target has taken the payload of the drag in progress.</summary>
        public static bool Accepted { get { return _accepted; } }

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Reset();
        }

        /// <summary>
        /// Starts a drag. A drag that was never ended, because its source went away, is dropped
        /// and replaced. A count below 1 is carried as 1.
        /// </summary>
        public static void Begin(int itemId, int count, ManagedItemDragOrigin origin, int sourceIndex)
        {
            _payload = new ManagedItemPayload(itemId, count, origin, sourceIndex);
            _accepted = false;
            _active = true;
        }

        /// <summary>A target calls this when it took the payload. Ignored when no drag is active.</summary>
        public static void Accept()
        {
            if (_active)
            {
                _accepted = true;
            }
        }

        /// <summary>Ends the drag and returns whether a target took it. Returns false when none was active.</summary>
        public static bool End()
        {
            bool accepted = _accepted;
            _active = false;
            _accepted = false;
            return accepted;
        }

        /// <summary>Drops any drag in progress without a result.</summary>
        public static void Reset()
        {
            _active = false;
            _accepted = false;
            _payload = default(ManagedItemPayload);
        }
    }
}
