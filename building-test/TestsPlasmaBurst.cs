// Checks for the plasma impact burst: the tracker's timing and limits, the spell table, and the view that listens to spell impacts.
// The look of a burst is not checked here: it needs Unity.
using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Magic;
using MidManStudio.Gtg.Managed.Plasma;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        private static void PlasmaBurstTests()
        {
            PlasmaBurstTrackerTests();
            PlasmaBurstTrackerLimitTests();
            PlasmaSpellTableTests();
            PlasmaBurstViewTests();
        }

        private static void PlasmaBurstTrackerTests()
        {
            Section("Plasma burst tracker, one burst");
            ManagedPlasmaBurstTracker tracker = new ManagedPlasmaBurstTracker();
            List<ManagedPlasmaBurstTracker.Item> items = new List<ManagedPlasmaBurstTracker.Item>();
            Eq(tracker.Count, 0, "a new tracker is empty");
            Eq(tracker.Update(0.1f, items), 0, "updating an empty tracker returns 0");

            ManagedPlasmaRingSpec template = new ManagedPlasmaRingSpec { Seed = 11, Palette = ManagedPlasmaPalette.Fire, Turbulence = 0.8f, Streaks = 0.9f };
            Check(tracker.Add(template, new Vector3(1f, 2f, 3f), Vector3.up, 3f, 1f), "a burst is accepted");
            Eq(tracker.Count, 1, "one burst is alive");

            items.Add(default(ManagedPlasmaBurstTracker.Item));
            items.Add(default(ManagedPlasmaBurstTracker.Item));
            Eq(tracker.Update(0f, items), 1, "the first update returns the burst");
            Eq(items.Count, 1, "the list was cleared before it was filled");
            ManagedPlasmaBurstTracker.Item start = items[0];
            Near(start.Spec.RingRadius, tracker.StartRadius, "it starts at the small radius");
            Near(start.Spec.BandWidth, tracker.StartBand, "and the thick band");
            Near(start.Fade, 0f, "and is invisible at age 0, so it fades in");
            Near(start.Age, 0f, "at noise age 0");
            Near(start.Position.x, 1f, "the position keeps x");
            Near(start.Position.y, 2f + tracker.SurfaceOffset, "and moves out along the normal, so it clears the surface", 1e-5f);
            Near(start.Position.z, 3f, "and keeps z");
            Near(start.Normal.y, 1f, "the normal is kept");
            Near(start.Diameter, 2f * 3f * 0.9f / 0.7f, "a blast radius of 3 gives the quad that puts the ring's edge at 0.9 of it", 1e-3f);
            Eq(start.Spec.Palette, ManagedPlasmaPalette.Fire, "the palette comes from the template");
            Near(start.Spec.Turbulence, 0.8f, "and so does the turbulence");

            // Halfway through a one second burst.
            tracker.Update(0.5f, items);
            Eq(items.Count, 1, "it is alive at half time");
            float grown = 1f - 0.5f * 0.5f * 0.5f;
            Near(items[0].Spec.RingRadius, 0.2f + 0.5f * grown, "the radius has used 87.5 percent of its growth by half time (ease out)", 1e-4f);
            Near(items[0].Spec.BandWidth, 0.105f, "the band is half way between thick and thin", 1e-4f);
            Near(items[0].Spec.Streaks, 0.9f * 0.8f, "streaks have eased down by 20 percent", 1e-4f);
            Near(items[0].Fade, 1f, "it is fully visible before the fade start");
            Near(items[0].Age, 0.5f * tracker.NoiseSpeed, "the noise runs at NoiseSpeed times real time");

            // Run it through frame by frame and watch the invariants.
            ManagedPlasmaBurstTracker run = new ManagedPlasmaBurstTracker();
            run.Add(template, Vector3.zero, Vector3.up, 3f, 1f);
            float lastRadius = -1f;
            float lastBand = 99f;
            float peakFade = 0f;
            float lastFade = -1f;
            bool grew = true;
            bool thinned = true;
            bool rose = true;
            bool fell = true;
            bool inLimits = true;
            bool fadeOk = true;
            bool risen = false;
            int frames = 0;
            while (run.Count > 0 && frames < 500)
            {
                run.Update(0.01f, items);
                frames++;
                if (items.Count == 0)
                {
                    break;
                }

                ManagedPlasmaBurstTracker.Item it = items[0];
                grew &= it.Spec.RingRadius >= lastRadius - 1e-6f;
                thinned &= it.Spec.BandWidth <= lastBand + 1e-6f;
                lastRadius = it.Spec.RingRadius;
                lastBand = it.Spec.BandWidth;
                inLimits &= it.Spec.RingRadius >= ManagedPlasmaLimits.MinRingRadius && it.Spec.RingRadius <= ManagedPlasmaLimits.MaxRingRadius
                    && it.Spec.BandWidth >= ManagedPlasmaLimits.MinBand && it.Spec.BandWidth <= ManagedPlasmaLimits.MaxBand;
                fadeOk &= it.Fade >= 0f && it.Fade <= 1f;
                if (!risen)
                {
                    rose &= it.Fade >= lastFade - 1e-6f;
                    if (it.Fade >= 0.999f)
                    {
                        risen = true;
                    }
                }
                else if (it.Fade < peakFade - 1e-6f || lastFade < peakFade)
                {
                    // Once it has reached full, it only falls from here.
                    fell &= it.Fade <= lastFade + 1e-6f;
                }

                peakFade = Math.Max(peakFade, it.Fade);
                lastFade = it.Fade;
            }

            Check(grew, "the radius never shrinks");
            Check(thinned, "the band never thickens");
            Check(inLimits, "radius and band stay inside the ring limits every frame");
            Check(fadeOk, "fade stays between 0 and 1");
            Check(rose, "the fade rises first");
            Check(fell, "and then only falls");
            Near(peakFade, 1f, "it reaches full visibility");
            Check(lastFade < 0.2f, "it is nearly gone on the last frame it is drawn");
            Eq(run.Count, 0, "the burst is removed when its time is up");
            Check(frames >= 99 && frames <= 101, "after about one second (" + frames + " frames of 0.01)");
            Near(lastRadius, tracker.EndRadius, "it ends near the end radius", 0.01f);

            // The template is only read.
            Eq(template.Seed, 11, "the template seed is untouched");
            Near(template.RingRadius, 0.55f, "and so is its radius");
            Near(template.Streaks, 0.9f, "and its streaks");

            // Surface offset and normals.
            ManagedPlasmaBurstTracker n = new ManagedPlasmaBurstTracker();
            n.Add(template, new Vector3(0f, 1f, 0f), Vector3.zero, 3f, 1f);
            n.Update(0f, items);
            Near(items[0].Position.y, 1f, "a zero normal does not move the burst");
            Near(items[0].Normal.sqrMagnitude, 0f, "and is kept as zero, for the view to face the camera");
            n.Add(template, new Vector3(0f, 1f, 0f), new Vector3(float.NaN, 0f, 1f), 3f, 1f);
            n.Update(0f, items);
            Near(items[1].Position.z, 0f, "a NaN normal is treated as zero");
            Near(items[1].Normal.sqrMagnitude, 0f, "and kept as zero");
            n.Clear();
            n.Add(template, Vector3.zero, new Vector3(0f, 0f, 5f), 3f, 1f);
            n.Update(0f, items);
            Near(items[0].Normal.z, 1f, "a long normal is made one unit long");
            Near(items[0].Position.z, n.SurfaceOffset, "so the offset is always the same distance", 1e-5f);

            // Frame time edge cases.
            ManagedPlasmaBurstTracker t = new ManagedPlasmaBurstTracker();
            t.Add(template, Vector3.zero, Vector3.up, 3f, 1f);
            t.Update(0.5f, items);
            float radiusBefore = items[0].Spec.RingRadius;
            t.Update(-5f, items);
            Eq(items.Count, 1, "a negative frame time does not kill the burst");
            Near(items[0].Spec.RingRadius, radiusBefore, "and does not run it backwards");
            t.Update(30f, items);
            Eq(items.Count, 0, "a huge frame time ends it at once");
            Eq(t.Count, 0, "and removes it");
            t.Add(template, Vector3.zero, Vector3.up, 3f, 1f);
            t.Clear();
            Eq(t.Count, 0, "Clear drops every burst");
            Eq(t.Update(0.1f, items), 0, "and nothing comes back");

            // Quad size.
            Near(tracker.DiameterFor(0f), tracker.MinDiameter, "a blast of zero still gets the minimum size");
            Near(tracker.DiameterFor(-4f), tracker.MinDiameter, "a negative blast too");
            Near(tracker.DiameterFor(0.2f), tracker.MinDiameter, "a tiny blast is lifted to the minimum");
            Near(tracker.DiameterFor(3f), 2f * 3f * 0.9f / 0.7f, "a normal blast follows the radius", 1e-3f);
            Check(tracker.DiameterFor(6f) > tracker.DiameterFor(3f), "a bigger blast gives a bigger quad");
            Near(tracker.DiameterFor(1000f), tracker.MaxDiameter, "a huge blast is capped at the largest size");
            Near(tracker.DiameterFor(float.PositiveInfinity), tracker.MinDiameter, "an infinite blast is treated as none");
            Near(tracker.DiameterFor(float.NaN), tracker.MinDiameter, "and so is NaN");
            ManagedPlasmaBurstTracker odd = new ManagedPlasmaBurstTracker { EndRadius = 0f };
            Check(!float.IsInfinity(odd.DiameterFor(3f)) && !float.IsNaN(odd.DiameterFor(3f)), "an end radius of zero does not divide by zero");

            Section("Plasma burst tracker, refusals");
            ManagedPlasmaBurstTracker bad = new ManagedPlasmaBurstTracker();
            Check(!bad.Add(null, Vector3.zero, Vector3.up, 3f, 1f), "no template is refused");
            Check(!bad.Add(template, Vector3.zero, Vector3.up, 3f, 0f), "a length of 0 is refused");
            Check(!bad.Add(template, Vector3.zero, Vector3.up, 3f, -1f), "a negative length is refused");
            Check(!bad.Add(template, Vector3.zero, Vector3.up, 3f, float.NaN), "a NaN length is refused");
            Check(!bad.Add(template, new Vector3(float.NaN, 0f, 0f), Vector3.up, 3f, 1f), "a NaN position is refused");
            Check(!bad.Add(template, new Vector3(0f, float.PositiveInfinity, 0f), Vector3.up, 3f, 1f), "an infinite position is refused");
            Eq(bad.Count, 0, "none of them left a burst behind");
            Check(bad.Add(template, Vector3.zero, Vector3.up, float.NaN, 1f), "a NaN blast radius is accepted and drawn at the minimum size");
            bad.Update(0f, items);
            Near(items[0].Diameter, bad.MinDiameter, "at the minimum size");
        }

        private static void PlasmaBurstTrackerLimitTests()
        {
            Section("Plasma burst tracker, limits and seeds");
            List<ManagedPlasmaBurstTracker.Item> items = new List<ManagedPlasmaBurstTracker.Item>();
            ManagedPlasmaRingSpec template = new ManagedPlasmaRingSpec { Seed = 100, Palette = ManagedPlasmaPalette.Water };

            ManagedPlasmaBurstTracker tracker = new ManagedPlasmaBurstTracker { MaxBursts = 3 };
            for (int i = 0; i < 3; i++)
            {
                tracker.Add(template, new Vector3(i, 0f, 0f), Vector3.up, 3f, 1f);
            }

            tracker.Update(0f, items);
            Eq(items.Count, 3, "three bursts fit");
            Eq(items[0].Spec.Seed, 100, "the first has the template seed");
            Eq(items[1].Spec.Seed, 101, "the second has the next seed, so no two hits look alike");
            Eq(items[2].Spec.Seed, 102, "and the third the one after");
            Check(items[0].Position.x < items[1].Position.x && items[1].Position.x < items[2].Position.x, "they are listed oldest first");

            tracker.Add(template, new Vector3(9f, 0f, 0f), Vector3.up, 3f, 1f);
            tracker.Update(0f, items);
            Eq(items.Count, 3, "a fourth replaces the oldest");
            Near(items[0].Position.x, 1f, "the oldest one is gone", 1e-4f);
            Near(items[2].Position.x, 9f, "and the new one is last", 1e-4f);
            Eq(items[2].Spec.Seed, 103, "and has the next seed");
            Eq(template.Seed, 100, "the template seed never changes");

            ManagedPlasmaBurstTracker one = new ManagedPlasmaBurstTracker { MaxBursts = 0 };
            one.Add(template, Vector3.zero, Vector3.up, 3f, 1f);
            one.Add(template, Vector3.zero, Vector3.up, 3f, 1f);
            Eq(one.Count, 1, "a limit of 0 is raised to 1");

            ManagedPlasmaBurstTracker many = new ManagedPlasmaBurstTracker { MaxBursts = 999 };
            for (int i = 0; i < 40; i++)
            {
                many.Add(template, Vector3.zero, Vector3.up, 3f, 1f);
            }

            Eq(many.Count, ManagedPlasmaBurstTracker.HardMaxBursts, "a huge limit is cut to the hard cap");

            // Bursts started at different times end at different times, and the survivors keep their own clocks.
            ManagedPlasmaBurstTracker clocks = new ManagedPlasmaBurstTracker();
            clocks.Add(template, new Vector3(0f, 0f, 0f), Vector3.up, 3f, 0.4f);
            clocks.Update(0.3f, items);
            clocks.Add(template, new Vector3(5f, 0f, 0f), Vector3.up, 3f, 0.4f);
            clocks.Update(0.2f, items);
            Eq(items.Count, 1, "the older burst has ended and the newer one is still alive");
            Near(items[0].Position.x, 5f, "it is the newer one", 1e-4f);
            Near(items[0].Age, 0.2f * clocks.NoiseSpeed, "with its own age, which started when it was added", 1e-4f);

            // Many bursts and many frames: the count never passes the limit and it always empties.
            ManagedPlasmaBurstTracker soak = new ManagedPlasmaBurstTracker { MaxBursts = 8 };
            int worst = 0;
            for (int frame = 0; frame < 400; frame++)
            {
                if (frame % 3 == 0)
                {
                    soak.Add(template, new Vector3(frame, 0f, 0f), Vector3.up, 3f, 0.6f);
                }

                soak.Update(0.016f, items);
                worst = Math.Max(worst, items.Count);
            }

            Check(worst <= 8, "the live count never passes the limit (" + worst + ")");
            for (int frame = 0; frame < 100; frame++)
            {
                soak.Update(0.016f, items);
            }

            Eq(soak.Count, 0, "and every burst ends once nothing new arrives");
        }

        private static void PlasmaSpellTableTests()
        {
            Section("Plasma spell table");
            ManagedPlasmaSpellTable.Style fire = ManagedPlasmaSpellTable.For(ManagedSpellKind.Fireball);
            ManagedPlasmaSpellTable.Style ice = ManagedPlasmaSpellTable.For(ManagedSpellKind.Ice);
            ManagedPlasmaSpellTable.Style seeker = ManagedPlasmaSpellTable.For(ManagedSpellKind.Seeker);
            Eq(fire.Spec.Palette, ManagedPlasmaPalette.Fire, "a fireball bursts with the fire palette");
            Eq(ice.Spec.Palette, ManagedPlasmaPalette.Water, "ice bursts with the water palette");
            Eq(seeker.Spec.Palette, ManagedPlasmaPalette.Arcane, "a seeker bursts with the arcane palette");
            Check(fire.Seconds > 0f && ice.Seconds > 0f && seeker.Seconds > 0f, "every burst has a length");
            Check(fire.Seconds < 2f && ice.Seconds < 2f && seeker.Seconds < 2f, "and it is short");
            Check(fire.Spec.Seed != ice.Spec.Seed && ice.Spec.Seed != seeker.Spec.Seed && fire.Spec.Seed != seeker.Spec.Seed,
                "each spell starts from its own seed");

            ManagedPlasmaSpellTable.Style unknown = ManagedPlasmaSpellTable.For((ManagedSpellKind)99);
            Check(unknown != null, "an unknown spell still gets a style");
            Check(unknown == fire, "and it is the fireball style");

            foreach (ManagedSpellKind kind in new[] { ManagedSpellKind.Fireball, ManagedSpellKind.Ice, ManagedSpellKind.Seeker })
            {
                float radius = ManagedPlasmaSpellTable.BlastRadius(kind);
                Check(radius > 0f, kind + " has a blast radius to draw");
                ManagedSpellDefinition definition = ManagedSpellDefinition.For(kind);
                if (definition != null && definition.ImpactRadius > 0f)
                {
                    Near(radius, definition.ImpactRadius, kind + " follows the radius in the spell table");
                }
                else
                {
                    Near(radius, ManagedPlasmaSpellTable.For(kind).FallbackRadius, kind + " has no radius of its own, so the fallback is used");
                }
            }

            Near(ManagedPlasmaSpellTable.BlastRadius(ManagedSpellKind.Fireball), 3f, "a fireball burst is sized for 3 metres");
            Near(ManagedPlasmaSpellTable.BlastRadius(ManagedSpellKind.Seeker), 2.5f, "a seeker for 2.5");
            Near(ManagedPlasmaSpellTable.BlastRadius(ManagedSpellKind.Ice), 1.8f, "and ice, which has no blast of its own, for 1.8");
            Check(!ReferenceEquals(fire.Spec, null) && ReferenceEquals(ManagedPlasmaSpellTable.For(ManagedSpellKind.Fireball), fire),
                "the style is built once and shared");
        }

        private static void PlasmaBurstViewTests()
        {
            Section("Plasma burst view");
            Shader.Known.Add(ManagedPlasmaRenderer.ShaderName);
            Graphics.Calls.Clear();
            Debug.Errors.Clear();
            Debug.Warnings.Clear();
            int frame = Time.frameCount + 5000;
            float savedDelta = Time.deltaTime;
            UnityEngine.Camera savedCamera = UnityEngine.Camera.main;
            Time.deltaTime = 0.016f;
            int idA = Shader.PropertyToID("_ParamA");
            int idB = Shader.PropertyToID("_ParamB");

            GameObject camObject = new GameObject("burst test camera", typeof(UnityEngine.Camera));
            UnityEngine.Camera cam = camObject.GetComponent<UnityEngine.Camera>();
            camObject.transform.position = new Vector3(0f, 2f, -5f);
            UnityEngine.Camera.main = cam;

            GameObject go = new GameObject("burst view", typeof(ManagedPlasmaBurstView));
            go.layer = 7;
            ManagedPlasmaBurstView view = go.GetComponent<ManagedPlasmaBurstView>();
            Call(view, "OnEnable");

            Time.frameCount = frame++;
            Call(view, "LateUpdate");
            Eq(Graphics.Calls.Count, 0, "with no burst nothing is drawn");
            Check(view.DrawPath == null, "and no renderer is built yet");

            // A fireball lands.
            ManagedSpellCaster.Raise(new ManagedSpellImpact { Position = new Vector3(1f, 0f, 2f), Normal = Vector3.up, Kind = ManagedSpellKind.Fireball });
            Eq(view.AliveCount, 1, "a spell impact starts a burst");
            Eq(view.SpawnedCount, 1, "and it is counted");
            Time.frameCount = frame++;
            Call(view, "LateUpdate");
            Eq(Graphics.Calls.Count, 1, "the burst draws in one call");
            Eq(Graphics.Calls[0].Kind, "instanced", "through DrawMeshInstanced");
            Eq(Graphics.Calls[0].Count, 1, "with one instance");
            Eq(Graphics.Calls[0].Layer, 7, "on the view's layer");
            Near(Graphics.Calls[0].VectorArrays[idB][0].y, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Fire), "in the fire palette");
            float firstU = 0.016f / ManagedPlasmaSpellTable.For(ManagedSpellKind.Fireball).Seconds;
            Near(Graphics.Calls[0].VectorArrays[idA][0].z, 0.2f + 0.5f * (1f - (1f - firstU) * (1f - firstU) * (1f - firstU)),
                "at the radius one frame into the burst, still small", 1e-3f);
            Matrix4x4 m = Graphics.Calls[0].Snapshot[0];
            Near(m.m03, 1f, "placed at the impact x");
            Near(m.m13, 0.08f, "lifted off the surface", 1e-3f);
            Near(m.m23, 2f, "and at the impact z");
            float width = (float)Math.Sqrt(m.m00 * m.m00 + m.m10 * m.m10 + m.m20 * m.m20);
            Near(width, 2f * 3f * 0.9f / 0.7f, "the quad is sized from the 3 metre blast", 1e-2f);
            Near(m.m12 / width, 1f, "and lies flat: its Z axis points along the surface normal (up)", 1e-3f);

            // All three spells at once share one call and keep their own palettes.
            Graphics.Calls.Clear();
            ManagedSpellCaster.Raise(new ManagedSpellImpact { Position = new Vector3(4f, 0f, 0f), Normal = Vector3.up, Kind = ManagedSpellKind.Ice });
            ManagedSpellCaster.Raise(new ManagedSpellImpact { Position = new Vector3(6f, 0f, 0f), Normal = Vector3.up, Kind = ManagedSpellKind.Seeker });
            Time.frameCount = frame++;
            Call(view, "LateUpdate");
            Eq(Graphics.Calls.Count, 1, "three bursts of three spells are still one call");
            Eq(Graphics.Calls[0].Count, 3, "with three instances");
            Near(Graphics.Calls[0].VectorArrays[idB][0].y, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Fire), "the oldest is fire");
            Near(Graphics.Calls[0].VectorArrays[idB][1].y, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Water), "then ice is water");
            Near(Graphics.Calls[0].VectorArrays[idB][2].y, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Arcane), "then the seeker is arcane");
            Eq(view.AliveCount, 3, "three are alive");
            Check(view.Describe().Contains("3 alive"), "Describe says how many are alive");
            Check(view.Describe().Contains("Instanced"), "and the draw path");

            // A hit with no normal faces the camera.
            Graphics.Calls.Clear();
            ManagedSpellCaster.Raise(new ManagedSpellImpact { Position = new Vector3(0f, 2f, 5f), Normal = Vector3.zero, Kind = ManagedSpellKind.Seeker });
            Time.frameCount = frame++;
            Call(view, "LateUpdate");
            Matrix4x4 faced = Graphics.Calls[0].Snapshot[Graphics.Calls[0].Count - 1];
            Near(faced.m23, 5f, "the camera facing burst sits where it hit");
            float widthFaced = (float)Math.Sqrt(faced.m02 * faced.m02 + faced.m12 * faced.m12 + faced.m22 * faced.m22);
            Near(faced.m22 / widthFaced, 1f, "and its Z axis points away from the camera, along +Z", 1e-3f);

            // Rotation helper: straight up and down must give a real rotation, not a broken one.
            foreach (Vector3 dir in new[] { Vector3.up, Vector3.down, new Vector3(0f, 1f, 0.01f), Vector3.right, new Vector3(1f, 1f, 1f) })
            {
                Quaternion q = (Quaternion)CallStatic(typeof(ManagedPlasmaBurstView), "FacingRotation", dir);
                Vector3 axis = q * Vector3.forward;
                Vector3 want = dir.normalized;
                Check(!float.IsNaN(q.x) && !float.IsNaN(q.w), "a turn toward " + dir + " is a number");
                Near(axis.x, want.x, "Z points along " + dir + " (x)", 1e-3f);
                Near(axis.y, want.y, "Z points along " + dir + " (y)", 1e-3f);
                Near(axis.z, want.z, "Z points along " + dir + " (z)", 1e-3f);
            }

            // Run it out.
            Graphics.Calls.Clear();
            int guard = 0;
            while (view.AliveCount > 0 && guard < 400)
            {
                Time.frameCount = frame++;
                Call(view, "LateUpdate");
                guard++;
            }

            Eq(view.AliveCount, 0, "every burst ends");
            Check(guard < 100, "within a couple of seconds (" + guard + " frames)");
            int callsAfter = Graphics.Calls.Count;
            Time.frameCount = frame++;
            Call(view, "LateUpdate");
            Eq(Graphics.Calls.Count, callsAfter, "and nothing draws once they have");

            // Bad input.
            Check(!view.Spawn(ManagedSpellKind.Fireball, new Vector3(float.NaN, 0f, 0f), Vector3.up), "a NaN position is refused");
            Check(view.Describe().Contains("1 refused"), "and Describe counts it");
            Eq(view.AliveCount, 0, "nothing was started");
            Check(view.Spawn((ManagedSpellKind)99, Vector3.zero, Vector3.up), "an unknown spell still bursts");
            Eq(view.AliveCount, 1, "it is alive");

            // The cap in the view: 40 impacts at once leave only the most recent bursts.
            for (int i = 0; i < 40; i++)
            {
                ManagedSpellCaster.Raise(new ManagedSpellImpact { Position = new Vector3(i, 0f, 0f), Normal = Vector3.up, Kind = ManagedSpellKind.Fireball });
            }

            Check(view.AliveCount <= 8, "the view keeps no more than its limit of 8 (" + view.AliveCount + ")");
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Call(view, "LateUpdate");
            Check(Graphics.Calls.Count == 1 && Graphics.Calls[0].Count <= 8, "and draws them in one call");

            // The DrawMesh fallback.
            SetField(view, "_forceDrawMesh", true);
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Call(view, "LateUpdate");
            Eq(view.DrawPath, (ManagedPlasmaDrawPath?)ManagedPlasmaDrawPath.DrawMesh, "ticking Force DrawMesh switches the renderer");
            Check(Graphics.Calls.Count > 0 && Graphics.Calls[0].Kind == "mesh", "and the bursts are drawn one by one");
            SetField(view, "_forceDrawMesh", false);
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Call(view, "LateUpdate");
            Eq(view.DrawPath, (ManagedPlasmaDrawPath?)ManagedPlasmaDrawPath.Instanced, "and unticking switches back");

            // Alpha blend.
            view.Additive = false;
            Time.frameCount = frame++;
            Call(view, "LateUpdate");
            Check(view.Describe().Contains("alpha"), "the blend toggle reaches the renderer");

            // Disabling stops listening, clears the bursts and frees the renderer.
            Call(view, "OnDisable");
            Eq(view.AliveCount, 0, "disabling drops the bursts");
            Check(view.DrawPath == null, "and frees the renderer");
            int spawned = view.SpawnedCount;
            ManagedSpellCaster.Raise(new ManagedSpellImpact { Position = Vector3.zero, Normal = Vector3.up, Kind = ManagedSpellKind.Fireball });
            Eq(view.SpawnedCount, spawned, "a disabled view no longer hears impacts");

            // Enabling twice must not listen twice.
            Call(view, "OnEnable");
            Call(view, "OnEnable");
            ManagedSpellCaster.Raise(new ManagedSpellImpact { Position = Vector3.zero, Normal = Vector3.up, Kind = ManagedSpellKind.Fireball });
            Eq(view.SpawnedCount, spawned + 1, "enabling twice still starts one burst per impact");

            // A second view in the scene gets its own bursts from the same event.
            GameObject go2 = new GameObject("second burst view", typeof(ManagedPlasmaBurstView));
            ManagedPlasmaBurstView second = go2.GetComponent<ManagedPlasmaBurstView>();
            Call(second, "OnEnable");
            ManagedSpellCaster.Raise(new ManagedSpellImpact { Position = Vector3.zero, Normal = Vector3.up, Kind = ManagedSpellKind.Ice });
            Eq(second.SpawnedCount, 1, "a second view hears the same impact");
            Call(second, "OnDisable");
            Call(view, "OnDisable");

            Eq(Debug.Errors.Count, 0, "no errors were logged on the way");
            Time.deltaTime = savedDelta;
            UnityEngine.Camera.main = savedCamera;
        }
    }
}
