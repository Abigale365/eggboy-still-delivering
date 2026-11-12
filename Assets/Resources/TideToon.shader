// 2026-11-12 塞尔达式卡通材质（样张）。保留模型原来的贴图和法线，只换光照算法：
//   · 明暗分块：受光 / 背光之间是一条窄窄的过渡带，不是平滑渐变；背光里再分一层更深的
//   · 阴影带颜色：背光面 = 固有色 × 环境光 × 偏蓝紫的阴影色，不发灰发黑
//   · 边缘光：背对镜头的轮廓上一圈亮边，受光侧更明显
//   · 空气感：越远越往天色里收一点
// 材质属性的名字和 URP Lit 一致（_BaseMap / _BaseColor / _BumpMap / _EmissionColor），原来按这些名字改材质的代码照常能用。
// 风格参数是全局变量（_Toon*），由 TownLighting 按白天 / 黄昏设，look.json 可调。
Shader "TideTown/Toon"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _UseNormal ("Use Normal Map", Float) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _AlphaClip ("Alpha Clip", Float) = 0
        _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _BumpScale;
            half _UseNormal;
            half _Cutoff;
            half _AlphaClip;
            half _Cull;
        CBUFFER_END

        half4 SampleBase(float2 uv)
        {
            half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
            if (_AlphaClip > 0.5) clip(c.a - _Cutoff);
            return c;
        }
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // 全局风格参数（TownLighting 设）
            half4 _ToonShadowTint;    // 阴影色
            half4 _ToonHazeColor;     // 远处的空气色
            half _ToonLit;            // 受光面亮度
            half _ToonShade;          // 背光面亮度
            half _ToonEdge;           // 明暗交界的位置（0.5 = 正侧面）
            half _ToonSoft;           // 交界的软硬
            half _ToonRim;            // 边缘光强度
            half _ToonHaze;           // 空气感强度
            half _ToonNormal;
            half _ToonDebug;         // 法线贴图参与多少（1 = 全用；小一点画面更干净）

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = n.normalWS;
                o.tangentWS = half4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 base = SampleBase(i.uv);
                half3 albedo = base.rgb;

                half3 N = normalize(i.normalWS) * (facing >= 0 ? 1.0 : -1.0);
                if (_UseNormal > 0.5)
                {
                    half3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                    half3 T = normalize(i.tangentWS.xyz);
                    half3 B = cross(N, T) * i.tangentWS.w;
                    half3 Nm = normalize(nt.x * T + nt.y * B + nt.z * N);
                    N = normalize(lerp(N, Nm, _ToonNormal));
                }
                half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(shadowCoord);

                // 明暗分块
                half d = dot(N, sun.direction) * 0.5 + 0.5;
                half band = smoothstep(_ToonEdge - _ToonSoft, _ToonEdge + _ToonSoft, d);
                half cast = smoothstep(0.30, 0.62, sun.shadowAttenuation);
                half lit = band * cast;
                half deep = smoothstep(0.16, 0.16 + _ToonSoft * 2.0 + 0.04, d);   // 背光里更深的一层

                // 环境光取得平一点：法线和正上方各一半，背光面不会一块块花
                half3 ambient = SampleSH(normalize(N + half3(0, 1, 0)));
                half ao = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    float2 ssuv = GetNormalizedScreenSpaceUV(i.positionCS);
                    ao = lerp(1.0, SampleAmbientOcclusion(ssuv), 0.8);
                #endif

                half3 shade = albedo * ambient * _ToonShadowTint.rgb * _ToonShade * lerp(0.80, 1.0, deep) * ao;
                half3 light = albedo * (sun.color * _ToonLit + ambient * 0.55) * lerp(ao, 1.0, 0.6);
                half3 col = lerp(shade, light, lit);

                // 边缘光
                half fres = 1.0 - saturate(dot(N, V));
                half rim = smoothstep(0.58, 0.72, fres) * (0.25 + 0.75 * lit) * _ToonRim;
                col += rim * sun.color * lerp(albedo, half3(1, 1, 1), 0.35) * 0.5;

                // 路灯这类点光：也分块
                #if defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    for (uint li = 0u; li < count; li++)
                    {
                        Light l = GetAdditionalLight(li, i.positionWS);
                        half a = l.distanceAttenuation * l.shadowAttenuation * smoothstep(-0.05, 0.25, dot(N, l.direction));
                        col += albedo * l.color * (smoothstep(0.01, 0.12, a) * 0.22 + a * 0.6);
                    }
                #endif

                col += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;

                // 空气感
                float dist = distance(_WorldSpaceCameraPos, i.positionWS);
                half haze = saturate((dist - 30.0) / 260.0) * _ToonHaze;
                col = lerp(col, _ToonHazeColor.rgb, haze);

                col = MixFog(col, i.fogFactor);
                if (_ToonDebug > 0.5) col = sun.shadowAttenuation.xxx;
                return half4(col, 1.0);
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
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"   // Shadows.hlsl 要用里面的 LerpWhiteTo，少了这行整个通道编译不过、什么都投不出影子
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
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
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                SampleBase(i.uv);
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
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                SampleBase(i.uv);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                SampleBase(i.uv);
                return half4(normalize(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
