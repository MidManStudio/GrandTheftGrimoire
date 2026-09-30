// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterAuthoring.cs"
// ============================================================================

using Unity.Entities;
using UnityEngine;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Editor-only authoring component. Put it on an empty GameObject inside a
    /// SubScene. The pivot of that GameObject is the character's feet.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAuthoring : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _jumpSpeed = 7f;
        [SerializeField] private float _gravity = -20f;

        [Header("Ground Check")]
        [Tooltip("How far below the feet the ground ray reaches.")]
        [SerializeField] private float _groundCheckDistance = 0.2f;
        [Tooltip("The ground ray starts this far above the feet.")]
        [SerializeField] private float _groundSkin = 0.1f;

        [Header("Look")]
        [SerializeField] private float _mouseDegreesPerPixel = 0.1f;
        [SerializeField] private float _stickDegreesPerSecond = 180f;
        [SerializeField] private float _minPitch = -80f;
        [SerializeField] private float _maxPitch = 80f;

        private class Baker : Baker<CharacterAuthoring>
        {
            public override void Bake(CharacterAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                Transform authoringTransform = GetComponent<Transform>();

                AddComponent<CharacterTag>(entity);

                AddComponent(entity, new CharacterMoveSettings
                {
                    MoveSpeed = authoring._moveSpeed,
                    JumpSpeed = authoring._jumpSpeed,
                    Gravity = authoring._gravity,
                    GroundCheckDistance = authoring._groundCheckDistance,
                    GroundSkin = authoring._groundSkin,
                    MouseDegreesPerPixel = authoring._mouseDegreesPerPixel,
                    StickDegreesPerSecond = authoring._stickDegreesPerSecond,
                    MinPitch = authoring._minPitch,
                    MaxPitch = authoring._maxPitch,
                });

                AddComponent<CharacterInput>(entity);
                AddComponent(entity, new CharacterLook { Yaw = authoringTransform.eulerAngles.y, Pitch = 0f });
                AddComponent<CharacterVerticalVelocity>(entity);
                AddComponent<CharacterGroundState>(entity);
            }
        }
    }
}
