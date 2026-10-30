// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/specter.md, section "ManagedSpecterSwarm.cs"
// ============================================================================

using System.Collections.Generic;
using System.Text;
using MidManStudio.Gtg.Managed.Plasma;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Specter
{
    /// <summary>
    /// Variant A: specter butterflies moved by code. A small flock (<see cref="ManagedSpecterFlock"/>) flies them and the
    /// <see cref="ManagedSpecterRenderer"/> draws them as camera-facing quads through Graphics.DrawMeshInstanced, with
    /// Graphics.DrawMesh as the fallback. Call <see cref="Burst(int)"/> to release some at this object, or set an auto rate.
    /// Variant B is <see cref="ManagedSpecterParticles"/>, the same shader on the built-in Particle System.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedSpecterSwarm : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private ManagedPlasmaPalette _palette = ManagedPlasmaPalette.Arcane;
        [SerializeField] private int _seed = 1;
        [Range(0f, 0.6f)] [SerializeField] private float _wobble = 0.3f;
        [Range(0.1f, 4f)] [SerializeField] private float _glow = 1f;
        [Range(0f, 3f)] [SerializeField] private float _churn = 1f;
        [Range(0.5f, 3f)] [SerializeField] private float _reach = 1f;
        [Tooltip("HDR brightness. Above 1 feeds the URP Bloom, which makes the glow. Turn on HDR in the URP asset and add a Bloom override to a Volume.")]
        [Range(0f, 8f)] [SerializeField] private float _intensity = 2f;
        [Tooltip("Adds the glow to what is behind it. Untick to blend it over the background with its alpha.")]
        [SerializeField] private bool _additive = true;

        [Header("Butterflies")]
        [Tooltip("Height of one butterfly in metres.")]
        [Range(0.1f, 3f)] [SerializeField] private float _size = 0.7f;
        [Tooltip("Seconds a butterfly lives. Its pattern changes over this time and it fades out at the end.")]
        [Range(0.5f, 15f)] [SerializeField] private float _lifetime = 3f;
        [Tooltip("Butterflies appear inside a sphere of this radius around the object.")]
        [Range(0f, 3f)] [SerializeField] private float _spawnRadius = 0.5f;
        [Range(1, ManagedSpecterFlock.HardMaxButterflies)] [SerializeField] private int _maxButterflies = 32;
        [Tooltip("Butterflies released per second on their own. 0 means only Burst releases them.")]
        [Range(0f, 30f)] [SerializeField] private float _autoPerSecond;
        [Range(0f, 8f)] [SerializeField] private float _flapRate = 2.5f;
        [Range(0f, 6f)] [SerializeField] private float _wander = 2.2f;
        [Range(0f, 2f)] [SerializeField] private float _lift = 0.25f;

        [Header("Draw path")]
        [Tooltip("Skips Graphics.DrawMeshInstanced and uses one Graphics.DrawMesh per butterfly, to test the fallback.")]
        [SerializeField] private bool _forceDrawMesh;

        [Header("Debug")]
        [SerializeField] private bool _logBursts;

        private readonly ManagedSpecterFlock _flock = new ManagedSpecterFlock();
        private readonly List<ManagedSpecterFlock.Item> _items = new List<ManagedSpecterFlock.Item>();
        private ManagedSpecterRenderer _renderer;
        private bool _rendererForcedDrawMesh;
        private float _autoCarry;
        private int _spawned;

        /// <summary>Butterflies alive now.</summary>
        public int AliveCount { get { return _flock.Count; } }

        /// <summary>Butterflies released since this component enabled.</summary>
        public int SpawnedCount { get { return _spawned; } }

        /// <summary>The draw path in use, or null before the first frame that had something to draw.</summary>
        public ManagedPlasmaDrawPath? DrawPath { get { return _renderer != null ? _renderer.Path : (ManagedPlasmaDrawPath?)null; } }

        public ManagedPlasmaPalette Palette { get { return _palette; } set { _palette = value; } }

        public int Seed { get { return _seed; } set { _seed = value; } }

        public bool Additive { get { return _additive; } set { _additive = value; } }

        /// <summary>The flock, for tests and tuning.</summary>
        public ManagedSpecterFlock Flock { get { return _flock; } }

        /// <summary>Moves to the next palette: Fire, Water, Arcane, then Fire again.</summary>
        public void CyclePalette()
        {
            _palette = (ManagedPlasmaPalette)(((int)_palette + 1) % ManagedPlasmaLimits.PaletteCount);
        }

        private void OnDisable()
        {
            _flock.Clear();
            _autoCarry = 0f;
            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }
        }

        /// <summary>Releases <paramref name="count"/> butterflies around this object. Returns how many were added.</summary>
        public int Burst(int count)
        {
            return Burst(transform.position, count);
        }

        /// <summary>Releases <paramref name="count"/> butterflies around a point. Returns how many were added.</summary>
        public int Burst(Vector3 position, int count)
        {
            ApplySettingsToFlock();
            int added = _flock.Spawn(BuildTemplate(), position, count, _spawnRadius, _lifetime, _size);
            _spawned += added;
            if (_logBursts)
            {
                Debug.Log("[GTG Specter] Swarm released " + added + " at " + position + ".");
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

            if (_autoPerSecond > 0f)
            {
                _autoCarry += _autoPerSecond * Time.deltaTime;
                int whole = (int)_autoCarry;
                if (whole > 0)
                {
                    _autoCarry -= whole;
                    Burst(whole);
                }
            }

            if (_flock.Count == 0)
            {
                return;
            }

            if (_renderer == null)
            {
                _renderer = new ManagedSpecterRenderer(gameObject.layer, _forceDrawMesh, "swarm");
                _rendererForcedDrawMesh = _forceDrawMesh;
                _renderer.MaxDrawsPerFrame = ManagedSpecterFlock.HardMaxButterflies;
            }

            _renderer.Additive = _additive;
            _renderer.Intensity = _intensity;

            ApplySettingsToFlock();
            _flock.Update(Time.deltaTime, _items);

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            for (int i = 0; i < _items.Count; i++)
            {
                ManagedSpecterFlock.Item item = _items[i];
                Quaternion rotation = RotationFor(item.Position, cam);
                Matrix4x4 matrix = Matrix4x4.TRS(item.Position, rotation, new Vector3(item.Diameter * item.WidthScale, item.Diameter, item.Diameter));
                _renderer.Draw(item.Spec, matrix, item.Life, item.Fade);
            }

            // One Flush per frame sends the queue: Graphics.DrawMeshInstanced, or Graphics.DrawMesh as the fallback.
            _renderer.Flush();
        }

        private ManagedSpecterSpec BuildTemplate()
        {
            return new ManagedSpecterSpec
            {
                Seed = _seed,
                Palette = _palette,
                Wobble = _wobble,
                Glow = _glow,
                Churn = _churn,
                Reach = _reach,
            };
        }

        private void ApplySettingsToFlock()
        {
            _flock.MaxButterflies = _maxButterflies;
            _flock.FlapRate = _flapRate;
            _flock.Wander = _wander;
            _flock.Lift = _lift;
        }

        // Faces the camera. The quad's width stays horizontal, so the flap reads as wings opening and closing.
        private static Quaternion RotationFor(Vector3 position, UnityEngine.Camera cam)
        {
            if (cam == null)
            {
                return Quaternion.identity;
            }

            Vector3 away = position - cam.transform.position;
            if (away.sqrMagnitude < 0.0001f)
            {
                return Quaternion.identity;
            }

            return FacingRotation(away);
        }

        /// <summary>
        /// The rotation that points the quad's blue Z axis along a direction. A direction that is nearly straight up or down gets a
        /// sideways reference, so the turn never depends on how LookRotation treats a reference parallel to the direction.
        /// </summary>
        internal static Quaternion FacingRotation(Vector3 direction)
        {
            Vector3 forward = direction.normalized;
            Vector3 reference = Mathf.Abs(forward.y) > 0.98f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(forward, reference);
        }

        /// <summary>One line for the lab: butterflies alive and released, the draw path, the draw calls of the last frame and the blend mode.</summary>
        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Specter swarm (code-moved quads): ").Append(_flock.Count).Append(" alive, ").Append(_spawned).Append(" released, palette ").Append(_palette);
            if (_renderer == null)
            {
                text.Append(". Not drawing yet (it draws in LateUpdate while a butterfly is alive).");
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
