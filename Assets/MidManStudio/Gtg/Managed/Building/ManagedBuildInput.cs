// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedBuildInput.cs"
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>Input of one frame for build mode.</summary>
    public struct ManagedBuildInputFrame
    {
        public bool TogglePressed;

        /// <summary>Plus or minus one for a quarter turn, otherwise zero.</summary>
        public int Rotate;

        /// <summary>Left button went down this frame.</summary>
        public bool PrimaryPressed;

        /// <summary>Escape or the right button went down this frame.</summary>
        public bool CancelPressed;

        public bool PointerAvailable;

        /// <summary>Pointer position in screen pixels, origin at the bottom left.</summary>
        public Vector2 Pointer;
    }

    /// <summary>Polls the keyboard and mouse. Same direct style as ManagedCharacterInput.</summary>
    public static class ManagedBuildInput
    {
        public static ManagedBuildInputFrame Read(Key toggleKey, bool wheelRotates)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            var frame = new ManagedBuildInputFrame();

            if (keyboard != null)
            {
                // The Keyboard indexer throws for Key.None and for IMESelected, so look the key up by index.
                int keyIndex = (int)toggleKey - 1;
                if (keyIndex >= 0 && keyIndex < keyboard.allKeys.Count)
                {
                    frame.TogglePressed = keyboard.allKeys[keyIndex].wasPressedThisFrame;
                }

                if (keyboard.rKey.wasPressedThisFrame)
                {
                    bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                    frame.Rotate = shift ? -1 : 1;
                }

                frame.CancelPressed |= keyboard.escapeKey.wasPressedThisFrame;
            }

            if (mouse != null)
            {
                frame.PointerAvailable = true;
                frame.Pointer = mouse.position.ReadValue();
                frame.PrimaryPressed = mouse.leftButton.wasPressedThisFrame;
                frame.CancelPressed |= mouse.rightButton.wasPressedThisFrame;

                if (wheelRotates && frame.Rotate == 0)
                {
                    float wheel = mouse.scroll.ReadValue().y;
                    if (wheel > 0f)
                    {
                        frame.Rotate = 1;
                    }
                    else if (wheel < 0f)
                    {
                        frame.Rotate = -1;
                    }
                }
            }

            return frame;
        }
    }
}
