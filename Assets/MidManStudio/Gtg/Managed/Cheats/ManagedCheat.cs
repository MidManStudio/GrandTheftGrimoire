// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCheat.cs"
// ============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;

namespace MidManStudio.Gtg.Managed.Cheats
{
    /// <summary>
    /// One cheat. A toggle stays on until it is switched off, an action runs once. The apply
    /// delegate returns false when the cheat could not act, and then a toggle stays off. The
    /// state lives here, in memory, and nothing is ever saved.
    /// </summary>
    public sealed class ManagedCheat
    {
        public readonly string Code;
        public readonly string Name;
        public readonly string Group;
        public readonly string Description;
        public readonly bool IsToggle;

        internal readonly Func<IManagedCheatHost, bool> Apply;
        internal readonly Action<IManagedCheatHost> Revert;

        private ManagedCheat(
            string code, string name, string group, string description,
            Func<IManagedCheatHost, bool> apply, Action<IManagedCheatHost> revert)
        {
            Code = code;
            Name = name;
            Group = group;
            Description = description;
            Apply = apply;
            Revert = revert;
            IsToggle = revert != null;
        }

        public bool On { get; internal set; }

        /// <summary>A cheat that stays on until it is switched off.</summary>
        public static ManagedCheat Toggle(
            string code, string name, string group, string description,
            Func<IManagedCheatHost, bool> on, Action<IManagedCheatHost> off)
        {
            return new ManagedCheat(code, name, group, description, on, off);
        }

        /// <summary>A cheat that runs once each time it is used.</summary>
        public static ManagedCheat OneShot(
            string code, string name, string group, string description,
            Func<IManagedCheatHost, bool> run)
        {
            return new ManagedCheat(code, name, group, description, run, null);
        }
    }
}

#endif
