// Stand-in for the members of ManagedSpellCaster that ManagedSpellDamage and the NPC shots touch (the Impact event and Raise). The real caster needs the
// camera, physics sweeps and rendering, so it is not compiled here. If the real event changes its signature,
// ManagedSpellDamage still compiles in this harness and only the Editor build notices.
using System;

namespace MidManStudio.Gtg.Managed.Magic
{
    public sealed class ManagedSpellCaster
    {
        public static event Action<ManagedSpellImpact> Impact;

        public static int SubscriberCount { get { return Impact == null ? 0 : Impact.GetInvocationList().Length; } }

        public static void Raise(ManagedSpellImpact impact)
        {
            Action<ManagedSpellImpact> handler = Impact;
            if (handler != null) handler(impact);
        }

        public static void ClearForTest() { Impact = null; }
    }
}

// Stand-in for the sphere batch that draws NPC shots. The real one needs a material, a mesh and the GPU, so it is not
// compiled here. It records what was added, so a check can see that one sphere is drawn per shot.
namespace MidManStudio.Gtg.Managed.Rendering
{
    public sealed class ManagedSphereBatch : System.IDisposable
    {
        public static int Created;
        public static int Disposed;
        public int Count;
        public int DrawCalls;
        public UnityEngine.Vector3 LastPosition;
        public UnityEngine.Vector3 LastScale;
        public UnityEngine.Color LastColor;
        // Everything added since the last Clear, in order, so a check can see each sphere and not only the last one.
        public readonly System.Collections.Generic.List<UnityEngine.Vector3> Scales = new System.Collections.Generic.List<UnityEngine.Vector3>();
        public readonly System.Collections.Generic.List<UnityEngine.Color> Colors = new System.Collections.Generic.List<UnityEngine.Color>();

        public ManagedSphereBatch(int capacity, int layer, bool forceCombinedMesh, string label) { Created++; }
        public void Clear() { Count = 0; Scales.Clear(); Colors.Clear(); }
        public bool Add(UnityEngine.Vector3 position, UnityEngine.Vector3 scale, UnityEngine.Color color)
        {
            Count++; LastPosition = position; LastScale = scale; LastColor = color; Scales.Add(scale); Colors.Add(color); return true;
        }
        public void Draw() { DrawCalls++; }
        public void Dispose() { Disposed++; }
    }
}
