// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedChemicalHazardField.cs"
// ============================================================================

using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Magic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Chemistry
{
    /// <summary>
    /// Turns each spell impact into a chemical hazard. Runs the Alembic reaction for the
    /// recipe, grows the hazard to its full radius, removes it when its duration ends and
    /// draws it as a translucent sphere. Orange means Alembic confirmed the reaction,
    /// yellow means it did not. Put one in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedChemicalHazardField : MonoBehaviour
    {
        private sealed class Entry
        {
            public ManagedChemicalHazard Hazard;
            public GameObject View;
            public Material Material;
        }

        private readonly List<Entry> _entries = new List<Entry>(8);
        private ManagedChemistryReactor _reactor;
        private Transform _viewRoot;

        /// <summary>Number of live hazards.</summary>
        public int Count { get { return _entries.Count; } }

        public ManagedChemicalHazard Get(int index)
        {
            return _entries[index].Hazard;
        }

        private void Awake()
        {
            _viewRoot = new GameObject("GTG Hazard Views").transform;
        }

        private void OnEnable()
        {
            ManagedSpellCaster.Impact += OnImpact;
        }

        private void OnDisable()
        {
            ManagedSpellCaster.Impact -= OnImpact;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Release(_entries[i]);
            }

            _entries.Clear();

            if (_reactor != null)
            {
                _reactor.Dispose();
                _reactor = null;
            }

            if (_viewRoot != null)
            {
                Destroy(_viewRoot.gameObject);
            }
        }

        private void Update()
        {
            double now = Time.timeAsDouble;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry entry = _entries[i];
                ManagedChemicalHazard hazard = entry.Hazard;
                float age = (float)(now - hazard.SpawnTime);

                if (age >= hazard.Duration)
                {
                    Release(entry);
                    _entries.RemoveAt(i);
                    continue;
                }

                float grown = hazard.GrowDuration <= 0f ? 1f : Mathf.Clamp01(age / hazard.GrowDuration);
                hazard.CurrentRadius = hazard.MaxRadius * grown;
                UpdateView(entry, age);
            }
        }

        private void OnImpact(ManagedSpellImpact impact)
        {
            ManagedChemistryRecipe recipe = ManagedChemistryRecipe.ForSpell(impact.Kind);
            if (_reactor == null)
            {
                _reactor = new ManagedChemistryReactor();
            }

            ManagedReactionResult result = _reactor.Run(recipe);
            if (recipe.RequireConfirmation && !result.Confirmed)
            {
                Debug.LogWarning("[GTG Chemistry] " + recipe.Id + " was not confirmed by Alembic, no hazard spawned.");
                return;
            }

            float radius = recipe.RadiusFor(1f);
            var hazard = new ManagedChemicalHazard
            {
                Origin = impact.Position,
                CurrentRadius = 0f,
                MaxRadius = radius,
                Type = recipe.Hazard,
                SpawnTime = Time.timeAsDouble,
                Duration = recipe.HazardDurationSeconds,
                GrowDuration = recipe.GrowSeconds,
                AlembicConfirmed = result.Confirmed,
            };

            Entry entry = CreateEntry(hazard);
            _entries.Add(entry);
            UpdateView(entry, 0f);

            string alembic = !result.NativeUsed ? "unavailable" : (result.Confirmed ? "confirmed" : "no bond events");
            Debug.Log(
                "[GTG Chemistry] " + recipe.Id + " impact at " + impact.Position +
                ": alembic " + alembic +
                ", bonds formed " + result.FormedBonds +
                ", steps " + result.Steps +
                ", temperature " + result.TemperatureK.ToString("F0") + " K" +
                ", radius " + radius.ToString("F2") + " m");
        }

        private Entry CreateEntry(ManagedChemicalHazard hazard)
        {
            GameObject view = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            view.name = "HazardView";

            Collider collider = view.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);

            view.transform.SetParent(_viewRoot, false);

            Material material = ManagedSpellVfxMaterials.CreateUnlit(new Color(1f, 0.5f, 0.1f, 0.4f), true);
            if (material != null)
            {
                view.GetComponent<Renderer>().sharedMaterial = material;
            }

            return new Entry { Hazard = hazard, View = view, Material = material };
        }

        private static void UpdateView(Entry entry, float age)
        {
            ManagedChemicalHazard hazard = entry.Hazard;
            float fade = 1f - Mathf.Clamp01(age / Mathf.Max(0.0001f, hazard.Duration));
            float diameter = Mathf.Max(0.001f, hazard.CurrentRadius * 2f);

            entry.View.transform.position = hazard.Origin;
            entry.View.transform.localScale = new Vector3(diameter, diameter, diameter);

            if (entry.Material != null)
            {
                Color color = hazard.AlembicConfirmed
                    ? new Color(1f, 0.5f, 0.1f)
                    : new Color(1f, 0.85f, 0.2f);
                color.a = 0.4f * fade;
                entry.Material.color = color;
            }
        }

        private static void Release(Entry entry)
        {
            if (entry.View != null)
            {
                Destroy(entry.View);
            }

            if (entry.Material != null)
            {
                Destroy(entry.Material);
            }
        }
    }
}
