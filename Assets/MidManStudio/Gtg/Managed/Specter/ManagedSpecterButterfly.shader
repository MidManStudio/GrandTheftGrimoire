// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/specter.md, section "ManagedSpecterButterfly.shader"
// ============================================================================

Shader "MidManStudio/Gtg/SpecterButterfly"
{
    Properties
    {
        _RampTex   ("Color Ramp (x = brightness, y = palette row)", 2D) = "white" {}
        _Intensity ("Intensity (HDR multiplier)", Range(0, 8)) = 2
        _DstBlend  ("Dst Blend (1 = additive, 10 = alpha)", Float) = 1

        // Used only with the particle system path (keyword _SPECTER_STREAMS). The code path sends the same four
        // numbers per instance through _ParamA and _ParamB instead.
        _PaletteRow ("Palette row (0 to 1, particle path)", Range(0, 1)) = 0.5
        _Wobble     ("Wobble (particle path)", Range(0, 0.6)) = 0.3
        _Glow       ("Glow (particle path)", Range(0.1, 4)) = 1
        _Churn      ("Churn (particle path)", Range(0, 3)) = 1
        _Reach      ("Reach (particle path)", Range(0.5, 3)) = 1
        [Toggle(_SPECTER_STREAMS)] _UseStreams ("Read life and random from particle vertex streams", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _SPECTER_STREAMS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_RampTex);
            SAMPLER(sampler_RampTex);

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _PaletteRow;
                float _Wobble;
                float _Glow;
                float _Churn;
                float _Reach;
            CBUFFER_END

            // Per butterfly values on the code path. Graphics.DrawMeshInstanced sets them per instance through a property
            // block. When the shader is not instanced (Graphics.DrawMesh path) the macros give plain uniforms, set the same way.
            // _ParamA = (life 0 to 1, fade 0 to 1, seed, palette row 0 to 1).
            // _ParamB = (wobble, glow, churn, reach).
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ParamA)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ParamB)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
            #if defined(_SPECTER_STREAMS)
                // Particle path. Renderer vertex streams, in this order: Position, Color, UV, AgePercent, StableRandomX.
                // Unity packs UV into TEXCOORD0.xy, AgePercent into TEXCOORD0.z and StableRandomX into TEXCOORD0.w.
                half4  color      : COLOR;
                float4 uv0        : TEXCOORD0;
            #else
                float2 uv         : TEXCOORD0;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 paramA     : TEXCOORD1;
                float4 paramB     : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

            #if defined(_SPECTER_STREAMS)
                output.uv = input.uv0.xy;
                // fade comes from the particle's color alpha, so Color over Lifetime and the start color work as usual.
                output.paramA = float4(saturate(input.uv0.z), saturate(input.color.a), input.uv0.w * 92.0, _PaletteRow);
                output.paramB = float4(_Wobble, _Glow, _Churn, _Reach);
            #else
                output.uv = input.uv;
                // Read here, not in the fragment stage, so the fragment stage needs no instance id.
                output.paramA = UNITY_ACCESS_INSTANCED_PROP(Props, _ParamA);
                output.paramB = UNITY_ACCESS_INSTANCED_PROP(Props, _ParamB);
            #endif
                return output;
            }

            // ---- noise -------------------------------------------------------------------------------------------
            // A hash with no sin(), so it gives the same noise on every GPU.
            float Hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash13(i);
                float n100 = Hash13(i + float3(1, 0, 0));
                float n010 = Hash13(i + float3(0, 1, 0));
                float n110 = Hash13(i + float3(1, 1, 0));
                float n001 = Hash13(i + float3(0, 0, 1));
                float n101 = Hash13(i + float3(1, 0, 1));
                float n011 = Hash13(i + float3(0, 1, 1));
                float n111 = Hash13(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

            // The tutorial's Noise Texture node: 3D, fBM, detail 2 (three octaves), roughness 0.5, lacunarity 2,
            // distortion 0.25. Distortion bends the lookup with one more noise. Result 0 to 1, mean about 0.5.
            float SpecterNoise(float3 p)
            {
                float bend = (ValueNoise(p + 31.7) - 0.5) * 0.5;
                p += float3(bend, -bend, bend * 0.7);

                float sum = ValueNoise(p);
                p = p * 2.0 + 17.1;
                sum += 0.5 * ValueNoise(p);
                p = p * 2.0 + 17.1;
                sum += 0.25 * ValueNoise(p);
                return sum / 1.75;
            }

            float SmootherStep01(float edge0, float edge1, float x)
            {
                float t = saturate((x - edge0) / (edge1 - edge0));
                return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
            }

            // ---- the butterfly -----------------------------------------------------------------------------------
            // The tutorial graph, step by step:
            //  polar uv      angle from straight up, folded so left and right mirror (the wings), plus the radius
            //  time offset   the lookup slides with life, so the pattern changes as the butterfly ages
            //  random offset each butterfly looks somewhere else in the noise
            //  noise         3D fBM on that polar coordinate, scaled (5, 3.7, 10), bends the radius a little
            //  gradient      1 at the center, 0 far away, as a smooth step on the bent radius
            //  life fade     in the last quarter of life the gradient drops, so the butterfly fades away
            //  light falloff log base g of 0.999: about 0.001 / (1 - g), a bright core with a long soft tail (tail trimmed, see below)
            //  color         the palette ramp, brightness across, one palette per row
            float4 frag(Varyings input) : SV_Target
            {
                float life   = input.paramA.x;
                float fade   = input.paramA.y;
                float seed   = input.paramA.z;
                float row    = input.paramA.w;
                float wobble = input.paramB.x;
                float glow   = input.paramB.y;
                float churn  = input.paramB.z;
                float reach  = input.paramB.w;

                float2 p = input.uv - 0.5;
                float rq = length(p) * 2.0;
                if (rq >= 1.0 || fade <= 0.0)
                {
                    discard;
                }

                // polar uv: 0.5 straight up, 0 straight down, the same on both sides.
                float theta = atan2(abs(p.x), p.y);
                float fold = 0.5 - theta * 0.15915494;
                float rt = rq * reach * 0.4;

                // time offset and random offset
                float3 v = float3(fold, rt, 0.0);
                v += (life * 4.0 * churn) * float3(0.1, -0.1, -0.05);
                v += seed;

                float n = SpecterNoise(v * float3(5.0, 3.7, 10.0));
                n = saturate((n - 0.5) * 1.5 + 0.5);

                // gradient: 1 at the center, 0 far away
                float rr = rt + n * wobble;
                float g = 1.0 - smoothstep(-0.07, 1.5, rr);

                // life fade: the last quarter of life pulls the gradient down by up to 0.12
                g -= 0.12 * SmootherStep01(0.75, 1.0, life);

                // light falloff
                float gl = clamp(g, 0.0001, 0.999);
                float e = glow * 15.0 * log(0.999) / log(gl);

                // The tutorial leaves the long tail of that falloff to the compositor's Bloom. Without it the tail would
                // show as a dim disc, so the first 0.045 is cut off and the shape ends inside the quad.
                e = max(e - 0.045, 0.0);

                float b = saturate(e * 2.5);
                float3 color = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(b, row)).rgb;
                color *= _Intensity * (1.0 + max(e - 1.0, 0.0));

                float alpha = saturate(e * 2.5) * fade * (1.0 - smoothstep(0.7, 1.0, rq));
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
