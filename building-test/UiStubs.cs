// Stand-ins for the UnityEngine.UIElements surface the build tray uses, with a small event dispatcher that follows
// the rules the panel relies on: pointer events go to the capturing element, bubble to parents, stop on
// StopPropagation, and releasing a capture sends PointerCaptureOutEvent. Verification harness only.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2Int { public int x, y; public Vector2Int(int x, int y) { this.x = x; this.y = y; } }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
}

namespace UnityEngine.UIElements
{
    public enum Position { Relative, Absolute }
    public enum FlexDirection { Column, ColumnReverse, Row, RowReverse }
    public enum Justify { FlexStart, Center, FlexEnd, SpaceBetween, SpaceAround }
    public enum Align { Auto, FlexStart, Center, FlexEnd, Stretch }
    public enum DisplayStyle { Flex, None }
    public enum WhiteSpace { Normal, NoWrap }
    public enum PickingMode { Position, Ignore }
    public enum PanelScaleMode { ConstantPixelSize, ConstantPhysicalSize, ScaleWithScreenSize }
    public enum PanelScreenMatchMode { MatchWidthOrHeight, ShrinkToFit, ExpandToFill }

    // Present in the real namespace and clashing with UnityEngine.Cursor, so a file that imports both and says Cursor fails here too.
    public struct Cursor { }

    public struct StyleLength { public float Value; public static implicit operator StyleLength(float v) { return new StyleLength { Value = v }; } }
    public struct StyleFloat { public float Value; public static implicit operator StyleFloat(float v) { return new StyleFloat { Value = v }; } }
    public struct StyleColor { public Color Value; public static implicit operator StyleColor(Color v) { return new StyleColor { Value = v }; } }
    public struct StyleEnum<T> where T : struct { public T Value; public static implicit operator StyleEnum<T>(T v) { return new StyleEnum<T> { Value = v }; } }
    public struct StyleFontDefinition { public Font Font; public StyleFontDefinition(Font f) { Font = f; } }

    public interface IStyle
    {
        StyleEnum<FlexDirection> flexDirection { get; set; }
        StyleEnum<Justify> justifyContent { get; set; }
        StyleEnum<Align> alignItems { get; set; }
        StyleEnum<Position> position { get; set; }
        StyleEnum<DisplayStyle> display { get; set; }
        StyleEnum<WhiteSpace> whiteSpace { get; set; }
        StyleEnum<TextAnchor> unityTextAlign { get; set; }
        StyleEnum<FontStyle> unityFontStyleAndWeight { get; set; }
        StyleFontDefinition unityFontDefinition { get; set; }
        StyleColor color { get; set; }
        StyleColor backgroundColor { get; set; }
        StyleLength left { get; set; }
        StyleLength top { get; set; }
        StyleLength right { get; set; }
        StyleLength bottom { get; set; }
        StyleLength width { get; set; }
        StyleLength height { get; set; }
        StyleLength minWidth { get; set; }
        StyleLength minHeight { get; set; }
        StyleLength marginTop { get; set; }
        StyleLength marginRight { get; set; }
        StyleLength marginBottom { get; set; }
        StyleLength paddingTop { get; set; }
        StyleLength paddingRight { get; set; }
        StyleLength paddingBottom { get; set; }
        StyleLength paddingLeft { get; set; }
        StyleLength fontSize { get; set; }
        StyleFloat borderTopWidth { get; set; }
        StyleFloat borderRightWidth { get; set; }
        StyleFloat borderBottomWidth { get; set; }
        StyleFloat borderLeftWidth { get; set; }
        StyleColor borderTopColor { get; set; }
        StyleColor borderRightColor { get; set; }
        StyleColor borderBottomColor { get; set; }
        StyleColor borderLeftColor { get; set; }
        StyleLength borderTopLeftRadius { get; set; }
        StyleLength borderTopRightRadius { get; set; }
        StyleLength borderBottomLeftRadius { get; set; }
        StyleLength borderBottomRightRadius { get; set; }
    }

