// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedBuildUiHost.cs"
// ============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>
    /// Creates the UIDocument the build tray lives in and makes sure UI Toolkit receives input.
    /// The panel itself is built in code, so there is no UXML asset.
    /// </summary>
    public static class ManagedBuildUiHost
    {
        /// <summary>
        /// Adds a UIDocument to <paramref name="owner"/>. When <paramref name="panelSettings"/> is
        /// null a PanelSettings is created at runtime and returned through <paramref name="created"/>,
        /// so the caller can destroy it. That runtime copy has no theme style sheet and may log a
        /// warning about it. An asset made with Create, UI Toolkit, Panel Settings Asset is the
        /// reliable choice, and it is the only one a player build can use without extra shader setup.
        /// </summary>
        public static UIDocument Create(GameObject owner, PanelSettings panelSettings, float sortingOrder, out PanelSettings created)
        {
            created = null;
            PanelSettings settings = panelSettings;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = new Vector2Int(1920, 1080);
                settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                settings.match = 0.5f;
                created = settings;
            }

            // The root element exists as soon as the component is enabled, and assigning the
            // settings afterwards attaches it to the panel.
            var document = owner.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.sortingOrder = sortingOrder;

            Font font = LoadBuiltinFont();
            if (font != null)
            {
                // Text style inherits, so the root is enough. Without a theme there is no default font.
                document.rootVisualElement.style.unityFontDefinition = new StyleFontDefinition(font);
            }

            return document;
        }

        /// <summary>
        /// UI Toolkit reads the old Input Manager unless a UGUI EventSystem is in the scene. With
        /// the Input System package the EventSystem needs the Input System UI module, which is
        /// what this adds when no EventSystem exists yet.
        /// </summary>
        public static void EnsureEventSystem()
        {
            EventSystem existing = Object.FindObjectOfType<EventSystem>();
            if (existing == null)
            {
                var go = new GameObject("GTG EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
                return;
            }

            if (existing.GetComponent<StandaloneInputModule>() != null && existing.GetComponent<InputSystemUIInputModule>() == null)
            {
                Debug.LogWarning(
                    "[GTG Build] The scene EventSystem uses StandaloneInputModule, which reads the old Input Manager. " +
                    "Replace it with InputSystemUIInputModule if the build tray does not react to the mouse.");
            }
        }

        private static Font LoadBuiltinFont()
        {
            try
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (System.ArgumentException)
            {
                return null;
            }
        }
    }
}
