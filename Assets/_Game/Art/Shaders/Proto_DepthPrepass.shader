// 가림막 반투명용 깊이 선기록. 색은 안 쓰고 깊이만 쓴다.
//
// 건물을 반투명으로 그리면 앞벽 너머로 뒷벽·지붕 안쪽·창틀이 겹쳐 비쳐 어수선해진다(알파 블렌딩은 ZWrite 를 끄므로).
// 같은 메시를 이 셰이더로 먼저 그려 '가장 앞면'의 깊이만 남기고, 이어서 반투명 머티리얼(ZTest LEqual)이
// 그 앞면만 칠하게 한다 — 유리 한 장처럼 보인다. ProtoSeeThrough 가 페이드 중인 렌더러에만 잠깐 붙인다.
//
// 큐 4050: **캐릭터가 다 그려진 뒤여야 한다.** 먼저 그리면 이 깊이에 막혀 건물 뒤 캐릭터가 아예 안 그려진다.
// 캐릭터 머티리얼은 에셋에선 2000 이지만 **플레이 중 ProtoGroundRing 이 4001 로 올린다**(발밑 링 4000 위에 그리려고).
// 처음에 3050 으로 뒀다가 에디터 렌더에선 멀쩡하고 플레이에서만 캐릭터가 사라졌다. 반투명 색 패스는 4051.
Shader "Hidden/Proto/DepthPrepass"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay+50" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
