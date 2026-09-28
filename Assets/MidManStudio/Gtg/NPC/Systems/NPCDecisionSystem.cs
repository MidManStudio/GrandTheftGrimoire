using MidManStudio.Gtg.NPC.Components;
using Unity.Entities;

namespace MidManStudio.Gtg.NPC.Systems
{
    // Stub: intentionally does not invoke native code until the plugin is packaged.
    // Future: batch observations at a capped decision rate and validate ABI once at startup.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct NPCDecisionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state) { }
    }
}
