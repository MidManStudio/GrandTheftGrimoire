// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphCastTracker.cs"
// ============================================================================

using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Magic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Glyphs
{
    /// <summary>
    /// Turns the cast status of a caster into the glyphs to draw this frame. While a wind-up runs there is one glyph in
    /// front of the shot point, drawing on as the charge rises. When the wind-up ends, the glyph finishes drawing, grows a little
    /// and fades out, and keeps doing so while the next cast starts. All plain values: no Unity objects, so the CI harness drives it
    /// frame by frame. The view component feeds it the status and sends what it returns to the renderer.
    /// </summary>
    public sealed class ManagedGlyphCastTracker
    {
        /// <summary>One glyph to draw this frame, in world space. Forward is the glyph's local Z.</summary>
        public struct Item
        {
            public ManagedGlyphSpec Spec;
            public Vector3 Position;
            public Vector3 Forward;

            /// <summary>Degrees of roll around Forward.</summary>
            public float Spin;

            public float Scale;
            public float Reveal;
            public float Fade;
        }

        private const int MaxFading = 4;

        private struct Fading
        {
            public Item Start;
            public float Elapsed;
        }

        /// <summary>Metres in front of the shot point, along the aim.</summary>
        public float ForwardDistance = 0.6f;

        /// <summary>Multiplies the size of every glyph.</summary>
        public float SizeScale = 1f;

        public float SpinDegreesPerSecond = 18f;

        /// <summary>How long a glyph takes to fade out once the shot has left.</summary>
        public float FadeSeconds = 0.35f;

        /// <summary>How much larger a fading glyph grows by the end, as a fraction of its size.</summary>
        public float BurstGrowth = 0.25f;

        private readonly Fading[] _fading = new Fading[MaxFading];
        private int _fadingCount;
        private bool _wasActive;
        private Item _last;
        private float _spin;

        /// <summary>Glyphs fading out right now.</summary>
        public int FadingCount { get { return _fadingCount; } }

        /// <summary>True while a wind-up is being followed.</summary>
        public bool IsActive { get { return _wasActive; } }

        /// <summary>
        /// Advances by dt and fills <paramref name="output"/> with this frame's glyphs: the one winding up first, then the
        /// fading ones. Returns how many. The list is cleared first.
        /// </summary>
        public int Update(ManagedCastStatus status, float dt, List<Item> output)
        {
            output.Clear();
            dt = Mathf.Max(0f, dt);

            if (status.Active)
            {
                if (!_wasActive)
                {
                    _spin = 0f;
                }

                _spin += SpinDegreesPerSecond * dt;
                ManagedGlyphSpec spec = ManagedGlyphSpellTable.For(status.Kind);
                _last = new Item
                {
                    Spec = spec,
                    Position = status.Origin + status.Direction * ForwardDistance,
                    Forward = status.Direction,
                    Spin = _spin,
                    Scale = SizeScale,
                    Reveal = spec.RevealForCharge(status.Charge),
                    Fade = 1f,
                };

                _wasActive = true;
                output.Add(_last);
            }
            else if (_wasActive)
            {
                // The wind-up ended: the glyph finishes drawing and fades out where it was.
                _wasActive = false;
                StartFade();
            }

            AdvanceFading(dt, output);
            return output.Count;
        }

        /// <summary>Drops every glyph at once, for when the caster is switched off.</summary>
        public void Clear()
        {
            _fadingCount = 0;
            _wasActive = false;
            _spin = 0f;
        }

        private void StartFade()
        {
            if (FadeSeconds <= 0f)
            {
                return;
            }

            // A full list loses its oldest glyph.
            if (_fadingCount == MaxFading)
            {
                for (int i = 1; i < MaxFading; i++)
                {
                    _fading[i - 1] = _fading[i];
                }

                _fadingCount--;
            }

            Item start = _last;
            start.Reveal = 1f;
            _fading[_fadingCount++] = new Fading { Start = start, Elapsed = 0f };
        }

        private void AdvanceFading(float dt, List<Item> output)
        {
            int keep = 0;
            for (int i = 0; i < _fadingCount; i++)
            {
                Fading f = _fading[i];
                f.Elapsed += dt;
                float t = Mathf.Clamp01(f.Elapsed / Mathf.Max(0.0001f, FadeSeconds));
                if (t >= 1f)
                {
                    continue;
                }

                Item item = f.Start;
                item.Fade = 1f - t;
                item.Scale = f.Start.Scale * (1f + BurstGrowth * t);
                item.Spin = f.Start.Spin + SpinDegreesPerSecond * 3f * f.Elapsed;
                output.Add(item);
                _fading[keep++] = f;
            }

            _fadingCount = keep;
        }
    }
}
