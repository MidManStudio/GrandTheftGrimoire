// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudSprites.cs"
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>
    /// Finds the drawn pieces of the HUD. Each piece is one image in
    /// <c>Assets/Resources/GtgHud/</c>, loaded by name. A piece that is not there comes back as
    /// null and the HUD draws a flat placeholder in its place, so the frame works before any art.
    /// A PNG may be imported as a Sprite or as a plain Texture, both are accepted.
    /// </summary>
    public static class ManagedHudSprites
    {
        /// <summary>The Resources folder the pieces are read from.</summary>
        public const string Folder = "GtgHud/";

        /// <summary>The folder inside <see cref="Folder"/> that holds item and wheel icons.</summary>
        public const string IconFolder = "Icons/";

        public const string FrameMissions = "hud_frame_missions";
        public const string FrameStatus = "hud_frame_status";
        public const string CompassBar = "hud_compass_bar";
        public const string CompassMarker = "hud_compass_marker";
        public const string HpBack = "hud_hp_back";
        public const string HpFill = "hud_hp_fill";
        public const string SpBack = "hud_sp_back";
        public const string SpFill = "hud_sp_fill";
        public const string CrownDim = "hud_crown_dim";
        public const string CrownLit = "hud_crown_lit";
        public const string CoinGold = "hud_coin_gold";
        public const string CoinSilver = "hud_coin_silver";
        public const string Slot = "hud_slot";
        public const string WheelButton = "hud_wheel_button";
        public const string WheelRing = "hud_wheel_ring";
        public const string WheelHighlight = "hud_wheel_highlight";

        private const int DiscSize = 64;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static Sprite _white;
        private static Sprite _disc;

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            _white = null;
            _disc = null;
        }

        /// <summary>
        /// The piece with this name, or null when there is none. A name that is null or empty has
        /// none. The answer is kept, a missing piece included, so a name is looked up once.
        /// </summary>
        public static Sprite Load(string name)
        {
            return Find(Folder + name, name);
        }

        /// <summary>
        /// The icon with this key from the Icons folder, or null. Items use <c>item_</c> followed by
        /// their id, and a wheel entry names its own key.
        /// </summary>
        public static Sprite LoadIcon(string key)
        {
            return Find(Folder + IconFolder + key, key);
        }

        /// <summary>The icon key of an item, <c>item_</c> followed by its id.</summary>
        public static string ItemIconKey(int itemId)
        {
            return "item_" + itemId;
        }

        /// <summary>A plain white square. Placeholders for bars and panels are tinted copies of it.</summary>
        public static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    _white = MakeSprite(1, false);
                }

                return _white;
            }
        }

        /// <summary>A white disc with a soft edge. Placeholders for round pieces are tinted copies of it.</summary>
        public static Sprite Disc
        {
            get
            {
                if (_disc == null)
                {
                    _disc = MakeSprite(DiscSize, true);
                }

                return _disc;
            }
        }

        private static Sprite Find(string path, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            Sprite sprite;
            if (Cache.TryGetValue(path, out sprite))
            {
                return sprite;
            }

            sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                // A PNG left on the default import type is a Texture2D. Wrap it in a sprite.
                Texture2D texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                    sprite.hideFlags = HideFlags.HideAndDontSave;
                }
            }

            Cache[path] = sprite;
            return sprite;
        }

        private static Sprite MakeSprite(int size, bool disc)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte alpha = 255;
                    if (disc)
                    {
                        // Alpha falls from 1 to 0 across the last pixel of the radius.
                        float dx = x + 0.5f - radius;
                        float dy = y + 0.5f - radius;
                        float edge = radius - (float)System.Math.Sqrt(dx * dx + dy * dy);
                        alpha = (byte)(Mathf.Clamp01(edge) * 255f);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
