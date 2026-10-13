// Stand-ins for the two character types the camera rig reads. Verification harness only.
namespace MidManStudio.Gtg.Managed.CharacterController
{
    public sealed class ManagedCharacter : UnityEngine.MonoBehaviour
    {
        public float Yaw, Pitch;
        public bool IsGrounded;
        public float VerticalVelocity;
        public ManagedCharacterFeature Features { get; set; } = ManagedCharacterFeature.All;
        public bool Has(ManagedCharacterFeature f) { return (Features & f) != 0; }
    }
}

namespace MidManStudio.Gtg.Managed.Magic
{
    public struct SpellStub { public string DisplayName; }
    public sealed class ManagedSpellCaster : UnityEngine.MonoBehaviour
    {
        public SpellStub SelectedSpell;
        public int ShotCount;
    }
}
