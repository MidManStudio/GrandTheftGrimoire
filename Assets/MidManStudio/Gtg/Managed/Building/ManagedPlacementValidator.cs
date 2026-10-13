// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedPlacementValidator.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>Why a spot is or is not acceptable. Valid is the only status that lets a piece be placed.</summary>
    public enum ManagedPlacementStatus
    {
        Valid,
        NoAim,
        OutOfReach,
        Occupied,
        NoGround,
        Uneven,
        Blocked,
        NotUnlocked,
        MissingItems,
    }

    public struct ManagedPlacementResult
    {
        public ManagedPlacementStatus Status;

        /// <summary>Anchor cell of the rotated footprint.</summary>
        public int CellX;

        public int CellZ;

        /// <summary>Height the piece stands on. The highest of the ground samples.</summary>
        public float GroundY;

        /// <summary>Center of the footprint on the ground plane, with Y at the ground.</summary>
        public Vector3 Center;

        public bool IsValid
        {
            get { return Status == ManagedPlacementStatus.Valid; }
        }
    }

    /// <summary>The two questions placement asks the physical world. Unity answers them with Physics queries.</summary>
    public interface IManagedBuildWorldProbe
    {
        /// <summary>Height of the ground under (x, z). <paramref name="referenceY"/> is where to start looking from, so a roof above is not hit.</summary>
        bool TryGetGroundHeight(float x, float z, float referenceY, out float y);

        /// <summary>True when something solid overlaps the axis aligned box.</summary>
        bool IsBlocked(Vector3 center, Vector3 halfExtents);
    }

    /// <summary>
    /// Decides whether a footprint can stand at the aimed spot. The cheap grid checks run before
    /// the physics checks, so a spot that is taken never costs a query.
    /// </summary>
    public static class ManagedPlacementValidator
    {
        public static ManagedPlacementResult Evaluate(
            ManagedBuildRules rules,
            ManagedPlacedStore store,
            IManagedBuildWorldProbe probe,
            ManagedBuildableDefinition def,
            int quarter,
            bool hasAim,
            Vector3 aimPoint,
            Vector3 reachOrigin)
        {
            var result = new ManagedPlacementResult();
            if (!hasAim)
            {
                result.Status = ManagedPlacementStatus.NoAim;
                return result;
            }

            ManagedBuildGrid grid = store.Grid;
            int width;
            int depth;
            ManagedBuildGrid.RotatedSize(def.FootprintCellsX, def.FootprintCellsZ, quarter, out width, out depth);

            result.CellX = grid.AnchorX(aimPoint.x, width);
            result.CellZ = grid.AnchorZ(aimPoint.z, depth);
            float centerX = grid.CenterX(result.CellX, width);
            float centerZ = grid.CenterZ(result.CellZ, depth);
            result.Center = new Vector3(centerX, aimPoint.y, centerZ);

            if (rules.MaxReach > 0f)
            {
                float dx = centerX - reachOrigin.x;
                float dz = centerZ - reachOrigin.z;
                if (dx * dx + dz * dz > rules.MaxReach * rules.MaxReach)
                {
                    result.Status = ManagedPlacementStatus.OutOfReach;
                    return result;
                }
            }

            if (!grid.AreFree(result.CellX, result.CellZ, width, depth))
            {
                result.Status = ManagedPlacementStatus.Occupied;
                return result;
            }

            // Four corners and the center. All of them must hit ground and stay within the allowed spread.
            float x0 = result.CellX * grid.CellSize + rules.EdgeInset;
            float x1 = (result.CellX + width) * grid.CellSize - rules.EdgeInset;
            float z0 = result.CellZ * grid.CellSize + rules.EdgeInset;
            float z1 = (result.CellZ + depth) * grid.CellSize - rules.EdgeInset;

            float low = float.MaxValue;
            float high = float.MinValue;
            for (int i = 0; i < 5; i++)
            {
                float sx = i == 4 ? centerX : ((i & 1) == 0 ? x0 : x1);
                float sz = i == 4 ? centerZ : ((i & 2) == 0 ? z0 : z1);
                float y;
                if (!probe.TryGetGroundHeight(sx, sz, aimPoint.y, out y))
                {
                    result.Status = ManagedPlacementStatus.NoGround;
                    return result;
                }

                if (y < low)
                {
                    low = y;
                }

                if (y > high)
                {
                    high = y;
                }
            }

            if (high - low > rules.MaxGroundDelta)
            {
                result.Status = ManagedPlacementStatus.Uneven;
                return result;
            }

            result.GroundY = high;
            result.Center = new Vector3(centerX, high, centerZ);

            float boxHeight = def.HeightMeters - rules.BlockerSkin;
            if (boxHeight > 0f)
            {
                var half = new Vector3(
                    (x1 - x0) * 0.5f,
                    boxHeight * 0.5f,
                    (z1 - z0) * 0.5f);
                var boxCenter = new Vector3(centerX, high + rules.BlockerSkin + boxHeight * 0.5f, centerZ);
                if (probe.IsBlocked(boxCenter, half))
                {
                    result.Status = ManagedPlacementStatus.Blocked;
                    return result;
                }
            }

            result.Status = ManagedPlacementStatus.Valid;
            return result;
        }

        /// <summary>Short player-facing reason for a status. Empty for Valid.</summary>
        public static string Describe(ManagedPlacementStatus status)
        {
            switch (status)
            {
                case ManagedPlacementStatus.NoAim: return "Aim at the ground";
                case ManagedPlacementStatus.OutOfReach: return "Too far away";
                case ManagedPlacementStatus.Occupied: return "Something is already here";
                case ManagedPlacementStatus.NoGround: return "No ground under it";
                case ManagedPlacementStatus.Uneven: return "Ground is too uneven";
                case ManagedPlacementStatus.Blocked: return "Something is in the way";
                case ManagedPlacementStatus.NotUnlocked: return "Blueprint not unlocked";
                case ManagedPlacementStatus.MissingItems: return "Missing materials";
                default: return string.Empty;
            }
        }
    }
}
