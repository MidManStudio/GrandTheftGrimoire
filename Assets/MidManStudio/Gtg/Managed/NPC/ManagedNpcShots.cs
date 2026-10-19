// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedNpcShots.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.Magic;
using MidManStudio.Gtg.Managed.Rendering;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>
    /// Every shot an NPC has in the air: the arrows and bolts of archers and crossbow guards, and the spells of magic
    /// users. They fly through one <see cref="ManagedSpellFlight"/>, the same flight code the player's spells use, so a
    /// shot is a few array entries and not a GameObject. Each step sweeps a small sphere from the old position to the
    /// new one, so a fast shot cannot pass through a thin collider. An arrow or bolt that hits hurts what it hit. A
    /// spell that hits or runs out of SP raises the same impact event as the player's spells, so the spell damage and
    /// the chemistry field treat it like any other impact. A hidden driver object, made on the first shot, steps and
    /// draws the shots, so there is nothing to add to the scene.
    /// </summary>
    public static class ManagedNpcShots
    {
        /// <summary>Shots in the air at once. A shooter whose shot does not fit keeps its ammo and tries again.</summary>
        public const int MaxShots = 256;

        private const int MaxOwnHitSkips = 4;
        private const float RestartPadding = 0.02f;
        private const int OverlapBufferSize = 8;
        private const int NoProfile = -1;

        // A spell with no SP left is drawn at these fractions of its full brightness and size, as the player's are.
        private const float MinDim = 0.35f;
        private const float MinScale = 0.6f;

        /// <summary>Layers a shot can hit. Both arrows and spells use it.</summary>
        public static LayerMask HitMask = ~0;

        private static readonly ManagedSpellFlight Flight = new ManagedSpellFlight(32);
        private static readonly Collider[] Overlaps = new Collider[OverlapBufferSize];
        private static GameObject[] _source = new GameObject[32];
        private static float[] _damage = new float[32];
        private static float[] _radius = new float[32];
        private static float[] _diameter = new float[32];
        private static Color[] _color = new Color[32];
        private static ManagedNpcShotDriver _driver;

        /// <summary>Shots in the air right now.</summary>
        public static int Count { get { return Flight.Count; } }

        /// <summary>Position of a shot below Count. For debug tools and tests.</summary>
        public static Vector3 PositionAt(int index) { return Flight.PositionAt(index); }

        /// <summary>Velocity of a shot below Count. For debug tools and tests.</summary>
        public static Vector3 VelocityAt(int index) { return Flight.VelocityAt(index); }

        /// <summary>True when the shot below Count is an arrow or bolt, false for a spell.</summary>
        public static bool IsProjectileAt(int index) { return Flight.ProfileAt(index) == NoProfile; }

        /// <summary>Removes every shot, for a cutscene, a scene change or a test.</summary>
        public static void Clear()
        {
            Flight.Clear();
            System.Array.Clear(_source, 0, _source.Length);
        }

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
            _driver = null;
            HitMask = ~0;
        }

        /// <summary>Adds an arrow or bolt. False when the air is full, and nothing was added.</summary>
        internal static bool AddProjectile(
            Vector3 origin, Vector3 velocity, float gravityScale, float damage, float radius, float diameter,
            Color color, float maxSeconds, GameObject source)
        {
            if (Flight.Count >= MaxShots)
            {
                return false;
            }

            EnsureDriver();

            // Unpowered, so the shot only falls and is not held to a speed. It carries one point of SP and burns none,
            // so the flight never reports it collapsed and it ends on hitting something or at its time cap.
            Flight.Add(new ManagedShotSpawn
            {
                Position = origin,
                Velocity = velocity,
                Profile = NoProfile,
                Sp = 1f,
                MaxAgeSeconds = maxSeconds,
                Powered = false,
                Speed = velocity.magnitude,
                GravityScale = gravityScale,
            });
            Store(Flight.Count - 1, source, damage, radius, diameter, color);
            return true;
        }

        /// <summary>Adds a spell made by <see cref="ManagedSpellDefinition.CreateSpawn(int, int, Vector3, Vector3, ManagedVesselDraw)"/>. False when the air is full.</summary>
        internal static bool AddSpell(ManagedShotSpawn spawn, GameObject source)
        {
            if (Flight.Count >= MaxShots)
            {
                return false;
            }

            EnsureDriver();
            Flight.Add(spawn);
            Store(Flight.Count - 1, source, 0f, 0f, 0f, default(Color));
            return true;
        }

        private static void Store(int index, GameObject source, float damage, float radius, float diameter, Color color)
        {
            if (index >= _source.Length)
            {
                int size = _source.Length * 2;
                System.Array.Resize(ref _source, size);
                System.Array.Resize(ref _damage, size);
                System.Array.Resize(ref _radius, size);
                System.Array.Resize(ref _diameter, size);
                System.Array.Resize(ref _color, size);
            }

            _source[index] = source;
            _damage[index] = damage;
            _radius[index] = radius;
            _diameter[index] = diameter;
            _color[index] = color;
        }

        private static void EnsureDriver()
        {
            if (_driver != null)
            {
                return;
            }

            GameObject host = new GameObject("GTG NPC Shots");
            _driver = host.AddComponent<ManagedNpcShotDriver>();
        }

        // A driver that woke up asks whether it is the one. The first to ask is, and so is a driver placed in a scene by
        // hand before any shot was fired, which then does the work instead of a hidden one. Any other is told to go.
        internal static bool Adopt(ManagedNpcShotDriver driver)
        {
            if (_driver == null || _driver == driver)
            {
                _driver = driver;
                return true;
            }

            return false;
        }

        internal static void DriverGone(ManagedNpcShotDriver driver)
        {
            if (_driver == driver)
            {
                _driver = null;
                Clear();
            }
        }

        // Counts down so a swap-remove never makes the loop visit a shot twice.
        internal static void Step(float dt)
        {
            float gravityY = Physics.gravity.y;

            for (int i = Flight.Count - 1; i >= 0; i--)
            {
                GameObject source = _source[i] != null ? _source[i] : null;
                int profile = Flight.ProfileAt(i);
                ManagedSpellDefinition spell = profile == NoProfile ? null : ManagedSpellDefinition.At(profile);

                Vector3 start;
                Vector3 end;
                Flight.Plan(i, dt, gravityY, out start, out end);

                RaycastHit hit;
                float radius = spell != null ? spell.SweepRadius : _radius[i];
                if (Sweep(start, end, radius, source != null ? source.transform : null, out hit))
                {
                    if (spell == null)
                    {
                        float damage = _damage[i];
                        Retire(i);
                        Hurt(hit.collider, damage, source);
                    }
                    else
                    {
                        int payload = Flight.PayloadAt(i);
                        Retire(i);
                        Detonate(spell, payload, hit.point, hit.normal, source);
                    }

                    continue;
                }

                Vector3 releasedAt;
                ManagedFlightState state = Flight.Commit(i, dt, end, out releasedAt);
                if (state == ManagedFlightState.Flying)
                {
                    continue;
                }

                // A spent arrow just drops. A spell's bubble collapses where its SP ran out and the payload releases there.
                if (spell == null)
                {
                    Retire(i);
                    continue;
                }

                int spellPayload = Flight.PayloadAt(i);
                Vector3 normal = -Flight.VelocityAt(i).normalized;
                Retire(i);
                Detonate(spell, spellPayload, releasedAt, normal, source);
            }
        }

        /// <summary>Fills a batch with one sphere per shot. The driver then draws the batch.</summary>
        internal static void FillBatch(ManagedSphereBatch batch)
        {
            batch.Clear();
            for (int i = 0; i < Flight.Count; i++)
            {
                int profile = Flight.ProfileAt(i);
                if (profile == NoProfile)
                {
                    batch.Add(Flight.PositionAt(i), Vector3.one * _diameter[i], _color[i]);
                    continue;
                }

                ManagedSpellDefinition spell = ManagedSpellDefinition.At(profile);
                float charge = Flight.SpFractionAt(i);
                float dim = MinDim + (1f - MinDim) * charge;
                Color color = new Color(spell.Color.r * dim, spell.Color.g * dim, spell.Color.b * dim, spell.Color.a);
                float diameter = spell.VisualDiameter * (MinScale + (1f - MinScale) * charge);
                batch.Add(Flight.PositionAt(i), Vector3.one * diameter, color);
            }
        }

        // Swap-removes a shot and its side arrays together, so index i always describes the same shot in both.
        private static void Retire(int index)
        {
            int last = Flight.Count - 1;
            if (index != last)
            {
                _source[index] = _source[last];
                _damage[index] = _damage[last];
                _radius[index] = _radius[last];
                _diameter[index] = _diameter[last];
                _color[index] = _color[last];
            }

            _source[last] = null;
            Flight.RemoveAt(index);
        }

        private static void Hurt(Collider collider, float damage, GameObject source)
        {
            if (collider == null)
            {
                return;
            }

            IManagedDamageable target = collider.GetComponentInParent<IManagedDamageable>();
            if (target != null && target.IsAlive)
            {
                target.TakeDamage(damage, source);
            }
        }

        private static void Detonate(ManagedSpellDefinition spell, int payload, Vector3 position, Vector3 normal, GameObject source)
        {
            ManagedSpellCaster.Raise(new ManagedSpellImpact
            {
                Position = position,
                Normal = normal,
                Kind = spell.Kind,
                PayloadId = payload,
                Source = source,
            });
        }

        /// <summary>
        /// The closest hit along a segment that does not belong to <paramref name="ignore"/>, or nothing. A hit on the
        /// ignored object restarts the sweep just past it, so the result is always the closest foreign collider. A zero
        /// radius sweeps a ray and a positive one a sphere.
        /// </summary>
        internal static bool Sweep(Vector3 start, Vector3 end, float radius, Transform ignore, out RaycastHit closest)
        {
            closest = default(RaycastHit);
            Vector3 delta = end - start;
            float remaining = delta.magnitude;
            if (remaining < 1e-5f)
            {
                return false;
            }

            Vector3 direction = delta / remaining;
            Vector3 origin = start;
            int mask = HitMask;

            for (int attempt = 0; attempt < MaxOwnHitSkips && remaining > 1e-4f; attempt++)
            {
                RaycastHit hit;
                bool found = radius > 0f
                    ? Physics.SphereCast(origin, radius, direction, out hit, remaining, mask, QueryTriggerInteraction.Ignore)
                    : Physics.Raycast(origin, direction, out hit, remaining, mask, QueryTriggerInteraction.Ignore);

                if (!found)
                {
                    return false;
                }

                if (ignore == null || !hit.collider.transform.IsChildOf(ignore))
                {
                    closest = hit;
                    return true;
                }

                float advance = hit.distance + RestartPadding;
                origin += direction * advance;
                remaining -= advance;
            }

            return false;
        }

        /// <summary>True when a foreign collider already overlaps the point, so a shot born there would sweep from the wrong side.</summary>
        internal static bool IsBlockedAt(Vector3 point, float radius, Transform ignore)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radius, Overlaps, HitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = Overlaps[i];
                Overlaps[i] = null;
                if (ignore == null || !collider.transform.IsChildOf(ignore))
                {
                    for (int j = i + 1; j < count; j++)
                    {
                        Overlaps[j] = null;
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
