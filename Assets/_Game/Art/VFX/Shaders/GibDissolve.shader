// 처치 파편(ZombieGibs) 조각용 — 좀비 툰 머티리얼의 알베도 · 하이라이트 · 그림자 색을 받아 두 단계 셀 셰이딩으로 그리고,
// _Dissolve(0→1)에 따라 노이즈로 녹아 사라진다(가장자리는 _EdgeColor). 양면(Cull Off 기본) — 조각 안쪽이 뚫려 보이지 않게.
// 런타임: ZombieGibs 가 Resources/VFX/M_GibDissolve 를 바탕으로 좀비 머티리얼마다 복제하고, 조각마다 MPB 로 _Dissolve · _NoiseOffset 을 준다.
Shader "ZombieOverrun/GibDissolve"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        _HColor ("Highlight Color", Color) = (1,1,1,1)
        _SColor ("Shadow Color", Color) = (0.2,0.2,0.2,1)
        _RampThreshold ("Ramp Threshold", Range(0,1)) = 0.5
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0,0.3)) = 0.08
        _EdgeColor ("Edge Color", Color) = (0.45,0.03,0.03,1)
        _NoiseScale ("Noise Scale", Float) = 18
        _NoiseOffset ("Noise Offset", Vector) = (0,0,0,0)
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

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _HColor, _SColor, _EdgeColor;
                half _RampThreshold, _Dissolve, _EdgeWidth;
                float _NoiseScale;
                float4 _NoiseOffset;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                half4 color : COLOR;          // 정점색 — ZombieGibs 가 안쪽 껍질·옆면에 살색을 넣는다(바깥은 흰색)
            };

            // 값 노이즈(텍스처 없이) — 조각 로컬 좌표 기준이라 조각마다 다른 무늬로 녹는다
            float hash31(float3 p) { p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
            float vnoise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash31(i),                      n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0)),    n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1)),    n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1)),    n111 = hash31(i + float3(1, 1, 1));
                float nx00 = lerp(n000, n100, f.x), nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x), nx11 = lerp(n011, n111, f.x);
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.fogFactor = ComputeFogFactor(vp.positionCS.z);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 np = i.positionOS * _NoiseScale + _NoiseOffset.xyz;
                float n = 0.65 * vnoise(np) + 0.35 * vnoise(np * 2.3 + 7.1);
                float d = _Dissolve * 1.08 - 0.04;   // 0 이면 아무것도 안 깎이고 1 이면 다 사라진다
                clip(n - d);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                albedo.rgb *= i.color.rgb;
                float3 nrm = normalize(i.normalWS) * (IS_FRONT_VFACE(cullFace, 1.0, -1.0));
                Light ml = GetMainLight();
                half ndl = dot(nrm, ml.direction) * 0.5 + 0.5;
                half ramp = smoothstep(_RampThreshold - 0.05, _RampThreshold + 0.05, ndl);
                half3 shade = lerp(_SColor.rgb, _HColor.rgb, ramp) * ml.color;
                half3 col = albedo.rgb * shade;

                half edge = 1 - smoothstep(d, d + _EdgeWidth, n);
                col = lerp(col, _EdgeColor.rgb, edge * saturate(_Dissolve * 8));
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
