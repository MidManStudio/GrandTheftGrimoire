// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphSpellTable.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Magic;

namespace MidManStudio.Gtg.Managed.Glyphs
{
    /// <summary>
    /// Which glyph each spell draws while it winds up. The table lives in code for now and moves to an mdix table later,
    /// like the spell table. Each spell has one fixed glyph, so the same spell always draws the same figure, and the
    /// renderer bakes it once and reuses it for every cast.
    /// This is the one place that connects the glyph system to the spell system. Magic does not know about glyphs.
    /// </summary>
    public static class ManagedGlyphSpellTable
    {
        // Built once and shared. Callers read them and never change them, because the renderer caches meshes by value.
        private static readonly ManagedGlyphSpec Fireball = new ManagedGlyphSpec
        {
            Style = ManagedGlyphStyle.Circle,
            Seed = 1,
            MaxDepth = 4,
            BaseDepth = 0,
            Radius = 0.22f,
            LineWidth = 0.05f,
            WheelCount = 1,
            Palette = ManagedGlyphPalette.Fire,
        };

        // A molecule grows about twice as wide as a circle for the same radius, so its radius is smaller.
        private static readonly ManagedGlyphSpec Ice = new ManagedGlyphSpec
        {
            Style = ManagedGlyphStyle.Molecule,
            AtomLook = ManagedGlyphAtomLook.Letters,
            Seed = 4,
            MaxDepth = 4,
            BaseDepth = 0,
            Radius = 0.13f,
            LineWidth = 0.07f,
            WheelCount = 1,
            Palette = ManagedGlyphPalette.Water,
        };

        private static readonly ManagedGlyphSpec Seeker = new ManagedGlyphSpec
        {
            Style = ManagedGlyphStyle.Circle,
            Seed = 3,
            MaxDepth = 3,
            BaseDepth = 0,
            Radius = 0.22f,
            LineWidth = 0.05f,
            WheelCount = 2,
            Palette = ManagedGlyphPalette.Arcane,
        };

        /// <summary>The glyph a spell draws. A spell with no entry gets the Fireball glyph. Never null.</summary>
        public static ManagedGlyphSpec For(ManagedSpellKind kind)
        {
            switch (kind)
            {
                case ManagedSpellKind.Ice:
                    return Ice;
                case ManagedSpellKind.Seeker:
                    return Seeker;
                default:
                    return Fireball;
            }
        }
    }
}
