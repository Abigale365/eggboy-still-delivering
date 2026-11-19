Shader "TideTown/BlackboardChalk"
{
    Properties
    {
        _Chalk ("Chalk", 2D) = "black" {}
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent+5"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        ZTest LEqual
        Pass
        {
            Name "BlackboardChalk"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_Chalk);
            SAMPLER(sampler_Chalk);
            float4 _Chalk_ST;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _Chalk);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_Chalk, sampler_Chalk, i.uv);
                return c;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
