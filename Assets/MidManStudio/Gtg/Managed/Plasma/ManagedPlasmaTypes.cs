// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/plasma.md, section "ManagedPlasmaTypes.cs"
// ============================================================================

using System;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Plasma
{
    /// <summary>The color ramps. The values are the row numbers in the ramp texture, so keep them 0, 1, 2.</summary>
    public enum ManagedPlasmaPalette
    {
        Fire = 0,
        Water = 1,
        Arcane = 2,
    }

    public static class ManagedPlasmaLimits
    {
        public const int PaletteCount = 3;

        /// <summary>Ring radius and band width are fractions of half the quad's width.</summary>
        public const float MinRingRadius = 0.15f;

        public const float MaxRingRadius = 0.85f;
        public const float MinBand = 0.03f;
        public const float MaxBand = 0.25f;

        /// <summary>Seeds are folded into this many values, so the shader's noise coordinates stay small.</summary>
        public const int SeedModulus = 251;
    }

    /// <summary>
    /// Everything that decides how one plasma ring looks, apart from when it is drawn: <see cref="Seed"/> picks the
    /// pattern, the others shape it. The same spec drawn at the same age always gives the same picture. A plain
    /// serializable class with no Unity objects, so the CI harness can use it.
    /// </summary>
    [Serializable]
    public sealed class ManagedPlasmaRingSpec
    {
        public int Seed = 1;

        public ManagedPlasmaPalette Palette = ManagedPlasmaPalette.Water;

        /// <summary>Radius of the ring, as a fraction of half the drawn quad. 0.55 leaves room for the wisps.</summary>
        public float RingRadius = 0.55f;

        /// <summary>Width of the bright band, as a fraction of half the drawn quad.</summary>
        public float BandWidth = 0.11f;

        /// <summary>0 is a round ring, 1 is a lumpy cloud edge.</summary>
        public float Turbulence = 0.7f;

        /// <summary>How much the wisps reach outward, 0 to 1.</summary>
        public float Streaks = 0.8f;

        public ManagedPlasmaRingSpec Clone()
        {
            return new ManagedPlasmaRingSpec
            {
                Seed = Seed,
                Palette = Palette,
                RingRadius = RingRadius,
                BandWidth = BandWidth,
                Turbulence = Turbulence,
                Streaks = Streaks,
            };
        }

        /// <summary>The seed as the shader sees it: folded into 0 to 250 and scaled, so a huge seed cannot push the noise out of float range.</summary>
        public float SeedValue
        {
            get
            {
                int folded = Seed % ManagedPlasmaLimits.SeedModulus;
                if (folded < 0)
                {
                    folded += ManagedPlasmaLimits.SeedModulus;
                }

                return folded * 0.37f;
            }
        }

        /// <summary>Where this palette's row sits in the ramp texture, as a v coordinate: the middle of its row.</summary>
        public float PaletteRow
        {
            get { return RowFor(Palette); }
        }

        public static float RowFor(ManagedPlasmaPalette palette)
        {
            int index = (int)palette;
            if (index < 0 || index >= ManagedPlasmaLimits.PaletteCount)
            {
                index = 0;
            }

            return (index + 0.5f) / ManagedPlasmaLimits.PaletteCount;
        }

        /// <summary>
        /// The shader's _ParamA: age in seconds, fade 0 to 1, ring radius and band width. Out of range values are clamped
        /// and a number that is not a number becomes the default, so one bad value cannot blank a ring.
        /// </summary>
        public Vector4 PackA(float age, float fade)
        {
            return new Vector4(
                Mathf.Max(0f, Safe(age, 0f)),
                Mathf.Clamp01(Safe(fade, 1f)),
                Mathf.Clamp(Safe(RingRadius, 0.55f), ManagedPlasmaLimits.MinRingRadius, ManagedPlasmaLimits.MaxRingRadius),
                Mathf.Clamp(Safe(BandWidth, 0.11f), ManagedPlasmaLimits.MinBand, ManagedPlasmaLimits.MaxBand));
        }

        /// <summary>The shader's _ParamB: seed value, palette row, turbulence and streaks.</summary>
        public Vector4 PackB()
        {
            return new Vector4(
                SeedValue,
                PaletteRow,
                Mathf.Clamp01(Safe(Turbulence, 0.7f)),
                Mathf.Clamp01(Safe(Streaks, 0.8f)));
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
