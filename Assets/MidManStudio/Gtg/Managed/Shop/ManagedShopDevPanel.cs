// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedShopDevPanel.cs"
// ============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using MidManStudio.Gtg.Managed.Camera;
using MidManStudio.Gtg.Managed.Economy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidManStudio.Gtg.Managed.Shop
{
    /// <summary>
    /// A throwaway panel for trying the shop engine before the real shop window exists. It lists
    /// the sample shops, buys and sells with the player's purse, and flips the flag of the hidden
    /// shop. One is created on its own when play starts, in the editor and in development builds
    /// only, and F9 opens and closes it. The drawn shop window replaces it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedShopDevPanel : MonoBehaviour
    {
        private const float PanelWidth = 520f;
        private const float WalletRetrySeconds = 1f;
        private const int BulkCount = 5;

        private static ManagedShopDevPanel _instance;

        private ManagedShopDevSession _session;
        private ManagedPlayerWallet _wallet;
        private Vector2 _scroll;
        private int _selected;
        private float _nextWalletSearch;
        private bool _open;
        private bool _wasLocked;

        public bool IsOpen { get { return _open; } }

        public ManagedShopDevSession Session
        {
            get
            {
                if (_session == null)
                {
                    _session = ManagedShopDevSession.CreateSample();
                }

                return _session;
            }
        }

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // A panel the scene already has, even a disabled one, is the scene's own choice.
            if (_instance != null || FindFirstObjectByType<ManagedShopDevPanel>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            var host = new GameObject("GTG Shop Test Panel");
            DontDestroyOnLoad(host);
            host.AddComponent<ManagedShopDevPanel>();
        }

        /// <summary>Public so a button or another tool can open and close the panel.</summary>
        public void TogglePanel()
        {
            SetOpen(!_open);
        }

        private void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("More than one ManagedShopDevPanel. Disabling the extra one on " + name + ".");
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

            if (_open)
            {
                SetOpen(false);
            }

            _instance = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
            {
                TogglePanel();
            }
        }

        // Opening frees the cursor so the buttons can be clicked and keeps the camera from taking it
        // back. Closing gives the cursor back only if the panel took it.
        private void SetOpen(bool open)
        {
            if (_open == open)
            {
                return;
            }

            _open = open;
            ManagedCameraRig.CursorCaptureBlocked = open;

            if (open)
            {
                _wasLocked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                FindWallet();
            }
            else if (_wasLocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _wasLocked = false;
            }
        }

        // The caster adds the wallet when play starts, so it is looked for, never created here.
        private void FindWallet()
        {
            _nextWalletSearch = Time.unscaledTime + WalletRetrySeconds;
            if (_wallet == null)
            {
                _wallet = FindFirstObjectByType<ManagedPlayerWallet>();
            }
        }

        private ManagedWallet CurrentWallet()
        {
            if (_wallet == null && Time.unscaledTime >= _nextWalletSearch)
            {
                FindWallet();
            }

            return _wallet != null ? _wallet.Wallet : null;
        }

        private void OnGUI()
        {
            if (!_open)
            {
                return;
            }

            ManagedShopDevSession session = Session;
            ManagedWallet wallet = CurrentWallet();

            // Left side, so it does not cover the cheat panel on the right.
            float height = Mathf.Min(Screen.height - 24f, 640f);
            GUILayout.BeginArea(new Rect(12f, 12f, PanelWidth, height), GUI.skin.box);

            GUILayout.Label("GTG shop test (editor and development builds only)");
            GUILayout.Label("F9 closes this panel. The shops and items are samples, not game content.");
            GUILayout.Label(wallet != null
                ? "Purse: " + wallet.Describe()
                : "No player wallet in the scene yet. The spell caster adds one when play starts.");
            GUILayout.Label("Bag, " + session.Bag.UsedSlots + " of " + session.Bag.SlotCount + " slots: " + session.DescribeBag());

            if (_selected < 0 || _selected >= session.ShopCount)
            {
                _selected = 0;
            }

            for (int i = 0; i < session.ShopCount; i++)
            {
                ManagedShop other = session.ShopAt(i);
                string mark = i == _selected ? "> " : string.Empty;
                if (GUILayout.Button(mark + (other.IsOpen ? "[open] " : "[closed] ") + other.DisplayName))
                {
                    _selected = i;
                }
            }

            ManagedShop shop = session.ShopAt(_selected);
            if (shop != null)
            {
                DrawShop(session, shop, wallet);
            }

            if (!string.IsNullOrEmpty(session.LastMessage))
            {
                GUILayout.Label(session.LastMessage);
            }

            GUILayout.EndArea();
        }

        private void DrawShop(ManagedShopDevSession session, ManagedShop shop, ManagedWallet wallet)
        {
            string keeper = shop.ShopkeeperName.Length > 0 ? shop.ShopkeeperName : "(no name yet)";
            GUILayout.Label("Shopkeeper: " + keeper);

            string flag = shop.RequiredFlag;
            if (flag.Length > 0)
            {
                GUILayout.BeginHorizontal();
                bool set = session.Flags.IsSet(flag);
                GUILayout.Label("Opens with the flag " + flag + ", now " + (set ? "set" : "clear"));
                if (GUILayout.Button(set ? "Clear flag" : "Set flag", GUILayout.Width(110f)))
                {
                    session.ToggleFlag(flag);
                    LogMessage(session);
                }

                GUILayout.EndHorizontal();
            }

            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("Sells");
            for (int i = 0; i < shop.SellOfferCount; i++)
            {
                ManagedShopOffer offer;
                if (!shop.TryGetSellOffer(i, out offer))
                {
                    continue;
                }

                string stock = offer.Stock == ManagedShopLine.Unlimited ? "unlimited" : offer.Stock + " left";
                string price = offer.UnitPriceSilver > 0 ? ManagedShopDevSession.Money(offer.UnitPriceSilver) : "no price";
                GUILayout.BeginHorizontal();
                GUILayout.Label(offer.Name + "  " + price + "  " + stock, GUILayout.Width(300f));
                if (GUILayout.Button("Buy 1", GUILayout.Width(70f)))
                {
                    session.Buy(_selected, wallet, offer.ItemId, 1);
                    LogMessage(session);
                }

                if (GUILayout.Button("Buy " + BulkCount, GUILayout.Width(70f)))
                {
                    session.Buy(_selected, wallet, offer.ItemId, BulkCount);
                    LogMessage(session);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            GUILayout.Label("Buys");
            for (int i = 0; i < shop.BuyOfferCount; i++)
            {
                ManagedShopOffer offer;
                if (!shop.TryGetBuyOffer(i, out offer))
                {
                    continue;
                }

                string price = offer.UnitPriceSilver > 0 ? ManagedShopDevSession.Money(offer.UnitPriceSilver) : "no price";
                GUILayout.BeginHorizontal();
                GUILayout.Label(offer.Name + "  pays " + price + "  you carry " + session.Bag.CountOf(offer.ItemId), GUILayout.Width(300f));
                if (GUILayout.Button("Sell 1", GUILayout.Width(70f)))
                {
                    session.Sell(_selected, wallet, offer.ItemId, 1);
                    LogMessage(session);
                }

                if (GUILayout.Button("Sell all", GUILayout.Width(70f)))
                {
                    session.SellAll(_selected, wallet, offer.ItemId);
                    LogMessage(session);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private static void LogMessage(ManagedShopDevSession session)
        {
            Debug.Log("[GTG Shop] " + session.LastMessage);
        }
    }
}

#endif
