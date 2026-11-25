// 경험치 젬 물고기 — 정점 변형으로 몸이 휜다 (임시).
//
// 왜 3D 모델이나 스프라이트 시트가 아닌가: 카메라가 피치 58° 고정이라 여러 각도에서 볼 일이 없고,
// 픽업 하나에 메시·머티리얼·드로우콜을 붙이는 것은 적 300마리 예산에서 아깝다. 그림 한 장을
// 가로로 쪼갠 쿼드에 얹고 길이 방향으로 사인파를 넣으면 **꼬리가 실제로 젓는다** — 아트 추가 0.
// 나무 바람 흔들림(BCM_WIND_SWAY)과 같은 방식이다.
//
// 로컬 좌표 규약 (ProtoGemPool.BuildFishQuad 가 맞춰서 굽는다):
//   x = -0.5(머리) … +0.5(꼬리),  y = -0.5 … +0.5,  z = 0
//   그림 3장 모두 왼쪽을 보고 있어서 머리가 -x 다.
Shader "Proto/GemFish"
{
    Properties
    {
        _BaseMap  ("그림", 2D) = "white" {}
        _Cutoff   ("알파 컷오프", Range(0,1)) = 0.5

        _IdleAmp  ("평상시 휨",   Range(0, 0.5)) = 0.020
        _FlopAmp  ("파닥일 때 휨", Range(0, 0.5)) = 0.170
        _IdleSpeed("평상시 속도",  Float) = 1.6
        // 느리면 '흐느적'으로 보인다. 파닥거림은 빠른 게 본질이다.
        _FlopSpeed("파닥 속도",    Float) = 34.0
        _WaveFreq ("몸을 지나는 파동 수", Float) = 0.85

        // ProtoGemPool 이 젬마다 MaterialPropertyBlock 으로 넣는다.
        _Flop     ("파닥 세기 (0~1, 런타임)", Range(0,1)) = 0
        _Phase    ("위상 (런타임)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "TransparentCutout"
            "Queue"          = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "GemFish"

            // 카메라를 향해 서는 빌보드는 뒷면이라는 개념이 없다. 컬링을 켜두면 와인딩 방향에
            // 따라 통째로 안 보인다 — 실제로 그 증상으로 한참 헤맸다(README 참조).
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

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
                float4 _BaseMap_ST;
                float  _Cutoff;
                float  _IdleAmp;
                float  _FlopAmp;
                float  _IdleSpeed;
                float  _FlopSpeed;
                float  _WaveFreq;
                float  _Flop;
                float  _Phase;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 머리는 고정하고 꼬리로 갈수록 크게 휜다. 제곱으로 떨어뜨려야 머리 쪽이
                // 뻣뻣하게 남고 꼬리만 젓는 것처럼 보인다 — 선형이면 몸 전체가 흐물거린다.
                float w = saturate(IN.positionOS.x + 0.5);
                w = w * w;

                float amp   = lerp(_IdleAmp,   _FlopAmp,   _Flop);
                float speed = lerp(_IdleSpeed, _FlopSpeed, _Flop);

                float bend = sin((IN.positionOS.x + 0.5) * 6.28318 * _WaveFreq
                                 + _Time.y * speed + _Phase);

                float3 pos = IN.positionOS.xyz;
                pos.y += bend * amp * w;

                OUT.positionHCS = TransformObjectToHClip(pos);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                clip(c.a - _Cutoff);
                return half4(c.rgb, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
