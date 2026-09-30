// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterInputSystem.cs"
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Phase 0 input gathering. Polls the local devices and writes the result
    /// into every <see cref="CharacterInput"/>. Correct for one local player only.
    /// Replaced, not extended, when Netcode for Entities input arrives.
    /// Not Burst compiled because it reads managed Input System objects.
    /// </summary>
    public partial struct CharacterInputSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            float deltaTime = SystemAPI.Time.DeltaTime;

            float2 move = float2.zero;
            float2 mouseDelta = float2.zero;
            float2 stick = float2.zero;
            bool jumpPressed = false;
            bool firePressed = false;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed) move.x -= 1f;
                jumpPressed |= keyboard.spaceKey.wasPressedThisFrame;
                firePressed |= keyboard.fKey.wasPressedThisFrame;
            }

            // Mouse look and click fire only count while the cursor is captured,
            // so the click that captures it does not also shoot.
            if (mouse != null && cursorLocked)
            {
                mouseDelta = mouse.delta.ReadValue();
                firePressed |= mouse.leftButton.wasPressedThisFrame;
            }

            if (gamepad != null)
            {
                move += gamepad.leftStick.ReadValue();
                stick = gamepad.rightStick.ReadValue();
                jumpPressed |= gamepad.buttonSouth.wasPressedThisFrame;
                firePressed |= gamepad.rightTrigger.wasPressedThisFrame;
            }

            move = math.length(move) > 1f ? math.normalize(move) : move;

            foreach (var (input, settings) in
                     SystemAPI.Query<RefRW<CharacterInput>, RefRO<CharacterMoveSettings>>()
                         .WithAll<CharacterTag>())
            {
                CharacterMoveSettings cfg = settings.ValueRO;
                input.ValueRW.Move = move;
                input.ValueRW.Look = mouseDelta * cfg.MouseDegreesPerPixel
                                     + stick * cfg.StickDegreesPerSecond * deltaTime;
                input.ValueRW.JumpPressed = jumpPressed;
                input.ValueRW.FirePressed = firePressed;
            }
        }
    }
}
