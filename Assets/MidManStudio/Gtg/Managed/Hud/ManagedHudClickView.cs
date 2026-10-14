// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudClickView.cs"
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// Makes a graphic clickable. It is not a uGUI Button on purpose: a Button is selectable, and
    /// a selected object takes the Space and Enter keys as a press, which would fire the jump key
    /// into the HUD. This only listens for a left click.
    /// </summary>
    public sealed class ManagedHudClickView : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Raised on a left click.</summary>
        public event Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            Action clicked = Clicked;
            if (clicked != null)
            {
                clicked();
            }
        }
    }
}
