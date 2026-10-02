// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedChemistryReactor.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MidManStudio.Alembic.Core;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Chemistry
{
    /// <summary>
    /// Runs the short Alembic simulation for a recipe and counts formed bonds as the
    /// confirmation. If the native library is missing or reports nothing, the result is
    /// unconfirmed and the caller decides what to do with it.
    /// </summary>
    public sealed class ManagedChemistryReactor : IDisposable
    {
        private const byte BondEventFormed = 0;

        private readonly List<AtomHandle> _handles = new List<AtomHandle>();
        private IntPtr _context;
        private bool _nativeChecked;
        private bool _nativeReady;

        public ManagedReactionResult Run(ManagedChemistryRecipe recipe)
        {
            if (!EnsureNative())
            {
                return new ManagedReactionResult(false, false, 0, 0, 0f);
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
                return new ManagedReactionResult(true, formed >= recipe.MinFormedBonds, formed, steps, temperature);
            }
            catch (Exception exception)
            {
                Debug.LogError("[GTG Chemistry] Alembic reaction failed: " + exception.Message);
                return new ManagedReactionResult(true, false, 0, 0, 0f);
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

        public void Dispose()
        {
            if (_context != IntPtr.Zero)
            {
                ChemistryLib.chem_context_destroy(_context);
                _context = IntPtr.Zero;
            }

            _nativeReady = false;
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

        // The native library is probed once. A failed probe is not retried, so a missing
        // library does not log on every impact.
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
