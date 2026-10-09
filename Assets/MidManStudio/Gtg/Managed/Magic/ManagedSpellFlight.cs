// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSpellFlight.cs"
// ============================================================================

using System;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Magic
{
    public enum ManagedFlightState : byte
    {
        Flying = 0,

        /// <summary>The SP ran out. The bubble collapses and the payload releases there.</summary>
        Collapsed = 1,

        /// <summary>The hard cap on flight time was reached, which only a zero burn can do.</summary>
        CapReached = 2,
    }

    /// <summary>Everything a shot needs at spawn, as plain values. A vessel item can fill it later.</summary>
    public struct ManagedShotSpawn
    {
        public Vector3 Position;
        public Vector3 Velocity;

        /// <summary>The caller's own index for visuals and damage. Flight stores it and never reads it.</summary>
        public int Profile;

        /// <summary>The caller's payload id, carried to the impact. Flight stores it and never reads it.</summary>
        public int Payload;

        /// <summary>SP in the vessel's crystal at the moment of the cast.</summary>
        public float Sp;

        public float HoldBurnPerSecond;
        public float DriveBurnPerMeter;
        public float MaxAgeSeconds;

        /// <summary>True when the SP drives the shot. False for a thrown shot.</summary>
        public bool Powered;

        /// <summary>Speed a powered shot holds. Unused when the shot is unpowered.</summary>
        public float Speed;

        /// <summary>Share of world gravity that bends the path. Zero flies straight.</summary>
        public float GravityScale;

        /// <summary>Speed lost to drag per second, for an unpowered shot.</summary>
        public float DragPerSecond;
    }

    /// <summary>
    /// Shots in flight, kept as parallel arrays of plain values with no class reference per
    /// shot. This module knows nothing about spell definitions, payloads or physics: every
    /// number it uses arrives in the spawn record. Collision stays with the caller:
    /// <see cref="Plan"/> says where a shot wants to go, the caller sweeps that segment, and
    /// <see cref="Commit"/> moves the shot and burns its SP.
    /// </summary>
    public sealed class ManagedSpellFlight
    {
        private Vector3[] _position;
        private Vector3[] _velocity;
        private float[] _sp;
        private float[] _spFull;
        private float[] _holdBurn;
        private float[] _driveBurn;
        private float[] _travelled;
        private float[] _age;
        private float[] _maxAge;
        private float[] _speed;
        private float[] _gravityScale;
        private float[] _drag;
        private bool[] _powered;
        private int[] _profile;
        private int[] _payload;
        private int _count;

        public ManagedSpellFlight(int capacity)
        {
            int size = Math.Max(1, capacity);
            _position = new Vector3[size];
            _velocity = new Vector3[size];
            _sp = new float[size];
            _spFull = new float[size];
            _holdBurn = new float[size];
            _driveBurn = new float[size];
            _travelled = new float[size];
            _age = new float[size];
            _maxAge = new float[size];
            _speed = new float[size];
            _gravityScale = new float[size];
            _drag = new float[size];
            _powered = new bool[size];
            _profile = new int[size];
            _payload = new int[size];
        }

        public int Count { get { return _count; } }

        public Vector3 PositionAt(int index) { return _position[index]; }

        public Vector3 VelocityAt(int index) { return _velocity[index]; }

        public int ProfileAt(int index) { return _profile[index]; }

        public int PayloadAt(int index) { return _payload[index]; }

        public float TravelledAt(int index) { return _travelled[index]; }

        /// <summary>Remaining SP as a share of the SP at the cast, from 0 to 1.</summary>
        public float SpFractionAt(int index)
        {
            return _spFull[index] > 0f ? Mathf.Clamp01(_sp[index] / _spFull[index]) : 0f;
        }

        public void Add(ManagedShotSpawn spawn)
        {
            if (_count == _position.Length)
            {
                Grow();
            }

            int i = _count++;
            _position[i] = spawn.Position;
            _velocity[i] = spawn.Velocity;
            _sp[i] = spawn.Sp;
            _spFull[i] = spawn.Sp;
            _holdBurn[i] = spawn.HoldBurnPerSecond;
            _driveBurn[i] = spawn.DriveBurnPerMeter;
            _travelled[i] = 0f;
            _age[i] = 0f;
            _maxAge[i] = spawn.MaxAgeSeconds;
            _speed[i] = spawn.Speed;
            _gravityScale[i] = spawn.GravityScale;
            _drag[i] = spawn.DragPerSecond;
            _powered[i] = spawn.Powered;
            _profile[i] = spawn.Profile;
            _payload[i] = spawn.Payload;
        }

        /// <summary>
        /// Swap-removes a shot. The last shot takes its place, so a loop that counts down from
        /// the end never visits a shot twice or skips one.
        /// </summary>
        public void RemoveAt(int index)
        {
            int last = _count - 1;
            if (index != last)
            {
                _position[index] = _position[last];
                _velocity[index] = _velocity[last];
                _sp[index] = _sp[last];
                _spFull[index] = _spFull[last];
                _holdBurn[index] = _holdBurn[last];
                _driveBurn[index] = _driveBurn[last];
                _travelled[index] = _travelled[last];
                _age[index] = _age[last];
                _maxAge[index] = _maxAge[last];
                _speed[index] = _speed[last];
                _gravityScale[index] = _gravityScale[last];
                _drag[index] = _drag[last];
                _powered[index] = _powered[last];
                _profile[index] = _profile[last];
                _payload[index] = _payload[last];
            }

            _count = last;
        }

        public void Clear()
        {
            _count = 0;
        }

        /// <summary>
        /// Updates the velocity of a shot for this step and returns the segment it wants to
        /// fly. A powered shot keeps its speed, so gravity and drag only bend the path. An
        /// unpowered shot, a thrown one, loses speed to drag and gains it from gravity.
        /// </summary>
        public void Plan(int index, float dt, float gravityY, out Vector3 start, out Vector3 end)
        {
            Vector3 velocity = _velocity[index];

            if (_gravityScale[index] != 0f)
            {
                velocity.y += gravityY * _gravityScale[index] * dt;
            }

            if (_drag[index] > 0f)
            {
                velocity /= 1f + _drag[index] * dt;
            }

            if (_powered[index])
            {
                float speed = velocity.magnitude;
                if (speed > 1e-4f)
                {
                    velocity *= _speed[index] / speed;
                }
            }

            _velocity[index] = velocity;
            start = _position[index];
            end = start + velocity * dt;
        }

        /// <summary>
        /// Moves a shot to <paramref name="end"/> and burns its SP. When the SP runs out inside
        /// the step, <paramref name="releasedAt"/> is the point on the segment where it ran
        /// out, so range does not depend on the frame rate. Otherwise it is the end point.
        /// </summary>
        public ManagedFlightState Commit(int index, float dt, Vector3 end, out Vector3 releasedAt)
        {
            Vector3 start = _position[index];
            Vector3 step = end - start;
            float length = step.magnitude;

            float before = _sp[index];
            float burn = _holdBurn[index] * dt + _driveBurn[index] * length;

            _age[index] += dt;
            releasedAt = end;

            if (before - burn <= 0f)
            {
                float fraction = burn > 0f ? Mathf.Clamp01(before / burn) : 1f;
                releasedAt = start + step * fraction;
                _position[index] = releasedAt;
                _travelled[index] += length * fraction;
                _sp[index] = 0f;
                return ManagedFlightState.Collapsed;
            }

            _position[index] = end;
            _travelled[index] += length;
            _sp[index] = before - burn;

            return _age[index] >= _maxAge[index]
                ? ManagedFlightState.CapReached
                : ManagedFlightState.Flying;
        }

        private void Grow()
        {
            int size = _position.Length * 2;
            Array.Resize(ref _position, size);
            Array.Resize(ref _velocity, size);
            Array.Resize(ref _sp, size);
            Array.Resize(ref _spFull, size);
            Array.Resize(ref _holdBurn, size);
            Array.Resize(ref _driveBurn, size);
            Array.Resize(ref _travelled, size);
            Array.Resize(ref _age, size);
            Array.Resize(ref _maxAge, size);
            Array.Resize(ref _speed, size);
            Array.Resize(ref _gravityScale, size);
            Array.Resize(ref _drag, size);
            Array.Resize(ref _powered, size);
            Array.Resize(ref _profile, size);
            Array.Resize(ref _payload, size);
        }
    }
}
