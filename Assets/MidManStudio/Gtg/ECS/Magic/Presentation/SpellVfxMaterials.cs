#if GTG_ECS
// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/magic.md, section "SpellVfxMaterials.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Magic
{
    /// <summary>Unlit test materials for the placeholder spell visuals.</summary>
    public static class SpellVfxMaterials
    {
        public static Material CreateUnlit(Color color, bool transparent)
        {
            Shader shader = transparent
                ? Shader.Find("Sprites/Default")
                : Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            return new Material(shader) { color = color };
        }
    }
}
#endif
