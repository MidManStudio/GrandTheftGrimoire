// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/specter.md, section "ManagedSpecterRenderer.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Plasma;
using UnityEngine;
using UnityEngine.Rendering;

namespace MidManStudio.Gtg.Managed.Specter
{
    /// <summary>
    /// Draws specter butterflies for the code-moved variant: one shared quad, one material, one small color ramp texture.
    /// <see cref="Draw"/> only queues a butterfly and <see cref="Flush"/> sends the queue, with Graphics.DrawMeshInstanced and the
    /// life, fade and look of each butterfly as per-instance values, or with one Graphics.DrawMesh per butterfly when instancing
    /// is not available. Call Flush once a frame after the Draw calls. The draw path enum is the plasma one, because both
    /// renderers pick between the same two calls.
    /// </summary>
    public sealed class ManagedSpecterRenderer : IDisposable
    {
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
        private float _intensity = 2f;

        /// <summary>Most butterflies queued in one frame. Draws past it are refused and Draw returns false.</summary>
        public int MaxDrawsPerFrame = 64;

        /// <param name="layer">Layer the butterflies are drawn on.</param>
        /// <param name="forceDrawMesh">Skips instancing and uses one Graphics.DrawMesh per butterfly, to test the fallback.</param>
        public ManagedSpecterRenderer(int layer, bool forceDrawMesh, string label)
        {
            _layer = layer;
            Shader shader = ManagedSpecterAssets.FindShader();
            _supported = shader != null;

            bool instanced = _supported && !forceDrawMesh && SystemInfo.supportsInstancing;
            _path = instanced ? ManagedPlasmaDrawPath.Instanced : ManagedPlasmaDrawPath.DrawMesh;

            if (!_supported)
            {
                Debug.LogError("[GTG Specter] Shader " + ManagedSpecterAssets.ShaderName + " was not found or is not supported. Butterflies will not draw. "
                    + "Add the shader to Always Included Shaders if this is a build.");
            }
            else
            {
                _quad = ManagedSpecterAssets.BuildQuad();
                _ramp = ManagedSpecterAssets.BuildRamp();
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

            Debug.Log("[GTG Specter] " + label + ": path " + _path + ", hardware instancing " + SystemInfo.supportsInstancing
                + ", shader " + (_supported ? "found" : "missing") + ", butterflies per frame " + MaxDrawsPerFrame);
        }

        /// <summary>The draw path in use.</summary>
        public ManagedPlasmaDrawPath Path { get { return _path; } }

        /// <summary>False when the shader was missing. Nothing draws then.</summary>
        public bool CanDraw { get { return _material != null; } }

        /// <summary>The shared material. One change tunes every butterfly of this renderer.</summary>
        public Material SpecterMaterial { get { return _material; } }

        /// <summary>Butterflies queued this frame.</summary>
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

        /// <summary>HDR brightness multiplier of every butterfly of this renderer, 0 to 8. Above 1 feeds the URP Bloom.</summary>
        public float Intensity
        {
            get { return _intensity; }
            set
            {
                _intensity = Mathf.Clamp(float.IsNaN(value) ? 2f : value, 0f, 8f);
                if (_material != null)
                {
                    _material.SetFloat("_Intensity", _intensity);
                }
            }
        }

        /// <summary>
        /// Queues one butterfly for this frame's Flush. <paramref name="matrix"/> places the quad, which is one unit wide before the
        /// matrix scales it. <paramref name="life"/> is 0 to 1 over the butterfly's life and drives the pattern and the fade out.
        /// <paramref name="fade"/> is 0 to 1. Returns false when the cap refused it or nothing can draw. A butterfly with no fade
        /// is accepted and not drawn.
        /// </summary>
        public bool Draw(ManagedSpecterSpec spec, Matrix4x4 matrix, float life, float fade)
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

            _queue.Add(new Item { Matrix = matrix, A = spec.PackA(life, fade), B = spec.PackB() });
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
            ManagedSpecterAssets.DestroyObject(_material);
            ManagedSpecterAssets.DestroyObject(_quad);
            ManagedSpecterAssets.DestroyObject(_ramp);
            _material = null;
            _quad = null;
            _ramp = null;
        }

        // A queue left over from an earlier frame was never flushed. It is dropped, with one warning, so the butterflies do not stack up.
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
                    Debug.LogWarning("[GTG Specter] Draw was called without a Flush in the previous frame. The queued butterflies were dropped.");
                    _warnedFlush = true;
                }

                _queue.Clear();
            }

            _frame = frame;
            _drawn = 0;
        }
    }
}
