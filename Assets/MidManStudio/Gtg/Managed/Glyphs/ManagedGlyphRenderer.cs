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
    /// <summary>How the renderer submits glyphs.</summary>
    public enum ManagedGlyphDrawPath
    {
        /// <summary>Graphics.DrawMeshInstanced. Glyphs that share a mesh go out in one call, with per-instance reveal and fade.</summary>
        Instanced,

        /// <summary>Graphics.DrawMesh, one call per glyph. Used when instancing is not available.</summary>
        DrawMesh,
    }

    /// <summary>
    /// Draws glyphs without a GameObject each. Meshes are baked once per spec and LOD and cached. Call Draw for every
    /// glyph alive this frame, then Flush once. Draw only queues. Flush sends the queue: glyphs that share a mesh go
    /// out as one Graphics.DrawMeshInstanced call, each with its own matrix, reveal and fade. When the machine or the
    /// shader cannot instance, Flush sends one Graphics.DrawMesh per glyph instead.
    /// Shader fallback: when the glyph shader is missing or unsupported the renderer uses Sprites/Default, which cannot
    /// grow a stroke, so whole levels appear one after another and the glyph is flat colored.
    /// </summary>
    public sealed class ManagedGlyphRenderer : IDisposable
    {
        public const string ShaderName = "MidManStudio/Gtg/GlyphLine";

        /// <summary>Distance in metres from the camera up to which a glyph is drawn Near.</summary>
        public const float NearDistance = 12f;

        public const float MidDistance = 35f;

        /// <summary>Unity draws at most this many instances in one DrawMeshInstanced call.</summary>
        public const int InstanceBatchSize = 1023;

        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        private sealed class Entry
        {
            public int Serial;
            public ManagedGlyphMeshData Data;
            public Mesh Mesh;
            public Mesh[] LevelMeshes;
            public int LastUsedFrame;
        }

        private struct Item
        {
            public Entry Entry;
            public int Sequence;
            public Matrix4x4 Matrix;
            public float Reveal;
            public float Fade;
            public int Levels;
        }

        private static readonly Comparison<Item> ByEntry = (a, b) =>
        {
            int c = a.Entry.Serial.CompareTo(b.Entry.Serial);
            return c != 0 ? c : a.Sequence.CompareTo(b.Sequence);
        };

        private readonly Dictionary<long, Entry> _cache = new Dictionary<long, Entry>();
        private readonly List<Item> _queue = new List<Item>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly int _layer;
        private readonly bool _custom;
        private readonly ManagedGlyphDrawPath _path;
        private Material _material;

        // Scratch arrays for one instanced call.
        private Matrix4x4[] _matrices;
        private float[] _reveals;
        private float[] _fades;

        private int _serial;
        private int _frame = -1;
        private int _drawn;
        private int _nearDrawn;
        private bool _warnedFlush;

        /// <summary>Most glyphs queued in one frame. Draws past it are refused and Draw returns false.</summary>
        public int MaxDrawsPerFrame = 12;

        /// <summary>Most Near glyphs in one frame. The rest are drawn Mid.</summary>
        public int MaxNearPerFrame = 3;

        /// <summary>Baked glyphs kept. The least recently drawn one is dropped past this.</summary>
        public int MaxCached = 24;

        public ManagedGlyphRenderer(int layer, string label) : this(layer, false, label)
        {
        }

        /// <param name="forceFallback">Skips the glyph shader and uses Sprites/Default.</param>
        public ManagedGlyphRenderer(int layer, bool forceFallback, string label) : this(layer, forceFallback, false, label)
        {
        }

        /// <param name="forceFallback">Skips the glyph shader and uses Sprites/Default.</param>
        /// <param name="forceDrawMesh">Skips instancing and uses one Graphics.DrawMesh per glyph.</param>
        public ManagedGlyphRenderer(int layer, bool forceFallback, bool forceDrawMesh, string label)
        {
            _layer = layer;
            Shader shader = Shader.Find(ShaderName);
            _custom = shader != null && shader.isSupported && !forceFallback;
            if (!_custom)
            {
                shader = Shader.Find("Sprites/Default");
            }

            bool instanced = _custom && !forceDrawMesh && SystemInfo.supportsInstancing;
            _path = instanced ? ManagedGlyphDrawPath.Instanced : ManagedGlyphDrawPath.DrawMesh;

            if (shader == null)
            {
                Debug.LogError("[GTG Glyph] No usable shader found. Glyphs will not draw.");
            }
            else
            {
                _material = new Material(shader);
                if (instanced)
                {
                    _material.enableInstancing = true;
                    _matrices = new Matrix4x4[InstanceBatchSize];
                    _reveals = new float[InstanceBatchSize];
                    _fades = new float[InstanceBatchSize];
                }
            }

            Debug.Log("[GTG Glyph] " + label + ": path " + _path + ", hardware instancing " + SystemInfo.supportsInstancing
                + ", shader " + (_custom ? "custom" : "Sprites/Default fallback") + ", draws per frame " + MaxDrawsPerFrame);
        }

        public bool UsingCustomShader { get { return _custom; } }

        public ManagedGlyphDrawPath Path { get { return _path; } }

        /// <summary>The material every glyph shares. Its properties tune the look of all glyphs of this renderer.</summary>
        public Material GlyphMaterial { get { return _material; } }

        /// <summary>Glyphs queued so far this frame.</summary>
        public int DrawsThisFrame { get { return _frame == Time.frameCount ? _drawn : 0; } }

        /// <summary>Draw calls the last Flush sent.</summary>
        public int LastFlushCalls { get; private set; }

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
        /// Queues one glyph for this frame's Flush. <paramref name="reveal"/> is 0 to 1 (see ManagedGlyphSpec.RevealForCharge),
        /// <paramref name="fade"/> is 0 to 1. Returns false when the per-frame cap refused it or nothing can draw.
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

            _queue.Add(new Item
            {
                Entry = entry,
                Sequence = _drawn,
                Matrix = matrix,
                Reveal = Mathf.Clamp01(reveal),
                Fade = Mathf.Clamp01(fade),
                Levels = spec.VisibleLevels(reveal),
            });
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

            _queue.Sort(ByEntry);
            int start = 0;
            while (start < _queue.Count)
            {
                Entry entry = _queue[start].Entry;
                int end = start + 1;
                while (end < _queue.Count && _queue[end].Entry == entry)
                {
                    end++;
                }

                SendGroup(entry, start, end);
                start = end;
            }

            _queue.Clear();
            _warnedFlush = false;
        }

        public void Dispose()
        {
            _queue.Clear();
            foreach (KeyValuePair<long, Entry> pair in _cache)
            {
                DestroyEntry(pair.Value);
            }

            _cache.Clear();
            DestroyObject(_material);
            _material = null;
        }

        // One mesh, items [start, end) of the sorted queue.
        private void SendGroup(Entry entry, int start, int end)
        {
            if (!_custom)
            {
                // Sprites/Default cannot grow a stroke, so each glyph shows the whole levels that have started.
                for (int i = start; i < end; i++)
                {
                    Mesh levelMesh = LevelMesh(entry, _queue[i].Levels);
                    Graphics.DrawMesh(levelMesh, _queue[i].Matrix, _material, _layer, null, 0, null, ShadowCastingMode.Off, false);
                    LastFlushCalls++;
                }

                return;
            }

            if (_path == ManagedGlyphDrawPath.Instanced)
            {
                int at = start;
                while (at < end)
                {
                    int n = Math.Min(InstanceBatchSize, end - at);
                    for (int k = 0; k < n; k++)
                    {
                        Item item = _queue[at + k];
                        _matrices[k] = item.Matrix;
                        _reveals[k] = item.Reveal;
                        _fades[k] = item.Fade;
                    }

                    _block.SetFloatArray(RevealId, _reveals);
                    _block.SetFloatArray(FadeId, _fades);
                    Graphics.DrawMeshInstanced(entry.Mesh, 0, _material, _matrices, n, _block, ShadowCastingMode.Off, false, _layer);
                    LastFlushCalls++;
                    at += n;
                }

                return;
            }

            for (int i = start; i < end; i++)
            {
                _block.SetFloat(RevealId, _queue[i].Reveal);
                _block.SetFloat(FadeId, _queue[i].Fade);
                Graphics.DrawMesh(entry.Mesh, _queue[i].Matrix, _material, _layer, null, 0, _block, ShadowCastingMode.Off, false);
                LastFlushCalls++;
            }
        }

        private void StartFrame()
        {
            int frame = Time.frameCount;
            if (frame == _frame)
            {
                return;
            }

            // Draws left from an earlier frame were never flushed. They are dropped, because they would be a frame stale.
            if (_queue.Count > 0)
            {
                if (!_warnedFlush)
                {
                    Debug.LogWarning("[GTG Glyph] Flush was not called after the last frame's Draw calls. Those glyphs were dropped.");
                    _warnedFlush = true;
                }

                _queue.Clear();
            }

            _frame = frame;
            _drawn = 0;
            _nearDrawn = 0;
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
                entry.Serial = ++_serial;
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

        // Never drops a glyph that is queued for the next Flush.
        private bool EvictOldest()
        {
            long oldestKey = 0;
            int oldest = int.MaxValue;
            bool found = false;
            foreach (KeyValuePair<long, Entry> pair in _cache)
            {
                if (IsQueued(pair.Value))
                {
                    continue;
                }

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

        private bool IsQueued(Entry entry)
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].Entry == entry)
                {
                    return true;
                }
            }

            return false;
        }

        // Shader fallback only: the mesh with the levels up to and including this count of levels.
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
