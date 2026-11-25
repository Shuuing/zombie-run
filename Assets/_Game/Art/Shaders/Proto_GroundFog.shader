// 필드 바닥 안개 (GDD §15 "원경 헤이즈"의 근경 보강).
//
// 왜 파티클이 아닌가: 화염방사기(Proto_FlameCone)와 같은 이유다. 안개는 화면 절반을
// **상시** 덮는다. 반투명 쿼드 수십 장이 겹치는 파티클은 기준 기기(A54)에서 그 비용을
// 매 프레임 낸다. 수평 시트 두 장이면 오버드로가 2겹으로 고정된다.
// 카메라가 피치 58° / 요 0° 고정이라(ProtoCameraFollow) 파티클의 장점인 "어느 각도에서나
// 부피감"도 여기서는 쓰이지 않는다.
//
// 텍스처를 쓰지 않는다 — 물·화염과 같은 방침(다운로드 150MB / 메모리 900MB 예산).
//
// ── 이 셰이더가 푸는 진짜 문제: 거리 게이트 ──────────────────────────────
// 카메라 높이 15.06m, 수직 FOV 42°, 피치 58° 에서 **보이는 지면의 카메라 거리**는
//   화면 하단 15.06/sin(79°) = 15.3m
//   화면 중앙 15.06/sin(58°) = 17.8m  ← 플레이어가 정확히 여기 선다
//   화면 상단 15.06/sin(37°) = 25.0m
// 즉 화면 전체가 겨우 10m 폭의 거리 띠다. 안개를 "월드 어디에나" 깔면 플레이어 발밑도
// 똑같이 뿌예져 조준선이 죽는다. 그래서 알파를 **카메라로부터의 거리**로 게이팅해서
// 하단·중앙은 0, 상단으로 갈수록 차오르게 한다 — _DistBand 가 그 네 지점이다.
// 카메라 수치(ProtoCameraFollow.distance/pitch, Camera.fieldOfView)를 바꾸면
// 이 띠도 같이 바꿔야 한다. ProtoGroundFog 의 주석에 재계산식이 있다.
//
// ── 두 번째 게이트: 캐릭터 주변 맑은 구역 ───────────────────────────────
// 거리 게이트만으로는 안개가 **화면 아래→위** 로만 깔린다. 카메라가 캐릭터 9.4m 뒤
// 15m 위에 있어서 "카메라로부터의 거리" 와 "캐릭터로부터의 거리" 가 다르기 때문이다 —
// 캐릭터 좌우 10m 지점은 3D 거리가 20.4m 라 캐릭터와 같은 화면 높이인데도 안개가 낀다.
// 자동 사격 게임에서 사거리 안이 뿌예지면 안개는 분위기가 아니라 방해물이 된다.
// 그래서 **캐릭터를 중심으로 한 수평 반경** 게이트를 하나 더 곱한다.
// 경계 폭을 반경보다 넉넉히(6.5 > 4.5) 주는 게 중요하다 — 좁으면 캐릭터를 따라다니는
// 동그란 원으로 읽혀서 버그처럼 보인다.
Shader "Proto/GroundFog"
{
    Properties
    {
        _Color      ("안개 색",        Color) = (0.68, 0.69, 0.70, 1)
        _Alpha      ("불투명도",        Range(0, 1)) = 0.30

        _NoiseScale ("노이즈 밀도 (1/m)", Float) = 0.17
        _Drift      ("흐름 방향 (m/s)",   Vector) = (0.6, 0.35, 0, 0)
        _Coverage   ("덮는 비율",        Range(0, 1)) = 0.48
        _Softness   ("덩어리 가장자리",   Range(0.01, 1)) = 0.30

        // x=페이드인 시작, y=완전히 참, z=페이드아웃 시작, w=사라짐. 전부 카메라로부터의 m.
        _DistBand   ("가시 구간 (m)",    Vector) = (13, 19, 40, 46)

        // 캐릭터 주변 맑은 구역. ProtoGroundFog 가 매 프레임 중심을 갱신한다.
        _ClearCenter  ("맑은 구역 중심 (월드 xz)", Vector) = (0, 0, 0, 0)
        _ClearRadius  ("맑은 구역 반경 (m)",       Float)  = 4.5
        _ClearFeather ("맑은 구역 경계 폭 (m)",    Float)  = 6.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            // Transparent+100 — 물(Bitgem StylisedWater, 3000)보다 뒤에 그려야
            // 호수 위에도 안개가 얹힌다.
            "Queue"          = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GroundFog"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Alpha;
                float  _NoiseScale;
                float4 _Drift;
                float  _Coverage;
                float  _Softness;
                float4 _DistBand;
                float4 _ClearCenter;
                float  _ClearRadius;
                float  _ClearFeather;
            CBUFFER_END

            // ── 값 노이즈 ────────────────────────────────────────────────
            // FlameCone 과 같은 구현. 시간·월드 좌표가 누적되면 float 정밀도가 무너지므로
            // 격자를 감는다.
            #define FOG_NOISE_PERIOD 128.0

            float hash21(float2 p)
            {
                p = fmod(p, FOG_NOISE_PERIOD);
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 wp = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS  = wp;
                OUT.positionHCS = TransformWorldToHClip(wp);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 거리 게이트를 **노이즈보다 먼저** 계산하고 바로 clip 한다.
                // 시트는 화면을 가득 덮는 큰 쿼드라, 노이즈를 다 돌린 뒤 버리면
                // 화면 하단 절반의 셰이딩 비용을 통째로 낭비한다.
                float d = distance(IN.positionWS, _WorldSpaceCameraPos);
                float gate = smoothstep(_DistBand.x, _DistBand.y, d)
                           * (1.0 - smoothstep(_DistBand.z, _DistBand.w, d));

                // 캐릭터 주변을 비운다. 수평 거리만 본다 — 시트가 y=0.15~0.55 로 낮아
                // 높이를 섞으면 구역이 캐릭터 발밑에서 살짝 찌그러진다.
                float dc = distance(IN.positionWS.xz, _ClearCenter.xz);
                gate *= smoothstep(_ClearRadius, _ClearRadius + max(_ClearFeather, 0.01), dc);

                clip(gate - 0.004);

                // 노이즈는 **월드 좌표** 기준이다. 시트가 카메라를 따라다니므로 (ProtoGroundFog)
                // 로컬 UV 를 쓰면 안개가 플레이어에 붙어 같이 미끄러진다 — 정지해 보이려면
                // 월드에 고정돼 있어야 한다. 흐름은 아래 _Drift 만으로 준다.
                float2 p = IN.positionWS.xz * _NoiseScale;
                float2 t = _Drift.xy * _Time.y * _NoiseScale;

                // 두 옥타브를 **서로 반대로** 흘린다. 같이 흘리면 통째로 미끄러지기만 하고
                // 뭉게뭉게 피어나는 느낌이 안 난다.
                float n = vnoise(p + t) * 0.62
                        + vnoise(p * 2.31 + 7.7 - t * 1.7) * 0.38;

                float m = smoothstep(_Coverage - _Softness, _Coverage + _Softness, n);

                float a = m * _Alpha * gate;
                clip(a - 0.004);

                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
