// 레벨업 빛기둥 (가법 발광). 텍스처 0장 — 모양을 전부 UV 에서 계산한다.
//
// 왜 텍스처를 안 쓰는가: 빛기둥은 세로로 길고 화면 해상도에 따라 크게 늘어난다.
// 비트맵을 쓰면 코어의 얇은 선이 뭉개지거나 반대로 계단이 보인다. 절차적으로 만들면
// 어느 해상도에서도 코어가 1픽셀까지 선명하고, 색·굵기·길이를 카드마다 바꿀 수 있다.
//
// UV 규약: x 는 폭 방향 0~1 (0.5 가 중심), y 는 0=밑동 1=꼭대기.
//
// 가법(Additive) 이라 알파가 아니라 **밝기**가 곧 불투명도다. 따라서 검정은 자동으로
// 투명이고, 알파 정렬 문제가 없다. 대신 흰 배경 위에서는 잘 안 보인다 —
// 하늘이 밝은 챕터가 생기면 _Color 를 진하게 올려야 한다.
Shader "Proto/LightBeam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.0, 0.95, 0.55, 1.0)
        _CoreColor ("Core Color", Color) = (1.0, 1.0, 0.92, 1.0)

        // 코어(중앙의 날카로운 선) 폭. 폭 전체를 1 로 봤을 때의 비율.
        _CoreWidth ("Core Width", Range(0.005, 0.5)) = 0.055
        // 코어를 감싸는 부드러운 광휘의 세기.
        _GlowStrength ("Glow Strength", Range(0, 4)) = 1.0
        // 위로 갈수록 좁아지는 정도. 1 이면 꼭대기가 한 점으로 모인다(창 모양).
        _Taper ("Taper", Range(0, 1)) = 0.82
        // 꼭대기가 사라지는 구간 길이. 크면 흐릿하게 풀리고 작으면 잘린 듯 끝난다.
        _TopFade ("Top Fade", Range(0.02, 1)) = 0.45
        // 밑동이 지면에 파묻히지 않게 살짝 밝히는 구간.
        _BottomBoost ("Bottom Boost", Range(0, 3)) = 1.2

        // 세로 광선 줄무늬 — 단조로운 그라디언트에 결을 준다.
        _RayStrength ("Ray Strength", Range(0, 1)) = 0.35
        _RayCount ("Ray Count", Range(2, 40)) = 13

        // 1 이면 세로 기둥이 아니라 **중심에서 사방으로 퍼지는 원형 감쇠**로 계산한다.
        // 밑동 섬광이 이 모드를 쓴다. 기둥 셰이더의 세로 그라디언트는 단방향이라
        // 그대로 지면에 눕히면 한쪽 변에서 뚝 끊긴 사각형이 된다.
        [ToggleUI] _Radial ("Radial (ground flash)", Float) = 0

        // C# 이 매 프레임 밀어 넣는 값들.
        _Progress ("Progress (0-1)", Range(0, 1)) = 1      // 기둥이 자라 오르는 진행도
        _Intensity ("Intensity", Range(0, 4)) = 1          // 전체 밝기 (페이드아웃용)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "LightBeam"

            // One One = 가법. 뒤에 있는 것에 빛을 더하기만 한다.
            Blend One One
            // ZWrite Off + ZTest LEqual: 앞에 선 밴·나무에는 정상적으로 가려지지만
            // 기둥끼리는 깊이로 서로를 지우지 않는다.
            ZTest LEqual
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _CoreColor;
                float _CoreWidth;
                float _GlowStrength;
                float _Taper;
                float _TopFade;
                float _BottomBoost;
                float _RayStrength;
                float _RayCount;
                float _Radial;
                float _Progress;
                float _Intensity;
            CBUFFER_END

            // 원형 감쇠 — 지면에 눕는 밑동 섬광용.
            // UV 중심 (0.5, 0.5) 에서의 반경으로만 밝기를 정하므로 어느 방향으로도 대칭이다.
            // 쿼드 모서리는 반경이 1 을 넘어 0 으로 잘리고, 가법 블렌딩에서 0 은 완전 투명이라
            // 사각형 실루엣이 남지 않는다.
            half4 FragRadial(float2 uv)
            {
                float r = saturate(length(uv - 0.5) * 2.0);

                float coreMask = saturate(1.0 - r / max(_CoreWidth, 1e-4));
                float core = coreMask * coreMask;

                float glow = 1.0 - r;
                glow = glow * glow * sqrt(max(glow, 0.0)) * _GlowStrength;

                // 방사형 광선 — 각도로 줄무늬를 넣어 원판이 그냥 뭉개진 원으로 안 보이게 한다.
                if (_RayStrength > 0.0)
                {
                    float ang = atan2(uv.y - 0.5, uv.x - 0.5);
                    float rays = sin(ang * _RayCount) * 0.5 + 0.5;
                    glow *= lerp(1.0, rays, _RayStrength);
                }

                float3 rgb = (_CoreColor.rgb * core + _Color.rgb * glow) * _Intensity;
                return half4(rgb, 1.0);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                if (_Radial > 0.5) return FragRadial(uv);

                // ── 자라 오르기 ────────────────────────────────────────────
                // _Progress 를 조금 넘겨(1 + _TopFade) 밀어야 진행도 1 에서 꼭대기까지
                // 완전히 다 자란다. 안 넘기면 끝까지 가도 위쪽이 잘려 있다.
                float grow = saturate((_Progress * (1.0 + _TopFade) - uv.y) / _TopFade);
                if (grow <= 0.0) return half4(0, 0, 0, 0);

                // ── 폭 방향 모양 ──────────────────────────────────────────
                // 중심에서의 거리 0~1. 높이에 따라 나누는 값을 줄여 위로 갈수록 좁아진다.
                float width = max(0.04, 1.0 - uv.y * _Taper);
                float d = saturate(abs(uv.x - 0.5) * 2.0 / width);

                // 코어: 좁고 강한 선. 4제곱이라 가장자리가 급하게 죽는다.
                float coreMask = saturate(1.0 - d / max(_CoreWidth, 1e-4));
                float core = coreMask * coreMask * coreMask * coreMask;

                // 광휘: 넓고 부드럽게. 코어를 감싼다.
                float glow = 1.0 - d;
                glow = glow * glow * sqrt(max(glow, 0.0)) * _GlowStrength;

                // ── 세로 광선 줄무늬 ──────────────────────────────────────
                // 중심 대칭으로 넣어야 기둥이 한쪽으로 쏠려 보이지 않는다.
                float rays = sin(abs(uv.x - 0.5) * _RayCount * 6.2831853) * 0.5 + 0.5;
                glow *= lerp(1.0, rays, _RayStrength);

                // ── 높이 방향 감쇠 ────────────────────────────────────────
                // 밑동을 밝게: 지면과 만나는 자리가 가장 강해야 "여기서 솟았다"로 읽힌다.
                float bottom = 1.0 + _BottomBoost * pow(saturate(1.0 - uv.y), 3.0);
                // 꼭대기를 풀어준다.
                float top = saturate((1.0 - uv.y) / _TopFade);

                float3 rgb = _CoreColor.rgb * core + _Color.rgb * glow;
                rgb *= bottom * top * grow * _Intensity;

                // 가법이므로 알파는 안 쓴다 (Blend One One).
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
