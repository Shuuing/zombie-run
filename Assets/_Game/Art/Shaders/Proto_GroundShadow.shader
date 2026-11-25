// 바닥에 깔리는 그림자 텍스처(융단 폭격의 공격헬기 그림자 등).
// 텍스처 알파 × _BaseColor.a 만큼 _BaseColor 색으로 어둡게 덮는다. 깊이는 쓰지 않고(ZWrite Off) 테스트만 한다 —
// 좀비가 그림자 위에 서 있으면 몸통이 그림자를 가린다. 큐는 좀비(≤2460) 뒤라 발밑 바닥에만 깔린다.
Shader "Proto/GroundShadow"
{
    Properties
    {
        _BaseMap ("Shadow (alpha)", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0.02, 0.03, 0.06, 0.5)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Geometry+470"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "GroundShadow"
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

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                return half4(_BaseColor.rgb, a);
            }
            ENDHLSL
        }
    }
}
