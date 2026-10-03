// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedIcosphere.cs"
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Rendering
{
    /// <summary>
    /// Vertex and triangle data of a sphere with diameter 1. Subdivision 0 is an
    /// icosahedron, and each level splits every triangle in four. Level 1 gives
    /// 42 vertices and 80 triangles, which is enough for a small unlit blob.
    /// </summary>
    public static class ManagedIcosphere
    {
        public static void Build(int subdivisions, out Vector3[] vertices, out int[] triangles)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new List<Vector3>
            {
                new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f), new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t), new Vector3(0f, 1f, t), new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f),
            };

            for (int i = 0; i < verts.Count; i++)
            {
                verts[i] = verts[i].normalized * 0.5f;
            }

            var tris = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (int level = 0; level < subdivisions; level++)
            {
                var split = new List<int>(tris.Count * 4);
                var midpoints = new Dictionary<long, int>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i];
                    int b = tris[i + 1];
                    int c = tris[i + 2];
                    int ab = Midpoint(verts, midpoints, a, b);
                    int bc = Midpoint(verts, midpoints, b, c);
                    int ca = Midpoint(verts, midpoints, c, a);

                    split.Add(a); split.Add(ab); split.Add(ca);
                    split.Add(b); split.Add(bc); split.Add(ab);
                    split.Add(c); split.Add(ca); split.Add(bc);
                    split.Add(ab); split.Add(bc); split.Add(ca);
                }

                tris = split;
            }

            vertices = verts.ToArray();
            triangles = tris.ToArray();
        }

        // One shared vertex per edge, keyed by the ordered pair of end indices.
        private static int Midpoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long low = System.Math.Min(a, b);
            long high = System.Math.Max(a, b);
            long key = (low << 32) | high;

            int index;
            if (cache.TryGetValue(key, out index))
            {
                return index;
            }

            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized * 0.5f);
            index = verts.Count - 1;
            cache[key] = index;
            return index;
        }
    }
}
