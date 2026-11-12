// 2026-11-13 植被材质（树冠、灌木、花）。参考《塞尔达：旷野之息》的做法：
//   · 树冠 = 里面一团实心体积 + 外面一层层叶片面片，轮廓是毛茸茸的叶子边，不是光滑的球
//   · 法线统一朝树冠外，整棵树的明暗是一大块一大块的；树冠底下偏暗，顶上偏亮
//   · 逆光看时叶片边缘透出一圈暖光
//   · 整团随风慢慢晃，叶片自己再细细地抖
// 顶点色约定（IslandBuilder.Flora.cs 写入）：R = 随风摆动的幅度，G = 随机相位，B = 在树冠里的高度（0 底 1 顶），
//   A = 1 是叶片 / 花瓣面片（用贴图的透明度裁边），0 是实心体积。
// _Flower = 1 时按花来画：贴图 R 是花瓣明暗，G 是花心。
Shader "TideTown/Foliage"
{
    Properties
    {
        _BaseMap ("Leaf / Petal Map", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0.45, 0.62, 0.25, 1)
        _ShadeColor ("Shade Color", Color) = (0.22, 0.40, 0.30, 1)
        _CenterColor ("Flower Center", Color) = (1.0, 0.82, 0.30, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.42
        _Flower ("Is Flower", Float) = 0
        _Sway ("Sway (m)", Float) = 0.07
        _Flutter ("Flutter (m)", Float) = 0.012
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadeColor;
            half4 _CenterColor;
            half _Cutoff;
            half _Flower;
            half _Sway;
            half _Flutter;
        CBUFFER_END

        float _TideWind;   // 全局风力，TownLighting 设；没设时是 0，叶子不动

        // 世界坐标上的风：一阵一阵地从西南往东北刮
        float3 WindOffset(float3 wp, float3 normalWS, half4 color)
        {
            float t = _Time.y;
            float gust = sin(t * 0.9 + wp.x * 0.21 + wp.z * 0.17) * 0.6 + sin(t * 1.7 + wp.x * 0.43 - wp.z * 0.31) * 0.4;
            float3 sway = float3(0.8, 0.0, 0.6) * gust * _Sway * color.r;
            float flut = sin(t * 5.3 + color.g * 40.0 + wp.x * 3.1 + wp.z * 2.7) * _Flutter * color.a;
            return (sway + normalWS * flut) * _TideWind;
        }

        half AlphaOf(float2 uv, half4 color)
        {
            half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
            return lerp(1.0, a, color.a);
        }
        ENDHLSL

        Pass
        {
            Name "FoliageForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            AlphaToMask On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 _ToonShadowTint;
            half4 _ToonHazeColor;
            half _ToonLit;
            half _ToonShade;
            half _ToonHaze;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                half3 n = TransformObjectToWorldNormal(v.normalOS);
                wp += WindOffset(wp, n, v.color);
                o.positionWS = wp;
                o.positionCS = TransformWorldToHClip(wp);
                o.normalWS = n;
                o.uv = v.uv;
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half alpha = lerp(1.0, tex.a, i.color.a);
                // 透明度锐化一下再交给多重采样：边缘干净，不发虚
                alpha = saturate((alpha - _Cutoff) / max(fwidth(alpha), 0.0001) + 0.5);
                clip(alpha - 0.01);

                half3 baseCol = _BaseColor.rgb;
                if (_Flower > 0.5)
                {
                    baseCol = lerp(baseCol * (0.72 + 0.28 * tex.r), _CenterColor.rgb, tex.g);
                }
                else
                {
                    // 叶片：贴图只管每片叶子的深浅；实心体积比叶片暗一档，从叶缝里看进去是深色
                    half leaf = lerp(0.74, 0.78 + 0.30 * tex.r, i.color.a);
                    baseCol *= leaf;
                    baseCol *= 0.94 + 0.12 * i.color.g;   // 一片片叶子颜色略有不同
                }

                half3 N = normalize(i.normalWS);
                half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));

                // 大块明暗：朝太阳的半边亮，另一半落进带颜色的阴影；树冠底下再暗一点
                half d = dot(N, sun.direction) * 0.5 + 0.5;
                half band = smoothstep(0.36, 0.62, d);
                half cast = lerp(1.0, smoothstep(0.25, 0.7, sun.shadowAttenuation), 0.85);
                half lit = band * cast;
                half height = lerp(0.72, 1.0, i.color.b);

                half3 ambient = SampleSH(normalize(N + half3(0, 1.2, 0)));
                half3 shadeCol = lerp(baseCol, _ShadeColor.rgb, _Flower > 0.5 ? 0.25 : 0.55);
                half3 shade = shadeCol * ambient * _ToonShadowTint.rgb * _ToonShade * height;
                half3 light = baseCol * (sun.color * _ToonLit + ambient * 0.55) * lerp(height, 1.0, 0.5);
                half3 col = lerp(shade, light, lit);

                // 逆光透亮：朝着太阳看时，叶片被照透
                half back = pow(saturate(dot(V, -sun.direction)), 3.0) * (1.0 - band * 0.5);
                col += baseCol * sun.color * back * 0.35 * cast * i.color.a;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    for (uint li = 0u; li < count; li++)
                    {
                        Light l = GetAdditionalLight(li, i.positionWS);
                        col += baseCol * l.color * l.distanceAttenuation * 0.6;
                    }
                #endif

                float dist = distance(_WorldSpaceCameraPos, i.positionWS);
                col = lerp(col, _ToonHazeColor.rgb, saturate((dist - 30.0) / 260.0) * _ToonHaze);
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                positionWS += WindOffset(positionWS, normalWS, v.color);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(AlphaOf(i.uv, i.color) - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                wp += WindOffset(wp, TransformObjectToWorldNormal(v.normalOS), v.color);
                o.positionCS = TransformWorldToHClip(wp);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(AlphaOf(i.uv, i.color) - _Cutoff);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; half3 normalWS : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                half3 n = TransformObjectToWorldNormal(v.normalOS);
                wp += WindOffset(wp, n, v.color);
                o.positionCS = TransformWorldToHClip(wp);
                o.uv = v.uv;
                o.color = v.color;
                o.normalWS = n;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(AlphaOf(i.uv, i.color) - _Cutoff);
                return half4(normalize(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
