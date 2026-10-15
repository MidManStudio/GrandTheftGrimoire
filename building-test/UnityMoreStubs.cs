// More hand written UnityEngine stand-ins, for the rest of the core Managed stack. Verification harness only.
// Signatures follow Unity 2022.3. Most members only keep a value, because the checks that use them read it back.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    // ---- attributes ------------------------------------------------------------------------------------------
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ContextMenuAttribute : Attribute { public ContextMenuAttribute(string name) { } }

    // ---- physics ---------------------------------------------------------------------------------------------
    [Flags] public enum CollisionFlags { None = 0, Sides = 1, Above = 2, Below = 4 }

    public class CharacterController : Collider
    {
        public float height = 2f, radius = 0.5f, slopeLimit = 45f, stepOffset = 0.3f, skinWidth = 0.08f;
        public Vector3 center;
        public Vector3 MoveRequested;
        public CollisionFlags NextFlags = CollisionFlags.Below;
        public CollisionFlags Move(Vector3 motion) { MoveRequested = motion; return NextFlags; }
    }

    // ---- math ------------------------------------------------------------------------------------------------
    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }
}
