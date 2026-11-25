// 경험치 젬 2 버전 — 금색 물고기 (2026-11-18 사용자 "동전은 아니고 그냥 금색 물고기로").
//
// 메시 규약(GoldFishMesh.Build): 물고기 그림(1 버전)과 같은 상자 — x = -0.5(코) … +0.5(꼬리), y = -0.5 … +0.5, 머리 -x.
// 그래서 몸 휨은 Proto/GemFish 와 같은 식이다(꼬리로 갈수록 y 로, 가중치 제곱). _Flop · _Phase 는 ProtoGemPool 이
// 젬마다 MaterialPropertyBlock 으로 넣는다 — 착지 파닥 · 끌려올 때 헤엄이 물고기 그림과 똑같이 나온다.
//
// 장면 조명을 쓰지 않는다: 빛 방향을 **카메라 기준**(_LightDirVS, 왼쪽 위 앞)으로 고정한 3단 툰 명암 + 반사 점 + 가장자리 빛.
// 밤 조명 · 가로등 자리와 상관없이 늘 같은 금색(적 도감 렌더에서 키 라이트가 뒤에서 오면 툰이 전부 그늘색이 됐던 일).
// 그 위에 좌표로 그리는 무늬: 비늘(엇갈린 반달, 옅게) · 밝은 배 · 볼터치 · 웃는 입 · 지느러미 결 · 몸을 쓸고 지나가는 광택 띠.
// 2판(같은 날 "좀 더 귀여운 걸로"): 아가미 선을 빼고 볼터치 · 입을 넣었다. 금색을 덜 갈색으로(그늘 금 밝게) · 명암 경계를 부드럽게 · 비늘 옅게.
// 밝은 경계를 0.84 로 올렸다 — 카메라를 정면으로 보는 옆면이 ndl 0.8 이라 0.72 면 몸 전체가 밝은 레몬색으로 떠서 금색이 안 읽혔다.
// 정점 색: r = 그늘 배율, g = 지느러미, b = 눈(0.25 흰자 · 0.5 하이라이트 · 1 검은자).
// 불투명(Geometry) — 밤 어둠 막(2900)에는 물고기 그림처럼 같이 어두워진다.
Shader "Proto/GoldFish"
{
    Properties
    {
        [Header(Gold)]
        _ColorLight  ("밝은 금", Color) = (1.0, 0.92, 0.50, 1)
        _ColorMid    ("금", Color) = (1.0, 0.74, 0.20, 1)
        _ColorDark   ("그늘 금", Color) = (0.86, 0.50, 0.14, 1)
        _RampLow     ("그늘 경계(0~1)", Range(0, 1)) = 0.36
        _RampHigh    ("밝은 경계(0~1)", Range(0, 1)) = 0.84
        _RampSoft    ("경계 부드러움", Range(0.001, 0.3)) = 0.09
        _LightDirVS  ("빛 방향 (카메라 기준: x 오른쪽 · y 위 · z 카메라 쪽)", Vector) = (-0.45, 0.65, 0.6, 0)
        _SpecColor2  ("반사 점 색", Color) = (1, 0.98, 0.86, 1)
        _SpecSize    ("반사 점 크기", Range(0, 0.3)) = 0.03
        _SpecStrength("반사 점 세기", Range(0, 1)) = 0.5
        _RimColor    ("가장자리 빛 색", Color) = (1, 0.93, 0.62, 1)
        _RimPower    ("가장자리 빛 폭(클수록 얇다)", Range(0.5, 8)) = 3
        _RimStrength ("가장자리 빛 세기", Range(0, 1)) = 0.3

        [Header(Pattern)]
        _BellyLight    ("밝은 배", Range(0, 0.6)) = 0.22
        _ScaleSize     ("비늘 크기", Range(0.02, 0.2)) = 0.072
        _ScaleStrength ("비늘 선 진하기", Range(0, 0.6)) = 0.1
        _BlushColor    ("볼터치 색", Color) = (1, 0.52, 0.42, 1)
        _BlushStrength ("볼터치 세기", Range(0, 1)) = 0.6
        _MouthColor    ("입 색", Color) = (0.45, 0.20, 0.06, 1)
        _MouthStrength ("입 진하기", Range(0, 1)) = 0.85
        _FinTint       ("지느러미 색 배율", Color) = (1.06, 1.0, 0.78, 1)
        _FinStripe     ("지느러미 결 진하기", Range(0, 0.6)) = 0.14
        _EyeColor      ("검은자", Color) = (0.13, 0.08, 0.05, 1)
        _ScleraColor   ("흰자", Color) = (1, 0.97, 0.88, 1)

        [Header(Sheen)]
        _SheenColor    ("광택 띠 색", Color) = (1, 1, 0.88, 1)
        _SheenPeriod   ("광택 주기(초)", Float) = 2.6
        _SheenWidth    ("광택 띠 폭", Range(0.02, 0.5)) = 0.10
        _SheenStrength ("광택 세기", Range(0, 2)) = 0.3

        [Header(Body bend same as GemFish)]
        _IdleAmp  ("평상시 휨",   Range(0, 0.5)) = 0.020
        _FlopAmp  ("파닥일 때 휨", Range(0, 0.5)) = 0.170
        _IdleSpeed("평상시 속도",  Float) = 1.6
        _FlopSpeed("파닥 속도",    Float) = 34.0
        _WaveFreq ("몸을 지나는 파동 수", Float) = 0.85
        _Flop     ("파닥 세기 (0~1, 런타임)", Range(0,1)) = 0
        _Phase    ("위상 (런타임)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ColorLight, _ColorMid, _ColorDark;
            half  _RampLow, _RampHigh, _RampSoft;
            float4 _LightDirVS;
            half4 _SpecColor2;
            half  _SpecSize, _SpecStrength;
            half4 _RimColor;
            half  _RimPower, _RimStrength;
            half  _BellyLight, _ScaleSize, _ScaleStrength;
            half4 _BlushColor, _MouthColor;
            half  _BlushStrength, _MouthStrength;
            half4 _FinTint;
            half  _FinStripe;
            half4 _EyeColor, _ScleraColor;
            half4 _SheenColor;
            float _SheenPeriod;
            half  _SheenWidth, _SheenStrength;
            float _IdleAmp, _FlopAmp, _IdleSpeed, _FlopSpeed, _WaveFreq, _Flop, _Phase;
        CBUFFER_END

        // Proto/GemFish 와 같은 몸 휨 — 머리 고정, 꼬리로 갈수록(가중치 제곱) y 로 사인파
        float3 BendFish(float3 pos)
        {
            float w = saturate(pos.x + 0.5);
            w = w * w;
            float amp   = lerp(_IdleAmp,   _FlopAmp,   _Flop);
            float speed = lerp(_IdleSpeed, _FlopSpeed, _Flop);
            float bend = sin((pos.x + 0.5) * 6.28318 * _WaveFreq + _Time.y * speed + _Phase);
            pos.y += bend * amp * w;
            return pos;
        }
        ENDHLSL

        Pass
        {
            Name "GoldFish"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;   // 휘기 전 — 무늬가 몸에 붙어 같이 휜다
                half4  color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 p = BendFish(IN.positionOS.xyz);
                VertexPositionInputs vp = GetVertexPositionInputs(p);
                OUT.positionHCS = vp.positionCS;
                OUT.positionWS  = vp.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.color       = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float3 l = normalize(mul((float3x3)UNITY_MATRIX_I_V, _LightDirVS.xyz));
                float2 xy = IN.positionOS.xy;
                half finM = IN.color.g;
                half eyeB = IN.color.b;
                half bodyM = (1.0h - finM) * step(eyeB, 0.1h);

                // 3단 툰 명암
                float ndl = dot(n, l) * 0.5 + 0.5;
                half3 col = lerp(_ColorDark.rgb, _ColorMid.rgb, smoothstep(_RampLow - _RampSoft, _RampLow + _RampSoft, ndl));
                col = lerp(col, _ColorLight.rgb, smoothstep(_RampHigh - _RampSoft, _RampHigh + _RampSoft, ndl));

                // 밝은 배
                col *= 1.0h + _BellyLight * saturate((-xy.y - 0.02) * 5.0) * bodyM;

                // 비늘 — 엇갈린 반달. 아가미 뒤 ~ 꼬리자루 앞에만
                float2 sp = xy / max(0.01, _ScaleSize);
                float row = floor(sp.y);
                sp.x += row * 0.5;
                float2 f = frac(sp);
                float d = length(f - float2(0.5, 0.0));
                float scaleLine = smoothstep(0.40, 0.5, d) * (1.0 - smoothstep(0.5, 0.6, d));
                float scaleMask = smoothstep(-0.12, -0.05, xy.x) * (1.0 - smoothstep(0.08, 0.15, xy.x));   // 눈 · 볼 뒤부터
                col *= 1.0h - _ScaleStrength * scaleLine * scaleMask * bodyM;

                // 볼터치 — 눈 뒤아래 동그란 분홍(양쪽 같은 자리, 몸에만)
                float2 bq = (xy - float2(-0.215, -0.118)) / float2(0.056, 0.036);
                float blush = 1.0 - smoothstep(0.55, 1.0, length(bq));
                col = lerp(col, _BlushColor.rgb, blush * _BlushStrength * bodyM);

                // 웃는 입 — 코 아래 작은 반달(아래쪽 절반만)
                float2 mq = xy - float2(-0.445, -0.040);
                float md = abs(length(mq) - 0.030);
                float mouth = (1.0 - smoothstep(0.004, 0.010, md)) * step(mq.y, 0.004);
                col = lerp(col, _MouthColor.rgb, mouth * _MouthStrength * bodyM);

                // 지느러미 — 밝은 금 + 몸 가운데서 퍼지는 결
                float rays = 0.5 + 0.5 * sin(atan2(xy.y, xy.x - 0.02) * 46.0);
                col = lerp(col, col * _FinTint.rgb * (1.0h - _FinStripe * smoothstep(0.55, 1.0, rays)), finM);

                col *= IN.color.r;

                // 반사 점 · 가장자리 빛
                float3 h = normalize(l + v);
                // 둥근 몸이라 반사 점이 몸을 따라 가로로 길게 눕는다 — 세면 등이 하얗게 날아가 반쯤만(크림색 윤기)
                col += _SpecColor2.rgb * (smoothstep(1.0 - _SpecSize, 1.0 - _SpecSize * 0.55, dot(n, h)) * _SpecStrength);
                col += _RimColor.rgb * (pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimStrength);

                // 광택 띠 — 코에서 꼬리로 비스듬히 쓸고 간다. 젬마다 _Phase 가 달라 한꺼번에 번쩍이지 않는다
                float s = frac(_Time.y / max(0.1, _SheenPeriod) + _Phase * 0.159);
                float pos = lerp(-0.9, 3.0, s);
                float band = 1.0 - smoothstep(0.0, _SheenWidth, abs(xy.x + xy.y * 0.35 - pos));
                col += _SheenColor.rgb * (band * band * _SheenStrength * step(eyeB, 0.1h));

                // 눈 — 흰자 · 하이라이트 · 검은자(빛 계산 위에 덮는다)
                half sclera = step(0.125h, eyeB) * step(eyeB, 0.375h);
                half hi     = step(0.375h, eyeB) * step(eyeB, 0.75h);
                half pupil  = step(0.75h, eyeB);
                col = lerp(col, _ScleraColor.rgb, sclera);
                col = lerp(col, _EyeColor.rgb, pupil);
                col = lerp(col, half3(1, 1, 1), hi);

                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        // 깊이 미리 그리기 — 몸 휨을 똑같이 해야 색 패스와 어긋나지 않는다
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            float4 vertDepth(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(BendFish(positionOS.xyz)); }
            half fragDepth() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    Fallback Off
}
