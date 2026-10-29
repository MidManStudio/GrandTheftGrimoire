// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/plasma.md, section "ManagedPlasmaRing.shader"
// ============================================================================

Shader "MidManStudio/Gtg/PlasmaRing"
{
    Properties
    {
        _RampTex   ("Color Ramp (x = brightness, y = palette row)", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 4)) = 1
        _DstBlend  ("Dst Blend (1 = additive, 10 = alpha)", Float) = 1
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_RampTex);
            SAMPLER(sampler_RampTex);

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            // Per ring values. Graphics.DrawMeshInstanced sets them per instance through a property block. When the
            // shader is not instanced (Graphics.DrawMesh path) the macros give plain uniforms, set the same way.
            // _ParamA = (age in seconds, fade 0 to 1, ring radius, band width), both radii in half quad widths.
            // _ParamB = (seed, palette row 0 to 1, turbulence 0 to 1, streaks 0 to 1).
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ParamA)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ParamB)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
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
                output.uv = input.uv;

                // Read here, not in the fragment stage, so the fragment stage needs no instance id.
                output.paramA = UNITY_ACCESS_INSTANCED_PROP(Props, _ParamA);
                output.paramB = UNITY_ACCESS_INSTANCED_PROP(Props, _ParamB);
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

            // Three octaves, 0 to 1.
            float Fbm(float3 p)
            {
                float sum = 0.5 * ValueNoise(p);
                p = p * 2.03 + 17.1;
                sum += 0.25 * ValueNoise(p);
                p = p * 2.03 + 17.1;
                sum += 0.125 * ValueNoise(p);
                return sum / 0.875;
            }

            // ---- the ring ----------------------------------------------------------------------------------------
            // The graph this is made from: particle motion -> UV flow -> UV blur -> a shape warped by the flow ->
            // slope blur -> levels -> edge detect -> polar to cartesian -> color ramp. Here every step is a few lines
            // of math in one pass, in polar form, so there are no textures to render first.
            float4 frag(Varyings input) : SV_Target
            {
                float age    = input.paramA.x;
                float fade   = input.paramA.y;
                float ringR  = input.paramA.z;
                float band   = max(input.paramA.w, 0.01);
                float seed   = input.paramB.x;
                float row    = input.paramB.y;
                float turb   = input.paramB.z;
                float streak = input.paramB.w;

                float2 p = (input.uv - 0.5) * 2.0;
                float r = length(p);
                if (r >= 0.98 || fade <= 0.0)
                {
                    discard;
                }

                // Unit direction from the center. Noise is sampled on this circle, so the ring has no seam.
                float2 u = p / max(r, 0.0001);
                float t = age;
                float3 s = seed * float3(12.9, 7.7, 3.3);

                // The whole look turns slowly.
                float ca = cos(t * 0.2);
                float sa = sin(t * 0.2);
                float2 ur = float2(u.x * ca - u.y * sa, u.x * sa + u.y * ca);

                // UV flow: a smooth vector field that bends the lookups.
                float2 flow = float2(Fbm(float3(ur * 1.4, t * 0.35) + s),
                                     Fbm(float3(ur * 1.4 + float2(7.3, 0.0), t * 0.35 + 11.0) + s)) - 0.5;

                // The warped shape: its outline is the ring.
                float2 warped = ur * 1.8 + flow * (2.0 * turb);
                float e = Fbm(float3(warped, t * 0.55 + s.z));
                float edge = ringR * (1.0 + turb * 0.9 * (e - 0.5));
                float dn = (r - edge) / band;

                // Slope blur: noise stretched along the radius, drifting outward. This gives the wisps.
                float w = Fbm(float3(ur * 4.5 + flow * 1.5 + s.xy * 1.7, r * 1.2 - t * 0.8 + s.z * 1.7));
                w = saturate((w - 0.35) * 2.4);

                // Edge detect: a bright thin line on the outline, a soft glow inside, wisps outside.
                float core = exp(-dn * dn * 6.0);
                float side = smoothstep(-0.15, 0.15, dn);
                float inner = exp(min(dn, 0.0) * 1.5);
                float outer = exp(-max(dn, 0.0) * 1.1);
                float reach = streak * w * exp(-max(dn, 0.0) * 0.35) * smoothstep(-2.0, 0.0, dn);
                float sparkle = 0.75 + 0.5 * Fbm(float3(ur * 9.0 + s.xy, t * 1.1 + s.z));

                float v = core * 0.9 * sparkle
                        + inner * (1.0 - side) * 0.5
                        + outer * side * (0.3 + 0.7 * w)
                        + reach * 0.8;
                v *= smoothstep(0.0, ringR * 0.6, r);

                // Levels, then the color ramp.
                v = saturate((v - 0.08) / 0.85);
                v = saturate(v * _Intensity);
                float3 color = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(v, row)).rgb;

                float alpha = smoothstep(0.02, 0.25, v) * fade * (1.0 - smoothstep(0.78, 0.98, r));
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
