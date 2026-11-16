// 2026-11-14 痕迹图：一张铺在世界上的俯视贴图（来回两张轮换），记着"这里的草木现在被推成什么样"。
// EGGBOY、小黑走过就往上盖印，草、花丛的顶点着色器来读。TideFeel.cs 每帧各画一遍两个通道：
//   通道 0（弹簧）：RG = 枝叶现在偏了多少（世界 xz），BA = 偏移的速度。被身体顶着时紧跟身体，松开后像弹簧一样晃回去。
//   通道 1（慢痕）：RG = 踩倒的方向和程度（几秒才立起来，草用），B = 盖印时脚底的高度，A = 最近有多"热闹"。
// 贴图首尾相接地铺满世界（世界坐标 / _TrailSize 取小数），所以不用跟着主角挪；远处的重影由读的一方按离主角的距离裁掉。
Shader "Hidden/TideTown/Trail"
{
    Properties
    {
        _MainTex ("State", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        float _TrailSize;        // 一张贴图盖多少米
        float _TrailDt;
        float4 _ActorPos[4];     // xyz 脚下位置，w = 影响半径（0 = 这一格没人）
        float4 _ActorVel[4];     // xy = 速度（世界 xz，米/秒），z = 压得多实（0..1，跳起来时是 0），w = 落地冲击（只给一帧）

        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

        Varyings vert(Attributes v)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
            o.uv = v.uv;
            return o;
        }

        // 这一格现在被谁、往哪边、推多少
        void Actors(float2 uv, out float2 push, out float press, out float2 slam, out float height, out float reach)
        {
            push = 0; press = 0; slam = 0; height = 0; reach = 9.0;
            float2 wp = uv * _TrailSize;
            float best = 0.0;
            for (int i = 0; i < 4; i++)
            {
                float rad = _ActorPos[i].w;
                if (rad <= 0.0) continue;
                float2 d = wp - _ActorPos[i].xz;
                d -= _TrailSize * round(d / _TrailSize);
                float dist = length(d);
                float x = dist / rad;
                float2 dir = d / max(dist, 1e-4);
                if (x < reach) { reach = x; height = _ActorPos[i].y; }
                if (x < 1.0)
                {
                    float core = (1.0 - x) * (1.0 - x) * (1.0 + 2.0 * x);    // 中心 1，边上 0
                    float ring = saturate(x * 3.0) * core;                    // 正中心没有"往外"的方向
                    float2 v = _ActorVel[i].xy;
                    float sp = length(v);
                    float2 fwd = v / max(sp, 1e-4);
                    float drag = saturate(sp / 1.6);
                    float moving = saturate(sp / 0.25);
                    // 身体两侧的往外分开；脚下和正前方的顺着走的方向倒（慢慢走也踩得实，跑起来带得更远）
                    float2 t = (dir * ring * (0.90 - 0.25 * drag) + fwd * core * moving * (0.55 + 0.4 * drag)) * _ActorVel[i].z;
                    float m = length(t);
                    if (m > best) { best = m; push = t; }
                    press = max(press, core * _ActorVel[i].z);
                }
                // 落地：比身体大一圈的地方一起往外倒
                float hit = _ActorVel[i].w;
                if (hit > 0.0 && x < 2.3)
                {
                    float q = x / 2.3;
                    slam += dir * saturate(q * 4.0) * (1.0 - q) * (1.0 - q) * hit;
                }
            }
        }
        ENDHLSL

        Pass
        {
            Name "Spring"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            float4 frag(Varyings i) : SV_Target
            {
                float4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float2 pos = s.xy, vel = s.zw;
                float2 push, slam; float press, height, reach;
                Actors(i.uv, push, press, slam, height, reach);

                // 顶着的时候又硬又不晃（紧跟身体）；松开以后软、阻尼小（晃三四下才停）
                float held = saturate(press * 1.6);
                float k = lerp(70.0, 420.0, held);
                float c = lerp(3.3, 42.0, held);
                float dt = _TrailDt * 0.5;
                for (int n = 0; n < 2; n++)
                {
                    vel = (vel + (push - pos) * k * dt) / (1.0 + c * dt);
                    pos += vel * dt;
                }
                vel += slam * 5.5;
                return float4(clamp(pos, -2.0, 2.0), clamp(vel, -14.0, 14.0));
            }
            ENDHLSL
        }

        Pass
        {
            Name "Slow"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            float4 frag(Varyings i) : SV_Target
            {
                float4 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float2 push, slam; float press, height, reach;
                Actors(i.uv, push, press, slam, height, reach);

                float2 g = a.xy;
                if (length(push) > length(g)) g = lerp(g, push, saturate(_TrailDt * 16.0));   // 踩下去很快
                else g *= exp(-_TrailDt * 0.55);                                              // 立起来很慢
                g += slam * 0.9;
                float gm = length(g);
                if (gm > 1.25) g *= 1.25 / gm;

                float h = reach < 2.4 ? height : a.z;
                float busy = max(a.w * exp(-_TrailDt * 2.0), press);
                return float4(g, h, busy);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
