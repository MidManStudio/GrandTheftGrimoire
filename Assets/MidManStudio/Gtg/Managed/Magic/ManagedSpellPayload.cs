// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSpellPayload.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>
    /// What a spell carries to its impact, as far as flight is concerned. Load is how hard
    /// the mix pushes on the bubble, on the same scale as a vessel's rating.
    /// </summary>
    public struct ManagedSpellPayload
    {
        public float Load;
    }

    /// <summary>
    /// The payload table. It is authored by hand for now. Later the outcome classifier
    /// produces a payload from a cook, and the id then points at that result instead.
    /// </summary>
    public static class ManagedSpellPayloads
    {
        // Indexed by payload id. For now the id equals the spell kind.
        private static readonly ManagedSpellPayload[] Table =
        {
            new ManagedSpellPayload { Load = 6f },
            new ManagedSpellPayload { Load = 3f },
        };

        public static int IdFor(ManagedSpellKind kind)
        {
            return (int)kind;
        }

        /// <summary>The load of a payload, or zero for an id the table does not have.</summary>
        public static float LoadOf(int id)
        {
            if (id < 0 || id >= Table.Length)
            {
                return 0f;
            }

            return Table[id].Load;
        }
    }
}
