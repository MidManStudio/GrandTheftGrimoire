// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCheatRegistry.cs"
// ============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using System.Text;

namespace MidManStudio.Gtg.Managed.Cheats
{
    /// <summary>
    /// The list of cheats, the typed-code buffer and the on and off bookkeeping. It holds no Unity
    /// types: it feeds on characters and acts through an <see cref="IManagedCheatHost"/>, so the
    /// panel, the typed codes and a test all drive it the same way.
    /// </summary>
    public sealed class ManagedCheatRegistry
    {
        public const int MinCodeLength = 4;
        public const int MaxCodeLength = 24;

        /// <summary>Seconds of silence after which a half typed code is forgotten.</summary>
        public const float TypingGapSeconds = 2f;

        private readonly List<ManagedCheat> _cheats = new List<ManagedCheat>(16);
        private readonly StringBuilder _buffer = new StringBuilder(MaxCodeLength);
        private float _lastCharTime = float.NegativeInfinity;

        public int Count { get { return _cheats.Count; } }

        public ManagedCheat Get(int index)
        {
            return _cheats[index];
        }

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _cheats.Count; i++)
                {
                    if (_cheats[i].On)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Adds a cheat. A code must be 4 to 24 characters of a to z and 0 to 9, and no code may
        /// appear inside another, because typing the longer one would trigger the shorter on the
        /// way. Returns false and adds nothing for a code that breaks a rule.
        /// </summary>
        public bool Add(ManagedCheat cheat)
        {
            if (cheat == null || !IsValidCode(cheat.Code))
            {
                return false;
            }

            for (int i = 0; i < _cheats.Count; i++)
            {
                string other = _cheats[i].Code;
                if (other.Contains(cheat.Code) || cheat.Code.Contains(other))
                {
                    return false;
                }
            }

            _cheats.Add(cheat);
            return true;
        }

        public ManagedCheat Find(string code)
        {
            for (int i = 0; i < _cheats.Count; i++)
            {
                if (_cheats[i].Code == code)
                {
                    return _cheats[i];
                }
            }

            return null;
        }

        public static bool IsValidCode(string code)
        {
            if (code == null || code.Length < MinCodeLength || code.Length > MaxCodeLength)
            {
                return false;
            }

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Takes one typed character. Letters are lowercased, and anything that is not a letter or a
        /// digit is ignored. When the buffer now ends with a cheat's code the buffer is cleared and
        /// that cheat is returned, otherwise null. <paramref name="now"/> is in seconds, and a gap
        /// of more than <see cref="TypingGapSeconds"/> since the last character starts a new word.
        /// </summary>
        public ManagedCheat Feed(char c, float now)
        {
            if (c >= 'A' && c <= 'Z')
            {
                c = (char)(c + ('a' - 'A'));
            }

            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')))
            {
                return null;
            }

            if (now - _lastCharTime > TypingGapSeconds)
            {
                _buffer.Length = 0;
            }

            _lastCharTime = now;
            _buffer.Append(c);
            if (_buffer.Length > MaxCodeLength)
            {
                _buffer.Remove(0, _buffer.Length - MaxCodeLength);
            }

            string typed = _buffer.ToString();
            for (int i = 0; i < _cheats.Count; i++)
            {
                if (typed.EndsWith(_cheats[i].Code))
                {
                    _buffer.Length = 0;
                    return _cheats[i];
                }
            }

            return null;
        }

        public void ClearBuffer()
        {
            _buffer.Length = 0;
            _lastCharTime = float.NegativeInfinity;
        }

        /// <summary>
        /// Uses a cheat. A toggle that is on is switched off, otherwise the cheat is applied. A
        /// toggle only turns on when its apply succeeded. Returns false when the cheat could not act.
        /// </summary>
        public bool Activate(ManagedCheat cheat, IManagedCheatHost host)
        {
            if (cheat == null || host == null)
            {
                return false;
            }

            if (cheat.IsToggle && cheat.On)
            {
                cheat.Revert(host);
                cheat.On = false;
                host.Log("[GTG Cheat] " + cheat.Name + " (" + cheat.Code + ") off");
                return true;
            }

            if (!cheat.Apply(host))
            {
                host.Log("[GTG Cheat] " + cheat.Name + " (" + cheat.Code + ") could not run");
                return false;
            }

            if (cheat.IsToggle)
            {
                cheat.On = true;
            }

            host.Log("[GTG Cheat] " + cheat.Name + " (" + cheat.Code + (cheat.IsToggle ? ") on" : ") done"));
            return true;
        }

        /// <summary>Switches every toggle that is on back off. Run when play ends.</summary>
        public void RevertAll(IManagedCheatHost host)
        {
            for (int i = 0; i < _cheats.Count; i++)
            {
                ManagedCheat cheat = _cheats[i];
                if (cheat.IsToggle && cheat.On)
                {
                    cheat.Revert(host);
                    cheat.On = false;
                }
            }
        }
    }
}

#endif
