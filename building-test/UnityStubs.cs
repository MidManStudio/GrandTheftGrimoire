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
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrderAttribute : Attribute { public int order { get; private set; } public DefaultExecutionOrderAttribute(int o) { order = o; } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponentAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class ExecuteAlwaysAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public sealed class RequireComponentAttribute : Attribute { public RequireComponentAttribute(Type t) { } }

    // ---- math ------------------------------------------------------------------------------------------------
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y); } }
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector2(x / m, y / m) : new Vector2(0, 0); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator *(Vector2 a, float f) { return new Vector2(a.x * f, a.y * f); }
        public static Vector2 operator /(Vector2 a, float f) { return new Vector2(a.x / f, a.y / f); }
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
        public static Vector3 operator /(Vector3 a, float f) { return new Vector3(a.x / f, a.y / f, a.z / f); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector3(x / m, y / m, z / m) : new Vector3(0, 0, 0); } }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
        public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = t < 0f ? 0f : (t > 1f ? 1f : t); return a + (b - a) * t; }
        public static float Angle(Vector3 from, Vector3 to)
        {
            float d = (float)Math.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
            if (d < 1e-15f) return 0f;
            float c = Dot(from, to) / d; c = c < -1f ? -1f : (c > 1f ? 1f : c);
            return (float)(Math.Acos(c) * 180.0 / Math.PI);
        }
        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
        {
            Vector3 d = target - current; float m = d.magnitude;
            return m <= maxDistanceDelta || m < 1e-5f ? target : current + d / m * maxDistanceDelta;
        }
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
        public static Quaternion AngleAxis(float angle, Vector3 axis)
        {
            axis = axis.normalized; double h = angle * Math.PI / 360.0; float s = (float)Math.Sin(h);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, (float)Math.Cos(h));
        }
        public static Quaternion LookRotation(Vector3 forward) { return LookRotation(forward, Vector3.up); }
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            Vector3 f = forward.normalized; Vector3 r = Vector3.Cross(up, f).normalized; Vector3 u = Vector3.Cross(f, r);
            float m00 = r.x, m01 = u.x, m02 = f.x, m10 = r.y, m11 = u.y, m12 = f.y, m20 = r.z, m21 = u.z, m22 = f.z;
            float tr = m00 + m11 + m22;
            if (tr > 0f) { float k = (float)Math.Sqrt(tr + 1f) * 2f; return new Quaternion((m21 - m12) / k, (m02 - m20) / k, (m10 - m01) / k, 0.25f * k); }
            if (m00 > m11 && m00 > m22) { float k = (float)Math.Sqrt(1f + m00 - m11 - m22) * 2f; return new Quaternion(0.25f * k, (m01 + m10) / k, (m02 + m20) / k, (m21 - m12) / k); }
            if (m11 > m22) { float k = (float)Math.Sqrt(1f + m11 - m00 - m22) * 2f; return new Quaternion((m01 + m10) / k, 0.25f * k, (m12 + m21) / k, (m02 - m20) / k); }
            { float k = (float)Math.Sqrt(1f + m22 - m00 - m11) * 2f; return new Quaternion((m02 + m20) / k, (m12 + m21) / k, 0.25f * k, (m10 - m01) / k); }
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
        public static bool operator ==(Matrix4x4 a, Matrix4x4 b)
        {
            float[] A = a.ToArray(), B = b.ToArray();
            for (int i = 0; i < 16; i++) if (A[i] != B[i]) return false;
            return true;
        }
        public static bool operator !=(Matrix4x4 a, Matrix4x4 b) { return !(a == b); }
        public override bool Equals(object obj) { return obj is Matrix4x4 && this == (Matrix4x4)obj; }
        public override int GetHashCode() { return m00.GetHashCode() ^ (m13.GetHashCode() << 1) ^ (m23.GetHashCode() << 2); }
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
        public static Color black { get { return new Color(0, 0, 0, 1); } }
        public static Color clear { get { return new Color(0, 0, 0, 0); } }
        public static Color red { get { return new Color(1, 0, 0, 1); } }
        public static Color yellow { get { return new Color(1, 0.92156863f, 0.015686275f, 1); } }
        public static Color Lerp(Color a, Color b, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }
        public static implicit operator Color(Color32 c) { return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); }
        public static implicit operator Color32(Color c) { return new Color32((byte)Math.Round(c.r * 255f), (byte)Math.Round(c.g * 255f), (byte)Math.Round(c.b * 255f), (byte)Math.Round(c.a * 255f)); }
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
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }
        public static int Abs(int v) { return Math.Abs(v); }
        public static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float Sqrt(float v) { return (float)Math.Sqrt(v); }
        public static float Sin(float v) { return (float)Math.Sin(v); }
        public static float Cos(float v) { return (float)Math.Cos(v); }
        public static float Acos(float v) { return (float)Math.Acos(v); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Pow(float a, float b) { return (float)Math.Pow(a, b); }
        public static float Repeat(float t, float length) { return Clamp(t - Floor(t / length) * length, 0f, length); }
        public static float DeltaAngle(float current, float target)
        {
            float delta = Repeat(target - current, 360f);
            if (delta > 180f) delta -= 360f;
            return delta;
        }
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }
        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = DeltaAngle(current, target);
            if (-maxDelta < delta && delta < maxDelta) return target;
            target = current + delta;
            return MoveTowards(current, target, maxDelta);
        }
        public static float Floor(float v) { return (float)Math.Floor(v); }
        public static float Ceil(float v) { return (float)Math.Ceiling(v); }
        public static int FloorToInt(float v) { return (int)Math.Floor(v); }
        public static int CeilToInt(float v) { return (int)Math.Ceiling(v); }
        public static int RoundToInt(float v) { return (int)Math.Round(v, MidpointRounding.ToEven); }
        public static bool Approximately(float a, float b) { return Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), 1.121039E-44f * 8f); }
    }

    // ---- object model ----------------------------------------------------------------------------------------
    public class Object
    {
        internal static readonly List<GameObject> Registry = new List<GameObject>();
        public string name;
        public HideFlags hideFlags;
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
        // A component reports the name of its GameObject, as in Unity.
        public new string name { get { return gameObject != null ? gameObject.name : base.name; } set { base.name = value; } }
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
        public T GetComponentInParent<T>() where T : class
        {
            for (Transform t = transform; t != null; t = t.parent) { T c = t.GetComponent<T>(); if (c != null) return c; }
            return null;
        }
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
        public Transform Find(string childName)
        {
            foreach (Transform ch in _children) if (ch.gameObject.name == childName) return ch;
            return null;
        }
        public Vector3 eulerAngles { get { return Vector3.zero; } }
        public Vector3 localPosition;
        public Vector3 localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 forward { get { return rotation * Vector3.forward; } }
        public Vector3 right { get { return rotation * Vector3.right; } }
        public Vector3 up { get { return rotation * Vector3.up; } }
        public Vector3 TransformPoint(Vector3 p) { return position + rotation * p; }
    }

    public class GameObject : Object
    {
        internal readonly List<Component> Components = new List<Component>();
        public int layer;
        public string tag;
        private bool _self = true;
        public Transform transform;
        public GameObject() : this("GameObject") { }
        public GameObject(string name, params Type[] components)
        {
            this.name = name;
            bool rect = false;
            foreach (Type c in components) { if (typeof(RectTransform).IsAssignableFrom(c)) rect = true; }
            var t = rect ? new RectTransform { gameObject = this } : new Transform { gameObject = this };
            transform = t;
            Components.Add(t);
            Registry.Add(this);
            foreach (Type c in components)
            {
                if (typeof(Transform).IsAssignableFrom(c)) continue;
                var comp = (Component)Activator.CreateInstance(c);
                comp.gameObject = this;
                Components.Add(comp);
                var init = comp as IStubLifecycle;
                if (init != null) init.OnAdded();
            }
        }
        public static GameObject CreatePrimitive(PrimitiveType type)
        {
            var go = new GameObject(type.ToString());
            go.AddComponent<BoxCollider>();
            return go;
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
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class
        {
            var r = new List<T>();
            AddComponentsBelow(this, includeInactive, r);
            return r.ToArray();
        }
        private static void AddComponentsBelow<T>(GameObject go, bool includeInactive, List<T> into) where T : class
        {
            if (!includeInactive && !go._self) return;
            foreach (Component c in go.Components) if (c is T && !c.Destroyed) into.Add((T)(object)c);
            foreach (Transform ch in go.transform.Children) AddComponentsBelow(ch.gameObject, includeInactive, into);
        }
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
    public struct Bounds
    {
        public Vector3 min, max;
        public Bounds(Vector3 center, Vector3 size) { min = center - size * 0.5f; max = center + size * 0.5f; }
        public Vector3 center { get { return (min + max) * 0.5f; } }
        public Vector3 size { get { return max - min; } }
        public Vector3 extents { get { return (max - min) * 0.5f; } }
        public Vector3 ClosestPoint(Vector3 p)
        {
            return new Vector3(Math.Min(Math.Max(p.x, min.x), max.x), Math.Min(Math.Max(p.y, min.y), max.y), Math.Min(Math.Max(p.z, min.z), max.z));
        }
    }

    public class Collider : Component
    {
        public bool isTrigger;
        public bool enabled = true;
        public Bounds bounds { get { return WorldBounds(); } }
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
        public static Vector3 gravity = new Vector3(0f, -9.81f, 0f);
        public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask, QueryTriggerInteraction q)
        {
            int count = 0;
            foreach (Collider c in Active(layerMask, q))
            {
                Bounds b = c.WorldBounds();
                float dx = Math.Max(Math.Max(b.min.x - position.x, 0f), position.x - b.max.x);
                float dy = Math.Max(Math.Max(b.min.y - position.y, 0f), position.y - b.max.y);
                float dz = Math.Max(Math.Max(b.min.z - position.z, 0f), position.z - b.max.z);
                if (dx * dx + dy * dy + dz * dz <= radius * radius && count < results.Length) results[count++] = c;
            }
            return count;
        }
        public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hit, float maxDistance, int layerMask, QueryTriggerInteraction q)
        {
            // A sphere cast is a ray against bounds grown by the radius, which is exact enough for the checks.
            hit = default(RaycastHit);
            float best = float.MaxValue; bool any = false;
            foreach (Collider c in Active(layerMask, q))
            {
                Bounds b = c.WorldBounds();
                var grown = new Bounds { min = b.min - new Vector3(radius, radius, radius), max = b.max + new Vector3(radius, radius, radius) };
                float t; Vector3 n;
                if (Slab(new Ray(origin, direction), grown, out t, out n) && t <= maxDistance && t < best)
                {
                    best = t; any = true;
                    hit = new RaycastHit { collider = c, distance = t, point = origin + direction * t, normal = n };
                }
            }
            return any;
        }
        public static bool Linecast(Vector3 a, Vector3 b, out RaycastHit hit, int mask, QueryTriggerInteraction q) { hit = default(RaycastHit); return false; }
    }

    // ---- rendering -------------------------------------------------------------------------------------------
    public class Shader : Object
    {
        public bool isSupported = true;
        public static int PropertyToID(string name) { return name.GetHashCode(); }
        public static HashSet<string> Known = new HashSet<string> { "Universal Render Pipeline/Lit", "Standard", "Sprites/Default" };
        public static Shader Find(string n) { return Known.Contains(n) ? new Shader { name = n } : null; }
    }
    public class Material : Object
    {
        public Shader shader;
        public bool enableInstancing;
        public Color color { get { Color c; return Colors.TryGetValue("_Color", out c) ? c : Color.white; } set { Colors["_Color"] = value; } }
        public readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>();
        public readonly Dictionary<string, float> Floats = new Dictionary<string, float>();
        public static HashSet<string> LitProps = new HashSet<string> { "_BaseColor" };
        public Material(Shader s) { shader = s; }
        public Material(Material m) { shader = m.shader; foreach (var kv in m.Colors) Colors[kv.Key] = kv.Value; }
        public bool HasProperty(string p) { return LitProps.Contains(p) || p == "_Color"; }
        public void SetColor(string p, Color c) { Colors[p] = c; }
        public void SetFloat(string p, float v) { Floats[p] = v; }
        public readonly Dictionary<string, Texture> Textures = new Dictionary<string, Texture>();
        public void SetTexture(string p, Texture t) { Textures[p] = t; }
    }
    public class Mesh : Object
    {
        public Vector3[] vertices, normals; public int[] triangles; public Color32[] colors32;
        public Bounds bounds;
        public Rendering.IndexFormat indexFormat;
        public readonly Vector2[][] UvChannels = new Vector2[8][];
        public int subMeshCount = 1;
        public void RecalculateBounds() { }
        public void MarkDynamic() { }
        public void Clear() { vertices = null; triangles = null; colors32 = null; }
        public void SetVertices(Vector3[] inVertices, int start, int length) { vertices = new Vector3[length]; Array.Copy(inVertices, start, vertices, 0, length); }
        public void SetUVs(int channel, Vector2[] uvs) { UvChannels[channel] = uvs; }
        public void SetColors(Color32[] inColors, int start, int length) { colors32 = new Color32[length]; Array.Copy(inColors, start, colors32, 0, length); }
        public void SetTriangles(int[] inTriangles, int start, int length, int submesh) { triangles = new int[length]; Array.Copy(inTriangles, start, triangles, 0, length); }
    }
    public class MaterialPropertyBlock
    {
        public readonly Dictionary<int, float> Floats = new Dictionary<int, float>();
        public void SetFloat(int nameID, float value) { Floats[nameID] = value; }
        public void SetFloat(string name, float value) { Floats[Shader.PropertyToID(name)] = value; }
        public float GetFloat(int nameID) { float v; return Floats.TryGetValue(nameID, out v) ? v : 0f; }
        public readonly Dictionary<int, float[]> FloatArrays = new Dictionary<int, float[]>();
        public void SetFloatArray(int nameID, float[] values) { FloatArrays[nameID] = values; }
        public void SetFloatArray(string name, float[] values) { FloatArrays[Shader.PropertyToID(name)] = values; }
        public readonly Dictionary<int, Vector4> Vectors = new Dictionary<int, Vector4>();
        public void SetVector(int nameID, Vector4 value) { Vectors[nameID] = value; }
        public void SetVector(string name, Vector4 value) { Vectors[Shader.PropertyToID(name)] = value; }
        public readonly Dictionary<int, Vector4[]> VectorArrays = new Dictionary<int, Vector4[]>();
        public void SetVectorArray(int nameID, Vector4[] values) { VectorArrays[nameID] = values; }
        public void SetVectorArray(string name, Vector4[] values) { VectorArrays[Shader.PropertyToID(name)] = values; }
    }
    public class Camera : Component
    {
        public static Camera main;
        public Rect pixelRect = new Rect(0, 0, 1920, 1080);
        public Func<Vector3, Ray> RayFactory;
        public Ray ScreenPointToRay(Vector3 p) { return RayFactory(p); }
        public Ray ViewportPointToRay(Vector3 p) { return RayFactory(new Vector3(p.x * pixelRect.width, p.y * pixelRect.height, p.z)); }
    }
    public class Renderer : Component { public Rendering.ShadowCastingMode shadowCastingMode; }
    public class Texture : Object
    {
        public int width, height;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
    }
    public class Texture2D : Texture
    {
        public static Texture2D whiteTexture = new Texture2D();
        public Texture2D() { width = 1; height = 1; }
        public Texture2D(int width, int height, TextureFormat format, bool mipChain) { this.width = width; this.height = height; }
        public Color32[] Pixels;
        public int Applied;
        public void SetPixels32(Color32[] colors) { Pixels = (Color32[])colors.Clone(); }
        public void Apply() { Applied++; }
    }
    public class Font : Object { public static Font CreateDynamicFontFromOSFont(string fontname, int size) { return new Font { name = fontname }; } }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public enum TextureFormat { RGBA32 = 4, ARGB32 = 5, RGB24 = 3, Alpha8 = 1 }
    [Flags] public enum HideFlags { None = 0, HideInHierarchy = 1, HideInInspector = 2, DontSaveInEditor = 4, NotEditable = 8, DontSaveInBuild = 16, DontUnloadUnusedAsset = 32, DontSave = 52, HideAndDontSave = 61 }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public static class SystemInfo { public static bool supportsInstancing = true; }
    public static class Gizmos
    {
        public static Color color;
        public static void DrawWireSphere(Vector3 center, float radius) { }
        public static void DrawRay(Vector3 from, Vector3 direction) { }
    }
    public static class Resources
    {
        public static readonly Dictionary<string, Object> Assets = new Dictionary<string, Object>();
        public static T Load<T>(string path) where T : Object
        {
            Object o;
            return Assets.TryGetValue(path, out o) && o is T ? (T)o : null;
        }
        public static bool HasLegacyRuntime = true;
        public static T GetBuiltinResource<T>(string path) where T : Object
        {
            if (!HasLegacyRuntime) throw new ArgumentException("The resource " + path + " could not be loaded from the resource file!");
            return (T)(object)new Font { name = path };
        }
    }

    public struct DrawCall { public Dictionary<int, float> Floats; public int Submesh; public string Kind; public int Count; public int Layer; public Mesh Mesh; public Material Material; public Matrix4x4 First; public Matrix4x4[] Source; public Matrix4x4[] Snapshot; public Dictionary<int, float[]> FloatArrays; public Dictionary<int, Vector4> Vectors; public Dictionary<int, Vector4[]> VectorArrays; }
    public static class Graphics
    {
        public static readonly List<DrawCall> Calls = new List<DrawCall>();
        public static void DrawMeshInstanced(Mesh mesh, int submesh, Material material, Matrix4x4[] matrices, int count,
            MaterialPropertyBlock block, Rendering.ShadowCastingMode shadows, bool receive, int layer)
        {
            if (count < 0 || count > 1023) throw new ArgumentException("Instance count must be between 0 and 1023");
            if (count > matrices.Length) throw new ArgumentException("count exceeds matrices.Length");
            // Source is the caller's array itself, Snapshot a copy and FloatArrays copies, as Unity reads them at the call and the caller may refill them for the next batch.
            Dictionary<int, float[]> arrays = null;
            Dictionary<int, Vector4[]> vectorArrays = null;
            if (block != null)
            {
                arrays = new Dictionary<int, float[]>();
                foreach (var kv in block.FloatArrays) arrays[kv.Key] = (float[])kv.Value.Clone();
                vectorArrays = new Dictionary<int, Vector4[]>();
                foreach (var kv in block.VectorArrays) vectorArrays[kv.Key] = (Vector4[])kv.Value.Clone();
            }
            Calls.Add(new DrawCall { Kind = "instanced", Count = count, Layer = layer, Mesh = mesh, Material = material, First = count > 0 ? matrices[0] : default(Matrix4x4), Source = matrices, Snapshot = (Matrix4x4[])matrices.Clone(), FloatArrays = arrays, VectorArrays = vectorArrays });
        }
        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer)
        {
            Calls.Add(new DrawCall { Kind = "mesh", Count = 1, Layer = layer, Mesh = mesh, Material = material, First = matrix });
        }
        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, int submeshIndex,
            MaterialPropertyBlock properties, Rendering.ShadowCastingMode castShadows, bool receiveShadows)
        {
            Calls.Add(new DrawCall
            {
                Kind = "mesh", Count = 1, Layer = layer, Mesh = mesh, Material = material, First = matrix, Submesh = submeshIndex,
                Floats = properties != null ? new Dictionary<int, float>(properties.Floats) : null,
                Vectors = properties != null ? new Dictionary<int, Vector4>(properties.Vectors) : null
            });
        }
    }

    // ---- misc ------------------------------------------------------------------------------------------------
    public static class Debug
    {
        public static readonly List<string> Warnings = new List<string>(), Errors = new List<string>();
        public static void LogWarning(object m) { Warnings.Add(m.ToString()); }
        public static void LogWarning(object m, Object ctx) { Warnings.Add(m.ToString()); }
        public static void LogError(object m) { Errors.Add(m.ToString()); }
        public static readonly List<string> Logs = new List<string>();
        public static void Log(object m) { Logs.Add(m.ToString()); }
        public static void Log(object m, Object ctx) { Logs.Add(m.ToString()); }
    }
    public static class Application { public static bool isPlaying = true; }
    public enum CursorLockMode { None, Locked, Confined }
    public static class Cursor { public static CursorLockMode lockState; public static bool visible = true; }
    public static class Screen { public static int width = 1920, height = 1080; }
    public static class Time
    {
        public static int frameCount;
        public static float unscaledTime, time, deltaTime = 0.016f, unscaledDeltaTime = 0.016f, timeScale = 1f;
    }

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
    public enum BlendMode { Zero = 0, One = 1, DstColor = 2, SrcColor = 3, OneMinusDstColor = 4, SrcAlpha = 5, OneMinusSrcColor = 6, DstAlpha = 7, OneMinusDstAlpha = 8, SrcAlphaSaturate = 9, OneMinusSrcAlpha = 10 }
    public enum IndexFormat { UInt16, UInt32 }
}

