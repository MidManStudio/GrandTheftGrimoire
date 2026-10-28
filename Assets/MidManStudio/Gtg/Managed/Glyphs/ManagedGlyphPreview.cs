// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphPreview.cs"
// ============================================================================

using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Glyphs
{
    public enum ManagedGlyphPalettePreset
    {
        Fire,
        Water,
        Arcane,
        Prism,
    }

    /// <summary>
    /// Draws one glyph at this object for testing the look. Set the shape in the Inspector, move Charge to grow it,
    /// or tick Auto Charge in Play mode to sweep it. In Edit mode the glyph only redraws when Unity runs the scene
    /// update, for example after a value changes. The GTG, Glyphs menu places and drives this component.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ManagedGlyphPreview : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] private int _seed = 1;
        [Range(0, 7)] [SerializeField] private int _maxDepth = 4;
        [Range(-1, 7)] [SerializeField] private int _baseDepth = 0;
        [SerializeField] private float _radius = 0.6f;
        [Range(0.01f, 0.12f)] [SerializeField] private float _lineWidth = 0.05f;
        [Range(1, 3)] [SerializeField] private int _wheels = 1;
        [SerializeField] private ManagedGlyphPalettePreset _palette = ManagedGlyphPalettePreset.Fire;

        [Header("Charge")]
        [Range(0f, 1f)] [SerializeField] private float _charge = 1f;
        [SerializeField] private bool _autoCharge;
        [SerializeField] private float _autoSeconds = 3f;
        [Range(0f, 1f)] [SerializeField] private float _fade = 1f;

        [Header("Detail")]
        [SerializeField] private bool _forceLod;
        [SerializeField] private ManagedGlyphLod _lod = ManagedGlyphLod.Near;

        private ManagedGlyphRenderer _renderer;
        private ManagedGlyphSpec _spec;

        public int Seed { get { return _seed; } set { _seed = value; _spec = null; } }

        public bool AutoCharge { get { return _autoCharge; } set { _autoCharge = value; } }

        /// <summary>The spec the Inspector values describe. Built again after a value changes.</summary>
        public ManagedGlyphSpec Spec
        {
            get
            {
                if (_spec == null)
                {
                    _spec = BuildSpec();
                }

                return _spec;
            }
        }

        public void Rebuild()
        {
            _spec = null;
        }

        private void OnValidate()
        {
            _spec = null;
        }

        private void OnDisable()
        {
            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }
        }

        private void Update()
        {
            if (_renderer == null)
            {
                _renderer = new ManagedGlyphRenderer(gameObject.layer, "preview");
            }

            ManagedGlyphSpec spec = Spec;
            float charge = _charge;
            if (_autoCharge && Application.isPlaying)
            {
                // Up and back, so the draw-on can be watched again and again.
                float u = Mathf.Repeat(Time.time / Mathf.Max(0.1f, _autoSeconds), 2f);
                charge = u <= 1f ? u : 2f - u;
            }

            ManagedGlyphLod lod = _lod;
            if (!_forceLod)
            {
                UnityEngine.Camera cam = UnityEngine.Camera.main;
                float distance = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 0f;
                lod = ManagedGlyphRenderer.ChooseLod(distance);
            }

            Matrix4x4 matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            _renderer.Draw(spec, lod, matrix, spec.RevealForCharge(charge), _fade);
        }

        private ManagedGlyphSpec BuildSpec()
        {
            ManagedGlyphSpec spec = new ManagedGlyphSpec
            {
                Seed = _seed,
                MaxDepth = _maxDepth,
                BaseDepth = _baseDepth,
                Radius = _radius,
                LineWidth = _lineWidth,
                WheelCount = _wheels,
            };

            switch (_palette)
            {
                case ManagedGlyphPalettePreset.Water:
                    spec.Palette = ManagedGlyphPalette.Water;
                    break;
                case ManagedGlyphPalettePreset.Arcane:
                    spec.Palette = ManagedGlyphPalette.Arcane;
                    break;
                case ManagedGlyphPalettePreset.Prism:
                    spec.Palette = ManagedGlyphPalette.Prism;
                    break;
                default:
                    spec.Palette = ManagedGlyphPalette.Fire;
                    break;
            }

            return spec;
        }

        /// <summary>Vertex, triangle and stroke counts and bake time of the current glyph at every LOD. Bakes each LOD fresh.</summary>
        public string DescribeStats()
        {
            ManagedGlyphSpec spec = Spec;
            StringBuilder text = new StringBuilder();
            text.Append("Glyph seed ").Append(spec.Seed).Append(", depth ").Append(spec.Depth)
                .Append(", wheels ").Append(spec.Wheels).Append("\n");
            for (int i = 0; i < 3; i++)
            {
                ManagedGlyphLod lod = (ManagedGlyphLod)i;
                Stopwatch watch = Stopwatch.StartNew();
                ManagedGlyphMeshData data = ManagedGlyphBaker.Bake(spec, lod);
                watch.Stop();
                text.Append("  ").Append(lod).Append(": ").Append(data.VertexCount).Append(" vertices, ")
                    .Append(data.TriangleCount).Append(" triangles, ").Append(data.StrokeCount).Append(" strokes, ")
                    .Append(data.NodeCount).Append(" nodes, bake ").Append(watch.Elapsed.TotalMilliseconds.ToString("F1"))
                    .Append(" ms").Append(data.Truncated ? ", cut by the point budget" : "").Append("\n");
            }

            return text.ToString();
        }
    }
}
