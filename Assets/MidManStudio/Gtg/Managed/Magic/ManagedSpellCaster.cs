// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSpellCaster.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.CharacterController;
using MidManStudio.Gtg.Managed.Rendering;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>
    /// Spell caster. A cast leaves the shot point and flies toward the crosshair. Each step
    /// sweeps a small sphere from the old position to the new one, so a fast shot cannot pass
    /// through a thin collider. A hit, or the end of the range, raises <see cref="Impact"/>.
    /// Shots are drawn through one <see cref="ManagedSphereBatch"/>, with no GameObject per
    /// shot. Put it on the same GameObject as ManagedCharacter.
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

        [Header("Collision")]
        [SerializeField] private LayerMask _hitMask = ~0;
        [Tooltip("A shot that reaches the end of its lifetime explodes where it is.")]
        [SerializeField] private bool _detonateAtRangeEnd = true;

        [Header("Rendering")]
        [Tooltip("Skips hardware instancing and uses the combined mesh path. For testing the fallback.")]
        [SerializeField] private bool _forceCombinedMesh;
        [SerializeField] private int _maxShots = 128;

        [Header("Debug")]
        [SerializeField] private bool _logImpacts = true;

        private struct Shot
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float RemainingLife;
            public float Travelled;
            public ManagedSpellDefinition Spell;
        }

        private const int OverlapBufferSize = 8;
        private const float RestartPadding = 0.02f;
        private const int MaxOwnHitSkips = 4;

        private readonly List<Shot> _shots = new List<Shot>(16);
        private readonly Collider[] _overlaps = new Collider[OverlapBufferSize];
        private ManagedCharacter _character;
        private ManagedSphereBatch _batch;
        private float _cooldownRemaining;
        private int _selected;

        public ManagedSpellDefinition SelectedSpell
        {
            get { return ManagedSpellDefinition.At(_selected); }
        }

        public int ShotCount { get { return _shots.Count; } }

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

            _batch = new ManagedSphereBatch(_maxShots, gameObject.layer, _forceCombinedMesh, "shots");
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
            for (int i = 0; i < _shots.Count; i++)
            {
                Shot shot = _shots[i];
                _batch.Add(shot.Position, Vector3.one * shot.Spell.VisualDiameter, shot.Spell.Color);
            }

            _batch.Draw();
        }

        private void SelectSpell()
        {
            ManagedCharacterInputFrame input = _character.Input;
            int count = ManagedSpellDefinition.Count;

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
            ManagedSpellDefinition spell = ManagedSpellDefinition.At(_selected);
            Vector3 origin = _shotPoint.position;
            Vector3 direction = ComputeAimDirection(origin);
            _cooldownRemaining = spell.CooldownSeconds;

            // A shot point inside a wall would sweep from the wrong side, so it explodes at once.
            if (IsBlockedAt(origin, spell.SweepRadius))
            {
                Detonate(spell, origin, -direction, "was cast inside a collider", 0f);
                return;
            }

            _shots.Add(new Shot
            {
                Position = origin,
                Velocity = direction * spell.Speed,
                RemainingLife = spell.LifetimeSeconds,
                Travelled = 0f,
                Spell = spell,
            });
        }

        // The ray through the screen center decides the target point. The shot then flies from
        // the shot point to that point, so it lands on the crosshair even though it leaves the hand.
        private Vector3 ComputeAimDirection(Vector3 origin)
        {
            Vector3 byAngles = Quaternion.Euler(_character.Pitch, _character.Yaw, 0f) * Vector3.forward;

            UnityEngine.Camera cam = _aimAtCrosshair ? UnityEngine.Camera.main : null;
            if (cam == null)
            {
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
                return ray.direction;
            }

            Vector3 direction = toTarget / distance;

            // A target behind the shot point would send the shot backwards.
            if (Vector3.Dot(direction, ray.direction) < 0.2f)
            {
                return ray.direction;
            }

            return direction;
        }

        private void StepShots(float dt)
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                Shot shot = _shots[i];
                Vector3 step = shot.Velocity * dt;
                Vector3 end = shot.Position + step;

                RaycastHit hit;
                if (TryHit(shot.Position, end, shot.Spell.SweepRadius, out hit))
                {
                    _shots.RemoveAt(i);
                    Detonate(
                        shot.Spell, hit.point, hit.normal,
                        "hit '" + hit.collider.name + "'", shot.Travelled + hit.distance);
                    continue;
                }

                shot.Position = end;
                shot.Travelled += step.magnitude;
                shot.RemainingLife -= dt;

                if (shot.RemainingLife <= 0f)
                {
                    _shots.RemoveAt(i);
                    if (_detonateAtRangeEnd)
                    {
                        Detonate(shot.Spell, end, -shot.Velocity.normalized, "reached the end of its range", shot.Travelled);
                    }

                    continue;
                }

                _shots[i] = shot;
            }
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

        private void Detonate(ManagedSpellDefinition spell, Vector3 position, Vector3 normal, string what, float travelled)
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