namespace UnityEngine.EventSystems
{
    public class EventSystem : UnityEngine.MonoBehaviour
    {
        public static EventSystem current;
        public bool PointerOver;
        public bool IsPointerOverGameObject() { return PointerOver; }
    }
    public class BaseInputModule : UnityEngine.MonoBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
}

namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule { public void AssignDefaultActions() { } }
}

namespace UnityEngine.InputSystem
{
    // Same order as the Input System enum. The keyboard indexer takes the value minus one, and throws for None and IMESelected.
    public enum Key
    {
        None = 0, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash, LeftBracket, RightBracket, Minus, Equals,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0,
        LeftShift, RightShift, LeftAlt, RightAlt, LeftCtrl, RightCtrl, LeftMeta, RightMeta, ContextMenu, Escape,
        LeftArrow, RightArrow, UpArrow, DownArrow, Backspace, PageDown, PageUp, Home, End, Insert, Delete,
        CapsLock, NumLock, PrintScreen, ScrollLock, Pause,
        NumpadEnter, NumpadDivide, NumpadMultiply, NumpadPlus, NumpadMinus, NumpadPeriod, NumpadEquals,
        Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        OEM1, OEM2, OEM3, OEM4, OEM5,
        IMESelected,
    }

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
        private readonly KeyControl[] _keys = new KeyControl[(int)Key.IMESelected - 1];
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
        public KeyControl spaceKey { get { return this[Key.Space]; } }
        public KeyControl tabKey { get { return this[Key.Tab]; } }
        public KeyControl escapeKey { get { return this[Key.Escape]; } }
        public KeyControl leftArrowKey { get { return this[Key.LeftArrow]; } }
        public KeyControl rightArrowKey { get { return this[Key.RightArrow]; } }
        public KeyControl upArrowKey { get { return this[Key.UpArrow]; } }
        public KeyControl downArrowKey { get { return this[Key.DownArrow]; } }
        public KeyControl aKey { get { return this[Key.A]; } }
        public KeyControl dKey { get { return this[Key.D]; } }
        public KeyControl fKey { get { return this[Key.F]; } }
        public KeyControl iKey { get { return this[Key.I]; } }
        public KeyControl jKey { get { return this[Key.J]; } }
        public KeyControl kKey { get { return this[Key.K]; } }
        public KeyControl lKey { get { return this[Key.L]; } }
        public KeyControl qKey { get { return this[Key.Q]; } }
        public KeyControl rKey { get { return this[Key.R]; } }
        public KeyControl sKey { get { return this[Key.S]; } }
        public KeyControl vKey { get { return this[Key.V]; } }
        public KeyControl wKey { get { return this[Key.W]; } }
        public KeyControl digit1Key { get { return this[Key.Digit1]; } }
        public KeyControl digit2Key { get { return this[Key.Digit2]; } }
        public KeyControl digit3Key { get { return this[Key.Digit3]; } }
        public KeyControl digit4Key { get { return this[Key.Digit4]; } }
        public KeyControl digit5Key { get { return this[Key.Digit5]; } }
        public KeyControl digit6Key { get { return this[Key.Digit6]; } }
        public KeyControl digit7Key { get { return this[Key.Digit7]; } }
        public KeyControl digit8Key { get { return this[Key.Digit8]; } }
        public KeyControl leftShiftKey { get { return this[Key.LeftShift]; } }
        public KeyControl rightShiftKey { get { return this[Key.RightShift]; } }
        public KeyControl backquoteKey { get { return this[Key.Backquote]; } }
        public KeyControl leftBracketKey { get { return this[Key.LeftBracket]; } }
        public KeyControl rightBracketKey { get { return this[Key.RightBracket]; } }
        public KeyControl backslashKey { get { return this[Key.Backslash]; } }
        public KeyControl f9Key { get { return this[Key.F9]; } }
    }

    public class Mouse
    {
        public static Mouse current;
        public ButtonControl leftButton = new ButtonControl(), rightButton = new ButtonControl();
        public Vector2Control position = new Vector2Control(), scroll = new Vector2Control(), delta = new Vector2Control();
    }

    public class Gamepad
    {
        public static Gamepad current;
        public ButtonControl rightStickButton = new ButtonControl();
        public ButtonControl buttonSouth = new ButtonControl(), buttonWest = new ButtonControl(), rightTrigger = new ButtonControl();
        public Vector2Control leftStick = new Vector2Control(), rightStick = new Vector2Control();
        public DpadControl dpad = new DpadControl();
    }
    public class DpadControl
    {
        public ButtonControl up = new ButtonControl(), down = new ButtonControl(), left = new ButtonControl(), right = new ButtonControl();
    }
}
