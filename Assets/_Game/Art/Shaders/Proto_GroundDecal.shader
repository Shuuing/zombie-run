// 바닥 데칼(피 얼룩·타이어 자국 등) — 텍스처 색을 알파로 얹는다.
// 처음엔 바닥에 곱했는데(Blend DstColor Zero) 어두운 아스팔트 × 진한 빨강 = 검갈색이라 피가 기름 얼룩처럼 보였다 → 알파 블렌드.
// 조명을 안 받으므로 _Bright 로 장면 밝기에 맞춘다. 밤은 화면 어둠 막(큐 2900)이 뒤에 덮어 같이 어두워진다.
Shader "Proto/GroundDecal"
{
    Properties
    {
        _BaseMap ("Decal (RGBA)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Strength ("Strength", Range(0, 1)) = 1
        _Bright ("Brightness", Range(0, 1.5)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Geometry+455"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "GroundDecal"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; half a : TEXCOORD1; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float  _Strength;
                float  _Bright;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.a = IN.color.a;   // 데칼마다 진하기(정점 알파)
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half k = saturate(c.a * _Strength * IN.a);
                return half4(c.rgb * _Tint.rgb * _Bright, k);
            }
            ENDHLSL
        }
    }
}
