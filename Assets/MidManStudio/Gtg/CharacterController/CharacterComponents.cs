// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterComponents.cs"
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>Marks an entity as a player-controlled character.</summary>
    public struct CharacterTag : IComponentData
    {
    }

    /// <summary>
    /// Movement and look tuning, baked once from <see cref="CharacterAuthoring"/>.
    /// Read-only at runtime.
    /// </summary>
    public struct CharacterMoveSettings : IComponentData
    {
        public float MoveSpeed;
        public float JumpSpeed;
        public float Gravity;

        /// <summary>How far below the feet the ground ray reaches, in meters.</summary>
        public float GroundCheckDistance;

        /// <summary>The ground ray starts this far above the feet, in meters.</summary>
        public float GroundSkin;

        public float MouseDegreesPerPixel;
        public float StickDegreesPerSecond;
        public float MinPitch;
        public float MaxPitch;
    }

    /// <summary>
    /// Per-frame input for one character. Written by
    /// <see cref="CharacterInputSystem"/>, read by the look, movement and spell systems.
    /// </summary>
    public struct CharacterInput : IComponentData
    {
        /// <summary>Local move direction from WASD or left stick, each axis in [-1, 1].</summary>
        public float2 Move;

        /// <summary>Look change this frame in degrees. x turns right, y looks up.</summary>
        public float2 Look;

        /// <summary>True only on the frame the jump button went down.</summary>
        public bool JumpPressed;

        /// <summary>True only on the frame the fire button went down.</summary>
        public bool FirePressed;
    }

    /// <summary>
    /// View direction in degrees. Pitch follows the Unity convention, so a
    /// positive value looks down. The camera rig and the spell aim both read it.
    /// </summary>
    public struct CharacterLook : IComponentData
    {
        public float Yaw;
        public float Pitch;
    }

    /// <summary>Vertical speed carried between frames so gravity accumulates.</summary>
    public struct CharacterVerticalVelocity : IComponentData
    {
        public float Value;
    }

    /// <summary>Result of this frame's ground check.</summary>
    public struct CharacterGroundState : IComponentData
    {
        public bool IsGrounded;
    }
}
