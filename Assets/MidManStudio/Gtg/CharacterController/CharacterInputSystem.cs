// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterInputSystem.cs"
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Phase 0 input gathering. The system polls the local devices on the main thread
    /// and a job writes the result into every <see cref="CharacterInput"/>. Correct for
    /// one local player only, replaced in Phase 1 by networked input.
    /// </summary>
    public partial struct CharacterInputSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;

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

            // Mouse look and mouse fire count only while the cursor is captured,
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

            if (math.length(move) > 1f)
            {
                move = math.normalize(move);
            }

            new InputJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Move = move,
                MouseDelta = mouseDelta,
                Stick = stick,
                JumpPressed = jumpPressed,
                FirePressed = firePressed,
            }.Schedule();
        }

        [BurstCompile]
        public partial struct InputJob : IJobEntity
        {
            public float DeltaTime;
            public float2 Move;
            public float2 MouseDelta;
            public float2 Stick;
            public bool JumpPressed;
            public bool FirePressed;

            public void Execute(ref CharacterInput input, in CharacterSettings settings)
            {
                input.Move = Move;
                input.Look = MouseDelta * settings.MouseDegreesPerPixel
                             + Stick * settings.StickDegreesPerSecond * DeltaTime;
                input.JumpPressed = JumpPressed;
                input.FirePressed = FirePressed;
            }
        }
    }
}
