// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedBuildPanel.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>
    /// The build tray, built in code with UI Toolkit. A bottom dock with an Inventory tab for the
    /// stations the player holds and a Blueprints tab for unlocked schematics. Pressing a slot
    /// starts placing, and the slot captures the pointer so the release arrives here wherever it
    /// happens. Plain C#, taking its collaborators through the constructor like the dragndrop
    /// UI Toolkit adapters do, so there is no hierarchy to search.
    /// </summary>
    public sealed class ManagedBuildPanel : IDisposable
    {
        private const float DragThresholdPixels = 6f;

        private static readonly Color PanelColor = new Color(0.09f, 0.08f, 0.07f, 0.92f);
        private static readonly Color SlotColor = new Color(0.17f, 0.15f, 0.13f, 1f);
        private static readonly Color TextColor = new Color(0.93f, 0.9f, 0.84f, 1f);
        private static readonly Color DimTextColor = new Color(0.65f, 0.62f, 0.57f, 1f);
        private static readonly Color EdgeColor = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color AccentColor = new Color(0.25f, 0.55f, 1f, 1f);
        private static readonly Color ErrorColor = new Color(1f, 0.38f, 0.32f, 1f);

        private sealed class SlotView
        {
            public VisualElement Element;
            public string Id;
            public ManagedPlacementSource Source;
        }

        private enum Tab
        {
            Inventory,
            Blueprints,
        }

        private readonly VisualElement _root;
        private readonly IManagedBuildStock _stock;
        private readonly ManagedBlueprintBook _blueprints;
        private readonly ManagedPlacementController _controller;
        private readonly Func<ManagedPlacementSource, string, bool> _onSlotPress;
        private readonly Action<bool, bool> _onSlotRelease;
        private readonly Action _onTrayPress;
        private readonly string _closeKeyLabel;

        private readonly VisualElement _tray;
        private readonly VisualElement _slotRow;
        private readonly Label _inventoryTab;
        private readonly Label _blueprintTab;
        private readonly Label _status;
        private readonly Label _hint;
        private readonly List<SlotView> _slots = new List<SlotView>();
        private readonly List<ManagedStockEntry> _entries = new List<ManagedStockEntry>();

        private Tab _tab = Tab.Inventory;
        private bool _visible;
        private bool _rebuildQueued;
        private int _activePointer = -1;
        private VisualElement _activeSlot;
        private Vector3 _pressPosition;

        /// <param name="parent">Element the panel fills. Normally the UIDocument root.</param>
        /// <param name="onSlotPress">Called when a slot is pressed. Returns true when placing started, which is when the slot takes the pointer.</param>
        /// <param name="onSlotRelease">Called when a drag that started on a slot ends. The arguments are whether the pointer is over the tray and whether it moved far enough to count as a drag.</param>
        /// <param name="onTrayPress">Called when the tray background is pressed.</param>
        /// <param name="closeKeyLabel">Name of the key that leaves build mode, shown in the hint line.</param>
        public ManagedBuildPanel(
            VisualElement parent,
            IManagedBuildStock stock,
            ManagedBlueprintBook blueprints,
            ManagedPlacementController controller,
            Func<ManagedPlacementSource, string, bool> onSlotPress,
            Action<bool, bool> onSlotRelease,
            Action onTrayPress,
            string closeKeyLabel)
        {
            _stock = stock;
            _blueprints = blueprints;
            _controller = controller;
            _onSlotPress = onSlotPress;
            _onSlotRelease = onSlotRelease;
            _onTrayPress = onTrayPress;
            _closeKeyLabel = closeKeyLabel;

            // Full screen container. It ignores the pointer, so only the tray takes clicks.
            _root = new VisualElement { name = "gtg-build-root", pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.justifyContent = Justify.FlexEnd;
            _root.style.alignItems = Align.Center;
            parent.Add(_root);

            _tray = new VisualElement { name = "gtg-build-tray" };
            _tray.style.flexDirection = FlexDirection.Column;
            _tray.style.marginBottom = 18;
            _tray.style.paddingTop = 10;
            _tray.style.paddingBottom = 10;
            _tray.style.paddingLeft = 12;
            _tray.style.paddingRight = 12;
            _tray.style.minWidth = 340;
            _tray.style.backgroundColor = PanelColor;
            SetRadius(_tray, 10);
            SetBorder(_tray, 1, EdgeColor);
            _tray.RegisterCallback<PointerDownEvent>(OnTrayPointerDown);
            _root.Add(_tray);

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;
            _tray.Add(header);

            _inventoryTab = MakeTab("Inventory", Tab.Inventory);
            _blueprintTab = MakeTab("Blueprints", Tab.Blueprints);
            header.Add(_inventoryTab);
            header.Add(_blueprintTab);

            _slotRow = new VisualElement();
            _slotRow.style.flexDirection = FlexDirection.Row;
            _slotRow.style.minHeight = 92;
            _tray.Add(_slotRow);

            _status = MakeLabel(string.Empty, 13, TextColor, FontStyle.Bold);
            _status.style.marginTop = 8;
            _status.style.minHeight = 18;
            _tray.Add(_status);

            _hint = MakeLabel(string.Empty, 11, DimTextColor, FontStyle.Normal);
            _hint.style.marginTop = 2;
            _tray.Add(_hint);

            _stock.Changed += QueueRebuild;
            _blueprints.Changed += QueueRebuild;
            _controller.StateChanged += OnControllerStateChanged;
            _controller.ResultChanged += OnControllerResultChanged;

            SetVisible(false);
            Rebuild();
            RefreshTexts();
        }

        public bool Visible
        {
            get { return _visible; }
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (!visible)
            {
                // A hidden tray must not keep the pointer, or its slots stay locked out until the next release.
                ReleaseCapture();
            }

            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                Rebuild();
                RefreshTexts();
            }
        }

        /// <summary>Shows a one-off message in the status line, for instance why a slot refused. The next state change replaces it.</summary>
        public void ShowNotice(string text)
        {
            _status.text = text;
            _status.style.color = ErrorColor;
        }

        /// <summary>True when the screen position is over the tray. Screen pixels, origin at the bottom left.</summary>
        public bool IsOverTray(Vector2 screenPixels)
        {
            if (!_visible || _root.panel == null)
            {
                return false;
            }

            // The panel counts from the top left, the screen from the bottom left.
            Vector2 p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screenPixels.x, Screen.height - screenPixels.y));
            return _tray.worldBound.Contains(p);
        }

        public void Dispose()
        {
            _stock.Changed -= QueueRebuild;
            _blueprints.Changed -= QueueRebuild;
            _controller.StateChanged -= OnControllerStateChanged;
            _controller.ResultChanged -= OnControllerResultChanged;
            _root.RemoveFromHierarchy();
        }

        private void QueueRebuild()
        {
            // A rebuild removes the slots. Doing it from inside a pointer event of one of them would
            // pull the element out from under its own handler, so it waits for the next scheduler tick.
            if (_rebuildQueued)
            {
                return;
            }

            _rebuildQueued = true;
            _root.schedule.Execute(() =>
            {
                _rebuildQueued = false;
                Rebuild();
            });
        }

        private void Rebuild()
        {
            _slotRow.Clear();
            _slots.Clear();

            if (_tab == Tab.Inventory)
            {
                _entries.Clear();
                _stock.CopyEntries(_entries);
                for (int i = 0; i < _entries.Count; i++)
                {
                    ManagedBuildableDefinition def;
                    if (ManagedBuildableCatalog.TryGet(_entries[i].ItemId, out def))
                    {
                        AddSlot(def, ManagedPlacementSource.Stock, "x" + _entries[i].Count);
                    }
                }

                if (_slots.Count == 0)
                {
                    _slotRow.Add(MakeLabel("No stations in your inventory", 12, DimTextColor, FontStyle.Italic));
                }
            }
            else
            {
                for (int i = 0; i < ManagedBuildableCatalog.Count; i++)
                {
                    ManagedBuildableDefinition def = ManagedBuildableCatalog.Get(i);
                    if (_blueprints.IsAvailable(def))
                    {
                        AddSlot(def, ManagedPlacementSource.Blueprint, DescribeCost(def));
                    }
                }

                if (_slots.Count == 0)
                {
                    _slotRow.Add(MakeLabel("No blueprints unlocked", 12, DimTextColor, FontStyle.Italic));
                }
            }

            StyleTab(_inventoryTab, _tab == Tab.Inventory);
            StyleTab(_blueprintTab, _tab == Tab.Blueprints);
            RefreshSelection();
        }

        private void AddSlot(ManagedBuildableDefinition def, ManagedPlacementSource source, string info)
        {
            var slot = new VisualElement();
            slot.style.width = 112;
            slot.style.height = 88;
            slot.style.marginRight = 8;
            slot.style.flexDirection = FlexDirection.Column;
            slot.style.alignItems = Align.Center;
            slot.style.justifyContent = Justify.Center;
            slot.style.backgroundColor = SlotColor;
            SetRadius(slot, 8);
            SetBorder(slot, 2, EdgeColor);

            var swatch = new VisualElement { pickingMode = PickingMode.Ignore };
            swatch.style.width = 34;
            swatch.style.height = 34;
            swatch.style.backgroundColor = def.Color;
            SetRadius(swatch, 5);
            slot.Add(swatch);

            Label name = MakeLabel(def.DisplayName, 11, TextColor, FontStyle.Bold);
            name.style.marginTop = 5;
            name.style.whiteSpace = WhiteSpace.Normal;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            slot.Add(name);

            Label count = MakeLabel(info, 11, DimTextColor, FontStyle.Normal);
            slot.Add(count);

            string id = def.Id;
            slot.RegisterCallback<PointerDownEvent>(evt => OnSlotPointerDown(evt, slot, source, id));
            slot.RegisterCallback<PointerUpEvent>(OnSlotPointerUp);
            slot.RegisterCallback<PointerCaptureOutEvent>(OnSlotCaptureOut);

            _slotRow.Add(slot);
            _slots.Add(new SlotView { Element = slot, Id = id, Source = source });
        }

        private void OnSlotPointerDown(PointerDownEvent evt, VisualElement slot, ManagedPlacementSource source, string id)
        {
            // Stopping propagation keeps the tray background handler from also seeing this press.
            evt.StopPropagation();
            if (evt.button != 0 || _activePointer >= 0)
            {
                return;
            }

            if (!_onSlotPress(source, id))
            {
                return;
            }

            _activePointer = evt.pointerId;
            _activeSlot = slot;
            _pressPosition = evt.position;
            slot.CapturePointer(evt.pointerId);
        }

        private void OnSlotPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _activePointer)
            {
                return;
            }

            evt.StopPropagation();
            FinishDrag(evt.position, false);
        }

        private void OnSlotCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId != _activePointer)
            {
                return;
            }

            // The pointer was taken away without a release, for instance by a focus loss. Treat it as a cancelled drag.
            FinishDrag(Vector3.zero, true);
        }

        // Clears the active drag first, so the capture-out event this triggers is ignored by its handler.
        private void ReleaseCapture()
        {
            int pointer = _activePointer;
            VisualElement slot = _activeSlot;
            _activePointer = -1;
            _activeSlot = null;

            if (slot != null && pointer >= 0 && slot.HasPointerCapture(pointer))
            {
                slot.ReleasePointer(pointer);
            }
        }

        private void FinishDrag(Vector3 position, bool interrupted)
        {
            ReleaseCapture();

            if (interrupted)
            {
                _onSlotRelease(true, true);
                return;
            }

            bool overTray = _tray.worldBound.Contains(new Vector2(position.x, position.y));
            bool dragged = (position - _pressPosition).magnitude > DragThresholdPixels;
            _onSlotRelease(overTray, dragged);
        }

        private void OnTrayPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0)
            {
                _onTrayPress();
            }
        }

        private void OnControllerStateChanged()
        {
            RefreshSelection();
            RefreshTexts();
        }

        private void OnControllerResultChanged(ManagedPlacementResult result)
        {
            RefreshTexts();
        }

        // Updates the existing slots in place. A rebuild here would remove the slot that is being pressed.
        private void RefreshSelection()
        {
            string selected = _controller.Mode == ManagedPlacementMode.Placing
                && _controller.Source != ManagedPlacementSource.Carried
                && _controller.Definition != null
                    ? _controller.Definition.Id
                    : null;

            for (int i = 0; i < _slots.Count; i++)
            {
                bool isSelected = selected != null && _slots[i].Id == selected && _slots[i].Source == _controller.Source;
                SetBorder(_slots[i].Element, 2, isSelected ? AccentColor : EdgeColor);
            }
        }

        private void RefreshTexts()
        {
            if (_controller.Mode != ManagedPlacementMode.Placing)
            {
                _status.text = string.Empty;
                _hint.text = "Drag a station into the world, or click it and then click the ground.  Click a placed station to pick it up.  "
                    + _closeKeyLabel + ": close";
                return;
            }

            ManagedPlacementResult r = _controller.Result;
            _status.text = r.IsValid ? "Ready to place" : ManagedPlacementValidator.Describe(r.Status);
            _status.style.color = r.IsValid ? AccentColor : ErrorColor;

            _hint.text = _controller.Source == ManagedPlacementSource.Carried
                ? "Click: put it down.  R or wheel: rotate.  Right click or Esc: put it back.  Click the tray: keep it in your inventory"
                : "Click: place.  R or wheel: rotate.  Right click or Esc: cancel";
        }

        private Label MakeTab(string text, Tab tab)
        {
            Label label = MakeLabel(text, 13, DimTextColor, FontStyle.Bold);

            // Labels ignore the pointer by default. A tab has to take the press itself.
            label.pickingMode = PickingMode.Position;
            label.style.marginRight = 14;
            label.style.paddingTop = 2;
            label.style.paddingBottom = 2;
            label.RegisterCallback<PointerDownEvent>(evt =>
            {
                evt.StopPropagation();
                if (evt.button == 0 && _tab != tab)
                {
                    _tab = tab;
                    Rebuild();
                }
            });
            return label;
        }

        private static string DescribeCost(ManagedBuildableDefinition def)
        {
            if (def.Cost.Length == 0)
            {
                return "Free";
            }

            var parts = new string[def.Cost.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = def.Cost[i].Count + " x " + def.Cost[i].ItemId;
            }

            return string.Join(", ", parts);
        }

        private static void StyleTab(Label tab, bool active)
        {
            tab.style.color = active ? TextColor : DimTextColor;
            tab.style.borderBottomWidth = 2;
            tab.style.borderBottomColor = active ? AccentColor : new Color(0f, 0f, 0f, 0f);
        }

        private static Label MakeLabel(string text, float size, Color color, FontStyle fontStyle)
        {
            var label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = fontStyle;
            return label;
        }

        private static void SetBorder(VisualElement e, float width, Color color)
        {
            e.style.borderTopWidth = width;
            e.style.borderRightWidth = width;
            e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width;
            e.style.borderTopColor = color;
            e.style.borderRightColor = color;
            e.style.borderBottomColor = color;
            e.style.borderLeftColor = color;
        }

        private static void SetRadius(VisualElement e, float radius)
        {
            e.style.borderTopLeftRadius = radius;
            e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius;
            e.style.borderBottomRightRadius = radius;
        }
    }
}
