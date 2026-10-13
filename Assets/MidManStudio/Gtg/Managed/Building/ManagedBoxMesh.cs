// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedBoxMesh.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>Vertex data of a unit box centered on the origin. Pure array work, no Unity objects.</summary>
    public static class ManagedBoxMesh
    {
        public const int VertexCount = 24;
        public const int IndexCount = 36;

        /// <summary>
        /// Six flat shaded faces of four vertices each. For every face the first tangent crossed
        /// with the second gives the outward normal, and the triangles (0,1,2) and (0,2,3) of a
        /// face then wind the way Unity treats as front facing.
        /// </summary>
        public static void Build(out Vector3[] vertices, out Vector3[] normals, out int[] triangles)
        {
            vertices = new Vector3[VertexCount];
            normals = new Vector3[VertexCount];
            triangles = new int[IndexCount];

            AddFace(0, vertices, normals, triangles, Vector3.right, Vector3.up, Vector3.forward);
            AddFace(1, vertices, normals, triangles, Vector3.left, Vector3.forward, Vector3.up);
            AddFace(2, vertices, normals, triangles, Vector3.up, Vector3.forward, Vector3.right);
            AddFace(3, vertices, normals, triangles, Vector3.down, Vector3.right, Vector3.forward);
            AddFace(4, vertices, normals, triangles, Vector3.forward, Vector3.right, Vector3.up);
            AddFace(5, vertices, normals, triangles, Vector3.back, Vector3.up, Vector3.right);
        }

        private static void AddFace(
            int face, Vector3[] vertices, Vector3[] normals, int[] triangles,
            Vector3 normal, Vector3 a, Vector3 b)
        {
            Vector3 center = normal * 0.5f;
            int v = face * 4;
            vertices[v + 0] = center - a * 0.5f - b * 0.5f;
            vertices[v + 1] = center + a * 0.5f - b * 0.5f;
            vertices[v + 2] = center + a * 0.5f + b * 0.5f;
            vertices[v + 3] = center - a * 0.5f + b * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                normals[v + i] = normal;
            }

            int t = face * 6;
            triangles[t + 0] = v + 0;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 0;
            triangles[t + 4] = v + 2;
            triangles[t + 5] = v + 3;
        }
    }
}
