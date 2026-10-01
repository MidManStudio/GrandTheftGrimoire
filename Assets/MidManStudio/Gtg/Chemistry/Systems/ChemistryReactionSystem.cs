// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/chemistry.md, section "ChemistryReactionSystem.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MidManStudio.Alembic.Core;
using MidManStudio.Gtg.Magic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MidManStudio.Gtg.Chemistry
{
    /// <summary>
    /// Turns each spell impact into a chemical hazard. Runs a short Alembic
    /// simulation for the recipe and counts formed bonds as the confirmation.
    /// If the native library is missing or reports nothing, the hazard still
    /// spawns and is marked unconfirmed, so gameplay tests keep working.
    /// Managed on purpose: the native calls cannot be Burst compiled.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ChemistryReactionSystem : SystemBase
    {
        private const byte BondEventFormed = 0;

        private readonly List<AtomHandle> _handles = new List<AtomHandle>();
        private readonly List<SpellImpact> _impacts = new List<SpellImpact>();
        private readonly List<Entity> _impactEntities = new List<Entity>();
        private IntPtr _context;
        private bool _nativeChecked;
        private bool _nativeReady;

        private readonly struct ReactionResult
        {
            public readonly bool NativeUsed;
            public readonly bool Confirmed;
            public readonly int FormedBonds;
            public readonly int Steps;
            public readonly float TemperatureK;

            public ReactionResult(bool nativeUsed, bool confirmed, int formedBonds, int steps, float temperatureK)
            {
                NativeUsed = nativeUsed;
                Confirmed = confirmed;
                FormedBonds = formedBonds;
                Steps = steps;
                TemperatureK = temperatureK;
            }
        }

        protected override void OnCreate()
        {
            RequireForUpdate<SpellImpact>();
        }

        protected override void OnDestroy()
        {
            if (_context != IntPtr.Zero)
            {
                ChemistryLib.chem_context_destroy(_context);
                _context = IntPtr.Zero;
            }
        }

        protected override void OnUpdate()
        {
            // Copy first: creating hazard entities is a structural change and is not
            // allowed while a query is being iterated.
            _impacts.Clear();
            _impactEntities.Clear();
            foreach (var (impact, entity) in SystemAPI.Query<RefRO<SpellImpact>>().WithEntityAccess())
            {
                _impacts.Add(impact.ValueRO);
                _impactEntities.Add(entity);
            }

            double now = SystemAPI.Time.ElapsedTime;
            for (int i = 0; i < _impacts.Count; i++)
            {
                HandleImpact(_impacts[i], now);
                EntityManager.DestroyEntity(_impactEntities[i]);
            }
        }

        private void HandleImpact(SpellImpact impact, double now)
        {
            ChemistryRecipe recipe = ChemistryRecipe.ForSpell(impact.Kind);
            ReactionResult result = RunReaction(recipe);

            if (recipe.RequireConfirmation && !result.Confirmed)
            {
                Debug.LogWarning("[GTG Chemistry] " + recipe.Id + " was not confirmed by Alembic, no hazard spawned.");
                return;
            }

            float radius = recipe.RadiusFor(1f);

            Entity hazard = EntityManager.CreateEntity(typeof(ChemicalHazard));
            EntityManager.SetComponentData(hazard, new ChemicalHazard
            {
                Origin = impact.Position,
                CurrentRadius = 0f,
                MaxRadius = radius,
                Type = recipe.Hazard,
                SpawnTime = now,
                Duration = recipe.HazardDurationSeconds,
                GrowDuration = recipe.GrowSeconds,
                AlembicConfirmed = result.Confirmed,
            });

            string alembic = !result.NativeUsed ? "unavailable" : (result.Confirmed ? "confirmed" : "no bond events");
            Debug.Log(
                "[GTG Chemistry] " + recipe.Id + " impact at " + impact.Position +
                ": alembic " + alembic +
                ", bonds formed " + result.FormedBonds +
                ", steps " + result.Steps +
                ", temperature " + result.TemperatureK.ToString("F0") + " K" +
                ", radius " + radius.ToString("F2") + " m");
        }

        private ReactionResult RunReaction(ChemistryRecipe recipe)
        {
            if (!EnsureNative())
            {
                return new ReactionResult(false, false, 0, 0, 0f);
            }

            _handles.Clear();
            try
            {
                // Drain events left over from an earlier reaction.
                CountFormedBonds();

                float rOh = ChemistryLib.chem_bond_r_min(8, 1) * recipe.SpawnSpacingFactor;
                float angle = 104.5f * Mathf.Deg2Rad;
                for (int m = 0; m < recipe.Molecules; m++)
                {
                    float z = m * recipe.MoleculeSeparationAngstrom;
                    SpawnAtom(8, 0f, 0f, z);
                    SpawnAtom(1, rOh, 0f, z);
                    SpawnAtom(1, rOh * Mathf.Cos(angle), rOh * Mathf.Sin(angle), z);
                }

                ChemistryLib.chem_init(_context, recipe.TemperatureK, recipe.Seed);

                int formed = 0;
                int steps = 0;
                while (steps < recipe.MaxSteps && formed < recipe.MinFormedBonds)
                {
                    ChemistryLib.chem_step(_context, recipe.DtFemtoseconds, recipe.CutoffAngstrom);
                    steps++;
                    formed += CountFormedBonds();
                }

                float temperature = ChemistryLib.chem_temperature(_context);
                return new ReactionResult(true, formed >= recipe.MinFormedBonds, formed, steps, temperature);
            }
            catch (Exception exception)
            {
                Debug.LogError("[GTG Chemistry] Alembic reaction failed: " + exception.Message);
                return new ReactionResult(true, false, 0, 0, 0f);
            }
            finally
            {
                for (int i = 0; i < _handles.Count; i++)
                {
                    ChemistryLib.DespawnAtom(_context, _handles[i]);
                }

                _handles.Clear();
            }
        }

        private void SpawnAtom(int atomicNumber, float x, float y, float z)
        {
            _handles.Add(ChemistryLib.chem_spawn_atom(_context, atomicNumber, x, y, z));
        }

        private int CountFormedBonds()
        {
            IntPtr events = ChemistryLib.chem_take_bond_events(_context, out int count);
            if (events == IntPtr.Zero || count <= 0)
            {
                return 0;
            }

            int size = Marshal.SizeOf<BondEvent>();
            int formed = 0;
            for (int i = 0; i < count; i++)
            {
                BondEvent bondEvent = Marshal.PtrToStructure<BondEvent>(IntPtr.Add(events, i * size));
                if (bondEvent.Kind == BondEventFormed)
                {
                    formed++;
                }
            }

            return formed;
        }

        private bool EnsureNative()
        {
            if (_nativeChecked)
            {
                return _nativeReady;
            }

            _nativeChecked = true;
            try
            {
                if (!ChemistryLib.IsAvailable)
                {
                    Debug.LogWarning("[GTG Chemistry] Alembic native library is unavailable. Hazards spawn without confirmation.");
                    return false;
                }

                ChemistryLib.ValidateStructSizes();
                _context = ChemistryLib.chem_context_create(0f);
                _nativeReady = _context != IntPtr.Zero;
            }
            catch (Exception exception)
            {
                Debug.LogError("[GTG Chemistry] Alembic setup failed: " + exception.Message);
                _nativeReady = false;
            }

            return _nativeReady;
        }
    }
}
