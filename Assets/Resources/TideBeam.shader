Shader "TideTown/LighthouseBeam"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.82, 0.55, 1)
        _Intensity ("Intensity", Float) = 1.0
        _Length ("Length", Float) = 58.0
        _Reveal ("Reveal", Range(0, 1)) = 1.0
        _R0 ("Start radius", Float) = 0.24
        _R1 ("End radius", Float) = 4.4
    }
    SubShader
    {
        Tags
        {
            "IgnoreProjector"="True"
            "Queue"="Transparent+20"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Name "LighthouseBeam"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            float4 _Color;
            float _Intensity;
            float _Length;
            float _Reveal;
            float _R0;
            float _R1;

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.posOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(posWS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = saturate(i.posOS.z / max(0.01, _Length));
                float rev = smoothstep(_Reveal, max(0.0, _Reveal - 0.12), t);
                float along = pow(1.0 - t, 1.55) * smoothstep(0.0, 0.04, t);
                float ndv = abs(dot(normalize(i.normalWS), normalize(i.viewDirWS)));
                float edge = pow(saturate(ndv), 1.6);
                float a = along * edge * rev * _Intensity * 0.42;
                return half4(_Color.rgb * a, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
