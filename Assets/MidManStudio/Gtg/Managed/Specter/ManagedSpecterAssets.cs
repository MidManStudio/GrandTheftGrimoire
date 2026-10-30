// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/specter.md, section "ManagedSpecterAssets.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Plasma;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Specter
{
    /// <summary>
    /// The pieces both motion variants build the same way: the shader, the one-unit quad and the color ramp texture. The ramp is
    /// the plasma ramp (<see cref="ManagedPlasmaRamp"/>), so Fire, Water and Arcane look the same on a ring and on a butterfly.
    /// </summary>
    public static class ManagedSpecterAssets
    {
        public const string ShaderName = "MidManStudio/Gtg/SpecterButterfly";

        /// <summary>The material keyword that makes the shader read life and random from particle vertex streams.</summary>
        public const string StreamsKeyword = "_SPECTER_STREAMS";

        /// <summary>The shader, or null when it is missing or not supported on this machine.</summary>
        public static Shader FindShader()
        {
            Shader shader = Shader.Find(ShaderName);
            return shader != null && shader.isSupported ? shader : null;
        }

        /// <summary>One unit wide quad in the XY plane, facing -Z like a Unity Quad. uv runs 0 to 1.</summary>
        public static Mesh BuildQuad()
        {
            Mesh mesh = new Mesh();
            mesh.name = "GTG Specter Quad";
            mesh.SetVertices(
                new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
                },
                0,
                4);
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0, 6, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.01f));
            return mesh;
        }

        /// <summary>The palette ramp: brightness across, one palette per row. Bilinear and clamped so rows never blend into each other.</summary>
        public static Texture2D BuildRamp()
        {
            Texture2D texture = new Texture2D(ManagedPlasmaRamp.Width, ManagedPlasmaLimits.PaletteCount, TextureFormat.RGBA32, false);
            texture.name = "GTG Specter Ramp";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(ManagedPlasmaRamp.Bake());
            texture.Apply();
            return texture;
        }

        /// <summary>Destroys a Unity object the right way for Play mode or Edit mode. Null is fine.</summary>
        public static void DestroyObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
