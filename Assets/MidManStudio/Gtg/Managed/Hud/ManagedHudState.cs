// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudState.cs"
// ============================================================================

using System.Collections.Generic;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// What the HUD shows, as plain values. The feed writes it and the widgets read it, and
    /// neither knows the other. Every setter holds its value to a range the widgets can draw.
    /// </summary>
    public sealed class ManagedHudState
    {
        /// <summary>Mission lines kept. More are dropped.</summary>
        public const int MaxMissionLines = 4;

        private readonly List<string> _missionLines = new List<string>(MaxMissionLines);

        public float HpFraction { get; private set; }
        public float HpCurrent { get; private set; }
        public float HpMax { get; private set; }

        /// <summary>The charge of the selected vessel, 0 through 1.</summary>
        public float SpFraction { get; private set; }

        /// <summary>False when there is no vessel to read, which the bar shows as empty.</summary>
        public bool HasVessel { get; private set; }

        public long Gold { get; private set; }
        public int Silver { get; private set; }

        /// <summary>Filled heat sections, 0 through <see cref="ManagedHudMath.MaxHeat"/>.</summary>
        public int HeatSections { get; private set; }

        /// <summary>The way the player faces, in degrees.</summary>
        public float YawDegrees { get; private set; }

        public string MissionTitle { get; private set; }

        /// <summary>
        /// Counts the times the missions panel changed. <see cref="SetMissions"/> with what is shown already
        /// does not count, so a widget can redraw when this differs from the value it drew, and a feed may set
        /// the missions every frame.
        /// </summary>
        public int MissionsVersion { get; private set; }

        /// <summary>
        /// True while the health bar, the vessel bar and the quick slots are hidden to make room
        /// for another screen at the bottom, such as the build tray.
        /// </summary>
        public bool BottomHidden { get; private set; }

        public ManagedHudState()
        {
            MissionTitle = string.Empty;
        }

        /// <summary>Heat shows its crowns while there is any.</summary>
        public bool Wanted { get { return HeatSections > 0; } }

        public int MissionLineCount { get { return _missionLines.Count; } }

        public string MissionLineAt(int index)
        {
            return index >= 0 && index < _missionLines.Count ? _missionLines[index] : string.Empty;
        }

        /// <summary>
        /// Sets the health. A maximum of 0 or less gives an empty bar and a maximum of 0, and the
        /// current amount is held between 0 and the maximum.
        /// </summary>
        public void SetHealth(float current, float max)
        {
            if (float.IsNaN(current) || float.IsNaN(max) || max <= 0f)
            {
                HpCurrent = 0f;
                HpMax = 0f;
                HpFraction = 0f;
                return;
            }

            HpMax = max;
            HpCurrent = current < 0f ? 0f : (current > max ? max : current);
            HpFraction = ManagedHudMath.BarFill(HpCurrent / max);
        }

        /// <summary>Sets the vessel charge. Without a vessel the fraction is 0.</summary>
        public void SetVessel(bool hasVessel, float fraction)
        {
            HasVessel = hasVessel;
            SpFraction = hasVessel ? ManagedHudMath.BarFill(fraction) : 0f;
        }

        /// <summary>Sets the coins. Negative amounts are held at 0.</summary>
        public void SetCoins(long gold, int silver)
        {
            Gold = gold < 0 ? 0 : gold;
            Silver = silver < 0 ? 0 : silver;
        }

        public void SetHeat(int sections)
        {
            HeatSections = ManagedHudMath.ClampHeat(sections);
        }

        public void SetBottomHidden(bool hidden)
        {
            BottomHidden = hidden;
        }

        public void SetYaw(float degrees)
        {
            YawDegrees = float.IsNaN(degrees) || float.IsInfinity(degrees) ? 0f : degrees;
        }

        /// <summary>
        /// Sets the mission panel. A null title is empty, and a null list clears the lines. Lines
        /// beyond <see cref="MaxMissionLines"/> are dropped, and a null line is empty.
        /// </summary>
        public void SetMissions(string title, IList<string> lines)
        {
            string newTitle = title ?? string.Empty;
            int newCount = lines == null ? 0 : (lines.Count < MaxMissionLines ? lines.Count : MaxMissionLines);

            bool same = newTitle == MissionTitle && newCount == _missionLines.Count;
            for (int i = 0; same && i < newCount; i++)
            {
                same = (lines[i] ?? string.Empty) == _missionLines[i];
            }

            if (same)
            {
                return;
            }

            MissionTitle = newTitle;
            _missionLines.Clear();
            for (int i = 0; i < newCount; i++)
            {
                _missionLines.Add(lines[i] ?? string.Empty);
            }

            MissionsVersion++;
        }
    }
}
