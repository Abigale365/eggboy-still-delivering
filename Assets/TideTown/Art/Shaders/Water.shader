// WP-08/P2 海面：URP 自定义着色器。
// 顶点色 R = 离岸距离，画岸边泡沫；深度纹理做浅水/深水过渡；两层噪声法线反向滚动；顶点微浪。
Shader "TideTown/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.33, 0.72, 0.72, 1)
        _DeepColor ("Deep", Color) = (0.10, 0.33, 0.47, 1)
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
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
            CBUFFER_END

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
                float phase = _Time.y * _WaveSpeed;
                wp.y += (sin(wp.x * 0.55 + phase * 1.1) + sin(wp.z * 0.68 - phase * 0.8)) * amp;
                o.positionCS = TransformWorldToHClip(wp);
                o.worldPos = wp;
                o.shore = v.color.r;
                o.noise = v.color.g;
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
                float3 N = normalize(float3(grad.x, 1.0, grad.y));

                // 浅水到深水：用场景深度和水平面的深度差，而不是只看离岸距离
                float depthT = saturate(thickness / max(0.01, _DepthRange));
                float3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthT);
                col += (N.x + N.z) * 0.03;
                col += nB * 0.02;

                // 岸线泡沫：深度差贴岸 + 顶点色 R 的离岸距离，两路一起驱动
                float foam = saturate(1.0 - thickness / max(0.05, _FoamDepth));
                foam *= 0.55 + 0.45 * nA;
                foam += saturate(1.0 - i.shore * _ShoreRange / max(0.1, _FoamWidth)) * (1.0 - depthT) * 0.5;
                foam = saturate(foam);
                col = lerp(col, _FoamColor.rgb, foam);

                // 主光高光
                Light mainLight = GetMainLight();
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 H = normalize(normalize(mainLight.direction) + V);
                float spec = pow(saturate(dot(N, H)), 48.0) * _SpecStrength;
                col += spec * mainLight.color;

                float alpha = lerp(0.32, _Alpha, depthT);
                alpha = saturate(alpha + foam * 0.35);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
