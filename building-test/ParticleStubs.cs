// Hand written stand-ins for the UnityEngine Particle System APIs the specter particle variant uses. Verification harness only.
// The module types are structs that point at shared data, as in Unity, so `ps.main.loop = true` is a compile error here too
// (CS1612) and a module copied into a local variable still writes through to the system.
// The stub does not simulate particles: Emit only counts them, up to Max Particles, and records where they were asked for.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Keyframe
    {
        public float time, value;
        public Keyframe(float time, float value) { this.time = time; this.value = value; }
    }

    public class AnimationCurve
    {
        public Keyframe[] keys;
        public AnimationCurve(params Keyframe[] keys) { this.keys = keys ?? new Keyframe[0]; }
        public int length { get { return keys.Length; } }
        public static AnimationCurve Constant(float timeStart, float timeEnd, float value)
        {
            return new AnimationCurve(new Keyframe(timeStart, value), new Keyframe(timeEnd, value));
        }
        public float Evaluate(float time)
        {
            // Straight lines between keys. Unity smooths them, which a check of the key values does not need.
            if (keys.Length == 0) return 0f;
            if (time <= keys[0].time) return keys[0].value;
            for (int i = 1; i < keys.Length; i++)
            {
                if (time <= keys[i].time)
                {
                    float span = keys[i].time - keys[i - 1].time;
                    float t = span > 0f ? (time - keys[i - 1].time) / span : 1f;
                    return keys[i - 1].value + (keys[i].value - keys[i - 1].value) * t;
                }
            }
            return keys[keys.Length - 1].value;
        }
    }

    public struct GradientColorKey
    {
        public Color color; public float time;
        public GradientColorKey(Color col, float time) { color = col; this.time = time; }
    }

    public struct GradientAlphaKey
    {
        public float alpha, time;
        public GradientAlphaKey(float alpha, float time) { this.alpha = alpha; this.time = time; }
    }

    public class Gradient
    {
        public GradientColorKey[] colorKeys = new GradientColorKey[0];
        public GradientAlphaKey[] alphaKeys = new GradientAlphaKey[0];
        public void SetKeys(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys)
        {
            if (colorKeys != null && colorKeys.Length > 8) throw new ArgumentException("A gradient takes at most 8 color keys");
            if (alphaKeys != null && alphaKeys.Length > 8) throw new ArgumentException("A gradient takes at most 8 alpha keys");
            this.colorKeys = colorKeys; this.alphaKeys = alphaKeys;
        }
        public float EvaluateAlpha(float time)
        {
            if (alphaKeys == null || alphaKeys.Length == 0) return 1f;
            if (time <= alphaKeys[0].time) return alphaKeys[0].alpha;
            for (int i = 1; i < alphaKeys.Length; i++)
            {
                if (time <= alphaKeys[i].time)
                {
                    float span = alphaKeys[i].time - alphaKeys[i - 1].time;
                    float t = span > 0f ? (time - alphaKeys[i - 1].time) / span : 1f;
                    return alphaKeys[i - 1].alpha + (alphaKeys[i].alpha - alphaKeys[i - 1].alpha) * t;
                }
            }
            return alphaKeys[alphaKeys.Length - 1].alpha;
        }
    }

    public enum ParticleSystemSimulationSpace { Local, World, Custom }
    public enum ParticleSystemShapeType { Sphere = 0, Hemisphere = 1, Cone = 4, Box = 5 }
    public enum ParticleSystemRenderMode { Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh, None }
    public enum ParticleSystemNoiseQuality { Low, Medium, High }
    public enum ParticleSystemStopBehavior { StopEmittingAndClear, StopEmitting }
    public enum ParticleSystemCurveMode { Constant, Curve, TwoCurves, TwoConstants }

    // The same member names and order as Unity's enum.
    public enum ParticleSystemVertexStream
    {
        Position, Normal, Tangent, Color, UV, UV2, UV3, UV4, AnimBlend, AnimFrame, Center, VertexID, SizeX, SizeXY, SizeXYZ,
        Rotation, Rotation3D, RotationSpeed, RotationSpeed3D, Velocity, Speed, AgePercent, InvStartLifetime,
        StableRandomX, StableRandomXY, StableRandomXYZ, StableRandomXYZW,
        VaryingRandomX, VaryingRandomXY, VaryingRandomXYZ, VaryingRandomXYZW,
        Custom1X, Custom1XY, Custom1XYZ, Custom1XYZW, Custom2X, Custom2XY, Custom2XYZ, Custom2XYZW,
        NoiseSumX, NoiseSumXY, NoiseSumXYZ, NoiseImpulseX, NoiseImpulseXY, NoiseImpulseXYZ,
        MeshIndex, ParticleIndex, ColorPackedAsTwoFloats, MeshAxisOfRotation, NextTrailCenter, PreviousTrailCenter,
        PercentageAlongTrail, TrailWidth,
    }

    public class ParticleSystem : Component, IStubLifecycle
    {
        public struct MinMaxCurve
        {
            public ParticleSystemCurveMode mode;
            public float constant, constantMin, constantMax, curveMultiplier;
            public AnimationCurve curve, curveMin, curveMax;

            public MinMaxCurve(float constant) : this() { mode = ParticleSystemCurveMode.Constant; this.constant = constant; }
            public MinMaxCurve(float min, float max) : this() { mode = ParticleSystemCurveMode.TwoConstants; constantMin = min; constantMax = max; }
            public MinMaxCurve(float multiplier, AnimationCurve curve) : this() { mode = ParticleSystemCurveMode.Curve; curveMultiplier = multiplier; this.curve = curve; }
            public MinMaxCurve(float multiplier, AnimationCurve min, AnimationCurve max) : this()
            {
                mode = ParticleSystemCurveMode.TwoCurves; curveMultiplier = multiplier; curveMin = min; curveMax = max;
            }
            public static implicit operator MinMaxCurve(float constant) { return new MinMaxCurve(constant); }
        }

        public struct MinMaxGradient
        {
            public Gradient gradient;
            public Color color;
            public MinMaxGradient(Gradient gradient) : this() { this.gradient = gradient; }
            public MinMaxGradient(Color color) : this() { this.color = color; }
            public static implicit operator MinMaxGradient(Color color) { return new MinMaxGradient(color); }
            public static implicit operator MinMaxGradient(Gradient gradient) { return new MinMaxGradient(gradient); }
        }

        public struct EmitParams
        {
            public Vector3 position;
            private bool _applyShape;
            public bool applyShapeToPosition { get { return _applyShape; } set { _applyShape = value; } }
        }

        // ---- shared data behind the module structs -------------------------------------------------------------
        internal sealed class Data
        {
            public bool loop = true, playOnAwake = true;
            public ParticleSystemSimulationSpace simulationSpace = ParticleSystemSimulationSpace.Local;
            public MinMaxGradient startColor = new MinMaxGradient(Color.white);
            public MinMaxCurve startLifetime = 5f, startSpeed = 5f, startSize = 1f, gravityModifier = 0f;
            public int maxParticles = 1000;

            public bool emissionEnabled = true;
            public MinMaxCurve rateOverTime = 10f;

            public bool shapeEnabled = true;
            public ParticleSystemShapeType shapeType = ParticleSystemShapeType.Cone;
            public float radius = 1f, radiusThickness = 1f;

            public bool noiseEnabled;
            public MinMaxCurve noiseStrength = 1f, noiseScrollSpeed = 0f;
            public float noiseFrequency = 0.5f;
            public bool noiseDamping = true;
            public ParticleSystemNoiseQuality noiseQuality = ParticleSystemNoiseQuality.High;

            public bool sizeEnabled, sizeSeparateAxes;
            public MinMaxCurve sizeX = 1f, sizeY = 1f;

            public bool colorEnabled;
            public MinMaxGradient colorOverLifetime = new MinMaxGradient(Color.white);
        }

        public struct MainModule
        {
            internal Data d;
            public bool loop { get { return d.loop; } set { d.loop = value; } }
            public bool playOnAwake { get { return d.playOnAwake; } set { d.playOnAwake = value; } }
            public ParticleSystemSimulationSpace simulationSpace { get { return d.simulationSpace; } set { d.simulationSpace = value; } }
            public MinMaxGradient startColor { get { return d.startColor; } set { d.startColor = value; } }
            public MinMaxCurve startLifetime { get { return d.startLifetime; } set { d.startLifetime = value; } }
            public MinMaxCurve startSpeed { get { return d.startSpeed; } set { d.startSpeed = value; } }
            public MinMaxCurve startSize { get { return d.startSize; } set { d.startSize = value; } }
            public MinMaxCurve gravityModifier { get { return d.gravityModifier; } set { d.gravityModifier = value; } }
            public int maxParticles { get { return d.maxParticles; } set { d.maxParticles = value; } }
        }

        public struct EmissionModule
        {
            internal Data d;
            public bool enabled { get { return d.emissionEnabled; } set { d.emissionEnabled = value; } }
            public MinMaxCurve rateOverTime { get { return d.rateOverTime; } set { d.rateOverTime = value; } }
        }

        public struct ShapeModule
        {
            internal Data d;
            public bool enabled { get { return d.shapeEnabled; } set { d.shapeEnabled = value; } }
            public ParticleSystemShapeType shapeType { get { return d.shapeType; } set { d.shapeType = value; } }
            public float radius { get { return d.radius; } set { d.radius = value; } }
            public float radiusThickness { get { return d.radiusThickness; } set { d.radiusThickness = value; } }
        }

        public struct NoiseModule
        {
            internal Data d;
            public bool enabled { get { return d.noiseEnabled; } set { d.noiseEnabled = value; } }
            public MinMaxCurve strength { get { return d.noiseStrength; } set { d.noiseStrength = value; } }
            public float frequency { get { return d.noiseFrequency; } set { d.noiseFrequency = value; } }
            public MinMaxCurve scrollSpeed { get { return d.noiseScrollSpeed; } set { d.noiseScrollSpeed = value; } }
            public bool damping { get { return d.noiseDamping; } set { d.noiseDamping = value; } }
            public ParticleSystemNoiseQuality quality { get { return d.noiseQuality; } set { d.noiseQuality = value; } }
        }

        public struct SizeOverLifetimeModule
        {
            internal Data d;
            public bool enabled { get { return d.sizeEnabled; } set { d.sizeEnabled = value; } }
            public bool separateAxes { get { return d.sizeSeparateAxes; } set { d.sizeSeparateAxes = value; } }
            public MinMaxCurve x { get { return d.sizeX; } set { d.sizeX = value; } }
            public MinMaxCurve y { get { return d.sizeY; } set { d.sizeY = value; } }
        }

        public struct ColorOverLifetimeModule
        {
            internal Data d;
            public bool enabled { get { return d.colorEnabled; } set { d.colorEnabled = value; } }
            public MinMaxGradient color { get { return d.colorOverLifetime; } set { d.colorOverLifetime = value; } }
        }

        // ---- the system ----------------------------------------------------------------------------------------
        private readonly Data _d = new Data();

        public MainModule main { get { return new MainModule { d = _d }; } }
        public EmissionModule emission { get { return new EmissionModule { d = _d }; } }
        public ShapeModule shape { get { return new ShapeModule { d = _d }; } }
        public NoiseModule noise { get { return new NoiseModule { d = _d }; } }
        public SizeOverLifetimeModule sizeOverLifetime { get { return new SizeOverLifetimeModule { d = _d }; } }
        public ColorOverLifetimeModule colorOverLifetime { get { return new ColorOverLifetimeModule { d = _d }; } }

        public bool isPlaying { get; private set; }
        public bool isStopped { get { return !isPlaying; } }
        public int particleCount { get; private set; }

        /// <summary>Where each Emit call with EmitParams asked for particles, in order, for tests.</summary>
        public readonly List<Vector3> EmitPositions = new List<Vector3>();
        public readonly List<bool> EmitAppliedShape = new List<bool>();
        public int EmitCalls;

        // A new Particle System starts playing, and Unity adds its renderer with it.
        public void OnAdded()
        {
            isPlaying = true;
            if (gameObject.GetComponent<ParticleSystemRenderer>() == null) gameObject.AddComponent<ParticleSystemRenderer>();
        }

        public void Play() { isPlaying = true; }
        public void Play(bool withChildren) { isPlaying = true; }
        public void Stop() { Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        public void Stop(bool withChildren) { Stop(withChildren, ParticleSystemStopBehavior.StopEmitting); }
        public void Stop(bool withChildren, ParticleSystemStopBehavior stopBehavior)
        {
            isPlaying = false;
            if (stopBehavior == ParticleSystemStopBehavior.StopEmittingAndClear) particleCount = 0;
        }
        public void Clear() { particleCount = 0; }

        public void Emit(int count) { Add(count); EmitCalls++; }
        public void Emit(EmitParams emitParams, int count)
        {
            EmitPositions.Add(emitParams.position);
            EmitAppliedShape.Add(emitParams.applyShapeToPosition);
            Add(count);
            EmitCalls++;
        }

        // Unity drops the particles that do not fit under Max Particles.
        private void Add(int count)
        {
            if (count < 0) throw new ArgumentException("count must not be negative");
            particleCount = Math.Min(_d.maxParticles, particleCount + count);
        }

        /// <summary>Stands in for the particles dying, for tests.</summary>
        public void StubSetParticleCount(int count) { particleCount = Math.Max(0, Math.Min(count, _d.maxParticles)); }
    }

    public class ParticleSystemRenderer : Renderer
    {
        public ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard;
        private readonly List<ParticleSystemVertexStream> _streams = new List<ParticleSystemVertexStream>
        {
            // Unity's defaults for a Billboard.
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
        };
        public int SetStreamsCalls;

        public void SetActiveVertexStreams(List<ParticleSystemVertexStream> streams)
        {
            if (streams == null) throw new ArgumentNullException("streams");
            _streams.Clear();
            _streams.AddRange(streams);
            SetStreamsCalls++;
        }

        public void GetActiveVertexStreams(List<ParticleSystemVertexStream> streams)
        {
            if (streams == null) throw new ArgumentNullException("streams");
            streams.Clear();
            streams.AddRange(_streams);
        }
    }
}
