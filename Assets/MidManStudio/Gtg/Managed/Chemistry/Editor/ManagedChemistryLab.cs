// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedChemistryLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Chemistry
{
    /// <summary>
    /// Editor test tools for chemistry hazards. Adds the hazard field that fireball and ice impacts
    /// need, and in Play mode counts and clears the hazards. Menu: GTG, Chemistry.
    /// </summary>
    public static class ManagedChemistryLab
    {
        public const string FieldName = "GTG Hazard Field";

        [MenuItem("GTG/Chemistry/Ensure Hazard Field", false, 1000)]
        private static void EnsureFieldMenu()
        {
            ManagedChemicalHazardField field = EnsureField();
            GtgLabKit.Finish(field.gameObject, "Hazard field ready. Impacts of fire and ice spells now leave hazards: a burn patch and a freeze patch.");
        }

        [MenuItem("GTG/Chemistry/Count And Clear Hazards (Play)", false, 1010)]
        private static void ClearMenu()
        {
            if (!GtgLabKit.RequirePlaying("Count And Clear Hazards"))
            {
                return;
            }

            ManagedChemicalHazardField field = Object.FindFirstObjectByType<ManagedChemicalHazardField>();
            if (field == null)
            {
                GtgLabKit.Warn("No ManagedChemicalHazardField in the scene. Use GTG, Chemistry, Ensure Hazard Field.");
                return;
            }

            int before = field.Count;
            field.Clear();
            GtgLabKit.Log("Cleared " + before + " hazard(s).");
        }

        /// <summary>The hazard field of the open scene, or a new one on its own object.</summary>
        public static ManagedChemicalHazardField EnsureField()
        {
            ManagedChemicalHazardField existing = Object.FindFirstObjectByType<ManagedChemicalHazardField>();
            if (existing != null)
            {
                return existing;
            }

            GameObject go = GtgLabKit.NewObject(FieldName, null);
            return Undo.AddComponent<ManagedChemicalHazardField>(go);
        }
    }
}
