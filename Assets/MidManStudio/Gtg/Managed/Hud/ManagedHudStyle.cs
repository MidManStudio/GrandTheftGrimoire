// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudStyle.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// The colours of the placeholder pieces and of the text. A piece with a drawn sprite is shown
    /// untinted, so none of these colour the finished art except the text.
    /// </summary>
    public static class ManagedHudStyle
    {
        public static readonly Color PanelBack = new Color(0.07f, 0.06f, 0.1f, 0.55f);
        public static readonly Color BarBack = new Color(0.05f, 0.05f, 0.07f, 0.8f);
        public static readonly Color HpFill = new Color(0.78f, 0.14f, 0.16f, 1f);
        public static readonly Color SpFill = new Color(0.25f, 0.5f, 0.95f, 1f);
        public static readonly Color CrownDim = new Color(0.35f, 0.3f, 0.2f, 0.6f);
        public static readonly Color CrownLit = new Color(1f, 0.78f, 0.12f, 1f);
        public static readonly Color CoinGold = new Color(0.95f, 0.75f, 0.15f, 1f);
        public static readonly Color CoinSilver = new Color(0.78f, 0.8f, 0.85f, 1f);
        public static readonly Color Slot = new Color(0.1f, 0.09f, 0.14f, 0.75f);
        public static readonly Color SlotPulse = new Color(0.85f, 0.75f, 0.35f, 0.95f);
        public static readonly Color CompassBack = new Color(0.07f, 0.06f, 0.1f, 0.5f);
        public static readonly Color CompassMark = new Color(0.9f, 0.9f, 0.95f, 1f);
        public static readonly Color CompassMarker = new Color(1f, 0.78f, 0.12f, 1f);
        public static readonly Color WheelButton = new Color(0.1f, 0.09f, 0.14f, 0.8f);
        public static readonly Color WheelBack = new Color(0.07f, 0.06f, 0.1f, 0.85f);
        public static readonly Color WheelHub = new Color(0.04f, 0.04f, 0.06f, 0.95f);
        public static readonly Color WheelHighlight = new Color(0.85f, 0.7f, 0.3f, 0.55f);
        public static readonly Color WheelDim = new Color(0f, 0f, 0f, 0.45f);
        public static readonly Color Text = new Color(0.95f, 0.95f, 0.98f, 1f);
        public static readonly Color TextMuted = new Color(0.7f, 0.7f, 0.76f, 1f);
        public static readonly Color TextActive = new Color(1f, 0.82f, 0.3f, 1f);

        /// <summary>The icon of a slot whose item is not carried any more.</summary>
        public static readonly Color IconSpent = new Color(1f, 1f, 1f, 0.35f);

        public static readonly Color IconNormal = Color.white;
    }
}
