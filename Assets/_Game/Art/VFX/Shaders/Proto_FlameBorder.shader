// 최종 보스 울타리의 불꽃 테두리 (ProtoBossArena).
// 2026-11-13 사용자: 그라인드 서바이버즈 보스전처럼 "아웃라인이 불꽃이 일렁이는" 테두리로. 예전 세로 벽(Sprites/Default 그라데이션)을 대신한다.
//
// 메시 두 장이 이 셰이더를 같이 쓰고 _Wall 로 나눈다. 둘 다 ProtoBossArena 가 울타리 선(모서리를 둥글린 사각형)을 따라 코드로 만든다.
//  · 바닥 띠(_Wall 0) — uv.x = 둘레를 따라 잰 거리(m), uv.y = 울타리 선에서 잰 거리(m, 바깥이 +).
//    제자리에서 일렁이는 빨간 불 선 + 안쪽으로 번지는 붉은 빛 + 울타리 바깥 어둠.
//  · 불꽃 카드(_Wall 1) — 선 위에 촘촘히 선 카드. 꼭짓점 넷이 같은 자리(밑변 가운데)에 있고 이 셰이더가 카메라 쪽으로 펼친다(빌보드).
//    uv = 카드 안 (가로 0~1, 높이 0~1), uv2.x = 카드마다 다른 무늬. 검붉은 큰 혀 + 밝은 빨간 작은 혀가 위로 타오른다.
//    (2판 2026-11-13 사용자 "불꽃이 옆으로 누워 있는 것 같다 — 방향만 위로". 1판은 같은 혀가 바닥 띠에 누워 바깥으로 뻗었고,
//     선 위에 이어진 세로 띠가 따로 있었다. 혀 식 · 색 · 길이 · 빠르기는 1판 그대로, 바깥 거리 대신 높이로 잰다.
//     이어진 세로 띠로 세우지 않은 까닭: 옆 벽은 선이 화면 위아래로 뻗어 띠의 옆면만 보이므로 불꽃이 다시 옆으로 누워 보인다.)
//
// 바닥 띠 노이즈는 둘레 방향 주기를 정수 칸 수(_Cells)로 맞춰, 한 바퀴 돌아 만나는 자리에 이음매가 없다. 시간 축도 주기(64칸)로 감아
// 오래 켜 둬도 입력이 커지지 않는다(모바일 정밀도). 텍스처는 안 쓴다. 옥타브는 둘뿐 — 잔결 없이 동글동글한 혹(캐주얼 그림체).
// 시간은 _FlameT(스크립트가 Time.time 을 넣는다) — 레벨업 일시정지(timeScale 0)에 다른 이펙트와 함께 멈춘다.
//
// 블렌드는 미리 곱한 알파(One, OneMinusSrcAlpha): 알파 몫은 바닥을 덮고(어둠·검붉은 혀), 알파 없는 몫은 더해져 빛난다(불 선·빛).
// 큐 3100(Transparent+100) — 밤 어둠 막(2900) 뒤라 밤에도 그대로 빛난다(BurnEdge 등 FX 와 같은 규칙). 불꽃 카드는 스크립트가 3101 로.
// 색 값은 화면에 보이는 sRGB 값이다(프로젝트가 Linear). HDR 색은 블룸 문턱을 넘어 번진다.
Shader "Proto/FlameBorder"
{
    Properties
    {
        [HDR] _HotColor   ("불 선 한가운데 (가장 뜨거운 곳)", Color) = (2.2, 0.75, 0.6, 1)
        [HDR] _LineColor  ("불 선 · 안쪽 빛", Color) = (2.2, 0.10, 0.06, 1)
        [HDR] _FlameColor ("불꽃 혀 뿌리", Color) = (1.0, 0.05, 0.03, 1)
        _TipColor   ("불꽃 혀 끝 (식은 검붉은 색)", Color) = (0.2, 0.015, 0.015, 1)
        _DarkColor  ("울타리 바깥 어둠", Color) = (0.03, 0.0, 0.0, 1)
        _OutsideDark ("바깥 어둠 세기", Range(0, 1)) = 0.55
        _CoreW     ("불 선 굵기 (m)", Range(0.01, 0.3)) = 0.09
        _Jag       ("불 선 일렁임 폭 (m)", Range(0, 0.5)) = 0.16
        _GlowIn    ("안쪽 빛 번짐 (m)", Range(0.05, 1.5)) = 0.35
        _GlowInStr ("안쪽 빛 세기", Range(0, 1)) = 0.3
        _FlameLen  ("불꽃 혀 길이 (m, 위로)", Range(0.2, 2.4)) = 1.9
        _FlameAmt  ("불꽃 혀 양", Range(0, 1)) = 0.6
        _Speed     ("일렁이는 빠르기", Range(0, 3)) = 1
        [HideInInspector] _InnerW ("바닥 띠 안쪽 폭 (m, 메시와 같은 값)", Float) = 1.0
        [HideInInspector] _OuterW ("바닥 띠 바깥 폭 (m, 메시와 같은 값)", Float) = 2.6
        [HideInInspector] _Perim  ("둘레 (m)", Float) = 80
        [HideInInspector] _Cells  ("둘레 칸 수 (정수)", Float) = 100
        [HideInInspector] _FlameT ("시간 (s)", Float) = 0
        [HideInInspector] _Reveal ("나타남 0~1", Float) = 1
        [HideInInspector] _Wall   ("0 바닥 띠, 1 불꽃 카드", Float) = 0
        [HideInInspector] _WallH  ("불꽃 카드 키 (m)", Float) = 2.0
        [HideInInspector] _CardW  ("불꽃 카드 폭 (m)", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "FlameBorder"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HotColor, _LineColor, _FlameColor, _TipColor, _DarkColor;
                float _OutsideDark, _CoreW, _Jag, _GlowIn, _GlowInStr, _FlameLen, _FlameAmt, _Speed;
                float _InnerW, _OuterW, _Perim, _Cells, _FlameT, _Reveal, _Wall, _WallH, _CardW;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.uv = v.uv;
                o.seed = v.uv2.x;
                if (_Wall > 0.5)
                {
                    // 불꽃 카드 — 밑변 가운데(꼭짓점 자리)에서 카메라 오른쪽 · 위쪽으로 펼친다. 나타나는 동안 키가 자란다
                    float3 c = TransformObjectToWorld(v.positionOS.xyz);
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    float3 up    = UNITY_MATRIX_V[1].xyz;
                    float3 wp = c + right * ((v.uv.x - 0.5) * _CardW) + up * (v.uv.y * _WallH * max(_Reveal, 0.001));
                    o.positionCS = TransformWorldToHClip(wp);
                }
                else o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            float hash12(float2 p)
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 주기 있는 값 노이즈 — 격자 좌표를 period 로 감는다(둘레 이음매 없애기 · 시간 감기)
            float pnoise(float2 p, float2 period)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float2 i0 = i - floor(i / period) * period;
                float2 i1 = i0 + 1.0;
                i1 -= floor(i1 / period) * period;
                float a = hash12(i0), b = hash12(float2(i1.x, i0.y));
                float c = hash12(float2(i0.x, i1.y)), d = hash12(i1);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 두 옥타브. 둘째는 2배 촘촘하고 주기도 2배라 주기가 그대로 맞는다
            float fbm2(float2 p, float2 period)
            {
                return pnoise(p, period) * 0.65 + pnoise(p * 2.0 + float2(0.0, 17.0), period * 2.0) * 0.35;
            }

            // 시간을 노이즈 시간 축 주기(64칸)로 감는다 — 감기는 순간에도 노이즈 값이 같아 끊김이 없다
            float Wrap(float t) { return fmod(t, 64.0); }

            half4 Ground(float2 uv, float T)
            {
                float d = uv.y;
                // 띠 밖(먼 바깥)은 고른 어둠뿐 — 노이즈 계산을 건너뛴다. 띠 끝에서 아래 계산과 같은 값이 되게 맞춰 두었다
                if (d > _OuterW - 0.01)
                {
                    float a0 = _OutsideDark * _Reveal;
                    return half4(_DarkColor.rgb * a0, a0);
                }

                float x = uv.x * (_Cells / _Perim);   // 둘레 칸 좌표 (주기 _Cells)

                // 1) 불 선 — 노이즈 y 축이 시간이라 제자리에서 일렁인다
                float j = (fbm2(float2(x, Wrap(T * 0.55)), float2(_Cells, 64.0)) - 0.5) * 2.0 * _Jag;
                float e = d - j;                        // 일렁이는 선에서 잰 거리, 바깥 +
                float ae = abs(e);
                float core = saturate(1.0 - ae / _CoreW);
                core *= sqrt(core);                     // ^1.5 — 가는 실선보다 도톰하게
                float halo = exp(-ae / (_CoreW * 2.2));
                float breathe = 0.75 + 0.5 * pnoise(float2(x * 2.0, Wrap(T * 1.7)), float2(_Cells * 2.0, 64.0));

                // 2) 안쪽 빛 — 울타리 안으로 붉게 번진다. 띠 안쪽 끝(_InnerW)에서 0 이 되게 줄여 끊긴 자국이 없다
                float glowIn = e < 0.0 ? exp(e / _GlowIn) : 0.0;
                glowIn *= saturate((d + _InnerW) / (_InnerW * 0.6));

                // (불꽃 혀는 2판에서 불꽃 카드로 옮겼다 — 위로 타오른다. Card() 참고)

                // 3) 바깥 어둠 — 선에서 멀어질수록 진해져 띠 끝 전에 _OutsideDark 에 닿는다
                float dark = _OutsideDark * smoothstep(-0.05, 0.7, e);

                // 합치기(미리 곱한 알파): 어둠(덮기) → 빛(더하기)
                float3 col = _DarkColor.rgb * dark;
                float  a   = dark;
                float3 emit = _LineColor.rgb * (core + halo * 0.3) * breathe
                            + _HotColor.rgb * core * core * core * breathe
                            + _LineColor.rgb * glowIn * _GlowInStr;
                col += emit;
                return half4(col * _Reveal, a * _Reveal);
            }

            // 불꽃 카드 — 1판 바닥 혀와 같은 식을 "선에서 바깥으로 잰 거리" 대신 "높이"로 돌렸다(그래서 위로 타오른다).
            //   큰 혀 — 가로 0.8m 굽이, 뿌리만 붉고 금방 검붉게 식는다(덮는 몫).
            //   작은 혀 — 가로 0.4m 굽이, 아래쪽(_FlameLen 의 35%) 밝은 빨강(더하는 몫).
            // 값 노이즈는 0.5 근처에 몰려 혀 길이가 고르게 짧아지므로 대비를 1.7배 늘려 긴 혀·짧은 혀가 갈리게 한다.
            // 카드 가장자리로 갈수록 문턱을 올려(env) 카드 네모가 드러나지 않는다. 노이즈가 위로 흐른다(약 1.1m/s × _Speed).
            half4 Card(float2 uv, float seed, float T)
            {
                float cx = uv.x * 2.0 - 1.0;             // -1 ~ 1
                float xm = cx * _CardW * 0.5;            // 카드 가운데에서 가로 m
                float h  = uv.y * _WallH;                // 높이 m (나타나는 동안엔 카드가 줄어 있어 혀도 같이 줄어든다)
                float2 per = float2(1024.0, 64.0);
                float sx = seed * 512.0;                 // 카드마다 다른 무늬
                float env = 1.0 - cx * cx;
                // 밑동을 둥글게 — 카드 밑변이 가로줄로 끊겨 보이면 옆 벽(카드가 화면 위아래로 줄 선다)에서 계단처럼 쌓인다
                float baseCut = (1.0 - smoothstep(0.0, 0.3, h)) * cx * cx * 1.2;

                float warp = pnoise(float2(xm * 2.5 + sx, Wrap(T * 0.8) + h * 0.7), per) - 0.5;
                float2 q = float2(xm * 1.25 + warp * 0.9 + sx, h * 0.9 - Wrap(T * 1.0));
                float F = saturate((fbm2(q, per) - 0.5) * 1.7 + 0.5);
                float reach = saturate(h / _FlameLen);   // 0 = 뿌리, 1 = 혀가 닿는 끝
                float m = F - reach * 0.75 - reach * reach * 0.45 - (1.0 - _FlameAmt) * 0.6 + 0.05 - (1.0 - env) * 0.9 - baseCut;
                float tongue = smoothstep(0.0, 0.2, m);

                float2 q2 = float2(xm * 2.5 - warp * 0.8 + sx * 1.37, h * 2.2 - Wrap(T * 2.6));
                float F2 = saturate((fbm2(q2, per) - 0.5) * 1.7 + 0.5);
                float reach2 = saturate(h / (_FlameLen * 0.35));
                float lick = smoothstep(0.0, 0.2, F2 - reach2 - (1.0 - env) * 0.9 - baseCut);

                float root = exp(-h / 0.12) * env * env; // 땅에 닿은 뿌리 — 바닥 불 선과 이어진다
                float foot = smoothstep(0.0, 0.08, h);   // 밑변 바로 위는 살짝 비워 가로줄이 안 서게

                float  ta  = tongue * 0.92 * (1.0 - smoothstep(0.45, 1.0, reach) * 0.7) * foot;   // 떨어져 나간 끝은 옅은 연기처럼
                float3 tc  = lerp(_FlameColor.rgb * 0.6, _TipColor.rgb, smoothstep(0.05, 0.6, reach));
                float3 col = tc * ta;
                float3 emit = _FlameColor.rgb * lick * (1.0 - reach2 * 0.6) * 0.9 * foot
                            + _LineColor.rgb * root * 0.25;
                return half4((col + emit) * _Reveal, ta * _Reveal);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float T = _FlameT * _Speed;
                if (_Wall > 0.5) return Card(i.uv, i.seed, T);
                return Ground(i.uv, T);
            }
            ENDHLSL
        }
    }
}
