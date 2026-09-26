// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterInputSystem.cs"
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Phase 0 input gathering: polls the local keyboard/mouse/gamepad
    /// directly and writes the result into every <see cref="CharacterInput"/>
    /// in the world. That's correct for one local player and nothing else.
    ///
    /// This system gets replaced, not extended, once Netcode for Entities is
    /// wired in — networked input is gathered per-connection through NFE's
    /// own input-handling systems, not a world-wide poll like this one.
    /// Keeping this phase's version simple on purpose: it only has to prove
    /// movement feels right locally before that rework happens.
    ///
    /// Not Burst-compiled — it touches managed UnityEngine.InputSystem
    /// APIs, which Burst can't compile anyway.
    /// </summary>
    public partial struct CharacterInputSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var gamepad = Gamepad.current;

            float2 move = float2.zero;
            float2 look = float2.zero;
            bool jumpPressed = false;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed) move.x -= 1f;
                jumpPressed |= keyboard.spaceKey.wasPressedThisFrame;
            }

            if (mouse != null)
            {
                look += mouse.delta.ReadValue();
            }

            if (gamepad != null)
            {
                move += gamepad.leftStick.ReadValue();
                look += gamepad.rightStick.ReadValue();
                jumpPressed |= gamepad.buttonSouth.wasPressedThisFrame;
            }

            move = math.length(move) > 1f ? math.normalize(move) : move;

            foreach (var input in SystemAPI.Query<RefRW<CharacterInput>>().WithAll<CharacterTag>())
            {
                input.ValueRW.Move = move;
                input.ValueRW.Look = look;
                input.ValueRW.JumpPressed = jumpPressed;
            }
        }
    }
}
