// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphMolecule.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Glyphs
{
    /// <summary>
    /// Builds the strokes of a Molecule glyph: a chemistry diagram that grows outward from a root ring.
    /// Level 0 is the root ring, a hexagon (or a five ring) with alternating double bonds, a few ring atoms swapped for
    /// N, O or S. Each level after that is one shell of growth: every open
    /// vertex or edge of the previous shell may take a substituent (=O, OH, NH2, Cl, F, SH, CH3), a zigzag chain of one
    /// to three bonds, a ring joined by a single bond, or a ring fused along an edge. A chain may end open, so the next
    /// shell grows from its tip. Pieces that would overlap an earlier piece are skipped.
    /// Atoms are element letters (a stroke font, see <see cref="ManagedGlyphFont"/>) or small glowing balls.
    /// Pure arithmetic with no Unity objects, so it runs in the CI harness.
    /// </summary>
    public static class ManagedGlyphMolecule
    {
        private const float TwoPi = Mathf.PI * 2f;
        private const float SixtyDegrees = Mathf.PI / 3f;

        // Height of an element letter and radius of an atom ball, as fractions of one bond length.
        private const float LetterHeightPerBond = 0.5f;
        private const float BallRadiusPerBond = 0.17f;

        // What an atom takes away from the end of a bond, as a fraction of the letter height or of a bond.
        private const float LetterGapPerHeight = 0.62f;

        private struct Disc
        {
            public Vector2 Center;
            public float Radius;
            public int Owner;
        }

        // An open place where a piece can grow. A vertex site has a position and an outward direction. An edge site is a free ring edge.
        private sealed class Site
        {
            public Vector2 Pos;
            public Vector2 Dir;
            public int Owner;
            public bool Edge;
            public Vector2 A;
            public Vector2 B;
            public bool Used;

            // The ring the site belongs to and the vertex (or the edge from this vertex to the next), or null for the tip of a chain.
            public Ring Ring;
            public int Index;
        }

        private sealed class Ring
        {
            public Vector2 Center;
            public float Radius;
            public int N;
            public int Owner;
            public Vector2[] V;
            public bool[] Hetero;
            public bool[] Used;
            public int SkipEdge;
        }

        // A group that closes a bond: atom symbol, hydrogens, order of the bond that reaches it, and its share of the picks.
        private struct Substituent
        {
            public string Atom;
            public int Hydrogens;
            public int Order;
            public float Weight;

            public Substituent(string atom, int hydrogens, int order, float weight)
            {
                Atom = atom;
                Hydrogens = hydrogens;
                Order = order;
                Weight = weight;
            }
        }

        private static readonly Substituent[] Substituents =
        {
            new Substituent("O", 0, 2, 0.22f),
            new Substituent("O", 1, 1, 0.20f),
            new Substituent("N", 2, 1, 0.16f),
            new Substituent("Cl", 0, 1, 0.14f),
            new Substituent("F", 0, 1, 0.10f),
            new Substituent("S", 1, 1, 0.06f),
            new Substituent("C", 3, 1, 0.08f),
            new Substituent("P", 2, 1, 0.04f),
        };

        private sealed class Context
        {
            public ManagedGlyphSpec Spec;
            public ManagedGlyphLodSettings Settings;
            public ManagedGlyphStrokeSet Set;
            public ManagedGlyphPalette Palette;
            public ManagedGlyphOrnaments Allowed;
            public ManagedGlyphRandom Rng;
            public int Wheel;
            public float Z;
            public int Depth;
            public int Cap;
            public float GlyphRadius;
            public int PointLimit;

            public float Bond;
            public float LetterHeight;
            public bool Letters;
            public bool Balls;
            public float Gap;

            // The part of the level's slice that the piece being drawn owns, so pieces of one shell draw on one after another.
            public float FragStart;
            public float FragSpan = 1f;

            public readonly List<Disc> Discs = new List<Disc>();
            public int NextOwner;
        }

        public static ManagedGlyphStrokeSet Generate(ManagedGlyphSpec spec, ManagedGlyphLod lod)
        {
            ManagedGlyphLodSettings settings = ManagedGlyphLodSettings.For(lod);
            int depth = spec.Depth;
            int cap = Mathf.Min(depth, settings.DepthCap);
            int wheels = Mathf.Min(spec.Wheels, settings.WheelCap);
            ManagedGlyphPalette palette = spec.Palette ?? ManagedGlyphPalette.Fire;
            ManagedGlyphOrnaments allowed = spec.Ornaments & settings.Allowed;

            ManagedGlyphStrokeSet set = new ManagedGlyphStrokeSet(depth + 1);
            int pointBudget = settings.MaxPoints / wheels;

            for (int w = 0; w < wheels; w++)
            {
                Context ctx = new Context
                {
                    Spec = spec,
                    Settings = settings,
                    Set = set,
                    Palette = palette,
                    Allowed = allowed,
                    Rng = new ManagedGlyphRandom(unchecked(spec.Seed * 17 + w * 6151 + 29)),
                    Wheel = w,
                    Z = w * spec.WheelSpacing * spec.Radius,
                    Depth = depth,
                    Cap = cap,
                    GlyphRadius = spec.Radius * (1f - 0.12f * w),
                    PointLimit = set.PointCount + pointBudget,
                };

                bool labels = (allowed & ManagedGlyphOrnaments.Label) != 0;
                ctx.Letters = labels && spec.AtomLook == ManagedGlyphAtomLook.Letters;
                ctx.Balls = labels && spec.AtomLook == ManagedGlyphAtomLook.Balls;

                BuildWheel(ctx);
            }

            return set;
        }

        // ---- growth -----------------------------------------------------------------------------------------

        private static void BuildWheel(Context ctx)
        {
            ManagedGlyphRandom rng = ctx.Rng;
            int n = rng.Chance(0.22f) ? 5 : 6;
            ctx.Bond = ctx.GlyphRadius * 2f * Mathf.Sin(Mathf.PI / n);
            ctx.LetterHeight = ctx.Bond * LetterHeightPerBond;
            ctx.Gap = ctx.Letters ? ctx.LetterHeight * LetterGapPerHeight : (ctx.Balls ? ctx.Bond * BallRadiusPerBond : 0f);

            ctx.FragStart = 0f;
            ctx.FragSpan = 1f;
            int heteroCount = n == 6 ? PickRootHetero(rng) : 1;
            Ring root = EmitRing(ctx, Vector2.zero, ctx.GlyphRadius, n, rng.Range(0f, TwoPi), rng.Chance(0.5f) ? 1f : -1f, 0, -1, 0, heteroCount);
            ctx.Set.NodeCount++;

            List<Site> sites = new List<Site>();
            AddRingSites(root, sites);
            Shuffle(sites, rng);

            for (int level = 1; level <= ctx.Cap; level++)
            {
                if (sites.Count == 0)
                {
                    return;
                }

                if (ctx.Set.PointCount >= ctx.PointLimit)
                {
                    ctx.Set.Truncated = true;
                    return;
                }

                List<Site> fresh = new List<Site>();
                int want = FragmentCount(rng, level);
                int made = 0;
                for (int i = 0; i < sites.Count && made < want; i++)
                {
                    if (ctx.Set.PointCount >= ctx.PointLimit)
                    {
                        ctx.Set.Truncated = true;
                        break;
                    }

                    ctx.FragStart = 0.05f + 0.5f * made / want;
                    ctx.FragSpan = 0.45f;
                    if (TryGrow(ctx, sites[i], level, fresh))
                    {
                        sites[i].Used = true;
                        made++;
                        ctx.Set.NodeCount++;
                    }
                }

                // New sites first, so the molecule keeps growing outward. Unused older sites follow as a reserve.
                Shuffle(fresh, rng);
                List<Site> leftover = new List<Site>();
                for (int i = 0; i < sites.Count; i++)
                {
                    if (!sites[i].Used)
                    {
                        leftover.Add(sites[i]);
                    }
                }

                fresh.AddRange(leftover);
                sites = fresh;
            }
        }

        private static int PickRootHetero(ManagedGlyphRandom rng)
        {
            float r = rng.NextFloat();
            return r < 0.35f ? 0 : (r < 0.8f ? 1 : 2);
        }

        private static int FragmentCount(ManagedGlyphRandom rng, int level)
        {
            if (level <= 2)
            {
                return 3 + (rng.Chance(0.4f) ? 1 : 0);
            }

            if (level <= 4)
            {
                return 2 + (rng.Chance(0.4f) ? 1 : 0);
            }

            return 1 + (rng.Chance(0.5f) ? 1 : 0);
        }

        // The vertices of a ring that something has grown from are closed. The site list of an older shell may still hold them.
        private static bool SiteOpen(Site site)
        {
            if (site.Ring == null)
            {
                return true;
            }

            if (site.Edge)
            {
                return !site.Ring.Used[site.Index] && !site.Ring.Used[(site.Index + 1) % site.Ring.N];
            }

            return !site.Ring.Used[site.Index];
        }

        private static void CloseSite(Site site)
        {
            if (site.Ring == null)
            {
                return;
            }

            site.Ring.Used[site.Index] = true;
            if (site.Edge)
            {
                site.Ring.Used[(site.Index + 1) % site.Ring.N] = true;
            }
        }

        private static bool TryGrow(Context ctx, Site site, int level, List<Site> fresh)
        {
            if (!SiteOpen(site))
            {
                return false;
            }

            bool done = GrowAt(ctx, site, level, fresh);
            if (done)
            {
                CloseSite(site);
            }

            return done;
        }

        private static bool GrowAt(Context ctx, Site site, int level, List<Site> fresh)
        {
            if (site.Edge)
            {
                return Fuse(ctx, site, level, fresh);
            }

            float r = ctx.Rng.NextFloat();
            int first = level >= ctx.Cap ? (r < 0.5f ? 0 : 1) : (r < 0.38f ? 1 : (r < 0.62f ? 0 : 2));
            for (int k = 0; k < 3; k++)
            {
                int kind = (first + k) % 3;
                bool done = kind == 0 ? Substitute(ctx, site, level) : (kind == 1 ? Chain(ctx, site, level, fresh) : Link(ctx, site, level, fresh));
                if (done)
                {
                    return true;
                }
            }

            return false;
        }

        // A bond ending in an atom group that closes the site.
        private static bool Substitute(Context ctx, Site site, int level)
        {
            Substituent sub = PickSubstituent(ctx.Rng);
            Vector2 end = site.Pos + site.Dir * ctx.Bond;
            if (!Free(ctx, end, ctx.Bond * 0.4f, site.Owner))
            {
                return false;
            }

            Occupy(ctx, end, ctx.Bond * 0.4f, ++ctx.NextOwner);
            float trim = AtomTrim(ctx);
            EmitBond(ctx, site.Pos, end, 0f, trim, sub.Order, false, Vector2.zero, level, 0f, 0.3f);
            EmitSubstituent(ctx, sub, end, site.Dir, level);
            MaybeDot(ctx, end, site.Dir, level);
            return true;
        }

        // A zigzag chain of one to three bonds from the site. It ends in an atom group, or open so the next shell grows from its tip.
        private static bool Chain(Context ctx, Site site, int level, List<Site> fresh)
        {
            ManagedGlyphRandom rng = ctx.Rng;
            int bonds = 1 + rng.NextInt(3);
            float sign = rng.Chance(0.5f) ? 1f : -1f;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                Vector2 alt = Rotate(site.Dir, sign * SixtyDegrees);
                Vector2[] points = new Vector2[bonds + 1];
                Vector2[] dirs = new Vector2[bonds];
                points[0] = site.Pos;
                bool fits = true;
                for (int i = 0; i < bonds; i++)
                {
                    dirs[i] = i % 2 == 0 ? site.Dir : alt;
                    points[i + 1] = points[i] + dirs[i] * ctx.Bond;
                    if (!Free(ctx, points[i + 1], ctx.Bond * 0.3f, site.Owner))
                    {
                        fits = false;
                        break;
                    }
                }

                if (!fits)
                {
                    sign = -sign;
                    continue;
                }

                int owner = ++ctx.NextOwner;
                for (int i = 1; i <= bonds; i++)
                {
                    Occupy(ctx, points[i], ctx.Bond * 0.3f, owner);
                }

                bool terminal = level >= ctx.Cap || rng.Chance(0.45f);
                Substituent sub = default(Substituent);
                if (terminal)
                {
                    sub = PickSubstituent(rng);
                }

                int doubled = bonds > 1 && rng.Chance(0.2f) ? rng.NextInt(bonds - 1) : -1;
                for (int i = 0; i < bonds; i++)
                {
                    bool last = i == bonds - 1;
                    int order = i == doubled ? 2 : (last && terminal ? sub.Order : 1);
                    float trimEnd = last && terminal ? AtomTrim(ctx) : 0f;
                    float a = 0.1f + 0.5f * i / bonds;
                    EmitBond(ctx, points[i], points[i + 1], 0f, trimEnd, order, false, Vector2.zero, level, a, a + 0.3f);
                }

                if (terminal)
                {
                    EmitSubstituent(ctx, sub, points[bonds], dirs[bonds - 1], level);
                }
                else
                {
                    Vector2 next = bonds % 2 == 0 ? site.Dir : alt;
                    fresh.Add(new Site { Pos = points[bonds], Dir = next, Owner = owner });
                    if (ctx.Balls)
                    {
                        EmitBall(ctx, points[bonds], level, 0.6f, false, 0.55f, 0.85f);
                    }
                }

                MaybeDot(ctx, points[bonds], dirs[bonds - 1], level);
                return true;
            }

            return false;
        }

        // A ring joined to the site by one bond.
        private static bool Link(Context ctx, Site site, int level, List<Site> fresh)
        {
            ManagedGlyphRandom rng = ctx.Rng;
            int n = rng.Chance(0.3f) ? 5 : 6;
            float radius = ctx.Bond / (2f * Mathf.Sin(Mathf.PI / n));
            Vector2 end = site.Pos + site.Dir * ctx.Bond;
            Vector2 center = end + site.Dir * radius;
            if (!Free(ctx, center, radius * 0.92f, site.Owner))
            {
                return false;
            }

            EmitBond(ctx, site.Pos, end, 0f, 0f, 1, false, Vector2.zero, level, 0f, 0.3f);
            float angle0 = Mathf.Atan2(-site.Dir.y, -site.Dir.x);
            Ring ring = EmitRing(ctx, center, radius, n, angle0, rng.Chance(0.5f) ? 1f : -1f, level, -1, 1, rng.Chance(0.3f) ? 1 : 0);
            ring.Used[0] = true;
            AddRingSites(ring, fresh);
            return true;
        }

        // A ring that shares an edge with the ring the site belongs to.
        private static bool Fuse(Context ctx, Site site, int level, List<Site> fresh)
        {
            ManagedGlyphRandom rng = ctx.Rng;
            int n = rng.Chance(0.35f) ? 5 : 6;
            float radius = ctx.Bond / (2f * Mathf.Sin(Mathf.PI / n));
            float apothem = radius * Mathf.Cos(Mathf.PI / n);
            Vector2 mid = (site.A + site.B) * 0.5f;
            Vector2 center = mid + site.Dir * apothem;
            if (!Free(ctx, center, radius * 0.92f, site.Owner))
            {
                return false;
            }

            Vector2 fromA = site.A - center;
            Vector2 fromB = site.B - center;
            float sign = fromA.x * fromB.y - fromA.y * fromB.x >= 0f ? 1f : -1f;
            float angle0 = Mathf.Atan2(fromA.y, fromA.x);
            Ring ring = EmitRing(ctx, center, radius, n, angle0, sign, level, 0, 2, rng.Chance(0.3f) ? 1 : 0);
            ring.Used[0] = true;
            ring.Used[1] = true;
            AddRingSites(ring, fresh);
            return true;
        }

        // ---- pieces -----------------------------------------------------------------------------------------

        // A ring of n bonds. Vertex 0 sits at angle0 and the others follow in direction sign. Edge skipEdge is not drawn
        // (a fused ring shares it with its parent). The first protectedVertices vertices never become hetero atoms.
        private static Ring EmitRing(Context ctx, Vector2 center, float radius, int n, float angle0, float sign, int level,
            int skipEdge, int protectedVertices, int heteroCount)
        {
            ManagedGlyphRandom rng = ctx.Rng;
            Ring ring = new Ring
            {
                Center = center,
                Radius = radius,
                N = n,
                Owner = ++ctx.NextOwner,
                V = new Vector2[n],
                Hetero = new bool[n],
                Used = new bool[n],
                SkipEdge = skipEdge,
            };

            for (int i = 0; i < n; i++)
            {
                float a = angle0 + sign * TwoPi * i / n;
                ring.V[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }

            Occupy(ctx, center, radius, ring.Owner);

            // Hetero atoms: nitrogen in a six ring, N, O or S in a five ring.
            int[] atomOf = new int[n];
            for (int h = 0; h < heteroCount; h++)
            {
                for (int tries = 0; tries < 6; tries++)
                {
                    int v = protectedVertices + rng.NextInt(n - protectedVertices);
                    if (!ring.Hetero[v])
                    {
                        ring.Hetero[v] = true;
                        atomOf[v] = n == 6 ? 0 : rng.NextInt(3);
                        break;
                    }
                }
            }

            // Alternating double bonds, as in a Kekule drawing.
            bool[] doubled = new bool[n];
            int start = skipEdge >= 0 ? 1 : rng.NextInt(n);
            if (n == 6)
            {
                for (int e = start % 2; e < n; e += 2)
                {
                    doubled[e] = true;
                }
            }
            else
            {
                doubled[start % n] = true;
                doubled[(start + 2) % n] = true;
            }

            for (int e = 0; e < n; e++)
            {
                if (e == skipEdge)
                {
                    continue;
                }

                int next = (e + 1) % n;
                Vector2 mid = (ring.V[e] + ring.V[next]) * 0.5f;
                float trimA = ring.Hetero[e] ? ctx.Gap : 0f;
                float trimB = ring.Hetero[next] ? ctx.Gap : 0f;
                float a0 = 0.15f + 0.5f * e / n;
                EmitBond(ctx, ring.V[e], ring.V[next], trimA, trimB, doubled[e] ? 2 : 1, true, center - mid, level, a0, a0 + 0.3f);
            }

            for (int i = 0; i < n; i++)
            {
                if (ring.Hetero[i])
                {
                    Vector2 outward = (ring.V[i] - center).normalized;
                    EmitRingAtom(ctx, atomOf[i], n, ring.V[i], outward, level);
                }
                else if (ctx.Balls)
                {
                    EmitBall(ctx, ring.V[i], level, 0.6f, false, 0.55f, 0.85f);
                }
            }

            MaybeDot(ctx, ring.V[rng.NextInt(n)], (ring.V[0] - center).normalized, level);
            return ring;
        }

        private static void AddRingSites(Ring ring, List<Site> sites)
        {
            int n = ring.N;
            for (int i = 0; i < n; i++)
            {
                if (ring.Hetero[i] || ring.Used[i])
                {
                    continue;
                }

                sites.Add(new Site { Pos = ring.V[i], Dir = (ring.V[i] - ring.Center).normalized, Owner = ring.Owner, Ring = ring, Index = i });
            }

            for (int e = 0; e < n; e++)
            {
                int next = (e + 1) % n;
                // An edge that touches a vertex another ring already uses would put three rings round one vertex.
                if (e == ring.SkipEdge || ring.Hetero[e] || ring.Hetero[next] || ring.Used[e] || ring.Used[next])
                {
                    continue;
                }

                Vector2 mid = (ring.V[e] + ring.V[next]) * 0.5f;
                sites.Add(new Site { Edge = true, A = ring.V[e], B = ring.V[next], Dir = (mid - ring.Center).normalized, Owner = ring.Owner, Ring = ring, Index = e });
            }
        }

        private static void EmitRingAtom(Context ctx, int atom, int ringSize, Vector2 pos, Vector2 outward, int level)
        {
            string symbol = atom == 0 ? "N" : (atom == 1 ? "O" : "S");
            bool withHydrogen = atom == 0 && ringSize == 5;
            if (ctx.Letters)
            {
                EmitText(ctx, symbol, 0, pos, level, 0.65f, 1f);
                if (withHydrogen)
                {
                    EmitText(ctx, "H", 0, pos + outward * (ctx.LetterHeight * 1.15f), level, 0.75f, 1f);
                }
            }
            else if (ctx.Balls)
            {
                EmitBall(ctx, pos, level, 1f, true, 0.55f, 0.9f);
            }
        }

        private static void EmitSubstituent(Context ctx, Substituent sub, Vector2 pos, Vector2 dir, int level)
        {
            if (ctx.Letters)
            {
                // A group on the left reads backwards, as in a drawing: HO and H2N.
                bool mirrored = dir.x < -0.35f && sub.Hydrogens > 0;
                string text;
                int anchor;
                string hydrogen = sub.Hydrogens == 0 ? string.Empty : (sub.Hydrogens == 1 ? "H" : "H" + sub.Hydrogens);
                if (mirrored)
                {
                    text = hydrogen + sub.Atom;
                    anchor = hydrogen.Length;
                }
                else
                {
                    text = sub.Atom + hydrogen;
                    anchor = 0;
                }

                EmitText(ctx, text, anchor, pos, level, 0.65f, 1f);
            }
            else if (ctx.Balls)
            {
                bool carbon = sub.Atom == "C";
                EmitBall(ctx, pos, level, sub.Atom == "Cl" ? 1.2f : 1f, !carbon, 0.55f, 0.9f);
            }
        }

        // Letters in a row, left to right. The letter at anchorIndex is centered on the anchor point. Digits are small and sit low.
        private static void EmitText(Context ctx, string text, int anchorIndex, Vector2 anchor, int level, float a, float b)
        {
            int count = text.Length;
            float[] x = new float[count];
            float[] width = new float[count];
            float cursor = 0f;
            for (int i = 0; i < count; i++)
            {
                float scale = char.IsDigit(text[i]) ? 0.62f : 1f;
                width[i] = ManagedGlyphFont.Width(text[i]) * scale;
                x[i] = cursor;
                cursor += width[i] + ManagedGlyphFont.Gap * scale;
            }

            float anchorX = x[anchorIndex] + width[anchorIndex] * 0.5f;
            List<ManagedGlyphFont.Poly> polys = new List<ManagedGlyphFont.Poly>();
            for (int i = 0; i < count; i++)
            {
                bool digit = char.IsDigit(text[i]);
                float scale = digit ? 0.62f : 1f;
                float height = ctx.LetterHeight * scale;
                float lowered = digit ? -0.12f * ctx.LetterHeight : 0f;
                Vector2 origin = new Vector2(
                    anchor.x + (x[i] - anchorX) * ctx.LetterHeight,
                    anchor.y - 0.5f * ctx.LetterHeight + lowered);
                ManagedGlyphFont.Append(text[i], origin, height, polys);
            }

            float hw = HalfWidth(ctx, level) * 0.85f;
            for (int i = 0; i < polys.Count; i++)
            {
                AddStroke(ctx, ManagedGlyphStrokeKind.Letter, polys[i].Points, polys[i].Closed, level, hw, a, b, 1);
            }

            Occupy(ctx, anchor, ctx.LetterHeight * 0.9f, ++ctx.NextOwner);
        }

        private static void EmitBall(Context ctx, Vector2 pos, int level, float scale, bool ring, float a, float b)
        {
            float hw = HalfWidth(ctx, level);
            if (ring)
            {
                float radius = ctx.Bond * BallRadiusPerBond * scale;
                int segments = 14;
                Vector2[] points = new Vector2[segments];
                for (int i = 0; i < segments; i++)
                {
                    float t = TwoPi * i / segments;
                    points[i] = pos + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius;
                }

                AddStroke(ctx, ManagedGlyphStrokeKind.Atom, points, true, level, hw * 0.9f, a, b, 1);
            }

            AddDot(ctx, pos, level, hw * (ring ? 1.1f : 1.0f), a, b, ring ? 1 : 0);
        }

        // A bond line, or two or three parallel lines for a double or triple bond. trimA and trimB shorten the ends to
        // leave room for an atom. A double bond inside a ring has its second line on the ring side, shorter at both ends.
        private static void EmitBond(Context ctx, Vector2 a, Vector2 b, float trimA, float trimB, int order, bool inRing,
            Vector2 inward, int level, float ta, float tb)
        {
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 1e-4f)
            {
                return;
            }

            Vector2 dir = delta / length;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float hw = HalfWidth(ctx, level);

            if (order <= 1)
            {
                AddLine(ctx, a + dir * trimA, b - dir * trimB, level, hw, ta, tb);
                return;
            }

            if (order == 2 && inRing)
            {
                AddLine(ctx, a + dir * trimA, b - dir * trimB, level, hw, ta, tb);
                float side = Dot(perp, inward) >= 0f ? 1f : -1f;
                float offset = ctx.Bond * 0.2f;
                float shortA = trimA > 0f ? trimA + ctx.Bond * 0.1f : ctx.Bond * 0.16f;
                float shortB = trimB > 0f ? trimB + ctx.Bond * 0.1f : ctx.Bond * 0.16f;
                AddLine(ctx, a + dir * shortA + perp * (side * offset), b - dir * shortB + perp * (side * offset), level, hw, ta, tb);
                return;
            }

            float gap = order == 2 ? ctx.Bond * 0.1f : ctx.Bond * 0.15f;
            if (order == 2)
            {
                AddLine(ctx, a + dir * trimA + perp * gap, b - dir * trimB + perp * gap, level, hw, ta, tb);
                AddLine(ctx, a + dir * trimA - perp * gap, b - dir * trimB - perp * gap, level, hw, ta, tb);
                return;
            }

            AddLine(ctx, a + dir * trimA, b - dir * trimB, level, hw, ta, tb);
            AddLine(ctx, a + dir * trimA + perp * gap, b - dir * trimB + perp * gap, level, hw, ta, tb);
            AddLine(ctx, a + dir * trimA - perp * gap, b - dir * trimB - perp * gap, level, hw, ta, tb);
        }

        private static void AddLine(Context ctx, Vector2 p0, Vector2 p1, int level, float halfWidth, float ta, float tb)
        {
            if ((p1 - p0).magnitude < ctx.Bond * 0.04f)
            {
                return;
            }

            AddStroke(ctx, ManagedGlyphStrokeKind.Bond, new[] { p0, p1 }, false, level, halfWidth, ta, tb, 0);
        }

        private static void AddDot(Context ctx, Vector2 position, int level, float halfWidth, float a, float b, int tintShift)
        {
            float offset = ctx.GlyphRadius * 0.002f;
            Vector2[] points = { position, position + new Vector2(offset, 0f) };
            AddStroke(ctx, ManagedGlyphStrokeKind.Dot, points, false, level, halfWidth, a, b, tintShift);
        }

        // A loose electron dot beside an atom.
        private static void MaybeDot(Context ctx, Vector2 atom, Vector2 outward, int level)
        {
            if ((ctx.Allowed & ManagedGlyphOrnaments.Dot) == 0 || !ctx.Rng.Chance(0.3f))
            {
                return;
            }

            float angle = ctx.Rng.Range(-0.9f, 0.9f);
            Vector2 dir = Rotate(outward, angle);
            AddDot(ctx, atom + dir * (ctx.Bond * 0.62f), level, HalfWidth(ctx, level) * 1.6f, 0.8f, 1f, 1);
        }

        // ---- helpers ----------------------------------------------------------------------------------------

        private static Substituent PickSubstituent(ManagedGlyphRandom rng)
        {
            float total = 0f;
            for (int i = 0; i < Substituents.Length; i++)
            {
                total += Substituents[i].Weight;
            }

            float r = rng.NextFloat() * total;
            for (int i = 0; i < Substituents.Length; i++)
            {
                r -= Substituents[i].Weight;
                if (r <= 0f)
                {
                    return Substituents[i];
                }
            }

            return Substituents[0];
        }

        // What a bond gives up at the end that meets an atom.
        private static float AtomTrim(Context ctx)
        {
            return ctx.Gap;
        }

        private static bool Free(Context ctx, Vector2 center, float radius, int ignoreOwner)
        {
            for (int i = 0; i < ctx.Discs.Count; i++)
            {
                Disc d = ctx.Discs[i];
                if (d.Owner == ignoreOwner)
                {
                    continue;
                }

                if ((center - d.Center).magnitude < (radius + d.Radius) * 0.95f)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Occupy(Context ctx, Vector2 center, float radius, int owner)
        {
            ctx.Discs.Add(new Disc { Center = center, Radius = radius, Owner = owner });
        }

        private static void Shuffle(List<Site> list, ManagedGlyphRandom rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                Site tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private static float HalfWidth(Context ctx, int level)
        {
            float full = ctx.Spec.LineWidth * ctx.GlyphRadius * Mathf.Lerp(1f, 0.4f, level / (float)ManagedGlyphLimits.MaxDepth);
            return Mathf.Max(full, ctx.GlyphRadius * 0.006f);
        }

        private static float Dot(Vector2 a, Vector2 b)
        {
            return a.x * b.x + a.y * b.y;
        }

        private static Vector2 Rotate(Vector2 v, float angle)
        {
            float c = Mathf.Cos(angle);
            float s = Mathf.Sin(angle);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // Each level owns one slice of the 0 to 1 reveal range. a and b place the stroke inside the part of the slice that the piece owns.
        // tintShift 1 gives the stroke the next palette color, which is how atoms stand out from bonds.
        private static void AddStroke(Context ctx, ManagedGlyphStrokeKind kind, Vector2[] points, bool closed, int level,
            float halfWidth, float a, float b, int tintShift)
        {
            float slice = 1f / (ctx.Depth + 1);
            float ta = ctx.FragStart + ctx.FragSpan * Mathf.Clamp01(a);
            float tb = ctx.FragStart + ctx.FragSpan * Mathf.Clamp01(b);
            ManagedGlyphStroke stroke = new ManagedGlyphStroke
            {
                Kind = kind,
                Points = points,
                Closed = closed,
                Level = level,
                Z = ctx.Z,
                Tint = ctx.Palette.ColorFor(level + ctx.Wheel + tintShift),
                HalfWidth = halfWidth,
                T0 = (level + Mathf.Clamp01(ta)) * slice,
                T1 = (level + Mathf.Clamp01(tb)) * slice,
            };

            ctx.Set.Strokes.Add(stroke);
            ctx.Set.PointCount += points.Length;
        }
    }
}
