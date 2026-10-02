#if GTG_ECS
using MidManStudio.Gtg.NPC.Components;
using Unity.Entities;
using UnityEngine;

namespace MidManStudio.Gtg.NPC.Authoring
{
    public sealed class NPCAuthoring : MonoBehaviour
    {
        // Unity serializes enums as integers, so existing values on scenes and prefabs are kept.
        public NpcRole Role = NpcRole.Merchant;
        public DecisionBackend Backend = DecisionBackend.StateMachine;
        public bool CanMove = false;
        public LearningMode LearningMode = LearningMode.Disabled;

        private sealed class Baker : Baker<NPCAuthoring>
        {
            public override void Bake(NPCAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<NPCTag>(entity);
                AddComponent(entity, new NPCIdentity { Id = 0, Role = authoring.Role, Backend = authoring.Backend, CanMove = (byte)(authoring.CanMove ? 1 : 0), LearningMode = authoring.LearningMode });
                AddComponent(entity, new NPCObservation { ThreatVisible = 0, HealthFraction = 1.0f });
                AddComponent(entity, new NPCDecision { Action = NpcAction.Idle });
            }
        }
    }
}
#endif
