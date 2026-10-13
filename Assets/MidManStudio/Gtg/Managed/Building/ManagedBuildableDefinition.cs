// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedBuildableDefinition.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building
{
    public enum ManagedBuildableCategory
    {
        Station,
        Structure,
    }

    /// <summary>One material a blueprint consumes when it is placed.</summary>
    public struct ManagedBuildCostLine
    {
        public string ItemId;
        public int Count;

        public ManagedBuildCostLine(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    /// <summary>
    /// Tuning of one placeable thing. The table lives in code for now and moves to an mdix
    /// table later, like the spell definitions. The position in the table is the type index
    /// the placed piece store uses, so entries are appended and never reordered.
    /// </summary>
    public sealed class ManagedBuildableDefinition
    {
        /// <summary>Stable id. The same string is the stock item id of the placeable station.</summary>
        public string Id;

        public string DisplayName;
        public ManagedBuildableCategory Category;

        /// <summary>Footprint in grid cells along the piece's own X axis, before rotation.</summary>
        public int FootprintCellsX;

        /// <summary>Footprint in grid cells along the piece's own Z axis, before rotation.</summary>
        public int FootprintCellsZ;

        public float HeightMeters;

        /// <summary>Tint of the placed box until a real mesh and material exist.</summary>
        public Color Color;

        /// <summary>When true the blueprint must be unlocked before this can be placed from the Blueprints tab.</summary>
        public bool RequiresBlueprint;

        /// <summary>Materials taken from the stock when placing from a blueprint. Empty means free.</summary>
        public ManagedBuildCostLine[] Cost = new ManagedBuildCostLine[0];

        // Sizes and colors are placeholders, there is no art or measured design for these yet.
        public static readonly ManagedBuildableDefinition CraftingTable = new ManagedBuildableDefinition
        {
            Id = "crafting_table",
            DisplayName = "Crafting Table",
            Category = ManagedBuildableCategory.Station,
            FootprintCellsX = 3,
            FootprintCellsZ = 2,
            HeightMeters = 0.9f,
            Color = new Color(0.55f, 0.38f, 0.22f, 1f),
            RequiresBlueprint = true,
        };

        public static readonly ManagedBuildableDefinition ButcheryStation = new ManagedBuildableDefinition
        {
            Id = "butchery_station",
            DisplayName = "Butchery / Rendering Station",
            Category = ManagedBuildableCategory.Station,
            FootprintCellsX = 4,
            FootprintCellsZ = 2,
            HeightMeters = 1.0f,
            Color = new Color(0.6f, 0.2f, 0.18f, 1f),
            RequiresBlueprint = true,
        };
    }

    /// <summary>Lookup over the definition table.</summary>
    public static class ManagedBuildableCatalog
    {
        private static readonly ManagedBuildableDefinition[] Table =
        {
            ManagedBuildableDefinition.CraftingTable,
            ManagedBuildableDefinition.ButcheryStation,
        };

        private static readonly Dictionary<string, int> IndexById = BuildIndex();

        public static int Count
        {
            get { return Table.Length; }
        }

        public static ManagedBuildableDefinition Get(int typeIndex)
        {
            return Table[typeIndex];
        }

        public static bool TryGetIndex(string id, out int typeIndex)
        {
            if (id != null && IndexById.TryGetValue(id, out typeIndex))
            {
                return true;
            }

            typeIndex = -1;
            return false;
        }

        public static bool TryGet(string id, out ManagedBuildableDefinition definition)
        {
            int index;
            if (TryGetIndex(id, out index))
            {
                definition = Table[index];
                return true;
            }

            definition = null;
            return false;
        }

        private static Dictionary<string, int> BuildIndex()
        {
            var map = new Dictionary<string, int>(Table.Length);
            for (int i = 0; i < Table.Length; i++)
            {
                map.Add(Table[i].Id, i);
            }

            return map;
        }
    }
}
