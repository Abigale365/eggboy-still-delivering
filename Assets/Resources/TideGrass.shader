// 2026-11-13 草叶。参考《塞尔达：旷野之息》的草地：一片片真的草叶，根部深、叶尖浅，
// 风一阵一阵地从草面上扫过去，人走过时草往两边倒。
// 顶点色（IslandBuilder.Flora.cs 写入）：R = 在草叶上的高度（0 根 1 尖），G = 每丛的随机数，B = 每丛的色差。
// 草叶不投影（投了满地碎影），但会接别的东西投下来的影子。
Shader "TideTown/Grass"
{
    Properties
    {
        _BaseColor ("Root Color", Color) = (0.20, 0.40, 0.12, 1)
        _TipColor ("Tip Color", Color) = (0.56, 0.74, 0.24, 1)
        _DryColor ("Patch Color", Color) = (0.70, 0.74, 0.28, 1)
        _Bend ("Wind Bend (m)", Float) = 0.11
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Pass
        {
            Name "GrassForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _DryColor;
                half _Bend;
            CBUFFER_END

            float _TideWind;
            float4 _TidePlayerPos;    // xyz 主角脚下的位置，w = 1 有效
            half4 _ToonShadowTint;
            half4 _ToonHazeColor;
            half _ToonLit;
            half _ToonShade;
            half _ToonHaze;

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half gust : TEXCOORD3;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), f.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), f.x), f.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                float h = v.color.r;
                float t = _Time.y;

                // 风：一道道亮带从草面上扫过去
                float wave = sin(t * 1.4 - wp.x * 0.55 - wp.z * 0.42) * 0.5 + 0.5;
                float small = sin(t * 3.1 + wp.x * 1.9 + wp.z * 2.3 + v.color.g * 6.28);
                float bend = (wave * wave * 0.85 + small * 0.15 + 0.15) * _Bend * _TideWind;
                wp.xz += float2(0.8, 0.6) * bend * h * h;

                // 主角走过：脚边的草往外倒
                float2 away = wp.xz - _TidePlayerPos.xz;
                float dist = length(away);
                float push = saturate(1.0 - dist / 0.75) * step(abs(wp.y - _TidePlayerPos.y), 1.2) * _TidePlayerPos.w;
                wp.xz += normalize(away + 0.0001) * push * 0.22 * h;
                wp.y -= (bend * 0.35 + push * 0.12) * h * h;

                o.positionWS = wp;
                o.positionCS = TransformWorldToHClip(wp);
                o.color = v.color;
                o.gust = wave * wave;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half h = i.color.r;
                // 一片片深浅不同的草斑（大尺度），每丛再有一点自己的色差
                half patch = vnoise(i.positionWS.xz * 0.16) * 0.7 + vnoise(i.positionWS.xz * 0.47 + 7.3) * 0.3;
                half3 tip = lerp(_TipColor.rgb, _DryColor.rgb, smoothstep(0.52, 0.80, patch) * 0.55);
                half3 albedo = lerp(_BaseColor.rgb, tip, h * h * 0.85 + h * 0.15);
                albedo *= 0.90 + 0.20 * i.color.b;
                albedo *= 1.0 + i.gust * h * 0.16;   // 风扫过的地方叶尖翻亮

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half cast = smoothstep(0.30, 0.62, sun.shadowAttenuation);
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 shade = albedo * ambient * _ToonShadowTint.rgb * _ToonShade;
                half3 light = albedo * (sun.color * _ToonLit + ambient * 0.55);
                half3 col = lerp(shade, light, cast);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    for (uint li = 0u; li < count; li++)
                    {
                        Light l = GetAdditionalLight(li, i.positionWS);
                        col += albedo * l.color * l.distanceAttenuation * 0.6;
                    }
                #endif

                float dist = distance(_WorldSpaceCameraPos, i.positionWS);
                col = lerp(col, _ToonHazeColor.rgb, saturate((dist - 30.0) / 260.0) * _ToonHaze);
                col = MixFog(col, i.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
