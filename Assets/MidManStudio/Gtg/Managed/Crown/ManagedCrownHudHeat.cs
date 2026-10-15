// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCrownHudHeat.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Hud;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Crown
{
    /// <summary>
    /// Feeds the HUD's heat crowns from Crown Heat. It registers as the HUD's heat source while it is
    /// enabled. The HUD asks it for the sections at the player's position each frame, and it answers with the
    /// heat of the region around that point, or 0 outside every region. Add it to any GameObject, one is
    /// enough. The HUD never references the Crown folder, this file is the only link.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedCrownHudHeat : MonoBehaviour, IManagedHudHeatSource
    {
        /// <summary>The sections for a player standing at the point, see <see cref="ManagedCrownHeatMap"/>.</summary>
        public int SectionsAt(Vector3 point)
        {
            ManagedCrownRegion region = ManagedCrownRegion.Find(point);
            if (region == null)
            {
                return 0;
            }

            return ManagedCrownHeatMap.Sections(
                tier: region.Tier,
                heat: region.Heat,
                wantedAt: region.WantedAt,
                huntedAt: region.HuntedAt,
                outlawAt: region.OutlawAt,
                maxHeat: region.MaxHeat);
        }

        private void OnEnable()
        {
            ManagedHudHeatSources.Register(this);
        }

        private void OnDisable()
        {
            ManagedHudHeatSources.Unregister(this);
        }
    }
}
