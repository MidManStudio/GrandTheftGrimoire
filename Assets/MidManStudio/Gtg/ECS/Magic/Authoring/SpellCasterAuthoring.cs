#if GTG_ECS
// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/magic.md, section "SpellCasterAuthoring.cs"
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MidManStudio.Gtg.Magic
{
    /// <summary>Put this on the same GameObject as <c>CharacterAuthoring</c>, inside the SubScene.</summary>
    [DisallowMultipleComponent]
    public sealed class SpellCasterAuthoring : MonoBehaviour
    {
        [SerializeField] private float _cooldownSeconds = 0.4f;
        [SerializeField] private float _projectileSpeed = 25f;
        [SerializeField] private float _projectileLifetimeSeconds = 3f;
        [Tooltip("Muzzle position in the character's yaw space, from the feet pivot.")]
        [SerializeField] private Vector3 _muzzleOffset = new Vector3(0f, 1.4f, 0.6f);

        private sealed class Baker : Baker<SpellCasterAuthoring>
        {
            public override void Bake(SpellCasterAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                Vector3 offset = authoring._muzzleOffset;
                AddComponent(entity, new SpellCaster
                {
                    Cooldown = authoring._cooldownSeconds,
                    CooldownRemaining = 0f,
                    ProjectileSpeed = authoring._projectileSpeed,
                    ProjectileLifetime = authoring._projectileLifetimeSeconds,
                    MuzzleOffset = new float3(offset.x, offset.y, offset.z),
                });
            }
        }
    }
}
#endif
