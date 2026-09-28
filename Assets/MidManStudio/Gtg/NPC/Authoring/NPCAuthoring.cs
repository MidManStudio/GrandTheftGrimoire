using MidManStudio.Gtg.NPC.Components;
using Unity.Entities;
using UnityEngine;

namespace MidManStudio.Gtg.NPC.Authoring
{
    public sealed class NPCAuthoring : MonoBehaviour
    {
        public int Role = 0; // 0 merchant, 1 civilian, 2 guard, 3 enemy, 4 boss
        public int Backend = 0; // 0 FSM, 1 utility, 2 hybrid ML
        public bool CanMove = false;
        public int LearningMode = 0; // 0 disabled, 1 fixed weights, 2 online

        private sealed class Baker : Baker<NPCAuthoring>
        {
            public override void Bake(NPCAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<NPCTag>(entity);
                AddComponent(entity, new NPCIdentity { Id = 0, Role = (byte)authoring.Role, Backend = (byte)authoring.Backend, CanMove = (byte)(authoring.CanMove ? 1 : 0), LearningMode = (byte)authoring.LearningMode });
                AddComponent(entity, new NPCObservation { ThreatVisible = 0, HealthFraction = 1.0f });
                AddComponent(entity, new NPCDecision { Action = 0 });
            }
        }
    }
}
