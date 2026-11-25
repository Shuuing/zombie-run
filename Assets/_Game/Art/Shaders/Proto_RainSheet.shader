// 화면 앞 빗줄기 시트. **쿼드 한 장이 비 전체를 그린다.**
//
// 왜 파티클을 버렸나: 측정해 보니 비의 비용은 화면 채우기가 아니라 **입자 개수**였다.
// 크기를 2배(면적 4배)로 키워도 비용이 그대로인데 개수를 1/4 로 줄이면 55% 가 빠졌다 —
// 빌트인 파티클이 매 프레임 CPU 에서 정점을 새로 쓰기 때문이다(823개 = 정점 3,300개).
// 빗줄기는 통계적으로 균일해서 고유한 실루엣이 없다. 타일링 텍스처를 흘리는 것과
// 입자를 뿌리는 것이 눈으로 구분되지 않는다 — 번개와 정반대 성격이다.
//
// **한 패스에서 두 번 샘플한다.** 원경·근경을 쿼드 두 장으로 만들면 채우기가 두 배가 되는데,
// 모바일에서 진짜 위험한 건 그 채우기다. 같은 픽셀에서 UV 만 다르게 두 번 읽으면
// 시차는 그대로 얻고 채우기는 한 장 값만 낸다.
Shader "Proto/RainSheet"
{
    Properties
    {
        _MainTex ("빗줄기 (세로 타일링)", 2D) = "black" {}
        _Color ("색", Color) = (0.72, 0.78, 0.88, 1.0)

        _Opacity ("전체 불투명도", Range(0, 1)) = 0.5

        // 근경: 크고 빠르다. 원경: 작고 느리다. 둘이 겹쳐 깊이가 생긴다.
        _NearTile ("근경 타일 (x,y)", Vector) = (2.0, 1.2, 0, 0)
        _NearSpeed ("근경 속도", Float) = 2.6
        _FarTile ("원경 타일 (x,y)", Vector) = (4.5, 2.6, 0, 0)
        _FarSpeed ("원경 속도", Float) = 1.5
        _FarWeight ("원경 비중", Range(0, 1)) = 0.55

        // 바람: UV 를 옆으로도 흘려 비가 비스듬히 내리게 한다.
        _Slant ("기울기", Range(-1, 1)) = 0.18

        // 아래쪽(도로)에서 옅어지게 한다. 화면 전체를 같은 농도로 덮으면 바닥이 뿌옇게 죽는다.
        _BottomFade ("아래 페이드", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType"    = "Plane"
        }

        Pass
        {
            Name "RainSheet"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // 카메라 바로 앞에 고정으로 붙는 판이라 깊이를 지킬 대상이 없다.
            // Always 로 두어야 프롭 뒤에 숨는 일이 없다.
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _NearTile;
                float4 _FarTile;
                float  _Opacity;
                float  _NearSpeed;
                float  _FarSpeed;
                float  _FarWeight;
                float  _Slant;
                float  _BottomFade;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // 두 겹을 같은 픽셀에서 UV 만 다르게 읽는다 (쿼드는 한 장뿐이다).
                // **부호 주의.** uv.y 를 빼면 화면에서는 위로 흐른다(텍스처 좌표와 화면 이동이 반대다).
                // 비는 내려와야 하므로 더한다 — 처음에 빼놨다가 비가 하늘로 솟았다.
                float2 uvN = IN.uv * _NearTile.xy;
                uvN.y += t * _NearSpeed;
                uvN.x += t * _NearSpeed * _Slant;

                float2 uvF = IN.uv * _FarTile.xy + float2(0.37, 0.61);   // 오프셋을 줘야 두 겹이 겹쳐 보이지 않는다
                uvF.y += t * _FarSpeed;
                uvF.x += t * _FarSpeed * _Slant;

                half near = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvN).a;
                half far  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvF).a;

                half a = saturate(near + far * _FarWeight);

                // 화면 아래쪽에서 옅어진다 (uv.y = 0 이 아래).
                a *= lerp(1.0 - _BottomFade, 1.0, saturate(IN.uv.y * 1.6));

                return half4(_Color.rgb, a * _Opacity * _Color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
