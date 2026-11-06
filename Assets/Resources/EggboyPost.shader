Shader "EGGBOY/Post"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Bloom ("Bloom", Float) = 0.55
        _Vignette ("Vignette", Float) = 0.35
        _Warm ("Warm", Float) = 0.05
        _Threshold ("Bloom Threshold", Float) = 1.05
        _Saturation ("Saturation", Float) = 1.12
        _Contrast ("Contrast", Float) = 1.04
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Bloom, _Vignette, _Warm, _Threshold, _Saturation, _Contrast;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                fixed3 c = tex2D(_MainTex, uv).rgb;

                // 简易亮度 bloom：四个方向的少量采样
                float2 t = _MainTex_TexelSize.xy;
                fixed3 b = 0;
                b += max(0, tex2D(_MainTex, uv + float2(3,0) * t).rgb - _Threshold);
                b += max(0, tex2D(_MainTex, uv + float2(-3,0) * t).rgb - _Threshold);
                b += max(0, tex2D(_MainTex, uv + float2(0,3) * t).rgb - _Threshold);
                b += max(0, tex2D(_MainTex, uv + float2(0,-3) * t).rgb - _Threshold);
                b += max(0, tex2D(_MainTex, uv + float2(6,6) * t).rgb - _Threshold);
                b += max(0, tex2D(_MainTex, uv + float2(-6,6) * t).rgb - _Threshold);
                b += max(0, tex2D(_MainTex, uv + float2(6,-6) * t).rgb - _Threshold);
                b += max(0, tex2D(_MainTex, uv + float2(-6,-6) * t).rgb - _Threshold);
                c += b * _Bloom * 0.125;

                // 暖色分级
                c.r += _Warm;
                c.b -= _Warm * 0.45;

                // ACES 色调映射，避免线性空间过曝成荧光色
                c = max(0, c);
                float3 a = c * (2.51 * c + 0.03);
                float3 b2 = c * (2.43 * c + 0.03) + 0.14;
                c = saturate(a / b2);

                // 轻微提高饱和度和对比，靠近网页版的色彩
                float lum = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(float3(lum, lum, lum), c, _Saturation);
                c = saturate((c - 0.5) * _Contrast + 0.5);

                // 暗角
                float d = distance(uv, float2(0.5, 0.5));
                c *= 1.0 - _Vignette * smoothstep(0.35, 0.85, d);

                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
