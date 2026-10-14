// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudBuilder.cs"
// ============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// The small helpers that make the HUD's uGUI objects: rectangles placed from the layout,
    /// images that use a drawn sprite or a tinted placeholder, the canvas, and the event system.
    /// </summary>
    internal static class ManagedHudBuilder
    {
        /// <summary>Places a rectangle from a layout rectangle. The anchor and the pivot are the same point.</summary>
        internal static void Apply(RectTransform rt, ManagedHudRect rect)
        {
            var anchor = new Vector2(rect.AnchorX, rect.AnchorY);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = new Vector2(rect.X, rect.Y);
            rt.sizeDelta = new Vector2(rect.Width, rect.Height);
        }

        /// <summary>Makes an empty rectangle under a parent and places it.</summary>
        internal static RectTransform NewRect(Transform parent, string name, ManagedHudRect rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Apply(rt, rect);
            return rt;
        }

        /// <summary>Makes an empty rectangle that fills its parent.</summary>
        internal static RectTransform NewStretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>
        /// Makes an image under a parent. When the drawn piece named <paramref name="spriteName"/>
        /// exists it is used untinted. Otherwise the fallback sprite is used in the fallback
        /// colour, so the piece still shows where it goes.
        /// </summary>
        internal static Image NewImage(Transform parent, string name, ManagedHudRect rect, string spriteName, Sprite fallback, Color fallbackColor, bool raycast)
        {
            RectTransform rt = NewRect(parent, name, rect);
            return StyleImage(rt.gameObject.AddComponent<Image>(), spriteName, fallback, fallbackColor, raycast);
        }

        /// <summary>Gives an image its drawn sprite or its placeholder, the way <see cref="NewImage"/> does.</summary>
        internal static Image StyleImage(Image image, string spriteName, Sprite fallback, Color fallbackColor, bool raycast)
        {
            Sprite drawn = ManagedHudSprites.Load(spriteName);
            image.sprite = drawn != null ? drawn : fallback;
            image.color = drawn != null ? Color.white : fallbackColor;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>
        /// Makes the HUD canvas under a parent. It covers the screen, scales from a 1920 by 1080
        /// reference, and sits above the scene.
        /// </summary>
        internal static Canvas NewCanvas(Transform parent, int sortingOrder)
        {
            var go = new GameObject("GTG HUD Canvas", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ManagedHudLayout.ReferenceWidth, ManagedHudLayout.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>
        /// Makes sure clicks and drags reach the HUD. A scene with no event system gets one. An event
        /// system that still has the old input module gets the Input System one in its place,
        /// because the old module cannot read the new input and fails every frame.
        /// </summary>
        internal static void EnsureEventSystem()
        {
            EventSystem system = Object.FindFirstObjectByType<EventSystem>();
            if (system == null)
            {
                var go = new GameObject("EventSystem");
                system = go.AddComponent<EventSystem>();
            }

            if (system.GetComponent<InputSystemUIInputModule>() != null)
            {
                return;
            }

            var legacy = system.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                Object.Destroy(legacy);
            }

            InputSystemUIInputModule module = system.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }
}
