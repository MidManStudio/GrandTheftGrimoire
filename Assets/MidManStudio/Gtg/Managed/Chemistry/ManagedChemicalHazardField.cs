// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedChemicalHazardField.cs"
// ============================================================================

using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Magic;
using MidManStudio.Gtg.Managed.Rendering;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Chemistry
{
    /// <summary>
    /// Turns each spell impact into a chemical hazard. Runs the Alembic reaction for the
    /// recipe, grows the hazard to its full radius, removes it when its duration ends and
    /// draws it as two translucent spheres through one <see cref="ManagedSphereBatch"/>,
    /// an outer shell and a brighter core. Put one in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedChemicalHazardField : MonoBehaviour
    {
        [Header("Rendering")]
        [Tooltip("Skips hardware instancing and uses the combined mesh path. For testing the fallback.")]
        [SerializeField] private bool _forceCombinedMesh;
        [Tooltip("Two spheres are drawn per hazard.")]
        [SerializeField] private int _maxHazards = 32;

        [Header("Debug")]
        [SerializeField] private bool _logReactions = true;

        private readonly List<ManagedChemicalHazard> _hazards = new List<ManagedChemicalHazard>(8);
        private ManagedChemistryReactor _reactor;
        private ManagedSphereBatch _batch;

        /// <summary>Number of live hazards.</summary>
        public int Count { get { return _hazards.Count; } }

        public ManagedChemicalHazard Get(int index)
        {
            return _hazards[index];
        }

        private void Awake()
        {
            _batch = new ManagedSphereBatch(_maxHazards * 2, gameObject.layer, _forceCombinedMesh, "hazards");
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
            _hazards.Clear();

            if (_reactor != null)
            {
                _reactor.Dispose();
                _reactor = null;
            }

            if (_batch != null)
            {
                _batch.Dispose();
                _batch = null;
            }
        }

        private void Update()
        {
            double now = Time.timeAsDouble;
            for (int i = _hazards.Count - 1; i >= 0; i--)
            {
                ManagedChemicalHazard hazard = _hazards[i];
                float age = (float)(now - hazard.SpawnTime);

                if (age >= hazard.Duration)
                {
                    _hazards.RemoveAt(i);
                    continue;
                }

                float grown = hazard.GrowDuration <= 0f ? 1f : Mathf.Clamp01(age / hazard.GrowDuration);
                hazard.CurrentRadius = hazard.MaxRadius * grown;
            }
        }

        private void LateUpdate()
        {
            double now = Time.timeAsDouble;
            _batch.Clear();
            for (int i = 0; i < _hazards.Count; i++)
            {
                ManagedChemicalHazard hazard = _hazards[i];
                AddViews(hazard, (float)(now - hazard.SpawnTime));
            }

            _batch.Draw();
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

            if (_hazards.Count >= _maxHazards)
            {
                _hazards.RemoveAt(0);
            }

            float radius = recipe.RadiusFor(1f);
            _hazards.Add(new ManagedChemicalHazard
            {
                Origin = impact.Position,
                CurrentRadius = recipe.GrowSeconds <= 0f ? radius : 0f,
                MaxRadius = radius,
                Type = recipe.Hazard,
                SpawnTime = Time.timeAsDouble,
                Duration = recipe.HazardDurationSeconds,
                GrowDuration = recipe.GrowSeconds,
                AlembicConfirmed = result.Confirmed,
            });

            if (_logReactions)
            {
                string alembic = !result.NativeUsed ? "unavailable" : (result.Confirmed ? "confirmed" : "not confirmed");
                Debug.Log(
                    "[GTG Chemistry] " + recipe.Id + " impact at " + impact.Position.ToString("F1") +
                    ": alembic " + alembic +
                    ", bonds formed " + result.FormedBonds +
                    ", broken " + result.BrokenBonds +
                    ", steps " + result.Steps +
                    ", temperature " + result.TemperatureK.ToString("F0") + " K" +
                    ", radius " + radius.ToString("F2") + " m");
            }
        }

        // Outer shell at the authoritative radius, brighter core inside it. The explosion
        // core fades faster than the shell, which reads as a flash.
        private void AddViews(ManagedChemicalHazard hazard, float age)
        {
            float life = 1f - Mathf.Clamp01(age / Mathf.Max(0.0001f, hazard.Duration));
            float diameter = Mathf.Max(0.001f, hazard.CurrentRadius * 2f);

            Color shell;
            Color core;
            float coreScale;
            if (hazard.Type == ManagedHazardType.Freeze)
            {
                shell = hazard.AlembicConfirmed ? new Color(0.55f, 0.85f, 1f) : new Color(0.75f, 0.82f, 0.88f);
                core = new Color(0.85f, 0.97f, 1f);
                shell.a = 0.30f * life;
                core.a = 0.45f * life;
                coreScale = 0.55f;
            }
            else
            {
                shell = hazard.AlembicConfirmed ? new Color(1f, 0.5f, 0.1f) : new Color(1f, 0.85f, 0.2f);
                core = new Color(1f, 0.9f, 0.5f);
                shell.a = 0.40f * life;
                core.a = 0.60f * life * life;
                coreScale = 0.45f;
            }

            _batch.Add(hazard.Origin, Vector3.one * diameter, shell);
            _batch.Add(hazard.Origin, Vector3.one * (diameter * coreScale), core);
        }
    }
}
