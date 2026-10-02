// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCharacterInput.cs"
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace MidManStudio.Gtg.Managed.CharacterController
{
    /// <summary>Input of one frame for one local player.</summary>
    public struct ManagedCharacterInputFrame
    {
        /// <summary>Local move direction, each axis in [-1, 1].</summary>
        public Vector2 Move;

        /// <summary>Look change this frame in degrees. x turns right, y looks up.</summary>
        public Vector2 Look;

        public bool JumpPressed;
        public bool FirePressed;
    }

    /// <summary>Polls the local devices. Keyboard, mouse and gamepad are all read directly.</summary>
    public static class ManagedCharacterInput
    {
        public static ManagedCharacterInputFrame Read(
            float deltaTime, float mouseDegreesPerPixel, float stickDegreesPerSecond)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;

            var frame = new ManagedCharacterInputFrame();
            Vector2 move = Vector2.zero;
            Vector2 mouseDelta = Vector2.zero;
            Vector2 stick = Vector2.zero;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed) move.x -= 1f;
                frame.JumpPressed |= keyboard.spaceKey.wasPressedThisFrame;
                frame.FirePressed |= keyboard.fKey.wasPressedThisFrame;
            }

            // Mouse look and mouse fire count only while the cursor is captured,
            // so the click that captures it does not also shoot.
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                mouseDelta = mouse.delta.ReadValue();
                frame.FirePressed |= mouse.leftButton.wasPressedThisFrame;
            }

            if (gamepad != null)
            {
                move += gamepad.leftStick.ReadValue();
                stick = gamepad.rightStick.ReadValue();
                frame.JumpPressed |= gamepad.buttonSouth.wasPressedThisFrame;
                frame.FirePressed |= gamepad.rightTrigger.wasPressedThisFrame;
            }

            if (move.magnitude > 1f)
            {
                move = move.normalized;
            }

            frame.Move = move;
            frame.Look = mouseDelta * mouseDegreesPerPixel + stick * (stickDegreesPerSecond * deltaTime);
            return frame;
        }
    }
}
