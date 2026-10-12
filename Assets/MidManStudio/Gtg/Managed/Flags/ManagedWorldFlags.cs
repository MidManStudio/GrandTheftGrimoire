// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedWorldFlags.cs"
// ============================================================================

using System;
using System.Collections.Generic;

namespace MidManStudio.Gtg.Managed.Flags
{
    /// <summary>
    /// A set of named flags. Mission code sets one when something has happened, and anything that
    /// holds an <see cref="IManagedFlagSource"/> can read it. It holds plain values and knows
    /// nothing about Unity. Nothing is saved.
    /// </summary>
    public sealed class ManagedWorldFlags : IManagedFlagSource
    {
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);

        public int Count { get { return _flags.Count; } }

        public bool IsSet(string flag)
        {
            return !string.IsNullOrEmpty(flag) && _flags.Contains(flag);
        }

        /// <summary>Sets a flag. Returns true when it was not set before. A null or empty name does nothing.</summary>
        public bool Set(string flag)
        {
            return !string.IsNullOrEmpty(flag) && _flags.Add(flag);
        }

        /// <summary>Clears a flag. Returns true when it was set before.</summary>
        public bool Clear(string flag)
        {
            return !string.IsNullOrEmpty(flag) && _flags.Remove(flag);
        }

        public void ClearAll()
        {
            _flags.Clear();
        }
    }
}
