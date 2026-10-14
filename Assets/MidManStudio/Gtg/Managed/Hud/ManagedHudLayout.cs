// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudLayout.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// A rectangle placed by an anchor in its parent, in reference pixels. The anchor is 0, 0.5 or
    /// 1 on each axis, and the rectangle's pivot is the same point, so a rectangle anchored to the
    /// top right corner with a negative X and Y sits inside that corner. Y points up.
    /// </summary>
    public readonly struct ManagedHudRect
    {
        public readonly float AnchorX;
        public readonly float AnchorY;
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public ManagedHudRect(float anchorX, float anchorY, float x, float y, float width, float height)
        {
            AnchorX = anchorX;
            AnchorY = anchorY;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>The left edge in the parent, from the parent's left edge.</summary>
        public float Left(float parentWidth)
        {
            return AnchorX * parentWidth + X - AnchorX * Width;
        }

        /// <summary>The bottom edge in the parent, from the parent's bottom edge.</summary>
        public float Bottom(float parentHeight)
        {
            return AnchorY * parentHeight + Y - AnchorY * Height;
        }

        public float Right(float parentWidth)
        {
            return Left(parentWidth) + Width;
        }

        public float Top(float parentHeight)
        {
            return Bottom(parentHeight) + Height;
        }

        /// <summary>True when the two rectangles share any area. Rectangles that only touch do not overlap.</summary>
        public bool Overlaps(ManagedHudRect other, float parentWidth, float parentHeight)
        {
            return Left(parentWidth) < other.Right(parentWidth) && other.Left(parentWidth) < Right(parentWidth)
                && Bottom(parentHeight) < other.Top(parentHeight) && other.Bottom(parentHeight) < Top(parentHeight);
        }

        /// <summary>True when this rectangle lies inside a parent of the given size.</summary>
        public bool FitsIn(float parentWidth, float parentHeight)
        {
            return Left(parentWidth) >= 0f && Bottom(parentHeight) >= 0f
                && Right(parentWidth) <= parentWidth && Top(parentHeight) <= parentHeight;
        }
    }

    /// <summary>
    /// Where each piece of the HUD sits on a 1920 by 1080 canvas. The numbers are a first guess at
    /// the drawn draft, which is not aligned, and they are meant to be moved. The canvas scaler
    /// stretches them to the real screen. Pieces named for a parent are placed inside it.
    /// </summary>
    public static class ManagedHudLayout
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        // Top left: missions and tasks.
        public static readonly ManagedHudRect Missions = new ManagedHudRect(0f, 1f, 40f, -40f, 440f, 260f);
        public static readonly ManagedHudRect MissionsTitle = new ManagedHudRect(0f, 1f, 20f, -16f, 400f, 40f);
        public static readonly ManagedHudRect MissionsBody = new ManagedHudRect(0f, 1f, 20f, -64f, 400f, 180f);

        // Top middle: the compass bar.
        public static readonly ManagedHudRect Compass = new ManagedHudRect(0.5f, 1f, 0f, -24f, 640f, 56f);

        /// <summary>The span of bearings the bar shows, in degrees.</summary>
        public const float CompassSpanDegrees = 180f;

        /// <summary>Pixels for one degree on the bar.</summary>
        public const float CompassPixelsPerDegree = 640f / CompassSpanDegrees;

        /// <summary>Pixels over which a mark fades out at each end of the bar.</summary>
        public const float CompassFadePixels = 60f;

        /// <summary>Degrees between tick marks.</summary>
        public const int CompassTickDegrees = 15;

        /// <summary>Degrees between the letters, N, NE, E and so on.</summary>
        public const int CompassLabelDegrees = 45;

        // Top right: heat crowns and coins.
        public static readonly ManagedHudRect Status = new ManagedHudRect(1f, 1f, -40f, -40f, 460f, 160f);
        public static readonly ManagedHudRect GoldIcon = new ManagedHudRect(0f, 1f, 24f, -108f, 40f, 40f);
        public static readonly ManagedHudRect GoldText = new ManagedHudRect(0f, 1f, 76f, -108f, 150f, 40f);
        public static readonly ManagedHudRect SilverIcon = new ManagedHudRect(0f, 1f, 244f, -108f, 40f, 40f);
        public static readonly ManagedHudRect SilverText = new ManagedHudRect(0f, 1f, 296f, -108f, 150f, 40f);

        /// <summary>One of the three heat crowns, left to right. Anything else is the first.</summary>
        public static ManagedHudRect Crown(int index)
        {
            int i = index < 0 || index >= ManagedHudMath.CrownCount ? 0 : index;
            return new ManagedHudRect(0f, 1f, 40f + 130f * i, -12f, 120f, 90f);
        }

        // Bottom middle: health, vessel charge and the quick slots.
        public static readonly ManagedHudRect HpBar = new ManagedHudRect(0.5f, 0f, 0f, 140f, 520f, 28f);
        public static readonly ManagedHudRect SpBar = new ManagedHudRect(0.5f, 0f, 0f, 106f, 520f, 20f);

        /// <summary>The text laid over the health bar.</summary>
        public static readonly ManagedHudRect HpText = new ManagedHudRect(0.5f, 0.5f, 0f, 0f, 520f, 28f);

        /// <summary>Slots in the quick row.</summary>
        public const int QuickSlotCount = 5;

        /// <summary>One quick slot, left to right. Anything else is the first.</summary>
        public static ManagedHudRect QuickSlot(int index)
        {
            int i = index < 0 || index >= QuickSlotCount ? 0 : index;
            return new ManagedHudRect(0.5f, 0f, (i - 2) * 80f, 24f, 72f, 72f);
        }

        /// <summary>The key number shown in a slot's corner.</summary>
        public static readonly ManagedHudRect QuickSlotKey = new ManagedHudRect(0f, 1f, 6f, -4f, 30f, 24f);

        /// <summary>The amount shown in a slot's corner.</summary>
        public static readonly ManagedHudRect QuickSlotAmount = new ManagedHudRect(1f, 0f, -6f, 4f, 50f, 24f);

        /// <summary>The item icon inside a quick slot.</summary>
        public static readonly ManagedHudRect QuickSlotIcon = new ManagedHudRect(0.5f, 0.5f, 0f, 0f, 56f, 56f);

        /// <summary>The item name inside a quick slot, used when the item has no icon.</summary>
        public static readonly ManagedHudRect QuickSlotName = new ManagedHudRect(0.5f, 0.5f, 0f, 0f, 64f, 40f);

        // Bottom right: the button that opens the wheel.
        public static readonly ManagedHudRect WheelButton = new ManagedHudRect(1f, 0f, -40f, 40f, 170f, 170f);

        /// <summary>The icon of the active wheel entry inside the button.</summary>
        public static readonly ManagedHudRect WheelButtonIcon = new ManagedHudRect(0.5f, 0.5f, 0f, 0f, 88f, 88f);

        /// <summary>The name of the active wheel entry inside the button.</summary>
        public static readonly ManagedHudRect WheelButtonName = new ManagedHudRect(0.5f, 0.5f, 0f, 0f, 150f, 40f);

        /// <summary>The key hint at the bottom of the button.</summary>
        public static readonly ManagedHudRect WheelButtonKey = new ManagedHudRect(0.5f, 0f, 0f, 12f, 40f, 28f);

        // The open wheel, over the middle of the screen. It covers the HUD while it is open.
        public static readonly ManagedHudRect Wheel = new ManagedHudRect(0.5f, 0.5f, 0f, 0f, 560f, 560f);

        /// <summary>Inside this distance from the wheel's middle no section is hit.</summary>
        public const float WheelDeadRadius = 70f;

        /// <summary>Past this distance from the wheel's middle no section is hit.</summary>
        public const float WheelOuterRadius = 280f;

        /// <summary>The distance from the wheel's middle to the middle of a section's icon and name.</summary>
        public const float WheelContentRadius = 175f;

        /// <summary>How far below the icon of a section its name is drawn.</summary>
        public const float WheelNameDrop = 58f;

        public const float WheelIconSize = 88f;
        public const float WheelNameWidth = 190f;
        public const float WheelNameHeight = 32f;

        // Text sizes in reference pixels.
        public const float FontTitle = 28f;
        public const float FontBody = 24f;
        public const float FontSmall = 20f;
    }
}
