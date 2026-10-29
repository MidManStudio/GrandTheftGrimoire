// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/plasma.md, section "ManagedPlasmaPreview.cs"
// ============================================================================

using System.Text;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Plasma
{
    /// <summary>
    /// Draws one plasma ring at this object for testing the look. Set the shape in the Inspector. The ring animates by
    /// itself in Play mode (in Edit mode it only redraws when Unity runs the scene update, for example after a value
    /// changes). The GTG, Plasma menu places and drives this component.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ManagedPlasmaPreview : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] private int _seed = 1;
        [SerializeField] private ManagedPlasmaPalette _palette = ManagedPlasmaPalette.Water;
        [Range(0.15f, 0.85f)] [SerializeField] private float _ringRadius = 0.55f;
        [Range(0.03f, 0.25f)] [SerializeField] private float _bandWidth = 0.11f;
        [Range(0f, 1f)] [SerializeField] private float _turbulence = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float _streaks = 0.8f;

        [Header("Size and time")]
        [Tooltip("Width of the drawn quad in metres. The ring and its wisps fit inside it.")]
        [SerializeField] private float _diameter = 2f;
        [Tooltip("Runs the noise with the game clock. Untick it to hold the ring at Age.")]
        [SerializeField] private bool _animate = true;
        [Tooltip("Seconds the noise has run, used when Animate is off.")]
        [SerializeField] private float _age;
        [Range(0f, 1f)] [SerializeField] private float _fade = 1f;

        [Header("Look")]
        [Tooltip("Adds the glow to what is behind it. Untick to blend it over the background with its alpha.")]
        [SerializeField] private bool _additive = true;
        [Range(0f, 4f)] [SerializeField] private float _intensity = 1f;
        [Tooltip("Turns the ring to face the main camera. Off: it faces the object's blue Z axis.")]
        [SerializeField] private bool _faceCamera;

        [Header("Draw path")]
        [Tooltip("Skips Graphics.DrawMeshInstanced and uses one Graphics.DrawMesh per ring, to test the fallback.")]
        [SerializeField] private bool _forceDrawMesh;

        private ManagedPlasmaRenderer _renderer;
        private ManagedPlasmaRingSpec _spec;
        private bool _rendererForcedDrawMesh;

        public int Seed { get { return _seed; } set { _seed = value; _spec = null; } }

        public ManagedPlasmaPalette Palette { get { return _palette; } set { _palette = value; _spec = null; } }

        public bool Additive { get { return _additive; } set { _additive = value; } }

        /// <summary>The draw path in use, or null before the first frame.</summary>
        public ManagedPlasmaDrawPath? DrawPath { get { return _renderer != null ? _renderer.Path : (ManagedPlasmaDrawPath?)null; } }

        /// <summary>The spec the Inspector values describe. Built again after a value changes.</summary>
        public ManagedPlasmaRingSpec Spec
        {
            get
            {
                if (_spec == null)
                {
                    _spec = new ManagedPlasmaRingSpec
                    {
                        Seed = _seed,
                        Palette = _palette,
                        RingRadius = _ringRadius,
                        BandWidth = _bandWidth,
                        Turbulence = _turbulence,
                        Streaks = _streaks,
                    };
                }

                return _spec;
            }
        }

        /// <summary>Moves to the next palette: Fire, Water, Arcane, then Fire again.</summary>
        public void CyclePalette()
        {
            Palette = (ManagedPlasmaPalette)(((int)_palette + 1) % ManagedPlasmaLimits.PaletteCount);
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
            // The renderer is built once. Only the draw path toggle needs a new one.
            if (_renderer != null && _rendererForcedDrawMesh != _forceDrawMesh)
            {
                _renderer.Dispose();
                _renderer = null;
            }

            if (_renderer == null)
            {
                _renderer = new ManagedPlasmaRenderer(gameObject.layer, _forceDrawMesh, "preview");
                _rendererForcedDrawMesh = _forceDrawMesh;
            }

            _renderer.Additive = _additive;
            _renderer.Intensity = _intensity;

            float age = _animate ? Time.time : _age;
            Quaternion rotation = transform.rotation;
            if (_faceCamera)
            {
                UnityEngine.Camera cam = UnityEngine.Camera.main;
                if (cam != null)
                {
                    Vector3 away = transform.position - cam.transform.position;
                    if (away.sqrMagnitude > 0.0001f)
                    {
                        rotation = Quaternion.LookRotation(away);
                    }
                }
            }

            float size = Mathf.Max(0.05f, _diameter);
            Matrix4x4 matrix = Matrix4x4.TRS(transform.position, rotation, new Vector3(size, size, size));
            _renderer.Draw(Spec, matrix, age, _fade);

            // One Flush per frame sends the queue: Graphics.DrawMeshInstanced, or Graphics.DrawMesh as the fallback.
            _renderer.Flush();
        }

        /// <summary>One line for the lab: the ring's shape, the draw path and the calls the last frame made.</summary>
        public string DescribeStats()
        {
            ManagedPlasmaRingSpec spec = Spec;
            StringBuilder text = new StringBuilder();
            text.Append("Plasma ring seed ").Append(spec.Seed).Append(", palette ").Append(spec.Palette)
                .Append(", radius ").Append(spec.RingRadius.ToString("F2")).Append(", band ").Append(spec.BandWidth.ToString("F2"))
                .Append(", turbulence ").Append(spec.Turbulence.ToString("F2")).Append(", streaks ").Append(spec.Streaks.ToString("F2"));
            if (_renderer == null)
            {
                text.Append(". Not drawing yet (it draws from Update).");
            }
            else if (!_renderer.CanDraw)
            {
                text.Append(". The shader is missing, so nothing draws.");
            }
            else
            {
                text.Append(". Draw path ").Append(_renderer.Path).Append(", draw calls last frame ").Append(_renderer.LastFlushCalls)
                    .Append(", ").Append(_additive ? "additive" : "alpha").Append(" blend.");
            }

            return text.ToString();
        }
    }
}
