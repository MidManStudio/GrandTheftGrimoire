// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedBuiltInCheats.cs"
// ============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using MidManStudio.Gtg.Managed.Economy;
using MidManStudio.Gtg.Managed.Magic;

namespace MidManStudio.Gtg.Managed.Cheats
{
    /// <summary>
    /// The cheats that ship with the game. Every toggle changes one thing and its off step puts
    /// that thing back to normal, so the registry can undo all of them when play ends. The
    /// numbers are in the constants below and are placeholders to tune.
    /// </summary>
    public static class ManagedBuiltInCheats
    {
        public const string GroupSpells = "Vessels and spells";
        public const string GroupPlayer = "Player";
        public const string GroupTime = "Time";
        public const string GroupWorld = "World";
        public const string GroupMoney = "Money";

        public const float LongShotRangeScale = 2f;
        public const float FastFeetScale = 2f;
        public const float SlowMotionScale = 0.25f;
        public const float SkipShortSeconds = 15f;
        public const float SkipLongSeconds = 60f;
        public const long GiveGoldAmount = 10;
        public const long GiveSilverAmount = 50;
        public const int GiveCrystalsEach = 3;

        /// <summary>A registry holding every built-in cheat. Throws if a code breaks the registry rules, so a typo shows up in the first test run.</summary>
        public static ManagedCheatRegistry CreateRegistry()
        {
            var registry = new ManagedCheatRegistry();
            AddTo(registry);
            return registry;
        }

        public static void AddTo(ManagedCheatRegistry registry)
        {
            AddSpellCheats(registry);
            AddPlayerCheats(registry);
            AddTimeCheats(registry);
            AddWorldCheats(registry);
            AddMoneyCheats(registry);
        }

        private static void Register(ManagedCheatRegistry registry, ManagedCheat cheat)
        {
            if (!registry.Add(cheat))
            {
                throw new InvalidOperationException("Cheat code '" + cheat.Code + "' is not valid or clashes with another code.");
            }
        }

        private static void AddSpellCheats(ManagedCheatRegistry r)
        {
            Register(r, ManagedCheat.OneShot(
                "loadup", "Refill vessels", GroupSpells,
                "Fills every orb and restores every bottle.",
                h => h.RefillVessels()));

            Register(r, ManagedCheat.Toggle(
                "endlessmagic", "Endless vessels", GroupSpells,
                "A cast never runs a vessel dry.",
                h => SetModifier(h, m => m.InfiniteVessels = true),
                h => SetModifier(h, m => m.InfiniteVessels = false)));

            Register(r, ManagedCheat.Toggle(
                "quickcast", "No cooldown", GroupSpells,
                "Spells can be cast back to back.",
                h => SetModifier(h, m => m.CooldownScale = 0f),
                h => SetModifier(h, m => m.CooldownScale = 1f)));

            Register(r, ManagedCheat.Toggle(
                "longshot", "Long shots", GroupSpells,
                "Shots fly twice as far on the same vessel.",
                h => SetModifier(h, m => m.RangeScale = LongShotRangeScale),
                h => SetModifier(h, m => m.RangeScale = 1f)));

            Register(r, ManagedCheat.Toggle(
                "freesteer", "Free steering", GroupSpells,
                "Steering a shot costs no SP.",
                h => SetModifier(h, m => m.SteerCostScale = 0f),
                h => SetModifier(h, m => m.SteerCostScale = 1f)));
        }

        private static void AddPlayerCheats(ManagedCheatRegistry r)
        {
            Register(r, ManagedCheat.Toggle(
                "godmode", "God mode", GroupPlayer,
                "The player takes no damage.",
                h => h.SetInvulnerable(true),
                h => h.SetInvulnerable(false)));

            Register(r, ManagedCheat.OneShot(
                "healme", "Heal", GroupPlayer,
                "Restores the player to full health.",
                h => h.HealPlayer()));

            Register(r, ManagedCheat.Toggle(
                "fastfeet", "Fast feet", GroupPlayer,
                "The player walks twice as fast.",
                h => h.SetMoveSpeedScale(FastFeetScale),
                h => h.SetMoveSpeedScale(1f)));
        }

        private static void AddTimeCheats(ManagedCheatRegistry r)
        {
            Register(r, ManagedCheat.OneShot(
                "skipshort", "Skip 15 seconds", GroupTime,
                "Orbs charge as if 15 seconds of game time passed.",
                h => h.AdvanceTime(SkipShortSeconds)));

            Register(r, ManagedCheat.OneShot(
                "skiplong", "Skip 60 seconds", GroupTime,
                "Orbs charge as if 60 seconds of game time passed.",
                h => h.AdvanceTime(SkipLongSeconds)));

            Register(r, ManagedCheat.Toggle(
                "slowmotion", "Slow motion", GroupTime,
                "Game time runs at a quarter speed.",
                h => h.SetTimeScale(SlowMotionScale),
                h => h.SetTimeScale(1f)));
        }

        private static void AddWorldCheats(ManagedCheatRegistry r)
        {
            Register(r, ManagedCheat.OneShot(
                "wipenpcs", "Kill all NPCs", GroupWorld,
                "Every living NPC dies.",
                h => h.KillAllNpcs() > 0));

            Register(r, ManagedCheat.OneShot(
                "cleanup", "Clear hazards", GroupWorld,
                "Removes every chemical hazard from the world.",
                h => h.ClearHazards() > 0));
        }

        private static void AddMoneyCheats(ManagedCheatRegistry r)
        {
            Register(r, ManagedCheat.OneShot(
                "givegold", "Give 10 gold", GroupMoney,
                "Adds 10 gold to the purse.",
                h => WithWallet(h, w => w.AddGold(GiveGoldAmount))));

            Register(r, ManagedCheat.OneShot(
                "givesilver", "Give 50 silver", GroupMoney,
                "Adds 50 silver to the purse.",
                h => WithWallet(h, w => w.AddSilver(GiveSilverAmount))));

            Register(r, ManagedCheat.OneShot(
                "givecrystals", "Give crystals", GroupMoney,
                "Adds 3 crystals of each size to the purse.",
                h => WithWallet(h, w =>
                {
                    for (int size = 0; size < ManagedCrystals.SizeCount; size++)
                    {
                        w.AddCrystals((ManagedCrystalSize)size, GiveCrystalsEach);
                    }
                })));
        }

        // A cheat that needs the caster fails, and stays off, when there is none.
        private static bool SetModifier(IManagedCheatHost host, Action<ManagedSpellModifiers> change)
        {
            ManagedSpellModifiers modifiers = host.SpellModifiers;
            if (modifiers == null)
            {
                host.Log("[GTG Cheat] there is no spell caster in the scene");
                return false;
            }

            change(modifiers);
            return true;
        }

        private static bool WithWallet(IManagedCheatHost host, Action<ManagedWallet> change)
        {
            ManagedWallet wallet = host.Wallet;
            if (wallet == null)
            {
                host.Log("[GTG Cheat] there is no purse in the scene");
                return false;
            }

            change(wallet);
            return true;
        }
    }
}

#endif
