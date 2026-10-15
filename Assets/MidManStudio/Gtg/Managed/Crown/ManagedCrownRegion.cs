// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCrownRegion.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.NPC;
using MidManStudio.Gtg.NPC.Components;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Crown
{
    /// <summary>
    /// One area where the Crown's law holds, and the Crown Heat the player has earned in it. Heat rises when the
    /// player hurts or kills somebody the law protects while a guard of the region sees it, and fades while no guard
    /// sees the player. The tier of the heat is what guards of the region send to the native decision: from the wanted
    /// tier on, a retaliatory guard fights the player on sight. Outside every region nobody enforces the law.
    /// Put one on an empty object in each town, set its size and place it so that it covers the town.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedCrownRegion : MonoBehaviour
    {
        private const float ScanSeconds = 0.5f;

        private static readonly List<ManagedCrownRegion> Regions = new List<ManagedCrownRegion>();

        [Header("Area")]
        [SerializeField] private string _displayName = "Region";
        [Tooltip("Size of the box around this object, in meters. The box is not rotated.")]
        [SerializeField] private Vector3 _size = new Vector3(120f, 40f, 120f);

        [Header("Heat")]
        [Tooltip("The most heat the region can hold.")]
        [SerializeField] private float _maxHeat = 100f;
        [Tooltip("Heat at which the tier becomes 1, wanted.")]
        [SerializeField] private float _wantedAt = 10f;
        [Tooltip("Heat at which the tier becomes 2, hunted.")]
        [SerializeField] private float _huntedAt = 40f;
        [Tooltip("Heat at which the tier becomes 3, outlaw.")]
        [SerializeField] private float _outlawAt = 75f;
        [Tooltip("Seconds without a guard of the region seeing the player, after the last crime or the last sighting, before the heat starts to fade.")]
        [SerializeField] private float _unseenSeconds = 20f;
        [Tooltip("Heat lost per second once it fades.")]
        [SerializeField] private float _fadePerSecond = 0.5f;

        [Header("Crimes")]
        [Tooltip("Heat for hurting a merchant or a civilian in sight of a guard.")]
        [SerializeField] private float _assaultHeat = 5f;
        [Tooltip("Heat for killing a merchant or a civilian in sight of a guard.")]
        [SerializeField] private float _murderHeat = 25f;
        [Tooltip("Heat for hurting a guard in sight of a guard.")]
        [SerializeField] private float _assaultOnGuardHeat = 15f;
        [Tooltip("Heat for killing a guard in sight of a guard.")]
        [SerializeField] private float _murderOfGuardHeat = 40f;
        [Tooltip("Seconds before hurting the same victim again counts as a new assault. A kill always counts.")]
        [SerializeField] private float _assaultRepeatSeconds = 3f;

        private float _heat;
        private int _tier;
        private float _lastSeenOrCrime = float.NegativeInfinity;
        private float _nextScan;

        /// <summary>(this, old tier, new tier). Raised when the tier changes, from a crime, a bribe or the fade.</summary>
        public event Action<ManagedCrownRegion, int, int> TierChanged;

        /// <summary>(this, kind, seen by a guard). Raised for every crime in the region. Heat is added only when it was seen.</summary>
        public event Action<ManagedCrownRegion, ManagedCrimeKind, bool> CrimeReported;

        public static int Count { get { return Regions.Count; } }

        public static ManagedCrownRegion Get(int index)
        {
            return Regions[index];
        }

        public string DisplayName { get { return _displayName; } }

        /// <summary>Heat from 0 to <see cref="MaxHeat"/>.</summary>
        public float Heat { get { return _heat; } }

        public float MaxHeat { get { return _maxHeat; } }

        /// <summary>Heat at which the tier becomes 1, wanted.</summary>
        public float WantedAt { get { return _wantedAt; } }

        /// <summary>Heat at which the tier becomes 2, hunted.</summary>
        public float HuntedAt { get { return _huntedAt; } }

        /// <summary>Heat at which the tier becomes 3, outlaw.</summary>
        public float OutlawAt { get { return _outlawAt; } }

        /// <summary>0 clear, 1 wanted, 2 hunted, 3 outlaw. See <see cref="ManagedCrownTier"/>.</summary>
        public int Tier { get { return _tier; } }

        /// <summary>Seconds a hit on the same victim has to wait to count as a new assault.</summary>
        public float AssaultRepeatSeconds { get { return _assaultRepeatSeconds; } }

        /// <summary>The first region whose box holds the point, or null. Where boxes overlap, the one that registered first wins.</summary>
        public static ManagedCrownRegion Find(Vector3 point)
        {
            for (int i = 0; i < Regions.Count; i++)
            {
                if (Regions[i].Contains(point))
                {
                    return Regions[i];
                }
            }

            return null;
        }

        /// <summary>The tier of the region around the point, 0 outside every region.</summary>
        public static int TierAt(Vector3 point)
        {
            ManagedCrownRegion region = Find(point);
            return region != null ? region._tier : ManagedCrownTier.Clear;
        }

        public bool Contains(Vector3 point)
        {
            Vector3 center = transform.position;
            return Mathf.Abs(point.x - center.x) <= _size.x * 0.5f
                && Mathf.Abs(point.y - center.y) <= _size.y * 0.5f
                && Mathf.Abs(point.z - center.z) <= _size.z * 0.5f;
        }

        /// <summary>Adds heat from outside a crime, for instance an NPC that causes a stir. It restarts the wait before the fade.</summary>
        public void Add(float amount)
        {
            if (!(amount > 0f))
            {
                return;
            }

            _lastSeenOrCrime = Time.time;
            SetHeat(_heat + amount);
        }

        /// <summary>Takes heat away, for instance a bribe that works only in part.</summary>
        public void Reduce(float amount)
        {
            if (!(amount > 0f))
            {
                return;
            }

            SetHeat(_heat - amount);
        }

        /// <summary>Wipes the heat, for instance a bribe to a corrupt official.</summary>
        public void Clear()
        {
            SetHeat(0f);
        }

        /// <summary>
        /// Records a crime of the offender in this region. It adds the heat of the kind when a guard of the region
        /// sees the offender, and returns whether one did. Nobody seeing it means no heat, whatever happened.
        /// </summary>
        public bool Report(ManagedCrimeKind kind, ManagedNpcThreatSource offender)
        {
            bool witnessed = offender != null && GuardSees(offender);
            if (witnessed)
            {
                Add(HeatFor(kind));
            }

            Action<ManagedCrownRegion, ManagedCrimeKind, bool> reported = CrimeReported;
            if (reported != null)
            {
                reported(this, kind, witnessed);
            }

            return witnessed;
        }

        /// <summary>One line for the NPC debugger.</summary>
        public string Describe()
        {
            return _displayName + "  heat " + _heat.ToString("0.0") + "/" + _maxHeat.ToString("0") + "  " + ManagedCrownTier.Name(_tier) + " (tier " + _tier + ")";
        }

        // Called by a brain when something hurts it. Decides whether that is a crime and where it happened.
        internal static void ReportHurt(ManagedNpcBrain victim, bool killed, GameObject source)
        {
            if (Regions.Count == 0 || source == null)
            {
                return;
            }

            ManagedCrimeKind kind;
            switch (victim.Role)
            {
                case NpcRole.Merchant:
                case NpcRole.Civilian:
                    kind = killed ? ManagedCrimeKind.Murder : ManagedCrimeKind.Assault;
                    break;
                case NpcRole.Guard:
                    kind = killed ? ManagedCrimeKind.MurderOfGuard : ManagedCrimeKind.AssaultOnGuard;
                    break;
                default:
                    return;
            }

            ManagedNpcThreatSource offender = source.GetComponentInParent<ManagedNpcThreatSource>();
            if (offender == null)
            {
                return;
            }

            ManagedCrownRegion region = Find(victim.transform.position);
            if (region == null)
            {
                return;
            }

            if (!killed)
            {
                float now = Time.time;
                if (now < victim.CrimeRepeatTime)
                {
                    return;
                }

                victim.CrimeRepeatTime = now + region._assaultRepeatSeconds;
            }

            region.Report(kind, offender);
        }

        private float HeatFor(ManagedCrimeKind kind)
        {
            switch (kind)
            {
                case ManagedCrimeKind.Murder: return _murderHeat;
                case ManagedCrimeKind.AssaultOnGuard: return _assaultOnGuardHeat;
                case ManagedCrimeKind.MurderOfGuard: return _murderOfGuardHeat;
                default: return _assaultHeat;
            }
        }

        // A live guard of this region that has the offender in view right now.
        private bool GuardSees(ManagedNpcThreatSource offender)
        {
            int count = ManagedNpcDirector.RegisteredCount;
            for (int i = 0; i < count; i++)
            {
                ManagedNpcBrain brain = ManagedNpcDirector.GetBrain(i);
                if (brain.Role == NpcRole.Guard && brain.IsAlive && brain.ThreatVisible && brain.Threat == offender && Contains(brain.transform.position))
                {
                    return true;
                }
            }

            return false;
        }

        // A live guard of this region that sees any threat.
        private bool AnyGuardSees()
        {
            int count = ManagedNpcDirector.RegisteredCount;
            for (int i = 0; i < count; i++)
            {
                ManagedNpcBrain brain = ManagedNpcDirector.GetBrain(i);
                if (brain.Role == NpcRole.Guard && brain.IsAlive && brain.ThreatVisible && Contains(brain.transform.position))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetHeat(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, _maxHeat);
            if (clamped == _heat)
            {
                return;
            }

            _heat = clamped;
            int old = _tier;
            _tier = TierFor(_heat);
            if (_tier != old)
            {
                Action<ManagedCrownRegion, int, int> changed = TierChanged;
                if (changed != null)
                {
                    changed(this, old, _tier);
                }
            }
        }

        private int TierFor(float heat)
        {
            if (!(heat > 0f))
            {
                return ManagedCrownTier.Clear;
            }

            if (heat >= _outlawAt)
            {
                return ManagedCrownTier.Outlaw;
            }

            if (heat >= _huntedAt)
            {
                return ManagedCrownTier.Hunted;
            }

            return heat >= _wantedAt ? ManagedCrownTier.Wanted : ManagedCrownTier.Clear;
        }

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Regions.Clear();
        }

        private void OnEnable()
        {
            if (!Regions.Contains(this))
            {
                Regions.Add(this);
            }
        }

        private void OnDisable()
        {
            Regions.Remove(this);
        }

        private void OnValidate()
        {
            _size = new Vector3(Mathf.Max(0.1f, _size.x), Mathf.Max(0.1f, _size.y), Mathf.Max(0.1f, _size.z));
            _maxHeat = Mathf.Max(1f, _maxHeat);
            _wantedAt = Mathf.Clamp(_wantedAt, 0.01f, _maxHeat);
            _huntedAt = Mathf.Clamp(_huntedAt, _wantedAt, _maxHeat);
            _outlawAt = Mathf.Clamp(_outlawAt, _huntedAt, _maxHeat);
            _unseenSeconds = Mathf.Max(0f, _unseenSeconds);
            _fadePerSecond = Mathf.Max(0f, _fadePerSecond);
            _assaultHeat = Mathf.Max(0f, _assaultHeat);
            _murderHeat = Mathf.Max(0f, _murderHeat);
            _assaultOnGuardHeat = Mathf.Max(0f, _assaultOnGuardHeat);
            _murderOfGuardHeat = Mathf.Max(0f, _murderOfGuardHeat);
            _assaultRepeatSeconds = Mathf.Max(0f, _assaultRepeatSeconds);
        }

        private void Update()
        {
            if (_heat <= 0f)
            {
                return;
            }

            float now = Time.time;
            if (now >= _nextScan)
            {
                _nextScan = now + ScanSeconds;
                if (AnyGuardSees())
                {
                    _lastSeenOrCrime = now;
                }
            }

            if (now - _lastSeenOrCrime >= _unseenSeconds)
            {
                SetHeat(_heat - _fadePerSecond * Time.deltaTime);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _tier >= ManagedCrownTier.Wanted ? Color.red : Color.yellow;
            Gizmos.DrawWireCube(transform.position, _size);
        }
    }
}
