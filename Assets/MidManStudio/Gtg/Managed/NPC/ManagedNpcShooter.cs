// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedNpcShooter.cs"
// ============================================================================

using System;
using MidManStudio.Gtg.Managed.Magic;
using MidManStudio.Gtg.NPC.Native;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>What a shooter looses: an arrow or bolt that flies and falls, or a spell from the spell table.</summary>
    public enum ManagedNpcRangedKind : byte
    {
        Projectile = 0,
        Spell = 1,
    }

    /// <summary>What came of one try to fire.</summary>
    public enum ManagedNpcFireResult : byte
    {
        /// <summary>A shot left and a unit of ammo was spent.</summary>
        Fired = 0,

        /// <summary>Ready, but the cooldown has not run out.</summary>
        Waiting = 1,

        /// <summary>Nothing left to shoot.</summary>
        NoAmmo = 2,

        /// <summary>Something is in the way, or there is no way to hit the point from here. Nothing was spent.</summary>
        Blocked = 3,
    }

    /// <summary>
    /// The ranged weapon of one NPC: a bow, a crossbow or a staff. It holds the ammo (arrows, bolts, or casts for a
    /// magic user), the cooldown and the aim, and looses the shot when the actor asks. It decides nothing, Rust still
    /// chooses Shoot or Kite, and the actor picks the place to stand and calls <see cref="TryFire"/>. Put it on any NPC
    /// whose attack style is Ranged or Hybrid. The brain reads <see cref="IsReady"/> as the observation's
    /// ranged_ready, so an NPC with nothing to shoot is no longer told to shoot.
    /// An arrow or bolt is aimed with gravity in mind, so it arcs onto the target. A spell flies straight along the
    /// line to the target and uses the numbers of its entry in the spell table.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedNpcShooter : MonoBehaviour
    {
        // The stand-off distance is kept this far past the kite line of the decision. A shooter that stands inside the
        // kite line would back away, be told to shoot, walk back in and back away again.
        private const float KiteMarginMeters = 1f;

        [Header("Weapon")]
        [Tooltip("Projectile is an arrow or bolt that flies and falls. Spell is cast from the spell table.")]
        [SerializeField] private ManagedNpcRangedKind _kind = ManagedNpcRangedKind.Projectile;
        [Tooltip("The spell a magic user casts. Fireball is the one that hurts on its own today. A spell that steers flies straight for an NPC, its steering is not used yet.")]
        [SerializeField] private ManagedSpellKind _spell = ManagedSpellKind.Fireball;
        [Tooltip("Where shots leave, in the NPC's local space from the feet pivot.")]
        [SerializeField] private Vector3 _shotPointOffset = new Vector3(0.3f, 1.4f, 0.5f);

        [Header("Reach")]
        [Tooltip("Farthest the NPC fires at. It also needs to see the target, so the brain's view range limits it as well.")]
        [SerializeField] private float _range = 20f;
        [Tooltip("How far from the target the shooter takes its place. Kept clear of the distance where the decision tells a shooter to back away.")]
        [SerializeField] private float _standOffDistance = 12f;

        [Header("Fire")]
        [SerializeField] private float _cooldownSeconds = 1.5f;
        [Tooltip("Random spread around the aim, in degrees. Zero hits exactly where it aims.")]
        [SerializeField] private float _spreadDegrees = 2f;

        [Header("Ammo")]
        [Tooltip("Shots the NPC holds: arrows, bolts, or casts for a magic user. It starts full.")]
        [SerializeField] private float _capacity = 12f;
        [Tooltip("Seconds for one shot to come back. Zero means it never refills, as a quiver does. A magic user sets this so mana returns.")]
        [SerializeField] private float _regenSecondsPerShot;

        [Header("Projectile")]
        [SerializeField] private float _projectileSpeed = 35f;
        [Tooltip("Share of world gravity that bends the path. Zero flies straight.")]
        [SerializeField] private float _projectileGravityScale = 0.5f;
        [SerializeField] private float _projectileDamage = 12f;
        [Tooltip("Radius of the sphere swept along the path, so thin colliders are still hit.")]
        [SerializeField] private float _projectileRadius = 0.1f;
        [Tooltip("Diameter of the drawn shot.")]
        [SerializeField] private float _projectileDiameter = 0.15f;
        [SerializeField] private Color _projectileColor = new Color(0.95f, 0.9f, 0.75f, 1f);
        [Tooltip("Seconds in the air before a missed shot drops out.")]
        [SerializeField] private float _projectileSeconds = 4f;

        private float _ammo;
        private float _regenStamp;
        private float _nextFireTime;
        private bool _started;

        /// <summary>(this). Raised for every shot, for an animation or a sound.</summary>
        public event Action<ManagedNpcShooter> Fired;

        public ManagedNpcRangedKind Kind { get { return _kind; } }

        /// <summary>Farthest the NPC fires at, in meters.</summary>
        public float Range { get { return _range; } }

        /// <summary>How far from the target the shooter takes its place, in meters.</summary>
        public float StandOffDistance { get { return _standOffDistance; } }

        public float Capacity { get { return _capacity; } }

        /// <summary>Shots held right now, with the part of a shot that has come back.</summary>
        public float Ammo
        {
            get
            {
                Regen();
                return _ammo;
            }
        }

        /// <summary>True while there is at least one shot to fire. The brain sends this as ranged_ready.</summary>
        public bool IsReady
        {
            get
            {
                Regen();
                return _ammo >= 1f;
            }
        }

        /// <summary>Where shots leave, in world space.</summary>
        public Vector3 ShotPoint { get { return transform.TransformPoint(_shotPointOffset); } }

        private void Awake()
        {
            Begin();
        }

        // Called from Awake, and from the properties for a shooter that was made in code before its Awake ran.
        private void Begin()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _ammo = _capacity;
            _regenStamp = Time.time;
        }

        private void OnValidate()
        {
            _capacity = Mathf.Max(0f, _capacity);
            _regenSecondsPerShot = Mathf.Max(0f, _regenSecondsPerShot);
            _cooldownSeconds = Mathf.Max(0f, _cooldownSeconds);
            _spreadDegrees = Mathf.Clamp(_spreadDegrees, 0f, 45f);
            _projectileSpeed = Mathf.Max(0.1f, _projectileSpeed);
            _projectileGravityScale = Mathf.Max(0f, _projectileGravityScale);
            _projectileDamage = Mathf.Max(0f, _projectileDamage);
            _projectileRadius = Mathf.Max(0f, _projectileRadius);
            _projectileDiameter = Mathf.Max(0.01f, _projectileDiameter);
            _projectileSeconds = Mathf.Max(0.1f, _projectileSeconds);

            float floor = NPCManagedFallback.KiteDistance + KiteMarginMeters;
            _range = Mathf.Max(_range, floor);
            _standOffDistance = Mathf.Clamp(_standOffDistance, floor, _range);
        }

        /// <summary>Adds shots, or takes them away with a negative number. The result stays between zero and the capacity.</summary>
        public void AddAmmo(float shots)
        {
            Begin();
            Regen();
            _ammo = Mathf.Clamp(_ammo + shots, 0f, _capacity);
        }

        /// <summary>Fills the pool.</summary>
        public void Refill()
        {
            Begin();
            _regenStamp = Time.time;
            _ammo = _capacity;
        }

        private void Regen()
        {
            Begin();
            float now = Time.time;
            float elapsed = now - _regenStamp;
            _regenStamp = now;
            if (elapsed > 0f && _regenSecondsPerShot > 0f && _ammo < _capacity)
            {
                _ammo = Mathf.Min(_capacity, _ammo + elapsed / _regenSecondsPerShot);
            }
        }

        /// <summary>
        /// Looses one shot at the point, if the weapon is ready and the way is clear. The way is clear when a straight
        /// line from the shot point to the aim point reaches the threat, or nothing, before anything else. The straight
        /// line is checked for an arc too, so a shooter does not lob over a wall. A shot that cannot be fired costs no
        /// ammo and starts no cooldown.
        /// </summary>
        /// <param name="now">Time.time of the call.</param>
        /// <param name="aimPoint">Where to hit, in world space.</param>
        /// <param name="threat">What is aimed at. A hit on it, or on a part of it, does not count as blocking.</param>
        public ManagedNpcFireResult TryFire(float now, Vector3 aimPoint, Transform threat)
        {
            Regen();
            if (_ammo < 1f)
            {
                return ManagedNpcFireResult.NoAmmo;
            }

            if (now < _nextFireTime)
            {
                return ManagedNpcFireResult.Waiting;
            }

            Vector3 origin = ShotPoint;
            RaycastHit blocker;
            if (ManagedNpcShots.Sweep(origin, aimPoint, 0f, transform, out blocker)
                && (threat == null || !blocker.collider.transform.IsChildOf(threat)))
            {
                return ManagedNpcFireResult.Blocked;
            }

            Vector2 disc = _spreadDegrees > 0f ? UnityEngine.Random.insideUnitCircle : new Vector2(0f, 0f);
            bool added;
            if (_kind == ManagedNpcRangedKind.Projectile)
            {
                if (ManagedNpcShots.IsBlockedAt(origin, _projectileRadius, transform))
                {
                    return ManagedNpcFireResult.Blocked;
                }

                float gravity = -Physics.gravity.y * _projectileGravityScale;
                Vector3 direction;
                if (!ManagedNpcBallistics.TryLaunchDirection(origin, aimPoint, _projectileSpeed, gravity, out direction))
                {
                    return ManagedNpcFireResult.Blocked;
                }

                direction = ManagedNpcBallistics.Spread(direction, _spreadDegrees, disc.x, disc.y);
                added = ManagedNpcShots.AddProjectile(
                    origin, direction * _projectileSpeed, _projectileGravityScale, _projectileDamage,
                    _projectileRadius, _projectileDiameter, _projectileColor, _projectileSeconds, gameObject);
            }
            else
            {
                ManagedSpellDefinition spell = ManagedSpellDefinition.For(_spell);
                if (spell == null || ManagedNpcShots.IsBlockedAt(origin, spell.SweepRadius, transform))
                {
                    return ManagedNpcFireResult.Blocked;
                }

                Vector3 line = aimPoint - origin;
                if (line.sqrMagnitude < 1e-6f)
                {
                    return ManagedNpcFireResult.Blocked;
                }

                Vector3 direction = ManagedNpcBallistics.Spread(line.normalized, _spreadDegrees, disc.x, disc.y);

                // A full vessel's worth of SP, the same numbers the player's belt gives a new vessel.
                ManagedVesselDraw draw = new ManagedVesselDraw { Sp = spell.VesselSp, Capacity = spell.VesselSp, Rating = spell.VesselRating };
                ManagedShotSpawn spawn = spell.CreateSpawn(
                    ManagedSpellDefinition.IndexOf(spell.Kind), ManagedSpellPayloads.IdFor(spell.Kind), origin, direction, draw);

                // No lock is taken, so a steered spell flies straight.
                spawn.TurnRatePerSecond = 0f;
                spawn.Target = 0;
                added = ManagedNpcShots.AddSpell(spawn, gameObject);
            }

            if (!added)
            {
                return ManagedNpcFireResult.Blocked;
            }

            _ammo -= 1f;
            _nextFireTime = now + _cooldownSeconds;
            Action<ManagedNpcShooter> handler = Fired;
            if (handler != null)
            {
                handler(this);
            }

            return ManagedNpcFireResult.Fired;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(ShotPoint, 0.1f);
        }
    }
}
