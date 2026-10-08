// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/data.md, section "NpcArchetypeDatabase.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Gtg.NPC.Components;
using MidManStudio.Mdix.Unity;
using UnityEngine;

namespace MidManStudio.Gtg.Data
{
    /// <summary>
    /// One archetype from <c>Assets/NPC/Archetypes/archetypes.mdix</c>. The enums are the ones in
    /// <c>NPCEnums.cs</c>, which <c>scripts/check_npc_enum_sync.py</c> keeps in step with the file
    /// and the Rust ABI.
    /// </summary>
    [Serializable]
    public sealed class NpcArchetypeDefinition
    {
        public string Id;
        public NpcRole Role;
        public DecisionBackend DecisionBackend;
        public bool CanMove;
        public LearningMode LearningMode;

        /// <summary>Id of a dialogue file. An empty string means the archetype has no fixed dialogue.</summary>
        public string DialogueRef;

        // Derived by the file from the backend and the learning mode, not separate decisions.
        public bool UsesMl;
        public bool MayUpdateWeights;
    }

    /// <summary>
    /// The archetype table from <c>archetypes.mdix</c> as a ScriptableObject. A loader should
    /// refuse a table whose <see cref="AbiVersion"/> differs from the one it was built for.
    /// </summary>
    [MdixBakeable("", "NPC archetypes (archetypes.mdix)")]
    public sealed class NpcArchetypeDatabase : ScriptableObject
    {
        public string Schema;
        public int AbiVersion;

        public List<NpcArchetypeDefinition> Archetypes = new List<NpcArchetypeDefinition>();
    }
}
