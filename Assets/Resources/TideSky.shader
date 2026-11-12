// 2026-11-12 美术改版：潮汐镇的黄昏天空。
// 三段渐变（地平线桃橙 → 粉紫 → 靛蓝）+ 低太阳和光晕 + 手绘感的云（底面被夕阳染粉）+ 星星 + 一弯月亮。
// _Exposure 给 LighthouseFX 的 SkyDimmer 用：灯塔点灯后天色压暗，星星跟着变亮。
Shader "TideTown/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.20, 0.22, 0.55, 1)
        _MidColor ("Mid", Color) = (0.66, 0.42, 0.72, 1)
        _HorizonColor ("Horizon", Color) = (1.0, 0.70, 0.52, 1)
        _SunColor ("Sun", Color) = (1.0, 0.86, 0.60, 1)
        _SunDir ("Sun Direction", Vector) = (0.62, 0.18, -0.64, 0)
        _CloudLight ("Cloud Lit", Color) = (1.0, 0.72, 0.74, 1)
        _CloudShade ("Cloud Shade", Color) = (0.56, 0.40, 0.68, 1)
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.48
        _CloudSpeed ("Cloud Speed", Float) = 0.004
        _MoonDir ("Moon Direction", Vector) = (0.45, 0.50, 0.74, 0)
        _MoonColor ("Moon", Color) = (1.0, 0.93, 0.74, 1)
        _Stars ("Stars", Range(0, 2)) = 0.8
        _Exposure ("Exposure", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            fixed4 _TopColor, _MidColor, _HorizonColor, _SunColor, _CloudLight, _CloudShade, _MoonColor;
            float4 _SunDir, _MoonDir;
            float _CloudCover, _CloudSpeed, _Stars, _Exposure;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

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

            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int i = 0; i < 4; i++) { v += a * vnoise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = saturate(d.y);

                // 渐变：地平线 → 中段 → 天顶
                float3 col = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.26, up));
                col = lerp(col, _TopColor.rgb, smoothstep(0.18, 0.78, up));

                // 太阳：圆盘 + 两层光晕，把附近的天空染暖
                float3 sd = normalize(_SunDir.xyz);
                float sdot = saturate(dot(d, sd));
                col += _SunColor.rgb * (pow(sdot, 6.0) * 0.30 + pow(sdot, 40.0) * 0.45);
                float disk = smoothstep(0.9990, 0.9996, sdot);

                // 星星：只在天空够暗的地方亮；天色压暗（_Exposure 小）时更明显
                float3 sp = d * 120.0;
                float3 cell = floor(sp);
                float sh = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float3 fr = frac(sp) - 0.5;
                float star = step(0.993, sh) * smoothstep(0.30, 0.0, length(fr));
                star *= 0.6 + 0.4 * sin(_Time.y * (1.5 + sh * 4.0) + sh * 40.0);
                float dark = smoothstep(0.25, 0.75, up) * (1.0 + (1.0 - saturate(_Exposure)) * 2.5);
                float3 stars = star * dark * _Stars * float3(1.0, 0.95, 0.85);

                // 月亮：两个圆盘相减得到月牙
                float3 md = normalize(_MoonDir.xyz);
                float3 mright = normalize(cross(float3(0, 1, 0), md));
                float3 mup = cross(md, mright);
                float2 mp = float2(dot(d, mright), dot(d, mup)) / 0.035;
                float inFront = step(0.0, dot(d, md));
                float moonA = smoothstep(1.0, 0.92, length(mp));
                float moonB = smoothstep(1.0, 0.92, length(mp - float2(0.42, 0.18)) / 0.88);
                float moon = saturate(moonA - moonB) * inFront;
                float moonGlow = pow(saturate(dot(d, md)), 300.0) * 0.25;

                // 云：投到头顶的一张平面上，越靠近地平线越扁越密
                float2 cuv = d.xz / (d.y + 0.16) * 1.15;
                cuv += _Time.y * _CloudSpeed * float2(1.0, 0.35);
                float n = fbm(cuv);
                float n2 = fbm(cuv * 2.6 + 9.7);
                float dens = n * 0.78 + n2 * 0.22;
                float cloud = smoothstep(1.0 - _CloudCover - 0.06, 1.0 - _CloudCover + 0.20, dens);
                cloud *= smoothstep(-0.02, 0.10, d.y) * (1.0 - smoothstep(0.55, 0.95, up) * 0.6);
                // 云的明暗：厚的地方朝上偏暗紫，边缘和朝太阳的一侧被染粉染金
                float edge = smoothstep(0.0, 0.5, cloud) * (1.0 - smoothstep(0.5, 1.0, cloud));
                float toSun = pow(sdot, 3.0);
                float3 ccol = lerp(_CloudShade.rgb, _CloudLight.rgb, saturate(0.45 + edge * 0.8 + toSun * 0.6 - n2 * 0.35));
                ccol += _SunColor.rgb * toSun * edge * 0.6;

                col += stars * (1.0 - cloud);
                col = lerp(col, _MoonColor.rgb * 1.25, moon * (1.0 - cloud * 0.8));
                col += _MoonColor.rgb * moonGlow;
                col = lerp(col, ccol, cloud * 0.92);
                col += _SunColor.rgb * disk * 1.4 * (1.0 - cloud * 0.5);

                // 地平线以下：往暗一点的地平线色收（海面远处的底色）
                float below = saturate(-d.y * 6.0);
                col = lerp(col, _HorizonColor.rgb * 0.72, below);

                col *= _Exposure;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
