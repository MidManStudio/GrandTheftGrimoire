// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedWallet.cs"
// ============================================================================

using System;
using System.Text;

namespace MidManStudio.Gtg.Managed.Economy
{
    /// <summary>
    /// What the player carries as money: gold, silver and SP crystals. Gold and silver are one
    /// amount counted in silver, 100 silver to the gold, so there is never a change problem.
    /// Crystals are items of fixed sizes, each worth a set amount of SP. They are kept as
    /// counts per size, are used whole, and are used up. The wallet holds plain values and
    /// knows nothing about vessels, shops or Unity.
    /// </summary>
    public sealed class ManagedWallet : IManagedCoinPurse
    {
        public const int SilverPerGold = 100;

        private readonly int[] _crystals = new int[ManagedCrystals.SizeCount];
        private long _silver;

        /// <summary>All the coin the wallet holds, counted in silver.</summary>
        public long TotalSilver { get { return _silver; } }

        /// <summary>Whole gold pieces in the total.</summary>
        public long Gold { get { return _silver / SilverPerGold; } }

        /// <summary>The silver left over after the gold, from 0 to 99.</summary>
        public int Silver { get { return (int)(_silver % SilverPerGold); } }

        /// <summary>Adds gold. A zero or negative amount does nothing.</summary>
        public void AddGold(long gold)
        {
            if (gold <= 0 || gold > (long.MaxValue - _silver) / SilverPerGold)
            {
                return;
            }

            _silver += gold * SilverPerGold;
        }

        /// <summary>Adds silver. A zero or negative amount does nothing.</summary>
        public void AddSilver(long silver)
        {
            if (silver <= 0 || silver > long.MaxValue - _silver)
            {
                return;
            }

            _silver += silver;
        }

        public bool CanAfford(long silver)
        {
            return silver >= 0 && _silver >= silver;
        }

        /// <summary>
        /// Pays a price written as gold and silver. Fails and changes nothing when either part is
        /// negative, the price overflows, or the wallet holds less than the price.
        /// </summary>
        public bool TrySpend(long gold, long silver)
        {
            if (gold < 0 || silver < 0 || gold > (long.MaxValue - silver) / SilverPerGold)
            {
                return false;
            }

            long cost = gold * SilverPerGold + silver;
            if (_silver < cost)
            {
                return false;
            }

            _silver -= cost;
            return true;
        }

        public int CrystalCount(ManagedCrystalSize size)
        {
            int index = (int)size;
            return index >= 0 && index < _crystals.Length ? _crystals[index] : 0;
        }

        /// <summary>Total SP held in crystals, counting each crystal at its full value.</summary>
        public float CrystalSp
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _crystals.Length; i++)
                {
                    total += _crystals[i] * ManagedCrystals.SpOf((ManagedCrystalSize)i);
                }

                return total;
            }
        }

        /// <summary>Adds crystals. A zero or negative count does nothing.</summary>
        public void AddCrystals(ManagedCrystalSize size, int count)
        {
            int index = (int)size;
            if (count <= 0 || index < 0 || index >= _crystals.Length || count > int.MaxValue - _crystals[index])
            {
                return;
            }

            _crystals[index] += count;
        }

        /// <summary>Uses one crystal of a size. Returns false when there is none.</summary>
        public bool TryTakeCrystal(ManagedCrystalSize size)
        {
            int index = (int)size;
            if (index < 0 || index >= _crystals.Length || _crystals[index] <= 0)
            {
                return false;
            }

            _crystals[index]--;
            return true;
        }

        /// <summary>
        /// Uses the crystal that suits filling <paramref name="missingSp"/>: the smallest one that
        /// holds at least that much, so little is wasted, or the largest one held when none is big
        /// enough. <paramref name="sp"/> is the SP the used crystal was worth. Returns false and
        /// changes nothing when there is no crystal or nothing is missing.
        /// </summary>
        public bool TryTakeCrystalFor(float missingSp, out float sp)
        {
            sp = 0f;
            if (!(missingSp > 0f))
            {
                return false;
            }

            int pick = -1;
            for (int i = 0; i < _crystals.Length; i++)
            {
                if (_crystals[i] > 0 && ManagedCrystals.SpOf((ManagedCrystalSize)i) >= missingSp)
                {
                    pick = i;
                    break;
                }
            }

            if (pick < 0)
            {
                for (int i = _crystals.Length - 1; i >= 0; i--)
                {
                    if (_crystals[i] > 0)
                    {
                        pick = i;
                        break;
                    }
                }
            }

            if (pick < 0)
            {
                return false;
            }

            _crystals[pick]--;
            sp = ManagedCrystals.SpOf((ManagedCrystalSize)pick);
            return true;
        }

        /// <summary>A short line for the overlay, such as "3 gold 50 silver, crystals S2 M1 L0".</summary>
        public string Describe()
        {
            var text = new StringBuilder(64);
            text.Append(Gold).Append(" gold ").Append(Silver).Append(" silver, crystals S")
                .Append(_crystals[(int)ManagedCrystalSize.Small]).Append(" M")
                .Append(_crystals[(int)ManagedCrystalSize.Medium]).Append(" L")
                .Append(_crystals[(int)ManagedCrystalSize.Large]);
            return text.ToString();
        }
    }
}
