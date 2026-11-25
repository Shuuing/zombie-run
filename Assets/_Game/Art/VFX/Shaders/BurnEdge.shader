// 화상 사망(ZombieBurnOut)의 불선 — 타들어가는 경계에 얹는 **가산(Add)** 선(2026-11-10 사용자: "경계라인은 add 로").
// 본체(ZombieOverrun/BurnDissolve)와 같은 메시를 두 번째 머티리얼로 한 번 더 그린다: 구멍 가장자리의 띠(문턱 ~ 문턱+_EdgeWidth)만 남기고 나머지는 버린다.
// 가장자리 안쪽(막 타는 자리)은 밝은 노랑, 바깥으로 갈수록 주황 → 붉게 식는다. 색은 HDR 이라 블룸 문턱을 넘어 번진다.
// 큐 3100(Transparent+100) — 밤 어둠 막(2900) 뒤라 어둠 속에서도 그대로 빛난다(FX 가산 머티리얼과 같은 규칙).
// 깊이는 본체가 써 둔 것에 LEqual 로 붙는다(Offset 으로 겹침 깜빡임 방지). 값은 전부 본체와 같은 MPB 가 준다.
Shader "ZombieOverrun/BurnEdge"
{
    Properties
    {
        [HDR] _EdgeColorInner ("Edge Inner (막 타는 자리)", Color) = (3.2,2.1,0.7,1)
        [HDR] _EdgeColorOuter ("Edge Outer (식는 쪽)", Color) = (2.2,0.45,0.05,1)
        _EdgeWidth ("Edge Width (본체와 같은 값)", Range(0,0.4)) = 0.08
        _Flicker ("Flicker (0 = 고른 밝기)", Range(0,1)) = 0.35
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _HeightBias ("Height Bias (본체와 같은 값)", Range(0,1)) = 0.7
        _NoiseScale ("Noise Scale (1/m)", Float) = 4.5
        _NoiseOffset ("Noise Offset", Vector) = (0,0,0,0)
        _BurnAnchor ("Burn Anchor (xyz 발 자리, w 몸 높이)", Vector) = (0,0,0,1.7)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "BurnEdge"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BurnDissolveCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EdgeColorInner, _EdgeColorOuter;
                half _EdgeWidth, _Flicker, _Dissolve, _HeightBias;
                float _NoiseScale;
                float4 _NoiseOffset;
                float4 _BurnAnchor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float n = BurnField(i.positionWS, _BurnAnchor, _NoiseScale, _NoiseOffset.xyz, _HeightBias);
                float d = BurnThreshold(_Dissolve);
                // 띠 안에서의 자리: 1 = 구멍 가장자리(막 타는 자리), 0 = 띠 바깥. 이미 사라진 곳(n < d)과 띠 밖은 버린다.
                float e = 1.0 - (n - d) / max(_EdgeWidth, 0.0001);
                clip(n - d);
                clip(e - 0.001);

                // 불씨처럼 일렁이게 — 자리마다 다른 박자로 밝기가 흔들린다
                float fl = BurnNoise((i.positionWS - _BurnAnchor.xyz) * (_NoiseScale * 3.0) + float3(0, _Time.y * 5.0, 0) + _NoiseOffset.xyz);
                float flick = lerp(1.0, 0.55 + 0.9 * fl, _Flicker);

                half3 col = lerp(_EdgeColorOuter.rgb, _EdgeColorInner.rgb, e * e) * (e * flick * BurnFadeIn(_Dissolve));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
