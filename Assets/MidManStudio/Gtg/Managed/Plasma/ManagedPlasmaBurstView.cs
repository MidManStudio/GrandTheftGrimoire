// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/plasma.md, section "ManagedPlasmaBurstView.cs"
// ============================================================================

using System.Collections.Generic;
using System.Text;
using MidManStudio.Gtg.Managed.Magic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Plasma
{
    /// <summary>
    /// Draws a plasma ring burst where every spell lands. It listens to <see cref="ManagedSpellCaster.Impact"/>, which the player's
    /// spells and the spells of NPC mages both raise, so one view in the scene covers every hit. The palette and size come from
    /// <see cref="ManagedPlasmaSpellTable"/>. Put one on any object, or use GTG, Plasma, Ensure Burst View. Spell damage and the
    /// chemistry field do not know about it, and it does not know about them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedPlasmaBurstView : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Multiplies how far the burst spreads. 1 puts the ring's edge a little inside the blast radius.")]
        [Range(0.3f, 3f)] [SerializeField] private float _sizeScale = 1f;
        [Range(0f, 4f)] [SerializeField] private float _intensity = 1.2f;
        [Tooltip("Adds the glow to what is behind it. Untick to blend it over the background with its alpha.")]
        [SerializeField] private bool _additive = true;
        [Tooltip("Turns every burst to face the camera. Off: a burst lies flat on the surface it hit.")]
        [SerializeField] private bool _faceCamera;

        [Header("Limits")]
        [Range(1, ManagedPlasmaBurstTracker.HardMaxBursts)] [SerializeField] private int _maxBursts = 8;

        [Header("Draw path")]
        [Tooltip("Skips Graphics.DrawMeshInstanced and uses one Graphics.DrawMesh per ring, to test the fallback.")]
        [SerializeField] private bool _forceDrawMesh;

        [Header("Debug")]
        [SerializeField] private bool _logBursts;

        private readonly ManagedPlasmaBurstTracker _tracker = new ManagedPlasmaBurstTracker();
        private readonly List<ManagedPlasmaBurstTracker.Item> _items = new List<ManagedPlasmaBurstTracker.Item>();
        private ManagedPlasmaRenderer _renderer;
        private bool _rendererForcedDrawMesh;
        private int _spawned;
        private int _refused;

        /// <summary>Bursts alive now.</summary>
        public int AliveCount { get { return _tracker.Count; } }

        /// <summary>Bursts started since this component enabled.</summary>
        public int SpawnedCount { get { return _spawned; } }

        /// <summary>The draw path in use, or null before the first frame.</summary>
        public ManagedPlasmaDrawPath? DrawPath { get { return _renderer != null ? _renderer.Path : (ManagedPlasmaDrawPath?)null; } }

        public bool Additive { get { return _additive; } set { _additive = value; } }

        private void OnEnable()
        {
            // Take the handler off first so a re-enable never listens twice.
            ManagedSpellCaster.Impact -= OnImpact;
            ManagedSpellCaster.Impact += OnImpact;
        }

        private void OnDisable()
        {
            ManagedSpellCaster.Impact -= OnImpact;
            _tracker.Clear();
            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }
        }

        private void OnImpact(ManagedSpellImpact impact)
        {
            Spawn(impact.Kind, impact.Position, impact.Normal);
        }

        /// <summary>
        /// Starts a burst for a spell hit at a point. The lab uses it to test without casting. A zero normal makes the burst
        /// face the camera. Returns false when the burst was refused, for a position that is not a number.
        /// </summary>
        public bool Spawn(ManagedSpellKind kind, Vector3 position, Vector3 normal)
        {
            ManagedPlasmaSpellTable.Style style = ManagedPlasmaSpellTable.For(kind);
            _tracker.MaxBursts = _maxBursts;
            _tracker.SizeScale = 0.9f * _sizeScale;

            bool added = _tracker.Add(style.Spec, position, normal, ManagedPlasmaSpellTable.BlastRadius(kind), style.Seconds);
            if (added)
            {
                _spawned++;
            }
            else
            {
                _refused++;
            }

            if (_logBursts)
            {
                Debug.Log("[GTG Plasma] " + (added ? "Burst " : "Burst refused ") + kind + " at " + position + ".");
            }

            return added;
        }

        private void LateUpdate()
        {
            // The renderer is built on the first frame with something to draw, and again when the draw path toggle changes.
            if (_renderer != null && _rendererForcedDrawMesh != _forceDrawMesh)
            {
                _renderer.Dispose();
                _renderer = null;
            }

            if (_tracker.Count == 0)
            {
                return;
            }

            if (_renderer == null)
            {
                _renderer = new ManagedPlasmaRenderer(gameObject.layer, _forceDrawMesh, "bursts");
                _rendererForcedDrawMesh = _forceDrawMesh;
                _renderer.MaxDrawsPerFrame = ManagedPlasmaBurstTracker.HardMaxBursts;
            }

            _renderer.Additive = _additive;
            _renderer.Intensity = _intensity;

            _tracker.Update(Time.deltaTime, _items);

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            for (int i = 0; i < _items.Count; i++)
            {
                ManagedPlasmaBurstTracker.Item item = _items[i];
                Quaternion rotation = RotationFor(item, cam);
                float size = item.Diameter;
                Matrix4x4 matrix = Matrix4x4.TRS(item.Position, rotation, new Vector3(size, size, size));
                _renderer.Draw(item.Spec, matrix, item.Age, item.Fade);
            }

            // One Flush per frame sends the queue: Graphics.DrawMeshInstanced, or Graphics.DrawMesh as the fallback.
            _renderer.Flush();
        }

        // Lies flat on the surface by default. A zero normal, or the Face Camera tick, turns the ring to the camera.
        private Quaternion RotationFor(ManagedPlasmaBurstTracker.Item item, UnityEngine.Camera cam)
        {
            bool wantsCamera = _faceCamera || item.Normal.sqrMagnitude < 0.0001f;
            if (wantsCamera && cam != null)
            {
                Vector3 away = item.Position - cam.transform.position;
                if (away.sqrMagnitude > 0.0001f)
                {
                    return FacingRotation(away);
                }
            }

            if (item.Normal.sqrMagnitude >= 0.0001f)
            {
                return FacingRotation(item.Normal);
            }

            return Quaternion.identity;
        }

        /// <summary>
        /// The rotation that points the ring's blue Z axis along a direction. A direction that is nearly straight up or down, which
        /// is every hit on flat ground, gets a sideways reference so the turn never depends on how LookRotation treats a reference
        /// parallel to the direction.
        /// </summary>
        internal static Quaternion FacingRotation(Vector3 direction)
        {
            Vector3 forward = direction.normalized;
            Vector3 reference = Mathf.Abs(forward.y) > 0.98f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(forward, reference);
        }

        /// <summary>One line for the lab: bursts alive and started, the draw path and the calls the last frame made.</summary>
        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Plasma bursts: ").Append(_tracker.Count).Append(" alive, ").Append(_spawned).Append(" started");
            if (_refused > 0)
            {
                text.Append(", ").Append(_refused).Append(" refused");
            }

            if (_renderer == null)
            {
                text.Append(". Not drawing yet (it draws in LateUpdate while a burst is alive).");
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
