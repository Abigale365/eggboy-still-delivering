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
            float _TideWaterY;        // 全局：现在的海面高度（TownLighting 每帧设）
            // 2026-11-14 痕迹图（TideFeel.cs / TideTrail.shader）：走过的地方草被踩倒，顺着走的方向趴下，几秒后慢慢立起来
            TEXTURE2D(_TideTrailSpring); SAMPLER(sampler_TideTrailSpring);
            TEXTURE2D(_TideTrailAux);
            float4 _TideTrail;        // x = 1 / 贴图盖的米数，yz = 主角的 xz，w = 1 有效
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
                half trod : TEXCOORD4;    // 被踩倒的程度：倒下的草叶面朝天，整片发亮发浅，痕迹看得出来
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
                float rootY = wp.y - 0.21 * h;   // 这片草叶的根大约在哪（草叶平均 21 厘米高）
                float2 rootXZ = wp.xz;

                // 风：一道道亮带从草面上扫过去
                float wave = sin(t * 1.4 - wp.x * 0.55 - wp.z * 0.42) * 0.5 + 0.5;
                float small = sin(t * 3.1 + wp.x * 1.9 + wp.z * 2.3 + v.color.g * 6.28);
                float bend = (wave * wave * 0.85 + small * 0.15 + 0.15) * _Bend * _TideWind;
                wp.xz += float2(0.8, 0.6) * bend * h * h;

                wp.y -= bend * 0.35 * h * h;

                // 走过的痕迹：草叶绕着根弯下去（越靠叶尖弯得越多，像被压弯的弧），不是整片平移
                float trod = 0.0;
                if (_TideTrail.w > 0.5)
                {
                    float2 uv = wp.xz * _TideTrail.x;
                    float4 sp = SAMPLE_TEXTURE2D_LOD(_TideTrailSpring, sampler_TideTrailSpring, uv, 0);
                    float4 ax = SAMPLE_TEXTURE2D_LOD(_TideTrailAux, sampler_TideTrailSpring, uv, 0);
                    float focus = saturate((11.0 - length(wp.xz - _TideTrail.yz)) * 0.5);
                    float dy = wp.y - ax.z;
                    float gate = saturate((dy + 0.3) / 0.2) * saturate((0.9 - dy) / 0.3) * focus;
                    // 慢痕管"趴着"，弹簧管刚松开时那一下回弹；每丛草快慢差一点
                    float2 b = (ax.xy * 1.95 + sp.xy * 0.35 + sp.zw * ((v.color.g - 0.5) * 0.05)) * gate;   // 2026-11-16：慢痕 1.7 → 1.95，小黑走过的浅痕也趴得下去
                    float m = length(b);
                    if (m > 1e-4)
                    {
                        float ang = min(m, 1.25) * 0.98 * h;  // 踩实了叶尖倒到 70 度：再平就贴在地上看不见了，留一点才看得出往哪边倒
                        float len = 0.21 * h;                 // 草叶平均 21 厘米高
                        wp.xz += b / m * len * sin(ang);
                        wp.y -= len * (1.0 - cos(ang));
                        trod = saturate(m);
                    }
                }
                o.trod = trod;

                // 2026-11-15 PL-15 涨潮以后水下不长草：按草根的高度整片收（rootY 是被风吹、被踩之前估的）。
                // 根高出水面 10 厘米以上的不动；根在水面以下的整片缩回根上、再往地里沉 30 厘米（叶长是估的，
                // 缩不干净的那一小截藏进地面底下）；中间那一圈是矮下去的草，岸边不是一刀切齐，每丛再错开几厘米。
                // 只在顶点里做、片元里不裁：草是全场最密的东西，片元里加 clip 会丢掉提前深度剔除，帧率吃亏。
                // 这个着色器只有颜色通道，草本来就不投影、不进深度图，所以没有别的通道要跟着改。
                float keep = saturate((rootY - _TideWaterY - (v.color.g - 0.5) * 0.06) / 0.10);
                wp.y = lerp(min(rootY, wp.y) - 0.30 * (1.0 - keep), wp.y, keep);
                wp.xz = lerp(rootXZ, wp.xz, keep);

                // 2026-11-16 C4：远处草叶提前、平滑地收掉。离镜头 42 m 起草叶往根上缩，58 m 以外缩成一个点（退化三角形不出片元），
                // 远处那一大片本来就只是地面颜色上的细碎噪点，省掉最密那层东西的片元开销。地面本身是同色的草皮，看不出断层。
                float camD = distance(_WorldSpaceCameraPos.xz, rootXZ);
                float shrink = 1.0 - smoothstep(42.0, 58.0, camD);
                wp = lerp(float3(rootXZ.x, rootY, rootXZ.y), wp, shrink);

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
                h = lerp(h, 1.0, i.trod * 0.55);     // 倒下的草看到的是整片叶面，不再是根深尖浅
                // 一片片深浅不同的草斑（大尺度），每丛再有一点自己的色差
                half patch = vnoise(i.positionWS.xz * 0.16) * 0.7 + vnoise(i.positionWS.xz * 0.47 + 7.3) * 0.3;
                half3 tip = lerp(_TipColor.rgb, _DryColor.rgb, smoothstep(0.52, 0.80, patch) * 0.55);
                half3 albedo = lerp(_BaseColor.rgb, tip, h * h * 0.85 + h * 0.15);
                albedo *= 0.90 + 0.20 * i.color.b;
                albedo *= 1.0 + i.gust * h * 0.16;   // 风扫过的地方叶尖翻亮
                // 踩倒的草叶面朝天，比立着的亮一点。2026-11-16：0.14 → 0.24，再往干草色偏一点，压痕在黄昏光下也读得出来
                albedo = lerp(albedo, _DryColor.rgb * 0.9, i.trod * 0.18);
                albedo *= 1.0 + i.trod * 0.24;

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
