// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedQuestText.cs"
// ============================================================================

using System;
using System.Collections.Generic;

namespace MidManStudio.Gtg.Managed.Quests
{
    /// <summary>
    /// The words the missions panel shows for quests. Questly carries only keys such as
    /// <c>quest.collect_wood.title</c>, and this table turns a key into text. It reads a plain text file with
    /// one <c>key=text</c> pair per line. It has no Unity types and no Questly types, so it can be built and
    /// checked anywhere.
    /// </summary>
    public sealed class ManagedQuestText
    {
        private readonly Dictionary<string, string> _text = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>How many keys have text.</summary>
        public int Count { get { return _text.Count; } }

        /// <summary>Goes up by one on every <see cref="Load"/> that took a line and every <see cref="Clear"/> that dropped one, so a reader can tell the words changed.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// Reads <c>key=text</c> lines and adds them to the table. A blank line, a line that starts with # or //,
        /// a line with no = sign, a line with an empty key and a line with empty text are skipped. The line is
        /// split on the first = sign, so the text may hold more. Spaces around the key and the text are dropped.
        /// A key that is already held takes the new text. Returns how many lines were taken.
        /// </summary>
        public int Load(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return 0;
            }

            int taken = 0;
            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimStart('\uFEFF').Trim();
                if (line.Length == 0 || line[0] == '#' || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                int split = line.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, split).Trim();
                string text = line.Substring(split + 1).Trim();
                if (key.Length == 0 || text.Length == 0)
                {
                    continue;
                }

                _text[key] = text;
                taken++;
            }

            if (taken > 0)
            {
                Version++;
            }

            return taken;
        }

        /// <summary>Drops every key.</summary>
        public void Clear()
        {
            if (_text.Count > 0)
            {
                _text.Clear();
                Version++;
            }
        }

        /// <summary>The text of a key. False for a null or empty key and for a key with no text.</summary>
        public bool TryGet(string key, out string text)
        {
            if (string.IsNullOrEmpty(key))
            {
                text = null;
                return false;
            }

            return _text.TryGetValue(key, out text);
        }

        /// <summary>The text of a key, or the key itself when it has none, so a missing line shows what to add. A null key gives an empty string.</summary>
        public string Resolve(string key)
        {
            if (key == null)
            {
                return string.Empty;
            }

            string text;
            return _text.TryGetValue(key, out text) ? text : key;
        }
    }
}
