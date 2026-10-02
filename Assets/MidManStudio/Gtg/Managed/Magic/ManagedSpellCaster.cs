// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSpellCaster.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.CharacterController;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>
    /// Fireball caster. Spawns a shot when the fire button goes down, the cast module is on
    /// and the cooldown has run out. Each step raycasts from the old position to the new one,
    /// so a fast shot cannot pass through a thin collider. A hit raises <see cref="Impact"/>.
    /// Put it on the same GameObject as ManagedCharacter.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ManagedCharacter))]
    public sealed class ManagedSpellCaster : MonoBehaviour
    {
        /// <summary>Raised once per hit, from Update of the caster.</summary>
        public static event Action<ManagedSpellImpact> Impact;

        [SerializeField] private float _cooldownSeconds = 0.4f;
        [SerializeField] private float _projectileSpeed = 25f;
        [SerializeField] private float _projectileLifetimeSeconds = 3f;
        [Tooltip("Muzzle position in the character's yaw space, from the feet pivot.")]
        [SerializeField] private Vector3 _muzzleOffset = new Vector3(0f, 1.4f, 0.6f);
        [SerializeField] private LayerMask _hitMask = ~0;

        private struct Shot
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float RemainingLife;
            public GameObject View;
        }

        private const int HitBufferSize = 8;

        private readonly List<Shot> _shots = new List<Shot>(16);
        private readonly Stack<GameObject> _pool = new Stack<GameObject>();
        private readonly RaycastHit[] _hits = new RaycastHit[HitBufferSize];
        private ManagedCharacter _character;
        private Transform _viewRoot;
        private Material _material;
        private float _cooldownRemaining;

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Impact = null;
        }

        private void Awake()
        {
            _character = GetComponent<ManagedCharacter>();
            _viewRoot = new GameObject("GTG Fireball Views").transform;
            _material = ManagedSpellVfxMaterials.CreateUnlit(new Color(1f, 0.45f, 0.1f, 1f), false);
        }

        private void OnDestroy()
        {
            if (_viewRoot != null)
            {
                Destroy(_viewRoot.gameObject);
            }

            if (_material != null)
            {
                Destroy(_material);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - dt);

            if (_character.Has(ManagedCharacterFeature.Cast)
                && _character.Input.FirePressed
                && _cooldownRemaining <= 0f)
            {
                Cast();
            }

            StepShots(dt);
        }

        private void Cast()
        {
            Quaternion yaw = Quaternion.Euler(0f, _character.Yaw, 0f);
            Quaternion aim = Quaternion.Euler(_character.Pitch, _character.Yaw, 0f);

            var shot = new Shot
            {
                Position = transform.position + yaw * _muzzleOffset,
                Velocity = aim * Vector3.forward * _projectileSpeed,
                RemainingLife = _projectileLifetimeSeconds,
                View = Acquire(),
            };

            shot.View.transform.position = shot.Position;
            _shots.Add(shot);
            _cooldownRemaining = _cooldownSeconds;
        }

        private void StepShots(float dt)
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                Shot shot = _shots[i];
                Vector3 end = shot.Position + shot.Velocity * dt;

                RaycastHit hit;
                if (TryHit(shot.Position, end, out hit))
                {
                    Release(shot.View);
                    _shots.RemoveAt(i);
                    RaiseImpact(hit);
                    continue;
                }

                shot.Position = end;
                shot.RemainingLife -= dt;
                if (shot.RemainingLife <= 0f)
                {
                    Release(shot.View);
                    _shots.RemoveAt(i);
                    continue;
                }

                shot.View.transform.position = end;
                _shots[i] = shot;
            }
        }

        // Closest hit that does not belong to the caster.
        private bool TryHit(Vector3 start, Vector3 end, out RaycastHit closest)
        {
            closest = default(RaycastHit);
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance < 1e-5f)
            {
                return false;
            }

            int count = Physics.RaycastNonAlloc(
                start, delta / distance, _hits, distance, _hitMask, QueryTriggerInteraction.Ignore);

            bool found = false;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance < best)
                {
                    best = hit.distance;
                    closest = hit;
                    found = true;
                }
            }

            return found;
        }

        private static void RaiseImpact(RaycastHit hit)
        {
            Action<ManagedSpellImpact> handler = Impact;
            if (handler != null)
            {
                handler(new ManagedSpellImpact
                {
                    Position = hit.point,
                    Normal = hit.normal,
                    Kind = ManagedSpellKind.Fireball,
                });
            }
        }

        private GameObject Acquire()
        {
            if (_pool.Count > 0)
            {
                GameObject pooled = _pool.Pop();
                pooled.SetActive(true);
                return pooled;
            }

            GameObject view = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            view.name = "FireballView";

            Collider collider = view.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);

            view.transform.SetParent(_viewRoot, false);
            view.transform.localScale = Vector3.one * 0.4f;

            if (_material != null)
            {
                view.GetComponent<Renderer>().sharedMaterial = _material;
            }

            Light glow = view.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.55f, 0.15f);
            glow.range = 6f;
            glow.intensity = 3f;
            return view;
        }

        private void Release(GameObject view)
        {
            view.SetActive(false);
            _pool.Push(view);
        }
    }
}
