// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/data.md, section "NpcDialogue.cs"
// ============================================================================

using MidManStudio.Mdix.Unity;
using UnityEngine;

namespace MidManStudio.Gtg.Data
{
    /// <summary>
    /// Fixed lines for one NPC, from a file such as <c>Assets/NPC/Dialogue/merchant_default.mdix</c>.
    /// An archetype points at one of these through its <c>DialogueRef</c>, which holds <see cref="Id"/>.
    /// </summary>
    [MdixBakeable("", "NPC dialogue (merchant_default.mdix)")]
    public sealed class NpcDialogue : ScriptableObject
    {
        public string Id;
        public string Greeting;
        public string Farewell;
    }
}
