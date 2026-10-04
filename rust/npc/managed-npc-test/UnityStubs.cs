// Sandbox-only stand-in for the few UnityEngine APIs the Managed NPC code touches.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator *(Vector3 a, float f) { return new Vector3(a.x * f, a.y * f, a.z * f); }
        public static Vector3 operator /(Vector3 a, float f) { return new Vector3(a.x / f, a.y / f, a.z / f); }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public float magnitude { get { return (float)Math.Sqrt(sqrMagnitude); } }
    }
    public struct Quaternion
    {
        public static Quaternion AngleAxis(float angle, Vector3 axis) { return default(Quaternion); }
        public static Vector3 operator *(Quaternion q, Vector3 v) { return v; }
    }
    public struct Color { public static Color red = default(Color), yellow = default(Color); }
    public struct Rect { public Rect(float a, float b, float c, float d) { } }
    public static class Gizmos
    {
        public static Color color;
        public static void DrawWireSphere(Vector3 c, float r) { }
        public static void DrawRay(Vector3 a, Vector3 b) { }
    }
    public static class GUI { public static void Label(Rect r, string s) { } }
    public static class Mathf
    {
        public const float Deg2Rad = 0.0174532924f;
        public static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
        public static int Clamp(int v, int a, int b) { return v < a ? a : (v > b ? b : v); }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static float Cos(float f) { return (float)Math.Cos(f); }
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
    }
    public static class Time { public static float time; }
    public static class Debug
    {
        public static List<string> Logs = new List<string>();
        public static List<string> Warnings = new List<string>();
        public static void Log(string s) { Logs.Add(s); }
        public static void LogWarning(string s) { Warnings.Add(s); }
    }
    public struct LayerMask
    {
        public int value;
        public static implicit operator int(LayerMask m) { return m.value; }
        public static implicit operator LayerMask(int v) { return new LayerMask { value = v }; }
    }
    public enum QueryTriggerInteraction { Ignore }
    public enum FindObjectsInactive { Exclude, Include }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class SerializeFieldAttribute : Attribute { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class DefaultExecutionOrderAttribute : Attribute { public DefaultExecutionOrderAttribute(int o) { } }
    public class DisallowMultipleComponentAttribute : Attribute { }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }

    public class Object
    {
        public string name = "";
        internal static List<Component> All = new List<Component>();
        public static T FindFirstObjectByType<T>(FindObjectsInactive inactive) where T : Component
        {
            foreach (Component c in All) { T t = c as T; if (t != null && (inactive == FindObjectsInactive.Include || ((Behaviour)(object)t).enabled)) return t; }
            return null;
        }
        public static implicit operator bool(Object o) { return o != null; }
    }
    public class Transform
    {
        public Vector3 position;
        public Vector3 forward = new Vector3(0, 0, 1);
        public Transform parent;
        public bool IsChildOf(Transform p) { for (Transform t = this; t != null; t = t.parent) if (t == p) return true; return false; }
        public Vector3 TransformPoint(Vector3 local) { return position + local; } // no rotation or scale in the stub
    }
    public class Collider : Behaviour { }
    public struct RaycastHit { public Collider collider; }
    public static class Physics
    {
        public delegate bool Handler(Vector3 origin, Vector3 dir, out RaycastHit hit, float maxDistance, int mask);
        public static Handler OnRaycast;
        public static int RayCount;
        public static bool Raycast(Vector3 origin, Vector3 dir, out RaycastHit hit, float maxDistance, int mask, QueryTriggerInteraction q)
        {
            RayCount++;
            if (OnRaycast == null) { hit = default(RaycastHit); return false; }
            return OnRaycast(origin, dir, out hit, maxDistance, mask);
        }
    }
    public class GameObject : Object
    {
        public Transform transform = new Transform();
        public List<Component> Components = new List<Component>();
        public GameObject(string n) { name = n; }
        public T GetComponent<T>() where T : class { foreach (Component c in Components) { T t = c as T; if (t != null) return t; } return null; }
        public T AddComponent<T>() where T : Behaviour, new()
        {
            T c = new T();
            c.gameObject = this; c.name = name; Components.Add(c); Object.All.Add(c);
            Component.Invoke(c, "Awake");
            c.enabled = true;
            return c;
        }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() where T : class { foreach (Component c in gameObject.Components) { T t = c as T; if (t != null) return t; } return null; }
        public bool isActiveAndEnabled { get { return ((Behaviour)this).enabled; } }
        internal static void Invoke(object o, string method)
        {
            MethodInfo m = o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (m != null) m.Invoke(o, null);
        }
    }
    public class Behaviour : Component
    {
        private bool _enabled;
        public bool enabled
        {
            get { return _enabled; }
            set
            {
                if (value == _enabled) return;
                _enabled = value;
                Invoke(this, value ? "OnEnable" : "OnDisable");
            }
        }
    }
    public class MonoBehaviour : Behaviour { }
}
