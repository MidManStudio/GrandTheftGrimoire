// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedBuildGrid.cs"
// ============================================================================

using System;
using System.Collections.Generic;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>
    /// Square cells on the XZ plane that record which placed piece owns them. A footprint is
    /// anchored at its minimum corner cell. Pieces turn in 90 degree steps only, so a rotated
    /// footprint is still an axis aligned rectangle of cells and its center does not move.
    /// Plain arithmetic, no engine types.
    /// </summary>
    public sealed class ManagedBuildGrid
    {
        private readonly Dictionary<long, int> _owners = new Dictionary<long, int>();

        public ManagedBuildGrid(float cellSize)
        {
            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException("cellSize");
            }

            CellSize = cellSize;
        }

        public float CellSize { get; private set; }

        public int OccupiedCellCount
        {
            get { return _owners.Count; }
        }

        /// <summary>Footprint size after <paramref name="quarter"/> quarter turns. Odd counts swap the axes.</summary>
        public static void RotatedSize(int sizeX, int sizeZ, int quarter, out int width, out int depth)
        {
            bool swap = (quarter & 1) != 0;
            width = swap ? sizeZ : sizeX;
            depth = swap ? sizeX : sizeZ;
        }

        /// <summary>Anchor cell that puts the footprint center closest to the world position.</summary>
        public int AnchorX(float worldX, int width)
        {
            return (int)Math.Floor(worldX / CellSize - width * 0.5f + 0.5f);
        }

        public int AnchorZ(float worldZ, int depth)
        {
            return (int)Math.Floor(worldZ / CellSize - depth * 0.5f + 0.5f);
        }

        public float CenterX(int cellX, int width)
        {
            return (cellX + width * 0.5f) * CellSize;
        }

        public float CenterZ(int cellZ, int depth)
        {
            return (cellZ + depth * 0.5f) * CellSize;
        }

        public bool AreFree(int cellX, int cellZ, int width, int depth)
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    if (_owners.ContainsKey(Key(cellX + x, cellZ + z)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void Occupy(int cellX, int cellZ, int width, int depth, int pieceId)
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    _owners[Key(cellX + x, cellZ + z)] = pieceId;
                }
            }
        }

        /// <summary>Frees the cells of the footprint that <paramref name="pieceId"/> owns. Cells owned by others stay.</summary>
        public void Release(int cellX, int cellZ, int width, int depth, int pieceId)
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    long key = Key(cellX + x, cellZ + z);
                    int owner;
                    if (_owners.TryGetValue(key, out owner) && owner == pieceId)
                    {
                        _owners.Remove(key);
                    }
                }
            }
        }

        /// <summary>Id of the piece that owns the cell, or -1 when it is free.</summary>
        public int OwnerAt(int cellX, int cellZ)
        {
            int owner;
            return _owners.TryGetValue(Key(cellX, cellZ), out owner) ? owner : -1;
        }

        public void Clear()
        {
            _owners.Clear();
        }

        // The high half holds x and the low half holds z, so negative cells do not collide.
        private static long Key(int x, int z)
        {
            return ((long)x << 32) | (uint)z;
        }
    }
}
