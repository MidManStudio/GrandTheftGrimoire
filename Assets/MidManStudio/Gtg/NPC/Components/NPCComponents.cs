using Unity.Entities;

namespace MidManStudio.Gtg.NPC.Components
{
    public struct NPCTag : IComponentData { }
    public struct NPCIdentity : IComponentData { public ulong Id; public byte Role; public byte Backend; public byte CanMove; public byte LearningMode; }
    public struct NPCObservation : IComponentData { public byte ThreatVisible; public float HealthFraction; }
    public struct NPCDecision : IComponentData { public int Action; }
}
