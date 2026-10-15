// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudHeatSource.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// Where the HUD gets its heat from. A system that knows how wanted the player is implements this
    /// and registers itself with <see cref="ManagedHudHeatSources"/>. The HUD knows nothing of the system
    /// behind it, and the system knows nothing of the HUD beyond this file.
    /// </summary>
    public interface IManagedHudHeatSource
    {
        /// <summary>
        /// The filled heat sections, 0 through <see cref="ManagedHudMath.MaxHeat"/>, for a player standing at
        /// the point. A value outside that range is held to it. Called every frame, so keep it cheap.
        /// </summary>
        int SectionsAt(Vector3 point);
    }

    /// <summary>
    /// The one heat source the HUD reads. While none is registered the HUD leaves the heat alone, so
    /// <see cref="ManagedHud.SetHeat"/> and the dev keys still work. The last source to register wins, and
    /// one that is displaced does not come back when the newer one leaves.
    /// </summary>
    public static class ManagedHudHeatSources
    {
        private static IManagedHudHeatSource _current;

        /// <summary>The registered source, or null.</summary>
        public static IManagedHudHeatSource Current { get { return _current; } }

        /// <summary>Makes the source the current one. A null source is ignored.</summary>
        public static void Register(IManagedHudHeatSource source)
        {
            if (source != null)
            {
                _current = source;
            }
        }

        /// <summary>Clears the current source when it is this one. Any other source stays.</summary>
        public static void Unregister(IManagedHudHeatSource source)
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
