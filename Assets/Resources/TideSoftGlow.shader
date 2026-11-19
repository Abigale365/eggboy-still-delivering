Shader "TideTown/SoftGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.85, 0.6, 1)
        _Intensity ("Intensity", Float) = 1.0
        _Mode ("Mode", Float) = 0.0
        _Billboard ("Billboard", Float) = 1.0
        _Push ("Push toward camera", Float) = 0.0
    }
    SubShader
    {
        Tags
        {
            "IgnoreProjector"="True"
            "Queue"="Transparent+30"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Name "SoftGlow"
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
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float4 _Color;
            float _Intensity;
            float _Mode;
            float _Billboard;
            float _Push;

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 posWS;
                if (_Billboard > 0.5)
                {
                    float3 camRight = UNITY_MATRIX_V[0].xyz;
                    float3 camUp = UNITY_MATRIX_V[1].xyz;
                    float sx = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                    float sy = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                    posWS = centerWS + camRight * (v.positionOS.x * sx) + camUp * (v.positionOS.y * sy);
                }
                else
                {
                    posWS = TransformObjectToWorld(v.positionOS.xyz);
                }
                if (_Push != 0.0)
                {
                    float3 toCam = normalize(_WorldSpaceCameraPos - posWS);
                    posWS += toCam * _Push;
                }
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float g = 0.0;
                if (_Mode > 0.5)
                {
                    float core = exp(-r * r * 14.0);
                    float rayH = exp(-abs(p.y) * 18.0) * exp(-abs(p.x) * 2.6);
                    float rayV = exp(-abs(p.x) * 18.0) * exp(-abs(p.y) * 2.6);
                    g = (core + 0.55 * (rayH + rayV)) * saturate(1.0 - r);
                }
                else
                {
                    g = pow(saturate(1.0 - r), 2.4);
                }
                float3 col = _Color.rgb * i.color.rgb * i.color.a * g * _Intensity;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
