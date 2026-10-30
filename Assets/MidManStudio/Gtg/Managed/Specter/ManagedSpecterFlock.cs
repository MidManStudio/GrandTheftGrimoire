// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/specter.md, section "ManagedSpecterFlock.cs"
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Specter
{
    /// <summary>
    /// Moves a small flock of specter butterflies for the code-moved variant. Each butterfly drifts: a smooth wandering push, drag, a
    /// little lift, a wing flap, a life that runs 0 to 1 and a fade in and out. All plain values with no Unity objects, so the CI
    /// harness drives it frame by frame, and no Unity random: the flock has its own small generator, so a test run is repeatable.
    /// </summary>
    public sealed class ManagedSpecterFlock
    {
        /// <summary>One butterfly to draw this frame, in world space.</summary>
        public struct Item
        {
            /// <summary>Owned by the flock. Draw it at once and do not keep it.</summary>
            public ManagedSpecterSpec Spec;

            public Vector3 Position;

            /// <summary>Height of the drawn quad in metres. The width is this times <see cref="WidthScale"/>.</summary>
            public float Diameter;

            /// <summary>Wing flap, between the flock's FlapMin and 1. 1 is wings open.</summary>
            public float WidthScale;

            /// <summary>0 at birth to 1 at the end. Drives the pattern and the fade out inside the shader.</summary>
            public float Life;

            public float Fade;
        }

        private sealed class Butterfly
        {
            public ManagedSpecterSpec Spec;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Size;
            public float Age;
            public float Lifetime;
            public Vector3 WanderPhase;
            public Vector3 WanderRate;
            public float FlapPhase;
            public float FlapRate;
        }

        /// <summary>The most butterflies kept at once. A new one past this replaces the oldest.</summary>
        public const int HardMaxButterflies = 256;

        private readonly List<Butterfly> _flock = new List<Butterfly>();
        private int _serial;
        private uint _state = 2463534242u;

        /// <summary>Most butterflies alive at once, 1 to <see cref="HardMaxButterflies"/>.</summary>
        public int MaxButterflies = 32;

        /// <summary>Strength of the wandering push, in metres per second squared.</summary>
        public float Wander = 2.2f;

        /// <summary>How fast a butterfly loses speed, per second.</summary>
        public float Drag = 1.1f;

        /// <summary>A steady upward push, in metres per second squared.</summary>
        public float Lift = 0.25f;

        /// <summary>Speed limit, in metres per second.</summary>
        public float MaxSpeed = 1.6f;

        /// <summary>Wing flaps per second. Each butterfly flaps at 75 to 125 percent of it.</summary>
        public float FlapRate = 2.5f;

        /// <summary>Width scale with the wings nearly closed, 0 to 1.</summary>
        public float FlapMin = ManagedSpecterFlap.DefaultMin;

        /// <summary>Seconds a butterfly takes to fade in. 0 or less means it starts fully visible.</summary>
        public float FadeInSeconds = 0.2f;

        /// <summary>The share of the life, at the end, over which it fades out. 0.12 is the last 12 percent.</summary>
        public float FadeOutFraction = 0.12f;

        /// <summary>Longest step the motion takes in one go. A longer frame is split into one capped step.</summary>
        public float MaxStep = 0.1f;

        /// <summary>Butterflies alive now.</summary>
        public int Count { get { return _flock.Count; } }

        /// <summary>Starts the flock's own random sequence again from a number. The same number and the same calls give the same flock.</summary>
        public void Reseed(int seed)
        {
            _state = (uint)seed * 2654435761u + 1013904223u;
            if (_state == 0u)
            {
                _state = 2463534242u;
            }
        }

        /// <summary>
        /// Adds <paramref name="count"/> butterflies inside a sphere around <paramref name="center"/>. <paramref name="template"/> gives
        /// the palette and look and is not changed: every butterfly works on its own copy with a seed that differs from every earlier
        /// butterfly. Returns how many were added. It adds none for no template, a count, life or size of zero or less, or a center
        /// that is not a number. A count over the limit adds only as many as fit, the newest ones, and the oldest are replaced.
        /// </summary>
        public int Spawn(ManagedSpecterSpec template, Vector3 center, int count, float radius, float lifetime, float size)
        {
            if (template == null || count <= 0 || !(lifetime > 0f) || !(size > 0f) || !IsFinite(center)
                || float.IsInfinity(lifetime) || float.IsInfinity(size))
            {
                return 0;
            }

            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
            {
                radius = 0f;
            }

            int max = Mathf.Clamp(MaxButterflies, 1, HardMaxButterflies);
            int added = Mathf.Min(count, max);
            for (int i = 0; i < added; i++)
            {
                while (_flock.Count >= max)
                {
                    _flock.RemoveAt(0);
                }

                ManagedSpecterSpec spec = template.Clone();
                spec.Seed = template.Seed + _serial;
                _serial++;

                // A random point in a sphere by rejection, then a start velocity that leans upward.
                Vector3 offset = Vector3.zero;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    offset = new Vector3(Next() * 2f - 1f, Next() * 2f - 1f, Next() * 2f - 1f);
                    if (offset.sqrMagnitude <= 1f)
                    {
                        break;
                    }
                }

                Vector3 dir = new Vector3(Next() * 2f - 1f, Next() * 0.8f, Next() * 2f - 1f);
                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = Vector3.up;
                }

                _flock.Add(new Butterfly
                {
                    Spec = spec,
                    Position = center + offset * radius,
                    Velocity = dir.normalized * (0.3f + 0.5f * Next()),
                    Size = size,
                    Age = 0f,
                    Lifetime = lifetime,
                    WanderPhase = new Vector3(Next() * 6.2831853f, Next() * 6.2831853f, Next() * 6.2831853f),
                    WanderRate = new Vector3(0.8f + 1.1f * Next(), 0.8f + 1.1f * Next(), 0.8f + 1.1f * Next()),
                    FlapPhase = Next(),
                    FlapRate = FlapRate * (0.75f + 0.5f * Next()),
                });
            }

            return added;
        }

        /// <summary>
        /// Advances every butterfly by dt and fills <paramref name="output"/> with the ones still alive, oldest first. Returns how many.
        /// The list is cleared first. A butterfly whose time is up is removed in this call and not returned. A negative or NaN dt counts as 0.
        /// </summary>
        public int Update(float dt, List<Item> output)
        {
            output.Clear();
            if (float.IsNaN(dt) || dt < 0f)
            {
                dt = 0f;
            }

            float step = Mathf.Min(dt, Mathf.Max(0.001f, MaxStep));
            float speedLimit = Mathf.Max(0f, MaxSpeed);

            int write = 0;
            for (int read = 0; read < _flock.Count; read++)
            {
                Butterfly b = _flock[read];
                b.Age += dt;
                float life = b.Age / b.Lifetime;
                if (life >= 1f)
                {
                    continue;
                }

                _flock[write++] = b;

                // Smooth wandering push: three slow sines with their own phases. Up and down is gentler than sideways.
                Vector3 push = new Vector3(
                    Mathf.Sin(b.WanderRate.x * b.Age + b.WanderPhase.x),
                    0.6f * Mathf.Sin(b.WanderRate.y * b.Age + b.WanderPhase.y),
                    Mathf.Sin(b.WanderRate.z * b.Age + b.WanderPhase.z)) * Wander;
                Vector3 accel = push + Vector3.up * Lift - b.Velocity * Drag;
                b.Velocity += accel * step;
                float speed = b.Velocity.magnitude;
                if (speed > speedLimit)
                {
                    b.Velocity = b.Velocity * (speedLimit / speed);
                }

                b.Position += b.Velocity * step;
                b.FlapPhase += b.FlapRate * step;
                if (b.FlapPhase > 1000f)
                {
                    b.FlapPhase -= 1000f;
                }

                output.Add(new Item
                {
                    Spec = b.Spec,
                    Position = b.Position,
                    Diameter = b.Size,
                    WidthScale = ManagedSpecterFlap.WidthScale(b.FlapPhase, FlapMin),
                    Life = life,
                    Fade = FadeAt(b.Age, life),
                });
            }

            _flock.RemoveRange(write, _flock.Count - write);
            return output.Count;
        }

        /// <summary>Drops every butterfly at once.</summary>
        public void Clear()
        {
            _flock.Clear();
        }

        private float FadeAt(float age, float life)
        {
            return ManagedSpecterFade.At(age, life, FadeInSeconds, FadeOutFraction);
        }

        // xorshift32, 0 to 1 (never exactly 1).
        private float Next()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return (x & 0xFFFFFFu) / 16777216f;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }
    }
}
