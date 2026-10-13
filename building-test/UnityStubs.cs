// Hand written stand-ins for the UnityEngine APIs the Managed Building code uses. Verification harness only.
// Simplifications: colliders are boxes whose world bounds are the axis aligned box of their corners (exact for the
// quarter turns building uses), the physics world is a flat list, activeness follows the parent chain.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    // ---- attributes ------------------------------------------------------------------------------------------
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeFieldAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrderAttribute : Attribute { public DefaultExecutionOrderAttribute(int o) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponentAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public sealed class RequireComponentAttribute : Attribute { public RequireComponentAttribute(Type t) { } }

    // ---- math ------------------------------------------------------------------------------------------------
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator *(Vector2 a, float f) { return new Vector2(a.x * f, a.y * f); }
        public static implicit operator Vector3(Vector2 v) { return new Vector3(v.x, v.y, 0); }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 down { get { return new Vector3(0, -1, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 back { get { return new Vector3(0, 0, -1); } }
        public static Vector3 right { get { return new Vector3(1, 0, 0); } }
        public static Vector3 left { get { return new Vector3(-1, 0, 0); } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator *(Vector3 a, float f) { return new Vector3(a.x * f, a.y * f, a.z * f); }
        public static Vector3 operator *(float f, Vector3 a) { return new Vector3(a.x * f, a.y * f, a.z * f); }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public float magnitude { get { return (float)Math.Sqrt(sqrMagnitude); } }
        public override string ToString() { return "(" + x + ", " + y + ", " + z + ")"; }
        public string ToString(string f) { return "(" + x.ToString(f) + ", " + y.ToString(f) + ", " + z.ToString(f) + ")"; }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity { get { return new Quaternion(0, 0, 0, 1); } }
        public static Quaternion operator *(Quaternion a, Quaternion b)
        {
            return new Quaternion(
                a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
                a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
                a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        }
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float x2 = q.x * 2, y2 = q.y * 2, z2 = q.z * 2;
            float xx = q.x * x2, yy = q.y * y2, zz = q.z * z2;
            float xy = q.x * y2, xz = q.x * z2, yz = q.y * z2;
            float wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;
            return new Vector3(
                (1 - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z,
                (xy + wz) * v.x + (1 - (xx + zz)) * v.y + (yz - wx) * v.z,
                (xz - wy) * v.x + (yz + wx) * v.y + (1 - (xx + yy)) * v.z);
        }
        private static Quaternion Axis(float deg, int axis)
        {
            double h = deg * Math.PI / 360.0; float s = (float)Math.Sin(h), c = (float)Math.Cos(h);
            return axis == 0 ? new Quaternion(s, 0, 0, c) : axis == 1 ? new Quaternion(0, s, 0, c) : new Quaternion(0, 0, s, c);
        }
        // Unity applies z, then x, then y.
        public static Quaternion Euler(float x, float y, float z) { return Axis(y, 1) * Axis(x, 0) * Axis(z, 2); }
    }

    public struct Matrix4x4
    {
        public float m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33;
        public static Matrix4x4 identity
        {
            get { var m = new Matrix4x4(); m.m00 = m.m11 = m.m22 = m.m33 = 1; return m; }
        }
        public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s)
        {
            float x2 = q.x * 2, y2 = q.y * 2, z2 = q.z * 2;
            float xx = q.x * x2, yy = q.y * y2, zz = q.z * z2;
            float xy = q.x * y2, xz = q.x * z2, yz = q.y * z2;
            float wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;
            var m = new Matrix4x4();
            m.m00 = (1 - (yy + zz)) * s.x; m.m01 = (xy - wz) * s.y; m.m02 = (xz + wy) * s.z; m.m03 = pos.x;
            m.m10 = (xy + wz) * s.x; m.m11 = (1 - (xx + zz)) * s.y; m.m12 = (yz - wx) * s.z; m.m13 = pos.y;
            m.m20 = (xz - wy) * s.x; m.m21 = (yz + wx) * s.y; m.m22 = (1 - (xx + yy)) * s.z; m.m23 = pos.z;
            m.m33 = 1;
            return m;
        }
        public static Matrix4x4 Scale(Vector3 s) { var m = new Matrix4x4(); m.m00 = s.x; m.m11 = s.y; m.m22 = s.z; m.m33 = 1; return m; }
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b)
        {
            var r = new Matrix4x4();
            float[] A = a.ToArray(), B = b.ToArray(), R = new float[16];
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { float s = 0; for (int k = 0; k < 4; k++) s += A[i * 4 + k] * B[k * 4 + j]; R[i * 4 + j] = s; }
            r.FromArray(R); return r;
        }
        public Vector3 MultiplyPoint3x4(Vector3 p)
        {
            return new Vector3(m00 * p.x + m01 * p.y + m02 * p.z + m03, m10 * p.x + m11 * p.y + m12 * p.z + m13, m20 * p.x + m21 * p.y + m22 * p.z + m23);
        }
        public Vector3 MultiplyVector(Vector3 p)
        {
            return new Vector3(m00 * p.x + m01 * p.y + m02 * p.z, m10 * p.x + m11 * p.y + m12 * p.z, m20 * p.x + m21 * p.y + m22 * p.z);
        }
        public float[] ToArray() { return new[] { m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33 }; }
        private void FromArray(float[] a)
        {
            m00 = a[0]; m01 = a[1]; m02 = a[2]; m03 = a[3]; m10 = a[4]; m11 = a[5]; m12 = a[6]; m13 = a[7];
            m20 = a[8]; m21 = a[9]; m22 = a[10]; m23 = a[11]; m30 = a[12]; m31 = a[13]; m32 = a[14]; m33 = a[15];
        }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static implicit operator Color(Color32 c) { return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); }
    }
    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public bool Contains(Vector2 p) { return p.x >= x && p.x < x + width && p.y >= y && p.y < y + height; }
    }

    public static class Mathf
    {
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }
        public static float Abs(float v) { return Math.Abs(v); }
    }

    // ---- object model ----------------------------------------------------------------------------------------
    public class Object
    {
        internal static readonly List<GameObject> Registry = new List<GameObject>();
        public string name;
        internal bool Destroyed;
        public static implicit operator bool(Object o) { return o != null && !o.Destroyed; }
        public static bool operator ==(Object a, Object b)
        {
            bool an = ReferenceEquals(a, null) || a.Destroyed, bn = ReferenceEquals(b, null) || b.Destroyed;
            if (an || bn) return an && bn;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) { return !(a == b); }
        public override bool Equals(object o) { return ReferenceEquals(this, o); }
        public override int GetHashCode() { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this); }

        public static void Destroy(Object o)
        {
            var go = o as GameObject;
            if (go != null) { go.DestroyRecursive(); return; }
            var c = o as Component;
            if (c != null) { c.gameObject.Components.Remove(c); c.Destroyed = true; return; }
            if (o != null) o.Destroyed = true;
        }
        public static void DestroyImmediate(Object o) { Destroy(o); }
        public static T FindObjectOfType<T>() where T : Object { return FindFirstObjectByType<T>(); }
        public static T FindFirstObjectByType<T>() where T : Object
        {
            foreach (GameObject go in Registry.ToArray())
                foreach (Component c in go.Components.ToArray())
                    if (c is T && !c.Destroyed) return (T)(object)c;
            return null;
        }
        public static T FindFirstObjectByType<T>(FindObjectsInactive inactive) where T : Object { return FindFirstObjectByType<T>(); }
    }
    public enum FindObjectsInactive { Exclude, Include }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() { return new T(); }
    }

    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() where T : class
        {
            foreach (Component c in gameObject.Components) if (c is T) return (T)(object)c;
            return null;
        }
        public bool TryGetComponent<T>(out T component) where T : class
        {
            component = GetComponent<T>();
            return component != null;
        }
        public T GetComponentInChildren<T>() where T : class { return GetComponent<T>(); }
        public T[] GetComponentsInChildren<T>() where T : class { var r = new List<T>(); var c = GetComponent<T>(); if (c != null) r.Add(c); return r.ToArray(); }
    }

    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { }

    public class Transform : Component
    {
        private Transform _parent;
        private readonly List<Transform> _children = new List<Transform>();
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Transform parent { get { return _parent; } }
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
        public void SetParent(Transform p, bool worldPositionStays)
        {
            if (_parent != null) _parent._children.Remove(this);
            _parent = p;
            if (p != null) p._children.Add(this);
        }
        public bool IsChildOf(Transform p)
        {
            for (Transform t = this; t != null; t = t._parent) if (ReferenceEquals(t, p)) return true;
            return false;
        }
        internal IEnumerable<Transform> Children { get { return _children; } }
        public Vector3 eulerAngles { get { return Vector3.zero; } }
    }

    public class GameObject : Object
    {
        internal readonly List<Component> Components = new List<Component>();
        public int layer;
        public string tag;
        private bool _self = true;
        public Transform transform;
        public GameObject() : this("GameObject") { }
        public GameObject(string name)
        {
            this.name = name;
            var t = new Transform { gameObject = this };
            transform = t;
            Components.Add(t);
            Registry.Add(this);
        }
        public bool activeSelf { get { return _self; } }
        public bool activeInHierarchy
        {
            get
            {
                if (Destroyed) return false;
                for (Transform t = transform; t != null; t = t.parent) if (!t.gameObject._self) return false;
                return true;
            }
        }
        public void SetActive(bool a) { _self = a; }
        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T { gameObject = this };
            Components.Add(c);
            var init = c as IStubLifecycle;
            if (init != null) init.OnAdded();
            return c;
        }
        public T GetComponent<T>() where T : class
        {
            foreach (Component c in Components) if (c is T) return (T)(object)c;
            return null;
        }
        public T GetComponentInChildren<T>() where T : class { return GetComponent<T>(); }
        public T[] GetComponentsInChildren<T>() where T : class { var r = new List<T>(); var c = GetComponent<T>(); if (c != null) r.Add(c); return r.ToArray(); }
        internal void DestroyRecursive()
        {
            foreach (Transform ch in new List<Transform>(transform.Children)) ch.gameObject.DestroyRecursive();
            Destroyed = true;
            foreach (Component c in Components) c.Destroyed = true;
            Registry.Remove(this);
        }
    }
    public interface IStubLifecycle { void OnAdded(); }

    // ---- physics ---------------------------------------------------------------------------------------------
    public struct LayerMask
    {
        public int value;
        public static implicit operator int(LayerMask m) { return m.value; }
        public static implicit operator LayerMask(int v) { return new LayerMask { value = v }; }
    }
    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }
    public struct Ray
    {
        public Vector3 origin, direction;
        public Ray(Vector3 o, Vector3 d) { origin = o; direction = d; }
    }
    public struct Bounds { public Vector3 min, max; }

    public class Collider : Component
    {
        public bool isTrigger;
        internal Bounds WorldBounds()
        {
            var box = this as BoxCollider;
            Vector3 h = box.size * 0.5f;
            Vector3 lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), hi = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                Vector3 p = transform.rotation * c + transform.position;
                lo = new Vector3(Math.Min(lo.x, p.x), Math.Min(lo.y, p.y), Math.Min(lo.z, p.z));
                hi = new Vector3(Math.Max(hi.x, p.x), Math.Max(hi.y, p.y), Math.Max(hi.z, p.z));
            }
            return new Bounds { min = lo, max = hi };
        }
    }
    public class BoxCollider : Collider { public Vector3 size = Vector3.one; }
    public struct RaycastHit { public Collider collider; public float distance; public Vector3 point; public Vector3 normal; }

    public static class Physics
    {
        private static IEnumerable<Collider> Active(int mask, QueryTriggerInteraction q)
        {
            foreach (GameObject go in Object.Registry.ToArray())
            {
                if (!go.activeInHierarchy || (mask & (1 << go.layer)) == 0) continue;
                foreach (Component c in go.Components.ToArray())
                {
                    var col = c as Collider;
                    if (col != null && !(q == QueryTriggerInteraction.Ignore && col.isTrigger)) yield return col;
                }
            }
        }
        private static bool Slab(Ray ray, Bounds b, out float t, out Vector3 normal)
        {
            float tmin = 0, tmax = float.MaxValue; t = 0; normal = Vector3.up;
            float[] o = { ray.origin.x, ray.origin.y, ray.origin.z }, d = { ray.direction.x, ray.direction.y, ray.direction.z };
            float[] lo = { b.min.x, b.min.y, b.min.z }, hi = { b.max.x, b.max.y, b.max.z };
            for (int i = 0; i < 3; i++)
            {
                if (Math.Abs(d[i]) < 1e-9f) { if (o[i] < lo[i] || o[i] > hi[i]) return false; continue; }
                float t1 = (lo[i] - o[i]) / d[i], t2 = (hi[i] - o[i]) / d[i];
                if (t1 > t2) { float s = t1; t1 = t2; t2 = s; }
                if (t1 > tmin) tmin = t1;
                if (t2 < tmax) tmax = t2;
                if (tmin > tmax) return false;
            }
            t = tmin;
            return true;
        }
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance, int layerMask, QueryTriggerInteraction q)
        {
            hit = default(RaycastHit);
            float best = float.MaxValue; bool any = false;
            foreach (Collider c in Active(layerMask, q))
            {
                float t; Vector3 n;
                if (Slab(new Ray(origin, direction), c.WorldBounds(), out t, out n) && t <= maxDistance && t < best)
                {
                    best = t; any = true;
                    hit = new RaycastHit { collider = c, distance = t, point = origin + direction * t, normal = n };
                }
            }
            return any;
        }
        public static int RaycastNonAlloc(Ray ray, RaycastHit[] results, float maxDistance, int layerMask, QueryTriggerInteraction q)
        {
            var list = new List<RaycastHit>();
            foreach (Collider c in Active(layerMask, q))
            {
                float t; Vector3 n;
                if (Slab(ray, c.WorldBounds(), out t, out n) && t <= maxDistance)
                    list.Add(new RaycastHit { collider = c, distance = t, point = ray.origin + ray.direction * t, normal = n });
            }
            // Unity returns the hits in no particular order. Reverse so a caller that assumes order is caught.
            list.Reverse();
            int count = Math.Min(list.Count, results.Length);
            for (int i = 0; i < count; i++) results[i] = list[i];
            return count;
        }
        public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation, int mask, QueryTriggerInteraction q)
        {
            int count = 0;
            foreach (Collider c in Active(mask, q))
            {
                Bounds b = c.WorldBounds();
                bool overlap = center.x + halfExtents.x > b.min.x && center.x - halfExtents.x < b.max.x
                    && center.y + halfExtents.y > b.min.y && center.y - halfExtents.y < b.max.y
                    && center.z + halfExtents.z > b.min.z && center.z - halfExtents.z < b.max.z;
                if (overlap && count < results.Length) results[count++] = c;
            }
            return count;
        }
        public static bool Linecast(Vector3 a, Vector3 b, out RaycastHit hit, int mask, QueryTriggerInteraction q) { hit = default(RaycastHit); return false; }
    }

    // ---- rendering -------------------------------------------------------------------------------------------
    public class Shader : Object
    {
        public static HashSet<string> Known = new HashSet<string> { "Universal Render Pipeline/Lit", "Standard", "Sprites/Default" };
        public static Shader Find(string n) { return Known.Contains(n) ? new Shader { name = n } : null; }
    }
    public class Material : Object
    {
        public Shader shader;
        public bool enableInstancing;
        public readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>();
        public static HashSet<string> LitProps = new HashSet<string> { "_BaseColor" };
        public Material(Shader s) { shader = s; }
        public Material(Material m) { shader = m.shader; foreach (var kv in m.Colors) Colors[kv.Key] = kv.Value; }
        public bool HasProperty(string p) { return LitProps.Contains(p) || p == "_Color"; }
        public void SetColor(string p, Color c) { Colors[p] = c; }
    }
    public class Mesh : Object
    {
        public Vector3[] vertices, normals; public int[] triangles; public Color32[] colors32;
        public void RecalculateBounds() { }
    }
    public class MaterialPropertyBlock { }
    public class Camera : Component
    {
        public static Camera main;
        public Rect pixelRect = new Rect(0, 0, 1920, 1080);
        public Func<Vector3, Ray> RayFactory;
        public Ray ScreenPointToRay(Vector3 p) { return RayFactory(p); }
    }
    public class Renderer : Component { public Rendering.ShadowCastingMode shadowCastingMode; }
    public class Texture2D : Object { public static Texture2D whiteTexture = new Texture2D(); }
    public class Font : Object { }
    public static class Resources
    {
        public static bool HasLegacyRuntime = true;
        public static T GetBuiltinResource<T>(string path) where T : Object
        {
            if (!HasLegacyRuntime) throw new ArgumentException("The resource " + path + " could not be loaded from the resource file!");
            return (T)(object)new Font { name = path };
        }
    }

    public struct DrawCall { public string Kind; public int Count; public int Layer; public Mesh Mesh; public Material Material; public Matrix4x4 First; public Matrix4x4[] Source; }
    public static class Graphics
    {
        public static readonly List<DrawCall> Calls = new List<DrawCall>();
        public static void DrawMeshInstanced(Mesh mesh, int submesh, Material material, Matrix4x4[] matrices, int count,
            MaterialPropertyBlock block, Rendering.ShadowCastingMode shadows, bool receive, int layer)
        {
            if (count < 0 || count > 1023) throw new ArgumentException("Instance count must be between 0 and 1023");
            if (count > matrices.Length) throw new ArgumentException("count exceeds matrices.Length");
            Calls.Add(new DrawCall { Kind = "instanced", Count = count, Layer = layer, Mesh = mesh, Material = material, First = count > 0 ? matrices[0] : default(Matrix4x4), Source = matrices });
        }
        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer)
        {
            Calls.Add(new DrawCall { Kind = "mesh", Count = 1, Layer = layer, Mesh = mesh, Material = material, First = matrix });
        }
    }

    // ---- misc ------------------------------------------------------------------------------------------------
    public static class Debug
    {
        public static readonly List<string> Warnings = new List<string>(), Errors = new List<string>();
        public static void LogWarning(object m) { Warnings.Add(m.ToString()); }
        public static void LogWarning(object m, Object ctx) { Warnings.Add(m.ToString()); }
        public static void LogError(object m) { Errors.Add(m.ToString()); }
    }
    public static class Application { public static bool isPlaying = true; }
    public enum CursorLockMode { None, Locked, Confined }
    public static class Cursor { public static CursorLockMode lockState; public static bool visible = true; }
    public static class Screen { public static int width = 1920, height = 1080; }
    public static class Time { public static int frameCount; }

    // IMGUI, only what ManagedCameraRig draws.
    public class GUIStyle { public int fontSize; public GUIStyle() { } public GUIStyle(GUIStyle o) { } }
    public class GUISkin { public GUIStyle label = new GUIStyle(); }
    public static class GUI
    {
        public static GUISkin skin = new GUISkin();
        public static Color color;
        public static void Label(Rect r, string s) { }
        public static void Label(Rect r, string s, GUIStyle st) { }
        public static int TextureDraws;
        public static void DrawTexture(Rect r, Texture2D t) { TextureDraws++; }
    }
    public class GUILayoutOption { }
    public static class GUILayout
    {
        public static void BeginArea(Rect r) { }
        public static void EndArea() { }
        public static void BeginHorizontal() { }
        public static void EndHorizontal() { }
        public static void Label(string s, params GUILayoutOption[] o) { }
        public static bool Toggle(bool v, string s, params GUILayoutOption[] o) { return v; }
        public static GUILayoutOption Width(float w) { return null; }
    }
}

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
}

