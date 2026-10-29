// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/plasma.md, section "ManagedPlasmaRenderer.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MidManStudio.Gtg.Managed.Plasma
{
    /// <summary>Which Unity call sends the rings.</summary>
    public enum ManagedPlasmaDrawPath
    {
        /// <summary>Graphics.DrawMeshInstanced, rings that share the quad go out in one call. The primary path.</summary>
        Instanced,

        /// <summary>One Graphics.DrawMesh per ring. The fallback when instancing is not supported or is switched off.</summary>
        DrawMesh,
    }

    /// <summary>
    /// Draws plasma rings: one shared quad, one material, one small color ramp texture. <see cref="Draw"/> only queues a
    /// ring and <see cref="Flush"/> sends the queue, with Graphics.DrawMeshInstanced and the age, fade and shape of each ring
    /// as per-instance values, or with one Graphics.DrawMesh per ring when instancing is not available. Call Flush once a
    /// frame after the Draw calls. A ring is a few hundred lines of shader per pixel, so the number of rings per frame is capped.
    /// </summary>
    public sealed class ManagedPlasmaRenderer : IDisposable
    {
        public const string ShaderName = "MidManStudio/Gtg/PlasmaRing";

        /// <summary>Graphics.DrawMeshInstanced takes at most this many instances in one call.</summary>
        public const int InstanceBatchSize = 1023;

        private static readonly int ParamAId = Shader.PropertyToID("_ParamA");
        private static readonly int ParamBId = Shader.PropertyToID("_ParamB");

        private struct Item
        {
            public Matrix4x4 Matrix;
            public Vector4 A;
            public Vector4 B;
        }

        private readonly List<Item> _queue = new List<Item>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly int _layer;
        private readonly ManagedPlasmaDrawPath _path;
        private readonly bool _supported;
        private Material _material;
        private Mesh _quad;
        private Texture2D _ramp;

        // Scratch arrays for one instanced call. Their size never changes, because Unity rejects a property array that changes size.
        private Matrix4x4[] _matrices;
        private Vector4[] _paramsA;
        private Vector4[] _paramsB;

        private int _frame = -1;
        private int _drawn;
        private bool _warnedFlush;
        private bool _additive = true;
        private float _intensity = 1f;

        /// <summary>Most rings queued in one frame. Draws past it are refused and Draw returns false.</summary>
        public int MaxDrawsPerFrame = 16;

        /// <param name="layer">Layer the rings are drawn on.</param>
        /// <param name="forceDrawMesh">Skips instancing and uses one Graphics.DrawMesh per ring, to test the fallback.</param>
        public ManagedPlasmaRenderer(int layer, bool forceDrawMesh, string label)
        {
            _layer = layer;
            Shader shader = Shader.Find(ShaderName);
            _supported = shader != null && shader.isSupported;

            bool instanced = _supported && !forceDrawMesh && SystemInfo.supportsInstancing;
            _path = instanced ? ManagedPlasmaDrawPath.Instanced : ManagedPlasmaDrawPath.DrawMesh;

            if (!_supported)
            {
                Debug.LogError("[GTG Plasma] Shader " + ShaderName + " was not found or is not supported. Plasma rings will not draw. "
                    + "Add the shader to Always Included Shaders if this is a build.");
            }
            else
            {
                _quad = BuildQuad();
                _ramp = BuildRamp();
                _material = new Material(shader);
                _material.SetTexture("_RampTex", _ramp);
                _material.SetFloat("_Intensity", _intensity);
                _material.SetFloat("_DstBlend", (float)BlendMode.One);
                if (instanced)
                {
                    _material.enableInstancing = true;
                    _matrices = new Matrix4x4[InstanceBatchSize];
                    _paramsA = new Vector4[InstanceBatchSize];
                    _paramsB = new Vector4[InstanceBatchSize];
                }
            }

            Debug.Log("[GTG Plasma] " + label + ": path " + _path + ", hardware instancing " + SystemInfo.supportsInstancing
                + ", shader " + (_supported ? "found" : "missing") + ", rings per frame " + MaxDrawsPerFrame);
        }

        /// <summary>The draw path in use.</summary>
        public ManagedPlasmaDrawPath Path { get { return _path; } }

        /// <summary>False when the shader was missing. Nothing draws then.</summary>
        public bool CanDraw { get { return _material != null; } }

        /// <summary>The shared material. One change tunes every ring of this renderer.</summary>
        public Material PlasmaMaterial { get { return _material; } }

        /// <summary>Rings queued this frame.</summary>
        public int DrawsThisFrame { get { return _frame == Time.frameCount ? _drawn : 0; } }

        /// <summary>Unity draw calls the last Flush made.</summary>
        public int LastFlushCalls { get; private set; }

        /// <summary>True (the default) adds the glow to what is behind it. False blends it over the background with its alpha.</summary>
        public bool Additive
        {
            get { return _additive; }
            set
            {
                _additive = value;
                if (_material != null)
                {
                    _material.SetFloat("_DstBlend", (float)(value ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                }
            }
        }

        /// <summary>Brightness multiplier of every ring of this renderer, 0 to 4 in the shader.</summary>
        public float Intensity
        {
            get { return _intensity; }
            set
            {
                _intensity = Mathf.Clamp(value, 0f, 4f);
                if (_material != null)
                {
                    _material.SetFloat("_Intensity", _intensity);
                }
            }
        }

        /// <summary>
        /// Queues one ring for this frame's Flush. <paramref name="matrix"/> places the quad, which is one unit wide before the
        /// matrix scales it, so the matrix scale is the drawn diameter in metres. <paramref name="age"/> is seconds since the ring
        /// began, which moves the noise. <paramref name="fade"/> is 0 to 1. Returns false when the cap refused the ring or
        /// nothing can draw. A ring with no fade is accepted and not drawn.
        /// </summary>
        public bool Draw(ManagedPlasmaRingSpec spec, Matrix4x4 matrix, float age, float fade)
        {
            if (_material == null || spec == null)
            {
                return false;
            }

            StartFrame();
            if (fade <= 0f)
            {
                return true;
            }

            if (_drawn >= MaxDrawsPerFrame)
            {
                return false;
            }

            _queue.Add(new Item { Matrix = matrix, A = spec.PackA(age, fade), B = spec.PackB() });
            _drawn++;
            return true;
        }

        /// <summary>Sends everything queued since the last Flush. Call it once per frame after the Draw calls.</summary>
        public void Flush()
        {
            LastFlushCalls = 0;
            if (_queue.Count == 0)
            {
                return;
            }

            if (_material == null)
            {
                _queue.Clear();
                return;
            }

            if (_path == ManagedPlasmaDrawPath.Instanced)
            {
                int at = 0;
                while (at < _queue.Count)
                {
                    int n = Math.Min(InstanceBatchSize, _queue.Count - at);
                    for (int k = 0; k < n; k++)
                    {
                        Item item = _queue[at + k];
                        _matrices[k] = item.Matrix;
                        _paramsA[k] = item.A;
                        _paramsB[k] = item.B;
                    }

                    _block.SetVectorArray(ParamAId, _paramsA);
                    _block.SetVectorArray(ParamBId, _paramsB);
                    Graphics.DrawMeshInstanced(_quad, 0, _material, _matrices, n, _block, ShadowCastingMode.Off, false, _layer);
                    LastFlushCalls++;
                    at += n;
                }
            }
            else
            {
                for (int i = 0; i < _queue.Count; i++)
                {
                    _block.SetVector(ParamAId, _queue[i].A);
                    _block.SetVector(ParamBId, _queue[i].B);
                    Graphics.DrawMesh(_quad, _queue[i].Matrix, _material, _layer, null, 0, _block, ShadowCastingMode.Off, false);
                    LastFlushCalls++;
                }
            }

            _queue.Clear();
            _warnedFlush = false;
        }

        public void Dispose()
        {
            _queue.Clear();
            DestroyObject(_material);
            DestroyObject(_quad);
            DestroyObject(_ramp);
            _material = null;
            _quad = null;
            _ramp = null;
        }

        // A queue left over from an earlier frame was never flushed. It is dropped, with one warning, so the rings do not stack up.
        private void StartFrame()
        {
            int frame = Time.frameCount;
            if (frame == _frame)
            {
                return;
            }

            if (_queue.Count > 0)
            {
                if (!_warnedFlush)
                {
                    Debug.LogWarning("[GTG Plasma] Draw was called without a Flush in the previous frame. The queued rings were dropped.");
                    _warnedFlush = true;
                }

                _queue.Clear();
            }

            _frame = frame;
            _drawn = 0;
        }

        // One unit wide quad in the XY plane, facing -Z like a Unity Quad. uv runs 0 to 1.
        private static Mesh BuildQuad()
        {
            Mesh mesh = new Mesh();
            mesh.name = "GTG Plasma Quad";
            mesh.SetVertices(
                new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
                },
                0,
                4);
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0, 6, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.01f));
            return mesh;
        }

        private static Texture2D BuildRamp()
        {
            Texture2D texture = new Texture2D(ManagedPlasmaRamp.Width, ManagedPlasmaLimits.PaletteCount, TextureFormat.RGBA32, false);
            texture.name = "GTG Plasma Ramp";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(ManagedPlasmaRamp.Bake());
            texture.Apply();
            return texture;
        }

        private static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
}
