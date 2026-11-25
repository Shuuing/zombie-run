// ZombieOverrun/FX/Particle — 게임 파티클 이펙트가 함께 쓰는 셰이더 하나 (2026-11-20).
//
// 세 갈래를 합쳤다. 머티리얼을 옮길 때 그림이 그대로 나오도록 각 원본의 계산을 그대로 따른다.
//   · Universal Render Pipeline/Particles/Unlit (Unity 기본) — 속성 이름 · 키워드 · 계산이 같다.
//     (_BaseMap · _BaseColor · _SrcBlend/_DstBlend · _Cull · 플립북 섞기 · 소프트 파티클 · 카메라 흐려짐 · 발광 · 색 모드 · 안개)
//   · SurvivorAce/Effects/Particle · UVFlow, 탕탕 Effects/Particle · UVFlow · Turbulence · SheetBase
//     (텍스처 × 색 × 정점색, 블렌드 · 컬 · 깊이 검사를 머티리얼이 정함, 텍스처 흐름 · 회전, 마스크, 일렁임, 안개 없음)
// 원작 머티리얼은 _Fog 0 · _ZTest 원래 값 · 색은 _TintColor(HDR), URP 머티리얼은 _Fog 1 · _ZTest 4(LEqual) · 색은 _BaseColor 로 옮긴다 — FxShaderUnify.cs.
//
// GPU 인스턴싱은 일부러 뺐다: 원작 셰이더처럼 메시 파티클도 보통 경로로 그린다(같은 그림, 미리보기에서 원점에 그려지는 문제 없음).
Shader "ZombieOverrun/FX/Particle"
{
    Properties
    {
        [Header(Main)]
        [MainTexture] _BaseMap ("텍스처", 2D) = "white" {}
        // 색이 두 칸인 까닭: Unity 는 보통 색(_BaseColor, URP 와 같음)은 감마로 저장해 리니어로 바꿔 쓰고,
        // HDR 색(_TintColor, 원작 셰이더와 같음)은 리니어로 저장해 그대로 쓴다. 각자 원래 칸에 값을 두면 숫자를 바꾸지 않고 같은 그림이 나온다.
        [MainColor] _BaseColor ("색 (URP 파티클과 같음 — 셋업 코드가 씀)", Color) = (1,1,1,1)
        [HDR] _TintColor ("세기 색 (HDR — 원작 _TintColor)", Color) = (1,1,1,1)
        _MainSpeed ("텍스처 흐름 (xy = 초당 UV)", Vector) = (0,0,0,0)
        _MainRotation ("텍스처 회전 (도, 가운데 기준)", Float) = 0

        [Header(Mask)]
        [Toggle(_MASK_ON)] _MaskEnabled ("마스크 쓰기", Float) = 0
        _MaskTex ("마스크 (R x A 가 알파를 곱함)", 2D) = "white" {}
        _MaskSpeed ("마스크 흐름 (xy)", Vector) = (0,0,0,0)

        [Header(Noise)]
        [Toggle(_NOISE_ON)] _NoiseEnabled ("일렁임 쓰기", Float) = 0
        _NoiseMap ("일렁임 텍스처 (RG)", 2D) = "gray" {}
        _NoiseSpeed ("일렁임 흐름 (xy)", Vector) = (0,0,0,0)
        _NoiseStrength ("일렁임 세기 (UV)", Range(0, 0.5)) = 0.03

        // MasterStylizedFX 복사본에서 온 모드 — 입자의 사용자 정점 스트림(UV.zw · Custom1.xyzw)을 읽는다. 렌더러의 Custom Vertex Streams 가 원본대로 있어야 한다.
        //   디졸브: 마스크 텍스처(노이즈) R ^ UV.z 로 알파를 깎고 경계 선명도 Custom1.x, 노이즈 흐름 Custom1.y × 시간, 밝기 × (UV.w + 1)
        //   원샷: 텍스처 흐름 UV.z × _CustomScroll, 마스크(켜면) 경계 = Custom1.z · 흐름 Custom1.w × _CustomMaskScroll · _Feather, 고정 마스크, 밝기 × (1 + Custom1.x)
        [Header(MSFX)]
        [KeywordEnum(None, Dissolve, OneShot)] _Msfx ("MSFX 모드 (사용자 정점 스트림)", Float) = 0
        _CustomScroll ("  원샷: 텍스처 흐름 × UV.z", Vector) = (0,0,0,0)
        _CustomMaskScroll ("  원샷: 마스크 흐름 × Custom1.w", Vector) = (0,0,0,0)
        _Feather ("  원샷: 마스크 경계 부드럽게", Range(0, 1)) = 0
        _StaticMask ("  원샷: 고정 마스크 (R)", 2D) = "white" {}
        _StaticSmooth ("  원샷: 고정 마스크 smoothstep (x 시작 · y 끝)", Vector) = (0,1,0,0)

        [Header(Blend)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("섞기 Src (보통 SrcAlpha)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("섞기 Dst (One = 가산, OneMinusSrcAlpha = 알파)", Float) = 10
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendAlpha ("섞기 Src 알파", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendAlpha ("섞기 Dst 알파", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("면 버리기", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("깊이 검사 (Always = 늘 위에)", Float) = 4
        [Enum(Off, 0, On, 1)] _ZWrite ("깊이 쓰기", Float) = 0
        [ToggleUI] _Fog ("안개 받기", Float) = 1

        [Header(Particle)]
        [Toggle(_FLIPBOOKBLENDING_ON)] _FlipbookBlending ("플립북 섞기 (정점 스트림 UV2 + AnimBlend)", Float) = 0
        [Toggle(_SOFTPARTICLES_ON)] _SoftParticlesEnabled ("소프트 파티클", Float) = 0
        _SoftParticlesNearFadeDistance ("  가까이 사라짐", Float) = 0
        _SoftParticlesFarFadeDistance ("  멀리 사라짐", Float) = 1
        [Toggle(_FADING_ON)] _CameraFadingEnabled ("카메라 가까이 흐려짐", Float) = 0
        _CameraNearFadeDistance ("  가까이", Float) = 1
        _CameraFarFadeDistance ("  멀리", Float) = 2
        [Toggle(_EMISSION)] _EmissionEnabled ("발광 더하기", Float) = 0
        [HDR] _EmissionColor ("  발광 색", Color) = (0,0,0,1)
        _EmissionMap ("  발광 텍스처", 2D) = "white" {}

        // URP 파티클 머티리얼에서 넘어온 값 — 셋업 코드(FxTemplatePrefabs · InfernoFlameSetup 등)가 그대로 쓴다.
        [HideInInspector] _Surface ("__surface", Float) = 1
        [HideInInspector] _Blend ("__mode", Float) = 0
        [HideInInspector] _ColorMode ("_ColorMode", Float) = 0
        [HideInInspector] _BaseColorAddSubDiff ("_ColorMode", Vector) = (0,0,0,0)
        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _AlphaClip ("__clip", Float) = 0
        [HideInInspector] _QueueOffset ("Queue offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FxParticle"

            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _FLIPBOOKBLENDING_ON
            #pragma shader_feature_local _SOFTPARTICLES_ON
            #pragma shader_feature_local _FADING_ON
            #pragma shader_feature_local _MASK_ON
            #pragma shader_feature_local _NOISE_ON
            #pragma shader_feature_local _ _MSFX_DISSOLVE _MSFX_ONESHOT
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _ _COLOROVERLAY_ON _COLORCOLOR_ON _COLORADDSUBDIFF_ON
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _TintColor;
                float4 _MainSpeed;
                float _MainRotation;
                float4 _MaskTex_ST;
                float4 _MaskSpeed;
                float4 _NoiseMap_ST;
                float4 _NoiseSpeed;
                float _NoiseStrength;
                half4 _EmissionColor;
                half4 _BaseColorAddSubDiff;
                half _Cutoff;
                half _Fog;
                float _SoftParticlesNearFadeDistance;
                float _SoftParticlesFarFadeDistance;
                float _CameraFadingEnabled;
                float _CameraNearFadeDistance;
                float _CameraFarFadeDistance;
                float4 _CustomScroll;
                float4 _CustomMaskScroll;
                float _Feather;
                float4 _StaticMask_ST;
                float4 _StaticSmooth;
            CBUFFER_END

            #if defined(_MSFX_DISSOLVE) || defined(_MSFX_ONESHOT)
                #define FX_MSFX 1
            #endif

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_StaticMask);  SAMPLER(sampler_StaticMask);
            TEXTURE2D(_MaskTex);     SAMPLER(sampler_MaskTex);
            TEXTURE2D(_NoiseMap);    SAMPLER(sampler_NoiseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color       : COLOR;
            #if defined(_FLIPBOOKBLENDING_ON)
                float4 texcoords  : TEXCOORD0;   // xy = 이 칸, zw = 다음 칸 (URP 와 같은 정점 스트림)
                float texcoordBlend : TEXCOORD1;
            #elif defined(FX_MSFX)
                float4 texcoords  : TEXCOORD0;   // xy = UV, zw = 사용자 값 (MSFX)
                float4 custom1    : TEXCOORD1;
            #else
                float2 texcoords  : TEXCOORD0;
            #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color       : COLOR;
                float4 uv         : TEXCOORD0;   // xy = 본 텍스처(변환 · 흐름 끝), zw = 원래 UV(마스크 · 일렁임)
            #if defined(_FLIPBOOKBLENDING_ON)
                float3 uv2AndBlend : TEXCOORD1;
            #endif
            #if defined(_SOFTPARTICLES_ON) || defined(_FADING_ON)
                float4 projectedPosition : TEXCOORD2;
            #endif
                float3 positionWS : TEXCOORD3;   // 안개 (URP 14 는 픽셀에서 계산 — _FOG_FRAGMENT)
            #if defined(FX_MSFX)
                float4 msfx0 : TEXCOORD4;        // UV.zw → zw
                float4 msfx1 : TEXCOORD5;        // Custom1.xyzw
            #endif
            };

            float2 MainUV(float2 uv)
            {
                // 탕탕 UVFlow 의 _Angele 와 같은 회전(가운데 기준) → 타일링 · 오프셋 → 흐름
                if (_MainRotation != 0.0)
                {
                    float a = radians(_MainRotation);
                    float c = cos(a), s = sin(a);
                    uv = mul(float2x2(c, -s, s, c), uv - 0.5) + 0.5;
                }
                return uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _MainSpeed.xy * _Time.y;
            }

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.color = input.color;
                o.uv.xy = MainUV(input.texcoords.xy);
                o.uv.zw = input.texcoords.xy;
            #if defined(_FLIPBOOKBLENDING_ON)
                o.uv2AndBlend = float3(MainUV(input.texcoords.zw), input.texcoordBlend);
            #endif
            #if defined(FX_MSFX)
                o.msfx0 = input.texcoords;
                o.msfx1 = input.custom1;
                #if defined(_MSFX_ONESHOT)
                    o.uv.xy += input.texcoords.z * _CustomScroll.xy;   // 원샷: UV + UV.z × Scroll
                #endif
            #endif
            #if defined(_SOFTPARTICLES_ON) || defined(_FADING_ON)
                o.projectedPosition = pos.positionNDC;
            #endif
                o.positionWS = pos.positionWS;
                return o;
            }

            // ── URP Particles.hlsl 에서 그대로 옮긴 계산 ──
            #if defined(_ALPHAPREMULTIPLY_ON)
                #define SOFT_PARTICLE_MUL_ALBEDO(albedo, val) albedo * val
                #define ALBEDO_MUL albedo
            #elif defined(_ALPHAMODULATE_ON)
                #define SOFT_PARTICLE_MUL_ALBEDO(albedo, val) half4(lerp(half3(1.0, 1.0, 1.0), albedo.rgb, albedo.a * val), albedo.a * val)
                #define ALBEDO_MUL albedo.a
            #else
                #define SOFT_PARTICLE_MUL_ALBEDO(albedo, val) albedo * half4(1.0, 1.0, 1.0, val)
                #define ALBEDO_MUL albedo.a
            #endif

            half4 MixParticleColor(half4 baseColor, half4 particleColor, half4 colorAddSubDiff)
            {
            #if defined(_COLOROVERLAY_ON)
                half4 output = baseColor;
                output.rgb = lerp(1 - 2 * (1 - baseColor.rgb) * (1 - particleColor.rgb), 2 * baseColor.rgb * particleColor.rgb, step(baseColor.rgb, 0.5));
                output.a *= particleColor.a;
                return output;
            #elif defined(_COLORCOLOR_ON)
                half3 aHSL = RgbToHsv(baseColor.rgb);
                half3 bHSL = RgbToHsv(particleColor.rgb);
                half3 rHSL = half3(bHSL.x, bHSL.y, aHSL.z);
                return half4(HsvToRgb(rHSL), baseColor.a * particleColor.a);
            #elif defined(_COLORADDSUBDIFF_ON)
                half4 output = baseColor;
                output.rgb = baseColor.rgb + particleColor.rgb * colorAddSubDiff.x;
                output.rgb = lerp(output.rgb, abs(output.rgb), colorAddSubDiff.y);
                output.a *= particleColor.a;
                return output;
            #else
                return baseColor * particleColor;
            #endif
            }

            float SoftParticles(float near, float far, float4 projection)
            {
                float fade = 1;
                if (near > 0.0 || far > 0.0)
                {
                    float2 uv = UnityStereoTransformScreenSpaceTex(projection.xy / projection.w);
                    float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                    float sceneZ = (unity_OrthoParams.w == 0) ? LinearEyeDepth(rawDepth, _ZBufferParams) : LinearDepthToEyeDepth(rawDepth);
                    float thisZ = LinearEyeDepth(projection.z / projection.w, _ZBufferParams);
                    fade = saturate(far * ((sceneZ - near) - thisZ));
                }
                return fade;
            }

            half CameraFade(float near, float far, float4 projection)
            {
                float thisZ = LinearEyeDepth(projection.z / projection.w, _ZBufferParams);
                return half(saturate((thisZ - near) * far));
            }

            half3 AlphaModulateAndPremultiply(half3 albedo, half alpha)
            {
            #if defined(_ALPHAMODULATE_ON)
                return AlphaModulate(albedo, alpha);
            #elif defined(_ALPHAPREMULTIPLY_ON)
                return AlphaPremultiply(albedo, alpha);
            #else
                return albedo;
            #endif
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv.xy;
                float2 uv0 = i.uv.zw;
            #if defined(_NOISE_ON)
                // 탕탕 Turbulence: (노이즈 RG - 0.5) 만큼 본 UV 를 민다
                float2 nuv = uv0 * _NoiseMap_ST.xy + _NoiseMap_ST.zw + _NoiseSpeed.xy * _Time.y;
                float2 distort = (SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, nuv).rg - 0.5) * _NoiseStrength;
                uv += distort;
            #endif

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            #if defined(_FLIPBOOKBLENDING_ON)
                half4 albedo2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv2AndBlend.xy);
                albedo = lerp(albedo, albedo2, half(i.uv2AndBlend.z));
            #endif
                albedo *= _BaseColor * _TintColor;

                half4 colorAddSubDiff = half4(0, 0, 0, 0);
            #if defined(_COLORADDSUBDIFF_ON)
                colorAddSubDiff = _BaseColorAddSubDiff;
            #endif
                albedo = MixParticleColor(albedo, i.color, colorAddSubDiff);

            #if defined(_MSFX_DISSOLVE)
                // MSFX PowDissolve: a0 = 알파 × 노이즈R ^ UV.z, 알파 = saturate(a0 + (a0 - 0.1) × Custom1.x), 밝기 × (UV.w + 1)
                float2 duv = uv0 * _MaskTex_ST.xy + _MaskTex_ST.zw + float2(i.msfx1.y * _Time.y, 0.0);
                half a0 = albedo.a * pow(SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, duv).r, i.msfx0.z);
                albedo.a = saturate(a0 + (a0 - 0.1) * i.msfx1.x);
                albedo.rgb *= (i.msfx0.w + 1.0);
            #elif defined(_MSFX_ONESHOT)
                // MSFX CommonParticleOneShot: 알파 × 경계 마스크(켜면) × 고정 마스크, 밝기 × (1 + Custom1.x)
                #if defined(_MASK_ON)
                    float2 ouv = uv0 * _MaskTex_ST.xy + _MaskTex_ST.zw + i.msfx1.w * _CustomMaskScroll.xy;
                    half edge = lerp(-_Feather, 1.0 + _Feather, i.msfx1.z);
                    albedo.a *= smoothstep(edge - _Feather, edge + _Feather, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, ouv).r);
                #endif
                float2 suv = uv0 * _StaticMask_ST.xy + _StaticMask_ST.zw;
                albedo.a *= smoothstep(_StaticSmooth.x, _StaticSmooth.y, SAMPLE_TEXTURE2D(_StaticMask, sampler_StaticMask, suv).r);
                albedo.rgb *= (1.0 + i.msfx1.x);
            #elif defined(_MASK_ON)
                float2 muv = uv0 * _MaskTex_ST.xy + _MaskTex_ST.zw + _MaskSpeed.xy * _Time.y;
                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, muv);
                albedo.a *= mask.r * mask.a;
            #endif

            #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
            #endif
                albedo.rgb = AlphaModulateAndPremultiply(albedo.rgb, albedo.a);

            #if defined(_SOFTPARTICLES_ON)
                float softInv = 1.0 / max(0.01, _SoftParticlesFarFadeDistance - _SoftParticlesNearFadeDistance);
                albedo = SOFT_PARTICLE_MUL_ALBEDO(albedo, half(SoftParticles(_SoftParticlesNearFadeDistance, softInv, i.projectedPosition)));
            #endif
            #if defined(_FADING_ON)
                if (_CameraFadingEnabled > 0.5)
                {
                    float camInv = 1.0 / max(0.01, _CameraFarFadeDistance - _CameraNearFadeDistance);
                    ALBEDO_MUL *= CameraFade(_CameraNearFadeDistance, camInv, i.projectedPosition);
                }
            #endif

                half3 color = albedo.rgb;
            #if defined(_EMISSION)
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
                #if defined(_FLIPBOOKBLENDING_ON)
                    emission = lerp(emission, SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv2AndBlend.xy).rgb, half(i.uv2AndBlend.z));
                #endif
                color += emission * _EmissionColor.rgb;
            #endif

                if (_Fog > 0.5)
                    color = MixFog(color, InitializeInputDataFog(float4(i.positionWS, 1.0), 0.0));
                return half4(color, albedo.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
