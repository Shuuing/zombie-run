// 좀비 오버런 — 플랫 셀 스타일 물 (URP)
//
// 레퍼런스 GIF 의 손으로 그린 듯한 물. 굴절·반사·화면공간 포말을 쓰지 않는다 →
// _CameraDepthTexture / _CameraOpaqueTexture 가 모두 필요 없다. 이 프로젝트의 두 모바일
// 프리셋(URP-Balanced / URP-Performant)은 둘 다 꺼져 있고, 켜면 뎁스 프리패스 + 전체화면
// 컬러 복사 비용이 붙는다. pitch 58° 탑다운에서는 굴절이 거의 안 보여 값을 못 한다.
//
// 대신 필요한 정보를 메시 정점 컬러에 구워서 쓴다 (Blender 생성 스크립트가 만든다):
//   R = 깊이     0(물가) ~ 1(중앙)
//   G = 포말     1(물가/낙하지점) ~ 0(안쪽)
//   B = 흐름 좌표 0(위) ~ 1(아래)  — 폭포 낙하 진행도
//
// 기본은 텍스처 0장 — 물결·포말 무늬를 셰이더 안에서 값 노이즈로 만든다.
// 손으로 그린 무늬를 쓰고 싶으면 아래 두 슬롯에 텍스처를 넣고 토글을 켜면 된다.
// 토글이 꺼져 있으면 샘플러가 아예 컴파일되지 않아 예전과 완전히 같은 비용이다.
Shader "BCM/BCM_StylizedWater"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("Shallow", Color)      = (0.42, 0.62, 0.58, 0.55)
        _DeepColor    ("Deep", Color)         = (0.09, 0.22, 0.26, 0.92)
        _FoamColor    ("Foam", Color)         = (0.86, 0.92, 0.90, 1.0)
        _RippleColor  ("Ripple Line", Color)  = (0.72, 0.85, 0.84, 1.0)
        [IntRange] _ColorSteps ("Color Steps", Range(2, 8)) = 3

        [Header(Ripple Lines)]
        _RippleScale   ("Scale", Range(0.2, 8))      = 1.6
        _RippleSpeed   ("Speed", Range(0, 3))        = 0.28
        _RippleWidth   ("Line Width", Range(0.01, 0.5)) = 0.11
        _RippleAmount  ("Amount", Range(0, 1))       = 0.55
        _RippleStretch ("Stretch (flow dir)", Range(1, 12)) = 2.5

        [Header(Foam)]
        _FoamWidth  ("Shore Width", Range(0, 1))   = 0.42
        _FoamWobble ("Wobble", Range(0, 1))        = 0.35
        _FoamSpeed  ("Wobble Speed", Range(0, 4))  = 0.9

        [Header(Flow)]
        _FlowDir   ("Flow Dir (XZ)", Vector) = (0, 1, 0, 0)
        _FlowSpeed ("Flow Speed", Range(0, 4)) = 0.35

        [Header(Textures    optional)]
        // 무늬 텍스처 — 물결 선(호수)과 세로 줄무늬(폭포)를 만드는 노이즈를 이걸로 대체한다.
        // R = 물결/줄무늬, G = 물가 포말 흔들림. 회색조 한 장이면 R=G 라 그대로 쓸 수 있다.
        // 스크롤하며 반복 샘플하므로 반드시 이음매 없는(seamless) 텍스처여야 한다.
        [Toggle(BCM_WATER_PATTERNTEX)] _UsePatternTex ("Use Pattern Texture", Float) = 0
        _PatternTex ("Pattern (R=ripple  G=foam wobble)", 2D) = "gray" {}

        // 표면 텍스처 — 물색 위에 직접 입힌다. Detail 은 회색조 디테일용(중간회색 0.5 에서 무변화),
        // Tint 는 색이 칠해진 물 텍스처를 그대로 쓰고 싶을 때.
        [Toggle(BCM_WATER_SURFACETEX)] _UseSurfaceTex ("Use Surface Texture", Float) = 0
        _SurfaceTex ("Surface (RGB)", 2D) = "white" {}
        _SurfaceDetail ("Surface Detail (x2 multiply)", Range(0, 1)) = 0.6
        _SurfaceTint   ("Surface Tint (replace color)", Range(0, 1)) = 0
        _SurfaceScroll ("Surface Scroll By Flow", Range(0, 1)) = 1

        [Header(Shoreline    optional)]
        // 물가 선 자체를 움직인다. 정점 컬러 R(깊이)을 노이즈 임계값으로 잘라내서
        // 물이 밀려왔다 빠지는 것처럼 보이게 한다 — 포말도 그 선을 따라간다.
        // 물 메시가 의도한 물가보다 _ShoreCarve 만큼 바깥으로 더 나와 있어야 한다.
        [Toggle(BCM_WATER_SHORELINE)] _UseShoreline ("Animate Waterline", Float) = 0
        _ShoreCarve  ("Carve Depth", Range(0, 0.5))          = 0.12
        _ShoreSoft   ("Edge Softness", Range(0.001, 0.3))    = 0.04
        _ShoreFoam   ("Lap Foam Width", Range(0, 0.5))       = 0.10
        _ShoreScale  ("Wave Scale", Range(0.2, 12))          = 3
        _ShoreSpeed  ("Wave Speed", Range(0, 3))             = 0.5
        _ShoreRhythm ("Rhythm (noise to pulse)", Range(0, 1)) = 0.5

        [Header(Contact and Ripples    optional)]
        // 캐릭터가 물에 들어간 자리에 생기는 파문/접촉 띠.
        // 뎁스 텍스처(수면 깊이 - 씬 깊이) 방식이 아니다 — 이 셰이더 머리말대로 이 프로젝트는
        // _CameraDepthTexture 가 꺼져 있고 켜면 뎁스 프리패스가 통째로 붙는다.
        // 대신 WaterContactFoam.cs 가 (위치, 반지름, 세기, 두께)를 전역 배열로 올려주고
        // 여기서 수면에 원을 하나씩 그린다. 반지름이 커지고 세기가 빠지면 퍼지는 파문이 되고,
        // 반지름이 고정이면 몸에 붙은 접촉 띠가 된다 — 그리는 쪽은 같다.
        //
        // **아래 Width/Inner/Strength 는 WaterContactFoam.cs 가 점마다 덮어쓴다.**
        // 배열이 비었을 때의 기본값일 뿐이니, 값 조절은 캐릭터의 컴포넌트에서 할 것.
        [Toggle(BCM_WATER_CONTACT)] _UseContactFoam ("Contact and Ripples", Float) = 0
        _ContactWidth    ("Band Width (fallback)", Range(0.02, 1))     = 0.45
        _ContactInner    ("Inner Fill (fallback)", Range(0, 1))        = 0.25
        _ContactWobble   ("Wobble", Range(0, 1))            = 0.30
        _ContactWobScale ("Wobble Scale", Range(0.2, 12))   = 7
        _ContactSpeed    ("Wobble Speed", Range(0, 4))      = 1.1
        _ContactYRange   ("Height Range", Range(0.05, 4))   = 0.7
        _ContactStrength ("Strength", Range(0, 1))          = 1

        [Header(Surface Waves    optional)]
        // 수면을 정점에서 위아래로 흔든다. 물가(깊이 0)는 고정해 지형에 파묻히지 않게 한다.
        [Toggle(BCM_WATER_WAVES)] _UseWaves ("Vertex Waves", Float) = 0
        _WaveHeight ("Height", Range(0, 0.5)) = 0.06
        _WaveScale  ("Scale", Range(0.1, 8))  = 1.5
        _WaveSpeed  ("Speed", Range(0, 6))    = 1.2

        [Header(Mode)]
        [Toggle(BCM_WATER_FALL)] _WaterfallMode ("Waterfall Mode", Float) = 0
        _FallStreak ("Fall Streak Density", Range(1, 40)) = 14
        _FallSpeed  ("Fall Speed", Range(0, 8))  = 2.6

        [Header(Lighting)]
        [Toggle(BCM_WATER_LIGHTTINT)] _UseLightTint ("Tint By Main Light", Float) = 1
        _LightTint ("Light Tint Amount", Range(0, 1)) = 0.35
        _Glossy    ("Sun Glint", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        // 큐 2600: 캐릭터 발밑 블롭 그림자(2450)보다 뒤에 그려 물이 그림자를 덮는다
        // (물속 그림자는 물빛에 잠기는 게 맞다). 일반 Transparent(3000)보다는 앞에 둬서
        // 이후에 추가될 파티클/UI 계열이 물 위에 올라오게 한다.
        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off            // 폭포 시트는 양면이 보여야 한다

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local BCM_WATER_FALL
            #pragma shader_feature_local_fragment BCM_WATER_LIGHTTINT
            #pragma shader_feature_local_fragment BCM_WATER_PATTERNTEX
            #pragma shader_feature_local_fragment BCM_WATER_SURFACETEX
            #pragma shader_feature_local_fragment BCM_WATER_SHORELINE
            #pragma shader_feature_local_fragment BCM_WATER_CONTACT
            #pragma shader_feature_local BCM_WATER_WAVES
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Game/Art/Shaders/RainRipple.hlsl"   // 빗방울 파문(전역 _Rain_Ripple — 비가 안 오면 0)

            CBUFFER_START(UnityPerMaterial)
                half4  _ShallowColor, _DeepColor, _FoamColor, _RippleColor;
                float  _ColorSteps;
                float  _RippleScale, _RippleSpeed, _RippleWidth, _RippleAmount, _RippleStretch;
                float  _FoamWidth, _FoamWobble, _FoamSpeed;
                float4 _FlowDir;
                float  _FlowSpeed;
                float  _FallStreak, _FallSpeed;
                half   _LightTint, _Glossy;
                // 토글과 무관하게 항상 선언한다 — 변형(variant)마다 CBUFFER 레이아웃이 달라지면
                // SRP 배처가 배치를 끊는다.
                float4 _PatternTex_ST, _SurfaceTex_ST;
                float  _SurfaceDetail, _SurfaceTint, _SurfaceScroll;
                float  _ShoreCarve, _ShoreSoft, _ShoreFoam, _ShoreScale, _ShoreSpeed, _ShoreRhythm;
                float  _WaveHeight, _WaveScale, _WaveSpeed;
                float  _ContactWidth, _ContactInner, _ContactWobble, _ContactWobScale;
                float  _ContactSpeed, _ContactYRange, _ContactStrength;
            CBUFFER_END


            // ── 접촉 포말 전역 ──────────────────────────────────────────
            // 머티리얼이 아니라 WaterContactFoam.cs 가 매 프레임 올리는 전역이다.
            // UnityPerMaterial 안에 넣으면 안 된다 — 전역은 머티리얼별 값이 아니고,
            // 넣는 순간 SRP 배처가 이 셰이더의 배치를 끊는다.
            // 길이는 C# 의 WaterContactFoam.MaxContacts 와 반드시 같아야 한다.
            // 루프는 실제 개수(_BCM_WaterContactCount)까지만 돌기 때문에 **남는 칸은 비용이 0**이다
            // (상수 버퍼 512바이트뿐). 전역 배열 길이는 프로세스에서 한 번 고정되므로
            // 늘릴 때마다 에디터를 다시 켜야 한다 — 그래서 넉넉하게 16 으로 잡았다.
            #define BCM_MAX_CONTACTS 16
            float4 _BCM_WaterContacts[BCM_MAX_CONTACTS];   // xz = 월드 위치, y = 수면 높이, w = 반지름
            float4 _BCM_WaterContactFx[BCM_MAX_CONTACTS];  // x = 세기, y = 띠 폭(반지름 대비), z = 안쪽 채움, w = 흔들림 배수
            float  _BCM_WaterContactCount;
            TEXTURE2D(_PatternTex);  SAMPLER(sampler_PatternTex);
            TEXTURE2D(_SurfaceTex);  SAMPLER(sampler_SurfaceTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ── 값 노이즈 ────────────────────────────────────────────────
            // 텍스처를 쓰지 않으려고 해시 기반으로 만든다. 셀 스타일에는 이 정도 해상도로 충분하고
            // 텍스처 페치가 없어 모바일에서 대역폭을 먹지 않는다.
            // 격자 반복 주기 (노이즈 유닛). 아래 주석 참조.
            #define BCM_NOISE_PERIOD 128.0

            float hash21(float2 p)
            {
                // 시간 스크롤(_FlowSpeed/_RippleSpeed/_FallSpeed)이 누적되면 p 가 수천~수만까지
                // 커진다. 그 상태로 456.21 을 곱하면 float 유효자리를 넘겨 frac() 결과가 계단으로
                // 뭉개지고, 화면에 스크롤 방향과 직각인 규칙적인 줄무늬가 생긴다.
                // (실제로 강에서 났다. _FlowSpeed 를 0.35 -> 0.75 로 올리자 더 심해졌다.)
                //
                // 격자 좌표를 유한한 주기로 감아 입력을 항상 작게 유지한다. vnoise 가 i 와 i+1 을
                // 같이 샘플하므로 경계에서도 정확히 이어진다 — 이음매 없이 반복될 뿐이다.
                // 128 유닛 반복은 _RippleScale 9 / UV 0.22 기준 약 64m 라 화면 밖이다.
                p = p - BCM_NOISE_PERIOD * floor(p / BCM_NOISE_PERIOD);
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);                 // smoothstep 보간
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            /// 두 옥타브면 셀 무늬로 충분하다. 옥타브를 늘려도 계조에 묻힌다.
            float fbm2(float2 p)
            {
                return vnoise(p) * 0.65 + vnoise(p * 2.17 + 3.1) * 0.35;
            }

            /// 값을 n 계단으로 양자화 — 플랫 셀 룩의 핵심
            float quantize(float v, float n)
            {
                n = max(2.0, n);
                // min() 이 없으면 v 가 정확히 1.0 일 때 floor(1*n)/(n-1) = n/(n-1) > 1 이 나온다.
                // 그 값이 lerp 에 들어가면 _DeepColor 를 넘어서 외삽된다. v=0.9999 는 정상적으로
                // 1.0 이 나오므로, 넓은 면이 깊이 1.0 로 구워져 있으면 보간·반올림에 따라
                // 픽셀마다 1.0 과 1.333 을 오가며 하드한 줄무늬가 생긴다 (강에서 실제로 났다).
                return min(floor(saturate(v) * n), n - 1.0) / (n - 1.0);
            }

            /// 표면 텍스처를 물색 위에 입힌다. 포말·물결 선보다 **먼저** 부르는 게 중요하다 —
            /// 나중에 부르면 하얗게 남아야 할 포말까지 텍스처 무늬가 먹는다.
            half4 ApplySurfaceTex(half4 col, float2 uv, float2 flowDir, float t)
            {
            #ifdef BCM_WATER_SURFACETEX
                float2 suv = uv * _SurfaceTex_ST.xy + _SurfaceTex_ST.zw;
                suv += flowDir * (t * _FlowSpeed * _SurfaceScroll);
                half4 s = SAMPLE_TEXTURE2D(_SurfaceTex, sampler_SurfaceTex, suv);
                // Detail: 중간회색(0.5)에서 변화 없음 — 밝은 곳은 밝히고 어두운 곳은 어둡게
                col.rgb = lerp(col.rgb, saturate(col.rgb * s.rgb * 2.0), _SurfaceDetail);
                // Tint: 텍스처 색을 그대로 얹는다
                col.rgb = lerp(col.rgb, s.rgb, _SurfaceTint);
            #endif
                return col;
            }

            /// 파문/접촉 띠 — 점 하나당 수면 위의 원 하나.
            /// 원 그대로 두면 CG 같아서 테두리를 값 노이즈로 흔든다. 물결과 같은 노이즈라 톤이 맞는다.
            float ContactFoam(float3 posWS, float t)
            {
            #ifdef BCM_WATER_CONTACT
                float acc = 0.0;
                int n = (int)min(_BCM_WaterContactCount, (float)BCM_MAX_CONTACTS);

                for (int i = 0; i < n; i++)
                {
                    float4 c  = _BCM_WaterContacts[i];
                    float4 fx = _BCM_WaterContactFx[i];
                    float  str  = fx.x * _ContactStrength;
                    if (str <= 0.001) continue;                  // 다 퍼져서 사라진 파문
                    float  w    = fx.y > 0.0 ? fx.y : _ContactWidth;
                    float  fillA = fx.z;
                    // 흔들림 배수 — 파문은 거의 정원이어야 물결로 읽히고, 몸 둘레 띠는 많이 흔들려야
                    // 손그림처럼 보인다. 같은 값을 쓰면 파문이 감자 모양이 된다(실제로 그랬다).
                    float  wob  = _ContactWobble * fx.w;

                    // 먼 픽셀은 제곱거리로 먼저 버린다 — sqrt/나눗셈까지 가지 않게.
                    // 점이 12개까지 늘어나면 이 한 줄이 수면 전체의 비용을 좌우한다.
                    float2 rel = posWS.xz - c.xz;
                    float  reachR = (1.0 + w + wob) * c.w;
                    if (dot(rel, rel) > reachR * reachR) continue;

                    float d = length(rel) / max(0.01, c.w);      // 1.0 = 원 둘레

                    // 세로 게이트 — 높이가 다른 물(폭포 중간·개울)까지 번지지 않게 한다
                    float yg = 1.0 - saturate(abs(posWS.y - c.y) / _ContactYRange);
                    if (yg <= 0.0) continue;

                    // 노이즈 좌표를 점 기준(rel)으로 잡아야 캐릭터가 움직일 때 무늬가 따라온다.
                    // 월드 좌표로 잡으면 무늬는 제자리에 있고 원만 그 위를 지나가 미끄러져 보인다.
                    // 한 옥타브면 충분하다 — 테두리 흔들림이라 fbm2 의 둘째 옥타브가 안 보인다.
                    d += (vnoise(rel * _ContactWobScale + t * _ContactSpeed) - 0.5) * wob;

                    float band = 1.0 - smoothstep(0.0, w, abs(d - 1.0));
                    float fill = (1.0 - smoothstep(1.0 - w, 1.0, d)) * fillA;

                    // **모양만 계단으로 끊고 세기는 그대로 곱한다.**
                    // 세기까지 바깥의 quantize 에 태우면 파문이 사라질 때 3~4단으로 뚝뚝 끊긴다.
                    acc = max(acc, quantize(max(band, fill), _ColorSteps) * str * yg);
                }
                return saturate(acc);
            #else
                return 0.0;
            #endif
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);

            #if defined(BCM_WATER_WAVES) && !defined(BCM_WATER_FALL)
                // 사인 두 개를 어긋난 방향으로 겹쳐 규칙적인 격자무늬가 안 생기게 한다.
                float  wt  = _Time.y * _WaveSpeed;
                float2 wuv = IN.uv * _WaveScale * 6.2831853;
                float  h   = sin(wuv.x + wt) * 0.5
                           + sin(wuv.x * 0.7 + wuv.y * 1.3 + wt * 1.31) * 0.5;
                // 깊이(R)로 감쇠 — 물가는 거의 안 움직여야 지형 밖으로 삐져나오지 않는다
                posWS.y += h * _WaveHeight * saturate(IN.color.r * 2.0);
            #endif

                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = IN.uv;
                OUT.color      = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float depth = IN.color.r;   // 0 물가 ~ 1 중앙
                float foamM = IN.color.g;   // 물가/낙하지점 포말 마스크
                float flowC = IN.color.b;   // 흐름 좌표

                float t = _Time.y;

            #ifdef BCM_WATER_FALL
                // ── 폭포: 흐름 좌표를 따라 세로로 길게 늘인 줄무늬 ────────
                float2 fuv = float2(IN.uv.x * _FallStreak, flowC * _FallStreak * 0.35 - t * _FallSpeed);
            #ifdef BCM_WATER_PATTERNTEX
                float streak = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex,
                                                fuv * _PatternTex_ST.xy + _PatternTex_ST.zw).r;
            #else
                float streak = fbm2(fuv);
                // 세로 줄을 또렷하게 — x 방향으로만 고주파를 남긴다
                streak = streak * 0.55 + vnoise(float2(IN.uv.x * _FallStreak * 2.2, flowC * 3.0 - t * _FallSpeed * 1.4)) * 0.45;
            #endif

                float lines = smoothstep(0.5 - _RippleWidth, 0.5 + _RippleWidth, streak);
                float band  = quantize(lines, _ColorSteps);

                // 낙하할수록 하얗게 부서진다 (flowC 가 아래로 갈수록 1)
                float breakUp = saturate(flowC * 1.35);
                float foam = saturate(max(foamM, breakUp * 0.85) + band * 0.35);
                foam = quantize(foam, _ColorSteps);

                half4 col = lerp(_ShallowColor, _FoamColor, foam);
                col = ApplySurfaceTex(col, IN.uv, float2(0, 1), t);
                // 폭포는 물줄기라 호수보다 불투명해야 형태가 읽힌다. 가장자리(R 에 구워둔
                // 감쇠)만 흘려보내고 나머지는 포말 정도에 따라 진하게 남긴다.
                float edge = smoothstep(0.0, 0.45, depth);
                col.a = saturate(lerp(0.55, 1.0, foam) * edge);
            #else
                // ── 호수 / 강 ────────────────────────────────────────────
                float2 dir = normalize(float2(_FlowDir.x, _FlowDir.y) + 1e-5);
                // 흐름 방향으로 늘여서 샘플 → 방향성 있는 물결
                float2 base = IN.uv * _RippleScale;
                float2 strc = float2(dot(base, float2(dir.y, -dir.x)),
                                     dot(base, dir) / _RippleStretch);

                float2 scroll = dir * (t * _FlowSpeed);

                // 물결 무늬 소스. 텍스처를 끼우면 두 옥타브 값 노이즈 대신 그 R 채널을 쓴다 —
                // UV 는 동일하므로 _RippleScale / _RippleStretch / _FlowSpeed 가 그대로 먹는다.
            #ifdef BCM_WATER_PATTERNTEX
                float2 wuv = strc + float2(0.0, -t * _RippleSpeed) + scroll;
                half2 pat  = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex,
                                              wuv * _PatternTex_ST.xy + _PatternTex_ST.zw).rg;
                float waves = pat.r;
                float wobN  = pat.g;
            #else
                float n1 = fbm2(strc + float2(0.0, -t * _RippleSpeed) + scroll);
                float n2 = fbm2(strc * 1.9 + float2(t * _RippleSpeed * 0.7, 1.3) - scroll * 0.6);
                float waves = n1 * 0.6 + n2 * 0.4;
                float wobN  = fbm2(strc * 2.6 + t * _FoamSpeed * 0.35);
            #endif

                // 물결 선: 좁은 대역만 하얗게 남겨 손으로 그린 선처럼 보이게
                float lines = smoothstep(0.52 - _RippleWidth, 0.52 + _RippleWidth, waves)
                            * (1.0 - smoothstep(0.52 + _RippleWidth, 0.52 + _RippleWidth * 2.2, waves));
                lines *= _RippleAmount;

                // 기본 물색 — 깊이를 계단으로 끊는다
                float dq = quantize(depth, _ColorSteps);
                half4 col = lerp(_ShallowColor, _DeepColor, dq);
                col = ApplySurfaceTex(col, IN.uv, dir, t);

                // 물가 포말: 마스크 안에서만 흔들리게 한다. 흔들림을 마스크에 그냥 더하면
                // 마스크가 0인 수면 중앙까지 하얘진다 — 반드시 마스크로 곱해서 가둔다.
                float wob = (wobN - 0.5) * 2.0 * _FoamWobble;
                float fm   = saturate(foamM * (1.0 + wob));
                // _FoamWidth = 마스크 밴드 중 실제로 하얘지는 비율 (작을수록 얇은 띠)
                float cut  = 1.0 - saturate(_FoamWidth);
                float foam = saturate((fm - cut) / max(0.001, _FoamWidth));

                float shore = 1.0;
            #ifdef BCM_WATER_SHORELINE
                // ── 물가 선을 움직인다 ────────────────────────────────
                // 정점 컬러 R(깊이)을 임계값으로 잘라낸다. 임계값이 시간에 따라 오르내리면
                // 물가 선이 안팎으로 밀려왔다 빠진다. 무늬만 흐르는 것과 달리 실루엣이 움직인다.
                //
                // 노이즈만 쓰면 "흘러간다", 사인만 쓰면 "전체가 한꺼번에 뛴다".
                // _ShoreRhythm 으로 둘을 섞어야 파도가 치는 것처럼 읽힌다.
                float  lapN  = fbm2(IN.uv * _ShoreScale + dir * (t * _ShoreSpeed));
                float  pulse = sin(t * _ShoreSpeed * 1.7 + lapN * 6.2831853) * 0.5 + 0.5;
                float  lap   = lerp(lapN, pulse, _ShoreRhythm);
                float  wl    = _ShoreCarve * lap;                     // 물가 선 위치 (깊이 단위)

                shore = smoothstep(wl, wl + max(0.001, _ShoreSoft), depth);

                // 포말이 그 선을 따라간다. 이걸 안 하면 물이 빠진 자리에 포말만 남아 떠 있다.
                float lapFoam = 1.0 - smoothstep(wl, wl + max(0.001, _ShoreFoam), depth);
                foam = max(foam, lapFoam);
            #endif

                foam = quantize(foam, _ColorSteps);

                // 캐릭터가 들어간 자리 — 파문/접촉 띠.
                // **quantize 뒤에 합친다.** 파문은 스스로 계단을 먹고 나오므로 여기서 또 끊으면
                // 페이드가 계단이 되어 사라질 때 뚝뚝 끊긴다.
                foam = max(foam, ContactFoam(IN.positionWS, t));
                // 빗방울 파문(2026-11-12 사용자: "물 웅덩이에도 빗물 떨어지게") — 포말 색 고리. 비가 안 오면 0
                foam = max(foam, RainRipple(IN.positionWS.xz, t) * 0.9);

                col.rgb = lerp(col.rgb, _RippleColor.rgb, lines);
                col.rgb = lerp(col.rgb, _FoamColor.rgb, foam);
                col.a   = saturate(lerp(col.a, 1.0, max(foam, lines * 0.5)));
                col.a  *= shore;    // SHORELINE 이 꺼져 있으면 1.0 이라 무변화
            #endif

            #ifdef BCM_WATER_LIGHTTINT
                // 씬의 태양광에 색을 살짝 실어 암반(툰 셰이더)과 톤이 어긋나지 않게 한다.
                Light mainLight = GetMainLight();
                col.rgb *= lerp(half3(1, 1, 1), mainLight.color, _LightTint);

                // 태양 반짝임 — 계단으로 끊어 셀 룩을 유지
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 H = normalize(mainLight.direction + V);
                float  s = saturate(dot(normalize(IN.normalWS), H));
                float  glint = quantize(pow(s, 48.0), 2.0) * _Glossy;
                col.rgb += glint * _FoamColor.rgb;
            #endif

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
