// Hand written stand-ins for the UnityEditor APIs that the NPC debugger uses. Test infrastructure only. They check
// that the editor code compiles against the API shapes used here and record Handles calls so the drawing can be
// tested. They do not prove the shapes match the real UnityEditor, and the Editor still has to compile the files.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor
{
    public enum MessageType { None, Info, Warning, Error }

    public class MenuItemAttribute : Attribute { public MenuItemAttribute(string path) { } }

    public static class Selection { public static GameObject activeGameObject; }

    public class SceneView
    {
        public static event Action<SceneView> duringSceneGui;
        public static SceneView lastActiveSceneView;
        public static int RepaintCalls;
        public int FrameCalls;
        public static void RepaintAll() { RepaintCalls++; }
        public bool FrameSelected() { FrameCalls++; return true; }
        public static void Raise(SceneView v) { Action<SceneView> h = duringSceneGui; if (h != null) h(v); }
        public static int SubscriberCount { get { return duringSceneGui == null ? 0 : duringSceneGui.GetInvocationList().Length; } }
    }

    public class EditorWindow : ScriptableStub
    {
        public int RepaintCalls;
        public bool Shown;
        public static T GetWindow<T>(string title) where T : EditorWindow, new() { return new T(); }
        public void Show() { Shown = true; }
        public void Repaint() { RepaintCalls++; }
    }
    public class ScriptableStub { }

    public static class EditorGUILayout
    {
        public static void HelpBox(string message, MessageType type) { }
        public static int Popup(string label, int selected, string[] options) { return selected; }
        public static bool ToggleLeft(string label, bool value) { return value; }
        public static Vector2 BeginScrollView(Vector2 scroll) { return scroll; }
        public static void EndScrollView() { }
    }

    public static class Handles
    {
        public static Color color;
        public static List<string> Calls = new List<string>();
        public static void Reset() { Calls.Clear(); color = default(Color); }
        public static void Label(Vector3 position, string text) { Calls.Add("Label:" + text); }
        public static void Label(Vector3 position, string text, GUIStyle style) { Calls.Add("Label:" + text); }
        public static void DrawLine(Vector3 a, Vector3 b) { Calls.Add("Line"); }
        public static void DrawDottedLine(Vector3 a, Vector3 b, float size) { Calls.Add("Dotted"); }
        public static void DrawWireDisc(Vector3 center, Vector3 normal, float radius) { Calls.Add("WireDisc:" + radius.ToString("F2")); }
        public static void DrawSolidDisc(Vector3 center, Vector3 normal, float radius) { Calls.Add("SolidDisc"); }
        public static void DrawWireArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius) { Calls.Add("Arc:" + radius.ToString("F2")); }
    }
}

namespace UnityEngine
{
    public static class GUILayout
    {
        public static bool NextButton;
        public static List<string> Labels = new List<string>();
        public static bool Button(string text) { return NextButton; }
        public static void Label(string text) { Labels.Add(text); }
        public static void Space(float pixels) { }
    }

}
