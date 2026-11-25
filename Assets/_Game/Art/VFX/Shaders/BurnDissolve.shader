// 화상 사망(ZombieBurnOut) — 불타 죽은 좀비의 몸이 타들어가며 사라진다(2026-11-10 사용자: "불타 죽는 좀비는 디졸브로 타들어가는 느낌").
// 본체: 좀비 툰 머티리얼의 알베도 · 하이라이트 · 그림자 색을 받아 두 단계 셀 셰이딩으로 그리고, _Dissolve(0→1)에 따라 위에서부터 노이즈로 깎인다.
// 깎이는 자리 바로 앞은 새까맣게 그을린다(_CharColor). 밝은 불선은 여기서 안 그린다 — 가산 머티리얼 ZombieOverrun/BurnEdge 가 같은 메시를 한 번 더 그려 얹는다
// (밤 어둠 막(큐 2900) 뒤에 그려야 어둠 속에서 빛나므로 큐가 다른 머티리얼이어야 한다).
// 런타임: ProtoZombie 가 죽는 순간 머티리얼을 갈아끼우고 MPB 로 _Dissolve · _BurnAnchor · _NoiseOffset · _NoiseScale · _EdgeWidth 를 준다(_BaseColor · _EmissionColor 는 기존 그을림·이미션 그대로).
Shader "ZombieOverrun/BurnDissolve"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        _HColor ("Highlight Color", Color) = (1,1,1,1)
        _SColor ("Shadow Color", Color) = (0.2,0.2,0.2,1)
        _RampThreshold ("Ramp Threshold", Range(0,1)) = 0.5
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _EdgeWidth ("Edge Width (불선 띠 — BurnEdge 와 같은 값)", Range(0,0.4)) = 0.08
        _ScorchWidth ("Scorch Width (불선 앞 그을음 띠)", Range(0,0.6)) = 0.22
        _CharColor ("Char Color", Color) = (0.035,0.03,0.028,1)
        _HeightBias ("Height Bias (0 = 노이즈만, 1 = 위에서 아래로 일직선)", Range(0,1)) = 0.7
        _NoiseScale ("Noise Scale (1/m)", Float) = 4.5
        _NoiseOffset ("Noise Offset", Vector) = (0,0,0,0)
        _BurnAnchor ("Burn Anchor (xyz 발 자리, w 몸 높이)", Vector) = (0,0,0,1.7)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "BurnDissolveCommon.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _HColor, _SColor, _EmissionColor, _CharColor;
                half _RampThreshold, _Dissolve, _EdgeWidth, _ScorchWidth, _HeightBias;
                float _NoiseScale;
                float4 _NoiseOffset;
                float4 _BurnAnchor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float n = BurnField(i.positionWS, _BurnAnchor, _NoiseScale, _NoiseOffset.xyz, _HeightBias);
                float d = BurnThreshold(_Dissolve);
                clip(n - d);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float face = IS_FRONT_VFACE(cullFace, 1.0, -1.0);
                float3 nrm = normalize(i.normalWS) * face;
                Light ml = GetMainLight();
                half ndl = dot(nrm, ml.direction) * 0.5 + 0.5;
                half ramp = smoothstep(_RampThreshold - 0.05, _RampThreshold + 0.05, ndl);
                half3 shade = lerp(_SColor.rgb, _HColor.rgb, ramp) * ml.color;
                half3 col = albedo.rgb * shade + _EmissionColor.rgb;

                // 그을음 — 불선(d ~ d+_EdgeWidth) 자리와 그 앞 _ScorchWidth 만큼은 재 색. 구멍으로 들여다보이는 안쪽 면은 통째로 재.
                half scorch = 1.0 - smoothstep(d + _EdgeWidth, d + _EdgeWidth + _ScorchWidth, n);
                scorch = max(scorch * BurnFadeIn(_Dissolve), face < 0 ? 1.0 : 0.0);
                col = lerp(col, _CharColor.rgb, scorch);

                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
