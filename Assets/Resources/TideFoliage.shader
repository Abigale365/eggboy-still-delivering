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

        // 2026-11-14 EGGBOY / 小黑穿过花丛：读痕迹图（TideFeel.cs 每帧更新，说明见 TideTrail.shader）。
        //   · 枝叶是"绕着根弯下去"，不是整片平移：越高的地方弯得越多，长度不变，所以会往下低头
        //   · 身体两侧的往外分开，正前方的被带着往前倒；松开以后弹回来晃三四下
        //   · 每片叶子比枝条快一点或慢一点，不是整团一个节奏；枝条在动的时候叶片自己还会抖
        // 取代 11-13 的做法（按主角位置整片往外平移 + 最近 6 次蹭过的记录）。
        TEXTURE2D(_TideTrailSpring); SAMPLER(sampler_TideTrailSpring);
        TEXTURE2D(_TideTrailAux);
        float4 _TideTrail;            // x = 1 / 贴图盖的米数，yz = 主角的 xz，w = 1 有效

        float3 PushOffset(float3 wp, float3 normalWS, half4 color)
        {
            if (_TideTrail.w < 0.5) return 0;
            // 只管主角附近（贴图是首尾相接铺开的，远处有重影）；只管和身体差不多高的枝叶（头顶的树冠不动）
            // 2026-11-16：先算范围再采样，离主角 11 m 外的枝叶不再读贴图（全场枝叶顶点的大头都在这里省掉）
            float focus = saturate((11.0 - length(wp.xz - _TideTrail.yz)) * 0.5);
            if (focus <= 0.0) return 0;
            float2 uv = wp.xz * _TideTrail.x;
            float4 sp, ax;
            if (color.a > 0.5)
            {
                // 叶片 / 花瓣面片：四个角各读各的痕迹，贴着身体时痕迹梯度很陡，同一片的四个角被推得不一样远，面片被拉成长条。
                // 改成在 0.14 m 半径上取四点平均（面片本身 0.1–0.2 m），同一片的四个角读到的几乎一样，整片一起倒、不变形。
                float r = 0.14 * _TideTrail.x;
                sp = (SAMPLE_TEXTURE2D_LOD(_TideTrailSpring, sampler_TideTrailSpring, uv + float2(r, 0), 0)
                    + SAMPLE_TEXTURE2D_LOD(_TideTrailSpring, sampler_TideTrailSpring, uv - float2(r, 0), 0)
                    + SAMPLE_TEXTURE2D_LOD(_TideTrailSpring, sampler_TideTrailSpring, uv + float2(0, r), 0)
                    + SAMPLE_TEXTURE2D_LOD(_TideTrailSpring, sampler_TideTrailSpring, uv - float2(0, r), 0)) * 0.25;
                ax = (SAMPLE_TEXTURE2D_LOD(_TideTrailAux, sampler_TideTrailSpring, uv + float2(r, 0), 0)
                    + SAMPLE_TEXTURE2D_LOD(_TideTrailAux, sampler_TideTrailSpring, uv - float2(r, 0), 0)
                    + SAMPLE_TEXTURE2D_LOD(_TideTrailAux, sampler_TideTrailSpring, uv + float2(0, r), 0)
                    + SAMPLE_TEXTURE2D_LOD(_TideTrailAux, sampler_TideTrailSpring, uv - float2(0, r), 0)) * 0.25;
            }
            else
            {
                sp = SAMPLE_TEXTURE2D_LOD(_TideTrailSpring, sampler_TideTrailSpring, uv, 0);
                ax = SAMPLE_TEXTURE2D_LOD(_TideTrailAux, sampler_TideTrailSpring, uv, 0);
            }

            float dy = wp.y - ax.z;
            float gate = saturate((dy + 0.35) / 0.25) * saturate((1.5 - dy) / 0.5) * focus;
            if (gate <= 0.0) return 0;

            // 每片叶子自己的时间差：拿弹簧的速度往前 / 往后推几十毫秒
            float lag = (color.g - 0.5) * 0.10;
            float2 b = (sp.xy + sp.zw * lag) * gate;
            float m = length(b);
            float3 off = 0;
            if (m > 1e-4)
            {
                float2 dir = b / m;
                float soft = 0.12 + 0.88 * color.b;          // 根扎在地里不动，越往上越软（B = 在这一团里的高度）
                float ang = min(m, 1.3) * 1.05 * soft;        // 最多弯 78 度
                float hy = 0.10 + 0.62 * color.b;             // 这片叶子离根大约多高（米）
                off.xz = dir * hy * sin(ang);
                off.y = -hy * (1.0 - cos(ang));
            }
            // 枝条晃得越快，叶片抖得越厉害
            float speed = min(length(sp.zw) * gate, 3.0);
            off += normalWS * (sin(_Time.y * 31.0 + color.g * 50.0) * speed * 0.006 * color.a);
            return off;
        }

        // 世界坐标上的风：一阵一阵地从西南往东北刮
        float3 WindOffset(float3 wp, float3 normalWS, half4 color)
        {
            float t = _Time.y;
            float gust = sin(t * 0.9 + wp.x * 0.21 + wp.z * 0.17) * 0.6 + sin(t * 1.7 + wp.x * 0.43 - wp.z * 0.31) * 0.4;
            float3 sway = float3(0.8, 0.0, 0.6) * gust * _Sway * color.r;
            float flut = sin(t * 5.3 + color.g * 40.0 + wp.x * 3.1 + wp.z * 2.7) * _Flutter * color.a;
            return (sway + normalWS * flut) * _TideWind + PushOffset(wp, normalWS, color);
        }

        half AlphaOf(float2 uv, half4 color)
        {
            half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
            return lerp(1.0, a, color.a);
        }

        // 2026-11-15 PL-15 涨潮以后水下不开花。花丛是静态合批的，拿不到每丛的根，只能按每个顶点自己的高度来：
        //   · 顶点：水面上 10 厘米以内的往下收，水面下 4 厘米以下的全压到水下 8 厘米那一层（片元里裁掉）
        //   · 花（_Flower）：估出来的根泡进水里 5 厘米以上，整丛收掉——不然水面上漂着一层花
        //   · 叶子：根泡进水里 30 厘米以上整丛收掉；更浅的只去掉泡在水里的那截，露在水面上的枝冠留着
        //   · 片元：水面下 2 厘米以下裁掉，边缘按水平位置错开 ±3 厘米，不是一刀切齐
        // 四个通道（颜色、影子、深度、深度法线）都调，水下的影子一起消失。
        float _TideWaterY;   // 全局：现在的海面高度（TownLighting 每帧设）

        float3 SubmergeVertex(float3 wp, half4 color)
        {
            float y0 = wp.y;
            float sub = saturate((_TideWaterY + 0.10 - y0) / 0.14);
            wp.y = lerp(y0, _TideWaterY - 0.08, sub * sub);
            // 花：根泡进水里 5 厘米就整丛收掉。叶子：根泡进 30 厘米才收——再浅的留着露出水面的枝冠；
            // 不收的话涨潮后水面上漂着一个个灌木尖（11-15 第一版样张里就是这样，像垃圾）。
            float rootY = y0 - (0.10 + 0.62 * color.b);   // 和 PushOffset 里估根的办法一样
            float drown = saturate((_TideWaterY - (_Flower > 0.5 ? 0.05 : 0.30) - rootY) / 0.12);
            wp.y = lerp(wp.y, min(wp.y, _TideWaterY - 0.08), drown);
            return wp;
        }

        void SubmergeClip(float3 positionWS)
        {
            float jit = (sin(positionWS.x * 7.3 + positionWS.z * 2.9) + sin(positionWS.z * 6.1 - positionWS.x * 3.7)) * 0.015;
            clip(positionWS.y - (_TideWaterY - 0.02 + jit));
        }

        // 2026-11-15 PL-03 前景挡住主角时镂空（和 TideToon.shader 里的是同一段，改要两边一起改，说明在那边）。
        // 用在 Forward / DepthOnly / DepthNormals；ShadowCaster 不调——树冠镂空了影子还在。
        float4 _TideCamFade;      // xyz = 相机位置，w = 1 生效（没人设时全 0，不起作用）
        float4 _TidePlayerPos;    // xyz = 主角脚下，w = 1 有主角

        float TideBayer2(float2 a) { a = floor(a); return frac(a.x * 0.5 + a.y * a.y * 0.75); }
        float TideBayer4(float2 a) { return TideBayer2(a * 0.5) * 0.25 + TideBayer2(a); }

        void TideCamFadeClip(float3 positionWS, float2 pixel)
        {
            if (_TideCamFade.w < 0.5 || _TidePlayerPos.w < 0.5) return;
            if (positionWS.y < _TidePlayerPos.y + 0.28) return;   // 膝盖以下不裁（说明见 TideToon.shader）
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
                wp = SubmergeVertex(wp, v.color);
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
                SubmergeClip(i.positionWS);
                TideCamFadeClip(i.positionWS, i.positionCS.xy);
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
                    baseCol *= 0.92 + 0.16 * i.color.g;   // 一片片叶子颜色略有不同
                    // 2026-11-15 PL-17 一簇一簇的色差（簇的大小一米上下）：有的偏黄绿像新叶，有的偏蓝绿像老叶
                    half cl = sin(i.positionWS.x * 1.7 + i.positionWS.y * 2.3) * sin(i.positionWS.z * 1.9 - i.positionWS.y * 1.3);
                    baseCol *= lerp(half3(0.93, 0.99, 1.08), half3(1.08, 1.03, 0.90), cl * 0.5 + 0.5);
                }

                half3 N = normalize(i.normalWS);
                half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));

                // 大块明暗：朝太阳的半边亮，另一半落进带颜色的阴影
                half d = dot(N, sun.direction) * 0.5 + 0.5;
                half band = smoothstep(0.36, 0.62, d);
                half core = smoothstep(0.08, 0.30, d);   // 背光里再分一层：正背着太阳的那一块最深
                half cast = lerp(1.0, smoothstep(0.25, 0.7, sun.shadowAttenuation), 0.85);
                half lit = band * cast;

                // 2026-11-15 PL-17 树冠上下拉开：底下压深、偏冷，顶上提亮、偏暖（B = 在树冠里的高度）。
                // 花只按原来的幅度走，颜色主要看花瓣自己。
                half isLeaf = _Flower > 0.5 ? 0.0 : 1.0;
                half hb = i.color.b;
                half height = lerp(lerp(0.72, 0.56, isLeaf), lerp(1.0, 1.08, isLeaf), hb);
                half3 lowTint = lerp(half3(1, 1, 1), half3(0.92, 0.98, 1.06), isLeaf * (1.0 - hb));
                half3 topTint = lerp(half3(1, 1, 1), half3(1.06, 1.03, 0.90), isLeaf * hb);

                half3 ambient = SampleSH(normalize(N + half3(0, 1.2, 0)));
                half3 shadeCol = lerp(baseCol, _ShadeColor.rgb, _Flower > 0.5 ? 0.25 : 0.55);
                half3 shade = shadeCol * ambient * _ToonShadowTint.rgb * _ToonShade * height * lowTint * lerp(1.0, lerp(0.82, 1.0, core), isLeaf);
                half3 light = baseCol * (sun.color * _ToonLit + ambient * 0.55) * lerp(height, 1.0, lerp(0.5, 0.35, isLeaf)) * topTint;
                half3 col = lerp(shade, light, lit);

                // 逆光透亮：朝着太阳看时，叶片被照透；树冠轮廓那一圈透得最亮，带一点黄绿
                half back = pow(saturate(dot(V, -sun.direction)), 3.0) * (1.0 - band * 0.5);
                half edge = 1.0 - saturate(abs(dot(N, V)));
                half3 thru = lerp(half3(1, 1, 1), half3(1.12, 1.06, 0.72), isLeaf);
                col += baseCol * thru * sun.color * back * (0.35 + 0.55 * edge * edge * isLeaf) * cast * i.color.a;

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
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                positionWS += WindOffset(positionWS, normalWS, v.color);
                positionWS = SubmergeVertex(positionWS, v.color);
                o.positionWS = positionWS;
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
                SubmergeClip(i.positionWS);   // 水下那截不投影；相机镂空（TideCamFadeClip）这里不做
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
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                wp += WindOffset(wp, TransformObjectToWorldNormal(v.normalOS), v.color);
                wp = SubmergeVertex(wp, v.color);
                o.positionWS = wp;
                o.positionCS = TransformWorldToHClip(wp);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                SubmergeClip(i.positionWS);
                TideCamFadeClip(i.positionWS, i.positionCS.xy);
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
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; half3 normalWS : TEXCOORD2; float3 positionWS : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                half3 n = TransformObjectToWorldNormal(v.normalOS);
                wp += WindOffset(wp, n, v.color);
                wp = SubmergeVertex(wp, v.color);
                o.positionWS = wp;
                o.positionCS = TransformWorldToHClip(wp);
                o.uv = v.uv;
                o.color = v.color;
                o.normalWS = n;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                SubmergeClip(i.positionWS);
                TideCamFadeClip(i.positionWS, i.positionCS.xy);
                clip(AlphaOf(i.uv, i.color) - _Cutoff);
                return half4(normalize(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
