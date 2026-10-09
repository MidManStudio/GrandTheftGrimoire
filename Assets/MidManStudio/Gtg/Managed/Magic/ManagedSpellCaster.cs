// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSpellCaster.cs"
// ============================================================================

using System;
using MidManStudio.Gtg.Managed.CharacterController;
using MidManStudio.Gtg.Managed.Rendering;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>
    /// Spell caster. A cast draws a vessel from the belt, leaves the shot point and flies
    /// toward the crosshair, powered by the SP the vessel gave up. A bottle is used up by one
    /// cast. An orb is emptied by it and charges back with game time. Each step sweeps a small
    /// sphere from the old position to the new one, so a fast shot cannot pass through a thin
    /// collider. A hit, or the bubble collapsing when the SP runs out, raises
    /// <see cref="Impact"/>. Flight state lives in <see cref="ManagedSpellFlight"/> and vessel
    /// state in <see cref="ManagedVesselBelt"/>. A steered spell takes a lock at the cast from
    /// <see cref="ManagedSpellSeeker"/> and turns toward it every step. Shots are drawn through
    /// one <see cref="ManagedSphereBatch"/>, with no GameObject per shot. Put it on the same
    /// GameObject as ManagedCharacter.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ManagedCharacter))]
    public sealed class ManagedSpellCaster : MonoBehaviour
    {
        /// <summary>Raised once per hit, from Update of the caster.</summary>
        public static event Action<ManagedSpellImpact> Impact;

        [Header("Shot point")]
        [Tooltip("Where shots leave. Created under the character when empty. A staff or hand bone also works.")]
        [SerializeField] private Transform _shotPoint;
        [Tooltip("Position of the created shot point, in the character's local space from the feet pivot.")]
        [SerializeField] private Vector3 _shotPointOffset = new Vector3(0.35f, 1.45f, 0.7f);

        [Header("Aim")]
        [Tooltip("Shots fly from the shot point to what the screen center looks at.")]
        [SerializeField] private bool _aimAtCrosshair = true;
        [SerializeField] private float _aimMaxDistance = 120f;
        [Tooltip("An aim point nearer than this to the shot point falls back to the camera direction.")]
        [SerializeField] private float _minAimDistance = 1.5f;

        [Header("Vessels")]
        [Tooltip("One entry per belt slot, in key order. Empty uses the default: a fireball orb, ice bottles and a seeker orb.")]
        [SerializeField] private ManagedLoadoutEntry[] _loadout;

        [Header("Collision")]
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("Rendering")]
        [Tooltip("Skips hardware instancing and uses the combined mesh path. For testing the fallback.")]
        [SerializeField] private bool _forceCombinedMesh;
        [SerializeField] private int _maxShots = 128;

        [Header("Debug")]
        [SerializeField] private bool _logImpacts = true;

        private const int OverlapBufferSize = 8;
        private const float RestartPadding = 0.02f;
        private const int MaxOwnHitSkips = 4;

        // A steered shot this close to its point stops turning, so it does not spin in place.
        private const float MinSteerDistanceSquared = 0.25f;

        // After a cast with nothing to draw, how long before the next try. Keeps a held fire
        // button from logging every frame.
        private const float EmptyRetrySeconds = 0.3f;

        // A shot with no SP left is drawn at these fractions of its full brightness and size.
        private const float MinDim = 0.35f;
        private const float MinScale = 0.6f;

        private readonly ManagedSpellFlight _flight = new ManagedSpellFlight(16);
        private readonly ManagedSpellSeeker _seeker = new ManagedSpellSeeker(16);
        private readonly Collider[] _overlaps = new Collider[OverlapBufferSize];
        private ManagedVesselBelt _belt;
        private ManagedCharacter _character;
        private ManagedSphereBatch _batch;
        private float _cooldownRemaining;
        private int _selected;

        /// <summary>The spell in the selected belt slot.</summary>
        public ManagedSpellDefinition SelectedSpell
        {
            get { return ManagedSpellDefinition.For(_belt.SpellAt(_selected)); }
        }

        /// <summary>A short line about the selected vessel, for the debug overlay.</summary>
        public string SelectedVesselText
        {
            get { return _belt.Describe(_selected); }
        }

        public int ShotCount { get { return _flight.Count; } }

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Impact = null;
        }

        private void Awake()
        {
            _character = GetComponent<ManagedCharacter>();

            if (_shotPoint == null)
            {
                var point = new GameObject("GTG Shot Point");
                _shotPoint = point.transform;
                _shotPoint.SetParent(transform, false);
                _shotPoint.localPosition = _shotPointOffset;
            }

            BuildBelt();
            _batch = new ManagedSphereBatch(_maxShots, gameObject.layer, _forceCombinedMesh, "shots");
        }

        // Only entries whose spell has a definition get a slot, so the keys never point at a hole.
        private void BuildBelt()
        {
            ManagedLoadoutEntry[] entries = _loadout != null && _loadout.Length > 0 ? _loadout : DefaultLoadout();

            int valid = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && ManagedSpellDefinition.For(entries[i].Spell) != null)
                {
                    valid++;
                }
            }

            _belt = new ManagedVesselBelt(valid);
            int slot = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                ManagedLoadoutEntry entry = entries[i];
                ManagedSpellDefinition spell = entry != null ? ManagedSpellDefinition.For(entry.Spell) : null;
                if (spell == null)
                {
                    continue;
                }

                _belt.Set(slot++, entry.Vessel, entry.Spell, spell.VesselSp, spell.VesselRating, entry.Bottles, entry.OrbRechargeSeconds);
            }
        }

        private static ManagedLoadoutEntry[] DefaultLoadout()
        {
            return new[]
            {
                new ManagedLoadoutEntry { Spell = ManagedSpellKind.Fireball, Vessel = ManagedVesselKind.Orb },
                new ManagedLoadoutEntry { Spell = ManagedSpellKind.Ice, Vessel = ManagedVesselKind.Bottle, Bottles = 5 },
                new ManagedLoadoutEntry { Spell = ManagedSpellKind.Seeker, Vessel = ManagedVesselKind.Orb },
            };
        }

        /// <summary>Fills every orb and restores every bottle count. For testing, and a seam for a cheat.</summary>
        [ContextMenu("Refill vessels")]
        public void RefillVessels()
        {
            _belt.Refill();
        }

        private void OnDestroy()
        {
            if (_batch != null)
            {
                _batch.Dispose();
                _batch = null;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - dt);
            _belt.Tick(dt);

            SelectSpell();

            if (_character.Has(ManagedCharacterFeature.Cast)
                && _character.Input.FirePressed
                && _cooldownRemaining <= 0f)
            {
                Cast();
            }

            StepShots(dt);
        }

        private void LateUpdate()
        {
            _batch.Clear();
            for (int i = 0; i < _flight.Count; i++)
            {
                ManagedSpellDefinition spell = ManagedSpellDefinition.At(_flight.ProfileAt(i));

                // The shot shrinks and dims as its SP drains, so a dying spell can be seen.
                float charge = _flight.SpFractionAt(i);
                float dim = Mathf.Lerp(MinDim, 1f, charge);
                Color color = new Color(spell.Color.r * dim, spell.Color.g * dim, spell.Color.b * dim, spell.Color.a);
                float diameter = spell.VisualDiameter * Mathf.Lerp(MinScale, 1f, charge);

                _batch.Add(_flight.PositionAt(i), Vector3.one * diameter, color);
            }

            _batch.Draw();
        }

        private void SelectSpell()
        {
            ManagedCharacterInputFrame input = _character.Input;
            int count = _belt.SlotCount;

            if (input.SpellSlot > 0 && input.SpellSlot <= count)
            {
                _selected = input.SpellSlot - 1;
            }
            else if (input.CycleSpell != 0)
            {
                _selected = (_selected + input.CycleSpell + count) % count;
            }
        }

        private void Cast()
        {
            ManagedSpellDefinition spell = SelectedSpell;
            ManagedVesselDraw draw;
            if (!_belt.TryDraw(_selected, out draw))
            {
                _cooldownRemaining = EmptyRetrySeconds;
                if (_logImpacts)
                {
                    Debug.Log("[GTG Magic] " + spell.DisplayName + " cannot be cast, " + _belt.Describe(_selected));
                }

                return;
            }

            if (_logImpacts)
            {
                Debug.Log(
                    "[GTG Magic] " + spell.DisplayName + " cast with " + draw.Sp.ToString("F0") +
                    " of " + draw.Capacity.ToString("F0") + " SP");
            }

            Vector3 origin = _shotPoint.position;
            Vector3 aimPoint;
            Vector3 direction = ComputeAimDirection(origin, out aimPoint);
            int payload = ManagedSpellPayloads.IdFor(spell.Kind);
            _cooldownRemaining = spell.CooldownSeconds;

            // A shot point inside a wall would sweep from the wrong side, so it explodes at once.
            if (IsBlockedAt(origin, spell.SweepRadius))
            {
                Detonate(spell, payload, origin, -direction, "was cast inside a collider", 0f);
                return;
            }

            ManagedShotSpawn spawn = spell.CreateSpawn(
                ManagedSpellDefinition.IndexOf(spell.Kind), payload, origin, direction, draw);
            if (spell.Seeks)
            {
                spawn.Target = _seeker.Lock(
                    origin, direction, aimPoint, spell.SeekRange, spell.SeekConeHalfAngleDegrees, _hitMask, transform);

                if (_logImpacts)
                {
                    Debug.Log("[GTG Magic] " + spell.DisplayName + " locked onto " + _seeker.Describe(spawn.Target));
                }
            }

            _flight.Add(spawn);
        }

        // The ray through the screen center decides the target point. The shot then flies from
        // the shot point to that point, so it lands on the crosshair even though it leaves the hand.
        // The aim point is where the crosshair lands, or a far point along the aim when the
        // direction fell back to the camera. A steered spell without a target flies to it.
        private Vector3 ComputeAimDirection(Vector3 origin, out Vector3 aimPoint)
        {
            Vector3 byAngles = Quaternion.Euler(_character.Pitch, _character.Yaw, 0f) * Vector3.forward;

            UnityEngine.Camera cam = _aimAtCrosshair ? UnityEngine.Camera.main : null;
            if (cam == null)
            {
                aimPoint = origin + byAngles * _aimMaxDistance;
                return byAngles;
            }

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 target = ray.origin + ray.direction * _aimMaxDistance;

            RaycastHit hit;
            if (TryHit(ray.origin, target, 0f, out hit))
            {
                target = hit.point;
            }

            Vector3 toTarget = target - origin;
            float distance = toTarget.magnitude;
            if (distance < _minAimDistance)
            {
                aimPoint = origin + ray.direction * _aimMaxDistance;
                return ray.direction;
            }

            Vector3 direction = toTarget / distance;

            // A target behind the shot point would send the shot backwards.
            if (Vector3.Dot(direction, ray.direction) < 0.2f)
            {
                aimPoint = origin + ray.direction * _aimMaxDistance;
                return ray.direction;
            }

            aimPoint = target;
            return direction;
        }

        // Counts down so a swap-remove never makes the loop visit a shot twice.
        private void StepShots(float dt)
        {
            float gravityY = Physics.gravity.y;

            for (int i = _flight.Count - 1; i >= 0; i--)
            {
                SteerShot(i, dt);

                Vector3 start;
                Vector3 end;
                _flight.Plan(i, dt, gravityY, out start, out end);

                ManagedSpellDefinition spell = ManagedSpellDefinition.At(_flight.ProfileAt(i));
                int payload = _flight.PayloadAt(i);

                RaycastHit hit;
                if (TryHit(start, end, spell.SweepRadius, out hit))
                {
                    float travelledAtHit = _flight.TravelledAt(i) + hit.distance;
                    Retire(i);
                    Detonate(
                        spell, payload, hit.point, hit.normal,
                        "hit '" + hit.collider.name + "'", travelledAtHit);
                    continue;
                }

                Vector3 releasedAt;
                ManagedFlightState state = _flight.Commit(i, dt, end, out releasedAt);
                if (state == ManagedFlightState.Flying)
                {
                    continue;
                }

                // The bubble collapses where the SP ran out and the payload releases there.
                float travelled = _flight.TravelledAt(i);
                Vector3 normal = -_flight.VelocityAt(i).normalized;
                Retire(i);
                Detonate(
                    spell, payload, releasedAt, normal,
                    state == ManagedFlightState.Collapsed
                        ? "ran out of SP and the bubble collapsed"
                        : "reached its flight cap",
                    travelled);
            }
        }

        // A steered shot turns toward its lock for this step. The lock is the target, or where
        // the target was, or the aim point, until the shot has flown past it.
        private void SteerShot(int index, float dt)
        {
            int lockId = _flight.TargetAt(index);
            if (lockId == 0)
            {
                return;
            }

            Vector3 point;
            if (!_seeker.TryGetPoint(lockId, _flight.PositionAt(index), _flight.VelocityAt(index), out point))
            {
                return;
            }

            Vector3 toPoint = point - _flight.PositionAt(index);
            if (toPoint.sqrMagnitude < MinSteerDistanceSquared)
            {
                return;
            }

            _flight.Steer(index, toPoint, dt);
        }

        // Every way a shot ends goes through here, so its lock is always released.
        private void Retire(int index)
        {
            int lockId = _flight.TargetAt(index);
            if (lockId != 0)
            {
                _seeker.Release(lockId);
            }

            _flight.RemoveAt(index);
        }

        // Closest hit that does not belong to the caster. A hit on the caster restarts the
        // sweep just past it, so the result is always the closest foreign collider.
        private bool TryHit(Vector3 start, Vector3 end, float radius, out RaycastHit closest)
        {
            closest = default(RaycastHit);
            Vector3 delta = end - start;
            float remaining = delta.magnitude;
            if (remaining < 1e-5f)
            {
                return false;
            }

            Vector3 direction = delta / remaining;
            Vector3 origin = start;

            for (int attempt = 0; attempt < MaxOwnHitSkips && remaining > 1e-4f; attempt++)
            {
                RaycastHit hit;
                bool found = radius > 0f
                    ? Physics.SphereCast(origin, radius, direction, out hit, remaining, _hitMask, QueryTriggerInteraction.Ignore)
                    : Physics.Raycast(origin, direction, out hit, remaining, _hitMask, QueryTriggerInteraction.Ignore);

                if (!found)
                {
                    return false;
                }

                if (!hit.collider.transform.IsChildOf(transform))
                {
                    closest = hit;
                    return true;
                }

                float advance = hit.distance + RestartPadding;
                origin += direction * advance;
                remaining -= advance;
            }

            return false;
        }

        private bool IsBlockedAt(Vector3 point, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radius, _overlaps, _hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!_overlaps[i].transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        private void Detonate(ManagedSpellDefinition spell, int payload, Vector3 position, Vector3 normal, string what, float travelled)
        {
            if (_logImpacts)
            {
                Debug.Log(
                    "[GTG Magic] " + spell.DisplayName + " " + what +
                    " at " + position.ToString("F1") + " after " + travelled.ToString("F1") + " m");
            }

            Action<ManagedSpellImpact> handler = Impact;
            if (handler != null)
            {
                handler(new ManagedSpellImpact
                {
                    Position = position,
                    Normal = normal,
                    Kind = spell.Kind,
                    PayloadId = payload,
                    Source = gameObject,
                });
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 point = _shotPoint != null
                ? _shotPoint.position
                : transform.TransformPoint(_shotPointOffset);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(point, 0.1f);
        }
    }
}
