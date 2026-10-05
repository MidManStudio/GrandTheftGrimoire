// Hand written stand-ins for the few UnityEngine and UnityEngine.AI APIs that the Managed NPC code uses. Test
// infrastructure only: it models the behavior the checks rely on and nothing else. Known simplifications: rotation
// is yaw only, colliders are axis aligned boxes, the NavMesh is a callback and the NavMeshAgent moves in a straight
// line when the test calls Simulate.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 operator *(Vector2 a, float f) { return new Vector2(a.x * f, a.y * f); }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float f) { return new Vector3(a.x * f, a.y * f, a.z * f); }
        public static Vector3 operator /(Vector3 a, float f) { return new Vector3(a.x / f, a.y / f, a.z / f); }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public float magnitude { get { return (float)Math.Sqrt(sqrMagnitude); } }
        public Vector3 normalized { get { float m = magnitude; return m > 1e-6f ? this / m : zero; } }
    }
    public struct Quaternion
    {
        public float yaw; // degrees
        public static Quaternion identity { get { return default(Quaternion); } }
        public static Quaternion AngleAxis(float angle, Vector3 axis) { return new Quaternion { yaw = angle }; }
        public static Quaternion LookRotation(Vector3 d) { return new Quaternion { yaw = (float)(Math.Atan2(d.x, d.z) * 180.0 / Math.PI) }; }
        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegrees)
        {
            float delta = Mathf.DeltaAngle(from.yaw, to.yaw);
            float step = Math.Abs(delta) <= maxDegrees ? delta : Math.Sign(delta) * maxDegrees;
            return new Quaternion { yaw = from.yaw + step };
        }
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            double r = q.yaw * Math.PI / 180.0, c = Math.Cos(r), s = Math.Sin(r);
            return new Vector3((float)(v.x * c + v.z * s), v.y, (float)(-v.x * s + v.z * c));
        }
    }
    public struct Bounds
    {
        public Vector3 center, extents;
        public Vector3 ClosestPoint(Vector3 p)
        {
            return new Vector3(
                Mathf.Clamp(p.x, center.x - extents.x, center.x + extents.x),
                Mathf.Clamp(p.y, center.y - extents.y, center.y + extents.y),
                Mathf.Clamp(p.z, center.z - extents.z, center.z + extents.z));
        }
    }
    public struct Color
    {
        public float r, g, b, a;
        public static Color red = new Color(1, 0, 0, 1), yellow = new Color(1, 1, 0, 1);
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }
    public static class Application { public static bool isPlaying; }
    public class GUIStyleState { public Color textColor; }
    public class GUIStyle { public GUIStyleState normal = new GUIStyleState(); public int fontSize; }
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
        public static int RoundToInt(float f) { return (int)Math.Round(f, MidpointRounding.ToEven); }
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
        public static float DeltaAngle(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }
    }
    public static class Time { public static float time; public static float deltaTime = 0.02f; }
    public static class Random
    {
        private static System.Random _rng = new System.Random(1);
        public static void Seed(int seed) { _rng = new System.Random(seed); }
        public static Vector2 insideUnitCircle
        {
            get
            {
                double a = _rng.NextDouble() * Math.PI * 2.0, r = Math.Sqrt(_rng.NextDouble());
                return new Vector2((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r));
            }
        }
    }
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
    public enum RuntimeInitializeLoadType { SubsystemRegistration, BeforeSceneLoad }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class SerializeFieldAttribute : Attribute { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class DefaultExecutionOrderAttribute : Attribute { public DefaultExecutionOrderAttribute(int o) { } }
    public class DisallowMultipleComponentAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RequireComponentAttribute : Attribute { public RequireComponentAttribute(Type t) { } }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }

    public class Object
    {
        private string _name = "";
        public virtual string name { get { return _name; } set { _name = value; } }
        internal static List<Component> All = new List<Component>();
        public static List<KeyValuePair<Object, float>> Destroyed = new List<KeyValuePair<Object, float>>();
        public static void Destroy(Object o, float delay) { Destroyed.Add(new KeyValuePair<Object, float>(o, delay)); }
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
        public Quaternion rotation;
        public Transform parent;
        public GameObject gameObject;
        public Vector3 forward { get { return rotation * new Vector3(0, 0, 1); } }
        public bool IsChildOf(Transform p) { for (Transform t = this; t != null; t = t.parent) if (t == p) return true; return false; }
        public Vector3 TransformPoint(Vector3 local) { return position + rotation * local; }
    }
    public class Collider : Behaviour
    {
        public Vector3 center, extents = new Vector3(0.5f, 0.5f, 0.5f);
        public Bounds bounds { get { return new Bounds { center = transform.position + center, extents = extents }; } }
    }
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
        public static int OverlapSphereNonAlloc(Vector3 center, float radius, Collider[] results, int mask, QueryTriggerInteraction q)
        {
            int n = 0;
            foreach (Component c in Object.All)
            {
                Collider col = c as Collider;
                if (col == null || !col.enabled) continue;
                if ((col.bounds.ClosestPoint(center) - center).magnitude > radius) continue;
                if (n >= results.Length) break;
                results[n++] = col;
            }
            return n;
        }
    }
    public class GameObject : Object
    {
        public Transform transform;
        public List<Component> Components = new List<Component>();
        public GameObject(string n) { name = n; transform = new Transform { gameObject = this }; }
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
        public override string name { get { return gameObject != null ? gameObject.name : base.name; } set { if (gameObject != null) gameObject.name = value; else base.name = value; } }
        public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
        public T GetComponentInParent<T>() where T : class
        {
            for (Transform t = transform; t != null; t = t.parent) { T found = t.gameObject.GetComponent<T>(); if (found != null) return found; }
            return null;
        }
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

namespace UnityEngine.AI
{
    public struct NavMeshHit { public Vector3 position; }
    public class NavMeshPath
    {
        public Vector3[] corners = new Vector3[0];
        public int GetCornersNonAlloc(Vector3[] results)
        {
            int n = Math.Min(results.Length, corners.Length);
            for (int i = 0; i < n; i++) results[i] = corners[i];
            return n;
        }
    }
    public static class NavMesh
    {
        public const int AllAreas = -1;
        /// <summary>Which points count as walkable. Null means everything.</summary>
        public static Func<Vector3, bool> IsWalkable;
        public static bool SamplePosition(Vector3 source, out NavMeshHit hit, float maxDistance, int areaMask)
        {
            hit = new NavMeshHit { position = source };
            return IsWalkable == null || IsWalkable(source);
        }
    }
    public class NavMeshAgent : Behaviour
    {
        public bool isOnNavMesh = true;
        public bool isStopped;
        public float speed = 3.5f;
        public float stoppingDistance;
        public bool pathPending;
        public float remainingDistance;
        public Vector3 destination;
        public bool hasDestination;
        public int SetDestinationCalls;
        public bool hasPath { get { return hasDestination; } }
        public NavMeshPath path
        {
            get { return new NavMeshPath { corners = new[] { transform.position, destination } }; }
        }

        public bool SetDestination(Vector3 d)
        {
            destination = d; hasDestination = true; SetDestinationCalls++;
            Vector3 flat = new Vector3(d.x - transform.position.x, 0, d.z - transform.position.z);
            remainingDistance = flat.magnitude;
            return true;
        }

        // Test only: a straight-line mover that stops at the stopping distance.
        public void Simulate(float dt)
        {
            if (!isOnNavMesh || isStopped || !hasDestination) return;
            Vector3 flat = new Vector3(destination.x - transform.position.x, 0, destination.z - transform.position.z);
            float dist = flat.magnitude;
            if (dist > stoppingDistance)
            {
                float step = Mathf.Min(speed * dt, dist - stoppingDistance);
                transform.position = transform.position + flat.normalized * step;
                dist -= step;
            }
            remainingDistance = dist;
        }
    }
}
