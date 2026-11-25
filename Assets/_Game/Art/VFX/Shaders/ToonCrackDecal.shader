// 카툰 폭발 바닥 용암 균열(FX_ToonExplosion 의 Cracks 파티클 — 바닥에 눕는 빌보드 한 장) — 2026-11-12 레퍼런스 영상 재현.
//
// 텍스처는 **데이터**(선형 임포트, ArtSource/VFX/bake_toon_explosion.py):
//   R = 균열까지 거리(1 = 균열 한가운데 → 0), G = 사라지는 순서(균열 토막마다 무작위 + 바깥일수록 먼저), B = 그을음 자리.
// 입자 색 알파(수명 색) = 남은 양 1 → 0. 줄면 균열이 가늘어지고(R 문턱이 오른다) 순서값 G 가 큰 토막부터 끊겨 나간다 —
// 영상 2.4~3.0초처럼 그물이 조각조각 꺼진다. 그을음(B)은 알파 블렌드로 바닥을 어둡게, 균열 빛은 가산 — 한 패스(프리멀티플라이드).
// 입자 색 RGB = 빛 색 배수(식으며 붉어지게 수명 색으로).
Shader "ZombieOverrun/ToonCrackDecal"
{
    Properties
    {
        _MainTex ("균열 데이터 (R 거리 · G 순서 · B 그을음, 선형)", 2D) = "black" {}
        _ColCore  ("균열 심", Color) = (1.0, 0.56, 0.2, 1)
        _ColGlow  ("균열 빛", Color) = (0.8, 0.2, 0.06, 1)
        _ColScorch ("그을음", Color) = (0.1, 0.06, 0.065, 1)
        _GlowBoost ("빛 밝기 (1 넘으면 블룸)", Range(0.5, 4)) = 1.15
        _ScorchAlpha ("그을음 진하기", Range(0, 1)) = 0.85
        _CoreWidth ("심 두께 (R 문턱)", Range(0, 1)) = 0.52
        _GlowWidth ("빛 번짐 (R 문턱 더)", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "ToonCrackDecal"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _ColCore, _ColGlow, _ColScorch;
                half _GlowBoost, _ScorchAlpha, _CoreWidth, _GlowWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half life = IN.color.a;

                // 토막 끊기 — 순서값이 남은 양보다 크면 꺼진다
                half keep = smoothstep(0.0, 0.05, life * 1.08 - t.g);
                // 가늘어지기 — 남은 양이 0.6 아래로 내려가면 심 · 빛 두께가 준다
                half thick = lerp(0.35, 1.0, saturate(life / 0.6));
                half coreTh = 1.0 - _CoreWidth * thick;
                half glowTh = 1.0 - (_CoreWidth + _GlowWidth) * thick;
                half aa = max(fwidth(t.r), 1e-3);
                half core = smoothstep(coreTh - aa, coreTh + aa, t.r) * keep;
                half glow = smoothstep(glowTh, 1.0, t.r) * keep;

                half3 emit = (_ColGlow.rgb * glow * 0.7 + _ColCore.rgb * core) * _GlowBoost * IN.color.rgb;
                half scorch = t.b * _ScorchAlpha * saturate(life * 2.0);
                half3 rgb = emit + _ColScorch.rgb * scorch;
                return half4(rgb, scorch);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
