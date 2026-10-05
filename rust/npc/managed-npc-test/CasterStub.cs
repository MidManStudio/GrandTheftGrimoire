// Stand-in for the one member of ManagedSpellCaster that ManagedSpellDamage touches. The real caster needs the
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
