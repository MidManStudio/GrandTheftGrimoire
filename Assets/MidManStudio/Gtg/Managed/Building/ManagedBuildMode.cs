// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedBuildMode.cs"
// ============================================================================

using System;
using MidManStudio.Gtg.Managed.Camera;
using MidManStudio.Gtg.Managed.CharacterController;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>
    /// Scene component of the building system. It builds the store, the placement state machine,
    /// the instanced renderer, the collider proxies and the UI Toolkit tray, and runs the
    /// per-frame loop that feeds them the pointer. Press the toggle key to open the tray. Drag a
    /// station into the world or click it and then click the ground. Click a placed station to
    /// pick it up. Add it to any GameObject. Everything else is found or created. Inside
    /// MidManStudio.Gtg namespaces write UnityEngine.Camera in full, because the simple name
    /// Camera resolves to a namespace. UnityEngine.UIElements is not imported here for the same
    /// kind of reason, its Cursor struct would collide with UnityEngine.Cursor.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class ManagedBuildMode : MonoBehaviour
    {
        /// <summary>One line of the starting inventory.</summary>
        [Serializable]
        public sealed class StartingStation
        {
            [Tooltip("Id of a buildable, for instance crafting_table or butchery_station.")]
            public string BuildableId;

            public int Count = 1;
        }

        [Header("Scene references (all optional)")]
        [Tooltip("Reach is measured from this character, and the camera ray ignores it. Found automatically when empty.")]
        [SerializeField] private ManagedCharacter _player;
        [Tooltip("Frees the cursor while the tray is open. Found automatically when empty. Without one this component sets the cursor itself.")]
        [SerializeField] private ManagedCameraRig _cameraRig;
        [Tooltip("The camera the pointer ray starts from. The main camera when empty.")]
        [SerializeField] private UnityEngine.Camera _aimCamera;

        [Header("UI")]
        [Tooltip("PanelSettings asset for the tray. Create one with Create, UI Toolkit, Panel Settings Asset. When empty a runtime copy is made, which is fine in the editor but needs extra shader setup in a player build.")]
        [SerializeField] private UnityEngine.UIElements.PanelSettings _panelSettings;
        [SerializeField] private float _uiSortingOrder = 50f;

        [Header("Rendering")]
        [Tooltip("Lit material the placed pieces copy. A URP Lit material is the right choice. When empty a URP Lit shader is looked up by name, which a player build only finds when something else references it.")]
        [SerializeField] private Material _pieceMaterial;
        [Tooltip("Layer of the placed pieces and of their colliders. A camera that does not render this layer does not draw them.")]
        [Range(0, 31)]
        [SerializeField] private int _pieceLayer;

        [Header("Placement")]
        [SerializeField] private ManagedBuildRules _rules = new ManagedBuildRules();
        [Tooltip("What counts as ground, for the pointer ray and for the five samples under a footprint. Leave the player out of it when the player has its own layer.")]
        [SerializeField] private LayerMask _groundMask = ~0;
        [Tooltip("What blocks a footprint. The player counts too, so a station cannot be dropped on top of the player.")]
        [SerializeField] private LayerMask _blockerMask = ~0;
        [SerializeField] private float _aimRayLength = 100f;

        [Header("Input")]
        [SerializeField] private Key _toggleKey = Key.B;
        [Tooltip("The mouse wheel turns the piece being placed.")]
        [SerializeField] private bool _wheelRotates = true;
        [SerializeField] private bool _startInBuildMode;

        [Header("Starting items (ignored when SetStock supplies a stock)")]
        [SerializeField] private StartingStation[] _startingStock = new StartingStation[0];
        [Tooltip("Ids of the blueprints that start unlocked.")]
        [SerializeField] private string[] _startingBlueprints = new string[0];

        private ManagedBuildInventory _defaultStock;
        private IManagedBuildStock _customStock;
        private IManagedBuildStock _stock;
        private ManagedBlueprintBook _blueprints;
        private ManagedPlacedStore _store;
        private ManagedUnityBuildProbe _probe;
        private ManagedPlacementController _controller;
        private ManagedPlacedRenderer _renderer;
        private ManagedPlacedColliders _colliders;
        private ManagedBuildAim _aim;
        private ManagedBuildPanel _panel;
        private UnityEngine.UIElements.PanelSettings _createdPanelSettings;
        private GameObject _piecesRoot;
        private GameObject _uiRoot;

        private bool _built;
        private bool _active;
        private bool _restoreCursorLock;
        private bool _castSuppressed;
        private int _hoverPieceId = -1;

        /// <summary>Raised after build mode was switched on or off.</summary>
        public event Action<bool> BuildModeChanged;

        public bool BuildModeActive
        {
            get { return _active; }
        }

        /// <summary>The stock building spends from. Null until Start.</summary>
        public IManagedBuildStock Stock
        {
            get { return _stock; }
        }

        public ManagedBlueprintBook Blueprints
        {
            get { return _blueprints; }
        }

        /// <summary>Every placed piece. Null until Start. Read it to save, or fill it to load.</summary>
        public ManagedPlacedStore Store
        {
            get { return _store; }
        }

        /// <summary>The placement state machine. Null until Start.</summary>
        public ManagedPlacementController Placement
        {
            get { return _controller; }
        }

        /// <summary>
        /// Replaces the in-memory stock with one the game owns, for instance a view over an
        /// inventory container. The parts are built in Start, so this has to be called before
        /// that, from Awake or right after AddComponent.
        /// </summary>
        public void SetStock(IManagedBuildStock stock)
        {
            if (_built)
            {
                throw new InvalidOperationException(
                    "ManagedBuildMode builds its parts in Start. Call SetStock from Awake, or right after AddComponent.");
            }

            _customStock = stock;
        }

        /// <summary>
        /// Opens or closes the tray. Closing puts back a piece that is being carried and hands the
        /// cursor back. Before Start this only sets whether build mode starts open.
        /// </summary>
        public void SetBuildMode(bool active)
        {
            if (!_built)
            {
                _startInBuildMode = active;
                return;
            }

            if (active == _active)
            {
                return;
            }

            _active = active;
            _hoverPieceId = -1;

            if (active)
            {
                TakeCursorForUi();
                SuppressCasting();
            }
            else
            {
                _controller.Cancel();
                RestoreCasting();
                GiveCursorBack();
            }

            _panel.SetVisible(active);

            Action<bool> handler = BuildModeChanged;
            if (handler != null)
            {
                handler(active);
            }
        }

        private void Reset()
        {
            _startingStock = new[]
            {
                new StartingStation { BuildableId = ManagedBuildableDefinition.CraftingTable.Id, Count = 1 },
                new StartingStation { BuildableId = ManagedBuildableDefinition.ButcheryStation.Id, Count = 1 },
            };
            _startingBlueprints = new[]
            {
                ManagedBuildableDefinition.CraftingTable.Id,
                ManagedBuildableDefinition.ButcheryStation.Id,
            };
        }

        private void OnValidate()
        {
            if (_rules != null)
            {
                _rules.CellSize = Mathf.Max(0.05f, _rules.CellSize);
            }

            _aimRayLength = Mathf.Max(1f, _aimRayLength);
        }

        private void Start()
        {
            Build();
            _built = true;

            if (_startInBuildMode)
            {
                // SetBuildMode treats a call before the build as a request, so flip the flag back first.
                _startInBuildMode = false;
                SetBuildMode(true);
            }
        }

        private void Build()
        {
            if (_rules == null)
            {
                _rules = new ManagedBuildRules();
            }

            _rules.CellSize = Mathf.Max(0.05f, _rules.CellSize);

            if (_player == null)
            {
                _player = FindFirstObjectByType<ManagedCharacter>();
            }

            if (_cameraRig == null)
            {
                _cameraRig = FindFirstObjectByType<ManagedCameraRig>();
            }

            if (_customStock != null)
            {
                _stock = _customStock;
            }
            else
            {
                _defaultStock = new ManagedBuildInventory();
                SeedStock(_defaultStock);
                _stock = _defaultStock;
            }

            _blueprints = new ManagedBlueprintBook();
            SeedBlueprints(_blueprints);

            _store = new ManagedPlacedStore(new ManagedBuildGrid(_rules.CellSize), 64);
            _probe = new ManagedUnityBuildProbe(_groundMask, _blockerMask);
            _controller = new ManagedPlacementController(_rules, _store, _probe, _stock, _blueprints);

            // The pieces root stays at the scene root with no rotation or scale, because the
            // collider proxies are positioned in world space and sized in local space.
            _piecesRoot = new GameObject("GTG Placed Pieces");
            _renderer = new ManagedPlacedRenderer(_store, _pieceLayer, _pieceMaterial);
            _colliders = new ManagedPlacedColliders(_store, _piecesRoot.transform, _pieceLayer);

            // The pointer ray has to see the pieces as well as the ground, to tell which one is under it.
            int aimMask = _groundMask.value | (1 << _pieceLayer);
            _aim = new ManagedBuildAim(aimMask, _aimRayLength);
            if (_player != null)
            {
                _aim.IgnoreRoot = _player.transform;
            }

            ManagedBuildUiHost.EnsureEventSystem();
            _uiRoot = new GameObject("GTG Build UI");
            _uiRoot.transform.SetParent(transform, false);
            UnityEngine.UIElements.UIDocument document =
                ManagedBuildUiHost.Create(_uiRoot, _panelSettings, _uiSortingOrder, out _createdPanelSettings);
            _panel = new ManagedBuildPanel(
                document.rootVisualElement, _stock, _blueprints, _controller,
                OnSlotPress, OnSlotRelease, OnTrayPress, _toggleKey.ToString());
        }

        private void SeedStock(ManagedBuildInventory inventory)
        {
            if (_startingStock == null)
            {
                return;
            }

            for (int i = 0; i < _startingStock.Length; i++)
            {
                StartingStation line = _startingStock[i];
                if (line == null || line.Count <= 0)
                {
                    continue;
                }

                if (!ManagedBuildableCatalog.TryGetIndex(line.BuildableId, out _))
                {
                    Debug.LogWarning("[GTG Build] Starting stock names an unknown buildable: '" + line.BuildableId + "'.", this);
                    continue;
                }

                inventory.Give(line.BuildableId, line.Count);
            }
        }

        private void SeedBlueprints(ManagedBlueprintBook book)
        {
            if (_startingBlueprints == null)
            {
                return;
            }

            for (int i = 0; i < _startingBlueprints.Length; i++)
            {
                string id = _startingBlueprints[i];
                if (!ManagedBuildableCatalog.TryGetIndex(id, out _))
                {
                    Debug.LogWarning("[GTG Build] Starting blueprints name an unknown buildable: '" + id + "'.", this);
                    continue;
                }

                book.Unlock(id);
            }
        }

        // Runs after the character and the camera rig, so the aim sees this frame's camera.
        private void LateUpdate()
        {
            if (!_built)
            {
                return;
            }

            ManagedBuildInputFrame frame = ManagedBuildInput.Read(
                _toggleKey, _wheelRotates && _controller.Mode == ManagedPlacementMode.Placing);
            if (frame.TogglePressed)
            {
                SetBuildMode(!_active);
            }

            if (_active)
            {
                RunBuildFrame(frame);
            }

            // Placed pieces stay visible when the tray is closed.
            _renderer.DrawPieces();
        }

        private void RunBuildFrame(ManagedBuildInputFrame frame)
        {
            bool overTray = frame.PointerAvailable && _panel.IsOverTray(frame.Pointer);
            bool wasPlacing = _controller.Mode == ManagedPlacementMode.Placing;
            UpdateAim(frame.PointerAvailable, frame.Pointer, overTray);

            // Which state the frame started in decides what a press means, so the press that
            // picks a piece up cannot also put it down.
            if (wasPlacing)
            {
                if (frame.Rotate != 0)
                {
                    _controller.Rotate(frame.Rotate);
                }

                if (frame.CancelPressed)
                {
                    _controller.Cancel();
                }
                else if (frame.PrimaryPressed && !overTray && !_controller.CommitOnRelease)
                {
                    int placedId;
                    _controller.TryCommit(out placedId);
                }
            }
            else if (frame.PrimaryPressed && !overTray && _hoverPieceId >= 0)
            {
                if (_controller.BeginCarry(_hoverPieceId))
                {
                    _hoverPieceId = -1;

                    // Aim again, so the ghost is already under the pointer on the frame of the pick up.
                    UpdateAim(frame.PointerAvailable, frame.Pointer, overTray);
                }
            }

            if (_controller.Mode == ManagedPlacementMode.Placing)
            {
                _renderer.DrawGhost(_controller.Definition, _controller.Result, _controller.Quarter);
            }
            else if (_hoverPieceId >= 0 && !overTray)
            {
                _renderer.DrawHover(_hoverPieceId);
            }
        }

        // Casts the pointer ray and hands the result to the state machine while it is placing.
        private void UpdateAim(bool pointerAvailable, Vector2 pointer, bool overTray)
        {
            _hoverPieceId = -1;
            bool hasAim = false;
            Vector3 point = Vector3.zero;

            UnityEngine.Camera aimCamera = _aimCamera != null ? _aimCamera : UnityEngine.Camera.main;
            if (aimCamera != null && pointerAvailable && !overTray && aimCamera.pixelRect.Contains(pointer))
            {
                Ray ray = aimCamera.ScreenPointToRay(new Vector3(pointer.x, pointer.y, 0f));
                hasAim = _aim.Cast(ray, out point, out _hoverPieceId);
            }

            if (_controller.Mode == ManagedPlacementMode.Placing)
            {
                Vector3 reachOrigin = _player != null
                    ? _player.transform.position
                    : (aimCamera != null ? aimCamera.transform.position : Vector3.zero);
                _controller.SetAim(hasAim, point, reachOrigin);
            }
        }

        // A slot was pressed. Returns true when placing started, which is when the slot takes the pointer.
        private bool OnSlotPress(ManagedPlacementSource source, string buildableId)
        {
            // Pressing the tray with a piece in hand keeps the piece in the inventory.
            if (_controller.Mode == ManagedPlacementMode.Placing && _controller.Source == ManagedPlacementSource.Carried)
            {
                _controller.Stash();
                return false;
            }

            ManagedPlacementStatus refusal;
            if (_controller.BeginPlacing(source, buildableId, true, out refusal))
            {
                return true;
            }

            _panel.ShowNotice(ManagedPlacementValidator.Describe(refusal));
            return false;
        }

        // A drag that started on a slot ended.
        private void OnSlotRelease(bool overTray, bool dragged)
        {
            // The state machine may have left placing meanwhile, through Esc or a closed tray.
            if (_controller.Mode != ManagedPlacementMode.Placing)
            {
                return;
            }

            if (overTray)
            {
                // Dropping back on the tray cancels. A plain click selects, and the next click on the ground places.
                if (dragged)
                {
                    _controller.Cancel();
                }
                else
                {
                    _controller.ArmForClick();
                }

                return;
            }

            // The aim of the last frame can lag the release by one frame, so aim at the release position first.
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                UpdateAim(true, mouse.position.ReadValue(), false);
            }

            int placedId;
            if (!_controller.TryCommit(out placedId))
            {
                // A bad spot keeps the piece in hand, so the player can aim again and click.
                _controller.ArmForClick();
            }
        }

        private void OnTrayPress()
        {
            if (_controller.Mode != ManagedPlacementMode.Placing)
            {
                return;
            }

            if (_controller.Source == ManagedPlacementSource.Carried)
            {
                _controller.Stash();
            }
            else
            {
                _controller.Cancel();
            }
        }

        private void TakeCursorForUi()
        {
            _restoreCursorLock = Cursor.lockState == CursorLockMode.Locked;
            if (_cameraRig != null)
            {
                _cameraRig.CursorHeldByUi = true;
                _cameraRig.ReleaseCursor();
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void GiveCursorBack()
        {
            if (_cameraRig != null)
            {
                _cameraRig.CursorHeldByUi = false;
                if (_restoreCursorLock)
                {
                    _cameraRig.CaptureCursor();
                }
            }
            else if (_restoreCursorLock)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // The keyboard and gamepad still cast with a free cursor, so the module is switched off while the tray is open.
        private void SuppressCasting()
        {
            if (_player != null && _player.Has(ManagedCharacterFeature.Cast))
            {
                _player.Features &= ~ManagedCharacterFeature.Cast;
                _castSuppressed = true;
            }
        }

        private void RestoreCasting()
        {
            if (_castSuppressed && _player != null)
            {
                _player.Features |= ManagedCharacterFeature.Cast;
            }

            _castSuppressed = false;
        }

        private void OnDisable()
        {
            // A disabled component must not leave the cursor or the Cast module switched off.
            if (_built && _active)
            {
                SetBuildMode(false);
            }
        }

        private void OnDestroy()
        {
            if (_panel != null)
            {
                _panel.Dispose();
                _panel = null;
            }

            if (_colliders != null)
            {
                _colliders.Dispose();
                _colliders = null;
            }

            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }

            if (_createdPanelSettings != null)
            {
                Destroy(_createdPanelSettings);
                _createdPanelSettings = null;
            }

            if (_piecesRoot != null)
            {
                Destroy(_piecesRoot);
                _piecesRoot = null;
            }
        }
    }
}
