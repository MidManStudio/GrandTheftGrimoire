// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphCastView.cs"
// ============================================================================

using System.Collections.Generic;
using System.Text;
using MidManStudio.Gtg.Managed.Magic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Glyphs
{
    /// <summary>
    /// Draws the glyph of a spell while the player's caster winds it up. Put it on the same GameObject as the
    /// ManagedSpellCaster. Each frame it reads the caster's cast status, lets a <see cref="ManagedGlyphCastTracker"/> decide
    /// which glyphs to show, and sends them through one <see cref="ManagedGlyphRenderer"/>, which draws them with
    /// Graphics.DrawMeshInstanced and falls back to Graphics.DrawMesh. The caster knows nothing about this component.
    /// </summary>
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ManagedSpellCaster))]
    public sealed class ManagedGlyphCastView : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Metres in front of the shot point, along the aim.")]
        [SerializeField] private float _forwardDistance = 0.6f;
        [Tooltip("Multiplies the size of every glyph. 1 is the size set in ManagedGlyphSpellTable.")]
        [SerializeField] private float _sizeScale = 1f;
        [SerializeField] private float _spinDegreesPerSecond = 18f;

        [Header("Fade out")]
        [Tooltip("Seconds a glyph takes to fade after the shot leaves.")]
        [SerializeField] private float _fadeSeconds = 0.35f;
        [SerializeField] private float _burstGrowth = 0.25f;

        [Header("Rendering")]
        [Tooltip("Layer the glyphs are drawn on. The Default layer is drawn by every camera, including a first person camera that hides the Player layer.")]
        [SerializeField] private int _layer;
        [Tooltip("Skips Graphics.DrawMeshInstanced and uses one Graphics.DrawMesh per glyph, to test the fallback.")]
        [SerializeField] private bool _forceDrawMesh;

        [Header("Debug")]
        [SerializeField] private bool _logCasts = true;

        private readonly ManagedGlyphCastTracker _tracker = new ManagedGlyphCastTracker();
        private readonly List<ManagedGlyphCastTracker.Item> _items = new List<ManagedGlyphCastTracker.Item>(8);
        private ManagedSpellCaster _caster;
        private ManagedGlyphRenderer _renderer;
        private bool _rendererForcedDrawMesh;
        private bool _wasActive;
        private ManagedCastStatus _lastStatus;

        private void Awake()
        {
            _caster = GetComponent<ManagedSpellCaster>();
        }

        private void OnDisable()
        {
            _tracker.Clear();
            _wasActive = false;
            DisposeRenderer();
        }

        // After Update, so the caster has moved the wind-up on, and after the character and the camera have moved.
        private void LateUpdate()
        {
            if (_caster == null)
            {
                return;
            }

            if (_renderer != null && _rendererForcedDrawMesh != _forceDrawMesh)
            {
                DisposeRenderer();
            }

            if (_renderer == null)
            {
                _renderer = new ManagedGlyphRenderer(_layer, false, _forceDrawMesh, "cast view");
                _rendererForcedDrawMesh = _forceDrawMesh;
            }

            _tracker.ForwardDistance = _forwardDistance;
            _tracker.SizeScale = _sizeScale;
            _tracker.SpinDegreesPerSecond = _spinDegreesPerSecond;
            _tracker.FadeSeconds = _fadeSeconds;
            _tracker.BurstGrowth = _burstGrowth;

            ManagedCastStatus status = _caster.ReadCastStatus();
            LogTransition(status);
            _lastStatus = status;

            _tracker.Update(status, Time.deltaTime, _items);

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            for (int i = 0; i < _items.Count; i++)
            {
                ManagedGlyphCastTracker.Item item = _items[i];
                float distance = cam != null ? Vector3.Distance(cam.transform.position, item.Position) : 0f;
                ManagedGlyphLod lod = ManagedGlyphRenderer.ChooseLod(distance);
                Quaternion rotation = FaceRotation(item.Forward) * Quaternion.AngleAxis(item.Spin, Vector3.forward);
                Matrix4x4 matrix = Matrix4x4.TRS(item.Position, rotation, Vector3.one * item.Scale);
                _renderer.Draw(item.Spec, lod, matrix, item.Reveal, item.Fade);
            }

            // One Flush per frame sends the queue: Graphics.DrawMeshInstanced, or Graphics.DrawMesh as the fallback.
            _renderer.Flush();
        }

        /// <summary>One line for the lab: what the view is doing right now.</summary>
        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Cast view: ");
            if (_renderer == null)
            {
                text.Append("not drawing yet (press Play and cast).");
                return text.ToString();
            }

            text.Append("draw path ").Append(_renderer.Path).Append(", shader ")
                .Append(_renderer.UsingCustomShader ? "custom" : "Sprites/Default fallback")
                .Append(", winding up ").Append(_lastStatus.Active)
                .Append(", fading ").Append(_tracker.FadingCount)
                .Append(", glyphs this frame ").Append(_items.Count)
                .Append(", draw calls last frame ").Append(_renderer.LastFlushCalls)
                .Append(", baked glyphs ").Append(_renderer.CachedCount).Append(".");
            return text.ToString();
        }

        // The glyph faces along its local Z. A straight up or down aim has no sideways to take from the up axis.
        private static Quaternion FaceRotation(Vector3 forward)
        {
            if (forward.sqrMagnitude < 1e-6f)
            {
                return Quaternion.identity;
            }

            Vector3 up = Mathf.Abs(Vector3.Dot(forward.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(forward, up);
        }

        private void LogTransition(ManagedCastStatus status)
        {
            if (!_logCasts || status.Active == _wasActive)
            {
                _wasActive = status.Active;
                return;
            }

            _wasActive = status.Active;
            if (status.Active)
            {
                ManagedGlyphSpec spec = ManagedGlyphSpellTable.For(status.Kind);
                Debug.Log("[GTG Glyph] " + status.Kind + " wind-up starts, " + status.Seconds.ToString("F2") + " s, " + spec.Style + " glyph seed " + spec.Seed
                    + ", depth " + spec.Depth + ", base depth " + spec.BaseDepth);
            }
            else
            {
                Debug.Log("[GTG Glyph] wind-up over, the glyph fades out");
            }
        }

        private void DisposeRenderer()
        {
            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }
        }
    }
}
