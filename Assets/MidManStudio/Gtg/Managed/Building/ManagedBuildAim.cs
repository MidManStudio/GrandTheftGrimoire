// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedBuildAim.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>
    /// Turns the pointer ray into what build mode needs: the ground point to aim a piece at and the
    /// placed piece under the pointer, if any. One cast answers both, and the hits are sorted by
    /// hand because RaycastNonAlloc returns them unordered.
    /// </summary>
    public sealed class ManagedBuildAim
    {
        private readonly RaycastHit[] _hits = new RaycastHit[24];
        private readonly LayerMask _mask;
        private readonly float _length;

        public ManagedBuildAim(LayerMask mask, float length)
        {
            _mask = mask;
            _length = length;
        }

        /// <summary>Everything under this transform is skipped, so the camera behind the player does not aim at the player.</summary>
        public Transform IgnoreRoot { get; set; }

        /// <summary>
        /// Casts the ray. Returns true when ground was hit. A placed piece never counts as ground,
        /// so the aim point lands on the floor behind a station instead of on top of it, and the
        /// piece is reported through <paramref name="hoverPieceId"/> when it is in front of that
        /// floor. The id is -1 when no piece is under the pointer.
        /// </summary>
        public bool Cast(Ray ray, out Vector3 groundPoint, out int hoverPieceId)
        {
            groundPoint = Vector3.zero;
            hoverPieceId = -1;

            int count = Physics.RaycastNonAlloc(ray, _hits, _length, _mask, QueryTriggerInteraction.Ignore);
            float groundDistance = float.MaxValue;
            float pieceDistance = float.MaxValue;
            bool hasGround = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                Collider collider = hit.collider;
                if (collider == null || (IgnoreRoot != null && collider.transform.IsChildOf(IgnoreRoot)))
                {
                    continue;
                }

                ManagedPlacedPieceProxy proxy;
                if (collider.TryGetComponent(out proxy))
                {
                    if (hit.distance < pieceDistance)
                    {
                        pieceDistance = hit.distance;
                        hoverPieceId = proxy.PieceId;
                    }
                }
                else if (hit.distance < groundDistance)
                {
                    groundDistance = hit.distance;
                    groundPoint = hit.point;
                    hasGround = true;
                }
            }

            if (hoverPieceId >= 0 && pieceDistance > groundDistance)
            {
                hoverPieceId = -1;
            }

            return hasGround;
        }
    }
}