namespace UnityEngine.EventSystems
{
    public class EventSystem : UnityEngine.MonoBehaviour { }
    public class BaseInputModule : UnityEngine.MonoBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
}

namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule { }
}

namespace UnityEngine.InputSystem
{
    public enum Key { None = 0, Space = 1, B = 38, R = 50, Escape = 60, V = 53, Tab = 3, F = 42, IMESelected = 110 }

    public class ButtonControl
    {
        public bool wasPressedThisFrame, isPressed;
    }
    public class KeyControl : ButtonControl { }
    public class Vector2Control { public Vector2 Value; public Vector2 ReadValue() { return Value; } }
    public struct ReadOnlyArray<T>
    {
        private readonly T[] _a;
        public ReadOnlyArray(T[] a) { _a = a; }
        public int Count { get { return _a.Length; } }
        public T this[int i] { get { return _a[i]; } }
    }

    public class Keyboard
    {
        public static Keyboard current;
        private readonly KeyControl[] _keys = new KeyControl[109];
        public Keyboard() { for (int i = 0; i < _keys.Length; i++) _keys[i] = new KeyControl(); }
        public KeyControl this[Key key]
        {
            get
            {
                int index = (int)key - 1;
                if (index < 0 || index >= _keys.Length) throw new ArgumentOutOfRangeException("key");
                return _keys[index];
            }
        }
        public ReadOnlyArray<KeyControl> allKeys { get { return new ReadOnlyArray<KeyControl>(_keys); } }
        public KeyControl rKey { get { return this[Key.R]; } }
        public KeyControl escapeKey { get { return this[Key.Escape]; } }
        public KeyControl vKey { get { return this[Key.V]; } }
        public KeyControl leftShiftKey = new KeyControl();
        public KeyControl rightShiftKey = new KeyControl();
    }

    public class Mouse
    {
        public static Mouse current;
        public ButtonControl leftButton = new ButtonControl(), rightButton = new ButtonControl();
        public Vector2Control position = new Vector2Control(), scroll = new Vector2Control();
    }

    public class Gamepad
    {
        public static Gamepad current;
        public ButtonControl rightStickButton = new ButtonControl();
    }
}
