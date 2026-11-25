Shader "Proto/FlameSprite"
{
    // 화염 제트 플립북. 한 장의 쿼드에 아틀라스 한 칸을 그린다.
    //
    // 프레임 두 장을 섞는다(_FrameA/_FrameB/_FrameT). 15장뿐이라 그냥 끊어 넘기면
    // 20fps 로 재생해도 툭툭 튀는 게 보인다. 섞으면 재생 속도를 낮춰도 매끈하고,
    // 무엇보다 **루프가 되돌아가는 지점의 팝을 없앨 수 있다** — A 와 B 를 코드가
    // 직접 지정하므로 10번 프레임에서 5번으로 감을 때도 섞인다.
    //
    // 두 번 샘플하지만 같은 픽셀을 두 번 읽는 것뿐이라 채우기 비용은 한 장 값이다
    // (빗줄기 시트 Proto/RainSheet 와 같은 수법).
    Properties
    {
        _MainTex ("Atlas", 2D) = "black" {}
        _Grid      ("Grid (cols, rows)", Vector) = (4, 4, 0, 0)
        _FrameA    ("Frame A", Float) = 0
        _FrameB    ("Frame B", Float) = 1
        _FrameT    ("Blend A->B", Range(0,1)) = 0
        _Tint      ("Tint", Color) = (1,1,1,1)
        _TintMix   ("Tint Recolor (0 = 곱하기, 1 = 다시 칠하기)", Range(0,1)) = 0
        _Intensity ("Intensity", Range(0,4)) = 1
        // 기본값 Always. 제트는 노면에 거의 붙어 누워 있어서 깊이 테스트를 켜 두면
        // 도로가 불의 아랫부분을 **직선으로 잘라 먹는다**. 가법 블렌딩이라 위에 덧그려도
        // 밝아지기만 하고 가려지지 않는다 (번개 Proto/SpriteFlash 와 같은 이유).
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 8
        // 블렌드. 기본은 가산(SrcAlpha One — 예전 M_FlameSprite 그대로). 그을음 테두리가 있는 시트(화염방사기 시트, 2026-11-05) 는
        // 알파 블렌드(SrcAlpha OneMinusSrcAlpha) 로 그려야 검은 테두리가 남는다 — 가산이면 어두운 곳은 아무것도 더하지 않아 사라진다.
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "FlameSprite"
            Blend [_SrcBlend] [_DstBlend]   // 기본 가법(SrcAlpha One). 알파를 곱해 두므로 굽는 쪽에서 색을 순색으로 되돌려 놨다
            ZWrite Off
            ZTest [_ZTest]
            Cull Off                // 조준 방향에 따라 뒷면이 보인다

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Grid;
                float  _FrameA, _FrameB, _FrameT;
                float4 _Tint;
                float  _TintMix;
                float  _Intensity;
                float  _ZTest;
                float  _SrcBlend, _DstBlend;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = IN.uv;
                return o;
            }

            // 아틀라스 한 칸의 uv. 칸은 **왼쪽 위부터** 가로로 채워져 있는데 텍스처 v 는
            // 아래가 0 이라, 행을 뒤집어야 순서가 맞는다.
            float2 CellUV (float2 uv, float idx)
            {
                float cols = max(_Grid.x, 1.0);
                float rows = max(_Grid.y, 1.0);
                float c = fmod(idx, cols);
                float r = floor(idx / cols);
                return float2((uv.x + c) / cols, (uv.y + (rows - 1.0 - r)) / rows);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = IN.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                half4 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, CellUV(uv, _FrameA));
                half4 b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, CellUV(uv, _FrameB));
                half4 c = lerp(a, b, saturate(_FrameT));
                // _TintMix 1 = 다시 칠하기(인페르노). 아틀라스가 이미 주황이라 곱하면 파래지지 않으므로,
                // 밝기만 남기고 색은 _Tint 로 바꾼다. 가운데 백열은 흰색으로 남게 밝은 곳은 흰색 쪽으로 민다.
                half lum = dot(c.rgb, half3(0.299, 0.587, 0.114));
                half3 recolor = lerp(lum * _Tint.rgb * 1.6, lum.xxx * 1.6, saturate(lum * lum));
                c.rgb = lerp(c.rgb * _Tint.rgb, recolor, saturate(_TintMix)) * _Intensity;
                c.a   *= _Tint.a;
                return c;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
