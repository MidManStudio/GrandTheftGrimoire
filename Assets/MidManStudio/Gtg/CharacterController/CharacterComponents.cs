// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterComponents.cs"
// ============================================================================

using System;
using Unity.Entities;
using Unity.Mathematics;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>Marks an entity as a player-controlled character.</summary>
    public struct CharacterTag : IComponentData
    {
    }

    /// <summary>One switch per character module. A module system does nothing when its flag is off.</summary>
    [Flags]
    public enum CharacterFeature : uint
    {
        None = 0,
        Look = 1 << 0,
        Move = 1 << 1,
        Jump = 1 << 2,
        Gravity = 1 << 3,
        Cast = 1 << 4,
    }

    /// <summary>
    /// The enabled modules of a character. Baked from the authoring checkboxes and
    /// writable at runtime, so a stun, a cutscene or a flight mode only flips a flag.
    /// </summary>
    public struct CharacterFeatures : IComponentData
    {
        public CharacterFeature Enabled;

        public bool Has(CharacterFeature feature)
        {
            return (Enabled & feature) != 0;
        }
    }

    /// <summary>Tuning for the character modules, baked once and read only at runtime.</summary>
    public struct CharacterSettings : IComponentData
    {
        public float MoveSpeed;
        public float Acceleration;

        /// <summary>Share of the acceleration that still applies in the air, 0 to 1.</summary>
        public float AirControl;

        public float JumpSpeed;

        /// <summary>Seconds after leaving an edge in which a jump is still allowed.</summary>
        public float CoyoteTime;

        /// <summary>Negative, in meters per second squared.</summary>
        public float Gravity;

        /// <summary>Largest fall speed, positive.</summary>
        public float GravityCap;

        /// <summary>Downward speed held while grounded so the character follows the floor.</summary>
        public float GroundStickSpeed;

        public float GroundProbeDistance;

        /// <summary>Cosine of the steepest walkable slope angle.</summary>
        public float MaxSlopeDot;

        /// <summary>Gap kept between the capsule and every surface.</summary>
        public float SkinWidth;

        public int MaxSlideIterations;
        public float MouseDegreesPerPixel;
        public float StickDegreesPerSecond;
        public float MinPitch;
        public float MaxPitch;
    }

    /// <summary>Per-frame input for one character, written by <see cref="CharacterInputSystem"/>.</summary>
    public struct CharacterInput : IComponentData
    {
        /// <summary>Local move direction, each axis in [-1, 1].</summary>
        public float2 Move;

        /// <summary>Look change this frame in degrees. x turns right, y looks up.</summary>
        public float2 Look;

        public bool JumpPressed;
        public bool FirePressed;
    }

    /// <summary>
    /// View direction in degrees. Pitch follows the Unity convention, so a positive
    /// value looks down. The camera rig and the spell aim both read it.
    /// </summary>
    public struct CharacterLook : IComponentData
    {
        public float Yaw;
        public float Pitch;
    }

    /// <summary>Motion state shared by the modules. The movement system turns it into a position change.</summary>
    public struct CharacterMotor : IComponentData
    {
        public float3 HorizontalVelocity;
        public float VerticalVelocity;
        public bool IsGrounded;
        public float3 GroundNormal;
        public float TimeSinceGrounded;
    }
}
