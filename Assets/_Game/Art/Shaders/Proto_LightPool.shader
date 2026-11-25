// 가로등 불빛 웅덩이 — 바닥에 눕힌 원판에 부드러운 원형 빛을 더한다(가법). 텍스처 0장.
//
// 진짜 포인트 라이트를 가로등마다 달지 않는 이유: URP Balanced 는 오브젝트당 추가 라이트가 2개까지라, 도시 바닥처럼
// 큰 한 메시는 가까운 두 개만 받는다. 게다가 라이트 수만큼 픽셀 비용이 늘어난다. 밤 분위기에 필요한 건 '바닥의 빛 웅덩이'라
// 가법 원판 하나면 같은 그림이 거의 공짜다(드로우 1, 오버드로 원판 넓이만큼).
// Proto_LightBeam 의 _Radial 모드는 각도 줄무늬가 있어 섬광용이고, 이건 매끈한 감쇠만 있다.
Shader "Proto/LightPool"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.0, 0.72, 0.38, 1.0)
        _Intensity ("Intensity", Range(0, 3)) = 0.6
        _Falloff ("Falloff (1=선형, 2=부드럽게)", Range(0.5, 4)) = 2.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Falloff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half r = saturate(length(i.uv - 0.5) * 2.0);          // 중심 0 → 가장자리 1
                half a = pow(1.0 - r, _Falloff);
                half3 rgb = _Color.rgb * (a * _Intensity);
                // 가법이라 안개는 '더할 빛을 줄이는' 쪽으로 — 멀리서 빛 웅덩이가 안개 속에 녹게
                rgb *= 1.0 - saturate(1.0 - i.fog) * 0.7;
                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }
}
