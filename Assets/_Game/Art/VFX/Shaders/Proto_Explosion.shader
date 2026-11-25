// 로켓 착탄 폭발 (GDD §05 — 착탄 반경 2.4m).
//
// 한 셰이더로 네 겹을 그린다 — `_Kind` 로 갈린다:
//   0 = 화구      : 카메라를 보는 빌보드. 테두리를 각도 노이즈로 침식해 완벽한 원이 아니다.
//   1 = 충격파 링 : 지면에 눕는 원판. **테두리가 정확히 폭발 반경**이라 무엇이 맞았는지 읽힌다.
//   2 = 연기      : 화구가 꺼진 뒤 부풀며 올라가는 어두운 빌보드.
//   3 = 섬광      : 0.07초짜리 흰 원판. 첫 프레임의 열감을 담당한다.
//
// 지면 그을음 자국이 한때 있었지만(kind 3) **제거했다** — 도로가 어두워지는 게 보기 싫다는
// 판단이다. 다시 넣으려면 지면에 눕는 원판 + `1 - smoothstep(0.45, 1, p)` 알파가 전부다.
//
// 왜 완벽한 원이 문제였나: 침식이 없을 때 화구는 동심원 밴드가 그려진 정원(正圓)이라
// "폭발"보다 "조준 표식"으로 읽혔다. 각도 노이즈 한 번이 이 인상을 가장 크게 바꾼다.
//
// 텍스처를 쓰지 않는다(물 셰이더·화염 콘과 같은 방침). 플립북을 쓰려면 아트가 프레임을
// 그려야 하고, 8×8 512px 이면 4096² 한 장이다 — 폭발 하나가 그 예산을 먹을 이유가 없다.
//
// UV 규약: 원판 메시가 `length(uv - 0.5) * 2` 로 반경 0(중심)~1(테두리)이 되게 굽는다
// (ProtoLevelUpBeam.BuildDisc 와 같은 규약).
Shader "Proto/Explosion"
{
    Properties
    {
        _ColorCore ("심 (안쪽)", Color) = (1.00, 0.97, 0.78, 1)
        _ColorMid  ("중간",      Color) = (1.00, 0.62, 0.16, 1)
        _ColorEdge ("바깥",      Color) = (0.82, 0.20, 0.07, 1)

        _Bands     ("색 계단 수", Range(2, 8)) = 4
        _RingWidth ("링 두께 (0~1)", Range(0.02, 1)) = 0.34

        _NoiseScale ("노이즈 밀도", Float) = 1.15
        _Lobe       ("실루엣 침식량", Range(0, 0.9)) = 0.42
        _Alpha      ("불투명도", Range(0, 1)) = 1

        _Kind      ("종류 0=화구 1=링 2=연기 3=섬광", Float) = 0
        _Progress  ("진행 0~1 (런타임)", Range(0, 1)) = 0
        _Seed      ("노이즈 시드 (런타임)", Float) = 0
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
            Name "Explosion"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorCore;
                float4 _ColorMid;
                float4 _ColorEdge;
                float  _Bands;
                float  _RingWidth;
                float  _NoiseScale;
                float  _Lobe;
                float  _Alpha;
                float  _Kind;
                float  _Progress;
                float  _Seed;
            CBUFFER_END

            // ── 값 노이즈 (화염 콘과 같은 구현) ─────────────────────────────
            #define EXP_NOISE_PERIOD 128.0

            float hash21(float2 p)
            {
                p = fmod(p, EXP_NOISE_PERIOD);
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

            float fbm(float2 p)
            {
                return vnoise(p) * 0.65 + vnoise(p * 2.17 + 11.3) * 0.35;
            }

            // v==1 에서 램프 밖으로 외삽되는 것을 막은 버전 (물 셰이더에서 하드한 줄무늬를
            // 만들었던 버그다).
            float quantize(float v, float n)
            {
                n = max(2.0, n);
                return min(floor(saturate(v) * n), n - 1.0) / (n - 1.0);
            }

            half3 ramp(float v)
            {
                float q = quantize(v, _Bands);
                return q < 0.5 ? lerp(_ColorCore.rgb, _ColorMid.rgb,  q * 2.0)
                               : lerp(_ColorMid.rgb,  _ColorEdge.rgb, (q - 0.5) * 2.0);
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
                float2 d   = (IN.uv - 0.5) * 2.0;
                float  r   = length(d);                    // 0 = 중심, 1 = 메시 테두리
                float2 dir = d / max(1e-4, r);
                float  p   = saturate(_Progress);

                // 각도 노이즈: 단위원 위의 좌표로 뽑으면 이음매 없이 한 바퀴 돈다.
                // uv.x 를 각도로 쓰면 0/1 경계에 세로 금이 생긴다.
                float ns = fbm(dir * _NoiseScale + _Seed);

                float a, v;

                if (_Kind < 0.5)
                {
                    // ── 화구 ──
                    // 침식은 **안쪽으로만** 한다. 밖으로 부풀리면 r>1 인 혹이 메시 테두리에
                    // 잘려 직선으로 끊긴다 (그래서 ProtoExplosion.fireballScale 로 보상한다).
                    float lobe = 1.0 - ns * _Lobe;
                    a = 1.0 - smoothstep(lobe * 0.58, lobe, r);

                    // 내부도 노이즈로 흔들어 동심원 밴드를 깨고, 진행에 따라 바깥으로 흐르게 한다.
                    float ni = fbm(dir * (_NoiseScale * 2.3) + float2(_Seed + 7.3, r * 1.9 - p * 1.1));
                    v = saturate(r / max(0.2, lobe) * 1.05 + (ni - 0.5) * 0.55);

                    a *= (1.0 - p) * (1.0 - p);
                }
                else if (_Kind < 1.5)
                {
                    // ── 충격파 링 ──
                    // 테두리는 흐트리지 않는다. 데미지 숫자가 없는 게임이라 "어디까지 맞았나"는
                    // 이 링의 바깥 지름에만 있는 정보다 (§15).
                    float w = max(_RingWidth * (1.0 - p * 0.7), 0.02);
                    a = 1.0 - saturate(abs(r - (1.0 - w * 0.5)) / (w * 0.5));
                    a = smoothstep(0.0, 1.0, a);
                    a *= lerp(1.0, ns, 0.30);              // 밝기만 얼룩지게 — 반경은 정확히 유지
                    a *= (1.0 - p) * (1.0 - p);
                    v  = 0.25;                             // 링은 밝은 노랑 쪽으로 고정
                }
                else if (_Kind < 2.5)
                {
                    // ── 연기 ── 화구보다 훨씬 흐리고 오래 남는다. 흩어질수록 더 침식된다.
                    float lobe = 1.0 - ns * _Lobe;
                    a  = 1.0 - smoothstep(lobe * 0.30, lobe, r);
                    a *= smoothstep(0.0, 0.18, p);         // 화구가 아직 밝을 때는 안 보이게
                    a *= (1.0 - p) * (1.0 - p);
                    a *= lerp(1.0, ns, p * 0.7);
                    v  = saturate(r * 0.9 + (ns - 0.5) * 0.4);
                }
                else
                {
                    // ── 섬광 ── 노이즈 없음. 한 프레임짜리라 모양보다 밝기가 전부다.
                    a = 1.0 - smoothstep(0.08, 1.0, r);
                    a *= 1.0 - p;
                    v = 0.0;
                }

                a *= _Alpha;
                clip(a - 0.01);
                return half4(ramp(v), a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
