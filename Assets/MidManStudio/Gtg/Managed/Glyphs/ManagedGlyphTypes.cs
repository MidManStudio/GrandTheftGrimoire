// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphTypes.cs"
// ============================================================================

using System;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Glyphs
{
    /// <summary>Fixed limits of the glyph system.</summary>
    public static class ManagedGlyphLimits
    {
        /// <summary>Deepest nesting a glyph can have. Level 0 is the root ring, so a glyph has up to 8 levels.</summary>
        public const int MaxDepth = 7;

        public const int MaxWheels = 3;

        public const int MaxPaletteColors = 4;
    }

    /// <summary>Detail level of a baked glyph. Near is the full glyph, Far is rings and lines only.</summary>
    public enum ManagedGlyphLod
    {
        Near = 0,
        Mid = 1,
        Far = 2,
    }

    /// <summary>The overall look of a glyph. Circle is nested rings joined by lines. Molecule is a chemistry diagram: rings of bonds, chains, element letters.</summary>
    public enum ManagedGlyphStyle
    {
        Circle = 0,
        Molecule = 1,
    }

    /// <summary>How a Molecule glyph draws its atoms: element letters as in a skeletal formula, or glowing balls as in a ball-and-stick picture.</summary>
    public enum ManagedGlyphAtomLook
    {
        Letters = 0,
        Balls = 1,
    }

    /// <summary>
    /// The extra figures a node may carry inside or beside its rings. Rosette, Star, Nest, Curl and Drip belong to the
    /// Circle style. Label belongs to the Molecule style. Dot is used by both.
    /// </summary>
    [Flags]
    public enum ManagedGlyphOrnaments
    {
        None = 0,
        Rosette = 1,
        Star = 2,
        Nest = 4,
        Curl = 8,
        Drip = 16,
        Dot = 32,
        Label = 64,
        All = 127,
    }

    public enum ManagedGlyphStrokeKind : byte
    {
        Ring,
        Edge,
        Rosette,
        Chord,
        Nest,
        Curl,
        Drip,
        Dot,
        Bond,
        Letter,
        Atom,
    }

    /// <summary>One to four halo colors. A line takes the color of its level, so deeper rings change color.</summary>
    public sealed class ManagedGlyphPalette
    {
        public readonly Color[] Colors;

        public ManagedGlyphPalette(params Color[] colors)
        {
            if (colors == null || colors.Length == 0)
            {
                colors = new[] { Color.white };
            }

            int count = Mathf.Min(colors.Length, ManagedGlyphLimits.MaxPaletteColors);
            Colors = new Color[count];
            Array.Copy(colors, Colors, count);
        }

        public Color ColorFor(int index)
        {
            int i = index % Colors.Length;
            return Colors[i < 0 ? i + Colors.Length : i];
        }

        public static readonly ManagedGlyphPalette Fire = new ManagedGlyphPalette(
            new Color(1f, 0.45f, 0.08f, 1f), new Color(1f, 0.16f, 0.1f, 1f), new Color(1f, 0.85f, 0.2f, 1f));

        public static readonly ManagedGlyphPalette Water = new ManagedGlyphPalette(
            new Color(0.15f, 0.8f, 1f, 1f), new Color(0.2f, 0.4f, 1f, 1f), new Color(0.2f, 1f, 0.7f, 1f));

        public static readonly ManagedGlyphPalette Arcane = new ManagedGlyphPalette(
            new Color(1f, 0.2f, 0.7f, 1f), new Color(0.7f, 0.2f, 1f, 1f), new Color(1f, 0.45f, 0.9f, 1f));

        public static readonly ManagedGlyphPalette Prism = new ManagedGlyphPalette(
            new Color(0.25f, 0.55f, 1f, 1f), new Color(1f, 0.9f, 0.2f, 1f), new Color(0.35f, 1f, 0.35f, 1f), new Color(1f, 1f, 1f, 1f));
    }

    /// <summary>
    /// Everything that decides how a glyph looks. The same spec and LOD always bake the same mesh.
    /// Reveal is not part of the spec: it is a number per draw, 0 to 1, that grows the strokes.
    /// </summary>
    [Serializable]
    public sealed class ManagedGlyphSpec
    {
        public int Seed = 1;

        /// <summary>Nesting depth, 0 to 7. 0 is one node, 7 is the deepest.</summary>
        public int MaxDepth = 4;

        /// <summary>Levels already drawn at charge 0. -1 draws nothing until charging starts. 0 is the default ring.</summary>
        public int BaseDepth = 0;

        /// <summary>Radius of the root ring in local units.</summary>
        public float Radius = 1f;

        /// <summary>Outer half width of a level 0 line, as a fraction of Radius. The bright core is a third of it.</summary>
        public float LineWidth = 0.05f;

        /// <summary>Flat discs stacked along the glyph's local Z, 1 to 3. Each is its own recursive figure.</summary>
        public int WheelCount = 1;

        /// <summary>Gap between stacked wheels as a fraction of Radius.</summary>
        public float WheelSpacing = 0.12f;

        public ManagedGlyphOrnaments Ornaments = ManagedGlyphOrnaments.All;

        public ManagedGlyphStyle Style = ManagedGlyphStyle.Circle;

        /// <summary>Molecule style only: element letters or glowing balls.</summary>
        public ManagedGlyphAtomLook AtomLook = ManagedGlyphAtomLook.Letters;

        [NonSerialized] public ManagedGlyphPalette Palette = ManagedGlyphPalette.Fire;

        public int Depth { get { return Mathf.Clamp(MaxDepth, 0, ManagedGlyphLimits.MaxDepth); } }

        public int LevelCount { get { return Depth + 1; } }

        public int Wheels { get { return Mathf.Clamp(WheelCount, 1, ManagedGlyphLimits.MaxWheels); } }

        /// <summary>Reveal value at which exactly <paramref name="levels"/> levels are fully drawn.</summary>
        public float RevealForLevels(int levels)
        {
            return Mathf.Clamp01(levels / (float)LevelCount);
        }

        /// <summary>Reveal at charge 0: the default rings.</summary>
        public float BaseReveal
        {
            get { return RevealForLevels(Mathf.Clamp(BaseDepth, -1, Depth) + 1); }
        }

        /// <summary>Reveal for a charge of 0 to 1. Charge 1 is the whole glyph.</summary>
        public float RevealForCharge(float charge01)
        {
            return Mathf.Lerp(BaseReveal, 1f, Mathf.Clamp01(charge01));
        }

        /// <summary>How many levels have started drawing at this reveal.</summary>
        public int VisibleLevels(float reveal)
        {
            if (reveal <= 0f)
            {
                return 0;
            }

            return Mathf.Clamp(Mathf.CeilToInt(reveal * LevelCount - 0.0001f), 1, LevelCount);
        }

        /// <summary>Cache key of the baked mesh. Reveal and BaseDepth are left out because they do not change geometry.</summary>
        public long CacheKey(ManagedGlyphLod lod)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                h = Mix(h, (ulong)(uint)Seed);
                h = Mix(h, (ulong)Depth);
                h = Mix(h, (ulong)Wheels);
                h = Mix(h, (ulong)(uint)Mathf.RoundToInt(Radius * 10000f));
                h = Mix(h, (ulong)(uint)Mathf.RoundToInt(LineWidth * 100000f));
                h = Mix(h, (ulong)(uint)Mathf.RoundToInt(WheelSpacing * 10000f));
                h = Mix(h, (ulong)(uint)(int)Ornaments);
                h = Mix(h, (ulong)(int)Style);
                h = Mix(h, (ulong)(int)AtomLook);
                h = Mix(h, (ulong)(int)lod);
                ManagedGlyphPalette palette = Palette ?? ManagedGlyphPalette.Fire;
                for (int i = 0; i < palette.Colors.Length; i++)
                {
                    Color32 c = palette.Colors[i];
                    h = Mix(h, (ulong)((c.r << 24) | (c.g << 16) | (c.b << 8) | c.a));
                }

                return (long)h;
            }
        }

        private static ulong Mix(ulong h, ulong v)
        {
            unchecked
            {
                h ^= v;
                h *= 1099511628211UL;
                h ^= h >> 29;
                return h;
            }
        }
    }

    /// <summary>Per-LOD limits the generator and the renderer follow.</summary>
    public sealed class ManagedGlyphLodSettings
    {
        /// <summary>Deepest level drawn at this LOD.</summary>
        public int DepthCap;

        public int WheelCap;

        /// <summary>Segments of a ring of the full glyph radius. Smaller rings get proportionally fewer.</summary>
        public float SegmentsPerRadius;

        public int MinSegments;

        public int MaxSegments;

        /// <summary>Generation stops adding nodes once the strokes hold this many points.</summary>
        public int MaxPoints;

        /// <summary>Children smaller than this fraction of the glyph radius are not made.</summary>
        public float MinRadiusFraction;

        /// <summary>Deepest level that may carry a rosette, star or nest.</summary>
        public int OrnamentLevelCap;

        public ManagedGlyphOrnaments Allowed;

        private static readonly ManagedGlyphLodSettings NearSettings = new ManagedGlyphLodSettings
        {
            DepthCap = ManagedGlyphLimits.MaxDepth,
            WheelCap = ManagedGlyphLimits.MaxWheels,
            SegmentsPerRadius = 64f,
            MinSegments = 10,
            MaxSegments = 72,
            MaxPoints = 9000,
            MinRadiusFraction = 0.012f,
            OrnamentLevelCap = 3,
            Allowed = ManagedGlyphOrnaments.All,
        };

        private static readonly ManagedGlyphLodSettings MidSettings = new ManagedGlyphLodSettings
        {
            DepthCap = 4,
            WheelCap = 2,
            SegmentsPerRadius = 36f,
            MinSegments = 8,
            MaxSegments = 40,
            MaxPoints = 2400,
            MinRadiusFraction = 0.03f,
            OrnamentLevelCap = 1,
            Allowed = ManagedGlyphOrnaments.Rosette | ManagedGlyphOrnaments.Star | ManagedGlyphOrnaments.Curl | ManagedGlyphOrnaments.Dot
                | ManagedGlyphOrnaments.Label,
        };

        private static readonly ManagedGlyphLodSettings FarSettings = new ManagedGlyphLodSettings
        {
            DepthCap = 2,
            WheelCap = 1,
            SegmentsPerRadius = 18f,
            MinSegments = 6,
            MaxSegments = 20,
            MaxPoints = 600,
            MinRadiusFraction = 0.06f,
            OrnamentLevelCap = -1,
            Allowed = ManagedGlyphOrnaments.None,
        };

        public static ManagedGlyphLodSettings For(ManagedGlyphLod lod)
        {
            switch (lod)
            {
                case ManagedGlyphLod.Near:
                    return NearSettings;
                case ManagedGlyphLod.Mid:
                    return MidSettings;
                default:
                    return FarSettings;
            }
        }
    }

    /// <summary>One drawn line before it becomes a ribbon: a polyline in the glyph plane with its look and its reveal window.</summary>
    public sealed class ManagedGlyphStroke
    {
        public Vector2[] Points;

        /// <summary>Offset along the glyph's local Z. Stacked wheels differ here.</summary>
        public float Z;

        /// <summary>A closed ring wraps from the last point back to the first and has no end caps.</summary>
        public bool Closed;

        public int Level;

        public Color32 Tint;

        /// <summary>Outer half width of the ribbon in local units. The glow fills all of it, the core a third.</summary>
        public float HalfWidth;

        /// <summary>The stroke starts drawing at this reveal value and is whole at T1.</summary>
        public float T0;

        public float T1;

        public ManagedGlyphStrokeKind Kind;
    }

    /// <summary>What the generator returns: the strokes of one glyph at one LOD.</summary>
    public sealed class ManagedGlyphStrokeSet
    {
        public readonly System.Collections.Generic.List<ManagedGlyphStroke> Strokes = new System.Collections.Generic.List<ManagedGlyphStroke>();

        public int LevelCount;

        public int NodeCount;

        public int PointCount;

        /// <summary>True when the point budget stopped the generation before every level was made.</summary>
        public bool Truncated;

        public ManagedGlyphStrokeSet(int levelCount)
        {
            LevelCount = levelCount;
        }
    }
}
