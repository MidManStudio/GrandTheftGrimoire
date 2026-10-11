// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCheatSceneHost.cs"
// ============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using MidManStudio.Gtg.Managed.CharacterController;
using MidManStudio.Gtg.Managed.Chemistry;
using MidManStudio.Gtg.Managed.Economy;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.Magic;
using MidManStudio.Gtg.Managed.NPC;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Cheats
{
    /// <summary>
    /// The cheat host for a running scene. It looks the player, the caster and the world objects
    /// up when a cheat needs them and keeps no state of its own besides the last message, so a
    /// respawn or a scene load never leaves it pointing at something gone.
    /// </summary>
    public sealed class ManagedCheatSceneHost : IManagedCheatHost
    {
        private ManagedSpellCaster _caster;
        private ManagedCharacter _character;

        /// <summary>The last line logged, for the panel to show.</summary>
        public string LastMessage { get; private set; }

        /// <summary>Unscaled time the last line was logged at.</summary>
        public float LastMessageTime { get; private set; }

        private ManagedSpellCaster Caster
        {
            get
            {
                if (_caster == null)
                {
                    _caster = UnityEngine.Object.FindFirstObjectByType<ManagedSpellCaster>();
                }

                return _caster;
            }
        }

        private ManagedCharacter Character
        {
            get
            {
                if (_character == null)
                {
                    _character = UnityEngine.Object.FindFirstObjectByType<ManagedCharacter>();
                }

                return _character;
            }
        }

        public ManagedSpellModifiers SpellModifiers
        {
            get { return Caster != null ? Caster.Modifiers : null; }
        }

        public ManagedWallet Wallet
        {
            get { return Caster != null ? Caster.Wallet : null; }
        }

        public bool RefillVessels()
        {
            ManagedSpellCaster caster = Caster;
            if (caster == null)
            {
                Log("[GTG Cheat] there is no spell caster in the scene");
                return false;
            }

            caster.RefillVessels();
            return true;
        }

        public bool AdvanceTime(float seconds)
        {
            ManagedSpellCaster caster = Caster;
            if (caster == null)
            {
                Log("[GTG Cheat] there is no spell caster in the scene");
                return false;
            }

            caster.AdvanceTime(seconds);
            return true;
        }

        public bool SetTimeScale(float scale)
        {
            Time.timeScale = Mathf.Max(0f, scale);
            return true;
        }

        public bool SetInvulnerable(bool on)
        {
            ManagedHealth health = PlayerHealth();
            if (health == null)
            {
                Log("[GTG Cheat] the player has no ManagedHealth");
                return false;
            }

            health.Invulnerable = on;
            return true;
        }

        public bool HealPlayer()
        {
            ManagedHealth health = PlayerHealth();
            if (health == null)
            {
                Log("[GTG Cheat] the player has no ManagedHealth");
                return false;
            }

            if (!health.IsAlive)
            {
                Log("[GTG Cheat] the player is dead and a heal cannot bring them back");
                return false;
            }

            health.Heal(health.Max);
            return true;
        }

        public bool SetMoveSpeedScale(float scale)
        {
            ManagedCharacter character = Character;
            if (character == null)
            {
                Log("[GTG Cheat] there is no player character in the scene");
                return false;
            }

            character.MoveSpeedScale = scale;
            return true;
        }

        // An NPC that is invulnerable by design survives and is not counted.
        public int KillAllNpcs()
        {
            ManagedNpcActor[] npcs = UnityEngine.Object.FindObjectsByType<ManagedNpcActor>(FindObjectsSortMode.None);
            int killed = 0;
            for (int i = 0; i < npcs.Length; i++)
            {
                ManagedHealth health = npcs[i].GetComponent<ManagedHealth>();
                if (health == null || !health.IsAlive)
                {
                    continue;
                }

                health.TakeDamage(health.Current);
                if (!health.IsAlive)
                {
                    killed++;
                }
            }

            if (killed == 0)
            {
                Log("[GTG Cheat] there is no living NPC to kill");
            }

            return killed;
        }

        public int ClearHazards()
        {
            ManagedChemicalHazardField[] fields =
                UnityEngine.Object.FindObjectsByType<ManagedChemicalHazardField>(FindObjectsSortMode.None);
            int cleared = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                cleared += fields[i].Count;
                fields[i].Clear();
            }

            if (cleared == 0)
            {
                Log("[GTG Cheat] there is no hazard to clear");
            }

            return cleared;
        }

        public void Log(string message)
        {
            LastMessage = message;
            LastMessageTime = Time.unscaledTime;
            Debug.Log(message);
        }

        private ManagedHealth PlayerHealth()
        {
            ManagedCharacter character = Character;
            return character != null ? character.GetComponent<ManagedHealth>() : null;
        }
    }
}

#endif
