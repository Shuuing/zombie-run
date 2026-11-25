// 항상 화면 맨 위에 그려지는 단색 언릿 셰이더.
//
// 왜 필요한가: 발밑 링을 평범한 머티리얼로 깔면 링 위에 서 있는 좀비 몸통에 가려진다.
// 좀비 더미에 묻힌 플레이어의 위치를 알려주는 것이 링의 존재 이유이므로, 깊이 테스트를
// 무시하고(ZTest Always) 오버레이 큐에 그려야 목적을 달성한다.
Shader "Proto/OverlayUnlit"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.35, 1.0, 0.45, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Overlay"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "OverlayUnlit"

            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(_BaseColor.rgb, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
