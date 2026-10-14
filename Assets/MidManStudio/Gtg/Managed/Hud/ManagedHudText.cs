// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedHudText.cs"
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidManStudio.Gtg.Managed.Hud
{
    /// <summary>Where text sits in its rectangle.</summary>
    public enum ManagedHudAlign
    {
        Left,
        Center,
        Right,
        TopLeft,
    }

    /// <summary>
    /// One piece of HUD text. It is a TextMesh Pro text when the TextMesh Pro essentials are in the
    /// project, and a built-in UI text when they are not, so the HUD reads either way. Setting the
    /// same string again does no work.
    /// </summary>
    public sealed class ManagedHudText
    {
        private static bool _tmpChecked;
        private static bool _tmpReady;
        private static Font _legacyFont;

        private readonly TMP_Text _tmp;
        private readonly Text _legacy;
        private string _text = string.Empty;

        private ManagedHudText(GameObject host, TMP_Text tmp, Text legacy)
        {
            GameObject = host;
            _tmp = tmp;
            _legacy = legacy;
        }

        public GameObject GameObject { get; private set; }

        /// <summary>True for a TextMesh Pro text.</summary>
        public bool IsTextMeshPro { get { return _tmp != null; } }

        public string Text
        {
            get { return _text; }
            set
            {
                string next = value ?? string.Empty;
                if (next == _text)
                {
                    return;
                }

                _text = next;
                if (_tmp != null)
                {
                    _tmp.text = next;
                }
                else
                {
                    _legacy.text = next;
                }
            }
        }

        public Color Color
        {
            get { return _tmp != null ? _tmp.color : _legacy.color; }
            set
            {
                if (_tmp != null)
                {
                    _tmp.color = value;
                }
                else
                {
                    _legacy.color = value;
                }
            }
        }

        // Static state survives a play session when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _tmpChecked = false;
            _tmpReady = false;
            _legacyFont = null;
        }

        /// <summary>
        /// Makes a text under a parent, placed by a layout rectangle. The size is in reference
        /// pixels. With <paramref name="bestFit"/> the text shrinks to fit its rectangle, down to
        /// half its size. A text never takes a click.
        /// </summary>
        public static ManagedHudText Create(Transform parent, string name, ManagedHudRect rect, float fontSize, ManagedHudAlign align, Color color, bool bestFit = false)
        {
            RectTransform rt = ManagedHudBuilder.NewRect(parent, name, rect);
            ManagedHudText text = Attach(rt.gameObject, fontSize, align, color, bestFit);
            return text;
        }

        /// <summary>Makes a text on a GameObject that already has a RectTransform.</summary>
        public static ManagedHudText Attach(GameObject host, float fontSize, ManagedHudAlign align, Color color, bool bestFit)
        {
            if (TmpReady())
            {
                TextMeshProUGUI tmp = host.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = fontSize;
                tmp.color = color;
                tmp.alignment = TmpAlignment(align);
                tmp.raycastTarget = false;
                tmp.enableWordWrapping = align == ManagedHudAlign.TopLeft || bestFit;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.enableAutoSizing = bestFit;
                tmp.fontSizeMin = fontSize * 0.5f;
                tmp.fontSizeMax = fontSize;
                return new ManagedHudText(host, tmp, null);
            }

            Text legacy = host.AddComponent<Text>();
            legacy.font = LegacyFont();
            legacy.fontSize = Mathf.RoundToInt(fontSize);
            legacy.color = color;
            legacy.alignment = LegacyAlignment(align);
            legacy.raycastTarget = false;
            legacy.horizontalOverflow = align == ManagedHudAlign.TopLeft || bestFit ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            legacy.verticalOverflow = VerticalWrapMode.Overflow;
            legacy.resizeTextForBestFit = bestFit;
            legacy.resizeTextMinSize = Mathf.RoundToInt(fontSize * 0.5f);
            legacy.resizeTextMaxSize = Mathf.RoundToInt(fontSize);
            return new ManagedHudText(host, null, legacy);
        }

        // TextMesh Pro draws nothing until its essentials are imported, because the default font
        // asset lives in them. The settings asset is looked for directly, so a missing one does
        // not open the importer window in the middle of play.
        private static bool TmpReady()
        {
            if (_tmpChecked)
            {
                return _tmpReady;
            }

            _tmpChecked = true;
            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            _tmpReady = settings != null && TMP_Settings.defaultFontAsset != null;
            if (!_tmpReady)
            {
                Debug.LogWarning("The HUD found no TextMesh Pro essentials, so it uses the built-in UI text. "
                    + "Window > TextMeshPro > Import TMP Essential Resources, then play again.");
            }

            return _tmpReady;
        }

        // Unity 2022 renamed the built-in font. The name is tried first and an OS font is the
        // fallback, so a different Unity version still gets text.
        private static Font LegacyFont()
        {
            if (_legacyFont == null)
            {
                try
                {
                    _legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                catch (System.ArgumentException)
                {
                    _legacyFont = null;
                }

                if (_legacyFont == null)
                {
                    _legacyFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
            }

            return _legacyFont;
        }

        private static TextAlignmentOptions TmpAlignment(ManagedHudAlign align)
        {
            switch (align)
            {
                case ManagedHudAlign.Left: return TextAlignmentOptions.Left;
                case ManagedHudAlign.Right: return TextAlignmentOptions.Right;
                case ManagedHudAlign.TopLeft: return TextAlignmentOptions.TopLeft;
                default: return TextAlignmentOptions.Center;
            }
        }

        private static TextAnchor LegacyAlignment(ManagedHudAlign align)
        {
            switch (align)
            {
                case ManagedHudAlign.Left: return TextAnchor.MiddleLeft;
                case ManagedHudAlign.Right: return TextAnchor.MiddleRight;
                case ManagedHudAlign.TopLeft: return TextAnchor.UpperLeft;
                default: return TextAnchor.MiddleCenter;
            }
        }
    }
}
