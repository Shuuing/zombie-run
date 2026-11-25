// 풀 상호작용 필드 (ProtoGrassField). 플레이어를 따라다니는 40m 텍스처 한 장. R = 발자국, G = 폭발.
// 패스 0 (Graphics.Blit): 원점이 움직인 만큼 밀고(scroll) 채널별로 회복(값을 깎는다). 매 프레임 한 번.
// 패스 1 (GL 사각형): 밟힌 자리를 부드러운 원으로 찍는다. Max 블렌드 — 겹쳐 밟아도 더 세지지 않는다.
//   세기는 uv1(TEXCOORD1)로 받는다 — GL.Color 는 8비트로 잘려 1 넘는 값(눌린 채 유지하는 시간)을 못 넘긴다.
Shader "Proto/GrassField"
{
    Properties
    {
        _MainTex ("", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        ENDHLSL

        Pass
        {
            Name "ScrollRecover"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _Scroll;   // xy = uv 이동량, zw = 이번 프레임 회복량(R, G)

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o; }

            float4 frag(V i) : SV_Target
            {
                float2 uv = i.uv + _Scroll.xy;
                float2 v = 0;
                if (all(uv >= 0.0) && all(uv <= 1.0)) v = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rg;
                v = max(0, v - _Scroll.zw);
                return float4(v, 0, 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Stamp"
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float3 strength : TEXCOORD1; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 strength : TEXCOORD1; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; o.strength = i.strength.xy; return o; }

            float4 frag(V i) : SV_Target
            {
                float f = saturate(1 - dot(i.uv, i.uv));
                f = f * f * (3 - 2 * f);
                return float4(i.strength * f, 0, 0);
            }
            ENDHLSL
        }
    }
}
