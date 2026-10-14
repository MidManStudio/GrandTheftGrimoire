// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudWheel.cs"
// ============================================================================

using System;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>One usable thing on the wheel, described only by what is drawn for it.</summary>
    public readonly struct ManagedWheelEntry
    {
        /// <summary>The name drawn in the section.</summary>
        public readonly string Label;

        /// <summary>The name of the icon sprite, without a folder or extension. May be empty.</summary>
        public readonly string IconKey;

        public ManagedWheelEntry(string label, string iconKey)
        {
            Label = label ?? string.Empty;
            IconKey = iconKey ?? string.Empty;
        }
    }

    /// <summary>
    /// The sections of the wheel and which one is active. The wheel knows nothing about weapons or
    /// spells. Whatever owns a usable thing puts an entry in a section, listens for
    /// <see cref="Picked"/>, and makes the thing active. It holds plain values and knows nothing
    /// about Unity.
    /// </summary>
    public sealed class ManagedHudWheel
    {
        /// <summary>The section count used when none is given.</summary>
        public const int DefaultSectionCount = 4;

        private readonly ManagedWheelEntry[] _entries;
        private readonly bool[] _filled;
        private int _active = -1;

        /// <summary>Raised after an entry is set or cleared.</summary>
        public event Action Changed;

        /// <summary>Raised with the section when the active section changes by a pick.</summary>
        public event Action<int> Picked;

        /// <summary>A section count below 1 gives 1 section.</summary>
        public ManagedHudWheel(int sectionCount = DefaultSectionCount)
        {
            int count = Math.Max(1, sectionCount);
            _entries = new ManagedWheelEntry[count];
            _filled = new bool[count];
        }

        public int SectionCount { get { return _entries.Length; } }

        /// <summary>The active section, or -1 when none is.</summary>
        public int Active { get { return _active; } }

        /// <summary>Reads a section. Returns false for an empty section or an index outside the wheel.</summary>
        public bool TryGetEntry(int section, out ManagedWheelEntry entry)
        {
            if (section < 0 || section >= _entries.Length || !_filled[section])
            {
                entry = default(ManagedWheelEntry);
                return false;
            }

            entry = _entries[section];
            return true;
        }

        /// <summary>Puts an entry in a section. Returns false for an index outside the wheel.</summary>
        public bool SetEntry(int section, ManagedWheelEntry entry)
        {
            if (section < 0 || section >= _entries.Length)
            {
                return false;
            }

            _entries[section] = entry;
            _filled[section] = true;
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Empties a section. The active section stays chosen only while it holds an entry, so
        /// emptying it leaves no section active. Returns false when it was already empty.
        /// </summary>
        public bool Clear(int section)
        {
            if (section < 0 || section >= _entries.Length || !_filled[section])
            {
                return false;
            }

            _filled[section] = false;
            _entries[section] = default(ManagedWheelEntry);
            if (_active == section)
            {
                _active = -1;
            }

            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Makes a section active. An empty section or an index outside the wheel cannot be
        /// picked and the call returns false. Picking the section that is already active returns
        /// true and raises nothing.
        /// </summary>
        public bool Pick(int section)
        {
            if (section < 0 || section >= _entries.Length || !_filled[section])
            {
                return false;
            }

            if (_active == section)
            {
                return true;
            }

            _active = section;
            Action<int> picked = Picked;
            if (picked != null)
            {
                picked(section);
            }

            return true;
        }

        private void RaiseChanged()
        {
            Action changed = Changed;
            if (changed != null)
            {
                changed();
            }
        }
    }
}
