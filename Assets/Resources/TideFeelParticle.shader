// 2026-11-14 踩水溅起的水珠、穿过花丛带起的花瓣和碎叶。形状直接在着色器里画，不用贴图。
// 粒子的顶点数据：颜色、UV，外加一个每粒固定的随机数（TEXCOORD0.z，TideFeel.cs 里打开的 StableRandomX）。
// _Shape = 0 圆水珠（半透明，中间亮）；1 花瓣 / 叶片（椭圆，边飘边翻面）。
Shader "TideTown/FeelParticle"
{
    Properties
    {
        _Shape ("Shape (0 drop, 1 petal)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "FeelParticle"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Shape;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float3 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 uv : TEXCOORD0;
                half3 light : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(wp);
                o.color = v.color;
                o.uv = v.uv;
                // 跟着天色走：白天亮，黄昏偏暖偏暗
                Light sun = GetMainLight();
                o.light = sun.color * 0.55 + SampleSH(half3(0, 1, 0)) * 0.75;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 q = i.uv.xy * 2.0 - 1.0;
                half3 col = i.color.rgb;
                half alpha;
                if (_Shape > 0.5)
                {
                    // 花瓣：一头尖的椭圆；绕长轴慢慢翻，翻到侧面时变窄，背面暗一点
                    float flip = sin(_Time.y * (4.0 + i.uv.z * 5.0) + i.uv.z * 40.0);
                    q.x /= max(0.22, abs(flip));
                    float r = length(q * float2(1.0, 0.62 + 0.18 * q.y));
                    alpha = 1.0 - smoothstep(0.82, 1.0, r);
                    col *= flip > 0.0 ? 1.0 : 0.78;
                    col *= 0.86 + 0.14 * (1.0 - r);
                }
                else
                {
                    // 水珠：圆的，中间一点高光
                    float r = length(q);
                    alpha = (1.0 - smoothstep(0.55, 1.0, r)) * 0.9;
                    col = lerp(col, half3(1, 1, 1), saturate(1.0 - r * 1.8) * 0.6);
                }
                col *= i.light;
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha * i.color.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
