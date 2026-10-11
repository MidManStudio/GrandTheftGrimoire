// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCheatManager.cs"
// ============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Text;
using MidManStudio.Gtg.Managed.Camera;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MidManStudio.Gtg.Managed.Cheats
{
    /// <summary>
    /// Feeds typed characters to the cheat registry and draws the cheat panel. One of these is
    /// created on its own when play starts, in the editor and in development builds only, and
    /// it survives scene loads. The backquote key opens and closes the panel. A code typed at
    /// any time works without the panel. A scene load switches every toggle off, because the
    /// objects they changed are replaced, and so does the end of play.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedCheatManager : MonoBehaviour
    {
        private const float PanelWidth = 560f;
        private const float MessageSeconds = 4f;

        private static ManagedCheatManager _instance;

        private readonly ManagedCheatRegistry _registry = ManagedBuiltInCheats.CreateRegistry();
        private readonly ManagedCheatSceneHost _host = new ManagedCheatSceneHost();
        private readonly StringBuilder _text = new StringBuilder(128);
        private Keyboard _keyboard;
        private Vector2 _scroll;
        private GUIStyle _badgeStyle;
        private GUIStyle _richStyle;
        private string _activeText = string.Empty;
        private bool _open;
        private bool _wasLocked;
        private bool _dirty = true;

        public bool IsOpen { get { return _open; } }

        public ManagedCheatRegistry Registry { get { return _registry; } }

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // A manager the scene already has, even a disabled one, is the scene's own choice.
            if (_instance != null || FindFirstObjectByType<ManagedCheatManager>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            var host = new GameObject("GTG Cheat Manager");
            DontDestroyOnLoad(host);
            host.AddComponent<ManagedCheatManager>();
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
                Debug.LogWarning("More than one ManagedCheatManager. Disabling the extra one on " + name + ".");
                enabled = false;
                return;
            }

            _instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            if (_instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            ReleaseKeyboard();
            if (_open)
            {
                SetOpen(false);
            }

            _registry.RevertAll(_host);
            _instance = null;
        }

        private void Update()
        {
            WatchKeyboard();

            Keyboard keyboard = _keyboard;
            if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame)
            {
                TogglePanel();
            }
        }

        // The keyboard object can change when a device is plugged in, so the listener follows it.
        private void WatchKeyboard()
        {
            Keyboard current = Keyboard.current;
            if (current == _keyboard)
            {
                return;
            }

            ReleaseKeyboard();
            _keyboard = current;
            if (_keyboard != null)
            {
                _keyboard.onTextInput += OnTextInput;
            }
        }

        private void ReleaseKeyboard()
        {
            if (_keyboard != null)
            {
                _keyboard.onTextInput -= OnTextInput;
                _keyboard = null;
            }
        }

        private void OnTextInput(char c)
        {
            ManagedCheat cheat = _registry.Feed(c, Time.unscaledTime);
            if (cheat != null)
            {
                Use(cheat);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
            {
                return;
            }

            _registry.ClearBuffer();
            _registry.RevertAll(_host);
            _dirty = true;
        }

        private void Use(ManagedCheat cheat)
        {
            _registry.Activate(cheat, _host);
            _dirty = true;
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
            }
            else if (_wasLocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _wasLocked = false;
            }
        }

        private void OnGUI()
        {
            if (_dirty)
            {
                RebuildActiveText();
            }

            if (_open)
            {
                DrawPanel();
            }
            else
            {
                DrawBadge();
            }
        }

        private void RebuildActiveText()
        {
            _dirty = false;
            _text.Length = 0;
            for (int i = 0; i < _registry.Count; i++)
            {
                ManagedCheat cheat = _registry.Get(i);
                if (cheat.On)
                {
                    if (_text.Length > 0)
                    {
                        _text.Append(", ");
                    }

                    _text.Append(cheat.Code);
                }
            }

            _activeText = _text.ToString();
        }

        // A closed panel still shows what is on, and the last message for a few seconds.
        private void DrawBadge()
        {
            bool showMessage = !string.IsNullOrEmpty(_host.LastMessage)
                && Time.unscaledTime - _host.LastMessageTime < MessageSeconds;
            if (_activeText.Length == 0 && !showMessage)
            {
                return;
            }

            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            }

            string line = _activeText.Length > 0 ? "Cheats on: " + _activeText : string.Empty;
            if (showMessage)
            {
                line = line.Length > 0 ? line + "\n" + _host.LastMessage : _host.LastMessage;
            }

            GUI.Label(new Rect(12f, Screen.height - 54f, 900f, 48f), line, _badgeStyle);
        }

        private void DrawPanel()
        {
            float height = Mathf.Min(Screen.height - 24f, 600f);
            var area = new Rect(Screen.width - PanelWidth - 12f, 12f, PanelWidth, height);
            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label("GTG cheats (editor and development builds only)");
            GUILayout.Label("Backquote closes this panel. Type a code at any time to use it.");

            _scroll = GUILayout.BeginScrollView(_scroll);
            string group = null;
            for (int i = 0; i < _registry.Count; i++)
            {
                ManagedCheat cheat = _registry.Get(i);
                if (cheat.Group != group)
                {
                    group = cheat.Group;
                    GUILayout.Space(6f);
                    GUILayout.Label("<b>" + group + "</b>", RichLabel());
                }

                GUILayout.BeginHorizontal();
                string mark = cheat.IsToggle ? (cheat.On ? "[on]  " : "[off] ") : "[run] ";
                if (GUILayout.Button(mark + cheat.Name, GUILayout.Width(190f)))
                {
                    Use(cheat);
                }

                GUILayout.Label(cheat.Code + "   " + cheat.Description);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_host.LastMessage))
            {
                GUILayout.Label(_host.LastMessage);
            }

            GUILayout.Label("Typing a code also presses the game keys, so the character may move or cast while you type.");
            GUILayout.EndArea();
        }

        private GUIStyle RichLabel()
        {
            if (_richStyle == null)
            {
                _richStyle = new GUIStyle(GUI.skin.label) { richText = true };
            }

            return _richStyle;
        }
    }
}

#endif
