// Checks for the spell wind-up timer, the glyph spell table and the glyph cast tracker. Verification harness only.
// The caster itself, the view component and the look need Unity.
using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Glyphs;
using MidManStudio.Gtg.Managed.Magic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        private static ManagedCastStatus CastStatus(ManagedSpellKind kind, float charge, Vector3 origin, Vector3 direction)
        {
            return new ManagedCastStatus { Active = true, Kind = kind, Charge = charge, Seconds = 0.5f, Origin = origin, Direction = direction };
        }

        private static void GlyphCastingTests()
        {
            SpellWindupTests();
            GlyphSpellTableTests();
            GlyphCastTrackerTests();
        }

        private static void SpellWindupTests()
        {
            Section("Spell windup");
            ManagedSpellWindup windup = new ManagedSpellWindup();
            Check(!windup.Active, "a new wind-up is idle");
            Near(windup.Charge, 0f, "with no charge");
            Check(!windup.Tick(1f), "ticking an idle wind-up does nothing");

            ManagedSpellDefinition fireball = ManagedSpellDefinition.Fireball;
            ManagedVesselDraw draw = new ManagedVesselDraw { Sp = 80f, Capacity = 100f, Rating = 10f };
            windup.Begin(fireball, draw, 0f);
            Check(!windup.Active, "a length of zero is ignored, so a spell with no cast time fires at once");
            windup.Begin(null, draw, 0.5f);
            Check(!windup.Active, "and so is a missing spell");

            windup.Begin(fireball, draw, 0.5f);
            Check(windup.Active, "Begin starts it");
            Check(windup.Spell == fireball, "it remembers the spell");
            Near(windup.Draw.Sp, 80f, "and what the vessel gave up");
            Near(windup.Duration, 0.5f, "and its length");
            Near(windup.Charge, 0f, "the charge starts at 0");

            Check(!windup.Tick(0.2f), "0.2 s in, not done");
            Near(windup.Charge, 0.4f, "charge 0.4");
            Check(!windup.Tick(-5f), "a negative step is ignored");
            Near(windup.Charge, 0.4f, "and changes nothing");
            Check(!windup.Tick(0.2f), "0.4 s in, not done");
            Near(windup.Charge, 0.8f, "charge 0.8");
            Check(windup.Tick(0.2f), "0.6 s in, done");
            Near(windup.Charge, 1f, "the charge stops at 1");
            Check(windup.Active, "it stays active until the caster clears it");
            Check(windup.Spell == fireball && windup.Draw.Capacity == 100f, "the caster can still read what it needs to fire");

            windup.Clear();
            Check(!windup.Active && windup.Spell == null, "Clear ends it");
            Near(windup.Charge, 0f, "and zeroes the charge");
            Check(!windup.Tick(1f), "a cleared wind-up does not tick");

            // Restart after a clear.
            windup.Begin(ManagedSpellDefinition.Ice, draw, 0.3f);
            Check(windup.Tick(0.3f), "exactly the length is done");

            // The spell table.
            Check(ManagedSpellDefinition.Fireball.CastSeconds > 0f, "fireball winds up");
            Check(ManagedSpellDefinition.Ice.CastSeconds > 0f, "ice winds up");
            Check(ManagedSpellDefinition.Seeker.CastSeconds > 0f, "the seeker winds up");
            Near(ManagedSpellDefinition.Fireball.CastSeconds, 0.5f, "fireball takes half a second");
            Near(new ManagedSpellDefinition().CastSeconds, 0f, "a new spell fires at once unless it is given a cast time");

            ManagedCastStatus none = default(ManagedCastStatus);
            Check(!none.Active, "a default cast status is inactive");
        }

        private static void GlyphSpellTableTests()
        {
            Section("Glyph spell table");
            ManagedGlyphSpec fire = ManagedGlyphSpellTable.For(ManagedSpellKind.Fireball);
            ManagedGlyphSpec ice = ManagedGlyphSpellTable.For(ManagedSpellKind.Ice);
            ManagedGlyphSpec seeker = ManagedGlyphSpellTable.For(ManagedSpellKind.Seeker);

            Eq(fire.Style, ManagedGlyphStyle.Circle, "fireball is a circle");
            Check(fire.Palette == ManagedGlyphPalette.Fire, "in fire colors");
            Eq(ice.Style, ManagedGlyphStyle.Molecule, "ice is a molecule");
            Check(ice.Palette == ManagedGlyphPalette.Water, "in water colors");
            Eq(ice.AtomLook, ManagedGlyphAtomLook.Letters, "with element letters");
            Eq(seeker.Style, ManagedGlyphStyle.Circle, "the seeker is a circle");
            Check(seeker.Palette == ManagedGlyphPalette.Arcane, "in arcane colors");
            Eq(seeker.Wheels, 2, "with two wheels");

            Check(ManagedGlyphSpellTable.For(ManagedSpellKind.Ice) == ice, "the same spec comes back every time, so the renderer bakes it once");
            Check(ManagedGlyphSpellTable.For((ManagedSpellKind)99) == fire, "a spell with no entry gets the fireball glyph");

            ManagedGlyphSpec[] all = { fire, ice, seeker };
            for (int i = 0; i < all.Length; i++)
            {
                ManagedGlyphSpec spec = all[i];
                Check(spec.Depth >= 1 && spec.Depth <= ManagedGlyphLimits.MaxDepth, "glyph " + i + " has a depth in range");
                Check(spec.BaseReveal > 0f && spec.BaseReveal < 1f, "glyph " + i + " shows its default ring at the press and is not whole yet");
                Near(spec.RevealForCharge(1f), 1f, "glyph " + i + " is whole at full charge");

                ManagedGlyphStrokeSet set = ManagedGlyphGenerator.Generate(spec, ManagedGlyphLod.Near);
                Check(set.Strokes.Count > 0, "glyph " + i + " generates");
                float far = 0f;
                for (int s = 0; s < set.Strokes.Count; s++)
                {
                    for (int k = 0; k < set.Strokes[s].Points.Length; k++)
                    {
                        far = Mathf.Max(far, set.Strokes[s].Points[k].magnitude);
                    }
                }

                Check(far > 0.3f && far < 1.6f, "glyph " + i + " is between 0.3 and 1.6 m from the center (" + far.ToString("F2") + " m)");
            }

            Check(fire.CacheKey(ManagedGlyphLod.Near) != ice.CacheKey(ManagedGlyphLod.Near) && ice.CacheKey(ManagedGlyphLod.Near) != seeker.CacheKey(ManagedGlyphLod.Near),
                "the three spells bake three different meshes");
        }

        private static void GlyphCastTrackerTests()
        {
            Section("Glyph cast tracker");
            ManagedGlyphCastTracker tracker = new ManagedGlyphCastTracker();
            List<ManagedGlyphCastTracker.Item> items = new List<ManagedGlyphCastTracker.Item>();
            Vector3 origin = new Vector3(1f, 2f, 3f);
            Vector3 dir = new Vector3(0f, 0f, 1f);

            Eq(tracker.Update(default(ManagedCastStatus), 0.016f, items), 0, "nothing draws with no cast");
            Check(!tracker.IsActive, "and the tracker is idle");

            ManagedGlyphSpec fire = ManagedGlyphSpellTable.For(ManagedSpellKind.Fireball);
            Eq(tracker.Update(CastStatus(ManagedSpellKind.Fireball, 0.5f, origin, dir), 0.1f, items), 1, "a wind-up draws one glyph");
            ManagedGlyphCastTracker.Item item = items[0];
            Check(item.Spec == fire, "it is the spell's glyph");
            Near(item.Position.x, 1f, "it sits at the shot point (x)");
            Near(item.Position.y, 2f, "(y)");
            Near(item.Position.z, 3f + tracker.ForwardDistance, "and in front of it along the aim (z)");
            Near(item.Forward.z, 1f, "facing the aim");
            Near(item.Reveal, fire.RevealForCharge(0.5f), "the reveal follows the charge");
            Near(item.Fade, 1f, "fully visible");
            Near(item.Scale, 1f, "at normal size");
            Check(tracker.IsActive, "the tracker follows a wind-up");

            // The reveal grows with the charge.
            float previous = -1f;
            bool rising = true;
            for (int i = 0; i <= 10; i++)
            {
                tracker.Update(CastStatus(ManagedSpellKind.Fireball, i / 10f, origin, dir), 0.016f, items);
                rising &= items[0].Reveal >= previous;
                previous = items[0].Reveal;
            }

            Check(rising, "the reveal never goes down while charging");
            Near(items[0].Reveal, 1f, "and is whole at full charge");
            tracker.Update(CastStatus(ManagedSpellKind.Fireball, 0f, origin, dir), 0.016f, items);
            Near(items[0].Reveal, fire.BaseReveal, "at charge 0 only the default ring shows");

            // Size and distance settings.
            tracker.SizeScale = 2f;
            tracker.ForwardDistance = 1f;
            tracker.Update(CastStatus(ManagedSpellKind.Fireball, 1f, origin, dir), 0.016f, items);
            Near(items[0].Scale, 2f, "the size scale is applied");
            Near(items[0].Position.z, 4f, "and so is the forward distance");
            tracker.SizeScale = 1f;
            tracker.ForwardDistance = 0.6f;

            // Spin: it grows while a wind-up runs and starts again at the next one.
            tracker.Clear();
            tracker.Update(CastStatus(ManagedSpellKind.Fireball, 0.1f, origin, dir), 0.5f, items);
            float spin1 = items[0].Spin;
            tracker.Update(CastStatus(ManagedSpellKind.Fireball, 0.2f, origin, dir), 0.5f, items);
            Check(items[0].Spin > spin1, "the glyph spins while it winds up");
            Near(items[0].Spin, tracker.SpinDegreesPerSecond * 1f, "at the set speed");

            // The shot leaves: the glyph finishes drawing, grows and fades, then is gone.
            tracker.Clear();
            tracker.Update(CastStatus(ManagedSpellKind.Fireball, 0.9f, origin, dir), 0.016f, items);
            int count = tracker.Update(default(ManagedCastStatus), 0.1f, items);
            Eq(count, 1, "after the shot leaves a fading glyph remains");
            Eq(tracker.FadingCount, 1, "and the tracker counts it");
            Check(!tracker.IsActive, "the wind-up is over");
            Near(items[0].Reveal, 1f, "it finishes drawing");
            Near(items[0].Fade, 1f - 0.1f / tracker.FadeSeconds, "it fades with time");
            Check(items[0].Scale > 1f, "and grows");
            Near(items[0].Position.z, 3f + tracker.ForwardDistance, "where the wind-up was");
            Check(items[0].Spec == fire, "it is still the same glyph");
            tracker.Update(default(ManagedCastStatus), 0.1f, items);
            Check(items[0].Fade < 1f - 0.1f / tracker.FadeSeconds, "the fade goes on");
            count = tracker.Update(default(ManagedCastStatus), 0.5f, items);
            Eq(count, 0, "after the fade time it is gone");
            Eq(tracker.FadingCount, 0, "and uncounted");

            // A new cast during a fade: the winding glyph first, then the fading one.
            tracker.Clear();
            tracker.Update(CastStatus(ManagedSpellKind.Fireball, 1f, origin, dir), 0.016f, items);
            tracker.Update(default(ManagedCastStatus), 0.05f, items);
            count = tracker.Update(CastStatus(ManagedSpellKind.Ice, 0.2f, origin, dir), 0.05f, items);
            Eq(count, 2, "a new cast and a fading glyph draw together");
            Check(items[0].Spec == ManagedGlyphSpellTable.For(ManagedSpellKind.Ice), "the winding glyph is first and is the ice glyph");
            Check(items[1].Spec == fire, "the fading one is the fireball glyph");
            Check(items[1].Fade < 1f && items[0].Fade >= 1f, "only the old one fades");

            // Only a few fading glyphs are kept.
            tracker.Clear();
            for (int i = 0; i < 8; i++)
            {
                tracker.Update(CastStatus(ManagedSpellKind.Fireball, 1f, origin, dir), 0.001f, items);
                tracker.Update(default(ManagedCastStatus), 0.001f, items);
            }

            Check(tracker.FadingCount <= 4 && tracker.FadingCount > 0, "the fading list is capped at four");

            // No fade time: the glyph just stops.
            tracker.Clear();
            tracker.FadeSeconds = 0f;
            tracker.Update(CastStatus(ManagedSpellKind.Fireball, 1f, origin, dir), 0.016f, items);
            Eq(tracker.Update(default(ManagedCastStatus), 0.016f, items), 0, "a fade time of zero removes the glyph at once");
            tracker.FadeSeconds = 0.35f;

            // A negative step is treated as zero.
            tracker.Clear();
            tracker.Update(CastStatus(ManagedSpellKind.Fireball, 0.5f, origin, dir), 0.2f, items);
            float spinBefore = items[0].Spin;
            tracker.Update(CastStatus(ManagedSpellKind.Fireball, 0.5f, origin, dir), -1f, items);
            Near(items[0].Spin, spinBefore, "a negative step does not turn the glyph back");

            tracker.Clear();
            Eq(tracker.Update(default(ManagedCastStatus), 0.016f, items), 0, "after Clear nothing draws");
            Eq(tracker.FadingCount, 0, "and nothing fades");
        }
    }
}
