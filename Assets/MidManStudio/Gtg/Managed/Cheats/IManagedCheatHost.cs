// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "IManagedCheatHost.cs"
// ============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using MidManStudio.Gtg.Managed.Economy;
using MidManStudio.Gtg.Managed.Magic;

namespace MidManStudio.Gtg.Managed.Cheats
{
    /// <summary>
    /// Everything a cheat is allowed to touch. The built-in cheats are written against this and
    /// never against the scene, so they can be tested without one and another host can stand in.
    /// A method that cannot do its job returns false, or null for a property, and logs why.
    /// </summary>
    public interface IManagedCheatHost
    {
        /// <summary>The caster's spell scales, or null when there is no caster.</summary>
        ManagedSpellModifiers SpellModifiers { get; }

        /// <summary>The player's purse, or null when there is none.</summary>
        ManagedWallet Wallet { get; }

        bool RefillVessels();

        /// <summary>Moves orb charging forward as if this many seconds of game time had passed.</summary>
        bool AdvanceTime(float seconds);

        /// <summary>Sets how fast game time runs. 1 is normal.</summary>
        bool SetTimeScale(float scale);

        bool SetInvulnerable(bool on);

        bool HealPlayer();

        bool SetMoveSpeedScale(float scale);

        /// <summary>Kills every living NPC and returns how many died.</summary>
        int KillAllNpcs();

        /// <summary>Removes every chemical hazard and returns how many there were.</summary>
        int ClearHazards();

        void Log(string message);
    }
}

#endif
