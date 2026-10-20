// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedNpcFavor.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.NPC;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Favor
{
    /// <summary>
    /// The favor meter of one relevant NPC: how much this person likes the player, from -100 to 100. Put it next to the
    /// ManagedNpcBrain of a shopkeeper, a guard captain, a quest giver or anybody whose opinion the story cares about.
    /// An NPC without one has no opinion of its own and shares the feeling of the crowd of the region it stands in, see
    /// <see cref="ManagedFavorRegion"/>. Every live gauge counts toward the Local Favor of its region, so the favor of
    /// the individuals builds up into the favor of the town.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedNpcFavor : MonoBehaviour
    {
        private static readonly List<ManagedNpcFavor> All = new List<ManagedNpcFavor>();

        [Header("Meter")]
        [Range(-100f, 100f)]
        [Tooltip("Favor when the scene starts. Zero is neutral, above zero likes the player, below zero dislikes.")]
        [SerializeField] private float _favor;
        [Tooltip("Optional. The region this NPC counts toward, wherever it walks. Left empty, it counts toward the region whose box holds it right now.")]
        [SerializeField] private ManagedFavorRegion _homeRegion;

        [Header("Reactions")]
        [Tooltip("Favor lost when the player hurts this NPC. Killing is three times as heavy, hurting is one.")]
        [SerializeField] private float _hurtPenalty = 20f;
        [Tooltip("Favor lost when the player hurts somebody else while this NPC has the player in sight. Killing is three times as heavy.")]
        [SerializeField] private float _witnessPenalty = 5f;

        private float _value;
        private bool _started;
        private ManagedNpcBrain _brain;

        /// <summary>
        /// Counts every change of any gauge, so a region can tell its Local Favor is out of date. Only ever grows, and
        /// only compared for equality.
        /// </summary>
        internal static int Version;

        /// <summary>(this, old favor, new favor). Raised when the value changes.</summary>
        public event Action<ManagedNpcFavor, float, float> Changed;

        public static int Count { get { return All.Count; } }

        public static ManagedNpcFavor Get(int index)
        {
            return All[index];
        }

        /// <summary>Favor from -100 to 100.</summary>
        public float Value
        {
            get
            {
                Begin();
                return _value;
            }
        }

        /// <summary>Favor from -1 to 1, the number the native decision is sent.</summary>
        public float Normalized { get { return ManagedFavorMath.Normalize(Value); } }

        /// <summary>The named band of the favor, see <see cref="ManagedFavorTier"/>.</summary>
        public int Tier { get { return ManagedFavorTier.For(Value); } }

        /// <summary>Favor lost when the player hurts this NPC, before it is scaled by how bad the act was.</summary>
        public float HurtPenalty { get { return _hurtPenalty; } }

        /// <summary>Favor lost when the player hurts somebody else in this NPC's sight, before it is scaled by how bad the act was.</summary>
        public float WitnessPenalty { get { return _witnessPenalty; } }

        /// <summary>False once the NPC is dead, and a dead NPC no longer counts toward its region.</summary>
        public bool IsAlive
        {
            get
            {
                Link();
                return _brain == null || _brain.IsAlive;
            }
        }

        /// <summary>The brain on this object, which the favor of a region reads for what the NPC sees. Null for a gauge on an object with no brain.</summary>
        internal ManagedNpcBrain Brain
        {
            get
            {
                Link();
                return _brain;
            }
        }

        /// <summary>The region this NPC counts toward: the home region when one is set, otherwise the one that holds it now. Null in the wilds.</summary>
        public ManagedFavorRegion Region
        {
            get
            {
                if (_homeRegion != null)
                {
                    return _homeRegion;
                }

                return ManagedFavorRegion.Find(transform.position);
            }
        }

        /// <summary>Adds to the favor, or takes away with a negative number. The result stays between -100 and 100.</summary>
        public void Add(float delta)
        {
            if (float.IsNaN(delta))
            {
                return;
            }

            Begin();
            Move(_value + delta);
        }

        /// <summary>Sets the favor, held between -100 and 100.</summary>
        public void Set(float favor)
        {
            if (float.IsNaN(favor))
            {
                return;
            }

            Begin();
            Move(favor);
        }

        /// <summary>One line for the NPC debugger.</summary>
        public string Describe()
        {
            return name + "  favor " + Value.ToString("0.0") + "  " + ManagedFavorTier.Name(Tier);
        }

        private void Move(float target)
        {
            float next = ManagedFavorMath.Clamp(target);
            if (next == _value)
            {
                return;
            }

            float old = _value;
            _value = next;
            Version++;
            Action<ManagedNpcFavor, float, float> changed = Changed;
            if (changed != null)
            {
                changed(this, old, next);
            }
        }

        // Called from Awake and from the properties, for a gauge made in code before its Awake ran.
        private void Begin()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _value = ManagedFavorMath.Clamp(_favor);
        }

        // Finds the brain on this object and hands it the gauge, so the brain can send the favor to the decision.
        private void Link()
        {
            if (_brain == null)
            {
                _brain = GetComponent<ManagedNpcBrain>();
            }
        }

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            All.Clear();
            Version = 0;
        }

        private void Awake()
        {
            Begin();
        }

        private void OnEnable()
        {
            Begin();
            Link();
            if (_brain != null)
            {
                _brain.FavorGauge = this;
            }

            if (!All.Contains(this))
            {
                All.Add(this);
            }

            Version++;
        }

        private void OnDisable()
        {
            if (_brain != null && _brain.FavorGauge == this)
            {
                _brain.FavorGauge = null;
            }

            All.Remove(this);
            Version++;
        }

        private void OnValidate()
        {
            _favor = Mathf.Clamp(_favor, -ManagedFavorMath.Max, ManagedFavorMath.Max);
            _hurtPenalty = Mathf.Max(0f, _hurtPenalty);
            _witnessPenalty = Mathf.Max(0f, _witnessPenalty);
        }
    }
}
