// 스프라이트 연출용 가법 언릿 — SpriteRenderer 전용.
//
// 이 프로젝트의 다른 VFX 셰이더(폭발·빛기둥·화염 콘)는 전부 절차적이다. 여기만 텍스처를
// 쓰는 이유는 하나다: **번개 가지는 절차적으로 못 만든다.** 프랙탈 노이즈로 만든 지그재그는
// 굵기가 일정해서 "전선"처럼 보이고, 착탄 순간의 방사 파편은 노이즈로 흉내가 안 난다.
// 아티스트가 그린 16장을 그대로 넘기는 편이 압도적으로 싸고 좋다.
//
// 가법(Blend SrcAlpha One)인 이유: 번개는 빛이다. 뒤에 무엇이 있든 더하기만 하면 정렬 문제가
// 없고, 검정(=알파 0 자리)은 자동으로 사라진다. 대신 밝은 하늘 위에서는 약해 보인다.
//
// ZTest Always 인 이유: 캐릭터 교체 연출은 캐릭터 **위**에 떨어져야 읽힌다. 깊이를 지키면
// 빌보드가 캐릭터 메시를 뚫고 지나가면서 번개 밑동이 몸통에 잘려 들어간다
// (발밑 링 `Proto/OverlayUnlit` 이 같은 이유로 깊이를 버렸다).
Shader "Proto/SpriteFlash"
{
    Properties
    {
        // SpriteRenderer 는 스프라이트 아틀라스를 _MainTex 에 직접 밀어 넣는다.
        // [PerRendererData] 를 빼면 머티리얼에 박힌 텍스처가 이겨서 전 프레임이 같은 그림이 된다.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Tint ("색조", Color) = (0.72, 0.88, 1.0, 1.0)
        _Intensity ("밝기 (런타임)", Range(0, 8)) = 1.6
        // 코어를 하얗게 태우는 정도. 0 이면 전부 _Tint 색으로 물들어 네온사인처럼 보인다.
        _CoreBurn ("코어 백열", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType"    = "Plane"
        }

        Pass
        {
            Name "SpriteFlash"

            Blend SrcAlpha One
            ZWrite Off
            ZTest Always
            Cull Off
            Lighting Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
                float  _Intensity;
                float  _CoreBurn;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;      // SpriteRenderer.color (페이드아웃이 여기로 온다)
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 굽는 단계에서 알파는 "덮인 정도", RGB 는 언믹스한 실제 색이다.
                // 알파가 높은 심지일수록 흰색으로 끌어올려 백열시킨다 — 색조만 곱하면
                // 가장 밝은 코어까지 파랗게 물들어서 번개가 아니라 네온 튜브로 읽힌다.
                half core = saturate(tex.a * tex.a);
                half3 tint = lerp(_Tint.rgb, half3(1, 1, 1), core * _CoreBurn);

                half3 rgb = tex.rgb * tint * IN.color.rgb * _Intensity;
                half  a   = tex.a * IN.color.a * _Tint.a;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
