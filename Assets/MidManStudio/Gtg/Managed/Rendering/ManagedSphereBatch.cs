// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSphereBatch.cs"
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MidManStudio.Gtg.Managed.Rendering
{
    public enum ManagedSphereDrawPath
    {
        /// <summary>Graphics.DrawMeshInstanced, per-instance matrix and color.</summary>
        Instanced,

        /// <summary>One dynamic mesh with every sphere baked in, drawn with one Graphics.DrawMesh.</summary>
        CombinedMesh,
    }

    /// <summary>
    /// Draws many small spheres without a GameObject each. Same two paths as the projectile
    /// package renderer: hardware instancing when the machine and the shader support it,
    /// otherwise one combined mesh. Call Clear, Add for every sphere, then Draw, once per frame.
    /// </summary>
    public sealed class ManagedSphereBatch : IDisposable
    {
        private const int InstanceBatchSize = 1023;
        private const string ShaderName = "MidManStudio/Gtg/SphereUnlit";

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly int _capacity;
        private readonly int _layer;
        private readonly Vector3[] _unitVerts;
        private readonly int[] _unitTris;
        private readonly Vector3[] _positions;
        private readonly Vector3[] _scales;
        private readonly Color[] _colors;

        private Material _material;
        private Mesh _mesh;
        private int _count;
        private ManagedSphereDrawPath _path;

        // Instanced path.
        private Matrix4x4[] _matrices;
        private Vector4[] _tints;
        private MaterialPropertyBlock _block;

        // Combined mesh path.
        private Vector3[] _verts;
        private Color32[] _cols;
        private int[] _tris;

        public ManagedSphereBatch(int capacity, int layer, bool forceCombinedMesh, string label)
        {
            _capacity = Mathf.Max(1, capacity);
            _layer = layer;
            ManagedIcosphere.Build(1, out _unitVerts, out _unitTris);

            _positions = new Vector3[_capacity];
            _scales = new Vector3[_capacity];
            _colors = new Color[_capacity];

            bool customShader;
            _material = CreateMaterial(out customShader);

            bool canInstance = !forceCombinedMesh && customShader && SystemInfo.supportsInstancing;
            _path = canInstance ? ManagedSphereDrawPath.Instanced : ManagedSphereDrawPath.CombinedMesh;

            if (_path == ManagedSphereDrawPath.Instanced)
            {
                SetUpInstanced();
            }
            else
            {
                SetUpCombined();
            }

            Debug.Log(
                "[GTG Render] " + label + ": path " + _path +
                ", hardware instancing " + SystemInfo.supportsInstancing +
                ", shader " + (customShader ? "custom" : "Sprites/Default fallback") +
                ", capacity " + _capacity);
        }

        public ManagedSphereDrawPath Path { get { return _path; } }

        public int Count { get { return _count; } }

        public void Clear()
        {
            _count = 0;
        }

        /// <summary>Returns false when the batch is full and the sphere was dropped.</summary>
        public bool Add(Vector3 position, Vector3 scale, Color color)
        {
            if (_count >= _capacity)
            {
                return false;
            }

            _positions[_count] = position;
            _scales[_count] = scale;
            _colors[_count] = color;
            _count++;
            return true;
        }

        public void Draw()
        {
            if (_count == 0 || _material == null)
            {
                return;
            }

            if (_path == ManagedSphereDrawPath.Instanced)
            {
                DrawInstanced();
            }
            else
            {
                DrawCombined();
            }
        }

        public void Dispose()
        {
            DestroyObject(_mesh);
            DestroyObject(_material);
            _mesh = null;
            _material = null;
        }

        private void SetUpInstanced()
        {
            _mesh = new Mesh { name = "GtgSphereUnit" };
            _mesh.vertices = _unitVerts;
            _mesh.triangles = _unitTris;

            // The shader multiplies the per-instance color by the vertex color, so it is white.
            var white = new Color32[_unitVerts.Length];
            for (int i = 0; i < white.Length; i++)
            {
                white[i] = new Color32(255, 255, 255, 255);
            }

            _mesh.colors32 = white;
            _mesh.RecalculateBounds();

            _matrices = new Matrix4x4[InstanceBatchSize];
            _tints = new Vector4[InstanceBatchSize];
            _block = new MaterialPropertyBlock();
        }

        private void SetUpCombined()
        {
            int vertexCount = _unitVerts.Length;
            int indexCount = _unitTris.Length;

            _mesh = new Mesh { name = "GtgSphereCombined" };
            _mesh.MarkDynamic();

            // 16 bit indices end at 65535 vertices, a large batch needs 32 bit.
            if (_capacity * vertexCount > 65535)
            {
                _mesh.indexFormat = IndexFormat.UInt32;
            }

            _verts = new Vector3[_capacity * vertexCount];
            _cols = new Color32[_capacity * vertexCount];
            _tris = new int[_capacity * indexCount];

            // Indices never change, only the vertices move, so they are filled once.
            for (int i = 0; i < _capacity; i++)
            {
                for (int j = 0; j < indexCount; j++)
                {
                    _tris[i * indexCount + j] = _unitTris[j] + i * vertexCount;
                }
            }
        }

        private void DrawInstanced()
        {
            int start = 0;
            while (start < _count)
            {
                int n = Math.Min(InstanceBatchSize, _count - start);
                for (int i = 0; i < n; i++)
                {
                    int s = start + i;
                    _matrices[i] = Matrix4x4.TRS(_positions[s], Quaternion.identity, _scales[s]);
                    Color c = _colors[s];
                    _tints[i] = new Vector4(c.r, c.g, c.b, c.a);
                }

                _block.SetVectorArray(ColorId, _tints);
                Graphics.DrawMeshInstanced(
                    _mesh, 0, _material, _matrices, n, _block,
                    ShadowCastingMode.Off, false, _layer);
                start += n;
            }
        }

        private void DrawCombined()
        {
            int vertexCount = _unitVerts.Length;
            int indexCount = _unitTris.Length;

            ManagedSphereBake.Bake(_unitVerts, _positions, _scales, _colors, _count, _verts, _cols);

            _mesh.Clear();
            _mesh.SetVertices(_verts, 0, _count * vertexCount);
            _mesh.SetColors(_cols, 0, _count * vertexCount);
            _mesh.SetTriangles(_tris, 0, _count * indexCount, 0);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);

            Graphics.DrawMesh(_mesh, Matrix4x4.identity, _material, _layer);
        }

        // The custom shader is used when it compiled for this machine. Sprites/Default is the
        // fallback. It multiplies by vertex color, so the combined path works with it too.
        private static Material CreateMaterial(out bool customShader)
        {
            Shader shader = Shader.Find(ShaderName);
            customShader = shader != null && shader.isSupported;
            if (!customShader)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                Debug.LogError("[GTG Render] No usable shader found. Spheres will not draw.");
                return null;
            }

            var material = new Material(shader);
            if (customShader)
            {
                material.enableInstancing = true;
            }

            return material;
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }

    /// <summary>Vertex baking of the combined mesh path. Pure array work, no Unity objects.</summary>
    public static class ManagedSphereBake
    {
        /// <summary>
        /// Writes <paramref name="count"/> copies of the unit sphere, each scaled per axis and
        /// moved to its position, into the output arrays. Sphere i owns the vertex range
        /// starting at i times the unit vertex count.
        /// </summary>
        public static void Bake(
            Vector3[] unitVerts, Vector3[] positions, Vector3[] scales, Color[] colors, int count,
            Vector3[] outVerts, Color32[] outColors)
        {
            int vertexCount = unitVerts.Length;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = positions[i];
                Vector3 s = scales[i];
                Color32 color = colors[i];
                int baseIndex = i * vertexCount;
                for (int v = 0; v < vertexCount; v++)
                {
                    Vector3 u = unitVerts[v];
                    outVerts[baseIndex + v] = new Vector3(p.x + u.x * s.x, p.y + u.y * s.y, p.z + u.z * s.z);
                    outColors[baseIndex + v] = color;
                }
            }
        }
    }
}
