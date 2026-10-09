// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedVesselBelt.cs"
// ============================================================================

using System;

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>
    /// The vessels the player can cast from, one per slot. A bottle slot holds a count of
    /// full bottles and each cast uses one up. An orb slot holds one orb whose SP is spent
    /// by a cast and charges back slowly with <see cref="Tick"/>. The belt knows nothing about
    /// spell definitions, flight or physics. It hands out a <see cref="ManagedVesselDraw"/>
    /// and the caller turns that into a shot. It holds plain values only, so another carrier,
    /// such as an inventory package, can fill and read it.
    /// </summary>
    public sealed class ManagedVesselBelt
    {
        // An orb below this has nothing to throw and fizzles in the hand.
        private const float MinDrawSp = 0.01f;

        private struct Slot
        {
            public bool InUse;
            public ManagedVesselKind Kind;
            public ManagedSpellKind Spell;
            public float Capacity;
            public float Rating;
            public float Sp;
            public float RechargeSeconds;
            public int Count;
            public int StartCount;
        }

        private readonly Slot[] _slots;

        public ManagedVesselBelt(int slotCount)
        {
            _slots = new Slot[Math.Max(1, slotCount)];
        }

        public int SlotCount { get { return _slots.Length; } }

        /// <summary>
        /// Puts a vessel in a slot, full. A bottle slot starts with <paramref name="count"/>
        /// bottles. An orb slot holds one orb that takes <paramref name="rechargeSeconds"/>
        /// to charge from empty. An index outside the belt is ignored.
        /// </summary>
        public void Set(
            int slot, ManagedVesselKind kind, ManagedSpellKind spell,
            float capacity, float rating, int count, float rechargeSeconds)
        {
            if (!InRange(slot))
            {
                return;
            }

            int held = kind == ManagedVesselKind.Orb ? 1 : Math.Max(0, count);
            _slots[slot] = new Slot
            {
                InUse = true,
                Kind = kind,
                Spell = spell,
                Capacity = Math.Max(0f, capacity),
                Rating = rating,
                Sp = Math.Max(0f, capacity),
                RechargeSeconds = rechargeSeconds,
                Count = held,
                StartCount = held,
            };
        }

        public bool IsInUse(int slot)
        {
            return InRange(slot) && _slots[slot].InUse;
        }

        public ManagedSpellKind SpellAt(int slot)
        {
            return InRange(slot) ? _slots[slot].Spell : ManagedSpellKind.Fireball;
        }

        public ManagedVesselKind KindAt(int slot)
        {
            return InRange(slot) ? _slots[slot].Kind : ManagedVesselKind.Bottle;
        }

        /// <summary>Bottles left in a bottle slot, or 1 for an orb.</summary>
        public int CountAt(int slot)
        {
            return IsInUse(slot) ? _slots[slot].Count : 0;
        }

        public float SpAt(int slot)
        {
            return IsInUse(slot) ? _slots[slot].Sp : 0f;
        }

        /// <summary>How full the vessel is, from 0 to 1. For a bottle slot, the share of bottles left.</summary>
        public float FractionAt(int slot)
        {
            if (!IsInUse(slot))
            {
                return 0f;
            }

            Slot s = _slots[slot];
            if (s.Kind == ManagedVesselKind.Bottle)
            {
                return s.StartCount > 0 ? (float)s.Count / s.StartCount : 0f;
            }

            return s.Capacity > 0f ? Math.Min(1f, s.Sp / s.Capacity) : 0f;
        }

        public bool CanDraw(int slot)
        {
            if (!IsInUse(slot))
            {
                return false;
            }

            Slot s = _slots[slot];
            return s.Kind == ManagedVesselKind.Bottle ? s.Count > 0 : s.Sp > MinDrawSp;
        }

        /// <summary>
        /// Takes what one cast uses. A bottle gives one full bottle. An orb gives all the SP it
        /// has, however little, and is empty after. Returns false and changes nothing when the
        /// slot is empty or unused.
        /// </summary>
        public bool TryDraw(int slot, out ManagedVesselDraw draw)
        {
            draw = default(ManagedVesselDraw);
            if (!CanDraw(slot))
            {
                return false;
            }

            Slot s = _slots[slot];
            draw.Capacity = s.Capacity;
            draw.Rating = s.Rating;

            if (s.Kind == ManagedVesselKind.Bottle)
            {
                draw.Sp = s.Capacity;
                s.Count--;
            }
            else
            {
                draw.Sp = s.Sp;
                s.Sp = 0f;
            }

            _slots[slot] = s;
            return true;
        }

        /// <summary>
        /// Charges every orb for <paramref name="dt"/> seconds of game time, at its capacity
        /// divided by its recharge time. An orb with no recharge time fills at once. Bottles do
        /// not charge.
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                Slot s = _slots[i];
                if (!s.InUse || s.Kind != ManagedVesselKind.Orb || s.Sp >= s.Capacity)
                {
                    continue;
                }

                float gain = s.RechargeSeconds > 0f ? s.Capacity / s.RechargeSeconds * dt : s.Capacity;
                s.Sp = Math.Min(s.Capacity, s.Sp + gain);
                _slots[i] = s;
            }
        }

        /// <summary>Fills every orb and restores every bottle slot to the count it started with. For testing.</summary>
        public void Refill()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot s = _slots[i];
                if (!s.InUse)
                {
                    continue;
                }

                s.Sp = s.Capacity;
                s.Count = s.StartCount;
                _slots[i] = s;
            }
        }

        /// <summary>A short line for the debug overlay, such as "orb 62 percent" or "bottles 4 of 5".</summary>
        public string Describe(int slot)
        {
            if (!IsInUse(slot))
            {
                return "nothing";
            }

            Slot s = _slots[slot];
            if (s.Kind == ManagedVesselKind.Bottle)
            {
                return s.Count > 0 ? "bottles " + s.Count + " of " + s.StartCount : "bottles used up";
            }

            return s.Sp > MinDrawSp ? "orb " + (int)(FractionAt(slot) * 100f) + " percent" : "orb empty";
        }

        private bool InRange(int slot)
        {
            return slot >= 0 && slot < _slots.Length;
        }
    }
}
