// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedPlacedStore.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>Plain copy of one placed piece's data. Cells are the anchor of the rotated footprint.</summary>
    public struct ManagedPlacedPiece
    {
        public int Id;
        public int TypeIndex;
        public int CellX;
        public int CellZ;
        public int Quarter;
        public float GroundY;
    }

    /// <summary>
    /// All placed matrices of one definition, in blocks of 1023. 1023 is the most instances one
    /// Graphics.DrawMeshInstanced call accepts, so a block is drawn straight from its array with
    /// no copy. Removal moves the last element into the hole, which keeps every block packed.
    /// </summary>
    public sealed class ManagedPieceBucket
    {
        public const int ChunkSize = 1023;

        private readonly List<Matrix4x4[]> _matrices = new List<Matrix4x4[]>();
        private readonly List<int[]> _pieceIds = new List<int[]>();

        public int Count { get; private set; }

        public int ChunkCount
        {
            get { return (Count + ChunkSize - 1) / ChunkSize; }
        }

        public Matrix4x4[] GetChunk(int chunk)
        {
            return _matrices[chunk];
        }

        /// <summary>How many entries of the block are live. Every block but the last is full.</summary>
        public int GetChunkLength(int chunk)
        {
            return Math.Min(ChunkSize, Count - chunk * ChunkSize);
        }

        public int PieceIdAt(int index)
        {
            return _pieceIds[index / ChunkSize][index % ChunkSize];
        }

        public Matrix4x4 MatrixAt(int index)
        {
            return _matrices[index / ChunkSize][index % ChunkSize];
        }

        internal int Add(int pieceId, Matrix4x4 matrix)
        {
            int chunk = Count / ChunkSize;
            if (chunk >= _matrices.Count)
            {
                _matrices.Add(new Matrix4x4[ChunkSize]);
                _pieceIds.Add(new int[ChunkSize]);
            }

            int slot = Count % ChunkSize;
            _matrices[chunk][slot] = matrix;
            _pieceIds[chunk][slot] = pieceId;
            return Count++;
        }

        /// <summary>Removes the entry and returns the id of the piece that moved into its place, or -1 when none moved.</summary>
        internal int RemoveAt(int index)
        {
            int last = Count - 1;
            int movedPieceId = -1;
            if (index != last)
            {
                int toChunk = index / ChunkSize;
                int toSlot = index % ChunkSize;
                int fromChunk = last / ChunkSize;
                int fromSlot = last % ChunkSize;
                _matrices[toChunk][toSlot] = _matrices[fromChunk][fromSlot];
                movedPieceId = _pieceIds[fromChunk][fromSlot];
                _pieceIds[toChunk][toSlot] = movedPieceId;
            }

            Count--;
            return movedPieceId;
        }

        internal void Clear()
        {
            Count = 0;
        }
    }

    /// <summary>
    /// Every placed piece, stored as parallel arrays indexed by piece id, plus one matrix bucket
    /// per definition for drawing. No piece is a GameObject or an object of its own. The store
    /// also owns the cell grid, so a piece and its cells are added and removed together.
    /// Ids are reused after a removal, so a holder of an id has to listen to
    /// <see cref="PieceRemoved"/>.
    /// </summary>
    public sealed class ManagedPlacedStore
    {
        private readonly ManagedBuildGrid _grid;
        private readonly ManagedPieceBucket[] _buckets;
        private readonly Stack<int> _freeIds = new Stack<int>();

        private int[] _type;
        private int[] _cellX;
        private int[] _cellZ;
        private int[] _quarter;
        private float[] _groundY;
        private int[] _bucketIndex;
        private int _highWater;

        public ManagedPlacedStore(ManagedBuildGrid grid, int initialCapacity)
        {
            if (grid == null)
            {
                throw new ArgumentNullException("grid");
            }

            _grid = grid;
            _buckets = new ManagedPieceBucket[ManagedBuildableCatalog.Count];
            for (int i = 0; i < _buckets.Length; i++)
            {
                _buckets[i] = new ManagedPieceBucket();
            }

            int capacity = Math.Max(16, initialCapacity);
            _type = new int[capacity];
            _cellX = new int[capacity];
            _cellZ = new int[capacity];
            _quarter = new int[capacity];
            _groundY = new float[capacity];
            _bucketIndex = new int[capacity];
            for (int i = 0; i < capacity; i++)
            {
                _type[i] = -1;
            }
        }

        public ManagedBuildGrid Grid
        {
            get { return _grid; }
        }

        public int Count { get; private set; }

        public event Action<ManagedPlacedPiece> PieceAdded;

        /// <summary>Raised after the piece is gone. The argument is a copy of what it was.</summary>
        public event Action<ManagedPlacedPiece> PieceRemoved;

        public ManagedPieceBucket GetBucket(int typeIndex)
        {
            return _buckets[typeIndex];
        }

        /// <summary>
        /// Adds a piece and claims its cells. Returns the piece id, or -1 when the type is unknown
        /// or any cell is already taken. Nothing changes on a refusal.
        /// </summary>
        public int Add(int typeIndex, int cellX, int cellZ, int quarter, float groundY)
        {
            if (typeIndex < 0 || typeIndex >= _buckets.Length)
            {
                return -1;
            }

            quarter &= 3;
            ManagedBuildableDefinition def = ManagedBuildableCatalog.Get(typeIndex);
            int width;
            int depth;
            ManagedBuildGrid.RotatedSize(def.FootprintCellsX, def.FootprintCellsZ, quarter, out width, out depth);
            if (!_grid.AreFree(cellX, cellZ, width, depth))
            {
                return -1;
            }

            int id = AllocateId();
            _type[id] = typeIndex;
            _cellX[id] = cellX;
            _cellZ[id] = cellZ;
            _quarter[id] = quarter;
            _groundY[id] = groundY;
            _grid.Occupy(cellX, cellZ, width, depth, id);
            _bucketIndex[id] = _buckets[typeIndex].Add(id, BuildMatrix(def, _grid.CellSize, cellX, cellZ, quarter, groundY));
            Count++;

            Action<ManagedPlacedPiece> added = PieceAdded;
            if (added != null)
            {
                added(Snapshot(id));
            }

            return id;
        }

        public bool Remove(int pieceId)
        {
            if (!IsAlive(pieceId))
            {
                return false;
            }

            ManagedPlacedPiece piece = Snapshot(pieceId);
            ManagedBuildableDefinition def = ManagedBuildableCatalog.Get(piece.TypeIndex);
            int width;
            int depth;
            ManagedBuildGrid.RotatedSize(def.FootprintCellsX, def.FootprintCellsZ, piece.Quarter, out width, out depth);
            _grid.Release(piece.CellX, piece.CellZ, width, depth, pieceId);

            int moved = _buckets[piece.TypeIndex].RemoveAt(_bucketIndex[pieceId]);
            if (moved >= 0)
            {
                _bucketIndex[moved] = _bucketIndex[pieceId];
            }

            _type[pieceId] = -1;
            _freeIds.Push(pieceId);
            Count--;

            Action<ManagedPlacedPiece> removed = PieceRemoved;
            if (removed != null)
            {
                removed(piece);
            }

            return true;
        }

        public bool IsAlive(int pieceId)
        {
            return pieceId >= 0 && pieceId < _highWater && _type[pieceId] >= 0;
        }

        public bool TryGet(int pieceId, out ManagedPlacedPiece piece)
        {
            if (!IsAlive(pieceId))
            {
                piece = default(ManagedPlacedPiece);
                return false;
            }

            piece = Snapshot(pieceId);
            return true;
        }

        /// <summary>Appends a copy of every live piece to <paramref name="into"/>, for saving.</summary>
        public void CopyPieces(List<ManagedPlacedPiece> into)
        {
            for (int id = 0; id < _highWater; id++)
            {
                if (_type[id] >= 0)
                {
                    into.Add(Snapshot(id));
                }
            }
        }

        /// <summary>
        /// Removes everything, for a scene reset or a load. <see cref="PieceRemoved"/> is raised
        /// once per piece after the store is empty, so the collider proxies follow.
        /// </summary>
        public void Clear()
        {
            List<ManagedPlacedPiece> gone = null;
            if (Count > 0 && PieceRemoved != null)
            {
                gone = new List<ManagedPlacedPiece>(Count);
                CopyPieces(gone);
            }

            for (int i = 0; i < _buckets.Length; i++)
            {
                _buckets[i].Clear();
            }

            for (int id = 0; id < _highWater; id++)
            {
                _type[id] = -1;
            }

            _freeIds.Clear();
            _highWater = 0;
            Count = 0;
            _grid.Clear();

            if (gone != null)
            {
                Action<ManagedPlacedPiece> removed = PieceRemoved;
                for (int i = 0; i < gone.Count && removed != null; i++)
                {
                    removed(gone[i]);
                }
            }
        }

        /// <summary>
        /// World matrix of the unit box that stands for the piece. The box is scaled to the
        /// unrotated footprint and height, then turned by the quarter turns, so the grid cells
        /// and the drawn box agree for every rotation.
        /// </summary>
        public static Matrix4x4 BuildMatrix(
            ManagedBuildableDefinition def, float cellSize, int cellX, int cellZ, int quarter, float groundY)
        {
            int width;
            int depth;
            ManagedBuildGrid.RotatedSize(def.FootprintCellsX, def.FootprintCellsZ, quarter, out width, out depth);
            var center = new Vector3(
                (cellX + width * 0.5f) * cellSize,
                groundY + def.HeightMeters * 0.5f,
                (cellZ + depth * 0.5f) * cellSize);
            var scale = new Vector3(def.FootprintCellsX * cellSize, def.HeightMeters, def.FootprintCellsZ * cellSize);
            return Matrix4x4.TRS(center, Quaternion.Euler(0f, (quarter & 3) * 90f, 0f), scale);
        }

        private int AllocateId()
        {
            if (_freeIds.Count > 0)
            {
                return _freeIds.Pop();
            }

            if (_highWater == _type.Length)
            {
                Grow(_type.Length * 2);
            }

            return _highWater++;
        }

        private void Grow(int capacity)
        {
            int old = _type.Length;
            Array.Resize(ref _type, capacity);
            Array.Resize(ref _cellX, capacity);
            Array.Resize(ref _cellZ, capacity);
            Array.Resize(ref _quarter, capacity);
            Array.Resize(ref _groundY, capacity);
            Array.Resize(ref _bucketIndex, capacity);
            for (int i = old; i < capacity; i++)
            {
                _type[i] = -1;
            }
        }

        private ManagedPlacedPiece Snapshot(int id)
        {
            return new ManagedPlacedPiece
            {
                Id = id,
                TypeIndex = _type[id],
                CellX = _cellX[id],
                CellZ = _cellZ[id],
                Quarter = _quarter[id],
                GroundY = _groundY[id],
            };
        }
    }
}
