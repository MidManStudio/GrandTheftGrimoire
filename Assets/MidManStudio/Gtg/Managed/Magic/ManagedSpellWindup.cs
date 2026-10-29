// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSpellWindup.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>
    /// A cast in progress, as another system sees it. The caster fills it each frame while a wind-up runs.
    /// Nothing in Magic knows who reads it: a glyph view reads it today, a HUD cast bar could tomorrow.
    /// </summary>
    public struct ManagedCastStatus
    {
        /// <summary>False when no cast is winding up. The other fields are then zero.</summary>
        public bool Active;

        public ManagedSpellKind Kind;

        /// <summary>How far the wind-up has run, 0 at the press and 1 when the shot leaves.</summary>
        public float Charge;

        /// <summary>Length of the whole wind-up in seconds.</summary>
        public float Seconds;

        /// <summary>The shot point in world space.</summary>
        public Vector3 Origin;

        /// <summary>Unit vector toward where the shot will fly.</summary>
        public Vector3 Direction;
    }

    /// <summary>
    /// The timer of one wind-up: which spell, what the vessel gave up, how long it runs and how long it has run.
    /// It holds plain values and touches no Unity object, so the CI harness can drive it frame by frame.
    /// </summary>
    public sealed class ManagedSpellWindup
    {
        public bool Active { get; private set; }

        public ManagedSpellDefinition Spell { get; private set; }

        /// <summary>What the vessel gave up at the press. The shot flies with it when the wind-up ends.</summary>
        public ManagedVesselDraw Draw { get; private set; }

        public float Duration { get; private set; }

        public float Elapsed { get; private set; }

        /// <summary>0 at the press, 1 when the wind-up is over.</summary>
        public float Charge
        {
            get { return Active && Duration > 0f ? Mathf.Clamp01(Elapsed / Duration) : 0f; }
        }

        /// <summary>Starts a wind-up. A length of zero or less is ignored, because a spell with no cast time fires at once.</summary>
        public void Begin(ManagedSpellDefinition spell, ManagedVesselDraw draw, float seconds)
        {
            if (spell == null || seconds <= 0f)
            {
                return;
            }

            Active = true;
            Spell = spell;
            Draw = draw;
            Duration = seconds;
            Elapsed = 0f;
        }

        /// <summary>Moves the wind-up forward. Returns true on the call that completes it. The caller then reads Spell and Draw and calls <see cref="Clear"/>.</summary>
        public bool Tick(float dt)
        {
            if (!Active)
            {
                return false;
            }

            Elapsed += Mathf.Max(0f, dt);
            return Elapsed >= Duration;
        }

        /// <summary>Ends the wind-up without firing, or after it fired.</summary>
        public void Clear()
        {
            Active = false;
            Spell = null;
            Draw = default(ManagedVesselDraw);
            Duration = 0f;
            Elapsed = 0f;
        }
    }
}
