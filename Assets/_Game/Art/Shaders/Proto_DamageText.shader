// 데미지 숫자(ProtoDamageText). 숫자 전부가 한 메시 — 쿼드를 CPU 가 카메라 쪽으로 세워서 넘긴다.
// 아틀라스는 흰 글자 + 검은 외곽선이라 정점 색을 곱하면 글자만 물들고 외곽선은 검정으로 남는다. 알파도 정점 색.
// 깊이를 무시하고(ZTest Always) 가장 나중에 그린다 — 좀비 몸·건물에 가려지면 숫자의 의미가 없다.
// 큐는 밤 어둠 막(2900)·캐릭터(4001) 뒤라 밤에도 원래 색으로 보인다.
Shader "Proto/DamageText"
{
    Properties
    {
        _BaseMap ("Digits", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Overlay+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "DamageText"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                return half4(t.rgb * IN.color.rgb, t.a * IN.color.a);
            }
            ENDHLSL
        }
    }
}
