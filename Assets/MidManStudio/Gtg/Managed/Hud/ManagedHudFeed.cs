// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudFeed.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.CharacterController;
using MidManStudio.Gtg.Managed.Economy;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.Items;
using MidManStudio.Gtg.Managed.Magic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// The one place the HUD reads the game. It finds the player and copies health, vessel charge,
    /// coins and heading into a <see cref="ManagedHudState"/>, and finds the player's item bag.
    /// Everything else in the HUD reads the state and knows none of these systems. Nothing here
    /// writes to the game except adding a bag to the player when the player has none.
    /// </summary>
    public sealed class ManagedHudFeed
    {
        /// <summary>Seconds between searches for the parts of the player that are missing.</summary>
        public const float SearchSeconds = 0.5f;

        private readonly bool _createBag;
        private ManagedCharacter _character;
        private ManagedHealth _health;
        private ManagedSpellCaster _caster;
        private ManagedPlayerItemBag _bag;
        private float _nextSearch;

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
