// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSpellDefinition.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>
    /// Tuning of one spell. The table lives in code for now and moves to an mdix table
    /// later, like the chemistry recipes. The order of the table is the slot order, so
    /// key 1 picks the first entry.
    /// </summary>
    public sealed class ManagedSpellDefinition
    {
        public ManagedSpellKind Kind;
        public string DisplayName;
        public float CooldownSeconds;

        /// <summary>
        /// Speed of the shot. A powered shot holds this speed for its whole flight. An
        /// unpowered shot, a thrown one, leaves at this speed and then follows gravity and drag.
        /// </summary>
        public float Speed;

        /// <summary>
        /// How long a clean mix flies at full speed, in seconds. The drive cost per meter is
        /// derived from it, so the table keeps the range a designer thinks in. The hard cap on
        /// flight time is twice this.
        /// </summary>
        public float LifetimeSeconds;

        /// <summary>True when the vessel's SP drives the shot. False for a thrown shot.</summary>
        public bool Powered = true;

        /// <summary>Share of world gravity that bends the path. Zero flies straight.</summary>
        public float GravityScale;

        /// <summary>Speed lost to drag per second. Only matters for an unpowered shot.</summary>
        public float DragPerSecond;

        // The four values below describe the stand-in vessel each cast uses until vessel
        // items exist. Later the vessel item supplies them and they leave this table.

        /// <summary>SP in the vessel's crystal at the cast.</summary>
        public float VesselSp = 100f;

        /// <summary>Load the vessel holds without extra cost, on the same scale as payload load.</summary>
        public float VesselRating = 10f;

        /// <summary>SP per second the vessel spends to hold the bubble, whatever the mix.</summary>
        public float HoldSpPerSecond = 4f;

        /// <summary>Extra SP per second for each unit of load above the vessel's rating.</summary>
        public float OverloadSpPerLoad = 3f;

        /// <summary>Radius of the sphere swept along the path, so thin colliders are still hit.</summary>
        public float SweepRadius;

        /// <summary>Diameter of the drawn shot.</summary>
        public float VisualDiameter;

        public Color Color;

        /// <summary>Damage at the center of the explosion. Zero means the spell hurts nothing by itself.</summary>
        public float ImpactDamage;

        /// <summary>Radius of the damaging explosion in meters.</summary>
        public float ImpactRadius;

        /// <summary>Share of the damage that remains at the edge of the radius, from 0 to 1.</summary>
        public float ImpactEdgeFraction;

        public static readonly ManagedSpellDefinition Fireball = new ManagedSpellDefinition
        {
            Kind = ManagedSpellKind.Fireball,
            DisplayName = "Fireball",
            CooldownSeconds = 0.4f,
            Speed = 25f,
            LifetimeSeconds = 3f,
            SweepRadius = 0.15f,
            VisualDiameter = 0.4f,
            Color = new Color(1f, 0.45f, 0.1f, 1f),
            ImpactDamage = 40f,
            ImpactRadius = 3f,
            ImpactEdgeFraction = 0.25f,
        };

        public static readonly ManagedSpellDefinition Ice = new ManagedSpellDefinition
        {
            Kind = ManagedSpellKind.Ice,
            DisplayName = "Ice",
            CooldownSeconds = 0.6f,
            Speed = 18f,
            LifetimeSeconds = 3.5f,
            SweepRadius = 0.15f,
            VisualDiameter = 0.35f,
            Color = new Color(0.55f, 0.85f, 1f, 1f),
        };

        /// <summary>
        /// SP per meter of drive. Zero for an unpowered shot. Derived so that a clean mix at
        /// full speed flies for <see cref="LifetimeSeconds"/>.
        /// </summary>
        public float DriveSpPerMeter
        {
            get
            {
                if (!Powered || Speed <= 0f || LifetimeSeconds <= 0f)
                {
                    return 0f;
                }

                return Mathf.Max(0f, VesselSp / LifetimeSeconds - HoldSpPerSecond) / Speed;
            }
        }

        /// <summary>Hard cap on flight time, a safety net for a spell whose burn is zero.</summary>
        public float MaxFlightSeconds
        {
            get { return LifetimeSeconds * 2f; }
        }

        /// <summary>Hold cost per second for a payload of the given load. Only load above the rating costs extra.</summary>
        public float HoldSpPerSecondFor(float load)
        {
            return HoldSpPerSecond + OverloadSpPerLoad * Mathf.Max(0f, load - VesselRating);
        }

        /// <summary>
        /// Builds the flight record for a cast of this spell from the stand-in vessel. This is
        /// the one place that turns authored numbers into flight values, so a vessel item
        /// replaces it later and flight itself never changes.
        /// </summary>
        public ManagedShotSpawn CreateSpawn(int profile, int payload, Vector3 origin, Vector3 direction)
        {
            return new ManagedShotSpawn
            {
                Position = origin,
                Velocity = direction * Speed,
                Profile = profile,
                Payload = payload,
                Sp = VesselSp,
                HoldBurnPerSecond = HoldSpPerSecondFor(ManagedSpellPayloads.LoadOf(payload)),
                DriveBurnPerMeter = DriveSpPerMeter,
                MaxAgeSeconds = MaxFlightSeconds,
                Powered = Powered,
                Speed = Speed,
                GravityScale = GravityScale,
                DragPerSecond = DragPerSecond,
            };
        }

        private static readonly ManagedSpellDefinition[] Slots = { Fireball, Ice };

        public static int Count { get { return Slots.Length; } }

        public static ManagedSpellDefinition At(int index)
        {
            return Slots[index];
        }

        /// <summary>The definition of a kind, or null when the table has none.</summary>
        public static ManagedSpellDefinition For(ManagedSpellKind kind)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Kind == kind)
                {
                    return Slots[i];
                }
            }

            return null;
        }
    }
}
