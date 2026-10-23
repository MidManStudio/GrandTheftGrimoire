// Stand-ins for the UnityEngine and mdix Unity types the Quests bridge and the Inventory bridge use. Verification harness only.
using System;
using System.Collections.Generic;
using MidManStudio.Mdix.Core;

namespace UnityEngine
{
    public class Object
    {
        public string name = "stand-in";
    }

    public class Component : Object
    {
        // Real Unity finds the other components of the same GameObject. The checks list them here by hand.
        public readonly List<Component> Siblings = new List<Component>();

        public T GetComponent<T>() where T : class
        {
            for (int i = 0; i < Siblings.Count; i++)
            {
                T found = Siblings[i] as T;
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }

    public class Behaviour : Component { }

    public class MonoBehaviour : Behaviour
    {
        // Real Unity answers true once OnEnable has run on an enabled component. The checks set it by hand.
        public bool isActiveAndEnabled { get; set; }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new()
        {
            return new T();
        }
    }

    public class TextAsset : Object
    {
        public string text;
        public TextAsset(string text) { this.text = text; }
    }

    // The checks never place an asset in a Resources folder, so every load finds nothing.
    public static class Resources
    {
        public static T Load<T>(string path) where T : Object
        {
            return null;
        }
    }

    public static class Debug
    {
        public static readonly List<string> Warnings = new List<string>();
        public static readonly List<string> Errors = new List<string>();
        public static void LogWarning(object message) { Warnings.Add(message == null ? "" : message.ToString()); }
        public static void LogError(object message) { Errors.Add(message == null ? "" : message.ToString()); }
        public static void Reset() { Warnings.Clear(); Errors.Clear(); }
    }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeFieldAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string text) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponent : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrderAttribute : Attribute
    {
        public int Order { get; private set; }
        public DefaultExecutionOrderAttribute(int order) { Order = order; }
    }
}

namespace MidManStudio.Mdix.Unity
{
    /// <summary>Stand-in for the attribute that marks a ScriptableObject the mdix package can bake. Gtg/Data/ItemDatabase.cs carries it.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MdixBakeableAttribute : Attribute
    {
        public MdixBakeableAttribute(string source, string title) { }
    }

    /// <summary>
    /// Stand-in for the asset of the mdix package, which needs the whole Unity binder. The real class returns
    /// Dix.LoadStr(source), and this one answers as a test tells it to.
    /// </summary>
    public sealed class MdixAsset : UnityEngine.ScriptableObject
    {
        public int LoadCalls;
        public bool ThrowOnLoad;

        public MdixResult<MdixDatabase> Load()
        {
            LoadCalls++;
            if (ThrowOnLoad)
            {
                throw new DllNotFoundException("mdix_ffi stand-in");
            }

            return MdixError.NativeError("stand-in asset has no source");
        }
    }
}
