// Checks for the plasma ring spec, its color ramps and the plasma renderer. Verification harness only.
// The shader and the look are not checked here: they need Unity.
using System;
using MidManStudio.Gtg.Managed.Plasma;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        private static void PlasmaTests()
        {
            PlasmaSpecTests();
            PlasmaRampTests();
            PlasmaRendererTests();
            PlasmaFallbackTests();
            PlasmaBurstTests();
        }

        private static void PlasmaSpecTests()
        {
            Section("Plasma spec");
            ManagedPlasmaRingSpec spec = new ManagedPlasmaRingSpec();
            Vector4 a = spec.PackA(2f, 0.5f);
            Near(a.x, 2f, "age goes to x");
            Near(a.y, 0.5f, "fade goes to y");
            Near(a.z, 0.55f, "the default ring radius goes to z");
            Near(a.w, 0.11f, "the default band width goes to w");

            Near(spec.PackA(-3f, 1f).x, 0f, "a negative age is 0");
            Near(spec.PackA(0f, 5f).y, 1f, "a fade over 1 is 1");
            Near(spec.PackA(0f, -1f).y, 0f, "a fade under 0 is 0");
            spec.RingRadius = 5f;
            spec.BandWidth = 9f;
            Near(spec.PackA(0f, 1f).z, ManagedPlasmaLimits.MaxRingRadius, "a huge ring radius is clamped");
            Near(spec.PackA(0f, 1f).w, ManagedPlasmaLimits.MaxBand, "a huge band is clamped");
            spec.RingRadius = 0f;
            spec.BandWidth = 0f;
            Near(spec.PackA(0f, 1f).z, ManagedPlasmaLimits.MinRingRadius, "a zero ring radius is clamped up");
            Near(spec.PackA(0f, 1f).w, ManagedPlasmaLimits.MinBand, "a zero band is clamped up");

            spec.RingRadius = float.NaN;
            spec.BandWidth = float.PositiveInfinity;
            spec.Turbulence = float.NaN;
            spec.Streaks = float.NegativeInfinity;
            Vector4 bad = spec.PackA(float.NaN, float.NaN);
            Near(bad.x, 0f, "a NaN age is 0");
            Near(bad.y, 1f, "a NaN fade is fully visible, so one bad value cannot blank a ring");
            Near(bad.z, 0.55f, "a NaN ring radius is the default");
            Near(bad.w, 0.11f, "an infinite band is the default");
            Vector4 badB = spec.PackB();
            Near(badB.z, 0.7f, "a NaN turbulence is the default");
            Near(badB.w, 0.8f, "an infinite streak value is the default");

            ManagedPlasmaRingSpec s = new ManagedPlasmaRingSpec { Seed = 1 };
            Near(s.SeedValue, 0.37f, "seed 1");
            s.Seed = ManagedPlasmaLimits.SeedModulus;
            Near(s.SeedValue, 0f, "the seed folds at the modulus");
            s.Seed = ManagedPlasmaLimits.SeedModulus + 2;
            Near(s.SeedValue, 0.74f, "and keeps counting");
            s.Seed = -1;
            Near(s.SeedValue, (ManagedPlasmaLimits.SeedModulus - 1) * 0.37f, "a negative seed folds to a positive value");
            float top = (ManagedPlasmaLimits.SeedModulus - 1) * 0.37f;
            s.Seed = int.MaxValue;
            Check(s.SeedValue >= 0f && s.SeedValue <= top + 1e-3f, "the largest seed stays in range");
            s.Seed = int.MinValue;
            Check(s.SeedValue >= 0f && s.SeedValue <= top + 1e-3f, "so does the smallest");

            Near(ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Fire), 0.5f / 3f, "Fire is the first row");
            Near(ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Water), 1.5f / 3f, "Water is the second row");
            Near(ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Arcane), 2.5f / 3f, "Arcane is the third row");
            Near(ManagedPlasmaRingSpec.RowFor((ManagedPlasmaPalette)7), 0.5f / 3f, "an unknown palette uses the first row");
            for (int p = 0; p < ManagedPlasmaLimits.PaletteCount; p++)
            {
                Eq((int)Mathf.Floor(ManagedPlasmaRingSpec.RowFor((ManagedPlasmaPalette)p) * ManagedPlasmaLimits.PaletteCount), p,
                    "palette " + p + " points at its own texture row");
            }

            ManagedPlasmaRingSpec one = new ManagedPlasmaRingSpec { Seed = 9, Palette = ManagedPlasmaPalette.Arcane, Turbulence = 0.3f };
            ManagedPlasmaRingSpec two = one.Clone();
            Eq(two.Seed, 9, "a clone keeps the seed");
            Eq(two.Palette, ManagedPlasmaPalette.Arcane, "and the palette");
            Near(two.Turbulence, 0.3f, "and the numbers");
            two.Seed = 10;
            Eq(one.Seed, 9, "and is independent of the original");
            Vector4 pa = one.PackA(1.25f, 0.75f);
            Vector4 pb = one.PackA(1.25f, 0.75f);
            Check(pa.x == pb.x && pa.y == pb.y && pa.z == pb.z && pa.w == pb.w, "the same spec packs the same way every time");
        }

        private static void PlasmaRampTests()
        {
            Section("Plasma ramp");
            for (int p = 0; p < ManagedPlasmaLimits.PaletteCount; p++)
            {
                ManagedPlasmaPalette palette = (ManagedPlasmaPalette)p;
                ManagedPlasmaRamp.Stop[] stops = ManagedPlasmaRamp.StopsFor(palette);
                Near(stops[0].At, 0f, palette + " starts at brightness 0");
                Near(stops[stops.Length - 1].At, 1f, palette + " ends at brightness 1");
                bool ascending = true;
                for (int i = 1; i < stops.Length; i++)
                {
                    ascending &= stops[i].At > stops[i - 1].At;
                }

                Check(ascending, palette + " stops ascend");
                Color dark = ManagedPlasmaRamp.Evaluate(palette, 0f);
                Check(dark.r == 0f && dark.g == 0f && dark.b == 0f, palette + " is black at 0, so an additive ring fades into the scene");
                Color hot = ManagedPlasmaRamp.Evaluate(palette, 1f);
                Check(hot.r >= 0.79f && hot.g >= 0.79f && hot.b >= 0.79f, palette + " is near white at 1");

                float last = -1f;
                bool brighter = true;
                for (int k = 0; k <= 128; k++)
                {
                    Color c = ManagedPlasmaRamp.Evaluate(palette, k / 128f);
                    float sum = c.r + c.g + c.b;
                    brighter &= sum >= last - 1e-5f;
                    last = sum;
                }

                Check(brighter, palette + " never gets darker as brightness rises");

                Color clampLow = ManagedPlasmaRamp.Evaluate(palette, -4f);
                Color clampHigh = ManagedPlasmaRamp.Evaluate(palette, 9f);
                Check(clampLow.r == dark.r && clampLow.b == dark.b, palette + " clamps below 0");
                Check(clampHigh.r == hot.r && clampHigh.b == hot.b, palette + " clamps above 1");
                Color nan = ManagedPlasmaRamp.Evaluate(palette, float.NaN);
                Check(nan.r == dark.r && nan.g == dark.g && nan.b == dark.b, palette + " treats NaN as 0");
            }

            Color fire = ManagedPlasmaRamp.Evaluate(ManagedPlasmaPalette.Fire, 0.45f);
            Check(fire.r > fire.g && fire.g > fire.b, "Fire is red then orange at mid brightness");
            Color water = ManagedPlasmaRamp.Evaluate(ManagedPlasmaPalette.Water, 0.45f);
            Check(water.b > water.g && water.g > water.r, "Water is blue at mid brightness");
            Color arcane = ManagedPlasmaRamp.Evaluate(ManagedPlasmaPalette.Arcane, 0.45f);
            Check(arcane.b > arcane.r && arcane.r > arcane.g, "Arcane is violet at mid brightness");
            Check(ManagedPlasmaRamp.StopsFor((ManagedPlasmaPalette)9) == ManagedPlasmaRamp.StopsFor(ManagedPlasmaPalette.Fire), "an unknown palette uses Fire's ramp");

            Color32[] pixels = ManagedPlasmaRamp.Bake();
            Eq(pixels.Length, ManagedPlasmaRamp.Width * ManagedPlasmaLimits.PaletteCount, "the bake fills one row per palette");
            bool opaque = true;
            for (int i = 0; i < pixels.Length; i++)
            {
                opaque &= pixels[i].a == 255;
            }

            Check(opaque, "every texel is opaque, brightness is the only thing that varies");
            Eq((int)pixels[0].r + pixels[0].g + pixels[0].b, 0, "row 0 starts black");
            Color32 waterEnd = pixels[ManagedPlasmaRamp.Width + ManagedPlasmaRamp.Width - 1];
            Eq((int)waterEnd.r, 230, "row 1 (Water) ends at 0.9 red");
            Eq((int)waterEnd.g, 255, "and full green");
            bool rowsMatch = true;
            for (int row = 0; row < ManagedPlasmaLimits.PaletteCount; row++)
            {
                for (int x = 0; x < ManagedPlasmaRamp.Width; x += 7)
                {
                    Color c = ManagedPlasmaRamp.Evaluate((ManagedPlasmaPalette)row, x / (float)(ManagedPlasmaRamp.Width - 1));
                    Color32 px = pixels[row * ManagedPlasmaRamp.Width + x];
                    rowsMatch &= Math.Abs(px.r - c.r * 255f) <= 1f && Math.Abs(px.g - c.g * 255f) <= 1f && Math.Abs(px.b - c.b * 255f) <= 1f;
                }
            }

            Check(rowsMatch, "each row holds its palette's ramp");
            Color32[] again = ManagedPlasmaRamp.Bake();
            bool same = true;
            for (int i = 0; i < pixels.Length; i++)
            {
                same &= pixels[i].r == again[i].r && pixels[i].g == again[i].g && pixels[i].b == again[i].b;
            }

            Check(same, "baking twice gives the same texels");
        }

        private static void PlasmaRendererTests()
        {
            Section("Plasma renderer, instanced path");
            Shader.Known.Add(ManagedPlasmaRenderer.ShaderName);
            Graphics.Calls.Clear();
            Debug.Warnings.Clear();
            Debug.Errors.Clear();
            int frame = Time.frameCount + 2000;
            int idA = Shader.PropertyToID("_ParamA");
            int idB = Shader.PropertyToID("_ParamB");

            ManagedPlasmaRenderer renderer = new ManagedPlasmaRenderer(5, false, "test");
            Check(renderer.CanDraw, "the shader is found");
            Eq(renderer.PlasmaMaterial.shader.name, ManagedPlasmaRenderer.ShaderName, "the material uses it");
            Eq(renderer.Path, ManagedPlasmaDrawPath.Instanced, "instancing is the default path");
            Check(renderer.PlasmaMaterial.enableInstancing, "the material has instancing on");
            Check(renderer.PlasmaMaterial.Textures.ContainsKey("_RampTex"), "the ramp texture is on the material");
            Texture2D ramp = renderer.PlasmaMaterial.Textures["_RampTex"] as Texture2D;
            Check(ramp != null && ramp.width == ManagedPlasmaRamp.Width && ramp.height == ManagedPlasmaLimits.PaletteCount, "it is Width x PaletteCount");
            Check(ramp != null && ramp.Pixels != null && ramp.Pixels.Length == ManagedPlasmaRamp.Width * ManagedPlasmaLimits.PaletteCount, "it holds the baked texels");
            Check(ramp != null && ramp.Applied >= 1, "and was applied");
            Check(ramp != null && ramp.filterMode == FilterMode.Bilinear && ramp.wrapMode == TextureWrapMode.Clamp, "filtered bilinear and clamped, so rows never blend into each other");
            Near(renderer.PlasmaMaterial.Floats["_DstBlend"], 1f, "additive blending by default");
            Near(renderer.PlasmaMaterial.Floats["_Intensity"], 1f, "intensity starts at 1");

            ManagedPlasmaRingSpec spec = new ManagedPlasmaRingSpec { Seed = 4, Palette = ManagedPlasmaPalette.Arcane, Turbulence = 0.2f, Streaks = 0.4f };
            Time.frameCount = frame++;
            Check(renderer.Draw(spec, Matrix4x4.identity, 1.5f, 0.75f), "a draw goes through");
            Eq(Graphics.Calls.Count, 0, "Draw only queues, nothing is sent before Flush");
            Eq(renderer.DrawsThisFrame, 1, "one ring is queued");
            renderer.Flush();
            Eq(Graphics.Calls.Count, 1, "Flush sends one call");
            Eq(renderer.LastFlushCalls, 1, "and says so");
            DrawCall call = Graphics.Calls[0];
            Eq(call.Kind, "instanced", "it is a DrawMeshInstanced call");
            Eq(call.Count, 1, "with one instance");
            Eq(call.Layer, 5, "the renderer's layer is used");
            Eq(call.Mesh.vertices.Length, 4, "the mesh is a quad");
            Eq(call.Mesh.triangles.Length, 6, "of two triangles");
            Check(call.Mesh.UvChannels[0] != null && call.Mesh.UvChannels[0].Length == 4, "with uv 0 to 1");
            Vector4 expectA = spec.PackA(1.5f, 0.75f);
            Vector4 expectB = spec.PackB();
            Vector4 gotA = call.VectorArrays[idA][0];
            Vector4 gotB = call.VectorArrays[idB][0];
            Check(gotA.x == expectA.x && gotA.y == expectA.y && gotA.z == expectA.z && gotA.w == expectA.w, "_ParamA carries age, fade, radius and band");
            Check(gotB.x == expectB.x && gotB.y == expectB.y && gotB.z == expectB.z && gotB.w == expectB.w, "_ParamB carries seed, palette row, turbulence and streaks");
            Eq(call.VectorArrays[idA].Length, ManagedPlasmaRenderer.InstanceBatchSize, "the scratch array never changes size");
            Mesh quad = call.Mesh;

            renderer.Flush();
            Eq(Graphics.Calls.Count, 1, "a second Flush with nothing queued sends nothing");
            Eq(renderer.LastFlushCalls, 0, "and says so");

            // Rings share one quad, so any number of them is one call, in draw order, each with its own numbers.
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Matrix4x4 m1 = Matrix4x4.TRS(new Vector3(1f, 0f, 0f), Quaternion.identity, Vector3.one);
            Matrix4x4 m2 = Matrix4x4.TRS(new Vector3(2f, 0f, 0f), Quaternion.identity, Vector3.one);
            ManagedPlasmaRingSpec other = new ManagedPlasmaRingSpec { Seed = 5, Palette = ManagedPlasmaPalette.Fire };
            renderer.Draw(spec, m1, 0.25f, 1f);
            renderer.Draw(other, m2, 0.75f, 0.5f);
            renderer.Flush();
            Eq(Graphics.Calls.Count, 1, "two rings with different specs are still one call");
            Eq(Graphics.Calls[0].Count, 2, "with two instances");
            Check(Graphics.Calls[0].Snapshot[0] == m1 && Graphics.Calls[0].Snapshot[1] == m2, "the matrices keep their draw order");
            Near(Graphics.Calls[0].VectorArrays[idA][0].x, 0.25f, "each instance has its own age (first)");
            Near(Graphics.Calls[0].VectorArrays[idA][1].x, 0.75f, "each instance has its own age (second)");
            Near(Graphics.Calls[0].VectorArrays[idA][1].y, 0.5f, "and its own fade");
            Near(Graphics.Calls[0].VectorArrays[idB][0].y, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Arcane), "and its own palette row (first)");
            Near(Graphics.Calls[0].VectorArrays[idB][1].y, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Fire), "and its own palette row (second)");
            Check(Graphics.Calls[0].Mesh == quad, "the same quad is used every frame");

            // Nothing is queued at fade 0, and a missing spec is refused.
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Check(renderer.Draw(spec, Matrix4x4.identity, 1f, 0f), "fade 0 is not a refusal");
            Check(!renderer.Draw(null, Matrix4x4.identity, 1f, 1f), "a null spec is refused");
            renderer.Flush();
            Eq(Graphics.Calls.Count, 0, "and neither draws anything");

            // Per frame cap.
            Graphics.Calls.Clear();
            renderer.MaxDrawsPerFrame = 3;
            Time.frameCount = frame++;
            bool a = renderer.Draw(spec, Matrix4x4.identity, 1f, 1f);
            bool b = renderer.Draw(spec, Matrix4x4.identity, 1f, 1f);
            bool c = renderer.Draw(spec, Matrix4x4.identity, 1f, 1f);
            bool d = renderer.Draw(spec, Matrix4x4.identity, 1f, 1f);
            Check(a && b && c, "three rings fit");
            Check(!d, "the fourth is refused");
            Eq(renderer.DrawsThisFrame, 3, "three draws this frame");
            renderer.Flush();
            Eq(Graphics.Calls[0].Count, 3, "three instances are sent");
            Time.frameCount = frame++;
            Check(renderer.Draw(spec, Matrix4x4.identity, 1f, 1f), "a new frame starts the count again");
            renderer.Flush();

            // More instances than one call can take.
            Graphics.Calls.Clear();
            renderer.MaxDrawsPerFrame = 2000;
            Time.frameCount = frame++;
            for (int i = 0; i < 1500; i++)
            {
                renderer.Draw(spec, Matrix4x4.identity, i * 0.01f, 1f);
            }

            renderer.Flush();
            Eq(Graphics.Calls.Count, 2, "1500 instances need two calls");
            Eq(Graphics.Calls[0].Count, ManagedPlasmaRenderer.InstanceBatchSize, "the first holds the batch size");
            Eq(Graphics.Calls[1].Count, 1500 - ManagedPlasmaRenderer.InstanceBatchSize, "the second holds the rest");
            Near(Graphics.Calls[0].VectorArrays[idA][0].x, 0f, "the first batch starts at the first age");
            Near(Graphics.Calls[1].VectorArrays[idA][0].x, ManagedPlasmaRenderer.InstanceBatchSize * 0.01f, "the second batch continues where the first ended");
            Eq(Graphics.Calls[1].VectorArrays[idA].Length, ManagedPlasmaRenderer.InstanceBatchSize, "and its array is the same size");
            renderer.MaxDrawsPerFrame = 16;

            // Draws that were never flushed are dropped when the next frame starts.
            Graphics.Calls.Clear();
            Debug.Warnings.Clear();
            Time.frameCount = frame++;
            renderer.Draw(spec, m1, 1f, 1f);
            Time.frameCount = frame++;
            renderer.Draw(spec, m2, 1f, 1f);
            renderer.Flush();
            Eq(Graphics.Calls.Count, 1, "a stale queue is dropped");
            Eq(Graphics.Calls[0].Count, 1, "only the current frame's ring is sent");
            Check(Graphics.Calls[0].Snapshot[0] == m2, "and it is the current one");
            Eq(Debug.Warnings.Count, 1, "the missing Flush is warned about once");

            // Blend and intensity.
            renderer.Additive = false;
            Near(renderer.PlasmaMaterial.Floats["_DstBlend"], 10f, "alpha blending is OneMinusSrcAlpha");
            Check(!renderer.Additive, "and the property says so");
            renderer.Additive = true;
            Near(renderer.PlasmaMaterial.Floats["_DstBlend"], 1f, "additive is One");
            renderer.Intensity = 9f;
            Near(renderer.Intensity, 4f, "intensity is capped at 4");
            Near(renderer.PlasmaMaterial.Floats["_Intensity"], 4f, "on the material too");
            renderer.Intensity = -1f;
            Near(renderer.Intensity, 0f, "and floored at 0");

            renderer.Dispose();
            Check(!renderer.CanDraw, "a disposed renderer cannot draw");
            Check(!renderer.Draw(spec, Matrix4x4.identity, 1f, 1f), "so Draw refuses");
            Graphics.Calls.Clear();
            renderer.Flush();
            Eq(Graphics.Calls.Count, 0, "and Flush after it sends nothing");
            Eq(Debug.Errors.Count, 0, "no errors were logged on the way");
        }

        private static void PlasmaFallbackTests()
        {
            Section("Plasma renderer, DrawMesh fallback");
            Shader.Known.Add(ManagedPlasmaRenderer.ShaderName);
            Graphics.Calls.Clear();
            Debug.Errors.Clear();
            int frame = Time.frameCount + 3000;
            int idA = Shader.PropertyToID("_ParamA");
            int idB = Shader.PropertyToID("_ParamB");
            Matrix4x4 m1 = Matrix4x4.TRS(new Vector3(1f, 0f, 0f), Quaternion.identity, Vector3.one);
            Matrix4x4 m2 = Matrix4x4.TRS(new Vector3(2f, 0f, 0f), Quaternion.identity, Vector3.one);
            ManagedPlasmaRingSpec first = new ManagedPlasmaRingSpec { Seed = 2, Palette = ManagedPlasmaPalette.Water };
            ManagedPlasmaRingSpec second = new ManagedPlasmaRingSpec { Seed = 3, Palette = ManagedPlasmaPalette.Fire };

            // DrawMesh when asked: one call per ring, with the numbers in a property block.
            ManagedPlasmaRenderer mesh = new ManagedPlasmaRenderer(2, true, "drawmesh");
            Eq(mesh.Path, ManagedPlasmaDrawPath.DrawMesh, "forcing DrawMesh selects it");
            Check(mesh.CanDraw, "the shader is still used");
            Check(!mesh.PlasmaMaterial.enableInstancing, "the material does not ask for instancing");
            Time.frameCount = frame++;
            mesh.Draw(first, m1, 0.4f, 0.9f);
            mesh.Draw(second, m2, 0.6f, 0.7f);
            mesh.Flush();
            Eq(Graphics.Calls.Count, 2, "two rings are two DrawMesh calls");
            Eq(mesh.LastFlushCalls, 2, "and the renderer says so");
            Eq(Graphics.Calls[0].Kind, "mesh", "they are DrawMesh calls");
            Eq(Graphics.Calls[0].Layer, 2, "on the renderer's layer");
            Near(Graphics.Calls[0].Vectors[idA].x, 0.4f, "the first age is in its property block");
            Near(Graphics.Calls[1].Vectors[idA].x, 0.6f, "the second age is in its own");
            Near(Graphics.Calls[1].Vectors[idA].y, 0.7f, "so is the fade");
            Near(Graphics.Calls[0].Vectors[idB].y, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Water), "and the palette row");
            Near(Graphics.Calls[1].Vectors[idB].y, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Fire), "of each ring");
            Check(Graphics.Calls[0].First == m1 && Graphics.Calls[1].First == m2, "the matrices keep their draw order");
            Check(Graphics.Calls[0].Mesh == Graphics.Calls[1].Mesh, "both use the one quad");
            mesh.Dispose();

            // A machine without instancing falls back by itself.
            bool before = SystemInfo.supportsInstancing;
            SystemInfo.supportsInstancing = false;
            Graphics.Calls.Clear();
            ManagedPlasmaRenderer noInstancing = new ManagedPlasmaRenderer(0, false, "no instancing");
            SystemInfo.supportsInstancing = before;
            Eq(noInstancing.Path, ManagedPlasmaDrawPath.DrawMesh, "no hardware instancing selects DrawMesh");
            Time.frameCount = frame++;
            noInstancing.Draw(first, m1, 1f, 1f);
            noInstancing.Flush();
            Eq(Graphics.Calls.Count, 1, "and still draws");
            Eq(Graphics.Calls[0].Kind, "mesh", "with DrawMesh");
            noInstancing.Dispose();

            // A missing shader draws nothing and says why, once.
            Shader.Known.Remove(ManagedPlasmaRenderer.ShaderName);
            Graphics.Calls.Clear();
            Debug.Errors.Clear();
            ManagedPlasmaRenderer missing = new ManagedPlasmaRenderer(0, false, "missing");
            Shader.Known.Add(ManagedPlasmaRenderer.ShaderName);
            Check(!missing.CanDraw, "with no shader nothing can draw");
            Eq(Debug.Errors.Count, 1, "one error says the shader is missing");
            Time.frameCount = frame++;
            Check(!missing.Draw(first, m1, 1f, 1f), "Draw refuses");
            missing.Flush();
            Eq(Graphics.Calls.Count, 0, "and Flush sends nothing");
            missing.Dispose();
        }
    }
}
