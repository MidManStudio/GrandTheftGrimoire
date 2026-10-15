// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedMissionSource.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Missions
{
    /// <summary>
    /// What the missions panel of the HUD shows: a title and a few lines. The tracker of this folder
    /// implements it, and so can an adapter for a quest package. The HUD reads it through
    /// <see cref="ManagedMissionSources"/> and knows nothing else about missions.
    /// </summary>
    public interface IManagedMissionSource
    {
        /// <summary>The heading of the panel, such as the name of the active mission.</summary>
        string Title { get; }

        /// <summary>How many lines there are. The panel shows the first few and ignores the rest.</summary>
        int LineCount { get; }

        /// <summary>The text of a line. An index out of range gives an empty line.</summary>
        string LineAt(int index);
    }

    /// <summary>
    /// The one mission source the HUD reads. While none is registered the HUD leaves the panel alone, so
    /// the HUD's own SetMissions call and the dev keys still work. The last source to register wins, and
    /// one that is displaced does not come back when the newer one leaves.
    /// </summary>
    public static class ManagedMissionSources
    {
        private static IManagedMissionSource _current;

        /// <summary>The registered source, or null.</summary>
        public static IManagedMissionSource Current { get { return _current; } }

        /// <summary>Makes the source the current one. A null source is ignored.</summary>
        public static void Register(IManagedMissionSource source)
        {
            if (source != null)
            {
                _current = source;
            }
        }

        /// <summary>Clears the current source when it is this one. Any other source stays.</summary>
        public static void Unregister(IManagedMissionSource source)
        {
            if (source != null && ReferenceEquals(_current, source))
            {
                _current = null;
            }
        }

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _current = null;
        }
    }
}
