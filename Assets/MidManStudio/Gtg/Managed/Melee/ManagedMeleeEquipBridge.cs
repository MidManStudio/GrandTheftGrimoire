// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedMeleeEquipBridge.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Hud;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Melee
{
    /// <summary>
    /// Ties the HUD to <see cref="ManagedMelee"/>. Using the quick slot that holds the weapon item
    /// equips it, and using it again puts it away. Picking a wheel section equips the weapon when
    /// the section shows the weapon icon and puts it away for any other section. The HUD is found
    /// by itself, so the order of the scene objects does not matter. Put it on the same
    /// GameObject as ManagedMelee.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ManagedMelee))]
    public sealed class ManagedMeleeEquipBridge : MonoBehaviour
    {
        // Frames between searches for a HUD that is not there yet.
        private const int SearchInterval = 30;

        private ManagedMelee _melee;
        private ManagedHud _hud;

        private void Awake()
        {
            _melee = GetComponent<ManagedMelee>();
        }

        private void Start()
        {
            TryBind();
        }

        private void Update()
        {
            if (_hud == null && Time.frameCount % SearchInterval == 0)
            {
                TryBind();
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void TryBind()
        {
            ManagedHud hud = FindFirstObjectByType<ManagedHud>();
            if (hud == null)
            {
                return;
            }

            _hud = hud;
            _hud.QuickSlotUsed += OnQuickSlotUsed;
            _hud.Wheel.Picked += OnWheelPicked;
        }

        private void Unbind()
        {
            if (_hud != null)
            {
                _hud.QuickSlotUsed -= OnQuickSlotUsed;
                _hud.Wheel.Picked -= OnWheelPicked;
            }

            _hud = null;
        }

        private void OnQuickSlotUsed(int slot, int itemId)
        {
            if (itemId == _melee.Weapon.ItemId)
            {
                _melee.Toggle();
            }
        }

        private void OnWheelPicked(int section)
        {
            ManagedWheelEntry entry;
            if (_hud == null || !_hud.Wheel.TryGetEntry(section, out entry))
            {
                return;
            }

            _melee.SetEquipped(entry.IconKey == ManagedHudSprites.ItemIconKey(_melee.Weapon.ItemId));
        }
    }
}
