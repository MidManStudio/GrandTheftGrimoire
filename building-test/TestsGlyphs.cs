// Checks for the glyph generator, the ribbon baker and the glyph renderer. Verification harness only.
// The shader and the look are not checked here: they need Unity.
using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Glyphs;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        private static ManagedGlyphSpec GlyphSpec(int seed, int depth, int wheels = 1)
        {
            return new ManagedGlyphSpec { Seed = seed, MaxDepth = depth, Radius = 1f, WheelCount = wheels };
        }

        private static void GlyphTests()
        {
            GlyphRevealTests();
            GlyphGeneratorTests();
            GlyphBudgetTests();
            GlyphBakerTests();
            GlyphRendererTests();
            GlyphMoleculeTests();
            GlyphCastingTests();
        }

        private static void GlyphRevealTests()
        {
            Section("Glyph reveal");
            ManagedGlyphSpec spec = GlyphSpec(1, 4);
            Eq(spec.LevelCount, 5, "depth 4 has 5 levels");
            Near(spec.RevealForLevels(1), 0.2f, "one level is a fifth of the reveal");
            Near(spec.BaseReveal, 0.2f, "default ring (base depth 0) is one level");
            spec.BaseDepth = -1;
            Near(spec.BaseReveal, 0f, "base depth -1 draws nothing at charge 0");
            spec.BaseDepth = 2;
            Near(spec.RevealForCharge(0f), 0.6f, "charge 0 shows the base levels");
            Near(spec.RevealForCharge(1f), 1f, "charge 1 is the whole glyph");
            Near(spec.RevealForCharge(5f), 1f, "charge is clamped");
            spec.BaseDepth = 0;
            Eq(spec.VisibleLevels(0f), 0, "reveal 0 has no level");
            Eq(spec.VisibleLevels(0.2f), 1, "reveal 0.2 is level 0 done");
            Eq(spec.VisibleLevels(0.21f), 2, "a hair past it starts level 1");
            Eq(spec.VisibleLevels(1f), 5, "reveal 1 shows every level");

            ManagedGlyphSpec deep = GlyphSpec(1, 12);
            Eq(deep.Depth, ManagedGlyphLimits.MaxDepth, "depth is capped at 7");
            Eq(GlyphSpec(1, 4, 9).Wheels, ManagedGlyphLimits.MaxWheels, "wheels are capped at 3");

            ManagedGlyphSpec a = GlyphSpec(5, 3);
            ManagedGlyphSpec b = GlyphSpec(5, 3);
            Eq(a.CacheKey(ManagedGlyphLod.Near), b.CacheKey(ManagedGlyphLod.Near), "same spec, same key");
            Check(a.CacheKey(ManagedGlyphLod.Near) != a.CacheKey(ManagedGlyphLod.Far), "LOD changes the key");
            b.Seed = 6;
            Check(a.CacheKey(ManagedGlyphLod.Near) != b.CacheKey(ManagedGlyphLod.Near), "seed changes the key");
            b.Seed = 5;
            b.Palette = ManagedGlyphPalette.Water;
            Check(a.CacheKey(ManagedGlyphLod.Near) != b.CacheKey(ManagedGlyphLod.Near), "palette changes the key");
            b.Palette = a.Palette;
            b.BaseDepth = 3;
            Eq(a.CacheKey(ManagedGlyphLod.Near), b.CacheKey(ManagedGlyphLod.Near), "base depth does not change geometry, so not the key");

            Near(ManagedGlyphPalette.Prism.ColorFor(4).b, ManagedGlyphPalette.Prism.ColorFor(0).b, "palette wraps");
            Near(ManagedGlyphPalette.Fire.ColorFor(-1).r, ManagedGlyphPalette.Fire.ColorFor(2).r, "palette wraps below zero");

            Eq(ManagedGlyphRenderer.ChooseLod(3f), ManagedGlyphLod.Near, "close is Near");
            Eq(ManagedGlyphRenderer.ChooseLod(20f), ManagedGlyphLod.Mid, "middle distance is Mid");
            Eq(ManagedGlyphRenderer.ChooseLod(80f), ManagedGlyphLod.Far, "far is Far");
        }

        private static void GlyphGeneratorTests()
        {
            Section("Glyph generator");
            ManagedGlyphSpec spec = GlyphSpec(7, 5);
            ManagedGlyphStrokeSet first = ManagedGlyphGenerator.Generate(spec, ManagedGlyphLod.Near);
            ManagedGlyphStrokeSet second = ManagedGlyphGenerator.Generate(spec, ManagedGlyphLod.Near);
            Eq(first.Strokes.Count, second.Strokes.Count, "same seed, same stroke count");
            Eq(first.PointCount, second.PointCount, "same seed, same point count");
            bool same = true;
            for (int i = 0; i < first.Strokes.Count && same; i++)
            {
                Vector2[] p = first.Strokes[i].Points;
                Vector2[] q = second.Strokes[i].Points;
                same = p.Length == q.Length && p[0].x == q[0].x && p[0].y == q[0].y && p[p.Length - 1].x == q[q.Length - 1].x;
            }

            Check(same, "same seed, identical points");

            ManagedGlyphStrokeSet other = ManagedGlyphGenerator.Generate(GlyphSpec(8, 5), ManagedGlyphLod.Near);
            Check(other.PointCount != first.PointCount || other.Strokes[0].Points[0].x != first.Strokes[0].Points[0].x, "another seed gives another glyph");

            // Every level 0 to 7 is reached at full depth, and each stroke stays inside its slice of the reveal range.
            ManagedGlyphSpec deep = GlyphSpec(3, 7);
            ManagedGlyphStrokeSet tree = ManagedGlyphGenerator.Generate(deep, ManagedGlyphLod.Near);
            bool[] seen = new bool[8];
            bool windowsOk = true;
            bool zOk = true;
            for (int i = 0; i < tree.Strokes.Count; i++)
            {
                ManagedGlyphStroke s = tree.Strokes[i];
                seen[s.Level] = true;
                float lo = s.Level / 8f - 1e-4f;
                float hi = (s.Level + 1) / 8f + 1e-4f;
                windowsOk &= s.T0 >= lo && s.T1 <= hi && s.T0 <= s.T1;
                zOk &= Math.Abs(s.Z) < 1e-6f;
            }

            for (int level = 0; level < 8; level++)
            {
                Check(seen[level], "depth 7 reaches level " + level);
            }

            Check(windowsOk, "every stroke reveals inside the slice of its level");
            Check(zOk, "one wheel lies flat at z 0");

            // Edges leave the parent rim: the level 1 edges start on the root ring.
            bool edgeOnRim = true;
            int edges = 0;
            for (int i = 0; i < tree.Strokes.Count; i++)
            {
                ManagedGlyphStroke s = tree.Strokes[i];
                if (s.Kind == ManagedGlyphStrokeKind.Edge && s.Level == 1)
                {
                    edges++;
                    edgeOnRim &= Math.Abs(s.Points[0].magnitude - 1f) < 1e-3f;
                }
            }

            Check(edges >= 2 && edges <= 3, "the root grows 2 or 3 children, got " + edges);
            Check(edgeOnRim, "the first edges start on the root rim");

            // Depth limits the levels, and the base depth does not change the strokes.
            ManagedGlyphStrokeSet shallow = ManagedGlyphGenerator.Generate(GlyphSpec(3, 2), ManagedGlyphLod.Near);
            int top = 0;
            for (int i = 0; i < shallow.Strokes.Count; i++)
            {
                top = Math.Max(top, shallow.Strokes[i].Level);
            }

            Eq(top, 2, "depth 2 stops at level 2");

            ManagedGlyphStrokeSet flat = ManagedGlyphGenerator.Generate(GlyphSpec(3, 0), ManagedGlyphLod.Near);
            Eq(flat.NodeCount, 1, "depth 0 is one node");

            // Wheels stack along z.
            ManagedGlyphStrokeSet wheels = ManagedGlyphGenerator.Generate(GlyphSpec(3, 2, 2), ManagedGlyphLod.Near);
            bool hasZero = false;
            bool hasStack = false;
            for (int i = 0; i < wheels.Strokes.Count; i++)
            {
                hasZero |= Math.Abs(wheels.Strokes[i].Z) < 1e-6f;
                hasStack |= Math.Abs(wheels.Strokes[i].Z - 0.12f) < 1e-5f;
            }

            Check(hasZero && hasStack, "two wheels sit at z 0 and z 0.12");

            // Rosette, star and nest stay inside the ring they are drawn in.
            ManagedGlyphOrnaments[] figures = { ManagedGlyphOrnaments.Rosette, ManagedGlyphOrnaments.Star, ManagedGlyphOrnaments.Nest };
            ManagedGlyphStrokeKind[] kinds = { ManagedGlyphStrokeKind.Rosette, ManagedGlyphStrokeKind.Chord, ManagedGlyphStrokeKind.Nest };
            for (int f = 0; f < 3; f++)
            {
                int found = 0;
                float farthest = 0f;
                for (int seed = 1; seed <= 60; seed++)
                {
                    ManagedGlyphSpec one = GlyphSpec(seed, 0);
                    one.Ornaments = figures[f];
                    ManagedGlyphStrokeSet set = ManagedGlyphGenerator.Generate(one, ManagedGlyphLod.Near);
                    for (int i = 0; i < set.Strokes.Count; i++)
                    {
                        if (set.Strokes[i].Kind != kinds[f])
                        {
                            continue;
                        }

                        found++;
                        Vector2[] pts = set.Strokes[i].Points;
                        for (int k = 0; k < pts.Length; k++)
                        {
                            farthest = Math.Max(farthest, pts[k].magnitude);
                        }
                    }
                }

                Check(found > 0, kinds[f] + " shows up for some seed");
                Check(farthest <= 0.86f, kinds[f] + " fits inside the ring, farthest " + farthest);
            }

            // Ornament flags are respected.
            ManagedGlyphSpec plain = GlyphSpec(2, 5);
            plain.Ornaments = ManagedGlyphOrnaments.None;
            ManagedGlyphStrokeSet bare = ManagedGlyphGenerator.Generate(plain, ManagedGlyphLod.Near);
            bool onlyRingsAndEdges = true;
            for (int i = 0; i < bare.Strokes.Count; i++)
            {
                ManagedGlyphStrokeKind k = bare.Strokes[i].Kind;
                onlyRingsAndEdges &= k == ManagedGlyphStrokeKind.Ring || k == ManagedGlyphStrokeKind.Edge;
            }

            Check(onlyRingsAndEdges, "no ornaments means rings and edges only");

            // Far LOD is rings and lines only whatever the spec allows.
            ManagedGlyphStrokeSet far = ManagedGlyphGenerator.Generate(GlyphSpec(2, 5), ManagedGlyphLod.Far);
            bool farPlain = true;
            int farTop = 0;
            for (int i = 0; i < far.Strokes.Count; i++)
            {
                ManagedGlyphStrokeKind k = far.Strokes[i].Kind;
                farPlain &= k == ManagedGlyphStrokeKind.Ring || k == ManagedGlyphStrokeKind.Edge;
                farTop = Math.Max(farTop, far.Strokes[i].Level);
            }

            Check(farPlain, "far LOD has rings and edges only");
            Check(farTop <= 2, "far LOD stops at level 2");
        }

        private static void GlyphBudgetTests()
        {
            Section("Glyph budget");
            int worstNear = 0;
            int worstMid = 0;
            int worstFar = 0;
            bool ordered = true;
            bool inRange = true;
            for (int seed = 1; seed <= 60; seed++)
            {
                ManagedGlyphSpec spec = GlyphSpec(seed, 7, 1 + seed % 3);
                ManagedGlyphMeshData near = ManagedGlyphBaker.Bake(spec, ManagedGlyphLod.Near);
                ManagedGlyphMeshData mid = ManagedGlyphBaker.Bake(spec, ManagedGlyphLod.Mid);
                ManagedGlyphMeshData far = ManagedGlyphBaker.Bake(spec, ManagedGlyphLod.Far);
                worstNear = Math.Max(worstNear, near.VertexCount);
                worstMid = Math.Max(worstMid, mid.VertexCount);
                worstFar = Math.Max(worstFar, far.VertexCount);
                ordered &= near.VertexCount >= mid.VertexCount && mid.VertexCount >= far.VertexCount;
                inRange &= near.VertexCount < 65535 && mid.VertexCount < 65535 && far.VertexCount < 65535;
            }

            Console.WriteLine("glyph worst vertex counts at depth 7: near " + worstNear + ", mid " + worstMid + ", far " + worstFar);
            Check(ordered, "near has at least as many vertices as mid, and mid as far");
            Check(inRange, "every glyph fits 16 bit indices");
            Check(worstNear <= 40000, "near glyph stays under 40000 vertices, worst " + worstNear);
            Check(worstMid <= 10000, "mid glyph stays under 10000 vertices, worst " + worstMid);
            Check(worstFar <= 2500, "far glyph stays under 2500 vertices, worst " + worstFar);
        }

        private static void GlyphBakerTests()
        {
            Section("Glyph baker");

            // One open stroke of two points: two cap pairs and two point pairs, three quads.
            ManagedGlyphStrokeSet set = new ManagedGlyphStrokeSet(2);
            set.Strokes.Add(new ManagedGlyphStroke
            {
                Points = new[] { new Vector2(0f, 0f), new Vector2(2f, 0f) },
                Closed = false,
                Level = 0,
                HalfWidth = 0.1f,
                T0 = 0.1f,
                T1 = 0.4f,
                Tint = new Color32(255, 0, 0, 255),
            });

            ManagedGlyphMeshData open = ManagedGlyphBaker.Bake(set);
            Eq(open.VertexCount, 8, "an open stroke of 2 points has 4 pairs");
            Eq(open.Triangles.Length, 18, "three quads, six indices each");
            Near(open.Uv0[0].x, -0.1f, "the start cap begins one half width before the line");
            Near(open.Uv0[6].x, 2.1f, "the end cap ends one half width after the line");
            Near(open.Uv1[3].x, 2f, "every vertex carries the stroke length");
            Near(open.Uv1[3].y, 0.1f, "and the half width");
            Near(open.Uv2[5].x, 0.1f, "and where the reveal starts");
            Near(open.Uv2[5].y, 0.4f, "and where it ends");
            Near(open.Uv0[2].y, 1f, "one side of a pair is +1");
            Near(open.Uv0[3].y, -1f, "the other is -1");
            Near(Math.Abs(open.Positions[2].y - open.Positions[3].y), 0.2f, "a pair is one full width apart");
            Eq((int)open.Colors[0].r, 255, "the tint is in the vertex color");
            Eq(open.LevelIndexEnd.Length, 2, "one end per level");
            Eq(open.LevelIndexEnd[0], 18, "level 0 owns the indices");
            Eq(open.LevelIndexEnd[1], 18, "level 1 is empty");

            // A closed ring of 64 points: no caps, one pair wraps back to the start, length is the perimeter.
            Vector2[] ring = new Vector2[64];
            for (int i = 0; i < 64; i++)
            {
                float a = Mathf.PI * 2f * i / 64f;
                ring[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }

            ManagedGlyphStrokeSet loop = new ManagedGlyphStrokeSet(1);
            loop.Strokes.Add(new ManagedGlyphStroke { Points = ring, Closed = true, Level = 0, HalfWidth = 0.05f, T0 = 0f, T1 = 1f });
            ManagedGlyphMeshData closed = ManagedGlyphBaker.Bake(loop);
            Eq(closed.VertexCount, 130, "a closed ring of 64 points has 65 pairs");
            Near(closed.Uv1[0].x, 6.2821f, "the length is the perimeter of the polygon", 0.01f);
            Near(closed.Uv0[128].x, closed.Uv1[0].x, "the wrap pair is at the full length");
            Near(closed.Positions[128].x, closed.Positions[0].x, "the wrap pair sits on the first pair");
            Near(closed.Positions[129].y, closed.Positions[1].y, "both sides of it");

            // Triangles are in level order and every index is valid.
            ManagedGlyphSpec spec = GlyphSpec(11, 6);
            ManagedGlyphMeshData glyph = ManagedGlyphBaker.Bake(spec, ManagedGlyphLod.Near);
            bool ascending = true;
            for (int i = 1; i < glyph.LevelIndexEnd.Length; i++)
            {
                ascending &= glyph.LevelIndexEnd[i] >= glyph.LevelIndexEnd[i - 1];
            }

            Check(ascending, "level index ends never go down");
            Eq(glyph.LevelIndexEnd[glyph.LevelIndexEnd.Length - 1], glyph.Triangles.Length, "the last level ends at the last index");
            Eq(glyph.Triangles.Length % 3, 0, "indices come in threes");
            bool valid = true;
            for (int i = 0; i < glyph.Triangles.Length; i++)
            {
                valid &= glyph.Triangles[i] >= 0 && glyph.Triangles[i] < glyph.VertexCount;
            }

            Check(valid, "every index points at a vertex");
            Eq(glyph.Uv0.Length, glyph.VertexCount, "uv0 matches the vertices");
            Eq(glyph.Uv1.Length, glyph.VertexCount, "uv1 matches the vertices");
            Eq(glyph.Uv2.Length, glyph.VertexCount, "uv2 matches the vertices");
            Eq(glyph.Colors.Length, glyph.VertexCount, "colors match the vertices");
            Check(glyph.BoundsSize.x > 1.5f && glyph.BoundsSize.y > 1.5f, "the bounds cover the glyph");

            // A glyph that reveals only some levels has the triangles of those levels first.
            ManagedGlyphMeshData one = ManagedGlyphBaker.Bake(GlyphSpec(11, 6), ManagedGlyphLod.Near);
            Check(one.LevelIndexEnd[0] > 0 && one.LevelIndexEnd[0] < one.Triangles.Length, "level 0 is a part of the whole");
        }

        // Draw calls whose mesh holds this many vertices.
        private static List<DrawCall> GlyphCallsOf(int vertexCount)
        {
            List<DrawCall> found = new List<DrawCall>();
            for (int i = 0; i < Graphics.Calls.Count; i++)
            {
                if (Graphics.Calls[i].Mesh != null && Graphics.Calls[i].Mesh.vertices.Length == vertexCount)
                {
                    found.Add(Graphics.Calls[i]);
                }
            }

            return found;
        }

        private static void GlyphRendererTests()
        {
            Section("Glyph renderer, instanced path");
            Shader.Known.Add(ManagedGlyphRenderer.ShaderName);
            Graphics.Calls.Clear();
            int frame = Time.frameCount + 100;
            int reveal = Shader.PropertyToID("_Reveal");
            int fade = Shader.PropertyToID("_Fade");

            ManagedGlyphRenderer renderer = new ManagedGlyphRenderer(3, "test");
            Check(renderer.UsingCustomShader, "the glyph shader is used when it is found");
            Eq(renderer.GlyphMaterial.shader.name, ManagedGlyphRenderer.ShaderName, "the material uses it");
            Eq(renderer.Path, ManagedGlyphDrawPath.Instanced, "instancing is the default path");
            Check(renderer.GlyphMaterial.enableInstancing, "the material has instancing on");

            ManagedGlyphSpec spec = GlyphSpec(4, 3);
            Time.frameCount = frame++;
            Check(renderer.Draw(spec, ManagedGlyphLod.Near, Matrix4x4.identity, 0.5f, 1f), "a draw goes through");
            Eq(Graphics.Calls.Count, 0, "Draw only queues, nothing is sent before Flush");
            Eq(renderer.DrawsThisFrame, 1, "one glyph is queued");
            renderer.Flush();
            Eq(Graphics.Calls.Count, 1, "Flush sends one call");
            Eq(renderer.LastFlushCalls, 1, "and says so");
            DrawCall call = Graphics.Calls[0];
            Eq(call.Kind, "instanced", "it is a DrawMeshInstanced call");
            Eq(call.Count, 1, "with one instance");
            Eq(call.Layer, 3, "the renderer's layer is used");
            Near(call.FloatArrays[reveal][0], 0.5f, "the reveal goes to the shader per instance");
            Near(call.FloatArrays[fade][0], 1f, "and the fade");
            Eq(call.Mesh.vertices.Length, renderer.GetData(spec, ManagedGlyphLod.Near).VertexCount, "the mesh holds the whole baked glyph");
            Check(call.Mesh.UvChannels[0] != null && call.Mesh.UvChannels[1] != null && call.Mesh.UvChannels[2] != null, "uv channels 0 to 2 are set");
            Eq(call.Mesh.triangles.Length, renderer.GetData(spec, ManagedGlyphLod.Near).Triangles.Length, "every triangle is drawn");

            renderer.Flush();
            Eq(Graphics.Calls.Count, 1, "a second Flush with nothing queued sends nothing");
            Eq(renderer.LastFlushCalls, 0, "and says so");

            // Glyphs that share a mesh go out in one call, in the order they were drawn.
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Matrix4x4 m1 = Matrix4x4.TRS(new Vector3(1f, 0f, 0f), Quaternion.identity, Vector3.one);
            Matrix4x4 m2 = Matrix4x4.TRS(new Vector3(2f, 0f, 0f), Quaternion.identity, Vector3.one);
            renderer.Draw(spec, ManagedGlyphLod.Near, m1, 0.25f, 1f);
            renderer.Draw(spec, ManagedGlyphLod.Near, m2, 0.75f, 0.5f);
            renderer.Flush();
            Eq(Graphics.Calls.Count, 1, "two glyphs with the same mesh are one call");
            Eq(Graphics.Calls[0].Count, 2, "with two instances");
            Check(Graphics.Calls[0].Snapshot[0] == m1 && Graphics.Calls[0].Snapshot[1] == m2, "the matrices keep their draw order");
            Near(Graphics.Calls[0].FloatArrays[reveal][0], 0.25f, "each instance has its own reveal (first)");
            Near(Graphics.Calls[0].FloatArrays[reveal][1], 0.75f, "each instance has its own reveal (second)");
            Near(Graphics.Calls[0].FloatArrays[fade][1], 0.5f, "and its own fade");

            // Different meshes are different calls.
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            ManagedGlyphSpec other = GlyphSpec(5, 3);
            renderer.Draw(spec, ManagedGlyphLod.Near, m1, 1f, 1f);
            renderer.Draw(other, ManagedGlyphLod.Near, m2, 1f, 1f);
            renderer.Draw(spec, ManagedGlyphLod.Near, m2, 1f, 1f);
            renderer.Flush();
            Eq(Graphics.Calls.Count, 2, "two different glyphs are two calls");
            Eq(Graphics.Calls[0].Count + Graphics.Calls[1].Count, 3, "carrying three instances");

            // Nothing is queued at reveal 0 or fade 0.
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Check(renderer.Draw(spec, ManagedGlyphLod.Near, Matrix4x4.identity, 0f, 1f), "reveal 0 is not a refusal");
            Check(renderer.Draw(spec, ManagedGlyphLod.Near, Matrix4x4.identity, 1f, 0f), "fade 0 is not a refusal");
            renderer.Flush();
            Eq(Graphics.Calls.Count, 0, "and draws nothing");
            Eq(renderer.CachedCount, 2, "two glyphs were baked so far");

            // Per frame caps.
            Graphics.Calls.Clear();
            renderer.MaxDrawsPerFrame = 3;
            renderer.MaxNearPerFrame = 1;
            Time.frameCount = frame++;
            bool a = renderer.Draw(spec, ManagedGlyphLod.Near, Matrix4x4.identity, 1f, 1f);
            bool b = renderer.Draw(spec, ManagedGlyphLod.Near, Matrix4x4.identity, 1f, 1f);
            bool c = renderer.Draw(spec, ManagedGlyphLod.Far, Matrix4x4.identity, 1f, 1f);
            bool d = renderer.Draw(spec, ManagedGlyphLod.Far, Matrix4x4.identity, 1f, 1f);
            Check(a && b && c, "three draws fit");
            Check(!d, "the fourth is refused");
            Eq(renderer.DrawsThisFrame, 3, "three draws this frame");
            renderer.Flush();
            Eq(Graphics.Calls.Count, 3, "three calls: Near, Mid and Far meshes");
            int nearVerts = renderer.GetData(spec, ManagedGlyphLod.Near).VertexCount;
            int midVerts = renderer.GetData(spec, ManagedGlyphLod.Mid).VertexCount;
            Eq(GlyphCallsOf(nearVerts).Count, 1, "the first Near stays Near");
            Eq(GlyphCallsOf(midVerts).Count, 1, "the second Near past the cap is drawn Mid");
            Time.frameCount = frame++;
            Check(renderer.Draw(spec, ManagedGlyphLod.Near, Matrix4x4.identity, 1f, 1f), "a new frame starts the count again");
            renderer.Flush();

            // More instances than one call can take.
            Graphics.Calls.Clear();
            renderer.MaxDrawsPerFrame = 2000;
            renderer.MaxNearPerFrame = 3;
            Time.frameCount = frame++;
            for (int i = 0; i < 1500; i++)
            {
                renderer.Draw(spec, ManagedGlyphLod.Far, Matrix4x4.identity, (i + 1) / 1500f, 1f);
            }

            renderer.Flush();
            Eq(Graphics.Calls.Count, 2, "1500 instances need two calls");
            Eq(Graphics.Calls[0].Count, ManagedGlyphRenderer.InstanceBatchSize, "the first holds the batch size");
            Eq(Graphics.Calls[1].Count, 1500 - ManagedGlyphRenderer.InstanceBatchSize, "the second holds the rest");
            Near(Graphics.Calls[0].FloatArrays[reveal][0], 1f / 1500f, "the first batch starts at the first reveal");
            Near(Graphics.Calls[1].FloatArrays[reveal][0], (ManagedGlyphRenderer.InstanceBatchSize + 1) / 1500f, "the second batch continues where the first ended");
            renderer.MaxDrawsPerFrame = 12;

            // Draws that were never flushed are dropped when the next frame starts.
            Graphics.Calls.Clear();
            Debug.Warnings.Clear();
            Time.frameCount = frame++;
            renderer.Draw(spec, ManagedGlyphLod.Near, m1, 1f, 1f);
            Time.frameCount = frame++;
            renderer.Draw(spec, ManagedGlyphLod.Near, m2, 1f, 1f);
            renderer.Flush();
            Eq(Graphics.Calls.Count, 1, "a stale queue is dropped");
            Eq(Graphics.Calls[0].Count, 1, "only the current frame's glyph is sent");
            Check(Graphics.Calls[0].Snapshot[0] == m2, "and it is the current one");
            Eq(Debug.Warnings.Count, 1, "the missing Flush is warned about once");

            // Cache eviction never drops a glyph that is queued.
            Graphics.Calls.Clear();
            renderer.MaxCached = 1;
            Time.frameCount = frame++;
            renderer.Draw(GlyphSpec(31, 2), ManagedGlyphLod.Far, Matrix4x4.identity, 1f, 1f);
            renderer.Draw(GlyphSpec(32, 2), ManagedGlyphLod.Far, Matrix4x4.identity, 1f, 1f);
            renderer.Flush();
            Eq(Graphics.Calls.Count, 2, "two queued glyphs both draw even past MaxCached");
            Check(Graphics.Calls[0].Mesh != null && Graphics.Calls[1].Mesh != null, "with their meshes alive");
            Time.frameCount = frame++;
            renderer.MaxCached = 2;
            renderer.Draw(GlyphSpec(21, 2), ManagedGlyphLod.Far, Matrix4x4.identity, 1f, 1f);
            Time.frameCount = frame++;
            renderer.Draw(GlyphSpec(22, 2), ManagedGlyphLod.Far, Matrix4x4.identity, 1f, 1f);
            renderer.Flush();
            Time.frameCount = frame++;
            renderer.Draw(GlyphSpec(23, 2), ManagedGlyphLod.Far, Matrix4x4.identity, 1f, 1f);
            renderer.Flush();
            Eq(renderer.CachedCount, 2, "the cache keeps at most MaxCached glyphs once nothing is queued");

            renderer.Dispose();
            Eq(renderer.CachedCount, 0, "dispose empties the cache");
            Check(!renderer.Draw(spec, ManagedGlyphLod.Near, Matrix4x4.identity, 1f, 1f), "a disposed renderer draws nothing");
            Graphics.Calls.Clear();
            renderer.Flush();
            Eq(Graphics.Calls.Count, 0, "and Flush after it sends nothing");

            GlyphRendererFallbackTests(spec, ref frame);
        }

        private static void GlyphRendererFallbackTests(ManagedGlyphSpec spec, ref int frame)
        {
            Section("Glyph renderer, DrawMesh fallback");
            int reveal = Shader.PropertyToID("_Reveal");
            int fade = Shader.PropertyToID("_Fade");
            Matrix4x4 m1 = Matrix4x4.TRS(new Vector3(1f, 0f, 0f), Quaternion.identity, Vector3.one);
            Matrix4x4 m2 = Matrix4x4.TRS(new Vector3(2f, 0f, 0f), Quaternion.identity, Vector3.one);

            // DrawMesh when asked: one call per glyph, with the numbers in a property block.
            Graphics.Calls.Clear();
            ManagedGlyphRenderer mesh = new ManagedGlyphRenderer(2, false, true, "drawmesh");
            Eq(mesh.Path, ManagedGlyphDrawPath.DrawMesh, "forcing DrawMesh selects it");
            Check(mesh.UsingCustomShader, "the glyph shader is still used");
            Check(!mesh.GlyphMaterial.enableInstancing, "the material does not ask for instancing");
            Time.frameCount = frame++;
            mesh.Draw(spec, ManagedGlyphLod.Near, m1, 0.4f, 0.9f);
            mesh.Draw(spec, ManagedGlyphLod.Near, m2, 0.6f, 0.7f);
            mesh.Flush();
            Eq(Graphics.Calls.Count, 2, "two glyphs are two DrawMesh calls");
            Eq(Graphics.Calls[0].Kind, "mesh", "they are DrawMesh calls");
            Near(Graphics.Calls[0].Floats[reveal], 0.4f, "the first reveal is in its property block");
            Near(Graphics.Calls[1].Floats[reveal], 0.6f, "the second reveal is in its own");
            Near(Graphics.Calls[1].Floats[fade], 0.7f, "so is the fade");
            Check(Graphics.Calls[0].First == m1 && Graphics.Calls[1].First == m2, "each has its matrix, in draw order");
            Eq(Graphics.Calls[0].Layer, 2, "the layer is used");
            mesh.Dispose();

            // No hardware instancing: the same fallback, chosen by itself.
            Graphics.Calls.Clear();
            bool had = SystemInfo.supportsInstancing;
            SystemInfo.supportsInstancing = false;
            ManagedGlyphRenderer noHw = new ManagedGlyphRenderer(0, "no hardware instancing");
            SystemInfo.supportsInstancing = had;
            Eq(noHw.Path, ManagedGlyphDrawPath.DrawMesh, "a machine without instancing uses DrawMesh");
            Time.frameCount = frame++;
            noHw.Draw(spec, ManagedGlyphLod.Near, m1, 1f, 1f);
            noHw.Flush();
            Eq(Graphics.Calls.Count, 1, "and still draws");
            Eq(Graphics.Calls[0].Kind, "mesh", "with a DrawMesh call");
            noHw.Dispose();

            // Sprites/Default: whole levels, no property block.
            Graphics.Calls.Clear();
            ManagedGlyphRenderer plain = new ManagedGlyphRenderer(0, true, "fallback");
            Check(!plain.UsingCustomShader, "forced fallback does not use the glyph shader");
            Eq(plain.Path, ManagedGlyphDrawPath.DrawMesh, "the shader fallback cannot instance");
            Eq(plain.GlyphMaterial.shader.name, "Sprites/Default", "it uses Sprites/Default");
            ManagedGlyphMeshData data = plain.GetData(spec, ManagedGlyphLod.Near);
            Time.frameCount = frame++;
            plain.Draw(spec, ManagedGlyphLod.Near, Matrix4x4.identity, spec.RevealForLevels(1), 1f);
            plain.Draw(spec, ManagedGlyphLod.Near, Matrix4x4.identity, 1f, 1f);
            plain.Flush();
            Eq(Graphics.Calls.Count, 2, "two glyphs are two calls");
            Eq(Graphics.Calls[0].Mesh.triangles.Length, data.LevelIndexEnd[0], "reveal of one level draws level 0 only");
            Eq(Graphics.Calls[1].Mesh.triangles.Length, data.Triangles.Length, "full reveal draws every level");
            Check(Graphics.Calls[0].Floats == null, "the fallback sets no shader numbers");
            plain.Dispose();

            // Without any shader the renderer says so and draws nothing.
            Shader.Known.Remove("Sprites/Default");
            Shader.Known.Remove(ManagedGlyphRenderer.ShaderName);
            Debug.Errors.Clear();
            ManagedGlyphRenderer none = new ManagedGlyphRenderer(0, "none");
            Check(Debug.Errors.Count == 1, "a missing shader logs one error");
            Check(!none.Draw(spec, ManagedGlyphLod.Near, Matrix4x4.identity, 1f, 1f), "and nothing draws");
            Shader.Known.Add("Sprites/Default");
            none.Dispose();
        }
    }
}
