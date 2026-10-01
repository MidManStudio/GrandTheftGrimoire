// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/character-controller.md, section "CharacterAuthoring.cs"
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MidManStudio.Gtg.CharacterController
{
    /// <summary>
    /// Editor-only authoring component. Put it on an empty GameObject inside a SubScene.
    /// The pivot is the feet. The CapsuleCollider next to it is baked by Unity Physics
    /// into the character's PhysicsCollider, and the movement systems cast that shape.
    /// Match the collider to the character: direction Y, center (0, height / 2, 0).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public class CharacterAuthoring : MonoBehaviour
    {
        [Header("Modules")]
        [SerializeField] private bool _lookEnabled = true;
        [SerializeField] private bool _moveEnabled = true;
        [SerializeField] private bool _jumpEnabled = true;
        [SerializeField] private bool _gravityEnabled = true;
        [SerializeField] private bool _castEnabled = true;

        [Header("Move")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _acceleration = 60f;
        [Range(0f, 1f)]
        [SerializeField] private float _airControl = 0.4f;

        [Header("Jump")]
        [SerializeField] private float _jumpSpeed = 7f;
        [SerializeField] private float _coyoteTime = 0.1f;

        [Header("Gravity")]
        [SerializeField] private float _gravity = -20f;
        [SerializeField] private float _gravityCap = 50f;
        [SerializeField] private float _groundStickSpeed = 2f;

        [Header("Collision")]
        [SerializeField] private float _groundProbeDistance = 0.1f;
        [SerializeField] private float _maxSlopeDegrees = 50f;
        [SerializeField] private float _skinWidth = 0.01f;
        [SerializeField] private int _maxSlideIterations = 4;

        [Header("Look")]
        [SerializeField] private float _mouseDegreesPerPixel = 0.1f;
        [SerializeField] private float _stickDegreesPerSecond = 180f;
        [SerializeField] private float _minPitch = -80f;
        [SerializeField] private float _maxPitch = 80f;

        // Runs when the component is first added. Shapes the capsule to a 2 m tall character with the pivot at the feet.
        private void Reset()
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.direction = 1;
                capsule.radius = 0.5f;
                capsule.height = 2f;
                capsule.center = new Vector3(0f, 1f, 0f);
            }
        }

        private class Baker : Baker<CharacterAuthoring>
        {
            public override void Bake(CharacterAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                Transform authoringTransform = GetComponent<Transform>();

                CharacterFeature features = CharacterFeature.None;
                if (authoring._lookEnabled) features |= CharacterFeature.Look;
                if (authoring._moveEnabled) features |= CharacterFeature.Move;
                if (authoring._jumpEnabled) features |= CharacterFeature.Jump;
                if (authoring._gravityEnabled) features |= CharacterFeature.Gravity;
                if (authoring._castEnabled) features |= CharacterFeature.Cast;

                AddComponent<CharacterTag>(entity);
                AddComponent(entity, new CharacterFeatures { Enabled = features });
                AddComponent(entity, new CharacterSettings
                {
                    MoveSpeed = authoring._moveSpeed,
                    Acceleration = authoring._acceleration,
                    AirControl = authoring._airControl,
                    JumpSpeed = authoring._jumpSpeed,
                    CoyoteTime = authoring._coyoteTime,
                    Gravity = authoring._gravity,
                    GravityCap = authoring._gravityCap,
                    GroundStickSpeed = authoring._groundStickSpeed,
                    GroundProbeDistance = authoring._groundProbeDistance,
                    MaxSlopeDot = math.cos(math.radians(authoring._maxSlopeDegrees)),
                    SkinWidth = authoring._skinWidth,
                    MaxSlideIterations = math.max(1, authoring._maxSlideIterations),
                    MouseDegreesPerPixel = authoring._mouseDegreesPerPixel,
                    StickDegreesPerSecond = authoring._stickDegreesPerSecond,
                    MinPitch = authoring._minPitch,
                    MaxPitch = authoring._maxPitch,
                });

                AddComponent<CharacterInput>(entity);
                AddComponent(entity, new CharacterLook { Yaw = authoringTransform.eulerAngles.y, Pitch = 0f });
                AddComponent(entity, new CharacterMotor { GroundNormal = new float3(0f, 1f, 0f) });
            }
        }
    }
}
