// WP-08 海面：自定义 URP 着色器。顶点色 R 记录离岸距离，画泡沫带；高度由 WaterLevel 移动海面平面。
// 来不及时的退路：删掉泡沫项就是纯色加波浪滚动。
Shader "TideTown/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.33, 0.72, 0.72, 1)
        _DeepColor ("Deep", Color) = (0.10, 0.33, 0.47, 1)
        _FoamColor ("Foam", Color) = (0.94, 0.98, 0.96, 1)
        _ShoreRange ("Shore Range (m)", Float) = 8
        _FoamWidth ("Foam Width (m)", Float) = 1.4
        _Alpha ("Alpha", Range(0, 1)) = 0.86
        _WaveSpeed ("Wave Speed", Float) = 1
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
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float _ShoreRange;
                float _FoamWidth;
                float _Alpha;
                float _WaveSpeed;
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
                float shore : TEXCOORD1;   // 0 = 岸边，1 = _ShoreRange 米以外
                float noise : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.worldPos = TransformObjectToWorld(v.positionOS.xyz);
                o.shore = v.color.r;
                o.noise = v.color.g;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;
                float2 p = i.worldPos.xz;
                float wave = sin(p.x * 1.1 + t * 1.2) * 0.5 + sin(p.y * 1.6 - t * 0.9) * 0.5;

                float d = i.shore * _ShoreRange;
                float3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(d / _ShoreRange));
                col += wave * 0.04;

                // 岸线泡沫：离岸越近越白，边缘用噪声和时间打散
                float edge = d - (0.30 + i.noise * 0.55 + wave * 0.25);
                float foam = saturate(1.0 - edge / max(0.05, _FoamWidth));
                foam = foam * foam * (0.55 + 0.45 * i.noise);
                col = lerp(col, _FoamColor.rgb, saturate(foam));

                return half4(col, _Alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
