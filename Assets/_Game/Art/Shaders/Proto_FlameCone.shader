// 화염방사기 부채꼴 (GDD §05 Cone).
//
// 왜 파티클 플립북이 아닌가: 화염방사기는 4.5m 부채꼴이 화면 중앙에 **상시** 떠 있다.
// 큰 반투명 쿼드 수십 장이 겹치는 파티클은 기준 기기(A54)에서 가장 비싼 짓이고,
// 그 비용을 매 프레임 낸다. 콘 메시 한 장이면 오버드로가 1~2겹으로 고정된다.
// 카메라가 피치 58° 고정에 요 0° 라 시점이 돌지 않으므로(ProtoCameraFollow), 파티클의
// 최대 장점인 "어느 각도에서나 부피감"이 여기서는 아예 쓰이지 않는다.
//
// 텍스처를 쓰지 않는다 — 물 셰이더(BCM_StylizedWater)와 같은 방침이다. 8×8 플립북을
// 512px 로 뽑으면 4096² 한 장인데, 다운로드 150MB / 메모리 900MB 예산에서 화염 하나가
// 그걸 먹을 이유가 없다.
//
// UV 규약: x = 부채꼴 각도(0~1), y = 진행 거리(0=총구, 1=사거리 끝). ProtoFlameCone 이 굽는다.
Shader "Proto/FlameCone"
{
    Properties
    {
        _ColorInner ("안쪽 (심)",   Color) = (1.00, 0.95, 0.62, 1)
        _ColorMid   ("중간",        Color) = (1.00, 0.55, 0.12, 1)
        _ColorOuter ("바깥 (끝)",   Color) = (0.86, 0.18, 0.06, 1)

        _Speed      ("스크롤 속도",  Float) = 2.3
        _NoiseScale ("노이즈 밀도",  Float) = 3.2
        _Erode      ("가장자리 침식", Range(0, 1)) = 0.55
        _Bands      ("색 계단 수",   Range(2, 8)) = 4
        _Alpha      ("불투명도",     Range(0, 1)) = 0.82
        _Fade       ("페이드(런타임)", Range(0, 1)) = 1
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
            Name "FlameCone"

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
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorInner;
                float4 _ColorMid;
                float4 _ColorOuter;
                float  _Speed;
                float  _NoiseScale;
                float  _Erode;
                float  _Bands;
                float  _Alpha;
                float  _Fade;
            CBUFFER_END

            // ── 값 노이즈 ────────────────────────────────────────────────
            // 시간이 누적되면 float 정밀도가 무너지므로 격자를 감는다
            // (물 셰이더의 BCM_NOISE_PERIOD 와 같은 이유).
            #define FLAME_NOISE_PERIOD 128.0

            float hash21(float2 p)
            {
                p = fmod(p, FLAME_NOISE_PERIOD);
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);          // smoothstep 보간
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                return vnoise(p) * 0.65 + vnoise(p * 2.17 + 11.3) * 0.35;
            }

            // v 를 n 단으로 계단화. floor(saturate(v)*n)/(n-1) 은 v==1 에서 1 을 넘겨
            // 램프 바깥으로 외삽된다 — 물 셰이더에서 실제로 하드한 줄무늬를 만들었던 버그라
            // min() 으로 막는다.
            float quantize(float v, float n)
            {
                n = max(2.0, n);
                return min(floor(saturate(v) * n), n - 1.0) / (n - 1.0);
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
                float  t  = _Time.y * _Speed;

                // 불길은 총구에서 바깥으로 흐른다 → y(거리) 방향으로만 스크롤.
                // x 는 각도라 스크롤하면 불이 옆으로 미끄러져 보인다.
                float n = fbm(float2(uv.x * _NoiseScale, uv.y * _NoiseScale * 2.0 - t));

                // 모양: 좌우 가장자리 · 끝단은 얇아지고, 총구 쪽은 짧게 붙여 띄운다.
                float edgeU = 1.0 - abs(uv.x * 2.0 - 1.0);
                float shape = saturate(edgeU * 1.7)
                            * saturate((1.0 - uv.y) * 2.4)
                            * saturate(uv.y * 7.0);

                float a = shape * lerp(1.0, n, _Erode) * _Alpha * _Fade;
                // 툰 룩: 가장자리를 흐리게 두지 않고 단단하게 끊는다.
                a = smoothstep(0.10, 0.40, a);
                clip(a - 0.01);

                // 색: 거리(+노이즈)로 심 → 중간 → 끝. 계단화가 툰 셰이딩과 결을 맞춘다.
                float v = quantize(saturate(uv.y * 1.15 + (1.0 - n) * 0.22), _Bands);
                half3 col = v < 0.5 ? lerp(_ColorInner.rgb, _ColorMid.rgb,   v * 2.0)
                                    : lerp(_ColorMid.rgb,   _ColorOuter.rgb, (v - 0.5) * 2.0);

                return half4(col, a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
