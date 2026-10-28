// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphGenerator.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Glyphs
{
    /// <summary>Small seeded generator (xorshift32). It never touches UnityEngine.Random, so a seed always gives the same glyph.</summary>
    public sealed class ManagedGlyphRandom
    {
        private uint _state;

        public ManagedGlyphRandom(int seed)
        {
            unchecked
            {
                _state = (uint)seed * 747796405u + 2891336453u;
                if (_state == 0u)
                {
                    _state = 0x9E3779B9u;
                }

                for (int i = 0; i < 4; i++)
                {
                    NextUInt();
                }
            }
        }

        public uint NextUInt()
        {
            unchecked
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return _state;
            }
        }

        /// <summary>0 up to but not including 1.</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>0 up to but not including <paramref name="maxExclusive"/>.</summary>
        public int NextInt(int maxExclusive)
        {
            return maxExclusive <= 1 ? 0 : (int)(NextUInt() % (uint)maxExclusive);
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }
    }

    /// <summary>
    /// Builds the strokes of a glyph. Pure arithmetic: no Unity objects, so it runs in the CI harness.
    /// A glyph is a tree of nodes. Each node is one to three concentric rings, optionally a figure
    /// inside (rosette, star polygon, harmonograph nest) and a few curls, drips and dots outside.
    /// A node with depth left grows two or three smaller child nodes, each joined by a hand-drawn line
    /// that leaves the rim along the radius, which is the normal of the circle there.
    /// </summary>
    public static class ManagedGlyphGenerator
    {
        private const float TwoPi = Mathf.PI * 2f;

        // Hypotrochoid (big circle K, rolling circle m) and epitrochoid pairs. Every pair is coprime, so the curve closes after m turns.
        private static readonly int[,] HypoPairs = { { 5, 2 }, { 7, 2 }, { 7, 3 }, { 8, 3 }, { 5, 3 }, { 9, 4 }, { 9, 2 }, { 11, 3 } };

        private static readonly int[,] EpiPairs = { { 3, 1 }, { 4, 1 }, { 5, 1 }, { 5, 2 }, { 7, 2 }, { 7, 3 } };

        // Star polygons {n/k}.
        private static readonly int[,] StarPairs = { { 5, 2 }, { 7, 2 }, { 7, 3 }, { 8, 3 }, { 9, 2 }, { 9, 4 }, { 6, 2 } };

        private struct Node
        {
            public Vector2 Center;
            public float Radius;
            public float Heading;
            public bool IsRoot;
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
                    Rng = new ManagedGlyphRandom(unchecked(spec.Seed * 31 + w * 7919 + 13)),
                    Wheel = w,
                    Z = w * spec.WheelSpacing * spec.Radius,
                    Depth = depth,
                    Cap = cap,
                    GlyphRadius = spec.Radius * (1f - 0.12f * w),
                    PointLimit = set.PointCount + pointBudget,
                };

                GenerateWheel(ctx);
            }

            return set;
        }

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
        }

        private static void GenerateWheel(Context ctx)
        {
            List<Node> current = new List<Node>();
            current.Add(new Node
            {
                Center = Vector2.zero,
                Radius = ctx.GlyphRadius,
                Heading = ctx.Rng.Range(0f, TwoPi),
                IsRoot = true,
            });

            for (int level = 0; level <= ctx.Cap; level++)
            {
                List<Node> next = new List<Node>();
                for (int n = 0; n < current.Count; n++)
                {
                    if (ctx.Set.PointCount >= ctx.PointLimit)
                    {
                        ctx.Set.Truncated = true;
                        return;
                    }

                    Node node = current[n];
                    EmitNode(ctx, node, level);
                    ctx.Set.NodeCount++;

                    if (level < ctx.Cap)
                    {
                        SpawnChildren(ctx, node, level, next);
                    }
                }

                current = next;
            }
        }

        // ---- tree -------------------------------------------------------------------------------------------

        private static int BranchCount(ManagedGlyphRandom rng, int level)
        {
            if (level == 0)
            {
                return 2 + (rng.Chance(0.5f) ? 1 : 0);
            }

            if (level <= 2)
            {
                return 2 + (rng.Chance(0.3f) ? 1 : 0);
            }

            if (level <= 4)
            {
                return 1 + (rng.Chance(0.75f) ? 1 : 0);
            }

            return 1 + (rng.Chance(0.3f) ? 1 : 0);
        }

        private static void SpawnChildren(Context ctx, Node parent, int level, List<Node> next)
        {
            ManagedGlyphRandom rng = ctx.Rng;
            int count = BranchCount(rng, level);
            float minRadius = ctx.GlyphRadius * ctx.Settings.MinRadiusFraction;
            float baseAngle = parent.Heading;

            for (int i = 0; i < count; i++)
            {
                float angle;
                if (parent.IsRoot)
                {
                    float step = TwoPi / count;
                    angle = baseAngle + i * step + rng.Range(-0.25f, 0.25f) * step;
                }
                else
                {
                    // Children fan out on the far side of the parent, away from the node they hang from.
                    float arc = Mathf.PI * 1.25f;
                    float step = arc / count;
                    angle = parent.Heading + (i - (count - 1) * 0.5f) * step + rng.Range(-0.2f, 0.2f) * step;
                }

                float childRadius = parent.Radius * rng.Range(0.46f, 0.6f);
                if (childRadius < minRadius)
                {
                    continue;
                }

                float gap = parent.Radius * rng.Range(0.45f, 0.95f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 start = parent.Center + dir * parent.Radius;
                Vector2 end = start + dir * gap;
                Vector2 childCenter = end + dir * childRadius;

                EmitEdge(ctx, start, end, dir, level + 1);
                next.Add(new Node { Center = childCenter, Radius = childRadius, Heading = angle, IsRoot = false });
            }
        }

        // ---- node contents ----------------------------------------------------------------------------------

        private static void EmitNode(Context ctx, Node node, int level)
        {
            ManagedGlyphRandom rng = ctx.Rng;
            ManagedGlyphLodSettings s = ctx.Settings;
            float minRadius = ctx.GlyphRadius * s.MinRadiusFraction;

            int rings = level == 0 ? 1 + rng.NextInt(3) : (level <= 3 && rng.Chance(0.4f) ? 2 : 1);
            float innerRadius = node.Radius;
            for (int j = 0; j < rings; j++)
            {
                float radius = node.Radius * (1f - 0.2f * j - (j == 2 ? 0.04f : 0f));
                if (radius < minRadius)
                {
                    break;
                }

                innerRadius = radius;
                int segments = SegmentsFor(ctx, radius);
                float phase = rng.Range(0f, TwoPi);
                float dirSign = rng.Chance(0.5f) ? 1f : -1f;
                Vector2[] points = new Vector2[segments];
                for (int i = 0; i < segments; i++)
                {
                    float a = phase + dirSign * TwoPi * i / segments;
                    points[i] = node.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                }

                AddStroke(ctx, ManagedGlyphStrokeKind.Ring, points, true, level,
                    HalfWidth(ctx, level) * (j == 0 ? 1f : 0.8f), 0.25f + 0.07f * j, 0.8f + 0.05f * j);
            }

            if (level <= s.OrnamentLevelCap && ctx.Allowed != ManagedGlyphOrnaments.None)
            {
                float chance = level == 0 ? 0.85f : (level <= 2 ? 0.45f : 0.2f);
                if (rng.Chance(chance))
                {
                    EmitFigure(ctx, node.Center, innerRadius * 0.85f, level);
                }
            }

            if ((ctx.Allowed & ManagedGlyphOrnaments.Curl) != 0 && level >= 1 && rng.Chance(0.5f))
            {
                EmitCurl(ctx, node, level);
            }

            if ((ctx.Allowed & ManagedGlyphOrnaments.Drip) != 0 && level >= 1 && level <= 4 && rng.Chance(0.35f))
            {
                EmitDrip(ctx, node, level);
            }

            if ((ctx.Allowed & ManagedGlyphOrnaments.Dot) != 0)
            {
                int dots = rng.NextInt(3);
                for (int i = 0; i < dots; i++)
                {
                    float a = rng.Range(0f, TwoPi);
                    float d = node.Radius * rng.Range(1.2f, 1.6f);
                    Vector2 p = node.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    EmitDot(ctx, p, level, 0.7f, 1f);
                }
            }
        }

        private static void EmitFigure(Context ctx, Vector2 center, float radius, int level)
        {
            ManagedGlyphOrnaments allowed = ctx.Allowed;
            List<int> choices = new List<int>(3);
            if ((allowed & ManagedGlyphOrnaments.Rosette) != 0)
            {
                choices.Add(0);
            }

            if ((allowed & ManagedGlyphOrnaments.Star) != 0)
            {
                choices.Add(1);
            }

            if ((allowed & ManagedGlyphOrnaments.Nest) != 0)
            {
                choices.Add(2);
            }

            if (choices.Count == 0)
            {
                return;
            }

            int pick = choices[ctx.Rng.NextInt(choices.Count)];
            if (pick == 0)
            {
                EmitRosette(ctx, center, radius, level);
            }
            else if (pick == 1)
            {
                EmitStar(ctx, center, radius, level);
            }
            else
            {
                EmitNest(ctx, center, radius, level);
            }
        }

        // ---- figures ----------------------------------------------------------------------------------------

        /// <summary>
        /// Spirograph rosette. Hypotrochoid with big circle K and rolling circle m:
        /// x = (K - m) cos t + d cos(((K - m) / m) t), y = (K - m) sin t - d sin(((K - m) / m) t).
        /// Epitrochoid: x = (K + m) cos t - d cos(((K + m) / m) t), y = (K + m) sin t - d sin(((K + m) / m) t).
        /// d stays below m, so the curve has soft loops and no cusps. K petals, closes after m turns.
        /// </summary>
        private static void EmitRosette(Context ctx, Vector2 center, float radius, int level)
        {
            ManagedGlyphRandom rng = ctx.Rng;
            bool epi = rng.Chance(0.4f);
            int[,] table = epi ? EpiPairs : HypoPairs;
            int row = rng.NextInt(table.GetLength(0));
            int big = table[row, 0];
            int small = table[row, 1];
            float d = small * rng.Range(0.55f, 0.9f);
            float ratio = epi ? (big + small) / (float)small : (big - small) / (float)small;
            float reach = epi ? (big + small) + d : (big - small) + d;
            float scale = radius / reach;

            int perTurn = Mathf.Clamp(SegmentsFor(ctx, radius), 24, 72);
            int count = Mathf.Max(perTurn * small, big * 12);
            float phase = rng.Range(0f, TwoPi);
            Vector2[] points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float t = TwoPi * small * i / count;
                float x;
                float y;
                if (epi)
                {
                    x = (big + small) * Mathf.Cos(t) - d * Mathf.Cos(ratio * t);
                    y = (big + small) * Mathf.Sin(t) - d * Mathf.Sin(ratio * t);
                }
                else
                {
                    x = (big - small) * Mathf.Cos(t) + d * Mathf.Cos(ratio * t);
                    y = (big - small) * Mathf.Sin(t) - d * Mathf.Sin(ratio * t);
                }

                points[i] = center + Rotate(new Vector2(x, y) * scale, phase);
            }

            AddStroke(ctx, ManagedGlyphStrokeKind.Rosette, points, true, level, HalfWidth(ctx, level) * 0.6f, 0.45f, 1f);
        }

        /// <summary>Star polygon {n/k}: n chords, vertex i to vertex i + k. Each chord is its own stroke, so the points stay sharp.</summary>
        private static void EmitStar(Context ctx, Vector2 center, float radius, int level)
        {
            ManagedGlyphRandom rng = ctx.Rng;
            int row = rng.NextInt(StarPairs.GetLength(0));
            int n = StarPairs[row, 0];
            int k = StarPairs[row, 1];
            float phase = rng.Range(0f, TwoPi);
            float hw = HalfWidth(ctx, level) * 0.6f;
            for (int i = 0; i < n; i++)
            {
                float a0 = phase + TwoPi * i / n;
                float a1 = phase + TwoPi * ((i + k) % n) / n;
                Vector2[] chord =
                {
                    center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius,
                    center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius,
                };

                AddStroke(ctx, ManagedGlyphStrokeKind.Chord, chord, false, level, hw, 0.45f + 0.04f * (i % 5), 0.9f + 0.02f * (i % 5));
            }
        }

        /// <summary>
        /// Harmonograph nest. Two counter-rotating arms with a slow decay, z(t) = (a1 e^(i p t) + a2 e^(i (-q t + f))) e^(-c t).
        /// The decay makes the line spiral inward into a tight woven nest.
        /// </summary>
        private static void EmitNest(Context ctx, Vector2 center, float radius, int level)
        {
            ManagedGlyphRandom rng = ctx.Rng;
            int p = 1 + rng.NextInt(3);
            int q = 2 + rng.NextInt(6);
            if (q == p)
            {
                q += 1;
            }

            float phi = rng.Range(0f, TwoPi);
            float decay = rng.Range(0.03f, 0.07f);
            int turns = 3 + rng.NextInt(3);
            int perTurn = Mathf.Max(70, 12 * Mathf.Max(p, q));
            int count = perTurn * turns;
            float a1 = 0.55f;
            float a2 = 0.45f;
            Vector2[] points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float t = TwoPi * turns * i / (count - 1);
                float fade = (float)Math.Exp(-decay * t);
                float x = (a1 * Mathf.Cos(p * t) + a2 * Mathf.Cos(-q * t + phi)) * fade;
                float y = (a1 * Mathf.Sin(p * t) + a2 * Mathf.Sin(-q * t + phi)) * fade;
                points[i] = center + new Vector2(x, y) * radius;
            }

            AddStroke(ctx, ManagedGlyphStrokeKind.Nest, points, false, level, HalfWidth(ctx, level) * 0.4f, 0.45f, 1f);
        }

        /// <summary>Swirl that starts on the rim and winds inward, like the curls in the reference art.</summary>
        private static void EmitCurl(Context ctx, Node node, int level)
        {
            ManagedGlyphRandom rng = ctx.Rng;
            float attach = node.Heading + rng.Range(-1.6f, 1.6f);
            Vector2 dir = new Vector2(Mathf.Cos(attach), Mathf.Sin(attach));
            Vector2 rim = node.Center + dir * node.Radius;
            float curlRadius = node.Radius * rng.Range(0.3f, 0.5f);
            Vector2 c = rim + dir * curlRadius;
            float turns = rng.Range(1.1f, 1.8f);
            float sign = rng.Chance(0.5f) ? 1f : -1f;
            float start = attach + Mathf.PI;
            int count = Mathf.Max(14, Mathf.RoundToInt(SegmentsFor(ctx, curlRadius) * turns * 0.6f));
            Vector2[] points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float u = i / (float)(count - 1);
                float a = start + sign * u * TwoPi * turns;
                float r = curlRadius * (1f - u * 0.9f);
                points[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }

            AddStroke(ctx, ManagedGlyphStrokeKind.Curl, points, false, level, HalfWidth(ctx, level) * 0.8f, 0.5f, 1f);
        }

        /// <summary>Short stub that leaves the rim and ends in a dot.</summary>
        private static void EmitDrip(Context ctx, Node node, int level)
        {
            ManagedGlyphRandom rng = ctx.Rng;
            float angle = rng.Range(0f, TwoPi);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 start = node.Center + dir * node.Radius;
            float length = node.Radius * rng.Range(0.3f, 0.6f);
            Vector2 end = start + dir * length;
            Vector2[] points = Sample(ctx, start, end, dir, 4);
            AddStroke(ctx, ManagedGlyphStrokeKind.Drip, points, false, level, HalfWidth(ctx, level) * 0.7f, 0.6f, 0.9f);
            EmitDot(ctx, end + dir * HalfWidth(ctx, level) * 0.5f, level, 0.9f, 1f);
        }

        private static void EmitDot(Context ctx, Vector2 position, int level, float t0, float t1)
        {
            float offset = ctx.GlyphRadius * 0.002f;
            Vector2[] points = { position, position + new Vector2(offset, 0f) };
            AddStroke(ctx, ManagedGlyphStrokeKind.Dot, points, false, level, HalfWidth(ctx, level) * 1.25f, t0, t1);
        }

        /// <summary>Hand-drawn line from a rim to the next rim. It leaves along the radius.</summary>
        private static void EmitEdge(Context ctx, Vector2 start, Vector2 end, Vector2 dir, int level)
        {
            float length = (end - start).magnitude;
            int count = Mathf.Clamp(Mathf.RoundToInt(ctx.Settings.SegmentsPerRadius * length / ctx.GlyphRadius * 0.5f), 4, 24);
            Vector2[] points = Sample(ctx, start, end, dir, count);
            AddStroke(ctx, ManagedGlyphStrokeKind.Edge, points, false, level, HalfWidth(ctx, level) * 0.8f, 0f, 0.5f);
        }

        // Cubic curve from start to end with two seeded sideways bends.
        private static Vector2[] Sample(Context ctx, Vector2 start, Vector2 end, Vector2 dir, int count)
        {
            Vector2 side = new Vector2(-dir.y, dir.x);
            float length = (end - start).magnitude;
            float w1 = ctx.Rng.Range(-0.12f, 0.12f) * length;
            float w2 = ctx.Rng.Range(-0.12f, 0.12f) * length;
            Vector2 c1 = start + (end - start) * (1f / 3f) + side * w1;
            Vector2 c2 = start + (end - start) * (2f / 3f) + side * w2;
            Vector2[] points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float u = 1f - t;
                points[i] = start * (u * u * u) + c1 * (3f * u * u * t) + c2 * (3f * u * t * t) + end * (t * t * t);
            }

            return points;
        }

        // ---- helpers ----------------------------------------------------------------------------------------

        private static int SegmentsFor(Context ctx, float radius)
        {
            ManagedGlyphLodSettings s = ctx.Settings;
            float relative = radius / ctx.GlyphRadius;
            return Mathf.Clamp(Mathf.RoundToInt(s.SegmentsPerRadius * relative), s.MinSegments, s.MaxSegments);
        }

        private static float HalfWidth(Context ctx, int level)
        {
            float full = ctx.Spec.LineWidth * ctx.GlyphRadius * Mathf.Lerp(1f, 0.4f, level / (float)ManagedGlyphLimits.MaxDepth);
            return Mathf.Max(full, ctx.GlyphRadius * 0.006f);
        }

        private static Vector2 Rotate(Vector2 v, float angle)
        {
            float c = Mathf.Cos(angle);
            float s = Mathf.Sin(angle);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // Each level owns one slice of the 0 to 1 reveal range. a and b place the stroke inside its slice.
        private static void AddStroke(Context ctx, ManagedGlyphStrokeKind kind, Vector2[] points, bool closed, int level,
            float halfWidth, float a, float b)
        {
            float slice = 1f / (ctx.Depth + 1);
            ManagedGlyphStroke stroke = new ManagedGlyphStroke
            {
                Kind = kind,
                Points = points,
                Closed = closed,
                Level = level,
                Z = ctx.Z,
                Tint = ctx.Palette.ColorFor(level + ctx.Wheel),
                HalfWidth = halfWidth,
                T0 = (level + Mathf.Clamp01(a)) * slice,
                T1 = (level + Mathf.Clamp01(b)) * slice,
            };

            ctx.Set.Strokes.Add(stroke);
            ctx.Set.PointCount += points.Length;
        }
    }
}
