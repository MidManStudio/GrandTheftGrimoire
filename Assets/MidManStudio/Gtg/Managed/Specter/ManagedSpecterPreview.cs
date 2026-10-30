// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/specter.md, section "ManagedSpecterPreview.cs"
// ============================================================================

using System.Text;
using MidManStudio.Gtg.Managed.Plasma;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Specter
{
    /// <summary>
    /// Draws one specter butterfly at this object for testing the look. Set the shape in the Inspector. In Play mode its pattern
    /// runs through a whole life again and again (in Edit mode it redraws only when Unity runs the scene update, for example after
    /// a value changes, so use the Life slider with Animate off to scrub it). The GTG, Specter menu places and drives this component.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ManagedSpecterPreview : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] private int _seed = 1;
        [SerializeField] private ManagedPlasmaPalette _palette = ManagedPlasmaPalette.Arcane;
        [Range(0f, 0.6f)] [SerializeField] private float _wobble = 0.3f;
        [Range(0.1f, 4f)] [SerializeField] private float _glow = 1f;
        [Range(0f, 3f)] [SerializeField] private float _churn = 1f;
        [Range(0.5f, 3f)] [SerializeField] private float _reach = 1f;

        [Header("Size and time")]
        [Tooltip("Height of the drawn quad in metres. The butterfly fits inside it.")]
        [Range(0.1f, 6f)] [SerializeField] private float _diameter = 1.5f;
        [Tooltip("Runs the butterfly through its life with the game clock. Untick it to hold it at Life.")]
        [SerializeField] private bool _animate = true;
        [Tooltip("Seconds one pass through the butterfly's life takes while Animate is on.")]
        [Range(0.5f, 15f)] [SerializeField] private float _loopSeconds = 4f;
        [Tooltip("Where in its life the butterfly is, used when Animate is off. 0 is birth and 1 is the end.")]
        [Range(0f, 1f)] [SerializeField] private float _life = 0.3f;
        [Tooltip("Flaps the wings (the quad's width) while Animate is on.")]
        [SerializeField] private bool _flap = true;
        [Range(0f, 1f)] [SerializeField] private float _fade = 1f;

        [Header("Look")]
        [Tooltip("Adds the glow to what is behind it. Untick to blend it over the background with its alpha.")]
        [SerializeField] private bool _additive = true;
        [Tooltip("HDR brightness. Above 1 feeds the URP Bloom.")]
        [Range(0f, 8f)] [SerializeField] private float _intensity = 2f;
        [Tooltip("Turns the butterfly to face the main camera. Off: it faces the object's blue Z axis.")]
        [SerializeField] private bool _faceCamera = true;

        [Header("Draw path")]
        [Tooltip("Skips Graphics.DrawMeshInstanced and uses Graphics.DrawMesh, to test the fallback.")]
        [SerializeField] private bool _forceDrawMesh;

        private ManagedSpecterRenderer _renderer;
        private ManagedSpecterSpec _spec;
        private bool _rendererForcedDrawMesh;

        public int Seed { get { return _seed; } set { _seed = value; _spec = null; } }

        public ManagedPlasmaPalette Palette { get { return _palette; } set { _palette = value; _spec = null; } }

        public bool Additive { get { return _additive; } set { _additive = value; } }

        /// <summary>The draw path in use, or null before the first frame.</summary>
        public ManagedPlasmaDrawPath? DrawPath { get { return _renderer != null ? _renderer.Path : (ManagedPlasmaDrawPath?)null; } }

        /// <summary>The spec the Inspector values describe. Built again after a value changes.</summary>
        public ManagedSpecterSpec Spec
        {
            get
            {
                if (_spec == null)
                {
                    _spec = new ManagedSpecterSpec
                    {
                        Seed = _seed,
                        Palette = _palette,
                        Wobble = _wobble,
                        Glow = _glow,
                        Churn = _churn,
                        Reach = _reach,
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
                _renderer = new ManagedSpecterRenderer(gameObject.layer, _forceDrawMesh, "preview");
                _rendererForcedDrawMesh = _forceDrawMesh;
            }

            _renderer.Additive = _additive;
            _renderer.Intensity = _intensity;

            float life = _life;
            float fade = _fade;
            float width = 1f;
            if (_animate)
            {
                float seconds = Mathf.Max(0.5f, _loopSeconds);
                float passes = Time.time / seconds;
                life = passes - Mathf.Floor(passes);
                fade *= ManagedSpecterFade.At(life * seconds, life, 0.2f, 0.12f);
                if (_flap)
                {
                    width = ManagedSpecterFlap.WidthScale(Time.time * 2.5f, ManagedSpecterFlap.DefaultMin);
                }
            }

            Quaternion rotation = transform.rotation;
            if (_faceCamera)
            {
                UnityEngine.Camera cam = UnityEngine.Camera.main;
                if (cam != null)
                {
                    Vector3 away = transform.position - cam.transform.position;
                    if (away.sqrMagnitude > 0.0001f)
                    {
                        rotation = ManagedSpecterSwarm.FacingRotation(away);
                    }
                }
            }

            float size = Mathf.Max(0.05f, _diameter);
            Matrix4x4 matrix = Matrix4x4.TRS(transform.position, rotation, new Vector3(size * width, size, size));
            _renderer.Draw(Spec, matrix, life, fade);

            // One Flush per frame sends the queue: Graphics.DrawMeshInstanced, or Graphics.DrawMesh as the fallback.
            _renderer.Flush();
        }

        /// <summary>One line for the lab: the butterfly's shape, the draw path and the calls the last frame made.</summary>
        public string DescribeStats()
        {
            ManagedSpecterSpec spec = Spec;
            StringBuilder text = new StringBuilder();
            text.Append("Specter butterfly seed ").Append(spec.Seed).Append(", palette ").Append(spec.Palette)
                .Append(", wobble ").Append(spec.Wobble.ToString("F2")).Append(", glow ").Append(spec.Glow.ToString("F2"))
                .Append(", churn ").Append(spec.Churn.ToString("F2")).Append(", reach ").Append(spec.Reach.ToString("F2"));
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
