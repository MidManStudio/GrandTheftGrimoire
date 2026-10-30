// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/specter.md, section "ManagedSpecterParticles.cs"
// ============================================================================

using System.Collections.Generic;
using System.Text;
using MidManStudio.Gtg.Managed.Plasma;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Specter
{
    /// <summary>
    /// Variant B: specter butterflies moved by Unity's built-in Particle System. The same shader as the code-moved variant
    /// (<see cref="ManagedSpecterSwarm"/>) with its <c>_SPECTER_STREAMS</c> keyword on, so it reads each particle's age and
    /// random number from vertex streams and the fade from the particle's color alpha. The flap is the size over lifetime
    /// (width only), the drift is the Noise module, the rise is negative gravity. A child object holds the Particle System
    /// and exists only while this component is enabled in Play mode. Look and motion settings are on this component, not on
    /// the child, because the child is rebuilt from them whenever a value changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedSpecterParticles : MonoBehaviour
    {
        /// <summary>Name of the child object that holds the Particle System.</summary>
        public const string ChildName = "GTG Specter Particles";

        /// <summary>The most particles the Inspector allows, the same cap as the code-moved flock.</summary>
        public const int HardMaxParticles = ManagedSpecterFlock.HardMaxButterflies;

        [Header("Look")]
        [SerializeField] private ManagedPlasmaPalette _palette = ManagedPlasmaPalette.Arcane;
        [Range(0f, 0.6f)] [SerializeField] private float _wobble = 0.3f;
        [Range(0.1f, 4f)] [SerializeField] private float _glow = 1f;
        [Range(0f, 3f)] [SerializeField] private float _churn = 1f;
        [Range(0.5f, 3f)] [SerializeField] private float _reach = 1f;
        [Tooltip("HDR brightness. Above 1 feeds the URP Bloom, which makes the glow. Turn on HDR in the URP asset and add a Bloom override to a Volume.")]
        [Range(0f, 8f)] [SerializeField] private float _intensity = 2f;
        [Tooltip("Adds the glow to what is behind it. Untick to blend it over the background with its alpha.")]
        [SerializeField] private bool _additive = true;

        [Header("Butterflies")]
        [Tooltip("Height of one butterfly in metres. Each one is 80 to 120 percent of this.")]
        [Range(0.1f, 3f)] [SerializeField] private float _size = 0.7f;
        [Tooltip("Seconds a butterfly lives. Each one lives 75 to 125 percent of this.")]
        [Range(0.5f, 15f)] [SerializeField] private float _lifetime = 3f;
        [Tooltip("Butterflies appear inside a sphere of this radius around the object.")]
        [Range(0f, 3f)] [SerializeField] private float _spawnRadius = 0.5f;
        [Range(1, HardMaxParticles)] [SerializeField] private int _maxParticles = 32;
        [Tooltip("Butterflies released per second on their own. 0 means only Burst releases them.")]
        [Range(0f, 30f)] [SerializeField] private float _autoPerSecond;

        [Header("Motion")]
        [Tooltip("Wing flaps over one butterfly's life.")]
        [Range(1f, 20f)] [SerializeField] private float _flapCycles = 7f;
        [Tooltip("Width scale with the wings nearly closed, 0 to 1.")]
        [Range(0.05f, 1f)] [SerializeField] private float _flapMin = ManagedSpecterFlap.DefaultMin;
        [Tooltip("Strength of the Noise module's wandering push.")]
        [Range(0f, 3f)] [SerializeField] private float _flutter = 0.8f;
        [Tooltip("Upward drift in metres per second squared. It is gravity with the sign flipped.")]
        [Range(0f, 2f)] [SerializeField] private float _rise = 0.25f;
        [Tooltip("Speed each butterfly starts with, in metres per second. Each one is 30 to 100 percent of this.")]
        [Range(0f, 3f)] [SerializeField] private float _startSpeed = 0.6f;

        [Header("Debug")]
        [SerializeField] private bool _logBursts;

        private GameObject _child;
        private ParticleSystem _system;
        private ParticleSystemRenderer _systemRenderer;
        private Material _material;
        private Texture2D _ramp;
        private bool _dirty = true;
        private bool _shaderMissing;
        private int _emitted;

        /// <summary>Butterflies alive now, or 0 before the system exists.</summary>
        public int AliveCount { get { return _system != null ? _system.particleCount : 0; } }

        /// <summary>Butterflies released by Burst since this component enabled. Ones released by the auto rate are not counted.</summary>
        public int SpawnedCount { get { return _emitted; } }

        public ManagedPlasmaPalette Palette
        {
            get { return _palette; }
            set { _palette = value; _dirty = true; }
        }

        public bool Additive
        {
            get { return _additive; }
            set { _additive = value; _dirty = true; }
        }

        /// <summary>The child's Particle System, or null while this component is disabled or the shader is missing.</summary>
        public ParticleSystem Particles { get { return _system; } }

        /// <summary>The material shared by every butterfly, or null while there is none.</summary>
        public Material SpecterMaterial { get { return _material; } }

        /// <summary>Moves to the next palette: Fire, Water, Arcane, then Fire again.</summary>
        public void CyclePalette()
        {
            Palette = (ManagedPlasmaPalette)(((int)_palette + 1) % ManagedPlasmaLimits.PaletteCount);
        }

        private void OnValidate()
        {
            _dirty = true;
        }

        private void OnEnable()
        {
            Build();
        }

        private void OnDisable()
        {
            Teardown();
        }

        private void Update()
        {
            if (_system == null)
            {
                return;
            }

            if (_dirty)
            {
                ApplySettings();
            }
        }

        /// <summary>Releases <paramref name="count"/> butterflies around this object. Returns how many the Particle System took.</summary>
        public int Burst(int count)
        {
            return Burst(transform.position, count);
        }

        /// <summary>Releases <paramref name="count"/> butterflies around a point. Returns how many the Particle System took.</summary>
        public int Burst(Vector3 position, int count)
        {
            if (count <= 0 || float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z)
                || float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
            {
                return 0;
            }

            if (_system == null)
            {
                Build();
                if (_system == null)
                {
                    return 0;
                }
            }

            if (_dirty)
            {
                ApplySettings();
            }

            // The Particle System drops what does not fit under Max Particles, so only the room left is asked for.
            int room = Mathf.Max(0, _maxParticles - _system.particleCount);
            int added = Mathf.Min(count, room);
            if (added <= 0)
            {
                return 0;
            }

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            emit.position = position;
            emit.applyShapeToPosition = true;
            _system.Emit(emit, added);
            _emitted += added;
            if (_logBursts)
            {
                Debug.Log("[GTG Specter] Particles released " + added + " at " + position + ".");
            }

            return added;
        }

        // Builds the child object, the material and the Particle System. Does nothing when one exists already.
        private void Build()
        {
            if (_system != null)
            {
                return;
            }

            Shader shader = ManagedSpecterAssets.FindShader();
            if (shader == null)
            {
                if (!_shaderMissing)
                {
                    Debug.LogError("[GTG Specter] Shader " + ManagedSpecterAssets.ShaderName + " was not found or is not supported. "
                        + "Particle butterflies will not draw. Add the shader to Always Included Shaders if this is a build.");
                }

                _shaderMissing = true;
                return;
            }

            _shaderMissing = false;

            _ramp = ManagedSpecterAssets.BuildRamp();
            _material = new Material(shader);
            _material.name = "GTG Specter Particles";
            _material.SetTexture("_RampTex", _ramp);
            _material.EnableKeyword(ManagedSpecterAssets.StreamsKeyword);
            _material.SetFloat("_UseStreams", 1f);

            _child = new GameObject(ChildName);
            _child.transform.SetParent(transform, false);
            _child.layer = gameObject.layer;
            _system = _child.AddComponent<ParticleSystem>();

            // A new Particle System starts playing with its default look, so it is stopped before it is set up.
            _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _systemRenderer = _child.GetComponent<ParticleSystemRenderer>();
            if (_systemRenderer == null)
            {
                _systemRenderer = _child.AddComponent<ParticleSystemRenderer>();
            }

            BuildStaticSettings();
            ApplySettings();
            _system.Play();
        }

        private void Teardown()
        {
            if (_system != null)
            {
                _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (_child != null)
            {
                ManagedSpecterAssets.DestroyObject(_child);
            }

            ManagedSpecterAssets.DestroyObject(_material);
            ManagedSpecterAssets.DestroyObject(_ramp);
            _child = null;
            _system = null;
            _systemRenderer = null;
            _material = null;
            _ramp = null;
            _emitted = 0;
            _dirty = true;
        }

        // Settings that never change with the Inspector values.
        private void BuildStaticSettings()
        {
            ParticleSystem.MainModule main = _system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Color.white;

            // The Billboard turns each particle to face the camera. The width and height come from the size over lifetime below.
            _systemRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            _systemRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _systemRenderer.receiveShadows = false;
            _systemRenderer.sharedMaterial = _material;

            // Streams, in the order the shader reads them: Position, Color, UV, AgePercent, StableRandomX.
            // Unity packs UV into TEXCOORD0.xy, then AgePercent into .z and StableRandomX into .w.
            List<ParticleSystemVertexStream> streams = new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.AgePercent,
                ParticleSystemVertexStream.StableRandomX,
            };
            _systemRenderer.SetActiveVertexStreams(streams);

            ParticleSystem.ShapeModule shape = _system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radiusThickness = 1f;

            ParticleSystem.NoiseModule noise = _system.noise;
            noise.enabled = true;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.4f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            // Alpha is the shader's fade: in over the first 6 percent of the life, out over the last 12.
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.06f),
                    new GradientAlphaKey(1f, 0.88f),
                    new GradientAlphaKey(0f, 1f),
                });
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        // Settings that follow the Inspector. Safe to call again at any time.
        private void ApplySettings()
        {
            _dirty = false;
            if (_system == null)
            {
                return;
            }

            _maxParticles = Mathf.Clamp(_maxParticles, 1, HardMaxParticles);
            float size = Mathf.Max(0.05f, _size);
            float lifetime = Mathf.Max(0.1f, _lifetime);

            ParticleSystem.MainModule main = _system.main;
            main.maxParticles = _maxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime * 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(_startSpeed * 0.3f, _startSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.2f);
            main.gravityModifier = -Mathf.Max(0f, _rise);

            ParticleSystem.EmissionModule emission = _system.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Max(0f, _autoPerSecond);

            ParticleSystem.ShapeModule shape = _system.shape;
            shape.radius = Mathf.Max(0.001f, _spawnRadius);

            ParticleSystem.NoiseModule noise = _system.noise;
            noise.strength = Mathf.Max(0f, _flutter);

            // The flap: width over life, two curves a quarter flap apart. Each particle takes a random blend of them, so the
            // butterflies do not all open and close together. Height stays 1.
            float[] timesA;
            float[] valuesA;
            float[] timesB;
            float[] valuesB;
            ManagedSpecterFlap.BuildCurveKeys(_flapCycles, _flapMin, 0f, out timesA, out valuesA);
            ManagedSpecterFlap.BuildCurveKeys(_flapCycles, _flapMin, 0.25f, out timesB, out valuesB);
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = _system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.separateAxes = true;
            sizeOverLifetime.x = new ParticleSystem.MinMaxCurve(1f, CurveFrom(timesA, valuesA), CurveFrom(timesB, valuesB));
            sizeOverLifetime.y = new ParticleSystem.MinMaxCurve(1f);

            if (_material != null)
            {
                _material.SetFloat("_DstBlend", (float)(_additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                _material.SetFloat("_Intensity", Mathf.Clamp(_intensity, 0f, 8f));
                _material.SetFloat("_PaletteRow", ManagedPlasmaRingSpec.RowFor(_palette));
                _material.SetFloat("_Wobble", Mathf.Clamp(_wobble, ManagedSpecterLimits.MinWobble, ManagedSpecterLimits.MaxWobble));
                _material.SetFloat("_Glow", Mathf.Clamp(_glow, ManagedSpecterLimits.MinGlow, ManagedSpecterLimits.MaxGlow));
                _material.SetFloat("_Churn", Mathf.Clamp(_churn, ManagedSpecterLimits.MinChurn, ManagedSpecterLimits.MaxChurn));
                _material.SetFloat("_Reach", Mathf.Clamp(_reach, ManagedSpecterLimits.MinReach, ManagedSpecterLimits.MaxReach));
            }
        }

        private static AnimationCurve CurveFrom(float[] times, float[] values)
        {
            Keyframe[] keys = new Keyframe[times.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = new Keyframe(times[i], values[i]);
            }

            return new AnimationCurve(keys);
        }

        /// <summary>One line for the lab: butterflies alive, how many Burst released, the palette, the vertex streams and the blend mode.</summary>
        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Specter particles (Particle System): ");
            if (_shaderMissing)
            {
                return text.Append("the shader is missing, so nothing draws.").ToString();
            }

            if (_system == null)
            {
                return text.Append("not built yet. It builds when this component enables in Play mode.").ToString();
            }

            List<ParticleSystemVertexStream> active = new List<ParticleSystemVertexStream>();
            _systemRenderer.GetActiveVertexStreams(active);
            text.Append(_system.particleCount).Append(" alive, ").Append(_emitted).Append(" released by Burst, palette ").Append(_palette)
                .Append(", ").Append(active.Count).Append(" vertex streams, ").Append(_additive ? "additive" : "alpha").Append(" blend.");
            return text.ToString();
        }
    }
}
