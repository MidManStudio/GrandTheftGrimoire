// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudSlotView.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Items;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// The behaviour of one quick slot: a left click uses it, a right click empties it, it can be
    /// dragged to another slot to swap, and an item dragged from anywhere that uses
    /// <see cref="ManagedItemDrag"/> can be dropped on it. A slot dragged onto nothing that takes
    /// it is emptied. The drawing is done by the HUD, this only reacts.
    /// </summary>
    public sealed class ManagedHudSlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
    {
        private ManagedHud _hud;
        private int _index;
        private bool _dragging;

        /// <summary>Ties the view to a HUD and to the slot it stands for.</summary>
        public void Bind(ManagedHud hud, int index)
        {
            _hud = hud;
            _index = index;
        }

        public int Index { get { return _index; } }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = false;
            ManagedQuickSlots quick = _hud != null ? _hud.QuickSlots : null;
            int itemId;
            if (quick == null || eventData.button != PointerEventData.InputButton.Left || !quick.TryGetItem(_index, out itemId))
            {
                return;
            }

            ManagedItemDrag.Begin(itemId, 1, ManagedItemDragOrigin.QuickSlot, _index);
            _dragging = true;
            _hud.ShowGhost(itemId, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragging)
            {
                _hud.MoveGhost(eventData.position);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            bool taken = ManagedItemDrag.End();
            _hud.HideGhost();

            // Dropped on nothing that takes it: the slot gives up its item. A target that took the
            // drag, another slot or a window, has dealt with it already.
            ManagedQuickSlots quick = _hud.QuickSlots;
            if (!taken && quick != null)
            {
                quick.Clear(_index);
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            ManagedQuickSlots quick = _hud != null ? _hud.QuickSlots : null;
            if (quick == null || !ManagedItemDrag.Active)
            {
                return;
            }

            ManagedItemPayload payload = ManagedItemDrag.Payload;
            bool placed;
            if (payload.Origin == ManagedItemDragOrigin.QuickSlot)
            {
                // Dropping a slot on itself keeps it as it is.
                placed = payload.SourceIndex == _index || quick.Swap(payload.SourceIndex, _index);
            }
            else
            {
                placed = quick.Assign(_index, payload.ItemId);
            }

            if (placed)
            {
                ManagedItemDrag.Accept();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_hud == null)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _hud.UseQuickSlot(_index);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                ManagedQuickSlots quick = _hud.QuickSlots;
                if (quick != null)
                {
                    quick.Clear(_index);
                }
            }
        }
    }
}
