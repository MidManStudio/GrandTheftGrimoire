// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCombatEngagement.cs"
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>
    /// Who is fighting one target and where each of them stands. Every NPC that is attacking the target
    /// holds a Member. A few members hold an attack token and take evenly spaced places on a ring just
    /// inside melee range, the rest wait on a wider ring and circle until a token comes free. The actors
    /// read their place from here and walk to it. Nothing here moves an NPC or decides to fight, Rust
    /// still chooses Attack, this only says where an attacker should be.
    /// One engagement lives on each ManagedNpcThreatSource. It does its work in a pass every 0.2 s, run by
    /// whichever actor asks first, so there is no driver to add to the scene.
    /// </summary>
    internal sealed class ManagedCombatEngagement
    {
        private const float PassIntervalSeconds = 0.2f;
        private const float OrbitLookAheadSeconds = 1f;
        private const float OrbitFlipSeconds = 8f;
        private const float BlockedBiasRadians = 0.6f;
        private const float MaxBiasRadians = 1.8f;
        private const float BiasSeconds = 3f;
        private const float Pi = 3.14159265f;
        private const float TwoPi = 6.28318531f;

        /// <summary>One attacker. The actor keeps the reference from Join to Leave.</summary>
        internal sealed class Member
        {
            internal ManagedNpcActor Actor;
            internal ManagedNpcBrain Brain;

            // Set by the actor at Join.
            internal float InnerRadius;
            internal float WaitRadius;
            internal float Spacing;
            internal float OrbitSpeed;

            // Written by the passes.
            internal bool HasToken;
            internal float TokenSince;
            internal float WaitingSince;
            internal float Angle;
            internal float Radius;
            internal float Bias;
            internal float BiasUntil;
            internal float Bearing;
            internal float Distance;

            // Set by the actor, read and cleared by the next pass.
            internal bool Blocked;
        }

        private readonly ManagedNpcThreatSource _target;
        private readonly List<Member> _members = new List<Member>();
        private Member[] _ring = new Member[8];
        private float[] _ringBearing = new float[8];
        private float _nextPass;
        private float _created;
        private Vector3 _center;

        internal ManagedCombatEngagement(ManagedNpcThreatSource target)
        {
            _target = target;
        }

        /// <summary>The point the rings are drawn around: where the most recent sighting put the target.</summary>
        internal Vector3 Center { get { return _center; } }

        internal int MemberCount { get { return _members.Count; } }

        internal int TokenHolderCount
        {
            get
            {
                int holders = 0;
                for (int i = 0; i < _members.Count; i++)
                {
                    if (_members[i].HasToken)
                    {
                        holders++;
                    }
                }

                return holders;
            }
        }

        /// <summary>
        /// Adds an attacker. It starts as a waiter and heads for the waiting ring, or stays put when it is already
        /// closer. The next pass, at most 0.2 s later, decides whether it gets a token.
        /// </summary>
        internal Member Join(ManagedNpcActor actor, ManagedNpcBrain brain, float now, float meleeRadius, float waitDistance, float spacing, float orbitSpeed)
        {
            // The first pass comes one interval after the first join, when the others that decided in the same tick
            // have joined too. So the attackers are weighed together, and the first to join gets no head start.
            if (_members.Count == 0)
            {
                _nextPass = now + PassIntervalSeconds;
                _created = now;
            }

            Member member = new Member
            {
                Actor = actor,
                Brain = brain,
                InnerRadius = meleeRadius,
                WaitRadius = meleeRadius + waitDistance,
                Spacing = spacing,
                OrbitSpeed = orbitSpeed,
                WaitingSince = now
            };

            // Until the first pass the member heads for the waiting ring along the line it already stands on. One that is
            // already closer than the ring stays where it is, so a near attacker does not walk away before it walks in.
            Vector3 here = brain.transform.position;
            Vector3 around = brain.LastKnownThreatPosition;
            float dx = here.x - around.x;
            float dz = here.z - around.z;
            member.Bearing = Mathf.Atan2(dz, dx);
            member.Angle = member.Bearing;
            member.Radius = Mathf.Max(member.InnerRadius, Mathf.Min(Mathf.Sqrt(dx * dx + dz * dz), member.WaitRadius));
            _center = around;
            _members.Add(member);
            return member;
        }

        internal void Leave(Member member)
        {
            _members.Remove(member);
        }

        /// <summary>Where a member should stand, on the ground plane of the ring center.</summary>
        internal Vector3 SlotPosition(Member member)
        {
            return new Vector3(
                _center.x + Mathf.Cos(member.Angle) * member.Radius,
                _center.y,
                _center.z + Mathf.Sin(member.Angle) * member.Radius);
        }

        /// <summary>Runs a pass when one is due. Cheap to call every frame.</summary>
        internal void Refresh(float now)
        {
            if (now < _nextPass)
            {
                return;
            }

            _nextPass = now + PassIntervalSeconds;
            int count = _members.Count;
            if (count == 0)
            {
                return;
            }

            // The shared center is the freshest sighting among the attackers. Each of them saw the target within the
            // lingering time, so nobody learns more than it would have known alone.
            ManagedNpcBrain freshest = null;
            float freshestTime = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                ManagedNpcBrain brain = _members[i].Brain;
                if (freshest == null || brain.LastSeenTime > freshestTime)
                {
                    freshest = brain;
                    freshestTime = brain.LastSeenTime;
                }
            }

            _center = freshest.LastKnownThreatPosition;

            for (int i = 0; i < count; i++)
            {
                Member member = _members[i];
                Vector3 position = member.Brain.transform.position;
                float dx = position.x - _center.x;
                float dz = position.z - _center.z;
                member.Distance = Mathf.Sqrt(dx * dx + dz * dz);
                member.Bearing = Mathf.Atan2(dz, dx);
            }

            AssignTokens(now);
            ApplyBlocked(now);
            AssignRing(true, now);
            AssignRing(false, now);
        }

        // Which members may strike. At most the target's limit, rotated so a waiter gets its turn.
        private void AssignTokens(float now)
        {
            int count = _members.Count;
            int limit = _target.MaxMeleeAttackers;
            if (limit <= 0 || limit >= count)
            {
                for (int i = 0; i < count; i++)
                {
                    Member member = _members[i];
                    if (!member.HasToken)
                    {
                        member.HasToken = true;
                        member.TokenSince = now;
                    }
                }

                return;
            }

            int holders = 0;
            for (int i = 0; i < count; i++)
            {
                if (_members[i].HasToken)
                {
                    holders++;
                }
            }

            // Over the limit, for instance after it was lowered: the farthest holders give up their place.
            while (holders > limit)
            {
                Member farthest = null;
                for (int i = 0; i < count; i++)
                {
                    Member member = _members[i];
                    if (member.HasToken && (farthest == null || member.Distance > farthest.Distance))
                    {
                        farthest = member;
                    }
                }

                Release(farthest, now);
                holders--;
            }

            // Rotation. A holder that kept its place for the rotation time gives it up, because some member is waiting.
            float rotation = _target.AttackerRotationSeconds;
            if (rotation > 0f)
            {
                for (int i = 0; i < count; i++)
                {
                    Member member = _members[i];
                    if (member.HasToken && now - member.TokenSince >= rotation)
                    {
                        Release(member, now);
                        holders--;
                    }
                }
            }

            // Fill the free places. The longest waiting goes first and the nearest wins a tie. A holder that was
            // just released has waited zero seconds, so it queues behind everyone who was already waiting.
            while (holders < limit)
            {
                Member best = null;
                for (int i = 0; i < count; i++)
                {
                    Member member = _members[i];
                    if (member.HasToken)
                    {
                        continue;
                    }

                    if (best == null
                        || member.WaitingSince < best.WaitingSince
                        || (member.WaitingSince == best.WaitingSince && member.Distance < best.Distance))
                    {
                        best = member;
                    }
                }

                if (best == null)
                {
                    break;
                }

                best.HasToken = true;
                best.TokenSince = now;
                holders++;
            }
        }

        private static void Release(Member member, float now)
        {
            member.HasToken = false;
            member.WaitingSince = now;
        }

        // A member that reported itself stuck is moved along its ring for a few seconds. The first nudge goes one way
        // or the other by NPC id, so two neighbours that block each other do not both step the same way.
        private void ApplyBlocked(float now)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                Member member = _members[i];
                if (member.Blocked)
                {
                    member.Blocked = false;
                    float direction = (member.Brain.Id & 1UL) == 0UL ? 1f : -1f;
                    float bias = member.Bias == 0f ? direction * BlockedBiasRadians : -member.Bias * 1.5f;
                    member.Bias = Mathf.Clamp(bias, -MaxBiasRadians, MaxBiasRadians);
                    member.BiasUntil = now + BiasSeconds;
                }
            }
        }

        // Spaces the members of one group evenly around the ring. The circle is cut into as many equal steps as there
        // are members, turned so that the members have the least to walk, and each member gets the step that keeps
        // the order they stand in. Waiters also get a step ahead along the circle, so they drift around the target.
        private void AssignRing(bool holders, float now)
        {
            int n = 0;
            for (int i = 0; i < _members.Count; i++)
            {
                Member member = _members[i];
                if (member.HasToken != holders)
                {
                    continue;
                }

                if (n == _ring.Length)
                {
                    System.Array.Resize(ref _ring, n * 2);
                    System.Array.Resize(ref _ringBearing, n * 2);
                }

                // Insertion sort by bearing. The groups are small and the pass runs five times a second.
                int at = n;
                while (at > 0 && _ringBearing[at - 1] > member.Bearing)
                {
                    _ring[at] = _ring[at - 1];
                    _ringBearing[at] = _ringBearing[at - 1];
                    at--;
                }

                _ring[at] = member;
                _ringBearing[at] = member.Bearing;
                n++;
            }

            if (n == 0)
            {
                return;
            }

            if (n == 1)
            {
                _ring[0].Angle = _ringBearing[0];
            }
            else
            {
                // Members keep the order they stand around the target. Turning the ring by whole steps would only give the
                // same places to the same members, so one turn is enough: the circular mean of how far each member stands
                // from its step, which leaves the members the least to walk.
                float step = TwoPi / n;
                float sumSin = 0f;
                float sumCos = 0f;
                for (int i = 0; i < n; i++)
                {
                    float d = _ringBearing[i] - step * i;
                    sumSin += Mathf.Sin(d);
                    sumCos += Mathf.Cos(d);
                }

                float offset = Mathf.Atan2(sumSin, sumCos);
                for (int i = 0; i < n; i++)
                {
                    _ring[i].Angle = offset + step * i;
                }
            }

            float orbit = (((int)((now - _created) / OrbitFlipSeconds)) & 1) == 0 ? 1f : -1f;
            for (int i = 0; i < n; i++)
            {
                Member member = _ring[i];
                if (holders)
                {
                    member.Radius = member.InnerRadius;
                }
                else
                {
                    // Enough circumference for everyone who waits, so a big crowd widens the ring and does not stack up.
                    member.Radius = Mathf.Max(member.WaitRadius, n * member.Spacing / TwoPi);
                    if (member.OrbitSpeed > 0f)
                    {
                        member.Angle += orbit * member.OrbitSpeed * OrbitLookAheadSeconds / member.Radius;
                    }
                }

                if (now < member.BiasUntil)
                {
                    member.Angle += member.Bias;
                }
                else
                {
                    member.Bias = 0f;
                }

                _ring[i] = null;
            }
        }

        private static float Wrap(float angle)
        {
            while (angle > Pi)
            {
                angle -= TwoPi;
            }

            while (angle < -Pi)
            {
                angle += TwoPi;
            }

            return angle;
        }
    }
}
