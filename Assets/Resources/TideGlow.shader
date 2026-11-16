// 2026-11-12 美术改版：路灯的光晕。一张永远朝镜头的小面片，叠加混合，亮度超过 1 交给后期的泛光去晕开。
Shader "TideTown/Glow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2.2, 1.5, 0.7, 1)
        _Size ("Size (m)", Float) = 1.1
        _Core ("Core Sharpness", Float) = 3.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Size;
                float _Core;
            CBUFFER_END
            float _TideLampOn;   // 全局：TownLighting 按天色设，白天 0.25 左右（微亮），黄昏 1

            // 2026-11-15 PL-13 白天的灯只是灯罩里一点暖光：光晕比按比例算的再小一圈、再淡一档，
            // 不然大太阳底下每盏灯顶着一团光球。黄昏（1）时两个系数都是 1，和原来一样。
            float GlowDim(float on) { return on * lerp(0.6, 1.0, on); }
            float GlowSize(float on) { return lerp(0.62, 1.0, saturate(on)); }

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 viewPos = TransformWorldToView(centerWS);
                viewPos.xy += v.positionOS.xy * _Size * GlowSize(_TideLampOn);
                viewPos.z += 0.25;   // 往镜头挪一点，不被灯罩自己挡住
                o.positionCS = TransformWViewToHClip(viewPos);
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = saturate(1.0 - length(i.uv));
                float a = pow(r, _Core) + pow(r, 12.0) * 1.5;
                return half4(_Color.rgb * a * GlowDim(_TideLampOn), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
