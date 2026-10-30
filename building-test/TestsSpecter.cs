// Checks for the specter butterfly: its spec, the wing flap and fade, the code-moved flock, the renderer, the swarm component,
// the Particle System component (against the hand-written particle stubs) and the preview. Verification harness only.
// The shader's look is not checked here: it needs Unity. A source check does make sure the C# and the shader agree on every name.
using System;
using System.Collections.Generic;
using System.IO;
using MidManStudio.Gtg.Managed.Plasma;
using MidManStudio.Gtg.Managed.Specter;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        private static void SpecterTests()
        {
            SpecterSpecTests();
            SpecterFlapAndFadeTests();
            SpecterAssetsTests();
            SpecterRendererTests();
            SpecterRendererFallbackTests();
            SpecterFlockTests();
            SpecterSwarmTests();
            SpecterParticlesTests();
            SpecterPreviewTests();
            SpecterShaderSourceTests();
        }

        private static readonly int SpecterParamA = Shader.PropertyToID("_ParamA");
        private static readonly int SpecterParamB = Shader.PropertyToID("_ParamB");

        private static void SpecterSpecTests()
        {
            Section("Specter spec");
            ManagedSpecterSpec spec = new ManagedSpecterSpec();
            Vector4 a = spec.PackA(0.5f, 0.75f);
            Near(a.x, 0.5f, "life goes to x");
            Near(a.y, 0.75f, "fade goes to y");
            Near(a.z, 0.37f, "seed 1 goes to z as 0.37");
            Near(a.w, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Arcane), "the Arcane row goes to w");
            Vector4 b = spec.PackB();
            Near(b.x, 0.3f, "default wobble");
            Near(b.y, 1f, "default glow");
            Near(b.z, 1f, "default churn");
            Near(b.w, 1f, "default reach");

            Near(spec.PackA(-2f, 1f).x, 0f, "a negative life is 0");
            Near(spec.PackA(9f, 1f).x, 1f, "a life over 1 is 1");
            Near(spec.PackA(0f, 4f).y, 1f, "a fade over 1 is 1");
            Near(spec.PackA(0f, -1f).y, 0f, "a fade under 0 is 0");
            Vector4 bad = spec.PackA(float.NaN, float.NaN);
            Near(bad.x, 0f, "a NaN life is 0");
            Near(bad.y, 1f, "a NaN fade is fully visible, so one bad value cannot blank a butterfly");
            Near(spec.PackA(float.PositiveInfinity, float.NegativeInfinity).x, 0f, "an infinite life is 0");
            Near(spec.PackA(float.PositiveInfinity, float.NegativeInfinity).y, 1f, "an infinite fade is 1");

            spec.Wobble = 9f; spec.Glow = 99f; spec.Churn = 99f; spec.Reach = 99f;
            Vector4 high = spec.PackB();
            Near(high.x, ManagedSpecterLimits.MaxWobble, "a huge wobble is clamped");
            Near(high.y, ManagedSpecterLimits.MaxGlow, "a huge glow is clamped");
            Near(high.z, ManagedSpecterLimits.MaxChurn, "a huge churn is clamped");
            Near(high.w, ManagedSpecterLimits.MaxReach, "a huge reach is clamped");
            spec.Wobble = -1f; spec.Glow = -1f; spec.Churn = -1f; spec.Reach = -1f;
            Vector4 low = spec.PackB();
            Near(low.x, ManagedSpecterLimits.MinWobble, "a negative wobble is clamped up");
            Near(low.y, ManagedSpecterLimits.MinGlow, "a negative glow is clamped up");
            Near(low.z, ManagedSpecterLimits.MinChurn, "a negative churn is clamped up");
            Near(low.w, ManagedSpecterLimits.MinReach, "a negative reach is clamped up");
            spec.Wobble = float.NaN; spec.Glow = float.PositiveInfinity; spec.Churn = float.NaN; spec.Reach = float.NegativeInfinity;
            Vector4 nan = spec.PackB();
            Near(nan.x, 0.3f, "a NaN wobble is the default");
            Near(nan.y, 1f, "an infinite glow is the default");
            Near(nan.z, 1f, "a NaN churn is the default");
            Near(nan.w, 1f, "an infinite reach is the default");

            ManagedSpecterSpec s = new ManagedSpecterSpec { Seed = ManagedPlasmaLimits.SeedModulus };
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

            for (int p = 0; p < ManagedPlasmaLimits.PaletteCount; p++)
            {
                ManagedSpecterSpec ps = new ManagedSpecterSpec { Palette = (ManagedPlasmaPalette)p };
                Near(ps.PackA(0f, 1f).w, ManagedPlasmaRingSpec.RowFor((ManagedPlasmaPalette)p), "palette " + p + " uses the plasma ramp row, so a spell's ring and butterflies match");
            }

            ManagedSpecterSpec one = new ManagedSpecterSpec { Seed = 9, Palette = ManagedPlasmaPalette.Fire, Wobble = 0.2f, Glow = 2f, Churn = 0.5f, Reach = 1.5f };
            ManagedSpecterSpec two = one.Clone();
            Eq(two.Seed, 9, "a clone keeps the seed");
            Eq(two.Palette, ManagedPlasmaPalette.Fire, "and the palette");
            Check(two.Wobble == 0.2f && two.Glow == 2f && two.Churn == 0.5f && two.Reach == 1.5f, "and the numbers");
            two.Seed = 10;
            two.Palette = ManagedPlasmaPalette.Water;
            Eq(one.Seed, 9, "and is independent of the original (seed)");
            Eq(one.Palette, ManagedPlasmaPalette.Fire, "and of the original (palette)");
            Vector4 pa = one.PackA(0.4f, 0.6f);
            Vector4 pb = one.PackA(0.4f, 0.6f);
            Check(SameV4(pa, pb), "the same spec packs the same way every time");
        }

        private static void SpecterFlapAndFadeTests()
        {
            Section("Specter flap and fade");
            float min = 0.35f;
            Near(ManagedSpecterFlap.WidthScale(0f, min), 1f, "phase 0 is wings open");
            Near(ManagedSpecterFlap.WidthScale(0.5f, min), min, "phase half is wings nearly closed");
            Near(ManagedSpecterFlap.WidthScale(1f, min), 1f, "phase 1 is open again");
            Near(ManagedSpecterFlap.WidthScale(7.5f, min), min, "and the pattern repeats");
            Near(ManagedSpecterFlap.WidthScale(-0.5f, min), min, "a negative phase works too");
            bool inRange = true;
            for (float phase = -3f; phase < 3f; phase += 0.037f)
            {
                float w = ManagedSpecterFlap.WidthScale(phase, min);
                inRange &= w >= min - 1e-5f && w <= 1f + 1e-5f;
            }

            Check(inRange, "the width scale always stays between min and 1");
            Near(ManagedSpecterFlap.WidthScale(float.NaN, min), 1f, "a NaN phase is open");
            Near(ManagedSpecterFlap.WidthScale(float.PositiveInfinity, min), 1f, "an infinite phase is open");
            Near(ManagedSpecterFlap.WidthScale(0.5f, float.NaN), ManagedSpecterFlap.DefaultMin, "a NaN minimum is the default");
            Near(ManagedSpecterFlap.WidthScale(0.5f, 2f), 1f, "a minimum over 1 means no flap");
            Near(ManagedSpecterFlap.WidthScale(0.5f, -1f), 0f, "a minimum under 0 closes completely");

            float[] times;
            float[] values;
            ManagedSpecterFlap.BuildCurveKeys(3f, min, out times, out values);
            Eq(times.Length, 25, "three cycles are 3 x 8 + 1 keys");
            Eq(values.Length, 25, "with a value for each");
            Near(times[0], 0f, "the curve starts at time 0");
            Near(times[times.Length - 1], 1f, "and ends at time 1");
            bool rising = true;
            for (int i = 1; i < times.Length; i++)
            {
                rising &= times[i] > times[i - 1];
            }

            Check(rising, "key times strictly increase, as Unity needs");
            Near(values[0], 1f, "it starts open");
            Near(values[4], min, "the first cycle closes half way through (key 4 of 8)");
            Near(values[8], 1f, "and is open at the end of the cycle");
            Near(values[values.Length - 1], 1f, "and ends open");
            float lowest = 9f;
            for (int i = 0; i < values.Length; i++)
            {
                lowest = Math.Min(lowest, values[i]);
            }

            Near(lowest, min, "the lowest key is the minimum");

            ManagedSpecterFlap.BuildCurveKeys(0f, min, out times, out values);
            Eq(times.Length, 9, "cycles under 1 count as 1");
            ManagedSpecterFlap.BuildCurveKeys(1000f, min, out times, out values);
            Eq(times.Length, 40 * 8 + 1, "cycles over 40 count as 40");
            ManagedSpecterFlap.BuildCurveKeys(float.NaN, min, out times, out values);
            Eq(times.Length, 9, "NaN cycles count as 1");
            ManagedSpecterFlap.BuildCurveKeys(2f, min, 0.25f, out times, out values);
            Near(values[0], min + (1f - min) * 0.5f, "a quarter flap offset starts half way between open and closed");
            ManagedSpecterFlap.BuildCurveKeys(2f, min, float.NaN, out times, out values);
            Near(values[0], 1f, "a NaN offset is none");

            Near(ManagedSpecterFade.At(0f, 0f, 0.2f, 0.12f), 0f, "a new butterfly is invisible, so it fades in");
            Near(ManagedSpecterFade.At(0.1f, 0.03f, 0.2f, 0.12f), 0.5f, "half way through the fade in");
            Near(ManagedSpecterFade.At(0.2f, 0.5f, 0.2f, 0.12f), 1f, "fully visible after the fade in");
            Near(ManagedSpecterFade.At(10f, 0.88f, 0.2f, 0.12f), 1f, "and still fully visible where the fade out starts");
            Near(ManagedSpecterFade.At(10f, 0.94f, 0.2f, 0.12f), 0.5f, "half way through the fade out");
            Near(ManagedSpecterFade.At(10f, 1f, 0.2f, 0.12f), 0f, "gone at the end of the life");
            Near(ManagedSpecterFade.At(0f, 0.5f, 0f, 0.12f), 1f, "a fade in of 0 seconds starts fully visible");
            Near(ManagedSpecterFade.At(float.NaN, float.NaN, 0.2f, 0.12f), 1f, "NaN age and life count as fully visible");
            Near(ManagedSpecterFade.At(1f, 0.99f, 0.2f, float.NaN), 1f, "a NaN fade out span means no fade out");
            bool bounded = true;
            for (float life = -0.2f; life < 1.4f; life += 0.013f)
            {
                float f = ManagedSpecterFade.At(life * 3f, life, 0.2f, 0.12f);
                bounded &= f >= 0f && f <= 1f;
            }

            Check(bounded, "the fade always stays between 0 and 1");
        }

        private static void SpecterAssetsTests()
        {
            Section("Specter assets");
            Eq(ManagedSpecterAssets.ShaderName, "MidManStudio/Gtg/SpecterButterfly", "the shader name");
            Eq(ManagedSpecterAssets.StreamsKeyword, "_SPECTER_STREAMS", "the keyword name");
            Shader.Known.Remove(ManagedSpecterAssets.ShaderName);
            Check(ManagedSpecterAssets.FindShader() == null, "no shader is found when it is not in the project");
            Shader.Known.Add(ManagedSpecterAssets.ShaderName);
            Check(ManagedSpecterAssets.FindShader() != null, "the shader is found when it is");
            Mesh quad = ManagedSpecterAssets.BuildQuad();
            Eq(quad.vertices.Length, 4, "the quad has four corners");
            Eq(quad.triangles.Length, 6, "and two triangles");
            Check(quad.UvChannels[0] != null && quad.UvChannels[0].Length == 4, "with uv 0 to 1");
            Near(quad.vertices[1].x - quad.vertices[0].x, 1f, "it is one unit wide");
            Near(quad.vertices[2].y - quad.vertices[0].y, 1f, "and one unit high");
            Texture2D ramp = ManagedSpecterAssets.BuildRamp();
            Check(ramp.width == ManagedPlasmaRamp.Width && ramp.height == ManagedPlasmaLimits.PaletteCount, "the ramp is the plasma ramp's size");
            Check(ramp.Pixels != null && ramp.Pixels.Length == ManagedPlasmaRamp.Width * ManagedPlasmaLimits.PaletteCount && ramp.Applied >= 1, "holding the baked texels, applied");
            Check(ramp.filterMode == FilterMode.Bilinear && ramp.wrapMode == TextureWrapMode.Clamp, "bilinear and clamped");
            ManagedSpecterAssets.DestroyObject(null);
            ManagedSpecterAssets.DestroyObject(quad);
            Check(quad == null, "DestroyObject removes an object, and null is fine");
        }

        private static void SpecterRendererTests()
        {
            Section("Specter renderer, instanced path");
            Shader.Known.Add(ManagedSpecterAssets.ShaderName);
            Graphics.Calls.Clear();
            Debug.Warnings.Clear();
            Debug.Errors.Clear();
            int frame = Time.frameCount + 7000;

            ManagedSpecterRenderer renderer = new ManagedSpecterRenderer(5, false, "test");
            Check(renderer.CanDraw, "the shader is found");
            Eq(renderer.SpecterMaterial.shader.name, ManagedSpecterAssets.ShaderName, "the material uses it");
            Eq(renderer.Path, ManagedPlasmaDrawPath.Instanced, "instancing is the default path");
            Check(renderer.SpecterMaterial.enableInstancing, "the material has instancing on");
            Check(renderer.SpecterMaterial.Textures.ContainsKey("_RampTex"), "the ramp is on the material");
            Check(!renderer.SpecterMaterial.IsKeywordEnabled(ManagedSpecterAssets.StreamsKeyword), "the stream keyword is off, the numbers come per instance");
            Near(renderer.SpecterMaterial.Floats["_DstBlend"], 1f, "additive blending by default");
            Near(renderer.SpecterMaterial.Floats["_Intensity"], 2f, "intensity starts at 2");

            ManagedSpecterSpec spec = new ManagedSpecterSpec { Seed = 4, Palette = ManagedPlasmaPalette.Water, Wobble = 0.2f, Glow = 1.5f, Churn = 0.4f, Reach = 1.2f };
            Time.frameCount = frame++;
            Check(renderer.Draw(spec, Matrix4x4.identity, 0.4f, 0.75f), "a draw goes through");
            Eq(Graphics.Calls.Count, 0, "Draw only queues, nothing is sent before Flush");
            Eq(renderer.DrawsThisFrame, 1, "one butterfly is queued");
            renderer.Flush();
            Eq(Graphics.Calls.Count, 1, "Flush sends one call");
            Eq(renderer.LastFlushCalls, 1, "and says so");
            DrawCall call = Graphics.Calls[0];
            Eq(call.Kind, "instanced", "it is a DrawMeshInstanced call");
            Eq(call.Count, 1, "with one instance");
            Eq(call.Layer, 5, "on the renderer's layer");
            Eq(call.Mesh.vertices.Length, 4, "the mesh is a quad");
            Check(SameV4(call.VectorArrays[SpecterParamA][0], spec.PackA(0.4f, 0.75f)), "_ParamA carries life, fade, seed and palette row");
            Check(SameV4(call.VectorArrays[SpecterParamB][0], spec.PackB()), "_ParamB carries wobble, glow, churn and reach");
            Eq(call.VectorArrays[SpecterParamA].Length, ManagedSpecterRenderer.InstanceBatchSize, "the scratch array never changes size");
            Mesh sharedQuad = call.Mesh;

            // Any number of butterflies is one call, in draw order, each with its own numbers.
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Matrix4x4 m1 = Matrix4x4.TRS(new Vector3(1f, 0f, 0f), Quaternion.identity, Vector3.one);
            Matrix4x4 m2 = Matrix4x4.TRS(new Vector3(2f, 0f, 0f), Quaternion.identity, Vector3.one);
            ManagedSpecterSpec other = new ManagedSpecterSpec { Seed = 5, Palette = ManagedPlasmaPalette.Fire };
            renderer.Draw(spec, m1, 0.25f, 1f);
            renderer.Draw(other, m2, 0.75f, 0.5f);
            renderer.Flush();
            Eq(Graphics.Calls.Count, 1, "two butterflies with different looks are still one call");
            Eq(Graphics.Calls[0].Count, 2, "with two instances");
            Check(Graphics.Calls[0].Snapshot[0] == m1 && Graphics.Calls[0].Snapshot[1] == m2, "the matrices keep their draw order");
            Near(Graphics.Calls[0].VectorArrays[SpecterParamA][0].x, 0.25f, "each has its own life (first)");
            Near(Graphics.Calls[0].VectorArrays[SpecterParamA][1].x, 0.75f, "each has its own life (second)");
            Near(Graphics.Calls[0].VectorArrays[SpecterParamA][1].y, 0.5f, "and its own fade");
            Near(Graphics.Calls[0].VectorArrays[SpecterParamA][0].w, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Water), "and its own palette row (first)");
            Near(Graphics.Calls[0].VectorArrays[SpecterParamA][1].w, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Fire), "and its own palette row (second)");
            Check(Graphics.Calls[0].Mesh == sharedQuad, "the same quad is used every frame");

            // Nothing is queued at fade 0 or without a spec.
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Check(renderer.Draw(spec, Matrix4x4.identity, 0.5f, 0f), "fade 0 is not a refusal");
            Check(!renderer.Draw(null, Matrix4x4.identity, 0.5f, 1f), "a null spec is refused");
            renderer.Flush();
            Eq(Graphics.Calls.Count, 0, "and neither draws anything");

            // Per frame cap.
            renderer.MaxDrawsPerFrame = 3;
            Time.frameCount = frame++;
            bool a = renderer.Draw(spec, Matrix4x4.identity, 0.1f, 1f);
            bool b = renderer.Draw(spec, Matrix4x4.identity, 0.1f, 1f);
            bool c = renderer.Draw(spec, Matrix4x4.identity, 0.1f, 1f);
            bool d = renderer.Draw(spec, Matrix4x4.identity, 0.1f, 1f);
            Check(a && b && c, "three butterflies fit");
            Check(!d, "the fourth is refused");
            renderer.Flush();
            Eq(Graphics.Calls[0].Count, 3, "three instances are sent");

            // More instances than one call can take.
            Graphics.Calls.Clear();
            renderer.MaxDrawsPerFrame = 2000;
            Time.frameCount = frame++;
            for (int i = 0; i < 1500; i++)
            {
                renderer.Draw(spec, Matrix4x4.identity, i / 2000f, 1f);
            }

            renderer.Flush();
            Eq(Graphics.Calls.Count, 2, "1500 instances need two calls");
            Eq(Graphics.Calls[0].Count, ManagedSpecterRenderer.InstanceBatchSize, "the first holds the batch size");
            Eq(Graphics.Calls[1].Count, 1500 - ManagedSpecterRenderer.InstanceBatchSize, "the second holds the rest");
            Near(Graphics.Calls[1].VectorArrays[SpecterParamA][0].x, ManagedSpecterRenderer.InstanceBatchSize / 2000f, "the second batch continues where the first ended");
            renderer.MaxDrawsPerFrame = 16;

            // Draws that were never flushed are dropped when the next frame starts.
            Graphics.Calls.Clear();
            Debug.Warnings.Clear();
            Time.frameCount = frame++;
            renderer.Draw(spec, m1, 0.1f, 1f);
            Time.frameCount = frame++;
            renderer.Draw(spec, m2, 0.1f, 1f);
            renderer.Flush();
            Eq(Graphics.Calls.Count, 1, "a stale queue is dropped");
            Eq(Graphics.Calls[0].Count, 1, "only the current frame's butterfly is sent");
            Check(Graphics.Calls[0].Snapshot[0] == m2, "and it is the current one");
            Eq(Debug.Warnings.Count, 1, "the missing Flush is warned about once");

            // Blend and intensity.
            renderer.Additive = false;
            Near(renderer.SpecterMaterial.Floats["_DstBlend"], 10f, "alpha blending is OneMinusSrcAlpha");
            renderer.Additive = true;
            Near(renderer.SpecterMaterial.Floats["_DstBlend"], 1f, "additive is One");
            renderer.Intensity = 99f;
            Near(renderer.Intensity, 8f, "intensity is capped at 8");
            Near(renderer.SpecterMaterial.Floats["_Intensity"], 8f, "on the material too");
            renderer.Intensity = -1f;
            Near(renderer.Intensity, 0f, "and floored at 0");
            renderer.Intensity = float.NaN;
            Near(renderer.Intensity, 2f, "NaN intensity is the default");

            renderer.Dispose();
            Check(!renderer.CanDraw, "a disposed renderer cannot draw");
            Check(!renderer.Draw(spec, Matrix4x4.identity, 0.1f, 1f), "so Draw refuses");
            Graphics.Calls.Clear();
            renderer.Flush();
            Eq(Graphics.Calls.Count, 0, "and Flush after it sends nothing");
            Eq(Debug.Errors.Count, 0, "no errors were logged on the way");
        }

        private static void SpecterRendererFallbackTests()
        {
            Section("Specter renderer, DrawMesh fallback and missing shader");
            Shader.Known.Add(ManagedSpecterAssets.ShaderName);
            Graphics.Calls.Clear();
            Debug.Errors.Clear();
            int frame = Time.frameCount + 8000;

            ManagedSpecterRenderer forced = new ManagedSpecterRenderer(6, true, "forced");
            Eq(forced.Path, ManagedPlasmaDrawPath.DrawMesh, "forcing DrawMesh picks the fallback");
            Check(!forced.SpecterMaterial.enableInstancing, "with instancing off on the material");
            ManagedSpecterSpec spec = new ManagedSpecterSpec { Seed = 2, Palette = ManagedPlasmaPalette.Fire };
            Time.frameCount = frame++;
            forced.Draw(spec, Matrix4x4.identity, 0.3f, 0.9f);
            forced.Draw(spec, Matrix4x4.identity, 0.6f, 0.8f);
            forced.Flush();
            Eq(Graphics.Calls.Count, 2, "one DrawMesh call per butterfly");
            Eq(forced.LastFlushCalls, 2, "and the renderer says so");
            Eq(Graphics.Calls[0].Kind, "mesh", "they are DrawMesh calls");
            Eq(Graphics.Calls[0].Layer, 6, "on the layer");
            Check(SameV4(Graphics.Calls[0].Vectors[SpecterParamA], spec.PackA(0.3f, 0.9f)), "the first call's _ParamA is its own");
            Check(SameV4(Graphics.Calls[1].Vectors[SpecterParamA], spec.PackA(0.6f, 0.8f)), "the second call's _ParamA is its own, not the first's");
            forced.Dispose();

            bool saved = SystemInfo.supportsInstancing;
            SystemInfo.supportsInstancing = false;
            ManagedSpecterRenderer noInstancing = new ManagedSpecterRenderer(0, false, "no instancing");
            SystemInfo.supportsInstancing = saved;
            Eq(noInstancing.Path, ManagedPlasmaDrawPath.DrawMesh, "hardware without instancing falls back too");
            noInstancing.Dispose();

            Shader.Known.Remove(ManagedSpecterAssets.ShaderName);
            Graphics.Calls.Clear();
            Debug.Errors.Clear();
            ManagedSpecterRenderer missing = new ManagedSpecterRenderer(0, false, "missing");
            Eq(Debug.Errors.Count, 1, "a missing shader logs one error");
            Check(!missing.CanDraw, "and cannot draw");
            Check(!missing.Draw(spec, Matrix4x4.identity, 0.1f, 1f), "Draw refuses");
            missing.Flush();
            Eq(Graphics.Calls.Count, 0, "and nothing is sent");
            Eq(Debug.Errors.Count, 1, "with no further errors");
            missing.Dispose();
            Shader.Known.Add(ManagedSpecterAssets.ShaderName);
        }

        private static void SpecterFlockTests()
        {
            Section("Specter flock, spawning");
            ManagedSpecterFlock flock = new ManagedSpecterFlock();
            List<ManagedSpecterFlock.Item> items = new List<ManagedSpecterFlock.Item>();
            Eq(flock.Count, 0, "a new flock is empty");
            Eq(flock.Update(0.1f, items), 0, "updating an empty flock returns 0");

            ManagedSpecterSpec template = new ManagedSpecterSpec { Seed = 10, Palette = ManagedPlasmaPalette.Water, Wobble = 0.25f };
            Vector3 center = new Vector3(2f, 1f, -3f);
            Eq(flock.Spawn(null, center, 3, 1f, 3f, 0.7f), 0, "no template adds none");
            Eq(flock.Spawn(template, center, 0, 1f, 3f, 0.7f), 0, "a count of 0 adds none");
            Eq(flock.Spawn(template, center, -4, 1f, 3f, 0.7f), 0, "a negative count adds none");
            Eq(flock.Spawn(template, center, 3, 1f, 0f, 0.7f), 0, "a life of 0 adds none");
            Eq(flock.Spawn(template, center, 3, 1f, float.NaN, 0.7f), 0, "a NaN life adds none");
            Eq(flock.Spawn(template, center, 3, 1f, float.PositiveInfinity, 0.7f), 0, "an infinite life adds none");
            Eq(flock.Spawn(template, center, 3, 1f, 3f, 0f), 0, "a size of 0 adds none");
            Eq(flock.Spawn(template, center, 3, 1f, 3f, float.NaN), 0, "a NaN size adds none");
            Eq(flock.Spawn(template, new Vector3(float.NaN, 0f, 0f), 3, 1f, 3f, 0.7f), 0, "a NaN center adds none");
            Eq(flock.Spawn(template, new Vector3(0f, float.PositiveInfinity, 0f), 3, 1f, 3f, 0.7f), 0, "an infinite center adds none");
            Eq(flock.Count, 0, "so the flock is still empty");

            Eq(flock.Spawn(template, center, 5, 1f, 3f, 0.7f), 5, "five are added");
            Eq(flock.Count, 5, "and counted");
            Eq(template.Seed, 10, "the template is not changed");
            flock.Update(0f, items);
            Eq(items.Count, 5, "an update with no time returns all five");
            HashSet<int> seeds = new HashSet<int>();
            bool inside = true;
            bool fresh = true;
            foreach (ManagedSpecterFlock.Item item in items)
            {
                seeds.Add(item.Spec.Seed);
                inside &= (item.Position - center).magnitude <= 1f + 1e-4f;
                fresh &= item.Life == 0f && item.Fade == 0f && item.Diameter == 0.7f;
                Eq(item.Spec.Palette, ManagedPlasmaPalette.Water, "each keeps the template's palette");
                Near(item.Spec.Wobble, 0.25f, "and its numbers");
            }

            Eq(seeds.Count, 5, "every butterfly has its own seed");
            Check(seeds.Contains(10) && seeds.Contains(14), "counting up from the template's seed");
            Check(inside, "all start inside the spawn sphere");
            Check(fresh, "all start at life 0, invisible (they fade in), at the asked size");

            // Past the limit the oldest are replaced.
            ManagedSpecterFlock small = new ManagedSpecterFlock { MaxButterflies = 3 };
            Eq(small.Spawn(template, center, 5, 0.5f, 3f, 0.7f), 3, "a count over the limit adds only as many as fit");
            Eq(small.Count, 3, "and the flock holds the limit");
            Eq(small.Spawn(template, center, 2, 0.5f, 3f, 0.7f), 2, "two more are added");
            Eq(small.Count, 3, "still the limit");
            small.Update(0f, items);
            HashSet<int> kept = new HashSet<int>();
            foreach (ManagedSpecterFlock.Item item in items)
            {
                kept.Add(item.Spec.Seed);
            }

            Check(kept.Contains(10 + 3) && kept.Contains(10 + 4), "the newest two are there");
            Check(!kept.Contains(10) && !kept.Contains(11), "the oldest were replaced");
            ManagedSpecterFlock tooMany = new ManagedSpecterFlock { MaxButterflies = 9999 };
            Eq(tooMany.Spawn(template, center, 1000, 0.5f, 3f, 0.7f), ManagedSpecterFlock.HardMaxButterflies, "the hard maximum caps the limit");
            ManagedSpecterFlock tooFew = new ManagedSpecterFlock { MaxButterflies = -5 };
            Eq(tooFew.Spawn(template, center, 4, 0.5f, 3f, 0.7f), 1, "a limit under 1 still allows one");

            flock.Clear();
            Eq(flock.Count, 0, "Clear drops every butterfly");
            ManagedSpecterFlock odd = new ManagedSpecterFlock();
            Eq(odd.Spawn(template, center, 2, float.NaN, 3f, 0.7f), 2, "a NaN radius is treated as 0");
            odd.Update(0f, items);
            Check(items[0].Position == center && items[1].Position == center, "so they start at the center");
            odd.Clear();
            Eq(odd.Spawn(template, center, 2, -3f, 3f, 0.7f), 2, "a negative radius is treated as 0");

            Section("Specter flock, motion");
            ManagedSpecterFlock a = new ManagedSpecterFlock();
            ManagedSpecterFlock b = new ManagedSpecterFlock();
            a.Reseed(77);
            b.Reseed(77);
            a.Spawn(template, center, 6, 1f, 2f, 0.7f);
            b.Spawn(template, center, 6, 1f, 2f, 0.7f);
            List<ManagedSpecterFlock.Item> itemsA = new List<ManagedSpecterFlock.Item>();
            List<ManagedSpecterFlock.Item> itemsB = new List<ManagedSpecterFlock.Item>();
            bool same = true;
            for (int f = 0; f < 60; f++)
            {
                a.Update(0.016f, itemsA);
                b.Update(0.016f, itemsB);
                same &= itemsA.Count == itemsB.Count;
                for (int i = 0; same && i < itemsA.Count; i++)
                {
                    same &= itemsA[i].Position == itemsB[i].Position && itemsA[i].Life == itemsB[i].Life && itemsA[i].WidthScale == itemsB[i].WidthScale;
                }
            }

            Check(same, "the same reseed and the same calls give the same flock, frame by frame");
            ManagedSpecterFlock c = new ManagedSpecterFlock();
            c.Reseed(78);
            c.Spawn(template, center, 6, 1f, 2f, 0.7f);
            List<ManagedSpecterFlock.Item> itemsC = new List<ManagedSpecterFlock.Item>();
            c.Update(0f, itemsC);
            ManagedSpecterFlock a2 = new ManagedSpecterFlock();
            a2.Reseed(77);
            a2.Spawn(template, center, 6, 1f, 2f, 0.7f);
            a2.Update(0f, itemsA);
            Check(itemsA[0].Position != itemsC[0].Position, "a different reseed gives a different flock");

            // One butterfly through a whole life, frame by frame.
            ManagedSpecterFlock run = new ManagedSpecterFlock();
            run.Reseed(5);
            run.Spawn(template, Vector3.zero, 1, 0f, 1f, 0.7f);
            float lastLife = -1f;
            bool lifeRises = true;
            bool fadeOk = true;
            bool widthOk = true;
            bool speedOk = true;
            bool finite = true;
            bool risenFade = false;
            Vector3 lastPos = Vector3.zero;
            int frames = 0;
            int lastCount = 1;
            while (run.Count > 0 && frames < 500)
            {
                run.Update(0.016f, items);
                frames++;
                if (items.Count == 0)
                {
                    break;
                }

                ManagedSpecterFlock.Item it = items[0];
                lifeRises &= it.Life > lastLife;
                lastLife = it.Life;
                fadeOk &= it.Fade >= 0f && it.Fade <= 1f && it.Life < 1f;
                widthOk &= it.WidthScale >= run.FlapMin - 1e-5f && it.WidthScale <= 1f + 1e-5f;
                speedOk &= (it.Position - lastPos).magnitude <= run.MaxSpeed * 0.016f + 1e-4f;
                finite &= !float.IsNaN(it.Position.x + it.Position.y + it.Position.z);
                risenFade |= it.Fade > 0.99f;
                lastPos = it.Position;
                lastCount = items.Count;
            }

            Check(lifeRises, "life only rises");
            Check(fadeOk, "fade stays between 0 and 1 and a butterfly past its life is never returned");
            Check(widthOk, "the flap stays between its minimum and 1");
            Check(speedOk, "no butterfly moves faster than the speed limit");
            Check(finite, "positions stay finite");
            Check(risenFade, "it becomes fully visible");
            Eq(run.Count, 0, "the butterfly is removed when its life is up");
            Check(frames >= 61 && frames <= 65, "after about one second of 16 ms frames, took " + frames);
            Eq(lastCount, 1, "and was returned right up to its last frame");

            // Odd frame times.
            ManagedSpecterFlock odd2 = new ManagedSpecterFlock();
            odd2.Reseed(3);
            odd2.Spawn(template, Vector3.zero, 3, 0.2f, 5f, 0.7f);
            odd2.Update(0f, items);
            Vector3 before = items[0].Position;
            odd2.Update(float.NaN, items);
            Check(items.Count == 3 && items[0].Position == before && items[0].Life == 0f, "a NaN frame time counts as 0");
            odd2.Update(-1f, items);
            Check(items.Count == 3 && items[0].Position == before && items[0].Life == 0f, "and so does a negative one");
            odd2.Update(float.PositiveInfinity, items);
            Eq(items.Count, 0, "an infinite frame time ends every life in one call");
            Eq(odd2.Count, 0, "and removes them");

            ManagedSpecterFlock longFrame = new ManagedSpecterFlock();
            longFrame.Reseed(4);
            longFrame.Spawn(template, Vector3.zero, 1, 0f, 20f, 0.7f);
            longFrame.Update(2f, items);
            Check(items.Count == 1 && items[0].Position.magnitude <= longFrame.MaxSpeed * longFrame.MaxStep + 1e-3f,
                "a long frame moves a butterfly by one capped step, not by the whole frame");
            Near(items[0].Life, 0.1f, "though its life still advances by the whole frame");

            // Lift with nothing else pushing: it only ever goes up.
            ManagedSpecterFlock rise = new ManagedSpecterFlock { Wander = 0f, Lift = 1f, Drag = 0f, MaxSpeed = 5f };
            rise.Reseed(9);
            rise.Spawn(template, Vector3.zero, 4, 0f, 10f, 0.7f);
            for (int f = 0; f < 50; f++)
            {
                rise.Update(0.02f, items);
            }

            bool allUp = items.Count == 4;
            foreach (ManagedSpecterFlock.Item item in items)
            {
                allUp &= item.Position.y > 0.4f;
            }

            Check(allUp, "with no wander and a lift of 1, every butterfly has risen after a second");
        }

        private static bool SameV4(Vector4 a, Vector4 b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
        }

        private static GameObject SpecterCamera()
        {
            GameObject camObject = new GameObject("specter test camera", typeof(UnityEngine.Camera));
            camObject.transform.position = new Vector3(0f, 1.5f, -6f);
            UnityEngine.Camera.main = camObject.GetComponent<UnityEngine.Camera>();
            return camObject;
        }

        private static void SpecterSwarmTests()
        {
            Section("Specter swarm");
            Shader.Known.Add(ManagedSpecterAssets.ShaderName);
            Graphics.Calls.Clear();
            Debug.Errors.Clear();
            Debug.Warnings.Clear();
            int frame = Time.frameCount + 9000;
            float savedDelta = Time.deltaTime;
            UnityEngine.Camera savedCamera = UnityEngine.Camera.main;
            Time.deltaTime = 0.016f;
            SpecterCamera();

            GameObject go = new GameObject("swarm", typeof(ManagedSpecterSwarm));
            go.layer = 9;
            go.transform.position = new Vector3(3f, 1f, 4f);
            ManagedSpecterSwarm swarm = go.GetComponent<ManagedSpecterSwarm>();

            Time.frameCount = frame++;
            Call(swarm, "LateUpdate");
            Eq(Graphics.Calls.Count, 0, "with no butterflies nothing is drawn");
            Check(swarm.DrawPath == null, "and no renderer is built yet");
            Check(swarm.Describe().Contains("Not drawing yet"), "and Describe says so");

            Eq(swarm.Burst(0), 0, "a burst of 0 adds none");
            Eq(swarm.Burst(-3), 0, "and so does a negative one");
            Eq(swarm.Burst(5), 5, "a burst of 5 adds 5");
            Eq(swarm.AliveCount, 5, "five are alive");
            Eq(swarm.SpawnedCount, 5, "and five are counted");
            Time.frameCount = frame++;
            Call(swarm, "LateUpdate");
            Eq(Graphics.Calls.Count, 1, "they draw in one call");
            Eq(Graphics.Calls[0].Kind, "instanced", "through DrawMeshInstanced");
            Eq(Graphics.Calls[0].Count, 5, "with five instances");
            Eq(Graphics.Calls[0].Layer, 9, "on the swarm's layer");
            Eq(swarm.DrawPath, (ManagedPlasmaDrawPath?)ManagedPlasmaDrawPath.Instanced, "and the swarm reports the path");
            Check(swarm.Describe().Contains("5 alive") && swarm.Describe().Contains("Instanced"), "Describe says how many and which path");

            Vector4[] paramsA = Graphics.Calls[0].VectorArrays[SpecterParamA];
            Matrix4x4[] matrices = Graphics.Calls[0].Snapshot;
            bool nearSwarm = true;
            bool facing = true;
            bool sizes = true;
            bool lifeOk = true;
            HashSet<float> seedValues = new HashSet<float>();
            for (int i = 0; i < 5; i++)
            {
                Vector3 at = new Vector3(matrices[i].m03, matrices[i].m13, matrices[i].m23);
                nearSwarm &= (at - go.transform.position).magnitude < 0.5f + 0.2f;
                Vector3 zAxis = new Vector3(matrices[i].m02, matrices[i].m12, matrices[i].m22).normalized;
                Vector3 away = (at - UnityEngine.Camera.main.transform.position).normalized;
                facing &= Vector3.Dot(zAxis, away) > 0.999f;
                float height = new Vector3(matrices[i].m01, matrices[i].m11, matrices[i].m21).magnitude;
                float width = new Vector3(matrices[i].m00, matrices[i].m10, matrices[i].m20).magnitude;
                sizes &= Math.Abs(height - 0.7f) < 1e-3f && width <= 0.7f + 1e-3f && width >= 0.7f * ManagedSpecterFlap.DefaultMin - 1e-3f;
                lifeOk &= paramsA[i].x > 0f && paramsA[i].x < 0.05f;
                seedValues.Add(paramsA[i].z);
            }

            Check(nearSwarm, "the butterflies are around the swarm (default radius 0.5 plus one frame of drift)");
            Check(facing, "each quad's Z axis points away from the camera");
            Check(sizes, "each is 0.7 high and no wider than that, narrowed by the flap");
            Check(lifeOk, "each is one frame into its life");
            Eq(seedValues.Count, 5, "each has its own seed in _ParamA");
            Near(paramsA[0].w, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Arcane), "the default palette is Arcane");

            // Palette, burst at a point, limit.
            Graphics.Calls.Clear();
            swarm.Palette = ManagedPlasmaPalette.Fire;
            swarm.CyclePalette();
            Eq(swarm.Palette, ManagedPlasmaPalette.Water, "Fire cycles to Water");
            swarm.CyclePalette();
            swarm.CyclePalette();
            Eq(swarm.Palette, ManagedPlasmaPalette.Fire, "and Arcane cycles back to Fire");
            Eq(swarm.Burst(new Vector3(-8f, 0f, 0f), 2), 2, "a burst at a point adds those");
            Time.frameCount = frame++;
            Call(swarm, "LateUpdate");
            Eq(Graphics.Calls[0].Count, 7, "seven are drawn now");
            Matrix4x4[] later = Graphics.Calls[0].Snapshot;
            Check(Math.Abs(later[5].m03 + 8f) < 0.7f && Math.Abs(later[6].m03 + 8f) < 0.7f, "the two new ones are around the point, after the older five");
            Near(Graphics.Calls[0].VectorArrays[SpecterParamA][5].w, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Fire), "in the new palette");
            Near(Graphics.Calls[0].VectorArrays[SpecterParamA][0].w, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Arcane), "while the older five keep theirs");

            SetField(swarm, "_maxButterflies", 4);
            Eq(swarm.Burst(10), 4, "a burst over the limit adds only what fits");
            Eq(swarm.AliveCount, 4, "the oldest were replaced");

            // The fallback path.
            SetField(swarm, "_forceDrawMesh", true);
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Call(swarm, "LateUpdate");
            Eq(swarm.DrawPath, (ManagedPlasmaDrawPath?)ManagedPlasmaDrawPath.DrawMesh, "ticking Force Draw Mesh switches to the fallback");
            Eq(Graphics.Calls.Count, 4, "one DrawMesh per butterfly");
            Eq(Graphics.Calls[0].Kind, "mesh", "as DrawMesh calls");
            SetField(swarm, "_forceDrawMesh", false);
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Call(swarm, "LateUpdate");
            Eq(swarm.DrawPath, (ManagedPlasmaDrawPath?)ManagedPlasmaDrawPath.Instanced, "and unticking it switches back");
            Eq(Graphics.Calls.Count, 1, "to one call");

            // Blend and intensity reach the material.
            swarm.Additive = false;
            SetField(swarm, "_intensity", 5f);
            Time.frameCount = frame++;
            Call(swarm, "LateUpdate");
            Material mat = Graphics.Calls[Graphics.Calls.Count - 1].Material;
            Near(mat.Floats["_DstBlend"], 10f, "alpha blend reaches the material");
            Near(mat.Floats["_Intensity"], 5f, "and so does intensity");
            swarm.Additive = true;

            // Butterflies live out their time and are removed.
            Time.deltaTime = 0.5f;
            for (int i = 0; i < 12; i++)
            {
                Time.frameCount = frame++;
                Call(swarm, "LateUpdate");
            }

            Eq(swarm.AliveCount, 0, "after the lifetime they are all gone");
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Call(swarm, "LateUpdate");
            Eq(Graphics.Calls.Count, 0, "and nothing is drawn");

            // Auto rate: 10 a second at 0.25 s frames is 2, 3, 2, 3.
            SetField(swarm, "_autoPerSecond", 10f);
            SetField(swarm, "_maxButterflies", 32);
            Time.deltaTime = 0.25f;
            int before = swarm.SpawnedCount;
            for (int i = 0; i < 4; i++)
            {
                Time.frameCount = frame++;
                Call(swarm, "LateUpdate");
            }

            Eq(swarm.SpawnedCount - before, 10, "the auto rate releases 10 in 4 frames, carrying the remainder");
            SetField(swarm, "_autoPerSecond", 0f);

            // Disable tears down.
            swarm.Burst(3);
            Time.frameCount = frame++;
            Call(swarm, "LateUpdate");
            Call(swarm, "OnDisable");
            Eq(swarm.AliveCount, 0, "disabling drops every butterfly");
            Check(swarm.DrawPath == null, "and the renderer");
            Eq(Debug.Errors.Count, 0, "no errors so far");

            // No shader: one error, nothing drawn, no crash.
            Shader.Known.Remove(ManagedSpecterAssets.ShaderName);
            Graphics.Calls.Clear();
            swarm.Burst(2);
            for (int i = 0; i < 3; i++)
            {
                Time.frameCount = frame++;
                Call(swarm, "LateUpdate");
            }

            Eq(Graphics.Calls.Count, 0, "without the shader nothing is drawn");
            Eq(Debug.Errors.Count, 1, "and one error is logged, not one per frame");
            Check(swarm.Describe().Contains("shader is missing"), "Describe says the shader is missing");
            Call(swarm, "OnDisable");
            Shader.Known.Add(ManagedSpecterAssets.ShaderName);

            // Facing a camera straight above or below must not give a broken rotation.
            Quaternion up = ManagedSpecterSwarm.FacingRotation(Vector3.up);
            Quaternion down = ManagedSpecterSwarm.FacingRotation(Vector3.down);
            Vector3 zUp = up * Vector3.forward;
            Vector3 zDown = down * Vector3.forward;
            Check(zUp.y > 0.999f && zDown.y < -0.999f, "looking straight up or down points the Z axis along it");
            Check(!float.IsNaN(up.x + up.y + up.z + up.w) && !float.IsNaN(down.x + down.y + down.z + down.w), "with no NaN");

            Time.deltaTime = savedDelta;
            UnityEngine.Camera.main = savedCamera;
        }

        private static void SpecterParticlesTests()
        {
            Section("Specter particles");
            Shader.Known.Add(ManagedSpecterAssets.ShaderName);
            Debug.Errors.Clear();
            Debug.Warnings.Clear();

            GameObject go = new GameObject("particles", typeof(ManagedSpecterParticles));
            go.layer = 11;
            go.transform.position = new Vector3(1f, 2f, 3f);
            ManagedSpecterParticles p = go.GetComponent<ManagedSpecterParticles>();
            Check(p.Particles == null, "nothing is built before the component enables");
            Check(p.Describe().Contains("not built yet"), "and Describe says so");
            Eq(p.AliveCount, 0, "with nothing alive");

            Call(p, "OnEnable");
            Check(p.Particles != null, "enabling builds the Particle System");
            Transform child = go.transform.Find(ManagedSpecterParticles.ChildName);
            Check(child != null && child.parent == go.transform, "on a child with the expected name");
            Eq(child.gameObject.layer, 11, "on the component's layer");
            ParticleSystemRenderer renderer = child.gameObject.GetComponent<ParticleSystemRenderer>();
            Check(renderer != null, "the child has a particle renderer");
            ParticleSystem ps = p.Particles;
            Check(ps.isPlaying, "the system is playing, so an Emit is simulated");

            List<ParticleSystemVertexStream> streams = new List<ParticleSystemVertexStream>();
            renderer.GetActiveVertexStreams(streams);
            Eq(streams.Count, 5, "five vertex streams are on");
            Check(streams.Count == 5
                    && streams[0] == ParticleSystemVertexStream.Position
                    && streams[1] == ParticleSystemVertexStream.Color
                    && streams[2] == ParticleSystemVertexStream.UV
                    && streams[3] == ParticleSystemVertexStream.AgePercent
                    && streams[4] == ParticleSystemVertexStream.StableRandomX,
                "in the order the shader reads: Position, Color, UV, AgePercent, StableRandomX");
            Eq(renderer.SetStreamsCalls, 1, "set once");
            Eq(renderer.renderMode, ParticleSystemRenderMode.Billboard, "billboards");
            Eq(renderer.shadowCastingMode, UnityEngine.Rendering.ShadowCastingMode.Off, "that cast no shadows");
            Check(!renderer.receiveShadows, "and receive none");

            Material mat = renderer.sharedMaterial;
            Check(mat != null && p.SpecterMaterial == mat, "the renderer uses the component's material");
            Eq(mat.shader.name, ManagedSpecterAssets.ShaderName, "which uses the specter shader");
            Check(mat.IsKeywordEnabled(ManagedSpecterAssets.StreamsKeyword), "with the stream keyword on");
            Near(mat.Floats["_UseStreams"], 1f, "and its toggle property set, so the Inspector agrees");
            Check(mat.Textures.ContainsKey("_RampTex"), "and the ramp");
            Near(mat.Floats["_DstBlend"], 1f, "additive by default");
            Near(mat.Floats["_Intensity"], 2f, "intensity 2");
            Near(mat.Floats["_PaletteRow"], ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Arcane), "the Arcane row");
            Near(mat.Floats["_Wobble"], 0.3f, "default wobble");
            Near(mat.Floats["_Glow"], 1f, "default glow");
            Near(mat.Floats["_Churn"], 1f, "default churn");
            Near(mat.Floats["_Reach"], 1f, "default reach");

            ParticleSystem.MainModule main = ps.main;
            Check(main.loop, "the system loops, so it never stops itself");
            Check(!main.playOnAwake, "and does not play on awake");
            Eq(main.simulationSpace, ParticleSystemSimulationSpace.World, "in world space");
            Eq(main.maxParticles, 32, "32 particles at most");
            Eq(main.startLifetime.mode, ParticleSystemCurveMode.TwoConstants, "lifetime is a range");
            Near(main.startLifetime.constantMin, 2.25f, "from 75 percent of 3 seconds");
            Near(main.startLifetime.constantMax, 3.75f, "to 125 percent");
            Near(main.startSize.constantMin, 0.56f, "size from 80 percent of 0.7");
            Near(main.startSize.constantMax, 0.84f, "to 120 percent");
            Near(main.startSpeed.constantMin, 0.18f, "speed from 30 percent of 0.6");
            Near(main.startSpeed.constantMax, 0.6f, "to the full value");
            Near(main.gravityModifier.constant, -0.25f, "gravity is negative, so they rise");
            Near(main.startColor.color.a, 1f, "the start color is opaque white");
            Check(ps.emission.enabled, "emission is on");
            Near(ps.emission.rateOverTime.constant, 0f, "at rate 0, only Burst releases");
            Eq(ps.shape.shapeType, ParticleSystemShapeType.Sphere, "they appear in a sphere");
            Near(ps.shape.radius, 0.5f, "of radius 0.5");
            Check(ps.noise.enabled, "the Noise module flutters them");
            Near(ps.noise.strength.constant, 0.8f, "at strength 0.8");

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            Check(size.enabled && size.separateAxes, "size over lifetime has separate axes");
            Eq(size.x.mode, ParticleSystemCurveMode.TwoCurves, "the width is a random pair of curves");
            Eq(size.x.curveMin.length, 7 * 8 + 1, "each curve has 7 cycles of 8 keys and the last");
            Near(size.x.curveMin.Evaluate(0f), 1f, "one starts with the wings open");
            Near(size.x.curveMax.Evaluate(0f), ManagedSpecterFlap.WidthScale(0.25f, ManagedSpecterFlap.DefaultMin), "the other a quarter flap in");
            float lowest = 9f;
            for (int i = 0; i < size.x.curveMin.length; i++)
            {
                lowest = Math.Min(lowest, size.x.curveMin.keys[i].value);
            }

            Near(lowest, ManagedSpecterFlap.DefaultMin, "the curve closes the wings to the minimum");
            Eq(size.y.mode, ParticleSystemCurveMode.Constant, "the height is constant");
            Near(size.y.constant, 1f, "1");

            ParticleSystem.ColorOverLifetimeModule colorModule = ps.colorOverLifetime;
            Check(colorModule.enabled && colorModule.color.gradient != null, "color over lifetime carries the fade");
            Gradient gradient = colorModule.color.gradient;
            Near(gradient.EvaluateAlpha(0f), 0f, "alpha is 0 at birth");
            Near(gradient.EvaluateAlpha(0.06f), 1f, "1 after the fade in");
            Near(gradient.EvaluateAlpha(0.5f), 1f, "1 in the middle");
            Near(gradient.EvaluateAlpha(1f), 0f, "0 at the end");

            Check(p.Describe().Contains("0 alive") && p.Describe().Contains("5 vertex streams"), "Describe says what is alive and how many streams");

            // Bursts.
            Eq(p.Burst(0), 0, "a burst of 0 adds none");
            Eq(p.Burst(-2), 0, "and so does a negative one");
            Eq(p.Burst(new Vector3(float.NaN, 0f, 0f), 3), 0, "and a NaN position");
            Eq(ps.EmitCalls, 0, "none of them reached the Particle System");
            Eq(p.Burst(5), 5, "a burst of 5 adds 5");
            Eq(p.AliveCount, 5, "five are alive");
            Eq(p.SpawnedCount, 5, "and counted");
            Check(ps.EmitPositions[0] == go.transform.position && ps.EmitAppliedShape[0], "it emits at the object, spread over the sphere");
            Eq(p.Burst(new Vector3(7f, 0f, 0f), 3), 3, "a burst at a point adds those");
            Check(ps.EmitPositions[1] == new Vector3(7f, 0f, 0f), "at that point");
            Eq(p.Burst(100), 24, "a burst over the limit adds only the room left (32 - 8)");
            Eq(p.AliveCount, 32, "so the system is full");
            Eq(p.Burst(1), 0, "and a full system takes none");
            Check(p.Describe().Contains("32 alive"), "Describe agrees");
            ps.StubSetParticleCount(10);
            Eq(p.Burst(4), 4, "room opens up as particles die");

            // Changing a value rebuilds the settings on the next frame.
            p.Palette = ManagedPlasmaPalette.Fire;
            p.Additive = false;
            SetField(p, "_size", 2f);
            SetField(p, "_lifetime", 8f);
            SetField(p, "_maxParticles", 10);
            SetField(p, "_autoPerSecond", 4f);
            SetField(p, "_rise", 0f);
            SetField(p, "_flapCycles", 3f);
            Near(ps.main.startSize.constantMin, 0.56f, "nothing changes before the next update");
            Call(p, "Update");
            Near(mat.Floats["_PaletteRow"], ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Fire), "the palette reaches the material");
            Near(mat.Floats["_DstBlend"], 10f, "so does alpha blending");
            Near(ps.main.startSize.constantMin, 1.6f, "the size follows");
            Near(ps.main.startSize.constantMax, 2.4f, "both ends");
            Near(ps.main.startLifetime.constantMax, 10f, "and the lifetime");
            Eq(ps.main.maxParticles, 10, "and the limit");
            Near(ps.emission.rateOverTime.constant, 4f, "and the auto rate");
            Near(Math.Abs(ps.main.gravityModifier.constant), 0f, "and the rise");
            Eq(ps.sizeOverLifetime.x.curveMin.length, 3 * 8 + 1, "and the flap cycles");
            Eq(p.Burst(50), 0, "a system at its new limit (10 alive) takes none");
            p.CyclePalette();
            Eq(p.Palette, ManagedPlasmaPalette.Water, "Fire cycles to Water");
            Call(p, "Update");
            Near(mat.Floats["_PaletteRow"], ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Water), "and the next update applies it");
            SetField(p, "_maxParticles", 9999);
            Call(p, "OnValidate");
            Call(p, "Update");
            Eq(ps.main.maxParticles, ManagedSpecterParticles.HardMaxParticles, "a limit over the hard maximum is capped");
            SetField(p, "_wobble", 9f);
            SetField(p, "_glow", -5f);
            Call(p, "OnValidate");
            Call(p, "Update");
            Near(mat.Floats["_Wobble"], ManagedSpecterLimits.MaxWobble, "a huge wobble is clamped on the material");
            Near(mat.Floats["_Glow"], ManagedSpecterLimits.MinGlow, "and a negative glow");

            // Disable tears down, enable builds again.
            Call(p, "OnDisable");
            Check(p.Particles == null, "disabling removes the system");
            Check(go.transform.Find(ManagedSpecterParticles.ChildName) == null, "and the child");
            Check(mat == null, "and the material");
            Eq(p.AliveCount, 0, "nothing is alive");
            Eq(p.SpawnedCount, 0, "and the count starts again");
            Call(p, "Update");
            Call(p, "OnDisable");
            Call(p, "OnEnable");
            Check(p.Particles != null && p.Particles != ps, "enabling again builds a fresh system");
            Eq(p.Burst(2), 2, "that takes bursts");
            Call(p, "OnDisable");

            // Burst before enabling builds on demand.
            Eq(p.Burst(2), 2, "a burst on a disabled component builds the system first");
            Call(p, "OnDisable");
            Eq(Debug.Errors.Count, 0, "no errors so far");

            // No shader.
            Shader.Known.Remove(ManagedSpecterAssets.ShaderName);
            Call(p, "OnEnable");
            Check(p.Particles == null, "without the shader nothing is built");
            Eq(p.Burst(3), 0, "a burst releases none");
            Eq(Debug.Errors.Count, 1, "and one error is logged, not one per try");
            Check(p.Describe().Contains("shader is missing"), "Describe says the shader is missing");
            Call(p, "OnDisable");
            Shader.Known.Add(ManagedSpecterAssets.ShaderName);
        }

        private static void SpecterPreviewTests()
        {
            Section("Specter preview");
            Shader.Known.Add(ManagedSpecterAssets.ShaderName);
            Graphics.Calls.Clear();
            Debug.Errors.Clear();
            int frame = Time.frameCount + 11000;
            float savedTime = Time.time;
            UnityEngine.Camera savedCamera = UnityEngine.Camera.main;
            SpecterCamera();

            GameObject go = new GameObject("preview", typeof(ManagedSpecterPreview));
            go.layer = 3;
            go.transform.position = new Vector3(0f, 1.5f, 0f);
            ManagedSpecterPreview preview = go.GetComponent<ManagedSpecterPreview>();
            SetField(preview, "_animate", false);
            SetField(preview, "_life", 0.3f);
            SetField(preview, "_fade", 0.5f);
            Check(preview.DrawPath == null, "no renderer is built before the first update");

            Time.frameCount = frame++;
            Call(preview, "Update");
            Eq(Graphics.Calls.Count, 1, "one update draws one call");
            Eq(Graphics.Calls[0].Count, 1, "of one butterfly");
            Eq(Graphics.Calls[0].Layer, 3, "on the object's layer");
            Vector4 a = Graphics.Calls[0].VectorArrays[SpecterParamA][0];
            Near(a.x, 0.3f, "held at the Life slider");
            Near(a.y, 0.5f, "with the Fade slider");
            Near(a.w, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Arcane), "in the Arcane palette");
            Matrix4x4 m = Graphics.Calls[0].Snapshot[0];
            Near(new Vector3(m.m01, m.m11, m.m21).magnitude, 1.5f, "the quad is the Diameter high");
            Near(new Vector3(m.m00, m.m10, m.m20).magnitude, 1.5f, "and as wide, with no flap while held", 1e-3f);
            Vector3 z = new Vector3(m.m02, m.m12, m.m22).normalized;
            Vector3 away = (go.transform.position - UnityEngine.Camera.main.transform.position).normalized;
            Check(Vector3.Dot(z, away) > 0.999f, "it faces the camera by default");

            // A new seed and palette change the spec.
            Graphics.Calls.Clear();
            preview.Seed = 5;
            preview.CyclePalette();
            Eq(preview.Palette, ManagedPlasmaPalette.Fire, "Arcane cycles to Fire");
            Time.frameCount = frame++;
            Call(preview, "Update");
            Near(Graphics.Calls[0].VectorArrays[SpecterParamA][0].z, 5 * 0.37f, "the new seed reaches _ParamA", 1e-3f);
            Near(Graphics.Calls[0].VectorArrays[SpecterParamA][0].w, ManagedPlasmaRingSpec.RowFor(ManagedPlasmaPalette.Fire), "and so does the palette");
            SetField(preview, "_wobble", 0.5f);
            Call(preview, "OnValidate");
            Check(Math.Abs(preview.Spec.Wobble - 0.5f) < 1e-6f, "an Inspector change builds a new spec");

            // Animating runs through the life and flaps.
            SetField(preview, "_animate", true);
            SetField(preview, "_loopSeconds", 4f);
            Graphics.Calls.Clear();
            Time.time = 1f;
            Time.frameCount = frame++;
            Call(preview, "Update");
            Near(Graphics.Calls[0].VectorArrays[SpecterParamA][0].x, 0.25f, "one second into a four second loop is life 0.25");
            Near(Graphics.Calls[0].VectorArrays[SpecterParamA][0].y, 0.5f, "fully faded in, so the fade is the slider's 0.5");
            Matrix4x4 flap = Graphics.Calls[0].Snapshot[0];
            float width = new Vector3(flap.m00, flap.m10, flap.m20).magnitude;
            Near(width, 1.5f * ManagedSpecterFlap.WidthScale(2.5f, ManagedSpecterFlap.DefaultMin), "the width follows the flap", 1e-3f);
            Graphics.Calls.Clear();
            Time.time = 5.5f;
            Time.frameCount = frame++;
            Call(preview, "Update");
            Near(Graphics.Calls[0].VectorArrays[SpecterParamA][0].x, 0.375f, "the loop starts again after 4 seconds");
            Graphics.Calls.Clear();
            Time.time = 7.99f;
            Time.frameCount = frame++;
            Call(preview, "Update");
            Check(Graphics.Calls[0].VectorArrays[SpecterParamA][0].y < 0.05f, "and the butterfly has faded out at the end of the loop");

            // Not facing the camera uses the object's rotation.
            SetField(preview, "_faceCamera", false);
            SetField(preview, "_animate", false);
            go.transform.rotation = Quaternion.identity;
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Call(preview, "Update");
            Matrix4x4 plain = Graphics.Calls[0].Snapshot[0];
            Near(plain.m22, 1.5f, "without Face Camera the quad keeps the object's rotation", 1e-3f);

            // The fallback and the description.
            SetField(preview, "_forceDrawMesh", true);
            Graphics.Calls.Clear();
            Time.frameCount = frame++;
            Call(preview, "Update");
            Eq(Graphics.Calls[0].Kind, "mesh", "Force Draw Mesh uses the fallback");
            Eq(preview.DrawPath, (ManagedPlasmaDrawPath?)ManagedPlasmaDrawPath.DrawMesh, "and the preview says so");
            string text = preview.DescribeStats();
            Check(text.Contains("seed 5") && text.Contains("Fire") && text.Contains("DrawMesh"), "DescribeStats names the seed, the palette and the path");
            Call(preview, "OnDisable");
            Check(preview.DrawPath == null, "disabling drops the renderer");
            Eq(Debug.Errors.Count, 0, "no errors were logged");

            Time.time = savedTime;
            UnityEngine.Camera.main = savedCamera;
        }

        // The C# sets shader properties and the keyword by name. A renamed property would fail silently in Unity, so the shader
        // source is read and every name is looked for. Skipped, with a note, when the shader file cannot be found from here.
        private static void SpecterShaderSourceTests()
        {
            Section("Specter shader names");
            string path = FindUpwards(Path.Combine("Assets", "MidManStudio", "Gtg", "Managed", "Specter", "ManagedSpecterButterfly.shader"));
            if (path == null)
            {
                Console.WriteLine("NOTE specter shader source not found from " + Directory.GetCurrentDirectory() + ", names not cross-checked");
                return;
            }

            string source = File.ReadAllText(path);
            Check(source.Contains("Shader \"" + ManagedSpecterAssets.ShaderName + "\""), "the shader declares the name the C# looks for");
            string[] properties = { "_RampTex", "_Intensity", "_DstBlend", "_PaletteRow", "_Wobble", "_Glow", "_Churn", "_Reach", "_UseStreams", "_ParamA", "_ParamB" };
            foreach (string property in properties)
            {
                Check(source.Contains(property), "the shader has " + property);
            }

            Check(source.Contains("[Toggle(" + ManagedSpecterAssets.StreamsKeyword + ")]"), "the toggle sets the keyword the C# enables");
            Check(source.Contains("#pragma multi_compile_local _ " + ManagedSpecterAssets.StreamsKeyword),
                "the keyword is a multi_compile, so a material made at run time still gets the variant");
            Check(source.Contains("#pragma multi_compile_instancing"), "instancing is compiled in");
            Check(source.Contains("Blend SrcAlpha [_DstBlend]"), "the blend reads _DstBlend, which the C# switches between 1 and 10");
            Check(source.Contains("Range(0, 0.6)") && source.Contains("Range(0.1, 4)") && source.Contains("Range(0, 3)") && source.Contains("Range(0.5, 3)"),
                "the property ranges match the C# limits");
            Check(source.Contains("input.uv0.w * 92.0"), "the stream path scales the random by 92 as the spec's seed range does (251 x 0.37)");
        }

        private static string FindUpwards(string relative)
        {
            DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 8 && dir != null; i++)
            {
                string candidate = Path.Combine(dir.FullName, relative);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            return null;
        }
    }
}
