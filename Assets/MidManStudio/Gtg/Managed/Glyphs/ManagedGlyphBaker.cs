// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphBaker.cs"
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Glyphs
{
    /// <summary>
    /// A baked glyph as plain arrays, ready to become one Mesh. Vertex layout:
    /// Uv0 = (distance along the stroke, side -1 to 1), Uv1 = (stroke length, outer half width),
    /// Uv2 = (reveal value where the stroke starts, reveal value where it is whole), Color = halo tint.
    /// Triangles are sorted by level, and LevelIndexEnd[l] is the index count after level l.
    /// </summary>
    public sealed class ManagedGlyphMeshData
    {
        public Vector3[] Positions;
        public Vector2[] Uv0;
        public Vector2[] Uv1;
        public Vector2[] Uv2;
        public Color32[] Colors;
        public int[] Triangles;
        public int[] LevelIndexEnd;
        public Vector3 BoundsCenter;
        public Vector3 BoundsSize;
        public int StrokeCount;
        public int PointCount;
        public int NodeCount;
        public bool Truncated;

        public int VertexCount { get { return Positions.Length; } }

        public int TriangleCount { get { return Triangles.Length / 3; } }
    }

    /// <summary>
    /// Turns the strokes of a glyph into ribbons: two vertices per point, a round end cap on every open stroke,
    /// and nothing per frame afterwards. The shader draws the core, the halo and the reveal from the vertex data.
    /// Pure array work, no Unity objects.
    /// </summary>
    public static class ManagedGlyphBaker
    {
        public static ManagedGlyphMeshData Bake(ManagedGlyphSpec spec, ManagedGlyphLod lod)
        {
            ManagedGlyphStrokeSet set = ManagedGlyphGenerator.Generate(spec, lod);
            return Bake(set);
        }

        public static ManagedGlyphMeshData Bake(ManagedGlyphStrokeSet set)
        {
            int levels = set.LevelCount;

            // Group by level so the triangles can be drawn level by level.
            List<ManagedGlyphStroke>[] byLevel = new List<ManagedGlyphStroke>[levels];
            for (int i = 0; i < levels; i++)
            {
                byLevel[i] = new List<ManagedGlyphStroke>();
            }

            int vertexCount = 0;
            int indexCount = 0;
            for (int i = 0; i < set.Strokes.Count; i++)
            {
                ManagedGlyphStroke stroke = set.Strokes[i];
                int level = Mathf.Clamp(stroke.Level, 0, levels - 1);
                byLevel[level].Add(stroke);
                int pairs = PairCount(stroke);
                vertexCount += pairs * 2;
                indexCount += (pairs - 1) * 6;
            }

            ManagedGlyphMeshData data = new ManagedGlyphMeshData
            {
                Positions = new Vector3[vertexCount],
                Uv0 = new Vector2[vertexCount],
                Uv1 = new Vector2[vertexCount],
                Uv2 = new Vector2[vertexCount],
                Colors = new Color32[vertexCount],
                Triangles = new int[indexCount],
                LevelIndexEnd = new int[levels],
                StrokeCount = set.Strokes.Count,
                PointCount = set.PointCount,
                NodeCount = set.NodeCount,
                Truncated = set.Truncated,
            };

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            int v = 0;
            int t = 0;
            for (int level = 0; level < levels; level++)
            {
                List<ManagedGlyphStroke> strokes = byLevel[level];
                for (int i = 0; i < strokes.Count; i++)
                {
                    WriteStroke(strokes[i], data, ref v, ref t, ref min, ref max);
                }

                data.LevelIndexEnd[level] = t;
            }

            if (vertexCount == 0)
            {
                min = Vector3.zero;
                max = Vector3.zero;
            }

            data.BoundsCenter = (min + max) * 0.5f;
            data.BoundsSize = max - min;
            return data;
        }

        private static int PairCount(ManagedGlyphStroke stroke)
        {
            int n = stroke.Points.Length;
            return stroke.Closed ? n + 1 : n + 2;
        }

        private static void WriteStroke(ManagedGlyphStroke stroke, ManagedGlyphMeshData data, ref int v, ref int t,
            ref Vector3 min, ref Vector3 max)
        {
            Vector2[] p = stroke.Points;
            int n = p.Length;
            bool closed = stroke.Closed;
            float hw = stroke.HalfWidth;

            // Arc length at every point, and the whole length.
            float[] arc = new float[n];
            for (int i = 1; i < n; i++)
            {
                arc[i] = arc[i - 1] + (p[i] - p[i - 1]).magnitude;
            }

            float length = arc[n - 1] + (closed ? (p[0] - p[n - 1]).magnitude : 0f);

            int firstVertex = v;
            if (!closed)
            {
                Vector2 tangent = Tangent(p, 0, false);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                Vector2 pos = p[0] - tangent * hw;
                WritePair(data, ref v, pos, normal, stroke, -hw, length, ref min, ref max);
            }

            for (int i = 0; i < n; i++)
            {
                Vector2 tangent = Tangent(p, i, closed);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                WritePair(data, ref v, p[i], normal, stroke, arc[i], length, ref min, ref max);
            }

            if (closed)
            {
                Vector2 tangent = Tangent(p, 0, true);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                WritePair(data, ref v, p[0], normal, stroke, length, length, ref min, ref max);
            }
            else
            {
                Vector2 tangent = Tangent(p, n - 1, false);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                Vector2 pos = p[n - 1] + tangent * hw;
                WritePair(data, ref v, pos, normal, stroke, length + hw, length, ref min, ref max);
            }

            int pairs = (v - firstVertex) / 2;
            for (int q = 0; q < pairs - 1; q++)
            {
                int a = firstVertex + q * 2;
                int b = a + 2;
                data.Triangles[t++] = a;
                data.Triangles[t++] = b;
                data.Triangles[t++] = a + 1;
                data.Triangles[t++] = a + 1;
                data.Triangles[t++] = b;
                data.Triangles[t++] = b + 1;
            }
        }

        private static void WritePair(ManagedGlyphMeshData data, ref int v, Vector2 position, Vector2 normal,
            ManagedGlyphStroke stroke, float along, float length, ref Vector3 min, ref Vector3 max)
        {
            float hw = stroke.HalfWidth;
            Vector2 left = position + normal * hw;
            Vector2 right = position - normal * hw;
            WriteVertex(data, v, left, stroke, along, 1f, length, ref min, ref max);
            WriteVertex(data, v + 1, right, stroke, along, -1f, length, ref min, ref max);
            v += 2;
        }

        private static void WriteVertex(ManagedGlyphMeshData data, int index, Vector2 position, ManagedGlyphStroke stroke,
            float along, float side, float length, ref Vector3 min, ref Vector3 max)
        {
            Vector3 pos = new Vector3(position.x, position.y, stroke.Z);
            data.Positions[index] = pos;
            data.Uv0[index] = new Vector2(along, side);
            data.Uv1[index] = new Vector2(length, stroke.HalfWidth);
            data.Uv2[index] = new Vector2(stroke.T0, stroke.T1);
            data.Colors[index] = stroke.Tint;

            if (pos.x < min.x) min.x = pos.x;
            if (pos.y < min.y) min.y = pos.y;
            if (pos.z < min.z) min.z = pos.z;
            if (pos.x > max.x) max.x = pos.x;
            if (pos.y > max.y) max.y = pos.y;
            if (pos.z > max.z) max.z = pos.z;
        }

        // Direction of the line at point i. A closed ring looks across the seam, an open line looks along its ends.
        private static Vector2 Tangent(Vector2[] p, int i, bool closed)
        {
            int n = p.Length;
            Vector2 d;
            if (closed)
            {
                d = p[(i + 1) % n] - p[(i - 1 + n) % n];
            }
            else if (i == 0)
            {
                d = p[Mathf.Min(1, n - 1)] - p[0];
            }
            else if (i == n - 1)
            {
                d = p[n - 1] - p[n - 2];
            }
            else
            {
                d = p[i + 1] - p[i - 1];
            }

            Vector2 unit = d.normalized;
            if (unit.x == 0f && unit.y == 0f)
            {
                return new Vector2(1f, 0f);
            }

            return unit;
        }
    }
}
