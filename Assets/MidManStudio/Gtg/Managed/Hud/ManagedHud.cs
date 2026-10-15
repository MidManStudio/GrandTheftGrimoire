// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHud.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Camera;
using MidManStudio.Gtg.Managed.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// The player's HUD, drawn with uGUI. Add it to one GameObject in the scene. At play start it
    /// builds a canvas with the missions panel, the compass bar, heat crowns and coins, the health
    /// and vessel bars, the quick slots, and the corner button of the item wheel. Every piece uses
    /// a drawn sprite from <c>Resources/GtgHud</c> when one exists and a flat placeholder when it
    /// does not. Health, vessel charge, coins and heading are read from the player each frame.
    /// The bars and the quick slots hide while build mode is open, because its tray sits there.
    /// Missions, heat and the wheel's contents have no system behind them yet, so they are set
    /// through <see cref="SetMissions"/>, <see cref="SetHeat"/> and <see cref="Wheel"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedHud : MonoBehaviour
    {
        /// <summary>The first number key of the quick slots. The belt of spells uses 1, 2 and 3.</summary>
        public const int FirstQuickKey = 4;

        private const float PulseSeconds = 0.18f;
        private const float CompassRedrawDegrees = 0.01f;

        private static ManagedHud _instance;

        [Tooltip("Draw order of the HUD canvas. Higher draws on top of other canvases.")]
        [SerializeField] private int _sortingOrder = 10;

        [Tooltip("Adds a ManagedPlayerItemBag to the player when it has none, so the quick slots have a bag to read.")]
        [SerializeField] private bool _createPlayerBag = true;

        private readonly ManagedHudState _state = new ManagedHudState();
        private readonly ManagedHudWheel _wheel = new ManagedHudWheel();
        private readonly ManagedCursorBlock _wheelBlock = new ManagedCursorBlock();
        private readonly ManagedCursorBlock _hoverBlock = new ManagedCursorBlock();
        private ManagedHudFeed _feed;
        private Canvas _canvas;

        // Missions and compass.
        private ManagedHudText _missionTitle;
        private ManagedHudText _missionBody;
        private bool _missionsDirty = true;
        private CompassMark[] _marks;
        private float _drawnYaw = float.NaN;

        // Status.
        private GameObject _crowns;
        private Image[] _crownLit;
        private int _drawnHeat = -1;
        private ManagedHudText _goldText;
        private ManagedHudText _silverText;
        private long _drawnGold = -1;
        private int _drawnSilver = -1;

        // Bars.
        // The bars and the quick slots share one parent, so they hide together.
        private GameObject _bottomGroup;
        private bool _bottomHidden;
        private Image _hpFill;
        private Image _spFill;
        private ManagedHudText _hpText;
        private float _drawnHp = -1f;
        private float _drawnHpMax = -1f;
        private float _drawnSp = -1f;

        // Quick slots.
        private SlotWidget[] _slots;
        private ManagedQuickSlots _boundQuick;
        private Image _ghost;

        // Wheel.
        private Image _wheelButtonIcon;
        private ManagedHudText _wheelButtonName;
        private GameObject _wheelRoot;
        private Image[] _wheelHighlights;
        private Image[] _wheelIcons;
        private ManagedHudText[] _wheelNames;
        private bool _wheelDirty = true;
        private bool _wheelOpen;
        private bool _wheelWasLocked;
        private int _hover = -1;

        private struct CompassMark
        {
            public float Bearing;
            public RectTransform Rect;
            public Image Tick;
            public ManagedHudText Letter;
        }

        private sealed class SlotWidget
        {
            public Image Frame;
            public Color BaseColor;
            public Image Icon;
            public ManagedHudText Name;
            public ManagedHudText Amount;
            public bool Filled;
            public int ItemId;
            public int Count = -1;
            public float PulseUntil;
        }

        /// <summary>Raised when a quick slot is used, with the slot and the item. The HUD does not decide what using does.</summary>
        public event Action<int, int> QuickSlotUsed;

        /// <summary>What the HUD shows. The feed writes it every frame.</summary>
        public ManagedHudState State { get { return _state; } }

        /// <summary>
        /// The sections of the item wheel. Fill them with whatever the player can make active, and
        /// listen for <see cref="ManagedHudWheel.Picked"/> to make the picked thing active.
        /// </summary>
        public ManagedHudWheel Wheel { get { return _wheel; } }

        public bool IsWheelOpen { get { return _wheelOpen; } }

        /// <summary>The player's bag component, or null until the player is found.</summary>
        public ManagedPlayerItemBag PlayerBag { get { return _feed != null ? _feed.PlayerBag : null; } }

        /// <summary>The player's bag, or null.</summary>
        public ManagedItemBag Bag
        {
            get
            {
                ManagedPlayerItemBag player = PlayerBag;
                return player != null ? player.Bag : null;
            }
        }

        /// <summary>The player's quick slots, or null until the player has a bag.</summary>
        public ManagedQuickSlots QuickSlots
        {
            get
            {
                ManagedPlayerItemBag player = PlayerBag;
                return player != null ? player.QuickSlots : null;
            }
        }

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        /// <summary>
        /// Sets how many of the nine heat sections are filled. The crowns show while it is above 0
        /// and fill crown by crown. Nothing drives heat yet, so whatever makes the player wanted
        /// calls this.
        /// </summary>
        public void SetHeat(int sections)
        {
            _state.SetHeat(sections);
        }

        /// <summary>Sets the missions panel. See <see cref="ManagedHudState.SetMissions"/> for the limits.</summary>
        public void SetMissions(string title, IList<string> lines)
        {
            _state.SetMissions(title, lines);
            _missionsDirty = true;
        }

        public void ToggleWheel()
        {
            if (_wheelOpen)
            {
                CloseWheel();
            }
            else
            {
                OpenWheel();
            }
        }

        /// <summary>
        /// Opens the wheel over the middle of the screen. The cursor is freed so a section can be
        /// clicked, and the camera rig is kept from taking it back until the wheel closes.
        /// </summary>
        public void OpenWheel()
        {
            if (_wheelOpen || _wheelRoot == null)
            {
                return;
            }

            _wheelOpen = true;
            _wheelRoot.SetActive(true);
            _wheelWasLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _wheelBlock.Set(true);
        }

        /// <summary>
        /// Closes the wheel and lifts its cursor block. The cursor goes back to locked only when
        /// the wheel took it and no other panel still holds the block.
        /// </summary>
        public void CloseWheel()
        {
            if (!_wheelOpen)
            {
                return;
            }

            _wheelOpen = false;
            if (_wheelRoot != null)
            {
                _wheelRoot.SetActive(false);
            }

            SetHover(-1);
            _wheelBlock.Set(false);
            if (_wheelWasLocked && !ManagedCursorBlock.Active)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// Uses the item in a quick slot, which raises <see cref="QuickSlotUsed"/> when the slot
        /// holds an item the player carries. Returns false otherwise.
        /// </summary>
        public bool UseQuickSlot(int slot)
        {
            ManagedQuickSlots quick = QuickSlots;
            ManagedItemBag bag = Bag;
            return quick != null && bag != null && quick.TryUse(slot, bag);
        }

        /// <summary>Shows the item being dragged under the pointer. A screen position is in pixels, y up.</summary>
        public void ShowGhost(int itemId, Vector2 screenPosition)
        {
            if (_ghost == null)
            {
                return;
            }

            Sprite icon = ManagedHudSprites.LoadIcon(ManagedHudSprites.ItemIconKey(itemId));
            _ghost.sprite = icon != null ? icon : ManagedHudSprites.Disc;
            _ghost.color = icon != null ? Color.white : ManagedHudStyle.SlotPulse;
            _ghost.gameObject.SetActive(true);
            MoveGhost(screenPosition);
        }

        public void MoveGhost(Vector2 screenPosition)
        {
            if (_ghost != null)
            {
                ((RectTransform)_ghost.transform).anchoredPosition = ScreenToCanvas(screenPosition);
            }
        }

        public void HideGhost()
        {
            if (_ghost != null)
            {
                _ghost.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("More than one ManagedHud. Disabling the extra one on " + name + ".");
                enabled = false;
                return;
            }

            _instance = this;
        }

        private void OnDisable()
        {
            if (_instance != this)
            {
                return;
            }

            CloseWheel();
            _hoverBlock.Set(false);
            UnbindQuick();
            HideGhost();
            _instance = null;
        }

        private void Start()
        {
            _feed = new ManagedHudFeed(_createPlayerBag);
            _state.SetMissions("Missions", new[] { "No active mission" });
            _wheel.Changed += OnWheelChanged;
            _wheel.Picked += OnWheelPicked;
            Build();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<ManagedHudDevKeys>().Bind(this);
#endif
        }

        private void Update()
        {
            if (_canvas == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null)
            {
                if (keyboard.qKey.wasPressedThisFrame)
                {
                    ToggleWheel();
                }

                // A hidden slot must not be used by its key.
                if (!_wheelOpen && !_bottomHidden)
                {
                    PollQuickKeys(keyboard);
                }
            }

            if (_wheelOpen)
            {
                PollWheel(mouse);
            }

            UpdateHoverBlock();
        }

        private void LateUpdate()
        {
            if (_canvas == null)
            {
                return;
            }

            _feed.Refresh(_state, Time.unscaledTime);
            ApplyBottomGroup();
            BindQuick();

            RefreshMissions();
            RefreshCompass();
            RefreshStatus();
            RefreshBars();
            RefreshSlots();
            RefreshWheel();
        }

        // Shows or hides the bars and the quick slots to match the state. A drag that started on a
        // slot cannot end once the slot is inactive, so it is dropped here.
        private void ApplyBottomGroup()
        {
            bool hide = _state.BottomHidden;
            if (hide == _bottomHidden)
            {
                return;
            }

            _bottomHidden = hide;
            if (hide && ManagedItemDrag.Active && ManagedItemDrag.Payload.Origin == ManagedItemDragOrigin.QuickSlot)
            {
                ManagedItemDrag.Reset();
                HideGhost();
            }

            _bottomGroup.SetActive(!hide);
        }

        // ---- input --------------------------------------------------------------------------------

        private void PollQuickKeys(Keyboard keyboard)
        {
            var keys = new[]
            {
                keyboard.digit4Key, keyboard.digit5Key, keyboard.digit6Key, keyboard.digit7Key, keyboard.digit8Key,
            };

            for (int i = 0; i < keys.Length && i < ManagedHudLayout.QuickSlotCount; i++)
            {
                if (keys[i].wasPressedThisFrame)
                {
                    UseQuickSlot(i);
                }
            }
        }

        // The wheel is read from the mouse directly, so a click on a section is judged by the same
        // hit test the tests use and never by the shape of a graphic.
        private void PollWheel(Mouse mouse)
        {
            if (mouse == null)
            {
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            float dx, dy;
            WheelLocal(position, out dx, out dy);
            int section = ManagedHudMath.WheelSectionAt(dx, dy, _wheel.SectionCount, ManagedHudLayout.WheelDeadRadius, ManagedHudLayout.WheelOuterRadius);
            ManagedWheelEntry entry;
            SetHover(section >= 0 && _wheel.TryGetEntry(section, out entry) ? section : -1);

            if (mouse.rightButton.wasPressedThisFrame)
            {
                CloseWheel();
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (section >= 0)
            {
                // A section with nothing in it does nothing, and the wheel stays open.
                if (_wheel.Pick(section))
                {
                    CloseWheel();
                }
            }
            else if (dx * dx + dy * dy > ManagedHudLayout.WheelOuterRadius * ManagedHudLayout.WheelOuterRadius)
            {
                CloseWheel();
            }
        }

        // While the cursor is free, a pointer over a clickable HUD piece keeps the camera rig from
        // capturing the cursor on that click.
        private void UpdateHoverBlock()
        {
            bool free = Cursor.lockState != CursorLockMode.Locked;
            EventSystem system = EventSystem.current;
            _hoverBlock.Set(free && system != null && system.IsPointerOverGameObject());
        }

        private void WheelLocal(Vector2 screen, out float dx, out float dy)
        {
            Vector2 local = ScreenToCanvas(screen);
            dx = local.x;
            dy = local.y;
        }

        // The wheel and the drag ghost sit on the middle of the canvas, so a screen point maps to
        // canvas units by the canvas scale around the middle of the screen.
        private Vector2 ScreenToCanvas(Vector2 screen)
        {
            float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            return new Vector2((screen.x - Screen.width * 0.5f) / scale, (screen.y - Screen.height * 0.5f) / scale);
        }

        private void SetHover(int section)
        {
            if (_hover == section)
            {
                return;
            }

            _hover = section;
            if (_wheelHighlights == null)
            {
                return;
            }

            for (int i = 0; i < _wheelHighlights.Length; i++)
            {
                _wheelHighlights[i].enabled = i == section;
            }
        }

        // ---- events -------------------------------------------------------------------------------

        private void OnWheelChanged()
        {
            _wheelDirty = true;
        }

        private void OnWheelPicked(int section)
        {
            _wheelDirty = true;
        }

        private void BindQuick()
        {
            ManagedQuickSlots quick = QuickSlots;
            if (ReferenceEquals(quick, _boundQuick))
            {
                return;
            }

            UnbindQuick();
            _boundQuick = quick;
            if (quick != null)
            {
                quick.Used += OnQuickUsed;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Count = -1;
            }
        }

        private void UnbindQuick()
        {
            if (_boundQuick != null)
            {
                _boundQuick.Used -= OnQuickUsed;
                _boundQuick = null;
            }
        }

        private void OnQuickUsed(int slot, int itemId)
        {
            if (_slots != null && slot >= 0 && slot < _slots.Length)
            {
                _slots[slot].PulseUntil = Time.unscaledTime + PulseSeconds;
            }

            Action<int, int> used = QuickSlotUsed;
            if (used != null)
            {
                used(slot, itemId);
            }
        }

        // ---- refresh ------------------------------------------------------------------------------

        private void RefreshMissions()
        {
            if (!_missionsDirty)
            {
                return;
            }

            _missionsDirty = false;
            _missionTitle.Text = _state.MissionTitle;
            var body = new System.Text.StringBuilder();
            for (int i = 0; i < _state.MissionLineCount; i++)
            {
                if (i > 0)
                {
                    body.Append('\n');
                }

                body.Append(_state.MissionLineAt(i));
            }

            _missionBody.Text = body.ToString();
        }

        private void RefreshCompass()
        {
            float yaw = _state.YawDegrees;
            if (!float.IsNaN(_drawnYaw) && Mathf.Abs(yaw - _drawnYaw) < CompassRedrawDegrees)
            {
                return;
            }

            _drawnYaw = yaw;
            float half = ManagedHudLayout.Compass.Width * 0.5f;
            for (int i = 0; i < _marks.Length; i++)
            {
                float offset = ManagedHudMath.CompassOffset(yaw, _marks[i].Bearing, ManagedHudLayout.CompassPixelsPerDegree);
                float alpha = ManagedHudMath.CompassAlpha(offset, half, ManagedHudLayout.CompassFadePixels);
                _marks[i].Rect.anchoredPosition = new Vector2(offset, 0f);

                if (_marks[i].Tick != null)
                {
                    Color c = ManagedHudStyle.CompassMark;
                    _marks[i].Tick.color = new Color(c.r, c.g, c.b, alpha * 0.6f);
                }
                else
                {
                    Color c = ManagedHudStyle.Text;
                    _marks[i].Letter.Color = new Color(c.r, c.g, c.b, alpha);
                }
            }
        }

        private void RefreshStatus()
        {
            int heat = _state.HeatSections;
            if (heat != _drawnHeat)
            {
                _drawnHeat = heat;
                _crowns.SetActive(_state.Wanted);
                for (int i = 0; i < _crownLit.Length; i++)
                {
                    _crownLit[i].fillAmount = ManagedHudMath.CrownFill(ManagedHudMath.CrownSections(heat, i));
                }
            }

            if (_state.Gold != _drawnGold)
            {
                _drawnGold = _state.Gold;
                _goldText.Text = _drawnGold.ToString();
            }

            if (_state.Silver != _drawnSilver)
            {
                _drawnSilver = _state.Silver;
                _silverText.Text = _drawnSilver.ToString();
            }
        }

        private void RefreshBars()
        {
            if (_state.HpFraction != _drawnHp || _state.HpMax != _drawnHpMax)
            {
                _drawnHp = _state.HpFraction;
                _drawnHpMax = _state.HpMax;
                _hpFill.fillAmount = _drawnHp;
                _hpText.Text = _state.HpMax > 0f
                    ? Mathf.CeilToInt(_state.HpCurrent) + " / " + Mathf.RoundToInt(_state.HpMax)
                    : string.Empty;
            }

            if (_state.SpFraction != _drawnSp)
            {
                _drawnSp = _state.SpFraction;
                _spFill.fillAmount = _drawnSp;
            }
        }

        private void RefreshSlots()
        {
            ManagedQuickSlots quick = _boundQuick;
            ManagedItemBag bag = Bag;
            float now = Time.unscaledTime;

            for (int i = 0; i < _slots.Length; i++)
            {
                SlotWidget slot = _slots[i];
                int itemId = 0;
                bool filled = quick != null && quick.TryGetItem(i, out itemId);
                int count = filled && bag != null ? bag.CountOf(itemId) : 0;

                if (filled != slot.Filled || itemId != slot.ItemId || count != slot.Count)
                {
                    slot.Filled = filled;
                    slot.ItemId = itemId;
                    slot.Count = count;
                    DrawSlot(slot, filled, itemId, count);
                }

                float pulse = slot.PulseUntil > now ? (slot.PulseUntil - now) / PulseSeconds : 0f;
                slot.Frame.color = Color.Lerp(slot.BaseColor, ManagedHudStyle.SlotPulse, pulse);
            }
        }

        private void DrawSlot(SlotWidget slot, bool filled, int itemId, int count)
        {
            if (!filled)
            {
                slot.Icon.enabled = false;
                slot.Name.Text = string.Empty;
                slot.Amount.Text = string.Empty;
                return;
            }

            Sprite icon = ManagedHudSprites.LoadIcon(ManagedHudSprites.ItemIconKey(itemId));
            bool carried = count > 0;
            if (icon != null)
            {
                slot.Icon.sprite = icon;
                slot.Icon.color = carried ? ManagedHudStyle.IconNormal : ManagedHudStyle.IconSpent;
                slot.Icon.enabled = true;
                slot.Name.Text = string.Empty;
            }
            else
            {
                slot.Icon.enabled = false;
                slot.Name.Text = ItemName(itemId);
                slot.Name.Color = carried ? ManagedHudStyle.Text : ManagedHudStyle.TextMuted;
            }

            slot.Amount.Text = count.ToString();
        }

        private string ItemName(int itemId)
        {
            ManagedPlayerItemBag player = PlayerBag;
            ManagedItemInfo info;
            if (player != null && player.Catalog != null && player.Catalog.TryGet(itemId, out info))
            {
                return info.Name;
            }

            return "item " + itemId;
        }

        private void RefreshWheel()
        {
            if (!_wheelDirty)
            {
                return;
            }

            _wheelDirty = false;
            for (int i = 0; i < _wheelNames.Length; i++)
            {
                ManagedWheelEntry entry;
                bool filled = _wheel.TryGetEntry(i, out entry);
                Sprite icon = filled ? ManagedHudSprites.LoadIcon(entry.IconKey) : null;
                _wheelIcons[i].sprite = icon;
                _wheelIcons[i].enabled = icon != null;
                _wheelNames[i].Text = filled ? entry.Label : string.Empty;
                _wheelNames[i].Color = i == _wheel.Active ? ManagedHudStyle.TextActive : ManagedHudStyle.Text;
            }

            ManagedWheelEntry active;
            if (_wheel.TryGetEntry(_wheel.Active, out active))
            {
                Sprite icon = ManagedHudSprites.LoadIcon(active.IconKey);
                _wheelButtonIcon.sprite = icon;
                _wheelButtonIcon.enabled = icon != null;
                _wheelButtonName.Text = icon != null ? string.Empty : active.Label;
            }
            else
            {
                _wheelButtonIcon.enabled = false;
                _wheelButtonName.Text = "Wheel";
            }
        }

        // ---- building -----------------------------------------------------------------------------

        private void Build()
        {
            ManagedHudBuilder.EnsureEventSystem();
            _canvas = ManagedHudBuilder.NewCanvas(transform, _sortingOrder);
            Transform root = _canvas.transform;

            BuildMissions(root);
            BuildCompass(root);
            BuildStatus(root);

            // One stretched parent for the bars and the slots. It has no graphic, so it takes no clicks.
            _bottomGroup = ManagedHudBuilder.NewStretch(root, "Bottom Group").gameObject;
            BuildBars(_bottomGroup.transform);
            BuildSlots(_bottomGroup.transform);
            BuildWheelButton(root);
            BuildWheelOverlay(root);
            BuildGhost(root);
        }

        private void BuildMissions(Transform root)
        {
            Image panel = ManagedHudBuilder.NewImage(root, "Missions", ManagedHudLayout.Missions, ManagedHudSprites.FrameMissions,
                ManagedHudSprites.White, ManagedHudStyle.PanelBack, false);
            _missionTitle = ManagedHudText.Create(panel.transform, "Missions Title", ManagedHudLayout.MissionsTitle,
                ManagedHudLayout.FontTitle, ManagedHudAlign.Left, ManagedHudStyle.TextActive);
            _missionBody = ManagedHudText.Create(panel.transform, "Missions Body", ManagedHudLayout.MissionsBody,
                ManagedHudLayout.FontBody, ManagedHudAlign.TopLeft, ManagedHudStyle.Text);
        }

        private void BuildCompass(Transform root)
        {
            Image bar = ManagedHudBuilder.NewImage(root, "Compass", ManagedHudLayout.Compass, ManagedHudSprites.CompassBar,
                ManagedHudSprites.White, ManagedHudStyle.CompassBack, false);

            // The marks sit in a clipped child, so one that slides past the end of the bar is cut off.
            RectTransform marks = ManagedHudBuilder.NewStretch(bar.transform, "Marks");
            marks.gameObject.AddComponent<RectMask2D>();

            string[] letters = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            int count = 360 / ManagedHudLayout.CompassTickDegrees;
            _marks = new CompassMark[count];
            for (int i = 0; i < count; i++)
            {
                int bearing = i * ManagedHudLayout.CompassTickDegrees;
                bool letter = bearing % ManagedHudLayout.CompassLabelDegrees == 0;
                var mark = new CompassMark { Bearing = bearing };
                if (letter)
                {
                    var rect = new ManagedHudRect(0.5f, 0.5f, 0f, 0f, 64f, 36f);
                    mark.Letter = ManagedHudText.Create(marks, "Mark " + bearing, rect, ManagedHudLayout.FontBody, ManagedHudAlign.Center, ManagedHudStyle.Text);
                    mark.Letter.Text = letters[bearing / ManagedHudLayout.CompassLabelDegrees];
                    mark.Rect = (RectTransform)mark.Letter.GameObject.transform;
                }
                else
                {
                    var rect = new ManagedHudRect(0.5f, 0.5f, 0f, 0f, 3f, 14f);
                    mark.Tick = ManagedHudBuilder.NewImage(marks, "Mark " + bearing, rect, null, ManagedHudSprites.White, ManagedHudStyle.CompassMark, false);
                    mark.Rect = (RectTransform)mark.Tick.transform;
                }

                _marks[i] = mark;
            }

            ManagedHudBuilder.NewImage(bar.transform, "Compass Marker", new ManagedHudRect(0.5f, 0f, 0f, 0f, 12f, 10f),
                ManagedHudSprites.CompassMarker, ManagedHudSprites.White, ManagedHudStyle.CompassMarker, false);
        }

        private void BuildStatus(Transform root)
        {
            Image panel = ManagedHudBuilder.NewImage(root, "Status", ManagedHudLayout.Status, ManagedHudSprites.FrameStatus,
                ManagedHudSprites.White, ManagedHudStyle.PanelBack, false);

            // The crowns are hidden until the player is wanted.
            _crowns = ManagedHudBuilder.NewStretch(panel.transform, "Crowns").gameObject;
            _crownLit = new Image[ManagedHudMath.CrownCount];
            for (int i = 0; i < ManagedHudMath.CrownCount; i++)
            {
                ManagedHudRect rect = ManagedHudLayout.Crown(i);
                Image dim = ManagedHudBuilder.NewImage(_crowns.transform, "Crown " + i, rect, ManagedHudSprites.CrownDim,
                    ManagedHudSprites.White, ManagedHudStyle.CrownDim, false);

                // The lit crown is the same size over the dim one and shows its first thirds.
                Image lit = ManagedHudBuilder.StyleImage(NewFill(dim.transform, "Lit"), ManagedHudSprites.CrownLit,
                    ManagedHudSprites.White, ManagedHudStyle.CrownLit, false);
                lit.fillMethod = Image.FillMethod.Horizontal;
                lit.fillOrigin = (int)Image.OriginHorizontal.Left;
                lit.fillAmount = 0f;
                _crownLit[i] = lit;
            }

            _crowns.SetActive(false);

            ManagedHudBuilder.NewImage(panel.transform, "Gold Icon", ManagedHudLayout.GoldIcon, ManagedHudSprites.CoinGold,
                ManagedHudSprites.Disc, ManagedHudStyle.CoinGold, false);
            _goldText = ManagedHudText.Create(panel.transform, "Gold Text", ManagedHudLayout.GoldText, ManagedHudLayout.FontBody,
                ManagedHudAlign.Left, ManagedHudStyle.Text);
            ManagedHudBuilder.NewImage(panel.transform, "Silver Icon", ManagedHudLayout.SilverIcon, ManagedHudSprites.CoinSilver,
                ManagedHudSprites.Disc, ManagedHudStyle.CoinSilver, false);
            _silverText = ManagedHudText.Create(panel.transform, "Silver Text", ManagedHudLayout.SilverText, ManagedHudLayout.FontBody,
                ManagedHudAlign.Left, ManagedHudStyle.Text);
        }

        private void BuildBars(Transform root)
        {
            Image hp = ManagedHudBuilder.NewImage(root, "Hp Bar", ManagedHudLayout.HpBar, ManagedHudSprites.HpBack,
                ManagedHudSprites.White, ManagedHudStyle.BarBack, false);
            _hpFill = BarFill(hp.transform, ManagedHudSprites.HpFill, ManagedHudStyle.HpFill);
            _hpText = ManagedHudText.Create(hp.transform, "Hp Text", ManagedHudLayout.HpText, ManagedHudLayout.FontSmall,
                ManagedHudAlign.Center, ManagedHudStyle.Text);

            Image sp = ManagedHudBuilder.NewImage(root, "Sp Bar", ManagedHudLayout.SpBar, ManagedHudSprites.SpBack,
                ManagedHudSprites.White, ManagedHudStyle.BarBack, false);
            _spFill = BarFill(sp.transform, ManagedHudSprites.SpFill, ManagedHudStyle.SpFill);
        }

        private static Image BarFill(Transform parent, string spriteName, Color fallback)
        {
            Image fill = ManagedHudBuilder.StyleImage(NewFill(parent, "Fill"), spriteName, ManagedHudSprites.White, fallback, false);
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            return fill;
        }

        // An image that fills its parent and is drawn as a fill.
        private static Image NewFill(Transform parent, string name)
        {
            RectTransform rt = ManagedHudBuilder.NewStretch(parent, name);
            Image image = rt.gameObject.AddComponent<Image>();
            image.type = Image.Type.Filled;
            return image;
        }

        private void BuildSlots(Transform root)
        {
            _slots = new SlotWidget[ManagedHudLayout.QuickSlotCount];
            for (int i = 0; i < _slots.Length; i++)
            {
                RectTransform rt = ManagedHudBuilder.NewRect(root, "Quick Slot " + i, ManagedHudLayout.QuickSlot(i));
                Image frame = ManagedHudBuilder.StyleImage(rt.gameObject.AddComponent<Image>(), ManagedHudSprites.Slot,
                    ManagedHudSprites.White, ManagedHudStyle.Slot, true);
                rt.gameObject.AddComponent<ManagedHudSlotView>().Bind(this, i);

                var slot = new SlotWidget { Frame = frame, BaseColor = frame.color };
                slot.Icon = ManagedHudBuilder.NewImage(rt, "Icon", ManagedHudLayout.QuickSlotIcon, null, null, Color.white, false);
                slot.Icon.preserveAspect = true;
                slot.Icon.enabled = false;
                slot.Name = ManagedHudText.Create(rt, "Name", ManagedHudLayout.QuickSlotName, ManagedHudLayout.FontSmall,
                    ManagedHudAlign.Center, ManagedHudStyle.Text, true);
                slot.Amount = ManagedHudText.Create(rt, "Amount", ManagedHudLayout.QuickSlotAmount, ManagedHudLayout.FontSmall,
                    ManagedHudAlign.Right, ManagedHudStyle.Text);
                ManagedHudText key = ManagedHudText.Create(rt, "Key", ManagedHudLayout.QuickSlotKey, ManagedHudLayout.FontSmall,
                    ManagedHudAlign.Left, ManagedHudStyle.TextMuted);
                key.Text = (FirstQuickKey + i).ToString();
                _slots[i] = slot;
            }
        }

        private void BuildWheelButton(Transform root)
        {
            Image button = ManagedHudBuilder.NewImage(root, "Wheel Button", ManagedHudLayout.WheelButton, ManagedHudSprites.WheelButton,
                ManagedHudSprites.Disc, ManagedHudStyle.WheelButton, true);
            button.gameObject.AddComponent<ManagedHudClickView>().Clicked += ToggleWheel;

            _wheelButtonIcon = ManagedHudBuilder.NewImage(button.transform, "Active Icon", ManagedHudLayout.WheelButtonIcon, null, null, Color.white, false);
            _wheelButtonIcon.preserveAspect = true;
            _wheelButtonIcon.enabled = false;
            _wheelButtonName = ManagedHudText.Create(button.transform, "Active Name", ManagedHudLayout.WheelButtonName,
                ManagedHudLayout.FontBody, ManagedHudAlign.Center, ManagedHudStyle.Text, true);
            ManagedHudText key = ManagedHudText.Create(button.transform, "Key", ManagedHudLayout.WheelButtonKey,
                ManagedHudLayout.FontSmall, ManagedHudAlign.Center, ManagedHudStyle.TextMuted);
            key.Text = "Q";
        }

        private void BuildWheelOverlay(Transform root)
        {
            _wheelRoot = ManagedHudBuilder.NewStretch(root, "Wheel Overlay").gameObject;

            // The dimmer takes clicks, so nothing under the open wheel can be pressed.
            Image dimmer = _wheelRoot.AddComponent<Image>();
            dimmer.color = ManagedHudStyle.WheelDim;
            dimmer.raycastTarget = true;

            Image disc = ManagedHudBuilder.NewImage(_wheelRoot.transform, "Wheel Disc", ManagedHudLayout.Wheel, ManagedHudSprites.WheelRing,
                ManagedHudSprites.Disc, ManagedHudStyle.WheelBack, false);
            bool ringDrawn = ManagedHudSprites.Load(ManagedHudSprites.WheelRing) != null;

            int count = _wheel.SectionCount;
            _wheelHighlights = new Image[count];
            _wheelIcons = new Image[count];
            _wheelNames = new ManagedHudText[count];
            for (int i = 0; i < count; i++)
            {
                // The highlight covers the whole disc and shows one section. A drawn highlight is
                // the first of four sections and is turned for the others.
                Image highlight = NewFill(disc.transform, "Highlight " + i);
                bool drawn = ManagedHudSprites.Load(ManagedHudSprites.WheelHighlight) != null;
                ManagedHudBuilder.StyleImage(highlight, ManagedHudSprites.WheelHighlight, ManagedHudSprites.Disc, ManagedHudStyle.WheelHighlight, false);
                if (drawn)
                {
                    highlight.type = Image.Type.Simple;
                }
                else
                {
                    highlight.fillMethod = Image.FillMethod.Radial360;
                    highlight.fillOrigin = (int)Image.Origin360.Top;
                    highlight.fillClockwise = true;
                    highlight.fillAmount = 1f / count;
                }

                highlight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -360f * i / count);
                highlight.enabled = false;
                _wheelHighlights[i] = highlight;
            }

            if (!ringDrawn)
            {
                // The placeholder ring has a hole in the middle, the dead zone of the hit test.
                float hole = ManagedHudLayout.WheelDeadRadius * 2f;
                ManagedHudBuilder.NewImage(disc.transform, "Wheel Hub", new ManagedHudRect(0.5f, 0.5f, 0f, 0f, hole, hole), null,
                    ManagedHudSprites.Disc, ManagedHudStyle.WheelHub, false);
            }

            for (int i = 0; i < count; i++)
            {
                float x, y;
                ManagedHudMath.WheelSectionDirection(i, count, out x, out y);
                x *= ManagedHudLayout.WheelContentRadius;
                y *= ManagedHudLayout.WheelContentRadius;

                var iconRect = new ManagedHudRect(0.5f, 0.5f, x, y, ManagedHudLayout.WheelIconSize, ManagedHudLayout.WheelIconSize);
                _wheelIcons[i] = ManagedHudBuilder.NewImage(disc.transform, "Icon " + i, iconRect, null, null, Color.white, false);
                _wheelIcons[i].preserveAspect = true;
                _wheelIcons[i].enabled = false;

                var nameRect = new ManagedHudRect(0.5f, 0.5f, x, y - ManagedHudLayout.WheelNameDrop, ManagedHudLayout.WheelNameWidth, ManagedHudLayout.WheelNameHeight);
                _wheelNames[i] = ManagedHudText.Create(disc.transform, "Name " + i, nameRect, ManagedHudLayout.FontBody,
                    ManagedHudAlign.Center, ManagedHudStyle.Text, true);
            }

            _wheelRoot.SetActive(false);
        }

        private void BuildGhost(Transform root)
        {
            var rect = new ManagedHudRect(0.5f, 0.5f, 0f, 0f, ManagedHudLayout.QuickSlotIcon.Width, ManagedHudLayout.QuickSlotIcon.Height);
            _ghost = ManagedHudBuilder.NewImage(root, "Drag Ghost", rect, null, ManagedHudSprites.Disc, ManagedHudStyle.SlotPulse, false);
            _ghost.preserveAspect = true;
            _ghost.gameObject.SetActive(false);
        }
    }
}
