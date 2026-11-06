Shader "EGGBOY/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.52,0.72,0.83,1)
        _HorizonColor ("Horizon", Color) = (1.0,0.82,0.62,1)
        _SunColor ("Sun", Color) = (1.0,0.95,0.82,1)
        _SunDir ("Sun Direction", Vector) = (-0.45,0.35,-0.82,0)
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
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            fixed4 _TopColor, _HorizonColor, _SunColor;
            float4 _SunDir;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = saturate(d.y * 7.0);
                float3 col = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(h, 0.7));
                col += _HorizonColor.rgb * pow(saturate(1.0 - abs(d.y) * 2.8), 4.0) * 0.18;
                float sun = pow(saturate(dot(d, normalize(_SunDir.xyz))), 48.0);
                col += _SunColor.rgb * sun * 0.75;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
