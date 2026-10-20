// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedFavorRegion.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Crown;
using MidManStudio.Gtg.Managed.NPC;
using MidManStudio.Gtg.NPC.Components;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Favor
{
    /// <summary>
    /// One area, a town or a district, and what the people of it think of the player. The Local Favor of the area is
    /// built from the favor of the relevant NPCs that stand in it, each with a <see cref="ManagedNpcFavor"/>, and from a
    /// crowd value that stands for everybody else, the people nobody gave a gauge. An NPC with no gauge feels the crowd
    /// value of the area it stands in. Hurting a townsperson lowers the favor of the victim, of the relevant NPCs that
    /// watched, and of the crowd. Put one on an empty object in each town, set its size and place it so that it covers the town.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedFavorRegion : MonoBehaviour
    {
        // Seconds between two refreshes of the Local Favor. The members walk from region to region, so it follows them.
        private const float RefreshSeconds = 0.5f;

        // Seconds before a second hit on the same victim counts as a new assault, for a victim in no region.
        private const float DefaultRepeatSeconds = 3f;

        private static readonly List<ManagedFavorRegion> Regions = new List<ManagedFavorRegion>();

        [Header("Area")]
        [SerializeField] private string _displayName = "Region";
        [Tooltip("Size of the box around this object, in meters. The box is not rotated.")]
        [SerializeField] private Vector3 _size = new Vector3(120f, 40f, 120f);

        [Header("Crowd")]
        [Range(-100f, 100f)]
        [Tooltip("Favor of everybody in the area who has no gauge of their own, when the scene starts.")]
        [SerializeField] private float _startCrowd;
        [Tooltip("How many relevant NPCs the crowd is worth when the Local Favor is built. A town with few relevant NPCs leans on the crowd, a town with many is their average.")]
        [SerializeField] private float _crowdWeight = 3f;
        [Tooltip("Crowd favor lost when the player hurts a merchant, a civilian or a guard in the area. Killing is three times as heavy, hurting is one.")]
        [SerializeField] private float _crowdPenalty = 4f;
        [Tooltip("Seconds before hurting the same victim again counts as a new assault. A kill always counts.")]
        [SerializeField] private float _assaultRepeatSeconds = 3f;

        private float _crowd;
        private float _local;
        private int _tier;
        private int _members;
        private bool _started;
        private bool _dirty = true;
        private int _seenVersion = -1;
        private float _nextRefresh;

        /// <summary>(this, old tier, new tier). Raised when the tier of the Local Favor changes.</summary>
        public event Action<ManagedFavorRegion, int, int> TierChanged;

        public static int Count { get { return Regions.Count; } }

        public static ManagedFavorRegion Get(int index)
        {
            return Regions[index];
        }

        public string DisplayName { get { return _displayName; } }

        /// <summary>How many relevant NPCs the crowd is worth in the Local Favor.</summary>
        public float CrowdWeight { get { return _crowdWeight; } }

        /// <summary>Crowd favor lost for hurting a townsperson, before it is scaled by how bad the act was.</summary>
        public float CrowdPenalty { get { return _crowdPenalty; } }

        /// <summary>Seconds a hit on the same victim has to wait to count as a new assault.</summary>
        public float AssaultRepeatSeconds { get { return _assaultRepeatSeconds; } }

        /// <summary>The favor of the people of the area who have no gauge, from -100 to 100.</summary>
        public float Crowd
        {
            get
            {
                Begin();
                return _crowd;
            }
        }

        /// <summary>The favor of the whole area, from -100 to 100: the gauges of its relevant NPCs and the crowd, see <see cref="ManagedFavorMath.Blend"/>.</summary>
        public float Local
        {
            get
            {
                EnsureFresh();
                return _local;
            }
        }

        /// <summary>The named band of the Local Favor, see <see cref="ManagedFavorTier"/>.</summary>
        public int Tier
        {
            get
            {
                EnsureFresh();
                return _tier;
            }
        }

        /// <summary>How many relevant NPCs counted toward the Local Favor at the last refresh.</summary>
        public int MemberCount
        {
            get
            {
                EnsureFresh();
                return _members;
            }
        }

        /// <summary>The first region whose box holds the point, or null. Where boxes overlap, the one that registered first wins.</summary>
        public static ManagedFavorRegion Find(Vector3 point)
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

        /// <summary>
        /// What an NPC with no gauge feels about the player at the point: the crowd favor of the region around it, from -1 to 1.
        /// Zero outside every region, which the native decision reads as neutral.
        /// </summary>
        public static float CrowdAt(Vector3 point)
        {
            ManagedFavorRegion region = Find(point);
            return region != null ? ManagedFavorMath.Normalize(region.Crowd) : 0f;
        }

        public bool Contains(Vector3 point)
        {
            Vector3 center = transform.position;
            return Mathf.Abs(point.x - center.x) <= _size.x * 0.5f
                && Mathf.Abs(point.y - center.y) <= _size.y * 0.5f
                && Mathf.Abs(point.z - center.z) <= _size.z * 0.5f;
        }

        /// <summary>Adds to the crowd favor, or takes away with a negative number. The result stays between -100 and 100.</summary>
        public void AddCrowd(float delta)
        {
            if (float.IsNaN(delta))
            {
                return;
            }

            Begin();
            MoveCrowd(_crowd + delta);
        }

        /// <summary>Sets the crowd favor, held between -100 and 100.</summary>
        public void SetCrowd(float favor)
        {
            if (float.IsNaN(favor))
            {
                return;
            }

            Begin();
            MoveCrowd(favor);
        }

        /// <summary>Builds the Local Favor again from the gauges standing in the area, and raises <see cref="TierChanged"/> when its tier changed.</summary>
        public void Refresh()
        {
            Begin();
            float sum = 0f;
            int members = 0;
            int gauges = ManagedNpcFavor.Count;
            for (int i = 0; i < gauges; i++)
            {
                ManagedNpcFavor gauge = ManagedNpcFavor.Get(i);
                if (gauge.IsAlive && gauge.Region == this)
                {
                    sum += gauge.Value;
                    members++;
                }
            }

            _members = members;
            _local = ManagedFavorMath.Blend(sum, members, _crowd, _crowdWeight);
            _dirty = false;
            _seenVersion = ManagedNpcFavor.Version;

            int old = _tier;
            _tier = ManagedFavorTier.For(_local);
            if (_tier != old)
            {
                Action<ManagedFavorRegion, int, int> changed = TierChanged;
                if (changed != null)
                {
                    changed(this, old, _tier);
                }
            }
        }

        /// <summary>One line for the NPC debugger.</summary>
        public string Describe()
        {
            EnsureFresh();
            return _displayName + "  local favor " + _local.ToString("0.0") + "  " + ManagedFavorTier.Name(_tier)
                + "  crowd " + _crowd.ToString("0.0") + "  members " + _members;
        }

        // Called by a brain when something hurts it. Hurting is a crime against favor when the player did it, and the
        // player is whatever has a threat source and is no NPC. The victim loses its own favor, a townsperson also costs the
        // favor of every relevant NPC that watched and the crowd of the area.
        internal static void ReportHurt(ManagedNpcBrain victim, bool killed, GameObject source)
        {
            if (source == null)
            {
                return;
            }

            ManagedNpcThreatSource offender = source.GetComponentInParent<ManagedNpcThreatSource>();
            if (offender == null || offender.GetComponentInParent<ManagedNpcBrain>() != null)
            {
                return;
            }

            ManagedFavorRegion region = Find(victim.transform.position);
            if (!killed)
            {
                float now = Time.time;
                if (now < victim.FavorRepeatTime)
                {
                    return;
                }

                victim.FavorRepeatTime = now + (region != null ? region._assaultRepeatSeconds : DefaultRepeatSeconds);
            }

            float severity = ManagedFavorSeverity.Of(killed ? ManagedCrimeKind.Murder : ManagedCrimeKind.Assault);

            ManagedNpcFavor own = victim.FavorGauge;
            if (own != null)
            {
                own.Add(-own.HurtPenalty * severity);
            }

            NpcRole role = victim.Role;
            if (role != NpcRole.Merchant && role != NpcRole.Civilian && role != NpcRole.Guard)
            {
                return;
            }

            int gauges = ManagedNpcFavor.Count;
            for (int i = 0; i < gauges; i++)
            {
                ManagedNpcFavor gauge = ManagedNpcFavor.Get(i);
                if (gauge == own || !gauge.IsAlive)
                {
                    continue;
                }

                ManagedNpcBrain watcher = gauge.Brain;
                if (watcher != null && watcher.ThreatVisible && watcher.Threat == offender)
                {
                    gauge.Add(-gauge.WitnessPenalty * severity);
                }
            }

            if (region != null)
            {
                region.AddCrowd(-region._crowdPenalty * severity);
            }
        }

        private void MoveCrowd(float target)
        {
            float next = ManagedFavorMath.Clamp(target);
            if (next == _crowd)
            {
                return;
            }

            _crowd = next;
            _dirty = true;
        }

        // Builds the Local Favor again when a crowd or a gauge changed since the last time.
        private void EnsureFresh()
        {
            Begin();
            if (_dirty || _seenVersion != ManagedNpcFavor.Version)
            {
                Refresh();
            }
        }

        // Called from the properties, for a region made in code before its Awake ran or set after it.
        private void Begin()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _crowd = ManagedFavorMath.Clamp(_startCrowd);
        }

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Regions.Clear();
        }

        private void Awake()
        {
            Begin();
        }

        private void OnEnable()
        {
            if (!Regions.Contains(this))
            {
                Regions.Add(this);
            }

            _dirty = true;
        }

        private void OnDisable()
        {
            Regions.Remove(this);
        }

        private void OnValidate()
        {
            _size = new Vector3(Mathf.Max(0.1f, _size.x), Mathf.Max(0.1f, _size.y), Mathf.Max(0.1f, _size.z));
            _startCrowd = Mathf.Clamp(_startCrowd, -ManagedFavorMath.Max, ManagedFavorMath.Max);
            _crowdWeight = Mathf.Max(0f, _crowdWeight);
            _crowdPenalty = Mathf.Max(0f, _crowdPenalty);
            _assaultRepeatSeconds = Mathf.Max(0f, _assaultRepeatSeconds);
        }

        private void Update()
        {
            float now = Time.time;
            if (now >= _nextRefresh)
            {
                _nextRefresh = now + RefreshSeconds;
                Refresh();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _tier >= ManagedFavorTier.Liked ? Color.green : (_tier <= ManagedFavorTier.Disliked ? Color.red : Color.yellow);
            Gizmos.DrawWireCube(transform.position, _size);
        }
    }
}
