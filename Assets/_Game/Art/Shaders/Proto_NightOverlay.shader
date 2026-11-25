// 밤 어둠 막 — 화면 전체를 어두운 색으로 덮고, 캐릭터 둘레(랜턴)와 가로등 자리만 동그랗게 뚫는다.
//
// **조명으로 어둡게 하지 않는 이유**(사용자 결정, 2026-09-30): 달빛+랜턴 포인트 라이트로 했더니 캐릭터가 랜턴에 하얗게 떴고,
// 저사양 품질(Performant)은 추가 라이트가 꺼져 랜턴이 아예 없었다. 막 한 장이면 어느 품질에서나 같고 조명 계산도 빠진다.
//
// **캐릭터는 원래 색 그대로** — 이 막은 큐 2900(불투명 배경·좀비 2000~2460 뒤, 이펙트 3000·캐릭터 4001 앞)에 그린다.
// 캐릭터는 ProtoGroundRing 이 4001 로 올리므로 막 위에 그려져 어둠을 안 탄다.
//
// 구멍은 화면 공간 원이지만 반경은 월드 m 로 받아 원근에 맞게 줄인다(멀리 있는 가로등 구멍은 작게).
// 점 투영과 픽셀 좌표 모두 URP 헬퍼(ComputeScreenPos / GetNormalizedScreenSpaceUV)로 구해 렌더 타깃 상하 반전에 안전하다.
Shader "Proto/NightOverlay"
{
    Properties
    {
        _DarkColor ("Darkness Color", Color) = (0.02, 0.03, 0.07, 1)
        _Darkness ("Darkness (0~1)", Range(0, 1)) = 0.75
        _WarmColor ("Lantern Warm Tint", Color) = (1.0, 0.72, 0.42, 1)
        _Warmth ("Lantern Warmth", Range(0, 0.4)) = 0.10
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define NIGHT_MAX_LIGHTS 16   // ProtoNightOverlay.MaxLights 와 같아야 한다(전역 배열은 첫 업로드 길이로 고정)

            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColor;
                half _Darkness;
                half4 _WarmColor;
                half _Warmth;
            CBUFFER_END

            // 전역(머티리얼 밖) — ProtoNightOverlay 가 매 프레임 올린다
            float4 _Night_Player;                         // xyz 월드(몸 가운데), w = 반경 m
            float4 _Night_Edge;                           // x = 플레이어 원 가장자리 폭(반경 대비), y = 가로등 원 가장자리 폭, z = 가로등 밝기, w = 캐릭터 빛 줄이는 양(시작 연출)
            float4 _Night_Lights[NIGHT_MAX_LIGHTS];       // xyz 월드, w = 반경 m
            float  _Night_LightCount;
            float4 _Night_Spread;                         // x = 착지 후 연출 섞기(0~1), y = 어두워지는 빠르기, z = 밝은 중심 반경(화면 높이 비), w = 최대 어둠
            float  _Night_Ambient;                        // 화면 전체 바닥 밝기 0~1 — 시작 연출 중에만 0 보다 크다(ProtoNightOverlay.IntroAmbient)

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 ndc : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                // 메시(-1~1 사각형)를 그대로 클립 공간 전체에 깐다 — 카메라 변환과 무관한 풀스크린.
                // 픽셀 위치도 이 NDC 를 보간해서 쓴다. **_ScreenParams 로 픽셀 좌표를 만들면 안 된다** — 에디터에서
                // 렌더 텍스처로 찍을 때 그 값이 실제 타깃 크기와 달라 구멍이 화면 밖으로 빠졌다(실제로 그랬다).
                Varyings o;
                o.positionCS = float4(v.positionOS.xy, UNITY_NEAR_CLIP_VALUE, 1.0);
                o.ndc = v.positionOS.xy;
                return o;
            }

            // 월드 점 p(반경 r m)가 NDC 위치 q 에 주는 빛(0~1). edge = 가장자리 부드러움(반경 대비 비율)
            half Hole(float2 q, float3 p, float r, float edge)
            {
                float4 cs = TransformWorldToHClip(p);
                // **카메라 바로 밑·뒤 점은 버린다.** 시점 깊이 w 가 작으면 원근 때문에 구멍이 화면 전체를 덮을 만큼 커진다 —
                // 화면에 보이지도 않는 발밑 가로등이 어둠을 통째로 걷어내 밤이 안 됐다(실제로 그랬다). 이 카메라(높이 15m,
                // 피치 58°)에서 보이는 땅은 w ≈ 15m 부터라 8m 미만은 전부 화면 밖이다.
                if (cs.w < 8.0) return 0;
                // 막의 픽셀(NDC)과 점의 투영 결과는 같은 클립 공간이라 그대로 비교한다. 렌더 타깃 상하 반전은 투영 행렬이
                // 월드 쪽을 뒤집고 래스터화가 막 쪽도 같은 공간에서 처리하므로 둘 다 같이 뒤집힌다 — 따로 보정하면 오히려 틀린다.
                float2 c = cs.xy / cs.w;
                float aspect = UNITY_MATRIX_P[1][1] / UNITY_MATRIX_P[0][0];          // 가로/세로 — 화면 크기 변수 대신 투영에서
                float rNdc = min(r * abs(UNITY_MATRIX_P[1][1]) / cs.w, 0.9);         // 월드 반경 → NDC 세로 반경(원근), 화면 절반 이하로 제한
                float d = length(float2((q.x - c.x) * aspect, q.y - c.y)) / max(rNdc, 1e-4);
                return 1.0 - smoothstep(1.0 - edge, 1.0, d);
            }

            // 착지 후용 — 남는 밝기 비율(1 = 그대로, 0 = 완전히 어둠). 캐릭터에서 화면 거리(화면 높이 단위)로 잰다.
            // 하나의 지수 곡선만 쓴다 — 월드 반경 빛 + 따로 덧씌운 화면 가장자리 어둠(비네트)을 겹쳤더니 둘이 만나는 곳에서
            // 밝기가 꺾여 큰 타원 경계로 보였다(2026-10-21 사용자 스크린샷). core = 완전히 밝은 중심 반경, k = 어두워지는 빠르기.
            half SoftKeep(float2 q, float3 p, float core, float k)
            {
                float4 cs = TransformWorldToHClip(p);
                if (cs.w < 8.0) return 1;
                float2 c = cs.xy / cs.w;
                float aspect = UNITY_MATRIX_P[1][1] / UNITY_MATRIX_P[0][0];
                float r = length(float2((q.x - c.x) * aspect, q.y - c.y)) * 0.5;   // NDC 세로 2 → 화면 높이 1
                return exp(-k * max(0.0, r - core));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 pix = i.ndc;
                half player = _Night_Player.w > 0 ? Hole(pix, _Night_Player.xyz, _Night_Player.w, _Night_Edge.x) : 0;
                half lamps = 0;
                int n = (int)_Night_LightCount;
                for (int k = 0; k < NIGHT_MAX_LIGHTS; k++)
                {
                    if (k >= n) break;
                    lamps = max(lamps, Hole(pix, _Night_Lights[k].xyz, _Night_Lights[k].w, _Night_Edge.y) * _Night_Edge.z);
                }
                half light = max(player, lamps);
                half a = _Darkness * (1.0 - light);

                // 착지 후(2026-10-21) — 밝은 곳이 넓고 부드럽게 퍼지고, 화면 끝으로 갈수록 어둡다.
                // 시작 연출의 또렷한 원(위 a)과 _Night_Spread.x 로 섞는다.
                if (_Night_Spread.x > 0.001 && _Night_Player.w > 0)
                {
                    half keep = SoftKeep(pix, _Night_Player.xyz, _Night_Spread.z, _Night_Spread.y);
                    // 시작 연출(낙하 중)엔 이 빛을 서서히 올린다 — _Night_Edge.w = 줄이는 양(0 = 평소, 1 = 꺼짐). 2026-11-10
                    keep *= 1.0 - saturate(_Night_Edge.w);
                    keep = max(keep, lamps);                                          // 가로등 아래는 그만큼 밝게
                    half warmKeep = keep;                                             // 따뜻한 색은 캐릭터·가로등 빛에만 — 바닥 밝기엔 안 얹는다
                    // 시작 연출 — 떨어지기 전엔 이 막을 거의 걷어 둔다(어둠은 연출의 막이 화면 전체에 고르게 맡는다). 2026-11-12
                    keep = max(keep, _Night_Ambient);
                    // keep 은 "눈에 보이는 밝기" 비율이다. 섞기는 리니어 공간이라 감마(2.2)로 바꿔 어둠 알파를 거꾸로 구한다 —
                    // 그냥 쓰면 어둠 50% 가 눈엔 20% 로 보여 넓게 하얗다가 끝에서 갑자기 꺼지는 경계가 되고, 95% 를 덮어도
                    // 화면 끝이 밝기 0.25 에서 멈췄다(2026-10-21 실측, 프로젝트 Linear). w = 가장 어두운 곳의 어둠(0~1, 눈 기준).
                    half seen = lerp(1.0 - _Night_Spread.w, 1.0, saturate(keep));
                    half aSpread = saturate(1.0 - pow(seen, 2.2));
                    a = lerp(a, aSpread, _Night_Spread.x);
                    player = lerp(player, warmKeep, _Night_Spread.x);                 // 따뜻한 색도 넓어진 빛을 따라간다
                }
                // 랜턴 원 안쪽은 아주 옅게 따뜻한 색을 얹는다(배경만 — 캐릭터는 막 뒤에 그려져 영향 없음)
                // 원 전체에 고르게 옅게 — 가장자리에만 몰리게 했더니 아스팔트 위에서 갈색 도넛 띠로 보였다
                half warm = player * _Warmth;
                half3 rgb = lerp(_DarkColor.rgb, _WarmColor.rgb, saturate(warm / max(a + warm, 1e-3)));
                half outA = saturate(a + warm);
                // 어두운 그러데이션은 8비트에서 동심원 띠가 지고, 채널마다 따로 계단이 져 띠에 분홍·녹색 기가 돈다(2026-11-10 미리보기 그림,
                // 어둠을 진하게 하면서 더 잘 보였다). 픽셀마다 화면 한 단계쯤 흩뜨린다 — 알갱이로는 안 보인다(Proto/IntroVeil 과 같은 방법).
                // 알파는 (1 - a) 에 비례해 흔든다: 거의 다 덮인 곳은 뒤 화면이 (1 - a) 만큼만 비치므로 그쪽을 흔들어야 띠가 풀린다.
                float dither = frac(52.9829189 * frac(dot(i.positionCS.xy, float2(0.06711056, 0.00583715)))) - 0.5;
                rgb = max(rgb + dither * (0.0006 + rgb * 0.03), 0.0);
                outA = saturate(outA + dither * 0.06 * (1.0 - outA));
                return half4(rgb, outA);
            }
            ENDHLSL
        }
    }
}
