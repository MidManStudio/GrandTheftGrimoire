// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedModelReport.cs"
// ============================================================================

using System.Text;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Facing
{
    /// <summary>
    /// Reads a character model that is in the scene and says whether it fits the player: height, where
    /// the soles are, whether it is skinned, whether it has a humanoid avatar. Editor only. It reads the
    /// model and changes nothing.
    /// </summary>
    public static class ManagedModelReport
    {
        // A player is a 2 m capsule, so a character between these heights is believable.
        private const float MinHeight = 1.4f;
        private const float MaxHeight = 2.4f;
        private const float FeetTolerance = 0.1f;

        /// <summary>
        /// A multi line report of <paramref name="root"/> measured against <paramref name="feet"/>, the
        /// world position of the soles of the player. <paramref name="problems"/> counts the FIX lines.
        /// </summary>
        public static string Describe(GameObject root, Vector3 feet, out int problems)
        {
            problems = 0;
            StringBuilder text = new StringBuilder();
            text.Append("Model report for ").Append(root.name).Append('\n');

            Renderer[] drawn = root.GetComponentsInChildren<Renderer>(true);
            if (drawn.Length == 0)
            {
                problems++;
                text.Append("FIX  no renderer under the model, so nothing can be seen.\n");
                return text.ToString();
            }

            Bounds bounds = drawn[0].bounds;
            int skinned = 0;
            int triangles = 0;
            int bones = 0;
            for (int i = 0; i < drawn.Length; i++)
            {
                bounds.Encapsulate(drawn[i].bounds);

                SkinnedMeshRenderer skin = drawn[i] as SkinnedMeshRenderer;
                Mesh mesh = null;
                if (skin != null)
                {
                    skinned++;
                    mesh = skin.sharedMesh;
                    bones = Mathf.Max(bones, skin.bones != null ? skin.bones.Length : 0);
                }
                else
                {
                    MeshFilter filter = drawn[i].GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                }

                triangles += CountTriangles(mesh);
            }

            float height = bounds.size.y;
            float feetOffset = bounds.min.y - feet.y;
            text.Append("     size ").Append(bounds.size.x.ToString("F2")).Append(" x ").Append(height.ToString("F2"))
                .Append(" x ").Append(bounds.size.z.ToString("F2")).Append(" m, ")
                .Append(drawn.Length).Append(" renderers, ").Append(triangles).Append(" triangles\n");

            if (height > 5f)
            {
                problems++;
                text.Append("FIX  height ").Append(height.ToString("F2")).Append(" m. This looks like Blender units, where the character is about 9 tall. "
                    + "Export with Apply Scale 0.2, or set Scale Factor 0.2 on the model importer.\n");
            }
            else if (height < MinHeight || height > MaxHeight)
            {
                problems++;
                text.Append("FIX  height ").Append(height.ToString("F2")).Append(" m. A player is about 1.8 m, so scale the model to match.\n");
            }
            else
            {
                text.Append("OK   height ").Append(height.ToString("F2")).Append(" m\n");
            }

            if (Mathf.Abs(feetOffset) > FeetTolerance)
            {
                problems++;
                text.Append("FIX  the soles are ").Append(feetOffset.ToString("F2")).Append(" m ")
                    .Append(feetOffset > 0f ? "above" : "below")
                    .Append(" the player's feet. The model origin must be at the soles.\n");
            }
            else
            {
                text.Append("OK   soles at the player's feet (").Append(feetOffset.ToString("F2")).Append(" m)\n");
            }

            Vector3 scale = root.transform.lossyScale;
            if (Mathf.Abs(scale.x - 1f) > 0.01f || Mathf.Abs(scale.y - 1f) > 0.01f || Mathf.Abs(scale.z - 1f) > 0.01f)
            {
                text.Append("     scale is ").Append(scale.ToString("F3")).Append(", not 1. Fine if the height is right, but a scale of 1 on the root is easier to live with.\n");
            }

            if (skinned == 0)
            {
                text.Append("     not skinned: it works as a statue that turns, but it will not bend with an animation.\n");
            }
            else
            {
                text.Append("OK   ").Append(skinned).Append(" skinned mesh(es), up to ").Append(bones).Append(" bones\n");
            }

            Animator animator = root.GetComponentInChildren<Animator>(true);
            Avatar avatar = animator != null ? animator.avatar : null;
            if (avatar == null)
            {
                text.Append("     no avatar: set Rig, Animation Type to Humanoid on the model importer to get one.\n");
            }
            else if (!avatar.isValid)
            {
                problems++;
                text.Append("FIX  the avatar is not valid. Open the importer, Rig tab, Configure, and read what it flags.\n");
            }
            else
            {
                text.Append(avatar.isHuman ? "OK   humanoid avatar\n" : "     generic avatar, not humanoid\n");
            }

            return text.ToString();
        }

        /// <summary>What the importer of a model asset is set to. Empty when the asset is not a model.</summary>
        public static string DescribeImporter(Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            ModelImporter importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return string.Empty;
            }

            return "Importer of " + path + ": scale factor " + importer.globalScale.ToString("F3")
                + ", use file scale " + importer.useFileScale + " (file scale " + importer.fileScale.ToString("F3")
                + "), rig " + importer.animationType + ", blend shapes " + importer.importBlendShapes + ".";
        }

        private static int CountTriangles(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) == MeshTopology.Triangles)
                {
                    total += (int)(mesh.GetIndexCount(i) / 3u);
                }
            }

            return total;
        }
    }
}
