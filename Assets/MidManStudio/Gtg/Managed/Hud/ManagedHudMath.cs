// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudMath.cs"
// ============================================================================

using System;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// The numbers behind the HUD: bar fill, compass offsets, crown sections and the hit test of the
    /// wheel. Plain functions on plain values, so they can be tested without a scene.
    /// </summary>
    public static class ManagedHudMath
    {
        /// <summary>Crowns shown for heat.</summary>
        public const int CrownCount = 3;

        /// <summary>Sections in one crown.</summary>
        public const int SectionsPerCrown = 3;

        /// <summary>The most heat sections there can be, every section of every crown.</summary>
        public const int MaxHeat = CrownCount * SectionsPerCrown;

        /// <summary>A fraction held to 0 through 1. Not a number is 0.</summary>
        public static float BarFill(float fraction)
        {
            if (float.IsNaN(fraction) || fraction <= 0f)
            {
                return 0f;
            }

            return fraction >= 1f ? 1f : fraction;
        }

        /// <summary>Heat held to 0 through <see cref="MaxHeat"/>.</summary>
        public static int ClampHeat(int heat)
        {
            if (heat < 0)
            {
                return 0;
            }

            return heat > MaxHeat ? MaxHeat : heat;
        }

        /// <summary>
        /// The filled sections, 0 through 3, of a crown. The first crown fills first, then the
        /// second, then the third. A crown index outside 0 through 2 has none.
        /// </summary>
        public static int CrownSections(int heat, int crown)
        {
            if (crown < 0)
            {
                return 0;
            }

            int left = ClampHeat(heat) - crown * SectionsPerCrown;
            if (left < 0)
            {
                return 0;
            }

            return left > SectionsPerCrown ? SectionsPerCrown : left;
        }

        /// <summary>How much of a crown is lit for its filled sections, in thirds.</summary>
        public static float CrownFill(int sections)
        {
            if (sections <= 0)
            {
                return 0f;
            }

            return sections >= SectionsPerCrown ? 1f : sections / (float)SectionsPerCrown;
        }

        /// <summary>An angle in degrees brought into more than -180 and up to 180. Not a number or infinity is 0.</summary>
        public static float WrapDegrees(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
            {
                return 0f;
            }

            float d = degrees % 360f;
            if (d > 180f)
            {
                d -= 360f;
            }
            else if (d <= -180f)
            {
                d += 360f;
            }

            return d;
        }

        /// <summary>
        /// Where a bearing sits on the compass bar, in pixels from the middle. Positive is right.
        /// Facing a bearing puts it in the middle, and turning right moves it left. The shortest
        /// way round is used, so a bearing 350 degrees away is 10 degrees the other way.
        /// </summary>
        public static float CompassOffset(float yawDegrees, float bearingDegrees, float pixelsPerDegree)
        {
            return WrapDegrees(bearingDegrees - yawDegrees) * pixelsPerDegree;
        }

        /// <summary>
        /// The opacity of a compass mark at an offset: 1 inside the bar, falling to 0 over the last
        /// <paramref name="fadePixels"/> before the edge, and 0 past the edge.
        /// </summary>
        public static float CompassAlpha(float offset, float halfWidth, float fadePixels)
        {
            float distance = Math.Abs(offset);
            if (distance >= halfWidth)
            {
                return 0f;
            }

            // With no fade the edge is the half width, and a mark inside it is solid.
            float edge = halfWidth - fadePixels;
            if (distance <= edge)
            {
                return 1f;
            }

            return BarFill((halfWidth - distance) / fadePixels);
        }

        /// <summary>
        /// The section of the wheel under a point, counted from the wheel's middle with y up.
        /// Section 0 starts straight up and the sections run clockwise, so with four sections 0 is
        /// the upper right, 1 the lower right, 2 the lower left and 3 the upper left. Returns -1
        /// inside the dead radius, past the outer radius, for a count below 1, or for a point that is not a number.
        /// </summary>
        public static int WheelSectionAt(float dx, float dy, int sectionCount, float deadRadius, float outerRadius)
        {
            if (sectionCount < 1 || float.IsNaN(dx) || float.IsNaN(dy))
            {
                return -1;
            }

            double distance2 = (double)dx * dx + (double)dy * dy;
            if (distance2 < (double)deadRadius * deadRadius || distance2 > (double)outerRadius * outerRadius)
            {
                return -1;
            }

            double angle = Math.Atan2(dx, dy) * 180.0 / Math.PI;
            if (angle < 0.0)
            {
                angle += 360.0;
            }

            int section = (int)(angle / (360.0 / sectionCount));

            // A point a hair left of straight up rounds to 360 degrees, which is section 0 again.
            return section >= sectionCount ? 0 : section;
        }

        /// <summary>The direction, clockwise from straight up, of the middle of a section. 0 for a count below 1.</summary>
        public static float WheelSectionAngle(int section, int sectionCount)
        {
            if (sectionCount < 1)
            {
                return 0f;
            }

            return (section + 0.5f) * 360f / sectionCount;
        }

        /// <summary>The x and y, with y up, of the unit vector from the wheel's middle to the middle of a section.</summary>
        public static void WheelSectionDirection(int section, int sectionCount, out float x, out float y)
        {
            double radians = WheelSectionAngle(section, sectionCount) * Math.PI / 180.0;
            x = (float)Math.Sin(radians);
            y = (float)Math.Cos(radians);
        }
    }
}
