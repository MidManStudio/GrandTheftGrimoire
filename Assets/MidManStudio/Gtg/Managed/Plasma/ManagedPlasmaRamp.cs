// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/plasma.md, section "ManagedPlasmaRamp.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Plasma
{
    /// <summary>
    /// The color ramp of each palette, the "Color Ramp" node of the reference graph. A ramp maps brightness (0 dark,
    /// 1 white hot) to a color. The renderer bakes all of them into one small texture, one row per palette, and the shader
    /// reads it with brightness as x and the palette row as y. Pure arithmetic, so the CI harness checks it.
    /// </summary>
    public static class ManagedPlasmaRamp
    {
        /// <summary>Texels across the ramp texture. Bilinear filtering smooths between them.</summary>
        public const int Width = 128;

        public struct Stop
        {
            public float At;
            public Color Color;

            public Stop(float at, float r, float g, float b)
            {
                At = at;
                Color = new Color(r, g, b, 1f);
            }
        }

        // Dark, deep, mid, bright and white hot. Black at 0, so an additive ring fades into whatever is behind it.
        private static readonly Stop[][] Ramps =
        {
            // Fire
            new[]
            {
                new Stop(0f, 0f, 0f, 0f), new Stop(0.18f, 0.35f, 0.04f, 0f), new Stop(0.45f, 0.95f, 0.25f, 0.03f),
                new Stop(0.75f, 1f, 0.65f, 0.15f), new Stop(1f, 1f, 0.96f, 0.8f),
            },

            // Water
            new[]
            {
                new Stop(0f, 0f, 0f, 0f), new Stop(0.18f, 0f, 0.08f, 0.4f), new Stop(0.45f, 0.05f, 0.42f, 0.95f),
                new Stop(0.75f, 0.35f, 0.85f, 1f), new Stop(1f, 0.9f, 1f, 1f),
            },

            // Arcane
            new[]
            {
                new Stop(0f, 0f, 0f, 0f), new Stop(0.18f, 0.25f, 0.02f, 0.4f), new Stop(0.45f, 0.75f, 0.15f, 0.85f),
                new Stop(0.75f, 1f, 0.5f, 0.95f), new Stop(1f, 1f, 0.9f, 1f),
            },
        };

        /// <summary>The stops of a palette, in order of brightness. An unknown palette gives Fire's.</summary>
        public static Stop[] StopsFor(ManagedPlasmaPalette palette)
        {
            int index = (int)palette;
            if (index < 0 || index >= Ramps.Length)
            {
                index = 0;
            }

            return Ramps[index];
        }

        /// <summary>The color at a brightness, 0 to 1 (clamped), by straight blending between the neighbouring stops.</summary>
        public static Color Evaluate(ManagedPlasmaPalette palette, float brightness)
        {
            Stop[] stops = StopsFor(palette);
            float v = float.IsNaN(brightness) ? 0f : Mathf.Clamp01(brightness);
            if (v <= stops[0].At)
            {
                return stops[0].Color;
            }

            for (int i = 1; i < stops.Length; i++)
            {
                if (v <= stops[i].At)
                {
                    float span = stops[i].At - stops[i - 1].At;
                    float t = span > 0f ? (v - stops[i - 1].At) / span : 1f;
                    return Color.Lerp(stops[i - 1].Color, stops[i].Color, t);
                }
            }

            return stops[stops.Length - 1].Color;
        }

        /// <summary>
        /// Fills <paramref name="pixels"/> (Width x PaletteCount, row by row, row 0 first) with every palette's ramp. Row r is the
        /// palette whose enum value is r, which is the row ManagedPlasmaRingSpec.RowFor points at.
        /// </summary>
        public static void Bake(Color32[] pixels)
        {
            int rows = ManagedPlasmaLimits.PaletteCount;
            for (int row = 0; row < rows; row++)
            {
                for (int x = 0; x < Width; x++)
                {
                    // Texel centers run from 0 to 1 across the row, so the last texel is the last stop.
                    float v = x / (float)(Width - 1);
                    Color c = Evaluate((ManagedPlasmaPalette)row, v);
                    pixels[row * Width + x] = new Color32(ToByte(c.r), ToByte(c.g), ToByte(c.b), 255);
                }
            }
        }

        /// <summary>A new pixel array of the right size, baked.</summary>
        public static Color32[] Bake()
        {
            Color32[] pixels = new Color32[Width * ManagedPlasmaLimits.PaletteCount];
            Bake(pixels);
            return pixels;
        }

        private static byte ToByte(float value)
        {
            return (byte)(Mathf.Clamp01(value) * 255f + 0.5f);
        }
    }
}
