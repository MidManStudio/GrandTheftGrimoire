// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphRenderer.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MidManStudio.Gtg.Managed.Glyphs
{
    /// <summary>
    /// Draws glyphs without a GameObject each. Meshes are baked once per spec and LOD and cached, then each glyph
    /// is one Graphics.DrawMesh with a matrix and two numbers (reveal and fade). Call Draw every frame while a glyph
    /// is alive. It does not use Graphics.DrawMeshInstanced.
    /// Fallback: when the glyph shader is missing or unsupported the renderer uses Sprites/Default, which cannot grow
    /// a stroke, so whole levels appear one after another and the glyph is flat colored.
    /// </summary>
    public sealed class ManagedGlyphRenderer : IDisposable
    {
        public const string ShaderName = "MidManStudio/Gtg/GlyphLine";

        /// <summary>Distance in metres from the camera up to which a glyph is drawn Near.</summary>
        public const float NearDistance = 12f;

        public const float MidDistance = 35f;

        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        private sealed class Entry
        {
            public ManagedGlyphMeshData Data;
            public Mesh Mesh;
            public Mesh[] LevelMeshes;
            public int LastUsedFrame;
        }

        private readonly Dictionary<long, Entry> _cache = new Dictionary<long, Entry>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly int _layer;
        private readonly bool _custom;
        private Material _material;

        private int _frame = -1;
        private int _drawn;
        private int _nearDrawn;

        /// <summary>Most glyphs drawn in one frame. Draws past it are refused and Draw returns false.</summary>
        public int MaxDrawsPerFrame = 12;

        /// <summary>Most Near glyphs in one frame. The rest are drawn Mid.</summary>
        public int MaxNearPerFrame = 3;

        /// <summary>Baked glyphs kept. The least recently drawn one is dropped past this.</summary>
        public int MaxCached = 24;

        public ManagedGlyphRenderer(int layer, string label) : this(layer, false, label)
        {
        }

        public ManagedGlyphRenderer(int layer, bool forceFallback, string label)
        {
            _layer = layer;
            Shader shader = Shader.Find(ShaderName);
            _custom = shader != null && shader.isSupported && !forceFallback;
            if (!_custom)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                Debug.LogError("[GTG Glyph] No usable shader found. Glyphs will not draw.");
            }
            else
            {
                _material = new Material(shader);
            }

            Debug.Log("[GTG Glyph] " + label + ": shader " + (_custom ? "custom" : "Sprites/Default fallback")
                + ", draws per frame " + MaxDrawsPerFrame);
        }

        public bool UsingCustomShader { get { return _custom; } }

        /// <summary>The material every glyph shares. Its properties tune the look of all glyphs of this renderer.</summary>
        public Material GlyphMaterial { get { return _material; } }

        public int DrawsThisFrame { get { return _frame == Time.frameCount ? _drawn : 0; } }

        public int CachedCount { get { return _cache.Count; } }

        public static ManagedGlyphLod ChooseLod(float distance)
        {
            if (distance < NearDistance)
            {
                return ManagedGlyphLod.Near;
            }

            return distance < MidDistance ? ManagedGlyphLod.Mid : ManagedGlyphLod.Far;
        }

        /// <summary>The baked arrays of a glyph, from the cache or baked now. For stats and tests.</summary>
        public ManagedGlyphMeshData GetData(ManagedGlyphSpec spec, ManagedGlyphLod lod)
        {
            return Acquire(spec, lod).Data;
        }

        /// <summary>
        /// Draws one glyph. <paramref name="reveal"/> is 0 to 1 (see ManagedGlyphSpec.RevealForCharge), <paramref name="fade"/> is 0 to 1.
        /// Returns false when the per-frame cap refused the draw or nothing can draw.
        /// </summary>
        public bool Draw(ManagedGlyphSpec spec, ManagedGlyphLod lod, Matrix4x4 matrix, float reveal, float fade)
        {
            if (_material == null)
            {
                return false;
            }

            StartFrame();
            if (reveal <= 0f || fade <= 0f)
            {
                return true;
            }

            if (_drawn >= MaxDrawsPerFrame)
            {
                return false;
            }

            if (lod == ManagedGlyphLod.Near)
            {
                if (_nearDrawn >= MaxNearPerFrame)
                {
                    lod = ManagedGlyphLod.Mid;
                }
                else
                {
                    _nearDrawn++;
                }
            }

            Entry entry = Acquire(spec, lod);
            if (entry.Mesh == null)
            {
                return false;
            }

            _drawn++;
            if (_custom)
            {
                _block.SetFloat(RevealId, Mathf.Clamp01(reveal));
                _block.SetFloat(FadeId, Mathf.Clamp01(fade));
                Graphics.DrawMesh(entry.Mesh, matrix, _material, _layer, null, 0, _block, ShadowCastingMode.Off, false);
            }
            else
            {
                Mesh levelMesh = LevelMesh(entry, spec.VisibleLevels(reveal));
                Graphics.DrawMesh(levelMesh, matrix, _material, _layer, null, 0, null, ShadowCastingMode.Off, false);
            }

            return true;
        }

        public void Dispose()
        {
            foreach (KeyValuePair<long, Entry> pair in _cache)
            {
                DestroyEntry(pair.Value);
            }

            _cache.Clear();
            DestroyObject(_material);
            _material = null;
        }

        private void StartFrame()
        {
            int frame = Time.frameCount;
            if (frame != _frame)
            {
                _frame = frame;
                _drawn = 0;
                _nearDrawn = 0;
            }
        }

        private Entry Acquire(ManagedGlyphSpec spec, ManagedGlyphLod lod)
        {
            long key = spec.CacheKey(lod);
            Entry entry;
            if (!_cache.TryGetValue(key, out entry))
            {
                // A loop, because MaxCached may have been lowered since the last bake.
                while (_cache.Count >= Math.Max(1, MaxCached) && EvictOldest())
                {
                }

                entry = new Entry();
                entry.Data = ManagedGlyphBaker.Bake(spec, lod);
                if (entry.Data.VertexCount > 0)
                {
                    entry.Mesh = CreateMesh(entry.Data, entry.Data.Triangles.Length, "GtgGlyph " + key);
                }

                entry.LevelMeshes = new Mesh[entry.Data.LevelIndexEnd.Length];
                _cache[key] = entry;
            }

            entry.LastUsedFrame = Time.frameCount;
            return entry;
        }

        private bool EvictOldest()
        {
            long oldestKey = 0;
            int oldest = int.MaxValue;
            bool found = false;
            foreach (KeyValuePair<long, Entry> pair in _cache)
            {
                if (pair.Value.LastUsedFrame < oldest)
                {
                    oldest = pair.Value.LastUsedFrame;
                    oldestKey = pair.Key;
                    found = true;
                }
            }

            if (found)
            {
                DestroyEntry(_cache[oldestKey]);
                _cache.Remove(oldestKey);
            }

            return found;
        }

        // Fallback only: the mesh with the levels up to and including this count of levels.
        private static Mesh LevelMesh(Entry entry, int visibleLevels)
        {
            int[] ends = entry.Data.LevelIndexEnd;
            int index = Mathf.Clamp(visibleLevels, 1, ends.Length) - 1;
            if (entry.LevelMeshes[index] == null)
            {
                entry.LevelMeshes[index] = CreateMesh(entry.Data, ends[index], "GtgGlyph level " + index);
            }

            return entry.LevelMeshes[index];
        }

        private static Mesh CreateMesh(ManagedGlyphMeshData data, int indexCount, string meshName)
        {
            Mesh mesh = new Mesh { name = meshName };

            // 16 bit indices end at 65535 vertices.
            if (data.VertexCount > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(data.Positions, 0, data.VertexCount);
            mesh.SetUVs(0, data.Uv0);
            mesh.SetUVs(1, data.Uv1);
            mesh.SetUVs(2, data.Uv2);
            mesh.SetColors(data.Colors, 0, data.VertexCount);
            mesh.SetTriangles(data.Triangles, 0, indexCount, 0);
            mesh.bounds = new Bounds(data.BoundsCenter, data.BoundsSize);
            return mesh;
        }

        private static void DestroyEntry(Entry entry)
        {
            DestroyObject(entry.Mesh);
            if (entry.LevelMeshes != null)
            {
                for (int i = 0; i < entry.LevelMeshes.Length; i++)
                {
                    DestroyObject(entry.LevelMeshes[i]);
                }
            }
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
}
