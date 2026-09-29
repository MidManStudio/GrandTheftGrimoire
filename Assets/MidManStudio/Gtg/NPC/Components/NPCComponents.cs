using Unity.Entities;

namespace MidManStudio.Gtg.NPC.Components
{
    public struct NPCTag : IComponentData { }
    public struct NPCIdentity : IComponentData
    {
        public ulong Id;
        public NpcRole Role;
        public DecisionBackend Backend;
        public byte CanMove;
        public LearningMode LearningMode;
    }
    public struct NPCObservation : IComponentData { public byte ThreatVisible; public float HealthFraction; }
    public struct NPCDecision : IComponentData { public NpcAction Action; }
}
