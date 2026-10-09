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

        /// <summary>
        /// Fastest the shot can turn, in degrees per second. Zero flies straight. Above zero
        /// the shot is steered toward a lock chosen at the cast.
        /// </summary>
        public float TurnDegreesPerSecond;

        /// <summary>SP the vessel spends for each radian of turn.</summary>
        public float SteerSpPerRadian;

        /// <summary>How far the lock-on scan looks, in meters.</summary>
        public float SeekRange;

        /// <summary>Half-angle of the lock-on cone around the aim, in degrees.</summary>
        public float SeekConeHalfAngleDegrees;

        // The first two values below are what a vessel for this spell is made with, until
        // crafted vessels exist. The belt copies them into the vessel, and a cast reads them
        // back from the vessel it drew, so a crafted vessel can carry its own. The hold values
        // are tuning for the bubble and stay here.

        /// <summary>SP of a full vessel for this spell. The drive cost per meter is derived from it.</summary>
        public float VesselSp = 100f;

        /// <summary>Load a vessel for this spell holds without extra cost, on the same scale as payload load.</summary>
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
        /// Placeholder spell. A fireball variant that homes, so steering can be felt in the
        /// game. Every number is a placeholder, and so is the name.
        /// </summary>
        public static readonly ManagedSpellDefinition Seeker = new ManagedSpellDefinition
        {
            Kind = ManagedSpellKind.Seeker,
            DisplayName = "Seeker",
            CooldownSeconds = 0.8f,
            Speed = 20f,
            LifetimeSeconds = 3.5f,
            SweepRadius = 0.15f,
            VisualDiameter = 0.4f,
            Color = new Color(1f, 0.8f, 0.2f, 1f),
            ImpactDamage = 30f,
            ImpactRadius = 2.5f,
            ImpactEdgeFraction = 0.25f,
            TurnDegreesPerSecond = 140f,
            SteerSpPerRadian = 10f,
            SeekRange = 40f,
            SeekConeHalfAngleDegrees = 20f,
        };

        /// <summary>True when the shot is steered.</summary>
        public bool Seeks
        {
            get { return TurnDegreesPerSecond > 0f; }
        }

        /// <summary>
        /// SP per meter of drive. Zero for an unpowered shot. Derived so that a clean mix at
        /// full speed flies for <see cref="LifetimeSeconds"/>.
        /// </summary>
        public float DriveSpPerMeter
        {
            get { return DriveSpPerMeterFor(VesselSp); }
        }

        /// <summary>
        /// SP per meter of drive for a vessel of the given capacity. The cost depends on the
        /// capacity and not on the SP the shot starts with, so a half charged orb burns at the
        /// same rate and flies half as far.
        /// </summary>
        public float DriveSpPerMeterFor(float capacity)
        {
            if (!Powered || Speed <= 0f || LifetimeSeconds <= 0f)
            {
                return 0f;
            }

            return Mathf.Max(0f, capacity / LifetimeSeconds - HoldSpPerSecond) / Speed;
        }

        /// <summary>Hard cap on flight time, a safety net for a spell whose burn is zero.</summary>
        public float MaxFlightSeconds
        {
            get { return LifetimeSeconds * 2f; }
        }

        /// <summary>Hold cost per second for a payload of the given load. Only load above the rating costs extra.</summary>
        public float HoldSpPerSecondFor(float load)
        {
            return HoldSpPerSecondFor(load, VesselRating);
        }

        /// <summary>Hold cost per second for a payload of the given load in a vessel of the given rating.</summary>
        public float HoldSpPerSecondFor(float load, float rating)
        {
            return HoldSpPerSecond + OverloadSpPerLoad * Mathf.Max(0f, load - rating);
        }

        /// <summary>
        /// Builds the flight record for a cast of this spell from what the vessel gave up. This
        /// is the one place that turns authored numbers and a vessel into flight values, so a
        /// cheat or a crafted vessel changes them here and flight itself never changes.
        /// </summary>
        public ManagedShotSpawn CreateSpawn(
            int profile, int payload, Vector3 origin, Vector3 direction, ManagedVesselDraw draw)
        {
            return new ManagedShotSpawn
            {
                Position = origin,
                Velocity = direction * Speed,
                Profile = profile,
                Payload = payload,
                Sp = draw.Sp,
                HoldBurnPerSecond = HoldSpPerSecondFor(ManagedSpellPayloads.LoadOf(payload), draw.Rating),
                DriveBurnPerMeter = DriveSpPerMeterFor(draw.Capacity),
                MaxAgeSeconds = MaxFlightSeconds,
                Powered = Powered,
                Speed = Speed,
                GravityScale = GravityScale,
                DragPerSecond = DragPerSecond,
                TurnRatePerSecond = TurnDegreesPerSecond * Mathf.Deg2Rad,
                SteerBurnPerRadian = SteerSpPerRadian,
            };
        }

        private static readonly ManagedSpellDefinition[] Slots = { Fireball, Ice, Seeker };

        public static int Count { get { return Slots.Length; } }

        public static ManagedSpellDefinition At(int index)
        {
            return Slots[index];
        }

        /// <summary>The table index of a kind, or -1 when the table has none. A shot carries it as its profile.</summary>
        public static int IndexOf(ManagedSpellKind kind)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Kind == kind)
                {
                    return i;
                }
            }

            return -1;
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
