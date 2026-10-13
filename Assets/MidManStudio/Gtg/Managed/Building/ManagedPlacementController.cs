// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedPlacementController.cs"
// ============================================================================

using System;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building
{
    public enum ManagedPlacementMode
    {
        Idle,
        Placing,
    }

    /// <summary>Where the piece being placed comes from.</summary>
    public enum ManagedPlacementSource
    {
        None,

        /// <summary>A station the player holds. Placing it spends one from the stock.</summary>
        Stock,

        /// <summary>An unlocked schematic. Placing it spends the definition's cost, which is nothing by default.</summary>
        Blueprint,

        /// <summary>A piece the player picked up from the world. Placing it again is free.</summary>
        Carried,
    }

    /// <summary>
    /// The placing state machine. Drag from the tray, click from the tray and pick up from the
    /// world all end up here as one Placing state, so the rules exist once. It knows nothing
    /// about screens, mice or UI, the caller feeds it an aim point. Plain C# apart from the
    /// vector type.
    /// </summary>
    public sealed class ManagedPlacementController
    {
        private readonly ManagedBuildRules _rules;
        private readonly ManagedPlacedStore _store;
        private readonly IManagedBuildWorldProbe _probe;
        private readonly IManagedBuildStock _stock;
        private readonly ManagedBlueprintBook _blueprints;

        private ManagedPlacedPiece _carriedOrigin;
        private int _typeIndex = -1;
        private int _lastQuarter;
        private bool _hasAim;
        private Vector3 _aimPoint;
        private Vector3 _reachOrigin;

        public ManagedPlacementController(
            ManagedBuildRules rules,
            ManagedPlacedStore store,
            IManagedBuildWorldProbe probe,
            IManagedBuildStock stock,
            ManagedBlueprintBook blueprints)
        {
            _rules = rules;
            _store = store;
            _probe = probe;
            _stock = stock;
            _blueprints = blueprints;

            // Valid is the zero value of the status, so an untouched result would read as a good spot.
            Result = new ManagedPlacementResult { Status = ManagedPlacementStatus.NoAim };
        }

        public ManagedPlacementMode Mode { get; private set; }

        public ManagedPlacementSource Source { get; private set; }

        /// <summary>The definition being placed. Null while idle.</summary>
        public ManagedBuildableDefinition Definition { get; private set; }

        /// <summary>Quarter turns, 0 to 3.</summary>
        public int Quarter { get; private set; }

        /// <summary>True when releasing the pointer should place the piece, as at the end of a drag from the tray.</summary>
        public bool CommitOnRelease { get; private set; }

        public ManagedPlacementResult Result { get; private set; }

        /// <summary>Raised when the mode, source or definition changed.</summary>
        public event Action StateChanged;

        /// <summary>Raised when the status, the cell or the rotation of the current result changed.</summary>
        public event Action<ManagedPlacementResult> ResultChanged;

        /// <summary>Raised with the id of every piece this controller adds to the store.</summary>
        public event Action<int> Placed;

        /// <summary>Raised when a carried piece went back to the stock because its old spot was gone.</summary>
        public event Action<ManagedBuildableDefinition> ReturnedToStock;

        /// <summary>Whether the source allows placing this definition now, ignoring where.</summary>
        public ManagedPlacementStatus CheckAvailability(ManagedBuildableDefinition def, ManagedPlacementSource source)
        {
            switch (source)
            {
                case ManagedPlacementSource.Stock:
                    return _stock.GetCount(def.Id) >= 1 ? ManagedPlacementStatus.Valid : ManagedPlacementStatus.MissingItems;

                case ManagedPlacementSource.Blueprint:
                    if (!_blueprints.IsAvailable(def))
                    {
                        return ManagedPlacementStatus.NotUnlocked;
                    }

                    return CanAfford(def) ? ManagedPlacementStatus.Valid : ManagedPlacementStatus.MissingItems;

                default:
                    return ManagedPlacementStatus.Valid;
            }
        }

        /// <summary>
        /// Starts placing a station from the stock or a schematic. Any piece being carried goes
        /// back to where it was first. Returns false, with the reason, when the source refuses.
        /// </summary>
        public bool BeginPlacing(
            ManagedPlacementSource source, string buildableId, bool commitOnRelease, out ManagedPlacementStatus refusal)
        {
            refusal = ManagedPlacementStatus.Valid;
            int typeIndex;
            if ((source != ManagedPlacementSource.Stock && source != ManagedPlacementSource.Blueprint)
                || !ManagedBuildableCatalog.TryGetIndex(buildableId, out typeIndex))
            {
                refusal = ManagedPlacementStatus.NoAim;
                return false;
            }

            ManagedBuildableDefinition def = ManagedBuildableCatalog.Get(typeIndex);
            refusal = CheckAvailability(def, source);
            if (refusal != ManagedPlacementStatus.Valid)
            {
                return false;
            }

            if (Mode == ManagedPlacementMode.Placing)
            {
                Cancel();
            }

            Enter(source, typeIndex, _lastQuarter, commitOnRelease);
            return true;
        }

        /// <summary>Takes a placed piece off the ground and starts carrying it. The piece id is gone afterwards.</summary>
        public bool BeginCarry(int pieceId)
        {
            if (Mode == ManagedPlacementMode.Placing || !_store.TryGet(pieceId, out _carriedOrigin))
            {
                return false;
            }

            _store.Remove(pieceId);
            Enter(ManagedPlacementSource.Carried, _carriedOrigin.TypeIndex, _carriedOrigin.Quarter, false);
            return true;
        }

        /// <summary>Feeds the aim of this frame and re-checks the spot. Does nothing while idle.</summary>
        public void SetAim(bool hasAim, Vector3 aimPoint, Vector3 reachOrigin)
        {
            _hasAim = hasAim;
            _aimPoint = aimPoint;
            _reachOrigin = reachOrigin;
            Revalidate();
        }

        public void Rotate(int direction)
        {
            if (Mode != ManagedPlacementMode.Placing || direction == 0)
            {
                return;
            }

            Quarter = (((Quarter + direction) % 4) + 4) % 4;
            _lastQuarter = Quarter;
            Revalidate();
        }

        /// <summary>
        /// Places the piece when the current spot is valid. Returns false and changes nothing
        /// otherwise. After a click-placed station the controller keeps placing while the source
        /// can still pay for another one.
        /// </summary>
        public bool TryCommit(out int pieceId)
        {
            pieceId = -1;
            if (Mode != ManagedPlacementMode.Placing || !Result.IsValid)
            {
                return false;
            }

            ManagedBuildableDefinition def = Definition;
            int id = _store.Add(_typeIndex, Result.CellX, Result.CellZ, Quarter, Result.GroundY);
            if (id < 0)
            {
                return false;
            }

            if (!Pay(def))
            {
                _store.Remove(id);
                Revalidate();
                return false;
            }

            pieceId = id;
            Action<int> placed = Placed;
            if (placed != null)
            {
                placed(id);
            }

            bool keepGoing = Source != ManagedPlacementSource.Carried
                && !CommitOnRelease
                && CheckAvailability(def, Source) == ManagedPlacementStatus.Valid;
            if (keepGoing)
            {
                Revalidate();
            }
            else
            {
                Leave();
            }

            return true;
        }

        /// <summary>Stops placing. A carried piece goes back to the spot it was lifted from.</summary>
        public void Cancel()
        {
            if (Mode != ManagedPlacementMode.Placing)
            {
                return;
            }

            if (Source == ManagedPlacementSource.Carried)
            {
                RestoreCarried();
            }

            Leave();
        }

        /// <summary>Stops placing and keeps the piece in the stock. A station that was only being aimed stays where it was.</summary>
        public void Stash()
        {
            if (Mode != ManagedPlacementMode.Placing)
            {
                return;
            }

            if (Source == ManagedPlacementSource.Carried)
            {
                _stock.Give(Definition.Id, 1);
                Leave();
                return;
            }

            Cancel();
        }

        /// <summary>
        /// Turns a drag into click-to-place. Used when a drag ends over the world on a bad spot or
        /// over the tray, so the player can still aim and click instead of losing the selection.
        /// </summary>
        public void ArmForClick()
        {
            if (Mode == ManagedPlacementMode.Placing && CommitOnRelease)
            {
                CommitOnRelease = false;
                RaiseStateChanged();
            }
        }

        private void Enter(ManagedPlacementSource source, int typeIndex, int quarter, bool commitOnRelease)
        {
            Mode = ManagedPlacementMode.Placing;
            Source = source;
            _typeIndex = typeIndex;
            Definition = ManagedBuildableCatalog.Get(typeIndex);
            Quarter = quarter & 3;
            _lastQuarter = Quarter;
            CommitOnRelease = commitOnRelease;
            _hasAim = false;
            Result = new ManagedPlacementResult { Status = ManagedPlacementStatus.NoAim };

            RaiseStateChanged();
            Revalidate();
        }

        private void Leave()
        {
            Mode = ManagedPlacementMode.Idle;
            Source = ManagedPlacementSource.None;
            Definition = null;
            _typeIndex = -1;
            CommitOnRelease = false;

            // Valid is the zero value of the status, so a default result would read as a good spot.
            Result = new ManagedPlacementResult { Status = ManagedPlacementStatus.NoAim };
            RaiseStateChanged();
        }

        private void RestoreCarried()
        {
            int id = _store.Add(
                _carriedOrigin.TypeIndex, _carriedOrigin.CellX, _carriedOrigin.CellZ,
                _carriedOrigin.Quarter, _carriedOrigin.GroundY);
            if (id >= 0)
            {
                return;
            }

            // Cannot happen while only this controller edits the store, but a lost station is worse than a moved one.
            _stock.Give(Definition.Id, 1);
            Action<ManagedBuildableDefinition> returned = ReturnedToStock;
            if (returned != null)
            {
                returned(Definition);
            }
        }

        private void Revalidate()
        {
            if (Mode != ManagedPlacementMode.Placing)
            {
                return;
            }

            ManagedPlacementResult next = ManagedPlacementValidator.Evaluate(
                _rules, _store, _probe, Definition, Quarter, _hasAim, _aimPoint, _reachOrigin);

            // A source that cannot pay overrides where the piece would stand, so the ghost turns red.
            ManagedPlacementStatus availability = CheckAvailability(Definition, Source);
            if (availability != ManagedPlacementStatus.Valid)
            {
                next.Status = availability;
            }

            ManagedPlacementResult previous = Result;
            Result = next;
            bool changed = previous.Status != next.Status
                || previous.CellX != next.CellX
                || previous.CellZ != next.CellZ
                || previous.GroundY != next.GroundY;
            if (changed)
            {
                Action<ManagedPlacementResult> handler = ResultChanged;
                if (handler != null)
                {
                    handler(next);
                }
            }
        }

        private bool CanAfford(ManagedBuildableDefinition def)
        {
            ManagedBuildCostLine[] cost = def.Cost;
            for (int i = 0; i < cost.Length; i++)
            {
                if (_stock.GetCount(cost[i].ItemId) < cost[i].Count)
                {
                    return false;
                }
            }

            return true;
        }

        private bool Pay(ManagedBuildableDefinition def)
        {
            switch (Source)
            {
                case ManagedPlacementSource.Stock:
                    return _stock.TryTake(def.Id, 1);

                case ManagedPlacementSource.Blueprint:
                    if (!CanAfford(def))
                    {
                        return false;
                    }

                    for (int i = 0; i < def.Cost.Length; i++)
                    {
                        _stock.TryTake(def.Cost[i].ItemId, def.Cost[i].Count);
                    }

                    return true;

                default:
                    return true;
            }
        }

        private void RaiseStateChanged()
        {
            Action handler = StateChanged;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
