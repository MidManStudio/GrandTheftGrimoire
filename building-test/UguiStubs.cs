// Hand written stand-ins for uGUI, its event system and TextMeshPro. Verification harness only.
// Every graphic is a plain holder of the values the HUD sets. Nothing is drawn or laid out.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Sprite : Object
    {
        public Texture2D texture; public Rect rect;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot) { return new Sprite { texture = texture, rect = rect }; }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) { return new Sprite { texture = texture, rect = rect }; }
    }

    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin, anchorMax, anchoredPosition, sizeDelta, pivot, offsetMin, offsetMax;
    }

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class Canvas : Behaviour
    {
        public RenderMode renderMode;
        public int sortingOrder;
        public float scaleFactor = 1f;
    }

    public class CanvasRenderer : Component { }
}

namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Behaviour
    {
        public UnityEngine.RectTransform rectTransform { get { return (UnityEngine.RectTransform)transform; } }
        public UnityEngine.Color color = UnityEngine.Color.white;
        public bool raycastTarget = true;
        public UnityEngine.Material material;
    }

    public class MaskableGraphic : Graphic { }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public enum OriginHorizontal { Left, Right }
        public enum Origin360 { Bottom, Right, Top, Left }

        public UnityEngine.Sprite sprite;
        public Type type;
        public FillMethod fillMethod;
        public int fillOrigin;
        public float fillAmount = 1f;
        public bool preserveAspect;
        public bool fillClockwise = true;
    }

    public class Text : MaskableGraphic
    {
        public string text = "";
        public UnityEngine.Font font;
        public int fontSize = 14;
        public UnityEngine.FontStyle fontStyle;
        public UnityEngine.TextAnchor alignment;
        public UnityEngine.HorizontalWrapMode horizontalOverflow;
        public UnityEngine.VerticalWrapMode verticalOverflow;
        public bool resizeTextForBestFit;
        public int resizeTextMinSize = 10, resizeTextMaxSize = 40;
    }

    public class CanvasScaler : UnityEngine.Behaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }

        public ScaleMode uiScaleMode;
        public UnityEngine.Vector2 referenceResolution;
        public ScreenMatchMode screenMatchMode;
        public float matchWidthOrHeight;
    }

    public class GraphicRaycaster : UnityEngine.Behaviour { }

    public class RectMask2D : UnityEngine.Behaviour { }
}

namespace UnityEngine.EventSystems
{
    public enum InputButton { Left, Right, Middle }

    public class PointerEventData
    {
        public enum InputButton { Left, Right, Middle }
        public InputButton button;
        public UnityEngine.Vector2 position;
    }

    public interface IPointerClickHandler { void OnPointerClick(PointerEventData eventData); }
    public interface IBeginDragHandler { void OnBeginDrag(PointerEventData eventData); }
    public interface IDragHandler { void OnDrag(PointerEventData eventData); }
    public interface IEndDragHandler { void OnEndDrag(PointerEventData eventData); }
    public interface IDropHandler { void OnDrop(PointerEventData eventData); }
}

namespace TMPro
{
    public enum TextAlignmentOptions { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }
    public enum TextOverflowModes { Overflow, Ellipsis, Masking, Truncate, ScrollRect, Page, Linked }

    public class TMP_Text : UnityEngine.UI.MaskableGraphic
    {
        public string text = "";
        public float fontSize = 36f;
        public TextAlignmentOptions alignment;
        public bool enableWordWrapping;
        public TextOverflowModes overflowMode;
        public bool enableAutoSizing;
        public float fontSizeMin = 18f, fontSizeMax = 72f;
    }

    public class TextMeshProUGUI : TMP_Text { }

    public class TMP_FontAsset : UnityEngine.ScriptableObject { }

    public class TMP_Settings : UnityEngine.ScriptableObject
    {
        public static TMP_FontAsset defaultFontAsset;
    }
}
