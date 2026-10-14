// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudDevKeys.cs"
// ============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using MidManStudio.Gtg.Managed.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// Keys for trying the HUD before the systems behind it exist. Editor and development builds
    /// only. The HUD adds it to its own GameObject. ] adds a heat section and wraps to none after
    /// nine, [ takes one away, and backslash gives the player sample items, fills the wheel with
    /// sample entries, and logs what the wheel and the quick slots do.
    /// </summary>
    public sealed class ManagedHudDevKeys : MonoBehaviour
    {
        private ManagedHud _hud;

        /// <summary>Ties the keys to a HUD.</summary>
        public void Bind(ManagedHud hud)
        {
            if (_hud != null)
            {
                _hud.Wheel.Picked -= OnPicked;
                _hud.QuickSlotUsed -= OnUsed;
            }

            _hud = hud;
            if (_hud != null)
            {
                _hud.Wheel.Picked += OnPicked;
                _hud.QuickSlotUsed += OnUsed;
            }
        }

        private void OnDestroy()
        {
            Bind(null);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (_hud == null || keyboard == null)
            {
                return;
            }

            int heat = _hud.State.HeatSections;
            if (keyboard.rightBracketKey.wasPressedThisFrame)
            {
                _hud.SetHeat(heat >= ManagedHudMath.MaxHeat ? 0 : heat + 1);
            }

            if (keyboard.leftBracketKey.wasPressedThisFrame)
            {
                _hud.SetHeat(heat <= 0 ? ManagedHudMath.MaxHeat : heat - 1);
            }

            if (keyboard.backslashKey.wasPressedThisFrame)
            {
                Stock();
            }
        }

        private void Stock()
        {
            ManagedPlayerItemBag player = _hud.PlayerBag;
            if (player == null)
            {
                Debug.Log("[GTG HUD] No player bag yet, so there is nothing to stock.");
                return;
            }

            ManagedItemSamples.StockPlayerBag(player);
            _hud.Wheel.SetEntry(0, new ManagedWheelEntry("Fireball", "spell_fireball"));
            _hud.Wheel.SetEntry(1, new ManagedWheelEntry("Ice", "spell_ice"));
            _hud.Wheel.SetEntry(2, new ManagedWheelEntry("Iron Sword", ManagedHudSprites.ItemIconKey(ManagedItemSamples.IronSword)));
            _hud.Wheel.SetEntry(3, new ManagedWheelEntry("Health Potion", ManagedHudSprites.ItemIconKey(ManagedItemSamples.HealthPotion)));
            Debug.Log("[GTG HUD] Stocked sample items, quick slots 4 and 5, and the four wheel sections.");
        }

        private static void OnPicked(int section)
        {
            Debug.Log("[GTG HUD] Wheel section " + section + " is now active.");
        }

        private static void OnUsed(int slot, int itemId)
        {
            Debug.Log("[GTG HUD] Quick slot " + slot + " used item " + itemId + ".");
        }
    }
}

#endif
