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
        _TopColor ("Moss Color (up-facing)", Color) = (0.36, 0.56, 0.20, 1)
        _TopAmount ("Moss Amount", Range(0, 1)) = 0
        _WetBand ("Wet Band Above Water (m)", Float) = 0
        _Strata ("Rock Strata", Range(0, 1)) = 0
        // 2026-11-15 PL-16 民居屋顶换色（TownToon 给每档颜色派生一份材质时才打开，默认 0 = 不动）
        _RoofTint ("Roof Tint", Color) = (1, 1, 1, 1)
        _RoofSrc ("Roof Source Color (a = 1 use gamut gate)", Color) = (0.5, 0.5, 0.5, 0)
        _RoofAmount ("Roof Tint Amount", Range(0, 1)) = 0
        // 2026-11-15 PL-13 窗灯：跟着路灯的开关（全局 _TideLampOn）亮，默认黑 = 不是窗
        [HDR] _WindowGlow ("Window Glow", Color) = (0, 0, 0, 1)
        // 2026-11-15 PL-03 地面不参与"挡住主角就镂空"（TownToon 给 Ground 层的材质设 1）
        _CamFadeOff ("No Camera Fade", Float) = 0
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
            half4 _TopColor;
            half _TopAmount;
            half _WetBand;
            half _Strata;
            half4 _RoofTint;
            half4 _RoofSrc;
            half _RoofAmount;
            half4 _WindowGlow;
            half _CamFadeOff;
        CBUFFER_END

        float _TideWaterY;   // 全局：现在的海面高度（TownLighting 每帧设）

        // 2026-11-15 PL-03 前景挡住主角时镂空（和 TideFoliage.shader 里的是同一段，改要两边一起改）。
        //   _TideCamFade：xyz = 相机位置，w = 1 生效（ChaseCamera 每帧设；没人设时全 0，这段不起作用）
        //   _TidePlayerPos：xyz = 主角脚下，w = 1 有主角（TownLighting 每帧设）
        // 相机 → 主角胸口（脚下 + 0.45m）这条线周围 0.85m 以内的东西按 4×4 抖动网点裁掉：
        // 0.45m 以内全裁，0.45–0.85m 渐渐补回来。只在相机和主角之间那一段（t 0.05–0.96）起作用。
        // 比工程书的契约多两条保护（不加会出毛病）：
        //   · 视线下方收得更紧（按 1.8 倍算距离）：主角面前的地面、台阶离视线只有几十厘米，不收紧会被挖出洞
        //   · 离主角胸口 0.55m 以内不裁：手里抱着的柴火、油壶、球就在这个范围里
        // 另外 TownToon 给地面（Ground 层）和带骨骼的东西（小黑）的材质设 _CamFadeOff = 1，整个不参与。
        // 用在 Forward / DepthOnly / DepthNormals；ShadowCaster 不调——东西镂空了影子还在。
        float4 _TideCamFade;
        float4 _TidePlayerPos;

        float TideBayer2(float2 a) { a = floor(a); return frac(a.x * 0.5 + a.y * a.y * 0.75); }
        float TideBayer4(float2 a) { return TideBayer2(a * 0.5) * 0.25 + TideBayer2(a); }

        void TideCamFadeClip(float3 positionWS, float2 pixel)
        {
            if (_TideCamFade.w < 0.5 || _TidePlayerPos.w < 0.5) return;
            // 2026-11-15：主角膝盖以下的东西（台阶、路缘、低矮台地边）最多挡住脚，不裁——
            // 原来视线擦过低台阶时会在前景挖出一片黑点，看着像地上漏洞
            if (positionWS.y < _TidePlayerPos.y + 0.28) return;
            float3 a = _TideCamFade.xyz;
            float3 ab = _TidePlayerPos.xyz + float3(0.0, 0.45, 0.0) - a;
            float len2 = max(dot(ab, ab), 1e-4);
            float t = dot(positionWS - a, ab) / len2;
            if (t < 0.05 || t > 0.96 || (1.0 - t) * sqrt(len2) < 0.55) return;
            float3 off = positionWS - (a + ab * t);
            if (off.y < 0.0) off.y *= 1.8;
            float vis = smoothstep(0.45, 0.85, length(off));
            clip(vis - TideBayer4(pixel) - 0.03125);
        }

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
            half _TideLampOn;        // 路灯 / 窗灯亮多少：白天一点点，黄昏 1（TownLighting 设）

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
                if (_CamFadeOff < 0.5) TideCamFadeClip(i.positionWS, i.positionCS.xy);
                half4 base = SampleBase(i.uv);
                half3 albedo = base.rgb;

                half3 N = normalize(i.normalWS) * (facing >= 0 ? 1.0 : -1.0);

                // 2026-11-15 PL-16 民居屋顶换色：只动朝上的面，只动和原屋顶同一路颜色的像素，瓦片的深浅纹理留着
                if (_RoofAmount > 0.001)
                {
                    half roof = smoothstep(0.40, 0.58, N.y);
                    if (_RoofSrc.a > 0.5)
                        roof *= smoothstep(0.90, 0.97, dot(normalize(albedo + 1e-4), normalize(_RoofSrc.rgb + 1e-4)));
                    half lumA = dot(albedo, half3(0.2126, 0.7152, 0.0722));
                    half lumS = max(dot(_RoofSrc.rgb, half3(0.2126, 0.7152, 0.0722)), 0.02);
                    albedo = lerp(albedo, _RoofTint.rgb * min(lumA / lumS, 1.8), roof * _RoofAmount);
                }

                if (_UseNormal > 0.5)
                {
                    half3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                    half3 T = normalize(i.tangentWS.xyz);
                    half3 B = cross(N, T) * i.tangentWS.w;
                    half3 Nm = normalize(nt.x * T + nt.y * B + nt.z * N);
                    N = normalize(lerp(N, Nm, _ToonNormal));
                }
                half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));

                // 2026-11-13 海岸的三样自然效果（石头 / 崖壁 / 沙子的材质才打开）
                if (_Strata > 0.001)
                {
                    float wob = sin(i.positionWS.x * 0.9 + i.positionWS.z * 0.7) * 1.4;
                    float s = sin(i.positionWS.y * 6.5 + wob) * 0.6 + sin(i.positionWS.y * 17.0 + wob * 2.0) * 0.4;
                    albedo *= 1.0 + s * 0.09 * _Strata;
                }
                if (_TopAmount > 0.001)
                {
                    float nz = frac(sin(dot(floor(i.positionWS.xz * 3.0), float2(12.9898, 78.233))) * 43758.5453);
                    half up = smoothstep(0.80, 0.95, normalize(i.normalWS).y + (nz - 0.5) * 0.10);
                    // 泡在水里和刚出水的石头不长苔：离水面一米以上才慢慢长出来
                    half dry = smoothstep(0.5, 1.6, i.positionWS.y - _TideWaterY);
                    albedo = lerp(albedo, _TopColor.rgb * (0.9 + 0.2 * nz), up * dry * _TopAmount);
                }
                if (_WetBand > 0.001)
                {
                    float wet = saturate(1.0 - (i.positionWS.y - _TideWaterY) / _WetBand);
                    albedo *= lerp(1.0, 0.60, wet * wet);
                }

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

                // 2026-11-15 PL-13 白天灯只微微亮：太阳直晒的地方灯光基本看不出来，阴影里才透出暖色。
                // 黄昏（_TideLampOn = 1）时 dayFade = 1，和原来完全一样。
                half dayFade = 1.0 - 0.65 * lit * (1.0 - saturate(_TideLampOn));

                // 路灯这类点光：也分块
                #if defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    for (uint li = 0u; li < count; li++)
                    {
                        Light l = GetAdditionalLight(li, i.positionWS);
                        half a = l.distanceAttenuation * l.shadowAttenuation * smoothstep(-0.05, 0.25, dot(N, l.direction));
                        col += albedo * l.color * (smoothstep(0.01, 0.12, a) * 0.22 + a * 0.6) * dayFade;
                    }
                #endif

                col += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                // 窗灯：玻璃材质才有 _WindowGlow（TownToon 设），亮度跟着路灯走；玻璃本身深浅不同的地方亮度也不同
                col += _WindowGlow.rgb * _TideLampOn * dayFade * (0.55 + 0.9 * dot(albedo, half3(0.2126, 0.7152, 0.0722)));

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
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 镂空要和颜色通道裁得一样：不然深度图里还留着被裁掉的墙，后面的海面、环境光遮蔽会算错
                if (_CamFadeOff < 0.5) TideCamFadeClip(i.positionWS, i.positionCS.xy);
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
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (_CamFadeOff < 0.5) TideCamFadeClip(i.positionWS, i.positionCS.xy);
                SampleBase(i.uv);
                return half4(normalize(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
