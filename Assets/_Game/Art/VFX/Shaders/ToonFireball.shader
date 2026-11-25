// 카툰 폭발 불덩이(FX_ToonExplosion 의 Fireball* 파티클) — 2026-11-12 레퍼런스 영상 재현.
//
// 구체 메시 파티클 한 알 = 불덩이 혹 하나. 텍스처 없이 3D 값 노이즈로 색을 **계단**으로 나눈다(카툰):
//   연노랑 심 → 노랑 → 주황 → 붉은 가장자리 → 검붉은 연기(2톤 명암 + 혹 윤곽).
// 열(heat)이 수명 따라 식으면 연기로 바뀐다. 어디부터 식나(영상 0.4~1.4초):
//   · 덩어리 **위쪽**부터(높이 항 _HeightHeat — 혹 하나가 아니라 불덩이 전체의 높이, 바닥에서 m)
//   · 혹의 **윗면** · 테두리부터 — 마지막엔 혹 **아랫면**에 초승달 같은 불빛만 남는다(_DownHeat, _FacingHeat)
//   · 무늬(3D 노이즈)가 높은 곳부터 — 동글동글한 검은 구멍
// 그 뒤 디졸브(dissolve)가 구멍을 뚫어 연기가 실처럼 갈라지며 사라진다 — **아래부터**(_DissolveLow, 영상 1.5~2.6초).
//
// 파티클 Custom Vertex Streams(ToonExplosionSetup.Fireball 이 맞춘다 — 순서를 바꾸면 TEXCOORD 자리가 어긋난다):
//   Position, Normal, Color, Custom1.xy + StableRandom.x + SizeX → TEXCOORD0 (x 열, y 디졸브, z 무작위, w 입자 크기)
//   Center.xyz                                                  → TEXCOORD1
// 노이즈 좌표 = (정점 − 입자 중심) / 크기 — 입자가 움직이고 커져도 무늬가 혹에 붙어 있다. StableRandom 이 알마다 무늬를 바꾼다.
// 높이 = (월드 y − 바닥 y) / 배율. 메시 파티클 정점은 Local 시뮬레이션이어도 **월드 좌표**라(11-12 렌더로 확인) 바닥 · 배율은
// 루트의 ToonExplosionAnchor 가 MaterialPropertyBlock _Ground(xyz 루트 월드 위치, w 배율)로 넣는다.
//
// 큐 2990(밤 어둠 막 2900 뒤 — 불이 어둠에 안 묻힌다, 다른 투명 3000 보다 먼저) · 깊이 씀 · 알파 클립 — 혹끼리 앞뒤가 깊이로 맞는다.
Shader "ZombieOverrun/ToonFireball"
{
    Properties
    {
        [Header(Fire)]
        _ColHot    ("심 (가장 뜨거운 곳)", Color) = (1.0, 0.88, 0.56, 1)
        _ColYellow ("노랑", Color) = (0.98, 0.64, 0.29, 1)
        _ColOrange ("주황", Color) = (0.98, 0.44, 0.15, 1)
        _ColRim    ("붉은 가장자리 (불 → 연기 경계)", Color) = (0.66, 0.2, 0.12, 1)
        _FireBoost ("불 밝기 (1 넘으면 블룸)", Range(0.5, 3)) = 1.12

        [Header(Smoke)]
        _ColSmokeLit   ("연기 밝은 면", Color) = (0.17, 0.065, 0.045, 1)
        _ColSmokeShade ("연기 그늘", Color) = (0.09, 0.022, 0.013, 1)
        _ColSmokeHot   ("갓 식은 연기 (불빛 받은)", Color) = (0.4, 0.15, 0.11, 1)
        _SmokeHotHeat  ("갓 식은 연기가 남는 열 폭", Range(0.05, 2)) = 0.5
        _SmokeRim      ("연기 테두리 어둡게 (혹 윤곽)", Range(0, 1)) = 0.45
        _ColDissolveEdge ("디졸브 가장자리", Color) = (0.05, 0.012, 0.008, 1)
        _LightDir ("연기 빛 방향 (월드)", Vector) = (-0.35, 0.8, -0.45, 0)

        [Header(Shape)]
        _NoiseScale  ("무늬 밀도", Float) = 3.0
        _NoiseAmount ("무늬가 열을 흔드는 양", Range(0, 2)) = 0.95
        _DownHeat    ("아랫면 열 (혹 아래 초승달 불빛)", Range(0, 1)) = 0.5
        _FacingHeat  ("정면 열 (혹 가운데가 밝다)", Range(0, 1)) = 0.22
        _HeightHeat  ("높이 열 (덩어리 아래가 오래 빛난다 — 식을수록 세진다)", Range(0, 2)) = 0.4
        _HeightRange ("덩어리 높이 (m, 배율 1)", Float) = 3.0
        _Displace    ("표면 울퉁불퉁", Range(0, 0.5)) = 0.26

        [Header(Bands)]
        _Band1 ("문턱 심", Float) = 1.06
        _Band2 ("문턱 노랑", Float) = 0.86
        _Band3 ("문턱 주황", Float) = 0.56
        _Band4 ("문턱 가장자리 (아래는 연기)", Float) = 0.46

        [Header(Dissolve)]
        _DissolveScale ("디졸브 무늬 밀도", Float) = 2.4
        _DissolveEdge  ("디졸브 가장자리 폭", Range(0, 0.3)) = 0.06
        _DissolveLow   ("아래부터 사라지기", Range(0, 1)) = 0.35

        [HideInInspector] _Ground ("바닥 (런타임 — ToonExplosionAnchor)", Vector) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "ToonFireball"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColHot, _ColYellow, _ColOrange, _ColRim;
                half  _FireBoost;
                half4 _ColSmokeLit, _ColSmokeShade, _ColSmokeHot, _ColDissolveEdge;
                half  _SmokeHotHeat, _SmokeRim;
                float4 _LightDir;
                float _NoiseScale, _NoiseAmount, _DownHeat, _FacingHeat, _HeightHeat, _HeightRange, _Displace;
                float _Band1, _Band2, _Band3, _Band4;
                float _DissolveScale, _DissolveEdge, _DissolveLow;
                float4 _Ground;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                float4 uv0        : TEXCOORD0;   // x 열, y 디졸브, z 무작위, w 입자 크기
                float4 uv1        : TEXCOORD1;   // xyz 입자 중심
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 local       : TEXCOORD2;  // xyz 구체 좌표(반지름 0.5), w 시드
                float3 data        : TEXCOORD3;  // 열, 디졸브, 바닥에서 높이(m)
                half4  color       : COLOR;
            };

            // 값 노이즈(텍스처 없이) — BurnDissolveCommon 과 같은 해시
            float TfHash(float3 p) { p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
            float TfNoise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = TfHash(i),                     n100 = TfHash(i + float3(1, 0, 0));
                float n010 = TfHash(i + float3(0, 1, 0)),   n110 = TfHash(i + float3(1, 1, 0));
                float n001 = TfHash(i + float3(0, 0, 1)),   n101 = TfHash(i + float3(1, 0, 1));
                float n011 = TfHash(i + float3(0, 1, 1)),   n111 = TfHash(i + float3(1, 1, 1));
                float nx00 = lerp(n000, n100, f.x), nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x), nx11 = lerp(n011, n111, f.x);
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }
            // 겹친 값 노이즈는 0.2~0.8 에 몰린다 — 0~1 로 편다
            float Spread(float n) { return saturate((n - 0.2) / 0.6); }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float size = max(IN.uv0.w, 1e-4);
                float3 local = (IN.positionOS.xyz - IN.uv1.xyz) / size;
                float seed = IN.uv0.z * 61.7;
                float lump = TfNoise(local * (_NoiseScale * 0.7) + seed + 3.7) - 0.5;
                float3 posOS = IN.positionOS.xyz + normalize(IN.normalOS) * lump * _Displace * size;
                OUT.positionHCS = TransformObjectToHClip(posOS);
                OUT.positionWS = TransformObjectToWorld(posOS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.local = float4(local, seed);
                float3 baseWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.data = float3(IN.uv0.xy, (baseWS.y - _Ground.y) / max(_Ground.w, 1e-3));
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float heat = IN.data.x;
                float diss = IN.data.y;
                float low = saturate(1.0 - IN.data.z / _HeightRange);   // 1 = 바닥, 0 = 덩어리 꼭대기

                // ── 디졸브(먼저 — 깎일 픽셀은 색 계산을 안 한다). 시작하면 아래가 더 빨리
                float3 dp = IN.local.xyz * _DissolveScale + IN.local.w * 1.7 + 11.3;
                float m = Spread(0.6 * TfNoise(dp) + 0.4 * TfNoise(dp * 2.4 + 2.1));
                float thr = diss * 1.06 - 0.03 + _DissolveLow * low * saturate(diss * 3.0) + (1.0 - IN.color.a);
                clip(m - thr);

                // ── 열 마당: 식은 정도 + 덩어리 높이 · 혹 아랫면 · 정면 보정 − 무늬
                float3 np = IN.local.xyz * _NoiseScale + IN.local.w;
                float n = Spread(0.55 * TfNoise(np) + 0.3 * TfNoise(np * 2.13 + 7.1) + 0.15 * TfNoise(np * 4.7 + 3.3));
                float facing = saturate(dot(N, V));
                // 높이 · 아랫면 항은 식을수록 세진다 — 갓 터졌을 땐 위아래 없이 고루 뜨겁고 검은 곳은 무늬 구멍뿐(영상 0.2~0.4초),
                // 식으며 덩어리 위부터 어두워지고 혹 아랫면에 초승달 불빛이 남는다(영상 0.8~1.4초)
                float cool = saturate(1.4 - heat);
                float f = heat + (low - 0.5) * _HeightHeat * cool + (-N.y) * _DownHeat * cool + (facing - 0.5) * _FacingHeat - (n - 0.5) * _NoiseAmount;

                float aa = fwidth(f) * 0.75 + 1e-4;
                float b1 = smoothstep(_Band1 - aa, _Band1 + aa, f);
                float b2 = smoothstep(_Band2 - aa, _Band2 + aa, f);
                float b3 = smoothstep(_Band3 - aa, _Band3 + aa, f);
                float b4 = smoothstep(_Band4 - aa, _Band4 + aa, f);

                half3 fire = lerp(_ColRim.rgb, _ColOrange.rgb, b3);
                fire = lerp(fire, _ColYellow.rgb, b2);
                fire = lerp(fire, _ColHot.rgb, b1);
                fire *= _FireBoost;

                // 연기 — 2톤 명암(빛 방향 고정, 무늬로 경계를 흔든다)
                float ndl = dot(N, normalize(_LightDir.xyz)) + (n - 0.5) * 0.9;
                float lit = smoothstep(0.08 - 0.03, 0.08 + 0.03, ndl);
                half3 smoke = lerp(_ColSmokeShade.rgb, _ColSmokeLit.rgb, lit);
                // 갓 식은 연기는 아직 불빛을 받아 붉은 갈색(영상 0.4~1초) — 열이 내려가며 검붉게
                smoke = lerp(smoke, _ColSmokeHot.rgb * lerp(0.75, 1.0, lit), saturate((heat - 0.05) / _SmokeHotHeat));
                // 혹 윤곽 — 테두리(화면 쪽으로 기운 면)를 한 단 어둡게. 영상의 연기는 혹마다 가장자리가 짙어 덩어리가 읽힌다
                smoke *= 1.0 - _SmokeRim * (1.0 - smoothstep(0.22, 0.3, facing));

                half3 col = lerp(smoke, fire, b4);

                // 디졸브 가장자리 — 더 어두운 실(연기가 갈라질 때 끝이 말린 것처럼)
                float edge = (1.0 - smoothstep(0.0, _DissolveEdge, m - thr)) * step(0.001, diss);
                col = lerp(col, _ColDissolveEdge.rgb, edge * (1.0 - b4));

                col *= IN.color.rgb;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
