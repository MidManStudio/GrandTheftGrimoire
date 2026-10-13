// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedPlacedColliders.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>Sits on a pooled collider object and tells a raycast hit which placed piece it belongs to.</summary>
    public sealed class ManagedPlacedPieceProxy : MonoBehaviour
    {
        public int PieceId;
        public int TypeIndex;
    }

    /// <summary>
    /// Gives every placed piece a box collider, so the player walks into it, a ray can pick it
    /// and the blocker check sees it. A piece has no GameObject of its own, so these are pooled
    /// proxies that follow the store through its added and removed events. One collider per piece
    /// suits stations. A structure with thousands of pieces would need merged colliders instead.
    /// </summary>
    public sealed class ManagedPlacedColliders : IDisposable
    {
        private readonly ManagedPlacedStore _store;
        private readonly Transform _parent;
        private readonly int _layer;
        private readonly List<ManagedPlacedPieceProxy> _byId = new List<ManagedPlacedPieceProxy>();
        private readonly Stack<ManagedPlacedPieceProxy> _pool = new Stack<ManagedPlacedPieceProxy>();

        public ManagedPlacedColliders(ManagedPlacedStore store, Transform parent, int layer)
        {
            _store = store;
            _parent = parent;
            _layer = layer;

            // Pieces that exist before this object does, for instance after a load, get proxies too.
            var existing = new List<ManagedPlacedPiece>();
            _store.CopyPieces(existing);
            for (int i = 0; i < existing.Count; i++)
            {
                OnAdded(existing[i]);
            }

            _store.PieceAdded += OnAdded;
            _store.PieceRemoved += OnRemoved;
        }

        public void Dispose()
        {
            _store.PieceAdded -= OnAdded;
            _store.PieceRemoved -= OnRemoved;
            for (int i = 0; i < _byId.Count; i++)
            {
                DestroyProxy(_byId[i]);
            }

            while (_pool.Count > 0)
            {
                DestroyProxy(_pool.Pop());
            }

            _byId.Clear();
        }

        private void OnAdded(ManagedPlacedPiece piece)
        {
            ManagedPlacedPieceProxy proxy = Rent();
            ManagedBuildableDefinition def = ManagedBuildableCatalog.Get(piece.TypeIndex);
            float cell = _store.Grid.CellSize;

            int width;
            int depth;
            ManagedBuildGrid.RotatedSize(def.FootprintCellsX, def.FootprintCellsZ, piece.Quarter, out width, out depth);

            // Placed while inactive, so the physics scene picks the collider up already in its final spot.
            Transform t = proxy.transform;
            t.SetPositionAndRotation(
                new Vector3(
                    (piece.CellX + width * 0.5f) * cell,
                    piece.GroundY + def.HeightMeters * 0.5f,
                    (piece.CellZ + depth * 0.5f) * cell),
                Quaternion.Euler(0f, (piece.Quarter & 3) * 90f, 0f));
            proxy.GetComponent<BoxCollider>().size =
                new Vector3(def.FootprintCellsX * cell, def.HeightMeters, def.FootprintCellsZ * cell);
            proxy.PieceId = piece.Id;
            proxy.TypeIndex = piece.TypeIndex;

            while (_byId.Count <= piece.Id)
            {
                _byId.Add(null);
            }

            _byId[piece.Id] = proxy;
            proxy.gameObject.SetActive(true);
        }

        private void OnRemoved(ManagedPlacedPiece piece)
        {
            if (piece.Id >= _byId.Count || _byId[piece.Id] == null)
            {
                return;
            }

            ManagedPlacedPieceProxy proxy = _byId[piece.Id];
            _byId[piece.Id] = null;
            proxy.gameObject.SetActive(false);
            _pool.Push(proxy);
        }

        private ManagedPlacedPieceProxy Rent()
        {
            if (_pool.Count > 0)
            {
                return _pool.Pop();
            }

            var go = new GameObject("GTG Placed Piece");
            go.SetActive(false);
            go.layer = _layer;
            go.transform.SetParent(_parent, false);
            go.AddComponent<BoxCollider>();
            return go.AddComponent<ManagedPlacedPieceProxy>();
        }

        private static void DestroyProxy(ManagedPlacedPieceProxy proxy)
        {
            if (proxy == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(proxy.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(proxy.gameObject);
            }
        }
    }
}