    public sealed class StubStyle : IStyle
    {
        public StyleEnum<FlexDirection> flexDirection { get; set; }
        public StyleEnum<Justify> justifyContent { get; set; }
        public StyleEnum<Align> alignItems { get; set; }
        public StyleEnum<Position> position { get; set; }
        public StyleEnum<DisplayStyle> display { get; set; }
        public StyleEnum<WhiteSpace> whiteSpace { get; set; }
        public StyleEnum<TextAnchor> unityTextAlign { get; set; }
        public StyleEnum<FontStyle> unityFontStyleAndWeight { get; set; }
        public StyleFontDefinition unityFontDefinition { get; set; }
        public StyleColor color { get; set; }
        public StyleColor backgroundColor { get; set; }
        public StyleLength left { get; set; }
        public StyleLength top { get; set; }
        public StyleLength right { get; set; }
        public StyleLength bottom { get; set; }
        public StyleLength width { get; set; }
        public StyleLength height { get; set; }
        public StyleLength minWidth { get; set; }
        public StyleLength minHeight { get; set; }
        public StyleLength marginTop { get; set; }
        public StyleLength marginRight { get; set; }
        public StyleLength marginBottom { get; set; }
        public StyleLength paddingTop { get; set; }
        public StyleLength paddingRight { get; set; }
        public StyleLength paddingBottom { get; set; }
        public StyleLength paddingLeft { get; set; }
        public StyleLength fontSize { get; set; }
        public StyleFloat borderTopWidth { get; set; }
        public StyleFloat borderRightWidth { get; set; }
        public StyleFloat borderBottomWidth { get; set; }
        public StyleFloat borderLeftWidth { get; set; }
        public StyleColor borderTopColor { get; set; }
        public StyleColor borderRightColor { get; set; }
        public StyleColor borderBottomColor { get; set; }
        public StyleColor borderLeftColor { get; set; }
        public StyleLength borderTopLeftRadius { get; set; }
        public StyleLength borderTopRightRadius { get; set; }
        public StyleLength borderBottomLeftRadius { get; set; }
        public StyleLength borderBottomRightRadius { get; set; }
    }

    public interface IPanel { }
    public sealed class StubPanel : IPanel { public float PixelsPerScreenPixel = 1f; }

    public interface IEventHandler { }
    public delegate void EventCallback<T>(T evt);

    public abstract class EventBase
    {
        public bool PropagationStopped { get; private set; }
        public void StopPropagation() { PropagationStopped = true; }
    }
    public abstract class PointerEventBase : EventBase
    {
        public int pointerId { get; set; }
        public int button { get; set; }
        public Vector3 position { get; set; }
    }
    public sealed class PointerDownEvent : PointerEventBase { }
    public sealed class PointerUpEvent : PointerEventBase { }
    public sealed class PointerCaptureOutEvent : EventBase { public int pointerId { get; set; } }

    public interface IVisualElementScheduledItem { }
    public sealed class StubScheduler
    {
        public readonly List<Action> Queue = new List<Action>();
        public IVisualElementScheduledItem Execute(Action a) { Queue.Add(a); return null; }
        public void RunAll() { var copy = Queue.ToArray(); Queue.Clear(); foreach (var a in copy) a(); }
    }
    public interface IVisualElementScheduler { }

    public class VisualElement : IEventHandler
    {
        private readonly Dictionary<Type, List<Delegate>> _callbacks = new Dictionary<Type, List<Delegate>>();
        private readonly List<VisualElement> _children = new List<VisualElement>();
        internal IPanel PanelRef;
        public string name;
        public PickingMode pickingMode = PickingMode.Position;
        public StubStyle StubStyleInstance = new StubStyle();
        public IStyle style { get { return StubStyleInstance; } }
        public Rect worldBound;
        public VisualElement parent { get; private set; }
        public readonly StubScheduler Scheduler = new StubScheduler();
        public StubScheduler schedule { get { return Root().Scheduler; } }
        public IEnumerable<VisualElement> Children { get { return _children; } }
        public int childCount { get { return _children.Count; } }

