// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/chemistry.md, section "ChemicalHazardPresentationSystem.cs"
// ============================================================================

using System.Collections.Generic;
using MidManStudio.Gtg.Magic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MidManStudio.Gtg.Chemistry
{
    /// <summary>
    /// Draws each hazard as a translucent sphere sized to its current radius.
    /// Orange means Alembic confirmed the reaction, yellow means it did not.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ChemicalHazardPresentationSystem : SystemBase
    {
        private sealed class HazardView
        {
            public GameObject Go;
            public Material Material;
        }

        private readonly Dictionary<Entity, HazardView> _views = new Dictionary<Entity, HazardView>();
        private readonly HashSet<Entity> _seen = new HashSet<Entity>();
        private readonly List<Entity> _stale = new List<Entity>();
        private Transform _root;

        protected override void OnCreate()
        {
            _root = new GameObject("GTG Hazard Views").transform;
        }

        protected override void OnDestroy()
        {
            foreach (KeyValuePair<Entity, HazardView> pair in _views)
            {
                Release(pair.Value);
            }

            _views.Clear();
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }
        }

        protected override void OnUpdate()
        {
            double now = SystemAPI.Time.ElapsedTime;
            _seen.Clear();

            foreach (var (hazard, entity) in
                     SystemAPI.Query<RefRO<ChemicalHazard>>().WithEntityAccess())
            {
                _seen.Add(entity);
                if (!_views.TryGetValue(entity, out HazardView view))
                {
                    view = Create();
                    _views.Add(entity, view);
                }

                ChemicalHazard value = hazard.ValueRO;
                float age = (float)(now - value.SpawnTime);
                float fade = 1f - math.saturate(age / math.max(0.0001f, value.Duration));
                float diameter = math.max(0.001f, value.CurrentRadius * 2f);

                view.Go.transform.position = new Vector3(value.Origin.x, value.Origin.y, value.Origin.z);
                view.Go.transform.localScale = new Vector3(diameter, diameter, diameter);

                if (view.Material != null)
                {
                    Color color = value.AlembicConfirmed
                        ? new Color(1f, 0.5f, 0.1f)
                        : new Color(1f, 0.85f, 0.2f);
                    color.a = 0.4f * fade;
                    view.Material.color = color;
                }
            }

            if (_views.Count == _seen.Count)
            {
                return;
            }

            _stale.Clear();
            foreach (KeyValuePair<Entity, HazardView> pair in _views)
            {
                if (!_seen.Contains(pair.Key))
                {
                    _stale.Add(pair.Key);
                }
            }

            for (int i = 0; i < _stale.Count; i++)
            {
                Release(_views[_stale[i]]);
                _views.Remove(_stale[i]);
            }
        }

        private HazardView Create()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "HazardView";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root, false);

            Material material = SpellVfxMaterials.CreateUnlit(new Color(1f, 0.5f, 0.1f, 0.4f), true);
            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            return new HazardView { Go = go, Material = material };
        }

        private static void Release(HazardView view)
        {
            if (view.Go != null)
            {
                Object.Destroy(view.Go);
            }

            if (view.Material != null)
            {
                Object.Destroy(view.Material);
            }
        }
    }
}
