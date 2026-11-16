// WP-08/P2 海面：URP 自定义着色器。
// 顶点色 R = 离岸距离，画岸边泡沫；深度纹理做浅水/深水过渡；两层噪声法线反向滚动；顶点微浪。
// 2026-11-15 PL-14：颜色分浅滩 / 近海 / 外海三档；推岸的浪和礁石边的白沫重写（见 frag 里的说明）。
// 浅 / 深 / 外海三个颜色和 _DepthRange 运行时由 TownLighting 设（TownLookSet.water*），材质文件里的值只在编辑器里预览用。
Shader "TideTown/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.27, 0.66, 0.66, 1)
        _DeepColor ("Deep", Color) = (0.09, 0.30, 0.53, 1)
        _FarColor ("Far Sea", Color) = (0.15, 0.29, 0.43, 1)
        _FoamColor ("Foam", Color) = (0.94, 0.98, 0.96, 1)
        _ShoreRange ("Shore Range (m)", Float) = 8
        _FoamWidth ("Foam Width (m)", Float) = 1.4
        _DepthRange ("Depth Range (m)", Float) = 4
        _FoamDepth ("Foam Depth (m)", Float) = 0.9
        _Alpha ("Alpha", Range(0, 1)) = 0.8
        _WaveSpeed ("Wave Speed", Float) = 1
        _WaveStrength ("Normal Strength", Range(0, 2)) = 0.6
        _SpecStrength ("Spec Strength", Float) = 0.35
        _VertWave ("Vertex Wave (m)", Float) = 0.06
        _SkyColor ("Sky Reflection", Color) = (1.0, 0.72, 0.62, 1)
        _SkyReflect ("Sky Reflection Strength", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FarColor;
                float4 _FoamColor;
                float _ShoreRange;
                float _FoamWidth;
                float _DepthRange;
                float _FoamDepth;
                float _Alpha;
                float _WaveSpeed;
                float _WaveStrength;
                float _SpecStrength;
                float _VertWave;
                float4 _SkyColor;
                float _SkyReflect;
            CBUFFER_END

            // 2026-11-14 EGGBOY / 小黑踩水（TideFeel.cs 每帧给）：一圈圈往外扩的涟漪 + 贴着身体的水线
            float4 _TideRipple[16];     // xy = 位置（世界 xz），z = 开始的时刻，w = 力度
            float4 _TideRippleB[16];    // x = 寿命（秒），y = 扩散速度（米/秒），z = 起始半径
            float4 _TideRippleFocus;    // xy = 主角的 xz，z = 多远以内才算，w = 1 表示现在有涟漪
            float4 _TideWade[4];        // xy = 位置，z = 身体在水面处的半径，w = 力度
            float _TideTime;
            float _TideWaterY;          // 全局：现在的海面高度（TownLighting 每帧设）

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float shore : TEXCOORD1;
                float noise : TEXCOORD2;
                float fogFactor : TEXCOORD3;
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
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                // 近岸略小、外海略大；波长够长，远处 16m 网格也不会太碎
                float amp = _VertWave * (0.55 + 0.45 * saturate(v.color.r));
                // 2026-11-15：海面是两张网格拼的——岛周围 ±48 米一张细的（1.5 米一格），外面一张粗的（16 米一格）。
                // 接缝上细网格的点跟着浪上下动，粗网格那条边是直的，两边对不上就裂开一条条缝，
                // 从缝里露出天空的颜色，远看是海上一圈断断续续的细白线。接缝两侧 6 米内把顶点浪压到 0，两张网格在缝上就贴死了。
                // （只压顶点的起伏，水面的波纹和高光是片元里算的，不受影响。）
                amp *= saturate(abs(max(abs(v.positionOS.x), abs(v.positionOS.z)) - 48.0) / 6.0);
                float phase = _Time.y * _WaveSpeed;
                wp.y += (sin(wp.x * 0.55 + phase * 1.1) + sin(wp.z * 0.68 - phase * 0.8)) * amp;
                o.positionCS = TransformWorldToHClip(wp);
                o.worldPos = wp;
                o.shore = v.color.r;
                o.noise = v.color.g;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 screenUV = i.positionCS.xy / _ScaledScreenParams.xy;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float waterEye = -TransformWorldToView(i.worldPos).z;
                float thickness = max(0.0, sceneEye - waterEye);

                float t = _Time.y * _WaveSpeed;
                float2 p = i.worldPos.xz;

                // 两层噪声法线，反向滚动
                float2 uvA = p * 0.16 + float2(t * 0.022, t * 0.031);
                float2 uvB = p * 0.25 - float2(t * 0.034, -t * 0.019);
                float nA = vnoise(uvA);
                float nB = vnoise(uvB);
                float e = 0.15;
                float2 grad = float2(
                    vnoise(uvA + float2(e, 0)) - nA + vnoise(uvB + float2(e, 0)) - nB,
                    vnoise(uvA + float2(0, e)) - nA + vnoise(uvB + float2(0, e)) - nB) * _WaveStrength;

                // 踩出来的涟漪：每圈是一道亮线（浪峰），里侧跟一道暗线（浪谷）；越扩越慢、越宽、越淡
                float ring = 0.0, trough = 0.0;
                float2 ringDir = 0.0;
                float2 fd = p - _TideRippleFocus.xy;
                if (_TideRippleFocus.w > 0.5 && dot(fd, fd) < _TideRippleFocus.z * _TideRippleFocus.z)
                {
                    for (int k = 0; k < 16; k++)
                    {
                        float age = _TideTime - _TideRipple[k].z;
                        float life = _TideRippleB[k].x;
                        if (age <= 0.0 || age >= life) continue;
                        float2 d = p - _TideRipple[k].xy;
                        float dist = length(d);
                        float u = age / life;
                        float r = _TideRippleB[k].z + _TideRippleB[k].y * age * (1.0 - 0.35 * u);
                        if (dist > r + 0.3) continue;
                        float w = 0.028 + 0.06 * u;
                        float fade = (1.0 - u) * sqrt(1.0 - u) * saturate(age * 12.0) * _TideRipple[k].w;
                        float crest = smoothstep(0.0, 0.55, 1.0 - abs(dist - r) / w);
                        float inner = smoothstep(0.0, 0.55, 1.0 - abs(dist - r * 0.6) / (w * 0.8)) * 0.55 * saturate(u * 5.0 - 0.6);   // 第二圈晚一点冒出来
                        float lit = (crest + inner) * fade;
                        ring += lit;
                        trough += saturate(1.0 - abs(dist - (r - w * 2.2)) / (w * 1.8)) * fade;
                        ringDir += d / max(dist, 1e-3) * lit;
                    }
                    // 身体周围的水线：贴着身体一圈，被细噪声搅得歪歪扭扭
                    for (int a = 0; a < 4; a++)
                    {
                        if (_TideWade[a].w <= 0.0) continue;
                        float2 d = p - _TideWade[a].xy;
                        float dist = length(d);
                        float wob = vnoise(p * 11.0 + float2(t * 0.9, -t * 0.7)) * 0.05;
                        float edge = smoothstep(0.0, 0.6, 1.0 - abs(dist - _TideWade[a].z - 0.03 - wob) / 0.06);
                        ring += edge * _TideWade[a].w;
                    }
                }
                ring = saturate(ring);
                trough = saturate(trough) * (1.0 - ring);

                float3 N = normalize(float3(grad.x + ringDir.x * 0.9, 1.0, grad.y + ringDir.y * 0.9));

                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);

                // 2026-11-15 PL-14 海水分三档，不再是一整片浓蓝：
                //   浅滩（看得到底）清透青绿 → 近海青蓝 → 外海偏灰的深蓝。
                // 深浅看两样：水有多厚（场景深度差）和离岸多远（顶点色 R，离岸 8 米封顶）；再往外按离岛中心的距离走向外海色。
                // 贴着岸的地方就算水很深（堤岸、崖脚底下没有海床）也不直接掉进深蓝，留一圈近海色。
                // （11-13 的写法是浅水色统一压一半；现在浅水色本身调淡了，只在真正看得到底的地方出现。）
                float depthT = saturate(thickness / max(0.01, _DepthRange));
                float nearT = saturate(i.shore);
                float deepness = depthT * lerp(0.60, 1.0, smoothstep(0.15, 1.0, nearT));
                float3 midCol = lerp(_ShallowColor.rgb, _DeepColor.rgb, 0.55);
                float3 col = lerp(_ShallowColor.rgb, midCol, smoothstep(0.0, 0.45, deepness));
                col = lerp(col, _DeepColor.rgb, smoothstep(0.40, 1.0, deepness));
                col = lerp(col, _FarColor.rgb, smoothstep(55.0, 170.0, length(p)) * depthT);
                col += (N.x + N.z) * 0.03;
                col += nB * 0.02;

                float sd = i.shore * _ShoreRange;   // 离岸大约多少米（9 封顶）
                // 2026-11-16 PL-14 收尾：涨到 L2–L4（海面 0.8 m 以上）时港湾、浅滩、街面都泡在水里，
                // 顶点色里的"离岸距离"还是按原来的海岸线算的，浪线和碎沫在街面、内港上画得又碎又白。
                // hi：0.8 m 起逐渐生效，1.6 m（L3）以上满档——浪线变稀、变窄、变淡，贴岸碎沫也收一点。
                float hi = smoothstep(0.8, 1.6, _TideWaterY);
                // 碎沫的花边：两层细噪声各自漂
                float lace = vnoise(p * 2.3 + float2(t * 0.21, -t * 0.17)) * 0.6 + vnoise(p * 5.7 - float2(t * 0.30, t * 0.26)) * 0.4;

                // 礁石、堤岸、木桩入水的地方：贴着一圈窄窄的白沫，宽窄跟着细浪一呼一吸，被花边咬得断断续续。
                // 用竖直方向的水深（视线斜着穿过水时厚度偏大，按视线的俯角折回来），斜着看白边不会变宽。
                float vdepth = thickness * max(0.35, V.y);
                float breath = 0.75 + 0.45 * sin(t * 1.3 + nA * 6.2832);
                // 白沫只出现在水深 3～11 厘米以内。第一版放到 30 多厘米：涨到 L4 时淹没的广场、草坪上只有薄薄一层水，
                // 整片都算"贴岸"，白成一片。
                float edgeW = max(0.03, _FoamDepth) * (0.35 + 0.35 * nB) * breath;
                float foam = smoothstep(0.15, 0.70, saturate(1.0 - vdepth / edgeW) * (0.50 + 0.85 * lace)) * 0.7;
                // 外面再拖一圈淡的、碎的
                foam += saturate(1.0 - abs(vdepth - edgeW * 2.4) / (edgeW * 0.9)) * smoothstep(0.50, 0.78, lace) * 0.35 * (1.0 - 0.5 * hi);
                foam *= 1.0 - 0.25 * hi;
                // 近岸浅水整体只带一点点白（11-12 是 0.5，近岸浅水全半白，礁石水下那截像裹了冰；11-13 压到 0.14，现在 0.08）
                foam += saturate(1.0 - sd / max(0.1, _FoamWidth)) * (1.0 - depthT) * 0.08;

                // 推岸的浪：两三道，从离岸六七米的地方生出来往岸上推，越近岸越淡。
                // 浪线被低频噪声推得前后错开（不是等距的同心圈）；朝岸的前缘是实的，后面拖着越来越碎的沫；沿岸一段有、一段没有。
                // （11-13 的写法是 sin 等距白条，太规整。）
                float warp = (vnoise(p * 0.10 + float2(t * 0.030, -t * 0.020)) - 0.5) * 3.0 + (vnoise(p * 0.33 + 5.1) - 0.5) * 0.9;
                float w = frac((sd + warp) * 0.42 + t * 0.14);   // 波长约 2.4 米，7 秒推过一个波长
                // 高潮位：浪峰后的拖尾从 0.48 收到 0.30，碎沫门槛抬高（更少、更成片），只留实的前缘
                float crest = smoothstep(0.0, 0.10, w) * (1.0 - smoothstep(0.10, lerp(0.48, 0.30, hi), w));
                float torn = smoothstep(0.22 + 0.45 * w + 0.12 * hi, 0.50 + 0.45 * w + 0.12 * hi, lace + crest * 0.15);   // 越往后要的噪声值越高，所以越碎
                // 水深不到 10 厘米的地方不画浪线、35 厘米以上才画足：顶点色里的离岸距离不分岸里岸外，
                // 涨潮淹到镇上以后，岸线往里几米的广场、草坪上那层薄水也会被画上浪线，白花花一片。
                // 高潮位：只在 4 m 以内推岸、水深 0.6 m 以上才画足（淹没的街面那层水不画浪线），沿岸断开得更多
                float reach = saturate(1.0 - sd / lerp(6.5, 4.0, hi)) * smoothstep(0.10, 0.55, sd) * smoothstep(lerp(0.10, 0.25, hi), lerp(0.35, 0.60, hi), vdepth);
                float seg = smoothstep(0.30 + 0.12 * hi, 0.60 + 0.12 * hi, nA * 0.55 + nB * 0.45);
                // 浪线收窄、压淡过一次（拖尾 0.62 → 0.48，强度 0.62 → 0.48）：涨潮后岸线内外全是深水，第一版的浪线糊成大理石花纹
                foam = saturate(foam + crest * torn * reach * seg * lerp(0.48, 0.30, hi));
                col = lerp(col, _FoamColor.rgb, foam);
                // 涟漪画在泡沫之上：浅水本来就偏白，靠浪谷那道暗线把亮线托出来
                col = lerp(col, _DeepColor.rgb * 0.75, trough * 0.6);
                col = lerp(col, float3(1.0, 1.0, 1.0), ring * 0.95);
                foam = max(foam, ring);

                // 2026-11-12 美术改版：斜着看海面时映出天色（黄昏的粉橙），正着往下看还是水色
                float fresnel = pow(1.0 - saturate(dot(N, V)), 5.0);
                col = lerp(col, _SkyColor.rgb, fresnel * _SkyReflect * (1.0 - foam));

                // 主光高光
                Light mainLight = GetMainLight();
                float3 H = normalize(normalize(mainLight.direction) + V);
                float spec = pow(saturate(dot(N, H)), 48.0) * _SpecStrength;
                col += spec * mainLight.color;
                // 细碎的波光：只在朝太阳的那一片海面上闪
                float glint = pow(saturate(dot(N, H)), 220.0) * step(0.62, nA * nB * 2.2);
                col += glint * mainLight.color * 1.2;
                col = MixFog(col, i.fogFactor);

                // 2026-11-13：浅水太透时礁石水下那截发白，像裹了一圈冰。
                // 深水的不透明度锁在 0.97 以上（材质里调低也不行）：外海底下没有海床，再透就露出天空盒的颜色，整片海发紫。
                float alpha = lerp(0.26, max(_Alpha, 0.97), smoothstep(0.0, 0.6, depthT));
                alpha = saturate(alpha + foam * 0.35 + ring * 0.6 + trough * 0.4);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
