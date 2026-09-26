// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterAuthoring.cs"
// ============================================================================

using Unity.Entities;
using UnityEngine;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Editor-only authoring component. Drop this on a GameObject inside a
    /// SubScene to get a baked character entity — the GameObject itself
    /// never exists at runtime, only the ECS data below does.
    /// </summary>
    public class CharacterAuthoring : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _jumpSpeed = 6f;
        [SerializeField] private float _gravity = -20f;

        [Header("Ground Check")]
        [Tooltip("How far below the character's feet to look for ground each frame.")]
        [SerializeField] private float _groundCheckDistance = 0.2f;

        private class Baker : Baker<CharacterAuthoring>
        {
            public override void Bake(CharacterAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<CharacterTag>(entity);

                AddComponent(entity, new CharacterMoveSettings
                {
                    MoveSpeed = authoring._moveSpeed,
                    JumpSpeed = authoring._jumpSpeed,
                    Gravity = authoring._gravity,
                    GroundCheckDistance = authoring._groundCheckDistance,
                });

                AddComponent<CharacterInput>(entity);
                AddComponent<CharacterVerticalVelocity>(entity);
                AddComponent<CharacterGroundState>(entity);
            }
        }
    }
}
