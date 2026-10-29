// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphLine.shader"
// ============================================================================

Shader "MidManStudio/Gtg/GlyphLine"
{
    Properties
    {
        _CoreColor   ("Core Color", Color) = (1, 1, 1, 1)
        _Intensity   ("Intensity", Range(0, 4)) = 1.4
        _CoreWidth   ("Core Width", Range(0.05, 0.6)) = 0.3
        _GlowPower   ("Glow Falloff", Range(0.5, 4)) = 2
        _HeadBoost   ("Draw Head Boost", Range(0, 4)) = 1.5
        _PulseSpeed  ("Pulse Speed", Float) = 2.5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.25
        _Reveal      ("Reveal", Range(0, 1)) = 1
        _Fade        ("Fade", Range(0, 1)) = 1
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

        Blend SrcAlpha OneMinusSrcAlpha
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

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float  _Intensity;
                float  _CoreWidth;
                float  _GlowPower;
                float  _HeadBoost;
                float  _PulseSpeed;
                float  _PulseAmount;
            CBUFFER_END

            // Per glyph values. Graphics.DrawMeshInstanced sets them per instance through a property block. When the
            // shader is not instanced (Graphics.DrawMesh path) the macros below give plain uniforms, set the same way.
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Reveal)
                UNITY_DEFINE_INSTANCED_PROP(float, _Fade)
            UNITY_INSTANCING_BUFFER_END(Props)

            // Vertex data written by ManagedGlyphBaker:
            // uv0 = (distance along the stroke, side -1 to 1)
            // uv1 = (stroke length, outer half width)
            // uv2 = (reveal value where the stroke starts, reveal value where it is whole)
            // color = halo tint
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float2 uv2        : TEXCOORD2;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float2 uv2        : TEXCOORD2;
                float4 color      : COLOR;
                float2 revealFade : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                output.color = input.color;

                // Read here, not in the fragment stage, so the fragment stage needs no instance id.
                output.revealFade = float2(UNITY_ACCESS_INSTANCED_PROP(Props, _Reveal),
                                           UNITY_ACCESS_INSTANCED_PROP(Props, _Fade));
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float along  = input.uv0.x;
                float side   = input.uv0.y;
                float length = input.uv1.x;
                float hw     = max(input.uv1.y, 0.00001);
                float t0     = input.uv2.x;
                float t1     = input.uv2.y;

                // How much of this stroke is drawn, 0 to 1. The stroke grows from its start.
                float drawn = saturate((input.revealFade.x - t0) / max(t1 - t0, 0.0001));
                if (drawn <= 0.0)
                {
                    discard;
                }

                float head = drawn * length;

                // Distance past the drawn part along the line, so the start, the end and the growing tip are round.
                float over = max(max(-along, along - head), 0.0);
                float across = side * hw;
                float d = sqrt(over * over + across * across) / hw;
                if (d >= 1.0)
                {
                    discard;
                }

                // Bright core, soft halo.
                float core = 1.0 - smoothstep(_CoreWidth * 0.7, _CoreWidth, d);
                float glow = pow(saturate(1.0 - d), _GlowPower);

                // Brightness wave that runs along the strokes.
                float wave = along / (hw * 14.0) * 6.2831853;
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed - wave);

                // The tip of a stroke that is still growing glows brighter.
                float behind = max(head - along, 0.0);
                float growing = 1.0 - step(0.999, drawn);
                float tip = growing * exp(-behind / (hw * 3.0));

                float3 halo = input.color.rgb * _Intensity * pulse;
                float3 coreColor = _CoreColor.rgb * _Intensity;
                float coreMix = saturate(core + tip * 0.5);
                float3 rgb = lerp(halo, coreColor, coreMix);

                float alpha = saturate(max(core, glow * 0.85) + tip * _HeadBoost * glow);
                alpha *= input.revealFade.y * input.color.a;
                return float4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
