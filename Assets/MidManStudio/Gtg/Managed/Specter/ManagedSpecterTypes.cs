// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/specter.md, section "ManagedSpecterTypes.cs"
// ============================================================================

using System;
using MidManStudio.Gtg.Managed.Plasma;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Specter
{
    /// <summary>Limits of the shader's per-butterfly numbers. Values outside them are clamped when they are packed.</summary>
    public static class ManagedSpecterLimits
    {
        public const float MinWobble = 0f;
        public const float MaxWobble = 0.6f;
        public const float MinGlow = 0.1f;
        public const float MaxGlow = 4f;
        public const float MinChurn = 0f;
        public const float MaxChurn = 3f;
        public const float MinReach = 0.5f;
        public const float MaxReach = 3f;
    }

    /// <summary>
    /// Everything that decides how one specter butterfly looks, apart from how old it is: <see cref="Seed"/> picks the
    /// pattern, the others shape it. The palette is one of the plasma palettes (Fire, Water, Arcane), so a spell can give its
    /// plasma ring and its butterflies the same colors. A plain serializable class with no Unity objects, so the CI harness can use it.
    /// </summary>
    [Serializable]
    public sealed class ManagedSpecterSpec
    {
        public int Seed = 1;

        public ManagedPlasmaPalette Palette = ManagedPlasmaPalette.Arcane;

        /// <summary>How much the noise bends the shape, 0 to 0.6. 0 is a smooth round glow, higher is more wing-like rays. The tutorial used 0.17.</summary>
        public float Wobble = 0.3f;

        /// <summary>Brightness multiplier of the falloff, 0.1 to 4.</summary>
        public float Glow = 1f;

        /// <summary>How fast the pattern changes as the butterfly ages, 0 to 3. 0 freezes it.</summary>
        public float Churn = 1f;

        /// <summary>Shrinks the shape inside the quad when above 1 and grows it when below, 0.5 to 3.</summary>
        public float Reach = 1f;

        public ManagedSpecterSpec Clone()
        {
            return new ManagedSpecterSpec
            {
                Seed = Seed,
                Palette = Palette,
                Wobble = Wobble,
                Glow = Glow,
                Churn = Churn,
                Reach = Reach,
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

        /// <summary>
        /// The shader's _ParamA: life 0 to 1, fade 0 to 1, seed value and palette row. A number that is not a number becomes
        /// the default (life 0, fade 1), so one bad value cannot blank a butterfly.
        /// </summary>
        public Vector4 PackA(float life, float fade)
        {
            return new Vector4(
                Mathf.Clamp01(Safe(life, 0f)),
                Mathf.Clamp01(Safe(fade, 1f)),
                SeedValue,
                ManagedPlasmaRingSpec.RowFor(Palette));
        }

        /// <summary>The shader's _ParamB: wobble, glow, churn and reach, each clamped to its limits.</summary>
        public Vector4 PackB()
        {
            return new Vector4(
                Mathf.Clamp(Safe(Wobble, 0.3f), ManagedSpecterLimits.MinWobble, ManagedSpecterLimits.MaxWobble),
                Mathf.Clamp(Safe(Glow, 1f), ManagedSpecterLimits.MinGlow, ManagedSpecterLimits.MaxGlow),
                Mathf.Clamp(Safe(Churn, 1f), ManagedSpecterLimits.MinChurn, ManagedSpecterLimits.MaxChurn),
                Mathf.Clamp(Safe(Reach, 1f), ManagedSpecterLimits.MinReach, ManagedSpecterLimits.MaxReach));
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    /// <summary>
    /// The wing flap both motion variants share. A flap is the quad squashed sideways and opened again: width scale 1 is wings
    /// open, <c>min</c> is wings nearly closed.
    /// </summary>
    public static class ManagedSpecterFlap
    {
        /// <summary>The smallest width scale used when the caller gives one that is not a number.</summary>
        public const float DefaultMin = 0.35f;

        /// <summary>Width scale for a flap phase in cycles (1.0 is one full open and close). Always between min and 1.</summary>
        public static float WidthScale(float phaseCycles, float min)
        {
            if (float.IsNaN(min) || float.IsInfinity(min))
            {
                min = DefaultMin;
            }

            min = Mathf.Clamp01(min);
            if (float.IsNaN(phaseCycles) || float.IsInfinity(phaseCycles))
            {
                return 1f;
            }

            float open = 0.5f + 0.5f * Mathf.Cos(phaseCycles * 6.2831853f);
            return min + (1f - min) * open;
        }

        /// <summary>
        /// Key times (0 to 1) and values for a curve that flaps <paramref name="cycles"/> times over a particle's life, for the
        /// Particle System's size over lifetime. Eight keys per cycle keep the shape smooth. Cycles under 1 count as 1 and
        /// over 40 as 40.
        /// </summary>
        public static void BuildCurveKeys(float cycles, float min, out float[] times, out float[] values)
        {
            BuildCurveKeys(cycles, min, 0f, out times, out values);
        }

        /// <summary>
        /// The same curve starting part way through a flap: <paramref name="phaseOffsetCycles"/> of 0.25 starts a quarter flap in.
        /// Two such curves with different offsets, given to the Particle System as a random pair, make the butterflies flap out of step.
        /// </summary>
        public static void BuildCurveKeys(float cycles, float min, float phaseOffsetCycles, out float[] times, out float[] values)
        {
            float safe = float.IsNaN(cycles) ? 1f : Mathf.Clamp(cycles, 1f, 40f);
            float offset = float.IsNaN(phaseOffsetCycles) || float.IsInfinity(phaseOffsetCycles) ? 0f : phaseOffsetCycles;
            int whole = Mathf.RoundToInt(safe);
            int count = whole * 8 + 1;
            times = new float[count];
            values = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                times[i] = t;
                values[i] = WidthScale(t * whole + offset, min);
            }
        }
    }

    /// <summary>The fade in and out of a butterfly, shared by the flock and the preview.</summary>
    public static class ManagedSpecterFade
    {
        /// <summary>
        /// Fade 0 to 1 for a butterfly <paramref name="ageSeconds"/> old at <paramref name="life"/> (0 to 1). It rises over
        /// <paramref name="fadeInSeconds"/> (0 or less means no fade in) and falls smoothly over the last
        /// <paramref name="fadeOutFraction"/> of the life. A number that is not a number gives 1 for the part it touches.
        /// </summary>
        public static float At(float ageSeconds, float life, float fadeInSeconds, float fadeOutFraction)
        {
            float fadeIn = 1f;
            if (fadeInSeconds > 0f && !float.IsNaN(ageSeconds) && !float.IsNaN(fadeInSeconds))
            {
                fadeIn = Mathf.Clamp01(ageSeconds / fadeInSeconds);
            }

            float fadeOut = 1f;
            if (!float.IsNaN(life) && !float.IsNaN(fadeOutFraction))
            {
                float span = Mathf.Clamp(fadeOutFraction, 0.0001f, 1f);
                float t = Mathf.Clamp01((life - (1f - span)) / span);
                fadeOut = 1f - t * t * (3f - 2f * t);
            }

            return fadeIn * fadeOut;
        }
    }
}
