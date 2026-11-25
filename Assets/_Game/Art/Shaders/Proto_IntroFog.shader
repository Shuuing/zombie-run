// 시작 연출의 안개 (ProtoIntroFog · ProtoIntro).
//
// **설정**(사용자 2026-11-12 오후): "처음 시작할 때 포그도 좀 깔아줘. 캐릭터가 착지하면 양옆으로 사라지게 해줘".
// 같은 날 오전에 뺀 안개(Proto/IntroVeil 의 노이즈 결)와 다르다 — 그건 밤 어둠 막 '안'에 깔려 막과 같이 어두워져
// "검은 안개"로 보였다. 이 안개는 **밤 어둠 막(4060) 위 · 비행기·낙하산·캐릭터(4062) 아래(4061)** 에 그려 어둠을 안 탄다.
// 깊이를 보지 않는다(ZTest Always): 땅 위 낮게 깔린 층이 건물·나무와 만나는 단면 줄이 안 생긴다.
//
// **2판**(같은 날 저녁, 사용자가 땅에 낮게 깔린 푸른 밤안개 사진을 보이며 "이런 안개를 말한건데, 지금은 구름 텍스쳐 모양도
// 너무 도드라지게 잘보이고, 안개 처럼 이쁘게"): 1판은 팩 안개 결 텍스처(TFF_Fog_01A_D) 판 99장을 카메라 쪽으로 세워
// 솜뭉치 모양이 하나하나 보였고 색도 회백색이었다.
// → 텍스처를 안 쓴다. 땅과 나란한 넓은 층 몇 장(아래일수록 짙게)을 겹치고, 층마다 아주 크고 부드러운 값 노이즈로만 짙기를 흔든다 —
//   결은 화면 가로(카메라 오른쪽 축)로 길게 늘여 사진처럼 옆으로 흐르는 띠가 되고, 뭉치 윤곽이 안 생기게 대비를 낮게 둔다.
//   색은 사진의 안개(가장 짙은 곳 sRGB ≈ (72, 96, 128)) 같은 푸른 회색.
//
// **3판**(같은 날 저녁, 사용자: "안개가 가만히만 있는데 조금씩 움직이면서 착지하면 서서히 옅어지게"):
//  2판은 띠를 따라(길이 20m) 0.4m/s 로만 흘러 움직임이 안 보였다 → 띠를 가로질러 천천히 밀리고(층마다 속도가 조금씩 달라 겹친 띠가 미끄러진다),
//  휘는 결(워프) 자체도 시간에 따라 일렁인다(_Fog_Flow).
// **4판**(사용자: "안개가 안 움직이던데, 다시 확인해줘"): 3판 속도(띠 따라 1m/s · 가로질러 0.35m/s · 일렁임 0.07)는 1초에 무늬 한 주기의 5~7% 라
//  몇 초뿐인 연출 동안 카메라가 크게 내려오는 사이 땅에 붙어 가만히 있는 것처럼 보였다(임시 프로젝트 GIF 로도 6초에 겨우 보였다).
//  → 띠 따라 2m/s · 가로질러 0.8m/s · 일렁임 0.15 — 바람에 밀려가듯. 층은 모두 같은 쪽으로 흐르고 속도만 ±15% 다르다 —
//  층마다 반대로 흘리면(3판) 빨라진 만큼 몇 초 만에 서로 메워져 다시 평평한 막이 된다.
// **6판**(사용자: "구름이 너무 긴 텍스쳐여서 그런지 안개가 흐르는 느낌이 어색해. 연기가 뿌옇게 그라운드 포그 깐것처럼"):
//  길쭉한 띠(가로 20 × 세로 5m)가 옆으로 미끄러지는 게 어색했다 → 거의 둥근 결(9 × 6.5m). 결 사이를 비우지 않고 바탕에 뿌연 층(_Fog_Noise.z 0.35)을
//  깔고 그 위로 짙은 연기 덩어리가 부드럽게 오간다. 결 두 장을 서로 직각으로 흘려 섞어 모양이 계속 바뀌고(피어오르듯), 워프를 세게 해 소용돌이친다.
//  바람에 밀리는 흐름은 0.6 · 0.25m/s 로 느리게 — 빠르게 흐르는 건 띠라서 보였던 것이고 연기는 느려도 모양이 바뀌어 움직임이 보인다.
// 걷힘: 착지하면 ProtoIntroFog 가 _Fog_Part.x 를 0 → 1 로 올린다(4초). 처음부터 끝까지 고르게 서서히 옅어지고, 캐릭터 둘레(착지 지점 4~14m)가
//  먼저 얇아진다(_Fog_Part.z). 착지 지점을 지나는 화면 세로선에서 양옆으로 조금(6m) 퍼지며, 그 틈 가장자리는 노이즈로 들쭉날쭉.
//  (2판은 1.8초에 26m 를 밀려나 갈라졌다 — 절반쯤에 이미 화면 밖이었다)
//  옅어지는 곡선은 1 - smoothstep(0.1, 1, x) — 고르게(0, 1) 하니 착지 1초 만에 이미 얇아 보였다(바닥도 같이 밝아져서).
//
// 빛: 어둠 막(Proto/IntroVeil)의 빛과 같은 원(착지 지점 · 반경 · 세기, ProtoIntro 가 같은 값을 넣는다)이 안개도 밝힌다 —
// 빛 밖은 달빛 정도(_Fog_Lit.y), 빛 안은 밝게(_Fog_Lit.z). 빛이 켜지기 전엔 조금 옅게(_Fog_Lit.w — 어둠 위라 대비만으로 짙어 보인다).
//
// 정점 데이터: POSITION = 층 모서리(월드), TEXCOORD1 = (층 불투명도, 노이즈 씨앗, 걷힘 속도 배율, 흐름 방향 ±1).
// 색 값은 화면에 보이는 sRGB 값이다 — 이 프로젝트는 Linear 라 Color 속성은 sRGB 로 읽혀 리니어로 바뀐다.
Shader "Proto/IntroFog"
{
    Properties
    {
        _Color ("안개 색 · 불투명도", Color) = (0.3, 0.37, 0.48, 1)
        _Fog_Noise ("x 결 가로 주기 m, y 결 세로 주기 m, z 바탕 뿌연 짙기 0~1, w 갈라진 틈 들쭉날쭉 m", Vector) = (9, 6.5, 0.35, 3)
        _Fog_Area ("x 안 반경 m(여기까지 제 농도), y 바깥 반경 m(0)", Vector) = (24, 40, 0, 0)
        _Fog_Flow ("x 화면 가로 흐름 m/s, y 화면 세로 흐름 m/s, z 결 일렁임(소용돌이) 속도", Vector) = (0.6, 0.25, 0.12, 0)
        // ProtoIntroFog 가 넣는다
        [HideInInspector] _Fog_Part ("x 옅어짐 0~1, y 양옆으로 퍼지는 거리 m, z 캐릭터 둘레 먼저 얇아지는 정도 0~1, w 전체 불투명도", Vector) = (0, 6, 0.6, 1)
        [HideInInspector] _Fog_Light ("xyz 빛 중심(착지 지점), w 빛 반경 m", Vector) = (0, 0, 0, 3)
        [HideInInspector] _Fog_Lit ("x 빛 세기 0~1, y 빛 밖 밝기, z 빛 안 밝기, w 빛 세기 0 일 때 불투명도 배율", Vector) = (0, 0.8, 1.35, 0.8)
        [HideInInspector] _Fog_Axis ("xy 갈라지는 방향(카메라 오른쪽을 땅에 눕힌 xz)", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay+61" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "IntroFog"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Fog_Noise;
                float4 _Fog_Area;
                float4 _Fog_Flow;
                float4 _Fog_Part;
                float4 _Fog_Light;
                float4 _Fog_Lit;
                float4 _Fog_Axis;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // 층 모서리(월드 — 오브젝트는 원점·단위 회전)
                float4 layer      : TEXCOORD1;  // 층 불투명도, 씨앗, 걷힘 속도 배율, 흐름 방향
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 posXZ      : TEXCOORD0;
                float4 layer      : TEXCOORD1;
            };

            // 값 노이즈 — 좌표가 작게 유지된다(층 크기 ÷ 주기 ≈ ±3 + 씨앗). 5차 보간이라 격자 결이 안 보인다.
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            float fbm2(float2 p) { return vnoise(p) * 0.65 + vnoise(p * 2.03 + 17.1) * 0.35; }

            Varyings vert(Attributes a)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(p);
                o.posXZ = p.xz;
                o.layer = a.layer;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 rel = i.posXZ - _Fog_Light.xz;
                float2 R = normalize(_Fog_Axis.xy + float2(1e-5, 0.0));
                float2 F = float2(-R.y, R.x);
                float s = dot(rel, R), d = dot(rel, F);       // 화면 가로 · 세로(땅 위) m
                float dist = length(rel);

                // 걷힘 — 착지 지점을 지나는 세로선에서 갈라져 양옆으로 밀려난다
                float part = saturate(_Fog_Part.x);
                float e = part * part * (3.0 - 2.0 * part);
                float shift = e * _Fog_Part.y * i.layer.z;
                float side = s >= 0.0 ? 1.0 : -1.0;
                float sq = s - side * shift;

                // 짙기 — 연기처럼(6판). 거의 둥근 결 두 장을 서로 직각으로 흘려 섞는다 — 한 장만 흘리면 무늬가 미끄러질 뿐이고,
                // 두 장이 엇갈리면 덩어리가 생겼다 풀렸다 하며 피어오르듯 모양이 바뀐다. 층은 같은 쪽으로, 속도만 ±15%(더 다르면 평평한 막).
                float tt = _Time.y;
                float sp = 0.85 + 0.3 * frac(i.layer.y * 3.17);
                float2 drift  = float2(_Fog_Flow.x, _Fog_Flow.y) * (tt * sp);            // 바람에 밀림(m)
                float2 drift2 = float2(-_Fog_Flow.y, _Fog_Flow.x) * (tt * sp * 0.7);     // 직각으로 — 섞여서 모양이 바뀐다
                float2 q = float2(sq, d) / _Fog_Noise.xy + i.layer.y;
                // 소용돌이(도메인 워프) — 워프 자체도 시간에 따라 돈다
                float2 wq = q * 0.7 + float2(tt * _Fog_Flow.z, -tt * _Fog_Flow.z * 0.8);
                q += (float2(vnoise(wq + 5.2), vnoise(wq + 11.7)) - 0.5) * 1.1;
                float n1 = fbm2(q - drift / _Fog_Noise.xy);
                float n2 = fbm2(q * 1.4 - drift2 / _Fog_Noise.xy + 7.3);
                float n = n1 * 0.6 + n2 * 0.4;
                // 바탕은 뿌옇게 깔고(_Fog_Noise.z) 짙은 연기 덩어리가 그 위로 — 둘이 섞인 값은 0.5 둘레에 더 몰려 있어 좁게 자른다
                float dens = lerp(_Fog_Noise.z, 1.0, smoothstep(0.38, 0.66, n));

                // 갈라진 틈 — 가장자리를 노이즈로 들쭉날쭉하게. 걷히기 전(shift 0)엔 틈이 없다
                float en = vnoise(float2(d / 5.0, i.layer.y * 3.1 + tt * 0.15));
                float m = abs(s) - shift + (en - 0.5) * _Fog_Noise.w * 2.0 * e;
                float gap = lerp(1.0, smoothstep(-5.0, 5.0, m), saturate(shift / 3.0));

                // 둘레 — 층 끝이 안 보이게 바깥으로 옅어진다
                float area = 1.0 - smoothstep(_Fog_Area.x, _Fog_Area.y, dist);

                // 빛 — 어둠 막과 같은 빛 원
                float lit = saturate(_Fog_Lit.x) * (1.0 - smoothstep(_Fog_Light.w * 0.35, _Fog_Light.w * 1.25, dist));
                half bright = lerp(_Fog_Lit.y, _Fog_Lit.z, lit);

                half alpha = i.layer.x * _Color.a * dens * gap * area;
                alpha *= lerp(_Fog_Lit.w, 1.0, saturate(_Fog_Lit.x));
                // 서서히 옅어진다 — 처음엔 거의 그대로 머물다가(착지 직후 바닥이 밝아지며 안개가 이미 옅어 보인다) 뒤로 갈수록, 캐릭터 둘레는 먼저
                float centerFirst = 1.0 - _Fog_Part.z * e * (1.0 - smoothstep(4.0, 14.0, dist));
                alpha *= (1.0 - smoothstep(0.1, 1.0, part)) * centerFirst * _Fog_Part.w;
                bright *= lerp(0.85, 1.12, dens);   // 짙은 띠가 조금 더 밝다 — 달빛 받은 안개처럼 볼록하게
                return half4(_Color.rgb * bright, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
