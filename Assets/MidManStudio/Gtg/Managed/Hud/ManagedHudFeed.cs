// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudFeed.cs"
// ============================================================================

using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Building;
using MidManStudio.Gtg.Managed.CharacterController;
using MidManStudio.Gtg.Managed.Economy;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.Items;
using MidManStudio.Gtg.Managed.Magic;
using MidManStudio.Gtg.Managed.Missions;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// The one place the HUD reads the game. It finds the player and copies health, vessel charge,
    /// coins and heading into a <see cref="ManagedHudState"/>, finds the player's item bag, and
    /// copies whether build mode is open, which hides the bottom group. It also copies the heat of
    /// the registered <see cref="IManagedHudHeatSource"/> and the lines of the registered
    /// <see cref="IManagedMissionSource"/>, when there are any. Everything else in the HUD reads the
    /// state and knows none of these systems. Nothing here writes to the game except adding a bag to
    /// the player when the player has none.
    /// </summary>
    public sealed class ManagedHudFeed
    {
        /// <summary>Seconds between searches for the parts of the player that are missing.</summary>
        public const float SearchSeconds = 0.5f;

        /// <summary>
        /// Seconds between searches for the build mode component while there is none. It is
        /// optional and the search walks the scene, so it runs less often than the player search.
        /// </summary>
        public const float BuildSearchSeconds = 2f;

        private readonly bool _createBag;
        private ManagedCharacter _character;
        private ManagedHealth _health;
        private ManagedSpellCaster _caster;
        private ManagedPlayerItemBag _bag;
        private ManagedBuildMode _buildMode;
        private float _nextSearch;
        private float _nextBuildSearch;
        private bool _hadHeatSource;
        private bool _hadMissionSource;
        private readonly List<string> _missionLines = new List<string>(ManagedHudState.MaxMissionLines);

        /// <summary>
        /// With <paramref name="createBag"/> the feed adds a <see cref="ManagedPlayerItemBag"/> to
        /// the player when it has none, so the quick slots have something to show.
        /// </summary>
        public ManagedHudFeed(bool createBag)
        {
            _createBag = createBag;
        }

        /// <summary>True while a player is known.</summary>
        public bool HasPlayer { get { return _character != null; } }

        /// <summary>The player's bag component, or null while there is none.</summary>
        public ManagedPlayerItemBag PlayerBag { get { return _bag; } }

        /// <summary>
        /// Copies what the HUD shows from the game. <paramref name="now"/> is unscaled time, which
        /// paces the search for a player that has not appeared yet. A part that is missing shows
        /// as empty.
        /// </summary>
        public void Refresh(ManagedHudState state, float now)
        {
            if (_character == null)
            {
                _health = null;
                _caster = null;
                _bag = null;
            }

            if (now >= _nextSearch && (_character == null || _health == null || _caster == null || _bag == null))
            {
                _nextSearch = now + SearchSeconds;
                Find();
            }

            if (_character != null)
            {
                state.SetYaw(_character.Yaw);
            }

            if (_health != null)
            {
                state.SetHealth(_health.Current, _health.Max);
            }
            else
            {
                state.SetHealth(0f, 0f);
            }

            if (_caster != null)
            {
                state.SetVessel(_caster.HasSelectedVessel, _caster.SelectedVesselFraction);
                ManagedWallet wallet = _caster.Wallet;
                state.SetCoins(wallet != null ? wallet.Gold : 0L, wallet != null ? wallet.Silver : 0);
            }
            else
            {
                state.SetVessel(false, 0f);
                state.SetCoins(0L, 0);
            }

            if (_buildMode == null && now >= _nextBuildSearch)
            {
                _nextBuildSearch = now + BuildSearchSeconds;
                _buildMode = Object.FindFirstObjectByType<ManagedBuildMode>();
            }

            state.SetBottomHidden(_buildMode != null && _buildMode.BuildModeActive);

            CopyHeat(state);
            CopyMissions(state);
        }

        // With a heat source the heat follows it every frame, and is 0 while there is no player to place.
        // When the source goes away the crowns clear, so a heat that nothing owns does not stay lit.
        private void CopyHeat(ManagedHudState state)
        {
            IManagedHudHeatSource source = ManagedHudHeatSources.Current;
            if (source != null)
            {
                _hadHeatSource = true;
                state.SetHeat(_character != null ? source.SectionsAt(_character.transform.position) : 0);
            }
            else if (_hadHeatSource)
            {
                _hadHeatSource = false;
                state.SetHeat(0);
            }
        }

        // With a mission source the panel follows it every frame. When the source goes away the panel
        // shows the empty state again, so the last mission does not stay up.
        private void CopyMissions(ManagedHudState state)
        {
            IManagedMissionSource source = ManagedMissionSources.Current;
            if (source != null)
            {
                _hadMissionSource = true;
                _missionLines.Clear();
                int count = source.LineCount;
                if (count > ManagedHudState.MaxMissionLines)
                {
                    count = ManagedHudState.MaxMissionLines;
                }

                for (int i = 0; i < count; i++)
                {
                    _missionLines.Add(source.LineAt(i));
                }

                state.SetMissions(source.Title, _missionLines);
            }
            else if (_hadMissionSource)
            {
                _hadMissionSource = false;
                _missionLines.Clear();
                _missionLines.Add(ManagedMissionTracker.NoMissionLine);
                state.SetMissions(ManagedMissionTracker.DefaultTitle, _missionLines);
            }
        }

        private void Find()
        {
            if (_character == null)
            {
                _character = Object.FindFirstObjectByType<ManagedCharacter>();
            }

            if (_character == null)
            {
                return;
            }

            if (_health == null)
            {
                _health = _character.GetComponent<ManagedHealth>();
            }

            if (_caster == null)
            {
                _caster = _character.GetComponent<ManagedSpellCaster>();
            }

            if (_bag == null)
            {
                _bag = _character.GetComponent<ManagedPlayerItemBag>();
                if (_bag == null && _createBag)
                {
                    _bag = _character.gameObject.AddComponent<ManagedPlayerItemBag>();
                }
            }
        }
    }
}
