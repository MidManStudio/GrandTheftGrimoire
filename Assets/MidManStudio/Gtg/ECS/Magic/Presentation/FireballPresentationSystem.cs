#if GTG_ECS
// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/magic.md, section "FireballPresentationSystem.cs"
// ============================================================================

using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace MidManStudio.Gtg.Magic
{
    /// <summary>
    /// Draws one glowing sphere per fireball entity. Entities are not rendered
    /// directly because this project has no Entities Graphics package.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class FireballPresentationSystem : SystemBase
    {
        private readonly Dictionary<Entity, GameObject> _views = new Dictionary<Entity, GameObject>();
        private readonly HashSet<Entity> _seen = new HashSet<Entity>();
        private readonly List<Entity> _stale = new List<Entity>();
        private readonly Stack<GameObject> _pool = new Stack<GameObject>();
        private Transform _root;
        private Material _material;

        protected override void OnCreate()
        {
            _root = new GameObject("GTG Fireball Views").transform;
            _material = SpellVfxMaterials.CreateUnlit(new Color(1f, 0.45f, 0.1f, 1f), false);
        }

        protected override void OnDestroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }

            if (_material != null)
            {
                Object.Destroy(_material);
            }
        }

        protected override void OnUpdate()
        {
            _seen.Clear();

            foreach (var (transform, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<FireballProjectile>()
                         .WithEntityAccess())
            {
                _seen.Add(entity);
                if (!_views.TryGetValue(entity, out GameObject view))
                {
                    view = Acquire();
                    _views.Add(entity, view);
                }

                Unity.Mathematics.float3 p = transform.ValueRO.Position;
                view.transform.position = new Vector3(p.x, p.y, p.z);
            }

            if (_views.Count == _seen.Count)
            {
                return;
            }

            _stale.Clear();
            foreach (KeyValuePair<Entity, GameObject> pair in _views)
            {
                if (!_seen.Contains(pair.Key))
                {
                    _stale.Add(pair.Key);
                }
            }

            for (int i = 0; i < _stale.Count; i++)
            {
                GameObject view = _views[_stale[i]];
                view.SetActive(false);
                _pool.Push(view);
                _views.Remove(_stale[i]);
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
            Object.Destroy(view.GetComponent<Collider>());
            view.transform.SetParent(_root, false);
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
    }
}
#endif
