// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedNpcShotDriver.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Rendering;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>
    /// The hidden object behind <see cref="ManagedNpcShots"/>. It steps the shots every frame and draws them through one
    /// <see cref="ManagedSphereBatch"/>. The first shot creates it, nobody adds it to a scene. When it is destroyed, for
    /// instance on a scene change, the shots in the air go with it.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class ManagedNpcShotDriver : MonoBehaviour
    {
        private ManagedSphereBatch _batch;

        private void Awake()
        {
            _batch = new ManagedSphereBatch(ManagedNpcShots.MaxShots, gameObject.layer, false, "npc shots");
        }

        private void Update()
        {
            ManagedNpcShots.Step(Time.deltaTime);
        }

        private void LateUpdate()
        {
            ManagedNpcShots.FillBatch(_batch);
            _batch.Draw();
        }

        private void OnDestroy()
        {
            if (_batch != null)
            {
                _batch.Dispose();
                _batch = null;
            }

            ManagedNpcShots.DriverGone(this);
        }
    }
}
