// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCursorBlock.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Camera
{
    /// <summary>
    /// One holder of the block that keeps the camera rig from taking the cursor back on a click. A
    /// panel that needs the mouse owns one of these and sets it while it is open. The rig does not
    /// capture the cursor while any holder is set, so a panel closing never frees the block that
    /// another open panel still needs. A holder counts once however often it is set.
    /// </summary>
    public sealed class ManagedCursorBlock
    {
        private static int _holders;
        private static int _generation;

        private bool _held;
        private int _heldGeneration;

        /// <summary>True while any holder is set.</summary>
        public static bool Active { get { return _holders > 0; } }

        /// <summary>The number of holders that are set.</summary>
        public static int Holders { get { return _holders; } }

        /// <summary>True while this holder is set.</summary>
        public bool Held { get { return _held && _heldGeneration == _generation; } }

        /// <summary>Sets or clears this holder. Setting a holder that is set, or clearing one that is not, does nothing.</summary>
        public void Set(bool held)
        {
            if (held)
            {
                if (Held)
                {
                    return;
                }

                _held = true;
                _heldGeneration = _generation;
                _holders++;
                return;
            }

            // A holder set before a reset was counted out by that reset, so it must not count again.
            if (_held && _heldGeneration == _generation)
            {
                _holders--;
            }

            _held = false;
        }

        /// <summary>
        /// Clears every holder at once. The rig calls it when play starts, because static state
        /// survives a play session when domain reload is off. A holder set earlier is not counted
        /// out a second time when it is cleared.
        /// </summary>
        public static void Reset()
        {
            _holders = 0;
            _generation++;
        }
    }
}
