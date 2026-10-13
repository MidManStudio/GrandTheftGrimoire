// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedUnityBuildProbe.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>Answers the placement questions with Unity Physics. Triggers never count.</summary>
    public sealed class ManagedUnityBuildProbe : IManagedBuildWorldProbe
    {
        // The ray starts this far above the reference height and may travel this far down.
        private const float RayLift = 1.5f;
        private const float RayLength = 6f;

        private readonly LayerMask _groundMask;
        private readonly LayerMask _blockerMask;
        private readonly Collider[] _overlaps = new Collider[8];

        public ManagedUnityBuildProbe(LayerMask groundMask, LayerMask blockerMask)
        {
            _groundMask = groundMask;
            _blockerMask = blockerMask;
        }

        public bool TryGetGroundHeight(float x, float z, float referenceY, out float y)
        {
            RaycastHit hit;
            if (Physics.Raycast(
                    new Vector3(x, referenceY + RayLift, z), Vector3.down, out hit, RayLength,
                    _groundMask, QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y;
                return true;
            }

            y = 0f;
            return false;
        }

        public bool IsBlocked(Vector3 center, Vector3 halfExtents)
        {
            return Physics.OverlapBoxNonAlloc(
                       center, halfExtents, _overlaps, Quaternion.identity, _blockerMask,
                       QueryTriggerInteraction.Ignore) > 0;
        }
    }
}