        public IPanel panel { get { return Root().PanelRef; } }
        private VisualElement Root() { VisualElement e = this; while (e.parent != null) e = e.parent; return e; }

        public void Add(VisualElement child)
        {
            if (child.parent != null) child.parent._children.Remove(child);
            child.parent = this;
            _children.Add(child);
        }
        public void Clear() { foreach (var c in _children) c.parent = null; _children.Clear(); }
        public void RemoveFromHierarchy() { if (parent != null) { parent._children.Remove(this); parent = null; } }
        public void RegisterCallback<T>(EventCallback<T> cb) where T : EventBase
        {
            List<Delegate> list;
            if (!_callbacks.TryGetValue(typeof(T), out list)) _callbacks[typeof(T)] = list = new List<Delegate>();
            list.Add(cb);
        }
        internal void Invoke(EventBase e)
        {
            List<Delegate> list;
            if (_callbacks.TryGetValue(e.GetType(), out list))
                foreach (Delegate d in list.ToArray()) d.DynamicInvoke(e);
        }
    }

    public class TextElement : VisualElement { public string text; }
    public class Label : TextElement { public Label() { } public Label(string t) { text = t; } }

    public static class PointerCaptureHelper
    {
        internal static readonly Dictionary<int, VisualElement> Captured = new Dictionary<int, VisualElement>();
        public static void CapturePointer(this IEventHandler handler, int pointerId)
        {
            Captured[pointerId] = (VisualElement)handler;
        }
        public static bool HasPointerCapture(this IEventHandler handler, int pointerId)
        {
            VisualElement e;
            return Captured.TryGetValue(pointerId, out e) && ReferenceEquals(e, handler);
        }
        public static void ReleasePointer(this IEventHandler handler, int pointerId)
        {
            if (!HasPointerCapture(handler, pointerId)) return;
            Captured.Remove(pointerId);
            Stub.Send((VisualElement)handler, new PointerCaptureOutEvent { pointerId = pointerId });
        }
    }

    public static class Stub
    {
        // Sends the event the way the panel does: the capturing element gets pointer events, then the event bubbles.
        public static void Send(VisualElement target, EventBase e)
        {
            var pe = e as PointerEventBase;
            if (pe != null)
            {
                VisualElement holder;
                if (PointerCaptureHelper.Captured.TryGetValue(pe.pointerId, out holder)) target = holder;
            }

            for (VisualElement t = target; t != null; t = t.parent)
            {
                t.Invoke(e);
                if (e.PropagationStopped) break;
            }
        }
        public static void Reset() { PointerCaptureHelper.Captured.Clear(); }
    }

    public static class RuntimePanelUtils
    {
        public static Vector2 ScreenToPanel(IPanel panel, Vector2 screen)
        {
            float k = ((StubPanel)panel).PixelsPerScreenPixel;
            return new Vector2(screen.x * k, screen.y * k);
        }
    }

    public class PanelSettings : ScriptableObject
    {
        public PanelScaleMode scaleMode;
        public Vector2Int referenceResolution;
        public PanelScreenMatchMode screenMatchMode;
        public float match;
    }

    public class UIDocument : MonoBehaviour, IStubLifecycle
    {
        private readonly VisualElement _root = new VisualElement { name = "Root" };
        private PanelSettings _settings;
        public float sortingOrder;
        public void OnAdded() { }
        public PanelSettings panelSettings
        {
            get { return _settings; }
            set { _settings = value; _root.PanelRef = value != null ? new StubPanel() : null; }
        }
        public VisualElement rootVisualElement { get { return _root; } }
    }
}
