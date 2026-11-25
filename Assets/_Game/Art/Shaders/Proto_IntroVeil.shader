// 시작 연출의 밤 어둠 막 (ProtoIntro).
//
// **설정**(사용자): 밤이라 어둡고, 캐릭터가 내려와 착지하면서 주변이 서서히 밝아진다 — 너무 밝게는 말고.
// 2026-11-12 "시작을 너무 어둡게 시작하는 것 같아" → 떨어지기 전에도 깜깜하지 않다. 화면 전체가 은은한 밝기(바닥 밝기, _Veil_Params.w)이고
// 그동안 평소 밤 어둠 막(Proto/NightOverlay)은 ProtoIntro 가 거의 걷어 둔다(_Night_Ambient) — 어둠을 이 막 하나가 고르게 맡아야
// 어둠을 안 타는 것들(아래)도 도시와 같은 밝기로 어두워진다. 캐릭터가 내려오면 밤 어둠 막이 돌아오고, 착지 뒤 이 막이 걷힌다.
// 안개는 없다(2026-11-12 사용자 "그냥 안개는 빼줘"). 그 전 판들의 안개 결(무늬 노이즈 · 회청색 결 · 빛 받은 안개)은
// "검은 안개"로 보이거나(어둠 색이 사실상 검정이었다) 은은하게 바꿔도 마음에 들지 않아 통째로 뺐다. 지금은 무늬 없는 고른 어둠뿐이다.
//
// **왜 따로 막을 두나.** 밤 어둠 막(Proto/NightOverlay)은 큐 2900 이라 그 뒤에 그려지는 것들은 어둠을 안 탄다 —
// 차 유리·물·바닥 안개 시트(3000~3100), 픽업·체력 막대(3100~), 필드에 놓인 탱크(4001), 건물 비쳐 보이기(4051).
// 평소엔 캐릭터 둘레가 밝아 티가 덜 나는데, 시작 연출은 불을 다 꺼 둔 채 멀리서 넓게 보니 그것들만 제 색으로 떠 보였다.
// 이 막은 큐 4060(Overlay+60)에 그려 **그것들까지 전부** 덮는다. 비행기·낙하산·캐릭터만 ProtoIntro 가 4062 로 올려 막 위에 그린다
// (4061 은 시작 안개 Proto/IntroFog — 막 위 · 캐릭터 아래. 11-12 오후).
// (11-12 이전엔 빛이 0 인 곳을 불투명하게 덮었다 — 그땐 밤 어둠 막도 같이 깔려 있어 도시는 거의 검정, 탱크·유리만 비쳐 보였기 때문.)
// 어둠 색은 ProtoIntro 가 밤 어둠 막 머티리얼(M_City_NightOverlay)의 색을 그대로 넣는다 — 막이 걷힐 때 색이 바뀌지 않는다.
//
// 빛은 화면이 아니라 **바닥(월드) 좌표**로 잰다 — 픽셀마다 시선이 바닥과 만나는 점을 구한다.
// 그래서 카메라가 당겨 들어와도 밝은 자리가 땅에 붙어 있다. 깊이 텍스처를 쓰지 않는다(모바일 — 텍스처 한 장도 안 쓴다).
// 시선 방향은 뷰 행렬의 축과 투영 행렬의 [0][0]·[1][1] 로 만든다. 역행렬(UNITY_MATRIX_I_VP)을 쓰지 않는 이유:
// URP 가 렌더 타깃 상하 반전을 투영 쪽에만 넣는 경로가 있어 역행렬이 어긋날 수 있다. P[1][1] 은 반전되면 부호가 바뀌므로
// 그대로 나누면 막(클립 공간에 직접 깐 사각형)과 같은 쪽으로 뒤집힌다 — Proto/NightOverlay 와 같은 이유.
//
// 색 값은 화면에 보이는 sRGB 값이다 — 이 프로젝트는 Linear 라 Color 속성은 sRGB 로 읽혀 리니어로 바뀐다.
Shader "Proto/IntroVeil"
{
    Properties
    {
        // 기본값 = M_City_NightOverlay 의 _DarkColor. 밤 씬에서는 ProtoIntro 가 그 머티리얼 색으로 덮어쓴다
        _DarkColor  ("밤 어둠 색", Color) = (0.01, 0.015, 0.045, 1)
        // ProtoIntro 가 매 프레임 넣는다
        [HideInInspector] _Veil_Center ("착지 지점 xyz, w = 빛 반경 m", Vector) = (0, 0, 0, 4)
        [HideInInspector] _Veil_Params ("x 빛 세기 0~1, y 앞쪽 배율, z 뒤쪽 배율, w 화면 전체 바닥 밝기 0~1(1 = 막 없음)", Vector) = (0, 0.7, 1.15, 0.25)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay+60" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "IntroVeil"
            // 미리 곱한 알파
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _DarkColor;
                float4 _Veil_Center;
                float4 _Veil_Params;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 ndc : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                // -1~1 사각형을 클립 공간 전체에 그대로 깐다(카메라 변환과 무관한 풀스크린)
                Varyings o;
                o.positionCS = float4(v.positionOS.xy, UNITY_NEAR_CLIP_VALUE, 1.0);
                o.ndc = v.positionOS.xy;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 이 픽셀의 시선이 바닥(y=0)과 만나는 점
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up    = UNITY_MATRIX_V[1].xyz;
                float3 fwd   = -UNITY_MATRIX_V[2].xyz;
                float3 dir = fwd + right * (i.ndc.x / UNITY_MATRIX_P[0][0]) + up * (i.ndc.y / UNITY_MATRIX_P[1][1]);
                float3 o = _WorldSpaceCameraPos;
                float t = dir.y < -1e-4 ? min(o.y / -dir.y, 300.0) : 300.0;   // 하늘을 보는 픽셀은 아주 먼 곳으로 친다
                float2 g = o.xz + dir.xz * t;

                // 착지 지점에서 빛이 번진다. 카메라 쪽(화면 앞)은 가깝게, 먼 쪽은 멀게 쳐서 앞쪽이 조금 먼저 밝아진다
                float2 f2 = fwd.xz / max(length(fwd.xz), 1e-4);
                float2 dv = g - _Veil_Center.xz;
                float s = dot(dv, f2);                         // + = 카메라에서 먼 쪽, - = 카메라 쪽
                float l = dot(dv, float2(-f2.y, f2.x));
                s *= s < 0.0 ? _Veil_Params.y : _Veil_Params.z;
                float R = max(_Veil_Center.w, 0.01);
                float d2 = (l * l + s * s) / (R * R);
                // 가우스(exp)는 반경 근처에서 뚝 떨어져 동그란 원으로 읽힌다 — 꼬리가 긴 곡선으로 은은하게 번지게 한다
                float fall = 1.0 / (1.0 + d2);
                half lit = saturate(_Veil_Params.x * fall * sqrt(fall));      // 반경에서 0.35, 두 배에서 0.09
                // 눈에 보이는 밝기 비율(0 = 깜깜, 1 = 그대로). 바닥 밝기 위에 빛을 얹는다 — max 로 하면 둘이 만나는 곳이 꺾여 원 경계가 보인다
                half floorKeep = saturate(_Veil_Params.w);
                half keep = floorKeep + (1.0 - floorKeep) * lit;
                // 섞기는 리니어 공간이라 눈 기준 밝기를 알파로 바꾼다(감마 ≈ 2) — Proto/NightOverlay 와 같은 이유
                half a = 1.0 - keep * keep;
                half3 rgb = _DarkColor.rgb * a;

                // 어두운 그러데이션은 8비트에서 동심원 띠가 지고, 검정 근처에선 채널마다 따로 계단이 져 띠에 색 기가 돈다.
                // 픽셀마다 화면 한 단계쯤 흩뜨린다(Proto/NightOverlay 와 같은 방법). 알갱이로는 안 보인다.
                float dither = frac(52.9829189 * frac(dot(i.positionCS.xy, float2(0.06711056, 0.00583715)))) - 0.5;
                rgb = max(rgb + dither * (0.0006 + rgb * 0.03), 0.0);
                a = saturate(a + dither * 0.06 * (1.0 - a));
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
