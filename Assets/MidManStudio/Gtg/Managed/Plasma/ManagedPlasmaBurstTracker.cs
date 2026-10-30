// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/plasma.md, section "ManagedPlasmaBurstTracker.cs"
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Plasma
{
    /// <summary>
    /// Keeps the plasma bursts that are alive and says what each one looks like this frame. A burst starts as a small
    /// thick ring, expands fast and slows down, thins out, and fades. All plain values with no Unity objects, so the
    /// CI harness drives it frame by frame. The view component feeds it the impacts and sends what it returns to the renderer.
    /// </summary>
    public sealed class ManagedPlasmaBurstTracker
    {
        /// <summary>One ring to draw this frame, in world space. Normal is the direction the ring faces, or zero when unknown.</summary>
        public struct Item
        {
            /// <summary>Owned by the tracker and changed every frame. Draw it at once and do not keep it.</summary>
            public ManagedPlasmaRingSpec Spec;

            public Vector3 Position;
            public Vector3 Normal;

            /// <summary>Width of the drawn quad in metres.</summary>
            public float Diameter;

            /// <summary>The noise age in seconds. It runs faster than real time, so a short burst still churns.</summary>
            public float Age;

            public float Fade;
        }

        private sealed class Burst
        {
            public ManagedPlasmaRingSpec Spec;
            public float StreaksAtStart;
            public Vector3 Position;
            public Vector3 Normal;
            public float Diameter;
            public float Age;
            public float Duration;
        }

        /// <summary>The most bursts kept at once. A new one past this replaces the oldest.</summary>
        public const int HardMaxBursts = 32;

        private readonly List<Burst> _bursts = new List<Burst>();
        private int _serial;

        /// <summary>Most bursts alive at once, 1 to <see cref="HardMaxBursts"/>.</summary>
        public int MaxBursts = 8;

        /// <summary>Ring radius at the start and at the end, as fractions of half the quad.</summary>
        public float StartRadius = 0.2f;

        public float EndRadius = 0.7f;

        /// <summary>Band width at the start (thick) and at the end (thin), same unit.</summary>
        public float StartBand = 0.16f;

        public float EndBand = 0.05f;

        /// <summary>The share of the life after which the burst fades out. Before it the burst is fully visible.</summary>
        public float FadeStart = 0.55f;

        /// <summary>The share of the life the burst takes to fade in. It is short, so the hit reads at once.</summary>
        public float FadeInFraction = 0.06f;

        /// <summary>Multiplies the quad size. The ring's end radius is placed at the blast radius times this.</summary>
        public float SizeScale = 0.9f;

        /// <summary>The smallest quad, in metres, so a spell with a tiny blast still shows.</summary>
        public float MinDiameter = 1.5f;

        /// <summary>The largest quad, in metres, so a mistyped blast radius never covers the whole level.</summary>
        public float MaxDiameter = 40f;

        /// <summary>Noise seconds per real second.</summary>
        public float NoiseSpeed = 2f;

        /// <summary>Metres the ring is moved out along the surface normal, so it does not sit inside the wall it hit.</summary>
        public float SurfaceOffset = 0.08f;

        /// <summary>Bursts alive now.</summary>
        public int Count { get { return _bursts.Count; } }

        /// <summary>
        /// The quad width, in metres, a burst with this blast radius is drawn at, kept between MinDiameter and MaxDiameter.
        /// A blast radius that is not a number, is infinite, or is below zero is treated as zero, which gives the minimum.
        /// </summary>
        public float DiameterFor(float blastRadius)
        {
            if (float.IsNaN(blastRadius) || float.IsInfinity(blastRadius) || blastRadius < 0f)
            {
                blastRadius = 0f;
            }

            float end = Mathf.Max(0.05f, EndRadius);
            float wanted = 2f * blastRadius * SizeScale / end;
            if (float.IsNaN(wanted))
            {
                wanted = 0f;
            }

            return Mathf.Clamp(wanted, MinDiameter, Mathf.Max(MinDiameter, MaxDiameter));
        }

        /// <summary>
        /// Starts a burst. <paramref name="template"/> gives the palette, seed, turbulence and streaks and is not changed: the
        /// burst works on its own copy, with a seed that differs from every earlier burst so two hits never look the same.
        /// Returns false for no template, a length of zero or less, or a position that is not a number.
        /// </summary>
        public bool Add(ManagedPlasmaRingSpec template, Vector3 position, Vector3 normal, float blastRadius, float seconds)
        {
            if (template == null || seconds <= 0f || float.IsNaN(seconds) || !IsFinite(position))
            {
                return false;
            }

            int max = Mathf.Clamp(MaxBursts, 1, HardMaxBursts);
            while (_bursts.Count >= max)
            {
                _bursts.RemoveAt(0);
            }

            ManagedPlasmaRingSpec spec = template.Clone();
            spec.Seed = template.Seed + _serial;
            _serial++;

            Vector3 facing = IsFinite(normal) && normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.zero;
            _bursts.Add(new Burst
            {
                Spec = spec,
                StreaksAtStart = template.Streaks,
                Position = position + facing * SurfaceOffset,
                Normal = facing,
                Diameter = DiameterFor(blastRadius),
                Age = 0f,
                Duration = seconds,
            });
            return true;
        }

        /// <summary>
        /// Advances every burst by dt and fills <paramref name="output"/> with the ones still alive, oldest first. Returns how many.
        /// The list is cleared first. A burst whose time is up is removed in this call and not returned.
        /// </summary>
        public int Update(float dt, List<Item> output)
        {
            output.Clear();
            dt = Mathf.Max(0f, dt);

            int write = 0;
            for (int read = 0; read < _bursts.Count; read++)
            {
                Burst burst = _bursts[read];
                burst.Age += dt;
                float u = burst.Age / burst.Duration;
                if (u >= 1f)
                {
                    continue;
                }

                _bursts[write++] = burst;

                // Fast out, slow down: the radius follows an ease-out curve. The band thins evenly.
                float grown = 1f - (1f - u) * (1f - u) * (1f - u);
                burst.Spec.RingRadius = Mathf.Lerp(StartRadius, EndRadius, grown);
                burst.Spec.BandWidth = Mathf.Lerp(StartBand, EndBand, u);
                burst.Spec.Streaks = burst.StreaksAtStart * (1f - 0.4f * u);

                output.Add(new Item
                {
                    Spec = burst.Spec,
                    Position = burst.Position,
                    Normal = burst.Normal,
                    Diameter = burst.Diameter,
                    Age = burst.Age * NoiseSpeed,
                    Fade = FadeAt(u),
                });
            }

            _bursts.RemoveRange(write, _bursts.Count - write);
            return output.Count;
        }

        /// <summary>Drops every burst at once.</summary>
        public void Clear()
        {
            _bursts.Clear();
        }

        // 0 at the very start rising to 1, full until FadeStart, then a smooth fall to 0 at the end.
        private float FadeAt(float u)
        {
            float fadeIn = FadeInFraction > 0f ? Mathf.Clamp01(u / FadeInFraction) : 1f;
            float outSpan = Mathf.Max(0.0001f, 1f - FadeStart);
            float t = Mathf.Clamp01((u - FadeStart) / outSpan);
            float fadeOut = 1f - t * t * (3f - 2f * t);
            return fadeIn * fadeOut;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }
    }
}
