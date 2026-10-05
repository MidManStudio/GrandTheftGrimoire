// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSpellDamage.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Health;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>
    /// Turns a spell impact into damage. It listens to ManagedSpellCaster.Impact on its own, so no
    /// scene object is needed, and it hurts every IManagedDamageable inside the explosion radius
    /// once, with less damage toward the edge. The numbers come from ManagedSpellDefinition.
    /// </summary>
    public static class ManagedSpellDamage
    {
        private const int MaxOverlaps = 32;

        private static readonly Collider[] Overlaps = new Collider[MaxOverlaps];
        private static readonly IManagedDamageable[] Targets = new IManagedDamageable[MaxOverlaps];
        private static readonly float[] Distances = new float[MaxOverlaps];

        /// <summary>Turns spell damage off, for a cutscene or a test scene.</summary>
        public static bool Enabled = true;

        /// <summary>When on, the caster is hurt by its own explosion. Off by default.</summary>
        public static bool HurtCaster;

        /// <summary>Layers the explosion looks at.</summary>
        public static LayerMask Mask = ~0;

        // Runs after the SubsystemRegistration reset of the caster's static event, so this
        // subscription is not wiped.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            ManagedSpellCaster.Impact -= OnImpact;
            ManagedSpellCaster.Impact += OnImpact;
        }

        private static void OnImpact(ManagedSpellImpact impact)
        {
            Apply(impact);
        }

        /// <summary>Applies the damage of one impact. Returns how many targets were hurt.</summary>
        public static int Apply(ManagedSpellImpact impact)
        {
            if (!Enabled)
            {
                return 0;
            }

            ManagedSpellDefinition spell = ManagedSpellDefinition.For(impact.Kind);
            if (spell == null || !(spell.ImpactDamage > 0f) || !(spell.ImpactRadius > 0f))
            {
                return 0;
            }

            int found = Physics.OverlapSphereNonAlloc(impact.Position, spell.ImpactRadius, Overlaps, Mask, QueryTriggerInteraction.Ignore);
            if (found > MaxOverlaps)
            {
                found = MaxOverlaps;
            }

            // One entry per target, with the distance of its nearest collider. A body with several
            // colliders is hurt once.
            int targetCount = 0;
            for (int i = 0; i < found; i++)
            {
                Collider collider = Overlaps[i];
                Overlaps[i] = null;
                if (collider == null)
                {
                    continue;
                }

                IManagedDamageable target = collider.GetComponentInParent<IManagedDamageable>();
                if (target == null || (!HurtCaster && IsCaster(target, impact.Source)))
                {
                    continue;
                }

                float distance = (collider.bounds.ClosestPoint(impact.Position) - impact.Position).magnitude;
                int slot = -1;
                for (int t = 0; t < targetCount; t++)
                {
                    if (ReferenceEquals(Targets[t], target))
                    {
                        slot = t;
                        break;
                    }
                }

                if (slot < 0)
                {
                    Targets[targetCount] = target;
                    Distances[targetCount] = distance;
                    targetCount++;
                }
                else if (distance < Distances[slot])
                {
                    Distances[slot] = distance;
                }
            }

            float edge = Mathf.Clamp01(spell.ImpactEdgeFraction);
            int hurt = 0;
            for (int t = 0; t < targetCount; t++)
            {
                float along = Mathf.Clamp01(Distances[t] / spell.ImpactRadius);
                float amount = spell.ImpactDamage * (1f + (edge - 1f) * along);
                IManagedDamageable target = Targets[t];
                Targets[t] = null;
                if (target.IsAlive)
                {
                    target.TakeDamage(amount, impact.Source);
                    hurt++;
                }
            }

            return hurt;
        }

        private static bool IsCaster(IManagedDamageable target, GameObject source)
        {
            Component component = target as Component;
            return source != null && component != null && component.transform.IsChildOf(source.transform);
        }
    }
}
