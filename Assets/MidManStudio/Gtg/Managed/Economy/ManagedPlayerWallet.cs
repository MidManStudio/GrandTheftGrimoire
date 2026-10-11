// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedPlayerWallet.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Economy
{
    /// <summary>
    /// Holds the player's <see cref="ManagedWallet"/> as a component, so the overlay, the caster
    /// and the cheats can find it. The caster adds one to its own object when there is none, so
    /// a scene needs no wiring. Nothing is saved: every play starts from the values below.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedPlayerWallet : MonoBehaviour
    {
        [Header("Starting purse for testing")]
        [SerializeField] private int _startGold = 3;
        [SerializeField] private int _startSilver = 50;
        [SerializeField] private int _startSmallCrystals = 2;
        [SerializeField] private int _startMediumCrystals = 1;
        [SerializeField] private int _startLargeCrystals;

        private ManagedWallet _wallet;

        public ManagedWallet Wallet
        {
            get
            {
                if (_wallet == null)
                {
                    Build();
                }

                return _wallet;
            }
        }

        private void Awake()
        {
            if (_wallet == null)
            {
                Build();
            }
        }

        private void Build()
        {
            _wallet = new ManagedWallet();
            _wallet.AddGold(_startGold);
            _wallet.AddSilver(_startSilver);
            _wallet.AddCrystals(ManagedCrystalSize.Small, _startSmallCrystals);
            _wallet.AddCrystals(ManagedCrystalSize.Medium, _startMediumCrystals);
            _wallet.AddCrystals(ManagedCrystalSize.Large, _startLargeCrystals);
        }
    }
}
