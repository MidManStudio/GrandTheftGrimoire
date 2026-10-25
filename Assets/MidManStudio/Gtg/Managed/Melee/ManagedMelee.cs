// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedMelee.cs"
// ============================================================================

using System;
using MidManStudio.Gtg.Managed.CharacterController;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Melee
{
    /// <summary>
    /// Melee for the managed character. While the weapon is equipped, a fire press starts a swing
    /// and no spell is cast. The swing checks a cone in front of the chest when its hit moment
    /// comes, and hurts what is inside. Equipping comes from <see cref="Equip"/>, which
    /// <see cref="ManagedMeleeEquipBridge"/> calls for the HUD, and picking a spell puts the weapon
    /// away. Runs between the character and the spell caster, so it takes the fire press before the
    /// caster sees it. Put it on the same GameObject as ManagedCharacter.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ManagedCharacter))]
    public sealed class ManagedMelee : MonoBehaviour
    {
        [SerializeField] private ManagedMeleeWeapon _weapon = new ManagedMeleeWeapon();
        [Tooltip("Layers the swing looks at.")]
        [SerializeField] private LayerMask _hitMask = ~0;
        [SerializeField] private bool _equipOnStart;
        [SerializeField] private bool _logHits = true;

        private readonly ManagedMeleeSwing _swing = new ManagedMeleeSwing();
        private ManagedCharacter _character;
        private bool _equipped;

        /// <summary>Raised when the weapon is equipped or put away, with the new state.</summary>
        public event Action<bool> EquippedChanged;

        /// <summary>Raised when a swing starts.</summary>
        public event Action SwingStarted;

        /// <summary>Raised at the hit moment of each swing, with how many targets were hurt. Zero is a miss.</summary>
        public event Action<int> Struck;

        public ManagedMeleeWeapon Weapon { get { return _weapon; } }
        public bool IsEquipped { get { return _equipped; } }
        public bool IsSwinging { get { return _swing.IsSwinging; } }

        /// <summary>How far through the swing it is, 0 through 1.</summary>
        public float SwingProgress { get { return _swing.Progress; } }

        private void Awake()
        {
            _character = GetComponent<ManagedCharacter>();
            if (_weapon == null)
            {
                _weapon = new ManagedMeleeWeapon();
            }

            _equipped = _equipOnStart;
        }

        /// <summary>Equips the weapon. Does nothing when it is already in hand.</summary>
        public void Equip()
        {
            SetEquipped(true);
        }

        /// <summary>Puts the weapon away and ends a swing under way.</summary>
        public void Unequip()
        {
            SetEquipped(false);
        }

        public void Toggle()
        {
            SetEquipped(!_equipped);
        }

        public void SetEquipped(bool equipped)
        {
            if (_equipped == equipped)
            {
                return;
            }

            _equipped = equipped;
            if (!equipped)
            {
                _swing.Cancel();
            }

            Log(equipped ? "[GTG Melee] " + _weapon.DisplayName + " equipped" : "[GTG Melee] " + _weapon.DisplayName + " put away");
            Action<bool> changed = EquippedChanged;
            if (changed != null)
            {
                changed(equipped);
            }
        }

        private void Update()
        {
            ManagedCharacterInputFrame input = _character.Input;
            if (_equipped && (input.SpellSlot > 0 || input.CycleSpell != 0))
            {
                Unequip();
            }

            // With the Cast module off, as in build mode, the weapon is idle and the press is left alone.
            if (!_equipped || !_character.Has(ManagedCharacterFeature.Cast))
            {
                _swing.Cancel();
                return;
            }

            if (_swing.Tick(Time.deltaTime))
            {
                Strike();
            }

            // A press during a swing is taken too, so it does not cast a spell.
            if (_character.ConsumeFire() && !_swing.IsSwinging)
            {
                _swing.Start(_weapon.SafeHitDelay, _weapon.SafeSwingSeconds);
                Action started = SwingStarted;
                if (started != null)
                {
                    started();
                }
            }
        }

        // The cone follows the aim at the hit moment, not at the press.
        private void Strike()
        {
            Vector3 origin = _character.FeetPosition + Vector3.up * _weapon.OriginHeight;
            Vector3 forward = Quaternion.Euler(0f, _character.Yaw, 0f) * Vector3.forward;
            int hits = ManagedMeleeStrike.Strike(origin, forward, _weapon, gameObject, _hitMask);
            Log("[GTG Melee] " + _weapon.DisplayName + " swing hit " + hits);
            Action<int> struck = Struck;
            if (struck != null)
            {
                struck(hits);
            }
        }

        private void Log(string message)
        {
            if (_logHits)
            {
                Debug.Log(message);
            }
        }
    }
}
