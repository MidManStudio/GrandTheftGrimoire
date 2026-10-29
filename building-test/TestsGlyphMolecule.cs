// Checks for the Molecule glyph style and its stroke font. Verification harness only.
// The look is not checked here: it needs Unity.
using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Glyphs;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        private static ManagedGlyphSpec MoleculeSpec(int seed, int depth, int wheels = 1, ManagedGlyphAtomLook look = ManagedGlyphAtomLook.Letters)
        {
            return new ManagedGlyphSpec
            {
                Seed = seed,
                MaxDepth = depth,
                Radius = 1f,
                WheelCount = wheels,
                Style = ManagedGlyphStyle.Molecule,
                AtomLook = look,
            };
        }

        private static int MoleculeCount(ManagedGlyphStrokeSet set, ManagedGlyphStrokeKind kind)
        {
            int count = 0;
            for (int i = 0; i < set.Strokes.Count; i++)
            {
                if (set.Strokes[i].Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static float MoleculeExtent(ManagedGlyphStrokeSet set)
        {
            float far = 0f;
            for (int i = 0; i < set.Strokes.Count; i++)
            {
                Vector2[] points = set.Strokes[i].Points;
                for (int k = 0; k < points.Length; k++)
                {
                    far = Mathf.Max(far, points[k].magnitude);
                }
            }

            return far;
        }

        private static void GlyphMoleculeTests()
        {
            GlyphFontTests();
            GlyphMoleculeShapeTests();
            GlyphMoleculeLimitTests();
        }

        private static void GlyphFontTests()
        {
            Section("Glyph font");
            string letters = "NOSHClPFB23";
            for (int i = 0; i < letters.Length; i++)
            {
                char c = letters[i];
                Check(ManagedGlyphFont.Has(c), "the font has " + c);
                List<ManagedGlyphFont.Poly> polys = new List<ManagedGlyphFont.Poly>();
                ManagedGlyphFont.Append(c, new Vector2(10f, 20f), 2f, polys);
                Check(polys.Count > 0, c + " draws at least one line");

                float width = ManagedGlyphFont.Width(c) * 2f;
                bool inside = true;
                for (int p = 0; p < polys.Count; p++)
                {
                    inside &= polys[p].Points.Length >= 2;
                    for (int k = 0; k < polys[p].Points.Length; k++)
                    {
                        Vector2 pt = polys[p].Points[k];
                        inside &= pt.x >= 10f - 0.02f && pt.x <= 10f + width + 0.02f && pt.y >= 20f - 0.02f && pt.y <= 22f + 0.02f;
                    }
                }

                Check(inside, c + " stays inside its cell");
            }

            List<ManagedGlyphFont.Poly> o = new List<ManagedGlyphFont.Poly>();
            ManagedGlyphFont.Append('O', Vector2.zero, 1f, o);
            Check(o.Count == 1 && o[0].Closed, "O is one closed line");
            List<ManagedGlyphFont.Poly> h = new List<ManagedGlyphFont.Poly>();
            ManagedGlyphFont.Append('H', Vector2.zero, 1f, h);
            Eq(h.Count, 3, "H is two uprights and a bar");

            Check(!ManagedGlyphFont.Has('x'), "a letter the font lacks is reported");
            List<ManagedGlyphFont.Poly> none = new List<ManagedGlyphFont.Poly>();
            ManagedGlyphFont.Append('x', Vector2.zero, 1f, none);
            Eq(none.Count, 0, "and draws nothing");
            Near(ManagedGlyphFont.Width('x'), 0f, "with no width");

            // The cell scales with the height and moves with the origin.
            List<ManagedGlyphFont.Poly> a = new List<ManagedGlyphFont.Poly>();
            List<ManagedGlyphFont.Poly> b = new List<ManagedGlyphFont.Poly>();
            ManagedGlyphFont.Append('N', Vector2.zero, 1f, a);
            ManagedGlyphFont.Append('N', new Vector2(3f, 4f), 2f, b);
            Vector2 a1 = a[0].Points[1];
            Vector2 b1 = b[0].Points[1];
            Near(b1.x, 3f + a1.x * 2f, "the origin moves the letter and the height scales it (x)");
            Near(b1.y, 4f + a1.y * 2f, "the origin moves the letter and the height scales it (y)");
        }

        private static void GlyphMoleculeShapeTests()
        {
            Section("Glyph molecule shape");

            ManagedGlyphSpec spec = MoleculeSpec(3, 4);
            ManagedGlyphStrokeSet a = ManagedGlyphGenerator.Generate(spec, ManagedGlyphLod.Near);
            ManagedGlyphStrokeSet b = ManagedGlyphGenerator.Generate(spec, ManagedGlyphLod.Near);
            Eq(a.Strokes.Count, b.Strokes.Count, "the same spec gives the same strokes");
            Eq(a.PointCount, b.PointCount, "and the same points");
            Check(a.Strokes[0].Points[0].x == b.Strokes[0].Points[0].x && a.Strokes[a.Strokes.Count - 1].Points[0].y == b.Strokes[b.Strokes.Count - 1].Points[0].y,
                "to the last coordinate");
            Check(ManagedGlyphGenerator.Generate(MoleculeSpec(4, 4), ManagedGlyphLod.Near).PointCount != a.PointCount
                || ManagedGlyphGenerator.Generate(MoleculeSpec(5, 4), ManagedGlyphLod.Near).PointCount != a.PointCount, "another seed gives another molecule");

            // A molecule has no circle-style figures.
            ManagedGlyphStrokeKind[] circleOnly =
            {
                ManagedGlyphStrokeKind.Ring, ManagedGlyphStrokeKind.Edge, ManagedGlyphStrokeKind.Rosette, ManagedGlyphStrokeKind.Chord,
                ManagedGlyphStrokeKind.Nest, ManagedGlyphStrokeKind.Curl, ManagedGlyphStrokeKind.Drip,
            };
            int bonds = 0;
            int letters = 0;
            bool onlyMolecule = true;
            bool windowsOk = true;
            bool levelsOk = true;
            bool nearZero = true;
            bool lettersNextColor = true;
            ManagedGlyphPalette palette = ManagedGlyphPalette.Fire;
            for (int seed = 1; seed <= 12; seed++)
            {
                ManagedGlyphSpec s = MoleculeSpec(seed, 4);
                ManagedGlyphStrokeSet set = ManagedGlyphGenerator.Generate(s, ManagedGlyphLod.Near);
                float slice = 1f / s.LevelCount;
                for (int i = 0; i < set.Strokes.Count; i++)
                {
                    ManagedGlyphStroke k = set.Strokes[i];
                    for (int c = 0; c < circleOnly.Length; c++)
                    {
                        onlyMolecule &= k.Kind != circleOnly[c];
                    }

                    levelsOk &= k.Level >= 0 && k.Level <= s.Depth;
                    windowsOk &= k.T0 >= k.Level * slice - 1e-4f && k.T1 <= (k.Level + 1) * slice + 1e-4f && k.T0 <= k.T1;
                    nearZero &= Mathf.Abs(k.Z) < 1e-5f;
                    if (k.Kind == ManagedGlyphStrokeKind.Bond)
                    {
                        bonds++;
                    }

                    if (k.Kind == ManagedGlyphStrokeKind.Letter)
                    {
                        letters++;
                        Color32 want = palette.ColorFor(k.Level + 1);
                        Color32 got = k.Tint;
                        lettersNextColor &= want.r == got.r && want.g == got.g && want.b == got.b;
                    }
                }
            }

            Check(onlyMolecule, "no circle-style stroke kinds in a molecule");
            Check(bonds > 100, "bonds are the bulk of it");
            Check(letters > 20, "element letters appear");
            Check(levelsOk, "every stroke has a level inside the depth");
            Check(windowsOk, "every stroke draws inside its level's slice of the reveal");
            Check(nearZero, "one wheel is flat at z 0");
            Check(lettersNextColor, "letters take the next palette color, so atoms stand out from bonds");

            // The root ring is level 0 and has six bonds, or five in a five ring. Deeper levels add strokes.
            ManagedGlyphStrokeSet root = ManagedGlyphGenerator.Generate(MoleculeSpec(3, 0), ManagedGlyphLod.Near);
            bool onlyZero = true;
            for (int i = 0; i < root.Strokes.Count; i++)
            {
                onlyZero &= root.Strokes[i].Level == 0;
            }

            Check(onlyZero, "depth 0 is the root ring alone");
            Check(MoleculeCount(root, ManagedGlyphStrokeKind.Bond) >= 5, "the root ring has its bonds");
            int small = 0;
            int large = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                small += ManagedGlyphGenerator.Generate(MoleculeSpec(seed, 1), ManagedGlyphLod.Near).Strokes.Count;
                large += ManagedGlyphGenerator.Generate(MoleculeSpec(seed, 5), ManagedGlyphLod.Near).Strokes.Count;
            }

            Check(large > small * 2, "deeper molecules have many more strokes");

            // Shells draw on one after another inside a level.
            ManagedGlyphStrokeSet shell = ManagedGlyphGenerator.Generate(MoleculeSpec(3, 4), ManagedGlyphLod.Near);
            List<float> starts = new List<float>();
            for (int i = 0; i < shell.Strokes.Count; i++)
            {
                if (shell.Strokes[i].Level == 1 && shell.Strokes[i].Kind == ManagedGlyphStrokeKind.Bond)
                {
                    float t = (float)Math.Round(shell.Strokes[i].T0, 3);
                    if (!starts.Contains(t))
                    {
                        starts.Add(t);
                    }
                }
            }

            Check(starts.Count >= 4, "the bonds of one level start at different times");

            // Atom look.
            ManagedGlyphStrokeSet letterSet = ManagedGlyphGenerator.Generate(MoleculeSpec(2, 4, 1, ManagedGlyphAtomLook.Letters), ManagedGlyphLod.Near);
            ManagedGlyphStrokeSet ballSet = ManagedGlyphGenerator.Generate(MoleculeSpec(2, 4, 1, ManagedGlyphAtomLook.Balls), ManagedGlyphLod.Near);
            Check(MoleculeCount(letterSet, ManagedGlyphStrokeKind.Letter) > 0 && MoleculeCount(letterSet, ManagedGlyphStrokeKind.Atom) == 0, "the letter look draws letters and no balls");
            Check(MoleculeCount(ballSet, ManagedGlyphStrokeKind.Atom) > 0 && MoleculeCount(ballSet, ManagedGlyphStrokeKind.Letter) == 0, "the ball look draws balls and no letters");

            // Bakes like any glyph.
            ManagedGlyphMeshData mesh = ManagedGlyphBaker.Bake(MoleculeSpec(3, 4), ManagedGlyphLod.Near);
            Check(mesh.VertexCount > 0 && mesh.TriangleCount > 0, "a molecule bakes to a mesh");
            Eq(mesh.LevelIndexEnd.Length, 5, "with one index end per level");
            bool inRange = true;
            for (int i = 0; i < mesh.Triangles.Length; i++)
            {
                inRange &= mesh.Triangles[i] >= 0 && mesh.Triangles[i] < mesh.VertexCount;
            }

            Check(inRange, "every triangle index is inside the vertices");

            // The style and the atom look change the cache key, so a circle and a molecule never share a mesh.
            ManagedGlyphSpec circle = GlyphSpec(3, 4);
            ManagedGlyphSpec molecule = MoleculeSpec(3, 4);
            ManagedGlyphSpec balls = MoleculeSpec(3, 4, 1, ManagedGlyphAtomLook.Balls);
            circle.Radius = molecule.Radius = balls.Radius = 1f;
            Check(circle.CacheKey(ManagedGlyphLod.Near) != molecule.CacheKey(ManagedGlyphLod.Near), "the style is part of the cache key");
            Check(molecule.CacheKey(ManagedGlyphLod.Near) != balls.CacheKey(ManagedGlyphLod.Near), "and so is the atom look");
            Eq(new ManagedGlyphSpec().Style, ManagedGlyphStyle.Circle, "a new spec is still a circle");

            // Wheels stack along z like the circle style.
            ManagedGlyphStrokeSet two = ManagedGlyphGenerator.Generate(MoleculeSpec(3, 3, 2), ManagedGlyphLod.Near);
            float topZ = 0f;
            for (int i = 0; i < two.Strokes.Count; i++)
            {
                topZ = Mathf.Max(topZ, two.Strokes[i].Z);
            }

            Near(topZ, new ManagedGlyphSpec().WheelSpacing * 1f, "the second wheel sits one wheel spacing back");
        }

        private static void GlyphMoleculeLimitTests()
        {
            Section("Glyph molecule limits");
            int[] budget = { 9000, 2400, 600 };
            ManagedGlyphLod[] lods = { ManagedGlyphLod.Near, ManagedGlyphLod.Mid, ManagedGlyphLod.Far };
            float widest = 0f;
            int worstNear = 0;
            for (int l = 0; l < 3; l++)
            {
                bool underBudget = true;
                for (int seed = 1; seed <= 30; seed++)
                {
                    ManagedGlyphSpec spec = MoleculeSpec(seed, 7, 1 + seed % 3);
                    ManagedGlyphStrokeSet set = ManagedGlyphGenerator.Generate(spec, lods[l]);
                    underBudget &= set.PointCount <= budget[l] + 80;
                    if (l == 0)
                    {
                        worstNear = Math.Max(worstNear, set.PointCount);
                        widest = Mathf.Max(widest, MoleculeExtent(set));
                    }
                }

                Check(underBudget, lods[l] + " stays inside its point budget");
            }

            Check(widest < 20f, "a depth 7 molecule stays under 20 times the radius");
            Console.WriteLine("glyph molecule at depth 7: worst near points " + worstNear + ", widest " + widest.ToString("F1") + " x radius");

            // Far has no letters or balls, Mid has letters.
            ManagedGlyphStrokeSet far = ManagedGlyphGenerator.Generate(MoleculeSpec(2, 7), ManagedGlyphLod.Far);
            Eq(MoleculeCount(far, ManagedGlyphStrokeKind.Letter) + MoleculeCount(far, ManagedGlyphStrokeKind.Atom), 0, "Far draws bonds only");
            bool farDepth = true;
            for (int i = 0; i < far.Strokes.Count; i++)
            {
                farDepth &= far.Strokes[i].Level <= 2;
            }

            Check(farDepth, "and stops at its depth cap");
            int midLetters = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                midLetters += MoleculeCount(ManagedGlyphGenerator.Generate(MoleculeSpec(seed, 4), ManagedGlyphLod.Mid), ManagedGlyphStrokeKind.Letter);
            }

            Check(midLetters > 0, "Mid still has letters");

            // Zero radius and extreme values do not break it.
            ManagedGlyphSpec odd = MoleculeSpec(9, 9, 5);
            ManagedGlyphStrokeSet clamped = ManagedGlyphGenerator.Generate(odd, ManagedGlyphLod.Near);
            Check(clamped.Strokes.Count > 0, "a depth and wheel count past the limits are clamped");
            Eq(clamped.LevelCount, ManagedGlyphLimits.MaxDepth + 1, "to the depth limit");
        }
    }
}
