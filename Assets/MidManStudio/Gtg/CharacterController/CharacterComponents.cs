// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterComponents.cs"
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Marks an entity as a player-controlled character. Empty on purpose —
    /// it exists so systems can query "the character" without depending on
    /// any of its other components.
    /// </summary>
    public struct CharacterTag : IComponentData
    {
    }

    /// <summary>
    /// Tunable movement numbers, baked once from <see cref="CharacterAuthoring"/>.
    /// Read-only at runtime — nothing in the movement system writes to this.
    /// </summary>
    public struct CharacterMoveSettings : IComponentData
    {
        public float MoveSpeed;
        public float JumpSpeed;
        public float Gravity;
        public float GroundCheckDistance;
    }

    /// <summary>
    /// Per-frame input state for one character, written by
    /// <see cref="CharacterInputSystem"/> and read by
    /// <see cref="CharacterMovementSystem"/>. Deliberately just data — no
    /// device polling happens outside the input system.
    /// </summary>
    public struct CharacterInput : IComponentData
    {
        /// <summary>Local-space move direction from WASD / left stick, each axis in [-1, 1].</summary>
        public float2 Move;

        /// <summary>Look delta from mouse / right stick for this frame.</summary>
        public float2 Look;

        /// <summary>True for exactly the frame the jump button went down.</summary>
        public bool JumpPressed;
    }

    /// <summary>
    /// Current vertical speed, carried across frames so gravity can
    /// accumulate between ground contacts. Horizontal movement doesn't need
    /// this — it's re-derived from <see cref="CharacterInput"/> every frame.
    /// </summary>
    public struct CharacterVerticalVelocity : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// Result of this frame's ground check, written by the movement system's
    /// raycast before it decides whether to apply gravity or a jump.
    /// </summary>
    public struct CharacterGroundState : IComponentData
    {
        public bool IsGrounded;
    }
}
