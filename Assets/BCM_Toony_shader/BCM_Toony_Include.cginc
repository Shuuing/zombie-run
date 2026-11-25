// Toony Colors Pro+Mobile 2
// (c) 2014-2026 Jean Moreno

/// #define fixed half
/// #define fixed2 half2
/// #define fixed3 half3
/// #define fixed4 half4

// Built-in renderer (CG) to SRP (HLSL) bindings
#if defined(TCP2_HYBRID_URP)
	#define UnityObjectToClipPos TransformObjectToHClip
	#define UnityObjectToWorldNormal TransformObjectToWorldNormal
	#define _WorldSpaceLightPos0 _MainLightPosition
	#define UnpackScaleNormal UnpackNormalScale
#else
	// URP to BIRP
	#define CopySign(x,s) ((s >= 0) ? abs(x) : -abs(x))
	#define UNITY_MATRIX_I_M unity_WorldToObject
#endif

#if defined(TCP2_HYBRID_URP)

	#ifndef URP_VERSION
		#error URP version undefined
	#endif

	// #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
	// This would cause a compilation error if URP isn't installed, so instead we use the dedicated
	// "URP Support" file which contains all needed .hlslinc files embedded within a single file.
	// #include "TCP2 Hybrid URP Support.cginc"

	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
	#if URP_VERSION >= 12
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
	#endif

	#define UNITY_PASS_FORWARDBASE
#endif

#if defined(TCP2_HYBRID_URP) && URP_VERSION >= 14
	#define IS_LIGHTING_FEATURE_ENABLED(feature)	if (IsLightingFeatureEnabled(feature))
#else
	#define IS_LIGHTING_FEATURE_ENABLED(feature)
#endif

#if defined(LOD_FADE_CROSSFADE)
	static const float DITHER_THRESHOLDS_4x4[16] =
	{
		0.0588, 0.5294, 0.1765, 0.6471,
		0.7647, 0.2941, 0.8823, 0.4118,
		0.2353, 0.7059, 0.1176, 0.5882,
		0.9412, 0.4706, 0.8235, 0.3529
	};
	float Dither4x4(float2 positionCS)
	{
		#if SHADER_API_GLES // gles 2.0
			float index = (floor(positionCS.x) % 4.0) * 4.0 + (floor(positionCS.y) % 4.0);
		#else
			uint index = (uint(positionCS.x) % 4u) * 4u + uint(positionCS.y) % 4u;
		#endif
		return DITHER_THRESHOLDS_4x4[index];
	}
#endif

//================================================================================================================================
//================================================================================================================================

// MAIN

//================================================================================================================================
//================================================================================================================================

// Uniforms
CBUFFER_START(UnityPerMaterial)
	half _RampSmoothing;
	half _RampThreshold;
	half _RampBands;
	half _RampBandsSmoothing;
	half _RampScale;
	half _RampOffset;

	float4 _BumpMap_ST;
	half _BumpScale;

	float4 _BaseMap_ST;

	half _Cutoff;

	half4 _BaseColor;

	float4 _EmissionMap_ST;
	half _EmissionChannel;
	half4 _EmissionColor;

	half4 _MatCapColor;
	half _MatCapMaskChannel;
	half _MatCapType;
	half _MatCapPower; // BCM: matcap intensity

	// BCM: per-region matcap, driven by the RGBA channels of _MatCapChannelTex
	half4 _MatCapColorR; half _MatCapTypeR; half _MatCapPowerR;
	half4 _MatCapColorG; half _MatCapTypeG; half _MatCapPowerG;
	half4 _MatCapColorB; half _MatCapTypeB; half _MatCapPowerB;
	half4 _MatCapColorA; half _MatCapTypeA; half _MatCapPowerA;

	half4 _SColor;
	half4 _HColor;

	// BCM: Multi-color ramp (Shadow + up to 6 Middle colors + Highlight, each with a band %)
	half _ShadowPercent;
	half _HighPercent;
	half _MidCount;
	half4 _MidColor0; half4 _MidColor1; half4 _MidColor2; half4 _MidColor3; half4 _MidColor4; half4 _MidColor5;
	half _MidPct0; half _MidPct1; half _MidPct2; half _MidPct3; half _MidPct4; half _MidPct5;

	half _RimMin;
	half _RimMax;
	half4 _RimColor;

	// BCM: Directional Rim (Light Direction) - added on top of the ported hybrid include
	half4 _RimColor2;
	half _RimMin2;
	half _RimMax2;
	half _RimLightDir;

	half _SpecularRoughness;
	half4 _SpecularColor;
	half _SpecularStrength; // BCM: specular intensity, separate from color/roughness
	half _SpecularAlbedoTint;         // BCM: 0 = uniform specular colour, 1 = follow the albedo
	half _SpecularPreserveBrightness; // BCM: take only the albedo hue, keep highlight brightness
	half _SpecularMapType;
	half _SpecularToonSize;
	half _SpecularToonSmoothness;

	half _ReflectionSmoothness;
	half4 _ReflectionColor;
	half _FresnelMax;
	half _FresnelMin;
	half _ReflectionMapType;

	half _OcclusionStrength;
	half _OcclusionChannel;
	// BCM: advanced occlusion (channel mask + remap)
	float4 _OcclusionMapChannelMask;
	half _OcclusionRemapStart;
	half _OcclusionRemapEnd;

	half _IndirectIntensity;
	half _SingleIndirectColor;

	// BCM: per-material direct-light scale (0..100 %) + tint color
	half _LightReceived;
	half4 _LightReceivedColor;

	// BCM: received real-time shadow shaping (strength + edge softness + tint)
	half _ReceiveShadowStrength;
	half _ReceiveShadowSmoothing;
	half4 _ReceiveShadowColor;

	half _OutlineWidth;
	half _OutlineMinWidth;
	half _OutlineMaxWidth;
	half4 _OutlineColor;
	half _OutlineTextureLOD;
	half _DirectIntensityOutline;
	half _IndirectIntensityOutline;
	float4 _OutlineColorMap_ST; // BCM: outline color map tiling/offset
	// BCM: per-vertex outline mask (vertex colour channel). 0 = no outline on that vertex.
	half _OutlineMaskChannel;
	half _OutlineMaskInvert;

	half _CastShadows; // BCM: 1 = cast shadows onto other surfaces, 0 = don't (gates the ShadowCaster pass)

	// BCM: Multiply section (masked color multiply over the albedo)
	half4 _MultiplyColor;
	half _MultiplyMaskChannel;
	half _MultiplyStrength;

	// BCM: Wind Sway section (정점 변형 — BCM_WIND_SWAY 키워드로 게이트)
	half _WindStrength;
	half _WindSpeed;
	float _WindHeight;
	float _WindBaseY;
	half _WindTurbulence;
	float4 _WindDir;
	half _WindPhaseScale;

	// BCM: Ground Splat section (지면 텍스처 혼합 — BCM_GROUND_SPLAT 키워드로 게이트)
	float4 _SplatTiles;       // 반복 주기(m): x=_BaseMap y=_SplatTex1 z=_SplatTex2 w=_SplatTex3
	half _SplatSharpness;
	half _SplatHeightBlend;
	half _SplatNoise;
	float _SplatNoiseScale;
	float _SplatTile4;         // 풀(_SplatTex4) 반복 주기(m)
	half4 _SplatTint4;         // 풀 색 보정(곱)
	half4 _SplatStrength;      // 층별 최대 불투명도 (R,G,B,A). 1 이면 꽉 덮는다, 낮추면 수채 번짐처럼 비친다
	half _BCM_NoFog;           // 1 = 거리 안개 무시 (인트로 비행기·낙하산)
CBUFFER_END

// --------------------------------------------------------------------------------------------------------------------
// BCM: 시야 가림 투시(see-through). 건물이 카메라와 캐릭터 사이에 오면 캐릭터 주변 원 안의 '앞쪽' 픽셀만
// 디더로 걸러내 캐릭터가 비쳐 보이게 한다. **완전히 사라지지 않는다** — 최소 불투명도(_BCM_SeeThroughParams.z)만큼 남는다.
//
// 왜 알파 블렌딩이 아니라 디더인가: 반투명으로 바꾸면 불투명 큐를 떠나 아웃라인·다른 반투명과 정렬이 꼬이고
// 머티리얼을 갈아 끼워야 한다. 디더는 불투명 그대로 픽셀만 clip 하므로 정렬 문제도, 추가 패스도 없다.
//
// 왜 레이캐스트가 없나: 대상(플레이어) 위치 하나를 전역으로 받아 픽셀마다 '원 안인가 + 대상보다 카메라에 가까운가'
// 만 본다. 건물이 몇 채든 CPU 비용이 0이고, 건물 뒤쪽 벽이나 대상 뒤의 건물은 깊이 조건에서 걸러진다.
// 전역은 C# ProtoSeeThrough 가 매 프레임 올린다. **UnityPerMaterial 밖에 둬야 한다**(안이면 SRP 배처가 끊긴다).
float4 _BCM_SeeThroughPos;     // xyz = 대상 월드 위치(가슴 높이), w = 1 이면 켜짐
float4 _BCM_SeeThroughParams;  // x = 반지름(화면 높이 비율) y = 가장자리 폭 z = 최소 불투명도 w = 깊이 여유(m)

#if defined(BCM_SEE_THROUGH)
// 4x4 베이어 임계값(0~1). 2x2 를 재귀로 겹친 식 — 상수 배열 동적 인덱스를 피한다(GLES 에서 비싸다).
float BCM_Bayer2(float2 a) { a = floor(a); return frac(a.x * 0.5 + a.y * a.y * 0.75); }
float BCM_Bayer4(float2 pixel) { return BCM_Bayer2(pixel * 0.5) * 0.25 + BCM_Bayer2(pixel) + 1.0 / 32.0; }

void BCM_SeeThroughClip(float3 positionWS, float2 pixel)
{
	if (_BCM_SeeThroughPos.w < 0.5) return;
	float4 cp = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
	float4 ct = mul(UNITY_MATRIX_VP, float4(_BCM_SeeThroughPos.xyz, 1.0));
	float2 d = (cp.xy / cp.w - ct.xy / ct.w) * 0.5;       // NDC 차이 → 화면 높이 비율
	d.x *= _ScreenParams.x / _ScreenParams.y;
	float r = length(d);
	float inCircle = 1.0 - smoothstep(_BCM_SeeThroughParams.x - _BCM_SeeThroughParams.y, _BCM_SeeThroughParams.x, r);
	// 원근 투영에서 clip.w = 시점 깊이. 대상보다 여유(w)만큼 더 가까운 픽셀만 — 대상 뒤의 벽·바닥은 그대로 둔다.
	float front = saturate((ct.w - cp.w - _BCM_SeeThroughParams.w) * 2.0);
	float opacity = lerp(1.0, _BCM_SeeThroughParams.z, inCircle * front);
	clip(opacity - BCM_Bayer4(pixel));
}
	#define BCM_SEE_THROUGH_CLIP(posWS, pixel) BCM_SeeThroughClip(posWS, pixel)
#else
	#define BCM_SEE_THROUGH_CLIP(posWS, pixel)
#endif

// --------------------------------------------------------------------------------------------------------------------
// BCM: 바람 흔들림 — 오브젝트 공간 정점을 월드로 올려 높이 마스크를 재고 다시 내린다.
//
// **왜 월드 공간을 거치는가.** 필드 프롭은 `FieldBuilder` 가 `isStatic = true` 로 놓는다.
// 정적 배칭은 메시를 월드 좌표로 미리 구워 넣고 `unity_ObjectToWorld` 를 단위행렬로 바꾼다.
// 그래서 오브젝트 공간 Y 나 오브젝트 원점을 기준으로 마스크를 만들면 **배칭 여부에 따라 결과가
// 달라진다**(배칭되면 원점이 사라져 마스크가 통째로 어긋난다). 월드 Y 는 두 경우 모두 같으므로
// 정적 배칭을 끄지 않고도 안전하다 — 나무 22그루가 계속 한 배치로 나간다.
//
// 위상도 같은 이유로 월드 XZ 에서 뽑는다. 덕분에 나무마다 자동으로 위상이 어긋나 한 그루씩
// 따로 흔들리고, 인스턴스별 프로퍼티를 심을 필요가 없다.
//
// **한계**: 노멀은 다시 계산하지 않는다. 흔들리는 동안 음영이 고정이라는 뜻인데, 진폭이
// 0.2m 안쪽인 완만한 흔들림에서는 눈에 안 띈다. 크게 휘두르려면 노멀도 같이 돌려야 한다.
void BCM_ApplyWindSway(inout float4 positionOS)
{
#if defined(BCM_WIND_SWAY)
	float3 positionWS = mul(UNITY_MATRIX_M, float4(positionOS.xyz, 1.0)).xyz;

	// 뿌리 고정: 밑면에서 0, _WindHeight 에서 1. 제곱을 걸어 아래쪽이 더 단단해진다
	// (선형이면 밑동부터 같이 밀려 나무가 통째로 기울어 보인다).
	float h = saturate((positionWS.y - _WindBaseY) / max(_WindHeight, 0.001));
	float mask = h * h;

	float2 dir = normalize(float2(_WindDir.x, _WindDir.z) + float2(1e-5, 1e-5));
	float phase = dot(positionWS.xz, float2(0.31, 0.27)) * _WindPhaseScale + _Time.y * _WindSpeed;
	// 기본 파동 + 2.7배 빠른 잔떨림. 배수를 정수로 두면 주기가 겹쳐 다시 기계적으로 보인다.
	float sway = sin(phase) + _WindTurbulence * sin(phase * 2.7 + 1.3);

	positionWS.xz += dir * sway * _WindStrength * mask;
	positionOS.xyz = mul(UNITY_MATRIX_I_M, float4(positionWS, 1.0)).xyz;
#endif
}

#if URP_VERSION >= 14 && defined(UNITY_DOTS_INSTANCING_ENABLED)

// --------------------------------
// DOTS INSTANCING & BRG SUPPORT

UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
	UNITY_DOTS_INSTANCED_PROP(float, _RampSmoothing)
	UNITY_DOTS_INSTANCED_PROP(float, _RampThreshold)
	UNITY_DOTS_INSTANCED_PROP(float, _RampBands)
	UNITY_DOTS_INSTANCED_PROP(float, _RampBandsSmoothing)
	UNITY_DOTS_INSTANCED_PROP(float, _RampScale)
	UNITY_DOTS_INSTANCED_PROP(float, _RampOffset)

	UNITY_DOTS_INSTANCED_PROP(float, _BumpScale)

	UNITY_DOTS_INSTANCED_PROP(float, _Cutoff)

	UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)

	UNITY_DOTS_INSTANCED_PROP(float, _EmissionChannel)
	UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)

	UNITY_DOTS_INSTANCED_PROP(float4, _MatCapColor)
	UNITY_DOTS_INSTANCED_PROP(float, _MatCapMaskChannel)
	UNITY_DOTS_INSTANCED_PROP(float, _MatCapType)

	UNITY_DOTS_INSTANCED_PROP(float4, _SColor)
	UNITY_DOTS_INSTANCED_PROP(float4, _HColor)

	UNITY_DOTS_INSTANCED_PROP(float, _RimMin)
	UNITY_DOTS_INSTANCED_PROP(float, _RimMax)
	UNITY_DOTS_INSTANCED_PROP(float4, _RimColor)

	UNITY_DOTS_INSTANCED_PROP(float, _SpecularRoughness)
	UNITY_DOTS_INSTANCED_PROP(float4, _SpecularColor)
	UNITY_DOTS_INSTANCED_PROP(float, _SpecularMapType)
	UNITY_DOTS_INSTANCED_PROP(float, _SpecularAlbedoTint)
	UNITY_DOTS_INSTANCED_PROP(float, _SpecularPreserveBrightness)
	UNITY_DOTS_INSTANCED_PROP(float, _SpecularToonSize)
	UNITY_DOTS_INSTANCED_PROP(float, _SpecularToonSmoothness)

	UNITY_DOTS_INSTANCED_PROP(float, _ReflectionSmoothness)
	UNITY_DOTS_INSTANCED_PROP(float4, _ReflectionColor)
	UNITY_DOTS_INSTANCED_PROP(float, _FresnelMax)
	UNITY_DOTS_INSTANCED_PROP(float, _FresnelMin)
	UNITY_DOTS_INSTANCED_PROP(float, _ReflectionMapType)

	UNITY_DOTS_INSTANCED_PROP(float, _OcclusionStrength)
	UNITY_DOTS_INSTANCED_PROP(float, _OcclusionChannel)

	UNITY_DOTS_INSTANCED_PROP(float, _IndirectIntensity)
	UNITY_DOTS_INSTANCED_PROP(float, _SingleIndirectColor)

	UNITY_DOTS_INSTANCED_PROP(float, _OutlineWidth)
	UNITY_DOTS_INSTANCED_PROP(float, _OutlineMinWidth)
	UNITY_DOTS_INSTANCED_PROP(float, _OutlineMaxWidth)
	UNITY_DOTS_INSTANCED_PROP(float4, _OutlineColor)
	UNITY_DOTS_INSTANCED_PROP(float, _OutlineMaskChannel)
	UNITY_DOTS_INSTANCED_PROP(float, _OutlineMaskInvert)
	UNITY_DOTS_INSTANCED_PROP(float, _OutlineTextureLOD)
	UNITY_DOTS_INSTANCED_PROP(float, _DirectIntensityOutline)
	UNITY_DOTS_INSTANCED_PROP(float, _IndirectIntensityOutline)
UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)

// --------

// See URP's LitInput.hlsl for why we use static variables
static float unity_DOTS_Sampled_RampSmoothing;
static float unity_DOTS_Sampled_RampThreshold;
static float unity_DOTS_Sampled_RampBands;
static float unity_DOTS_Sampled_RampBandsSmoothing;
static float unity_DOTS_Sampled_RampScale;
static float unity_DOTS_Sampled_RampOffset;
static float unity_DOTS_Sampled_BumpScale;
static float unity_DOTS_Sampled_Cutoff;
static float4 unity_DOTS_Sampled_BaseColor;
static float unity_DOTS_Sampled_EmissionChannel;
static float4 unity_DOTS_Sampled_EmissionColor;
static float4 unity_DOTS_Sampled_MatCapColor;
static float unity_DOTS_Sampled_MatCapMaskChannel;
static float unity_DOTS_Sampled_MatCapType;
static float4 unity_DOTS_Sampled_SColor;
static float4 unity_DOTS_Sampled_HColor;
static float unity_DOTS_Sampled_RimMin;
static float unity_DOTS_Sampled_RimMax;
static float4 unity_DOTS_Sampled_RimColor;
static float unity_DOTS_Sampled_SpecularRoughness;
static float4 unity_DOTS_Sampled_SpecularColor;
static float unity_DOTS_Sampled_SpecularMapType;
static float unity_DOTS_Sampled_SpecularAlbedoTint;
static float unity_DOTS_Sampled_SpecularPreserveBrightness;
static float unity_DOTS_Sampled_SpecularToonSize;
static float unity_DOTS_Sampled_SpecularToonSmoothness;
static float unity_DOTS_Sampled_ReflectionSmoothness;
static float4 unity_DOTS_Sampled_ReflectionColor;
static float unity_DOTS_Sampled_FresnelMax;
static float unity_DOTS_Sampled_FresnelMin;
static float unity_DOTS_Sampled_ReflectionMapType;
static float unity_DOTS_Sampled_OcclusionStrength;
static float unity_DOTS_Sampled_OcclusionChannel;
static float unity_DOTS_Sampled_IndirectIntensity;
static float unity_DOTS_Sampled_SingleIndirectColor;
static float unity_DOTS_Sampled_OutlineWidth;
static float unity_DOTS_Sampled_OutlineMinWidth;
static float unity_DOTS_Sampled_OutlineMaxWidth;
static float4 unity_DOTS_Sampled_OutlineColor;
static float unity_DOTS_Sampled_OutlineMaskChannel;
static float unity_DOTS_Sampled_OutlineMaskInvert;
static float unity_DOTS_Sampled_OutlineTextureLOD;
static float unity_DOTS_Sampled_DirectIntensityOutline;
static float unity_DOTS_Sampled_IndirectIntensityOutline;

// --------

void SetupDOTSTcp2PropertyCaches()
{
	unity_DOTS_Sampled_RampSmoothing = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _RampSmoothing);
	unity_DOTS_Sampled_RampThreshold = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _RampThreshold);
	unity_DOTS_Sampled_RampBands = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _RampBands);
	unity_DOTS_Sampled_RampBandsSmoothing = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _RampBandsSmoothing);
	unity_DOTS_Sampled_RampScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _RampScale);
	unity_DOTS_Sampled_RampOffset = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _RampOffset);
	// unity_DOTS_Sampled_BumpMap_ST = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float4 , _BumpMap_ST);
	unity_DOTS_Sampled_BumpScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _BumpScale);
	// unity_DOTS_Sampled_BaseMap_ST = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float4 , _BaseMap_ST);
	unity_DOTS_Sampled_Cutoff = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _Cutoff);
	unity_DOTS_Sampled_BaseColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float4 , _BaseColor);
	// unity_DOTS_Sampled_EmissionMap_ST = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float4 , _EmissionMap_ST);
	unity_DOTS_Sampled_EmissionChannel = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _EmissionChannel);
	unity_DOTS_Sampled_EmissionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float4 , _EmissionColor);
	unity_DOTS_Sampled_MatCapColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float4 , _MatCapColor);
	unity_DOTS_Sampled_MatCapMaskChannel = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _MatCapMaskChannel);
	unity_DOTS_Sampled_MatCapType = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _MatCapType);
	unity_DOTS_Sampled_SColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float4 , _SColor);
	unity_DOTS_Sampled_HColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float4 , _HColor);
	unity_DOTS_Sampled_RimMin = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _RimMin);
	unity_DOTS_Sampled_RimMax = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _RimMax);
	unity_DOTS_Sampled_RimColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float4 , _RimColor);
	unity_DOTS_Sampled_SpecularRoughness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _SpecularRoughness);
	unity_DOTS_Sampled_SpecularColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float4 , _SpecularColor);
	unity_DOTS_Sampled_SpecularMapType = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _SpecularMapType);
	unity_DOTS_Sampled_SpecularAlbedoTint = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _SpecularAlbedoTint);
	unity_DOTS_Sampled_SpecularPreserveBrightness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _SpecularPreserveBrightness);
	unity_DOTS_Sampled_SpecularToonSize = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _SpecularToonSize);
	unity_DOTS_Sampled_SpecularToonSmoothness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _SpecularToonSmoothness);
	unity_DOTS_Sampled_ReflectionSmoothness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _ReflectionSmoothness);
	unity_DOTS_Sampled_ReflectionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float4 , _ReflectionColor);
	unity_DOTS_Sampled_FresnelMax = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _FresnelMax);
	unity_DOTS_Sampled_FresnelMin = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _FresnelMin);
	unity_DOTS_Sampled_ReflectionMapType = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _ReflectionMapType);
	unity_DOTS_Sampled_OcclusionStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _OcclusionStrength);
	unity_DOTS_Sampled_OcclusionChannel = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _OcclusionChannel);
	unity_DOTS_Sampled_IndirectIntensity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _IndirectIntensity);
	unity_DOTS_Sampled_SingleIndirectColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _SingleIndirectColor);
	unity_DOTS_Sampled_OutlineWidth = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _OutlineWidth);
	unity_DOTS_Sampled_OutlineMinWidth = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _OutlineMinWidth);
	unity_DOTS_Sampled_OutlineMaxWidth = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _OutlineMaxWidth);
	unity_DOTS_Sampled_OutlineColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float4 , _OutlineColor);
	unity_DOTS_Sampled_OutlineMaskChannel = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _OutlineMaskChannel);
	unity_DOTS_Sampled_OutlineMaskInvert = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _OutlineMaskInvert);
	unity_DOTS_Sampled_OutlineTextureLOD = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _OutlineTextureLOD);
	unity_DOTS_Sampled_DirectIntensityOutline = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _DirectIntensityOutline);
	unity_DOTS_Sampled_IndirectIntensityOutline = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT (float  , _IndirectIntensityOutline);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSTcp2PropertyCaches()

#define _RampSmoothing 				unity_DOTS_Sampled_RampSmoothing
#define _RampThreshold 				unity_DOTS_Sampled_RampThreshold
#define _RampBands 					unity_DOTS_Sampled_RampBands
#define _RampBandsSmoothing 		unity_DOTS_Sampled_RampBandsSmoothing
#define _RampScale 					unity_DOTS_Sampled_RampScale
#define _RampOffset 				unity_DOTS_Sampled_RampOffset
#define _BumpScale 					unity_DOTS_Sampled_BumpScale
#define _Cutoff 					unity_DOTS_Sampled_Cutoff
#define _BaseColor 					unity_DOTS_Sampled_BaseColor
#define _EmissionChannel 			unity_DOTS_Sampled_EmissionChannel
#define _EmissionColor 				unity_DOTS_Sampled_EmissionColor
#define _MatCapColor 				unity_DOTS_Sampled_MatCapColor
#define _MatCapMaskChannel 			unity_DOTS_Sampled_MatCapMaskChannel
#define _MatCapType 				unity_DOTS_Sampled_MatCapType
#define _SColor 					unity_DOTS_Sampled_SColor
#define _HColor 					unity_DOTS_Sampled_HColor
#define _RimMin 					unity_DOTS_Sampled_RimMin
#define _RimMax 					unity_DOTS_Sampled_RimMax
#define _RimColor 					unity_DOTS_Sampled_RimColor
#define _SpecularRoughness 			unity_DOTS_Sampled_SpecularRoughness
#define _SpecularColor 				unity_DOTS_Sampled_SpecularColor
#define _SpecularMapType 			unity_DOTS_Sampled_SpecularMapType
#define _SpecularAlbedoTint 		unity_DOTS_Sampled_SpecularAlbedoTint
#define _SpecularPreserveBrightness	unity_DOTS_Sampled_SpecularPreserveBrightness
#define _SpecularToonSize 			unity_DOTS_Sampled_SpecularToonSize
#define _SpecularToonSmoothness 	unity_DOTS_Sampled_SpecularToonSmoothness
#define _ReflectionSmoothness 		unity_DOTS_Sampled_ReflectionSmoothness
#define _ReflectionColor 			unity_DOTS_Sampled_ReflectionColor
#define _FresnelMax 				unity_DOTS_Sampled_FresnelMax
#define _FresnelMin 				unity_DOTS_Sampled_FresnelMin
#define _ReflectionMapType 			unity_DOTS_Sampled_ReflectionMapType
#define _OcclusionStrength 			unity_DOTS_Sampled_OcclusionStrength
#define _OcclusionChannel 			unity_DOTS_Sampled_OcclusionChannel
#define _IndirectIntensity 			unity_DOTS_Sampled_IndirectIntensity
#define _SingleIndirectColor 		unity_DOTS_Sampled_SingleIndirectColor
#define _OutlineWidth 				unity_DOTS_Sampled_OutlineWidth
#define _OutlineMinWidth 			unity_DOTS_Sampled_OutlineMinWidth
#define _OutlineMaxWidth 			unity_DOTS_Sampled_OutlineMaxWidth
#define _OutlineColor 				unity_DOTS_Sampled_OutlineColor
#define _OutlineMaskChannel 		unity_DOTS_Sampled_OutlineMaskChannel
#define _OutlineMaskInvert 			unity_DOTS_Sampled_OutlineMaskInvert
#define _OutlineTextureLOD 			unity_DOTS_Sampled_OutlineTextureLOD
#define _DirectIntensityOutline 	unity_DOTS_Sampled_DirectIntensityOutline
#define _IndirectIntensityOutline 	unity_DOTS_Sampled_IndirectIntensityOutline

// --------------------------------

#endif

// Samplers
sampler2D _BaseMap;
sampler2D _Ramp;
sampler2D _BumpMap;
sampler2D _EmissionMap;
sampler2D _OcclusionMap;
sampler2D _ReflectionTex;
sampler2D _SpecGlossMap;
sampler2D _ShadowBaseMap;
sampler2D _MatCapTex;
sampler2D _MatCapMask;
// BCM: only declared in the channel variant so the four extra matcaps never cost a sampler slot otherwise
#if defined(BCM_MATCAP_CHANNEL)
	sampler2D _MatCapChannelTex;
	sampler2D _MatCapTexR;
	sampler2D _MatCapTexG;
	sampler2D _MatCapTexB;
	#if defined(BCM_MATCAP_CHANNEL_A)
		sampler2D _MatCapTexA;
	#endif
#endif
sampler2D _OutlineColorMap; // BCM: outline color map
sampler2D _HColorMap;       // BCM: Highlight Color map (multiplies _HColor), like UTS 2nd/1st Shade Map
sampler2D _SColorMap;       // BCM: Shadow Color map (multiplies _SColor)
sampler2D _MidColor0Map;    // BCM: Middle Color 1 map (multiplies _MidColor0), tri-color ramp
sampler2D _MultiplyMap;     // BCM: Multiply section mask (masked color multiply over the albedo)
#if defined(BCM_GROUND_SPLAT)
	sampler2D _SplatTex1;   // BCM: 지면 혼합 1층 (정점 컬러 R)
	sampler2D _SplatTex2;   // BCM: 지면 혼합 2층 (정점 컬러 G)
	sampler2D _SplatTex3;   // BCM: 지면 혼합 3층 (정점 컬러 B)
	sampler2D _SplatTex4;   // BCM: 지면 혼합 풀 — 바닥층 바로 위 (정점 컬러 A)
#endif

// Meta Pass
#if defined(UNITY_PASS_META) && !defined(TCP2_HYBRID_URP)
	#include "UnityMetaPass.cginc"
#endif

//Specular help functions (from UnityStandardBRDF.cginc)
inline float3 TCP2_SafeNormalize(float3 inVec)
{
	half dp3 = max(0.001f, dot(inVec, inVec));
	return inVec * rsqrt(dp3);
}

//GGX
#define TCP2_PI 3.14159265359
#define TCP2_INV_PI 0.31830988618f
#if defined(SHADER_API_MOBILE)
	#define TCP2_EPSILON 1e-4f
#else
	#define TCP2_EPSILON 1e-7f
#endif
inline half GGX(half NdotH, half roughness)
{
	half a2 = roughness * roughness;
	half d = (NdotH * a2 - NdotH) * NdotH + 1.0f;
	return TCP2_INV_PI * a2 / (d * d + TCP2_EPSILON);
}

// BCM: reshape the RECEIVED real-time shadow term (s: 1 = fully lit, 0 = fully shadowed).
// _ReceiveShadowStrength : 1 = full received shadow, 0 = ignore it (no darkening from cast shadows).
// _ReceiveShadowSmoothing: softens/widens the shadow edge transition. Reshapes the penumbra, so it is
//                          most visible when URP Soft Shadows (or a PCF gradient) are enabled.
//                          0..1 widens the smoothstep window until it spans the whole [0,1] input range.
//                          1..2 keeps widening PAST the input range: the curve now only uses its middle
//                          section, so contrast drops and the transition reads as much hazier. The window
//                          is deliberately NOT saturated - clamping it would make everything above 1 an
//                          exact duplicate of 1. Below 1 the bounds never leave [0,1] anyway, so the old
//                          behaviour is bit-identical and existing materials are untouched.
half BCM_ShapeReceivedShadow(half s)
{
	half w = _ReceiveShadowSmoothing * 0.5;
	if (w > 0.001)
		s = smoothstep(0.5 - w, 0.5 + w, s);
	return lerp((half)1.0, s, _ReceiveShadowStrength);
}

// BCM: colourise the RECEIVED shadow (s: 1 = fully lit, 0 = fully shadowed). Returns a per-channel
// multiplier for the ramp, so the shadowed side fades towards _ReceiveShadowColor instead of towards
// black. Black gives lerp(0,1,s) == s, i.e. exactly the plain scalar multiply used before this
// existed - which is why the property defaults to black and old materials are untouched.
// Takes the SHAPED shadow term only: distance attenuation must stay a plain scalar, otherwise a
// point light's falloff would get tinted too.
half3 BCM_TintReceivedShadow(half s)
{
	return lerp(_ReceiveShadowColor.rgb, half3(1.0, 1.0, 1.0), s);
}

// sColor/mid0Color/hColor are the Shadow / Middle-1 / Highlight colors already tinted by their optional
// color maps (see _SColorMap / _MidColor0Map / _HColorMap). Passed in so this stays UV-free.
half3 CalculateRamp(half ndlWrapped, half3 sColor, half3 mid0Color, half3 hColor)
{
	#if defined(BCM_RAMP_TRICOLOR)
		// BCM: Multi-color ramp. Ordered stops = Shadow, then _MidCount middle colors, then Highlight.
		// Each stop has a band width (%). ndlWrapped runs 0 (shadow side) -> 1 (light side).
		int bcmCount = (int)(_MidCount + 0.5);
		half bcmTotal = _ShadowPercent + _HighPercent;
		if (bcmCount > 0) bcmTotal += _MidPct0;
		if (bcmCount > 1) bcmTotal += _MidPct1;
		if (bcmCount > 2) bcmTotal += _MidPct2;
		if (bcmCount > 3) bcmTotal += _MidPct3;
		if (bcmCount > 4) bcmTotal += _MidPct4;
		if (bcmCount > 5) bcmTotal += _MidPct5;
		bcmTotal = max(1e-4, bcmTotal);

		half bcmSm = _RampSmoothing * 0.5;
		half bcmCum = _ShadowPercent;
		half3 ramp = sColor;
		if (bcmCount > 0) { half p = bcmCum / bcmTotal; ramp = lerp(ramp, mid0Color, smoothstep(p - bcmSm, p + bcmSm, ndlWrapped)); bcmCum += _MidPct0; }
		if (bcmCount > 1) { half p = bcmCum / bcmTotal; ramp = lerp(ramp, _MidColor1.rgb, smoothstep(p - bcmSm, p + bcmSm, ndlWrapped)); bcmCum += _MidPct1; }
		if (bcmCount > 2) { half p = bcmCum / bcmTotal; ramp = lerp(ramp, _MidColor2.rgb, smoothstep(p - bcmSm, p + bcmSm, ndlWrapped)); bcmCum += _MidPct2; }
		if (bcmCount > 3) { half p = bcmCum / bcmTotal; ramp = lerp(ramp, _MidColor3.rgb, smoothstep(p - bcmSm, p + bcmSm, ndlWrapped)); bcmCum += _MidPct3; }
		if (bcmCount > 4) { half p = bcmCum / bcmTotal; ramp = lerp(ramp, _MidColor4.rgb, smoothstep(p - bcmSm, p + bcmSm, ndlWrapped)); bcmCum += _MidPct4; }
		if (bcmCount > 5) { half p = bcmCum / bcmTotal; ramp = lerp(ramp, _MidColor5.rgb, smoothstep(p - bcmSm, p + bcmSm, ndlWrapped)); bcmCum += _MidPct5; }
		{ half pH = bcmCum / bcmTotal; ramp = lerp(ramp, hColor, smoothstep(pH - bcmSm, pH + bcmSm, ndlWrapped)); }
	#elif defined(TCP2_RAMPTEXT)
		half3 ramp = tex2D(_Ramp, _RampOffset + ((ndlWrapped.xx - 0.5) * _RampScale) + 0.5).rgb;
	#elif defined(TCP2_RAMP_BANDS) || defined(TCP2_RAMP_BANDS_CRISP)
		half bands = _RampBands;

		half rampThreshold = _RampThreshold;
		half rampSmooth = _RampSmoothing * 0.5;
		half x = smoothstep(rampThreshold - rampSmooth, rampThreshold + rampSmooth, ndlWrapped);

		#if defined(TCP2_RAMP_BANDS_CRISP)
			half bandsSmooth = fwidth(ndlWrapped) * (2.0 + bands);
		#else
			half bandsSmooth = _RampBandsSmoothing * 0.5;
		#endif
		half3 ramp = saturate((smoothstep(0.5 - bandsSmooth, 0.5 + bandsSmooth, frac(x * bands)) + floor(x * bands)) / bands).xxx;
	#else
		#if defined(TCP2_RAMP_CRISP)
			half rampSmooth = fwidth(ndlWrapped) * 0.5;
		#else
			half rampSmooth = _RampSmoothing * 0.5;
		#endif
		half rampThreshold = _RampThreshold;
		half3 ramp = smoothstep(rampThreshold - rampSmooth, rampThreshold + rampSmooth, ndlWrapped).xxx;
	#endif
	return ramp;
}

// BCM: specular tint. Mixes _SpecularColor toward the surface colour so a multi-coloured albedo
// gets per-pixel highlight colour (a red decal on a blue box gets a red highlight) instead of one
// uniform colour. _SpecularAlbedoTint = 0 reproduces the original single-colour behaviour exactly.
//
// IMPORTANT: pass the UNMODIFIED albedo, sampled before the shadow-albedo / matcap / decal blends.
// Those blends are lighting dependent, so feeding the blended albedo in here would make the
// highlight colour drift as the light moves and bleed the matcap colour into the highlight.
half3 BCM_SpecularTint(half3 baseAlbedo)
{
	half3 tintSource = baseAlbedo;
	if (_SpecularPreserveBrightness > 0.5)
	{
		// Keep hue/saturation only, so a dark albedo still gets a full-strength highlight and the
		// Strength slider stays meaningful. Pure black falls back to white rather than losing the
		// highlight completely.
		half maxc = max(tintSource.r, max(tintSource.g, tintSource.b));
		tintSource = (maxc > 1e-3h) ? tintSource / maxc : half3(1, 1, 1);
	}
	return _SpecularColor.rgb * lerp(half3(1, 1, 1), tintSource, _SpecularAlbedoTint);
}

half CalculateSpecular(half3 lightDir, half3 viewDir, float3 normal, half specularMap)
{
	float3 halfDir = TCP2_SafeNormalize(float3(lightDir) + float3(viewDir));
	half nh = saturate(dot(normal, halfDir));

	#if defined(TCP2_SPECULAR_STYLIZED) || defined(TCP2_SPECULAR_CRISP)
		half specSize = 1 - (_SpecularToonSize * specularMap);
		nh = nh * (1.0 / (1.0 - specSize)) - (specSize / (1.0 - specSize));

		#if defined(TCP2_SPECULAR_CRISP)
			float specSmoothness = fwidth(nh);
		#else
			float specSmoothness = _SpecularToonSmoothness;
		#endif

		half spec = smoothstep(0, specSmoothness, nh);
	#else
		float specularRoughness = max(0.00001,  _SpecularRoughness) * specularMap;
		half roughness = specularRoughness * specularRoughness;
		half spec = GGX(nh, saturate(roughness));
		spec *= TCP2_PI * 0.05;
		#ifdef UNITY_COLORSPACE_GAMMA
			spec = max(0, sqrt(max(1e-4h, spec)));
			half surfaceReduction = 1.0 - 0.28 * roughness * specularRoughness;
		#else
			half surfaceReduction = 1.0 / (roughness*roughness + 1.0);
		#endif
		spec *= surfaceReduction;
	#endif

	return max(0, spec);
}

#if defined(_DBUFFER)
	// Derived from 'ApplyDecal' in URP's DBuffer.hlsl, directly fetch the decal data so that we can blend it accordingly
	DecalSurfaceData GetDecals(float4 positionCS)
	{
		FETCH_DBUFFER(DBuffer, _DBufferTexture, int2(positionCS.xy));

		DecalSurfaceData decalSurfaceData = (DecalSurfaceData)0;
		DECODE_FROM_DBUFFER(DBuffer, decalSurfaceData);

		#if !defined(_DBUFFER_MRT3)
			decalSurfaceData.MAOSAlpha = 0;
		#endif

		return decalSurfaceData;
	}
#endif

// Custom macros to separate shadows from attenuation
// Based on UNITY_LIGHT_ATTENUATION macros from "AutoLight.cginc"

#ifdef POINT
#	define TCP2_LIGHT_ATTENUATION(input, worldPos) \
		unityShadowCoord3 lightCoord = mul(unity_WorldToLight, unityShadowCoord4(worldPos, 1)).xyz; \
		half shadow = UNITY_SHADOW_ATTENUATION(input, worldPos); \
		half attenuation = tex2D(_LightTexture0, dot(lightCoord, lightCoord).rr).r;
#endif

#ifdef SPOT
#	define TCP2_LIGHT_ATTENUATION(input, worldPos) \
		DECLARE_LIGHT_COORD(input, worldPos); \
		half shadow = UNITY_SHADOW_ATTENUATION(input, worldPos); \
		half attenuation = (lightCoord.z > 0) * UnitySpotCookie(lightCoord) * UnitySpotAttenuate(lightCoord.xyz);
#endif

#ifdef DIRECTIONAL
#	define TCP2_LIGHT_ATTENUATION(input, worldPos) \
		half shadow = UNITY_SHADOW_ATTENUATION(input, worldPos); \
		half attenuation = 1;
#endif

#ifdef POINT_COOKIE
#	define TCP2_LIGHT_ATTENUATION(input, worldPos) \
		DECLARE_LIGHT_COORD(input, worldPos); \
		half shadow = UNITY_SHADOW_ATTENUATION(input, worldPos); \
		half attenuation = tex2D(_LightTextureB0, dot(lightCoord, lightCoord).rr).r * texCUBE(_LightTexture0, lightCoord).w;
#endif

#ifdef DIRECTIONAL_COOKIE
#	define TCP2_LIGHT_ATTENUATION(input, worldPos) \
		DECLARE_LIGHT_COORD(input, worldPos); \
		half shadow = UNITY_SHADOW_ATTENUATION(input, worldPos); \
		half attenuation = tex2D(_LightTexture0, lightCoord).w;
#endif

#if (defined(LIGHTMAP_ON) || defined(UNITY_PASS_META)) \
|| (defined(SHADOWS_SCREEN) && !defined(LIGHTMAP_ON) && !defined(UNITY_NO_SCREENSPACE_SHADOWS) && defined(SHADOWS_SHADOWMASK) && !defined(SHADER_API_GLES)) \
|| defined(SHADOWS_SHADOWMASK) \
|| defined(TCP2_UV2_AS_NORMALS) \
|| (!defined(TCP2_HYBRID_URP) && defined(TCP2_OUTLINE_LIGHTING_ALL))
	#define NEEDS_TEXCOORD1
#endif

// Vertex input
struct Attributes
{
	float4 vertex         : POSITION;
	float3 normal         : NORMAL;
	float4 tangent        : TANGENT;
	float4 texcoord0      : TEXCOORD0;
	#if defined(BCM_GROUND_SPLAT)
		half4 color       : COLOR;
	#endif
	#if defined(NEEDS_TEXCOORD1)
		float2 texcoord1  : TEXCOORD1;
	#endif
	#if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
		float2 texcoord2 : TEXCOORD2;
	#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Vertex output / Fragment input
struct Varyings
{
	float4 pos             : SV_POSITION;
	float3 normal          : NORMAL;
	float4 worldPos        : TEXCOORD0; /* w = fog coords */
	float4 texcoords       : TEXCOORD1; /* xy = main texcoords, zw = raw texcoords */
#if defined(BCM_GROUND_SPLAT)
	half4 splat            : TEXCOORD8; /* BCM: 지면 혼합 덮임 정도 (정점 컬러) */
#endif
#if defined(_NORMALMAP) || (defined(TCP2_MOBILE) && (defined(TCP2_RIM_LIGHTING) || (defined(TCP2_REFLECTIONS) && defined(TCP2_REFLECTIONS_FRESNEL)))) // if normalmap or (mobile + rim or fresnel)
	float4 tangentWS       : TEXCOORD2; /* w = ndv (mobile) */
#endif
#if defined(_NORMALMAP)
	float4 bitangentWS     : TEXCOORD3;
#endif
#if defined(TCP2_MATCAP) && !defined(_NORMALMAP)
	float4 matcap          : TEXCOORD4;
#endif
#if defined(TCP2_HYBRID_URP)
	#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
		float4 shadowCoord : TEXCOORD5; // compute shadow coord per-vertex for the main light
	#endif
	#ifdef _ADDITIONAL_LIGHTS_VERTEX
		half3 vertexLights : TEXCOORD6;
	#endif
	#if defined(LIGHTMAP_ON) && defined(DYNAMICLIGHTMAP_ON)
		float4 lightmapUV  : TEXCOORD7;
		#define staticLightmapUV lightmapUV.xy
		#define dynamicLightmapUV lightmapUV.zw
	#elif defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
		float2 lightmapUV  : TEXCOORD7;
		#define staticLightmapUV lightmapUV.xy
		#define dynamicLightmapUV lightmapUV.xy
	#endif
#else
	#if defined(DYNAMICLIGHTMAP_ON) || defined(LIGHTMAP_ON)
		float4 lmap        : TEXCOORD5;
	#endif
	#if !defined(SHADOW_CASTER_PASS)
		UNITY_LIGHTING_COORDS(6,7)
	#endif
#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

struct Varyings_Meta
{
	float4 positionCS   : SV_POSITION;
	float2 uv    : TEXCOORD0;
};

#if USE_FORWARD_PLUS || USE_CLUSTER_LIGHT_LOOP
	// Fake InputData struct needed for Forward+ macro
	struct InputDataForwardPlusDummy
	{
		float3  positionWS;
		float2  normalizedScreenSpaceUV;
	};
#endif

#if defined(UNITY_PASS_META)
	#define VERTEX_OUTPUT Varyings_Meta
#else
	#define VERTEX_OUTPUT Varyings
#endif

VERTEX_OUTPUT Vertex(Attributes input)
{
	// BCM: 바람. 모든 정점 경로의 맨 앞에서 위치만 밀어 놓으면 뒤 계산은 손댈 필요가 없다.
	// **패스마다 빠짐없이 넣어야 한다** — 메인만 밀면 그림자·깊이·아웃라인이 제자리에 남아
	// 나무에서 그림자가 떨어져 나오고 아웃라인이 실루엣 밖에 뜬다.
	BCM_ApplyWindSway(input.vertex);

	#if defined(UNITY_PASS_META)
		Varyings_Meta meta_output;
		#if defined(TCP2_HYBRID_URP)
			meta_output.positionCS = MetaVertexPosition(input.vertex, input.texcoord1, input.texcoord2, unity_LightmapST, unity_DynamicLightmapST);
		#else
			meta_output.positionCS = UnityMetaVertexPosition(input.vertex, input.texcoord1, input.texcoord2, unity_LightmapST, unity_DynamicLightmapST);
		#endif
		meta_output.uv = TRANSFORM_TEX(input.texcoord0, _BaseMap);
		return meta_output;
	#else

		Varyings output = (Varyings)0;

		UNITY_SETUP_INSTANCE_ID(input);
		UNITY_TRANSFER_INSTANCE_ID(input, output);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

		// Texture Coordinates
		output.texcoords.xy = input.texcoord0.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
		output.texcoords.zw = input.texcoord0.xy;
		#if defined(BCM_GROUND_SPLAT)
			output.splat = input.color;
		#endif

		#if defined(TCP2_HYBRID_URP)
			OUTPUT_LIGHTMAP_UV(input.texcoord1, unity_LightmapST, output.staticLightmapUV);
			#ifdef DYNAMICLIGHTMAP_ON
				output.dynamicLightmapUV = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
			#endif

			VertexPositionInputs vertexInput = GetVertexPositionInputs(input.vertex.xyz);
			#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				output.shadowCoord = GetShadowCoord(vertexInput);
			#endif
			float3 positionWS = vertexInput.positionWS;
			float4 positionCS = vertexInput.positionCS;
			output.pos = positionCS;

			VertexNormalInputs vertexNormalInput = GetVertexNormalInputs(input.normal, input.tangent);
			float3 normalWS = vertexNormalInput.normalWS;
			#if defined(_NORMALMAP)
				float3 tangentWS = vertexNormalInput.tangentWS;
				float3 bitangentWS = vertexNormalInput.bitangentWS;
			#endif

			#ifdef _ADDITIONAL_LIGHTS_VERTEX
				// Vertex lighting
				output.vertexLights = VertexLighting(positionWS, normalWS);
			#endif
		#else
			#ifdef LIGHTMAP_ON
				output.lmap.xy = input.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				output.lmap.zw = 0;
			#endif
			#ifdef DYNAMICLIGHTMAP_ON
				output.lmap.zw = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
			#endif

			float3 positionWS = mul(UNITY_MATRIX_M, input.vertex).xyz;
			float4 positionCS = UnityWorldToClipPos(positionWS);
			output.pos = positionCS;

			half sign = half(input.tangent.w) * half(unity_WorldTransformParams.w);
			float3 normalWS = UnityObjectToWorldNormal(input.normal);
			#if defined(_NORMALMAP)
				float3 tangentWS = UnityObjectToWorldDir(input.tangent.xyz);
				float3 bitangentWS = cross(normalWS, tangentWS) * sign;
			#endif

			#if !defined(SHADOW_CASTER_PASS)
				// This Unity macro expects the vertex input to be named 'v'
				#define v input
				UNITY_TRANSFER_LIGHTING(output, input.texcoord1.xy);
			#endif
		#endif

		// world position
		output.worldPos = float4(positionWS.xyz, 0);

		// Compute fog factor
		#if defined(TCP2_HYBRID_URP)
			output.worldPos.w = ComputeFogFactor(positionCS.z);
		#else
			UNITY_TRANSFER_FOG_COMBINED_WITH_WORLD_POS(output, positionCS);
		#endif

		// normal
		output.normal = normalWS;

		// tangent
		#if defined(_NORMALMAP) || (defined(TCP2_MOBILE) && (defined(TCP2_RIM_LIGHTING) || (defined(TCP2_REFLECTIONS) && defined(TCP2_REFLECTIONS_FRESNEL)))) // if mobile + rim or fresnel
			output.tangentWS = float4(0, 0, 0, 0);
		#endif
		#if defined(_NORMALMAP)
			output.tangentWS.xyz = tangentWS;
			output.bitangentWS.xyz = bitangentWS;
		#endif
		#if defined(TCP2_MOBILE) && (defined(TCP2_RIM_LIGHTING) || (defined(TCP2_REFLECTIONS) && defined(TCP2_REFLECTIONS_FRESNEL))) // if mobile + rim or fresnel
			// Calculate ndv in vertex shader
			#if defined(TCP2_HYBRID_URP)
				half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
			#else
				half3 viewDirWS = TCP2_SafeNormalize(UnityWorldSpaceViewDir(positionWS));
			#endif
			output.tangentWS.w = 1 - max(0, dot(viewDirWS, normalWS));
		#endif

		#if defined(TCP2_MATCAP) && !defined(_NORMALMAP)
			// MatCap
			float3 worldNorm = normalize(UNITY_MATRIX_I_M[0].xyz * input.normal.x + UNITY_MATRIX_I_M[1].xyz * input.normal.y + UNITY_MATRIX_I_M[2].xyz * input.normal.z);
			worldNorm = mul((float3x3)UNITY_MATRIX_V, worldNorm);
			float4 screenPos = ComputeScreenPos(positionCS);
			float3 perspectiveOffset = (screenPos.xyz / screenPos.w) - 0.5;
			worldNorm.xy -= (perspectiveOffset.xy * perspectiveOffset.z) * 0.5;
			output.matcap.xy = worldNorm.xy * 0.5 + 0.5;
		#endif

		return output;

	#endif
}

#if defined(TCP2_HYBRID_URP)
	void ProcessAdditionalLight(Light light, float3 normalWS, half3 albedo, inout half3 color, half3 lightingFactor
	#if !defined(TCP2_OUTLINE_PASS)
		, inout half3 emission
	#endif
	#if defined(_SCREEN_SPACE_OCCLUSION)
		, AmbientOcclusionFactor aoFactor
	#endif
	#if defined(TCP2_SPECULAR)
		, half4 specMod   // BCM: rgb = tinted specular colour, a = roughness/size map
		, half3 viewDirWS
	#endif
	#if defined(TCP2_RIM_LIGHTING) && defined(TCP2_RIM_LIGHTING_LIGHTMASK)
		, half rim
	#endif
		)
	{
		#if defined(_SCREEN_SPACE_OCCLUSION)
			light.color *= aoFactor.directAmbientOcclusion;
		#endif

		half shadowTerm = BCM_ShapeReceivedShadow(light.shadowAttenuation);
		half atten = shadowTerm * light.distanceAttenuation;
		half3 attenRGB = BCM_TintReceivedShadow(shadowTerm) * light.distanceAttenuation;
		half3 lightDir = light.direction;
		half3 lightColor = light.color.rgb;

		half ndl = dot(normalWS, lightDir);
		half ndlWrapped = ndl * 0.5 + 0.5;
		ndl = saturate(ndl);

		// Calculate ramp (additional lights use untinted ramp colors)
		half3 ramp = CalculateRamp(ndlWrapped, _SColor.rgb, _MidColor0.rgb, _HColor.rgb);

		// Apply attenuation (shadowmaps & point/spot lights attenuation)
		ramp *= attenRGB;

		#if defined(TCP2_RIM_LIGHTING) && defined(TCP2_RIM_LIGHTING_LIGHTMASK)
			half3 rimMask = ramp.xxx * lightColor.rgb;
		#endif

		// Apply highlight color only
		#if (defined(BCM_RAMP_COLOR) && defined(TCP2_RAMPTEXT)) || defined(BCM_RAMP_TRICOLOR)
			// Color Ramp mode: additive light biased by transition luminance (shadow end adds ~0)
			half bcmAddLuma = saturate(dot(ramp, half3(0.3333, 0.3334, 0.3333)));
			ramp = _HColor.rgb * bcmAddLuma;
		#else
			ramp = lerp(half3(0,0,0), _HColor.rgb, ramp);
		#endif

		// Output color
		IS_LIGHTING_FEATURE_ENABLED(DEBUGLIGHTINGFEATUREFLAGS_ADDITIONAL_LIGHTS)
		color += albedo.rgb * lightColor.rgb * ramp * lightingFactor;

		// Specular
		#if defined(TCP2_SPECULAR)
			half spec = CalculateSpecular(lightDir, viewDirWS, normalWS, specMod.a);
			IS_LIGHTING_FEATURE_ENABLED(DEBUGLIGHTINGFEATUREFLAGS_ADDITIONAL_LIGHTS)
			emission.rgb += spec * atten * ndl * lightColor.rgb * specMod.rgb * _SpecularStrength;
		#endif
		// Rim Lighting
		#if defined(TCP2_RIM_LIGHTING) && defined(TCP2_RIM_LIGHTING_LIGHTMASK)
			IS_LIGHTING_FEATURE_ENABLED(DEBUGLIGHTINGFEATUREFLAGS_ADDITIONAL_LIGHTS)
			emission.rgb += rimMask * rim * _RimColor.rgb;
		#endif
	}
#endif

// Note: calculations from the main pass are defined with UNITY_PASS_FORWARDBASE
// However it is left out sometimes because some keywords aren't defined for the
// Forward Add pass (e.g. TCP2_MATCAP, TCP2_REFLECTIONS, ...)

// --------------------------------------------------------------------------------------------------------------------
// BCM: 지면 텍스처 혼합 (스플랫). 유니티 Terrain 의 레이어 블렌드와 같은 개념을 청크 메시 한 장으로 한다.
//
// 정점 컬러 RGB = 1·2·3층, A = 풀이 '얼마나 덮였나'(0.5 가 경계선). C# 쪽 길 공식이 정점마다 굽는다 —
// 길 모양의 정의는 C# 한 곳에만 있고, 셰이더는 경계를 어떻게 녹일지만 안다.
//
// **경계를 텍스처 명도로 흔든다(height blend).** 덮임 값만 보간하면 1m 격자 위로 부드러운
// 그라데이션이 될 뿐이라 '흐릿한 띠'로 읽힌다. 위층 텍스처의 밝은 알갱이가 먼저 올라오게 하면
// 흙이 자갈 틈으로 스며든 것처럼 경계가 알갱이 단위로 부서진다.
//
// UV 는 월드 XZ / 주기(m). 청크를 어디서 잘라도 무늬가 이어진다(_BaseMap_ST 는 쓰지 않는다).
#if defined(BCM_GROUND_SPLAT)
// 값 노이즈. 텍스처 명도만으로는 대비가 낮아(잔해·자갈이 고르게 칠해져 있다) 경계가 부서지지 않고
// 뿌옇게 번지기만 했다. 월드 좌표 노이즈로 경계선 자체를 흔들고, 명도는 알갱이 단위의 잔결만 맡는다.
float BCM_VNoise(float2 p)
{
	float2 i = floor(p), f = frac(p);
	f = f * f * (3.0 - 2.0 * f);
	float4 h = frac(sin(float4(dot(i, float2(127.1, 311.7)), dot(i + float2(1, 0), float2(127.1, 311.7)),
	                         dot(i + float2(0, 1), float2(127.1, 311.7)), dot(i + float2(1, 1), float2(127.1, 311.7)))) * 43758.5453);
	return lerp(lerp(h.x, h.y, f.x), lerp(h.z, h.w, f.x), f.y);
}

half BCM_SplatMask(half cover, half3 upper, half n)
{
	half h = dot(upper, half3(0.299h, 0.587h, 0.114h));
	half x = cover + (h - 0.5h) * _SplatHeightBlend + (n - 0.5h) * _SplatNoise;
	half s = max(_SplatSharpness, 0.001h);
	return smoothstep(0.5h - s, 0.5h + s, x);
}

half4 BCM_GroundSplat(float2 xz, half4 cover)
{
	float4 inv = 1.0 / max(_SplatTiles, 0.001);
	half3 col = tex2D(_BaseMap,   xz * inv.x).rgb;
	half3 c1  = tex2D(_SplatTex1, xz * inv.y).rgb;
	half3 c2  = tex2D(_SplatTex2, xz * inv.z).rgb;
	half3 c3  = tex2D(_SplatTex3, xz * inv.w).rgb;
	half3 c4  = tex2D(_SplatTex4, xz / max(_SplatTile4, 0.001)).rgb * _SplatTint4.rgb;
	// 아래층부터 차례로 덮는다 — 칠하는 순서가 곧 우선순위다(흙길이 맨 위).
	// 굵은 요철(~1.3m) + 잔 요철(~0.35m) 두 장을 층마다 다르게 섞는다 — 같은 노이즈를 쓰면
	// 세 층의 경계가 같은 모양으로 겹쳐 흔들려 '한 겹 굵은 선'으로 읽힌다(쿼드 적층 때와 같은 교훈).
	float2 np = xz * _SplatNoiseScale;
	half nA = BCM_VNoise(np * 0.8);
	half nB = BCM_VNoise(np * 2.9 + 31.7);
	// 풀은 바닥 바로 위 — 길(재·자갈·흙)이 풀밭을 가로질러 덮는다. 풀 경계는 잔 노이즈를 뒤집어 써서
	// 다른 층과 모양이 겹치지 않게 한다.
	col = lerp(col, c4, BCM_SplatMask(cover.a, c4, (1.0h - nA) * 0.4h + (1.0h - nB) * 0.6h) * _SplatStrength.a);
	col = lerp(col, c1, BCM_SplatMask(cover.r, c1, nA * 0.6h + nB * 0.4h) * _SplatStrength.r);
	col = lerp(col, c2, BCM_SplatMask(cover.g, c2, (1.0h - nA) * 0.6h + nB * 0.4h) * _SplatStrength.g);
	col = lerp(col, c3, BCM_SplatMask(cover.b, c3, nA * 0.6h + (1.0h - nB) * 0.4h) * _SplatStrength.b);
	return half4(col, 1.0h);
}
#endif

half4 Fragment (
	Varyings input
	, half vFace : VFACE
#ifdef _WRITE_RENDERING_LAYERS
	#if UNITY_VERSION >= 60020000
		, out uint outRenderingLayers : SV_Target1
	#else
		, out float4 outRenderingLayers : SV_Target1
	#endif
#endif
	) : SV_Target
{
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	#ifdef _WRITE_RENDERING_LAYERS
		#if UNITY_VERSION >= 60020000
			outRenderingLayers = 0;
		#else
			outRenderingLayers = float4(0, 0, 0, 0);
		#endif
	#endif

	// LOD Crossfading
	#if defined(LOD_FADE_CROSSFADE)
		const float dither = Dither4x4(input.pos.xy);
		const float ditherThreshold = unity_LODFade.x - CopySign(dither, unity_LODFade.x);
		clip(ditherThreshold);
	#endif

	// Texture coordinates
	float2 mainTexcoord = input.texcoords.xy;
	float2 rawTexcoord = input.texcoords.zw;

	// Vectors
	float3 positionWS = input.worldPos.xyz;
	BCM_SEE_THROUGH_CLIP(positionWS, input.pos.xy);
	float3 normalWS = normalize(input.normal);
	normalWS.xyz *= (vFace < 0) ? -1.0 : 1.0;

	#if defined(DEBUG_DISPLAY)
		InputData debugInputData = (InputData)0;
		debugInputData.positionWS = positionWS;
		debugInputData.positionCS = input.pos;

		SurfaceData debugSurfaceData = (SurfaceData)0;
	#endif

	#if defined(TCP2_HYBRID_URP)
		half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
	#else
		half3 viewDirWS = TCP2_SafeNormalize(UnityWorldSpaceViewDir(positionWS));
	#endif
	#if defined(_NORMALMAP)
		half3 tangentWS = input.tangentWS.xyz;
		half3 bitangentWS = input.bitangentWS.xyz;
		half3x3 tangentToWorldMatrix = half3x3(tangentWS.xyz, bitangentWS.xyz, normalWS.xyz);
	#endif

	// Base

	#if defined(BCM_GROUND_SPLAT)
		half4 albedo = BCM_GroundSplat(positionWS.xz, input.splat);
	#else
		half4 albedo = tex2D(_BaseMap, mainTexcoord).rgba;
	#endif
	albedo.rgb *= _BaseColor.rgb;
	half alpha = albedo.a * _BaseColor.a;
	// BCM: snapshot for the specular tint. Taken here on purpose - albedo is later blended with the
	// shadow albedo texture, the matcap and decals, all of which vary with lighting.
	half3 baseAlbedo = albedo.rgb;

	#if defined(BCM_MULTIPLY)
	// BCM: Multiply section - multiply the albedo by _MultiplyColor in the region the mask marks.
	// Mask defaults to white, so with no map assigned the whole surface is tinted; assign a mask to
	// confine it. _MultiplyColor defaults to white (identity), so nothing changes until a colour is set.
	{
		half4 mulMaskTex = tex2D(_MultiplyMap, mainTexcoord);
		half mulMask;
		int mulCh = (int)_MultiplyMaskChannel;
		if      (mulCh == 3) mulMask = mulMaskTex.a;
		else if (mulCh == 2) mulMask = mulMaskTex.b;
		else if (mulCh == 1) mulMask = mulMaskTex.g;
		else                 mulMask = mulMaskTex.r;
		mulMask = saturate(mulMask * _MultiplyStrength);
		albedo.rgb *= lerp(half3(1.0, 1.0, 1.0), _MultiplyColor.rgb, mulMask);
	}
	#endif
	half3 emission = half3(0,0,0);

	// Normal Mapping
	#if defined(_NORMALMAP)
		half4 normalMap = tex2D(_BumpMap, rawTexcoord * _BumpMap_ST.xy + _BumpMap_ST.zw).rgba;
		half3 normalTS = UnpackScaleNormal(normalMap, _BumpScale);
		normalWS = mul(normalTS, tangentToWorldMatrix);

		#if defined(DEBUG_DISPLAY)
			if (_DebugLightingMode == DEBUGLIGHTINGMODE_LIGHTING_WITHOUT_NORMAL_MAPS || _DebugLightingMode == DEBUGLIGHTINGMODE_REFLECTIONS)
			{
				// Cancel normal map for rendering debugger
				normalWS = normalize(input.normal);
			}
		#endif
	#endif

	// URP Decals
	#if defined(_DBUFFER)
		#if defined(_DBUFFER_MRT2) || defined(_DBUFFER_MRT3)
			#define HAS_DECAL_NORMALS
		#endif
		#if defined(_DBUFFER_MRT3)
			#define HAS_DECAL_MAOS
		#endif

		DecalSurfaceData decals = GetDecals(input.pos);
		albedo.rgb = albedo.rgb * decals.baseColor.a + decals.baseColor.rgb;
		#if defined(HAS_DECAL_NORMALS)
			// Always test the normal as we can have decompression artifact
			if (decals.normalWS.w < 1.0)
			{
				normalWS.xyz = normalize(normalWS.xyz * decals.normalWS.w + decals.normalWS.xyz);
			}
		#endif
	#endif

	// Alpha Testing
	#if defined(_ALPHATEST_ON)
		clip(alpha - _Cutoff);
	#endif

	// Emission
	#if defined(_EMISSION)
		emission = _EmissionColor.rgb;
		#if defined(TCP2_MOBILE)
			half4 emissionMap = tex2D(_EmissionMap, rawTexcoord * _EmissionMap_ST.xy + _EmissionMap_ST.zw);
			emission *= emissionMap.rgb;
		#else
			if (_EmissionChannel < 5)
			{
				half4 emissionMap = tex2D(_EmissionMap, rawTexcoord * _EmissionMap_ST.xy + _EmissionMap_ST.zw);
				if (_EmissionChannel >= 4)		emission *= emissionMap.rgb;
				else if (_EmissionChannel >= 3)	emission *= emissionMap.a;
				else if (_EmissionChannel >= 2) emission *= emissionMap.b;
				else if (_EmissionChannel >= 1) emission *= emissionMap.g;
				else							emission *= emissionMap.r;
			}
		#endif
		#if defined(DEBUG_DISPLAY)
			debugSurfaceData.emission = emission;
		#endif
	#endif

	#if defined(UNITY_PASS_META)
		half3 meta_albedo = albedo.rgb;
		half3 meta_emission = emission.rgb;
		half3 meta_specular = half3(0, 0, 0);
	#endif

	// MatCap
	#if defined(TCP2_MATCAP)
		// Screen-space matcap UV: per-pixel view normal when a normal map is active, otherwise the
		// perspective-corrected UV the vertex stage already built. Shared by every matcap texture below.
		#if defined(_NORMALMAP)
			half3 matcapCoordsNormal = mul((float3x3)UNITY_MATRIX_V, normalWS);
			half2 matcapUV = matcapCoordsNormal.xy * 0.5 + 0.5;
		#else
			half2 matcapUV = input.matcap.xy;
		#endif

		// Enable Mask - shared by the single-matcap and the per-region paths.
		half matcapMask = 1.0;
		#if defined(TCP2_MATCAP_MASK)
			half4 matcapMaskTex = tex2D(_MatCapMask, mainTexcoord);
			#if defined(TCP2_MOBILE)
				matcapMask *= matcapMaskTex.a;
			#else
				if (_MatCapMaskChannel >= 3)
				{
					matcapMask *= matcapMaskTex.a;
				}
				else if (_MatCapMaskChannel >= 2)
				{
					matcapMask *= matcapMaskTex.b;
				}
				else if (_MatCapMaskChannel >= 1)
				{
					matcapMask *= matcapMaskTex.g;
				}
				else
				{
					matcapMask *= matcapMaskTex.r;
				}
			#endif
		#endif

		#if defined(BCM_MATCAP_CHANNEL) && !defined(TCP2_MOBILE)
			// BCM: RGBA region map -> four independent matcaps. Every channel is a *weight*, never an
			// index, and that is what keeps region borders clean: where a mip level or a bilinear tap
			// blends red into green the result is a red/green cross-fade instead of a spurious "yellow"
			// region. Weights are normalised so overlapping regions cannot push the total above 1.
			half4 regionWeight = tex2D(_MatCapChannelTex, mainTexcoord);
			#if !defined(BCM_MATCAP_CHANNEL_A)
				// A is opt-in. A channel map saved without an alpha channel reads as alpha = 1 on every
				// texel, which would smear the A matcap over the whole mesh and, through the
				// normalisation below, halve every R/G/B weight.
				regionWeight.a = 0;
			#endif
			regionWeight /= max(1.0, regionWeight.r + regionWeight.g + regionWeight.b + regionWeight.a);
			regionWeight *= matcapMask;

			// Replace and Additive contributions accumulate separately and apply once each. Blending one
			// channel at a time would make the result depend on channel order wherever regions overlap.
			half3 matcapAdd     = half3(0, 0, 0);
			half3 matcapReplace = half3(0, 0, 0);
			half  replaceWeight = 0.0;

			// A textual macro rather than a helper function: taking sampler2D as a parameter is not
			// portable across every target this shader compiles for.
			#define BCM_MATCAP_REGION(tex, tint, power, type, weight) \
				{ \
					half3 regionMatCap = tex2D(tex, matcapUV).rgb * (tint).rgb * (power); \
					if ((type) >= 1) { matcapReplace += regionMatCap * (weight); replaceWeight += (weight); } \
					else             { matcapAdd     += regionMatCap * (weight); } \
				}

			BCM_MATCAP_REGION(_MatCapTexR, _MatCapColorR, _MatCapPowerR, _MatCapTypeR, regionWeight.r)
			BCM_MATCAP_REGION(_MatCapTexG, _MatCapColorG, _MatCapPowerG, _MatCapTypeG, regionWeight.g)
			BCM_MATCAP_REGION(_MatCapTexB, _MatCapColorB, _MatCapPowerB, _MatCapTypeB, regionWeight.b)
			#if defined(BCM_MATCAP_CHANNEL_A)
				BCM_MATCAP_REGION(_MatCapTexA, _MatCapColorA, _MatCapPowerA, _MatCapTypeA, regionWeight.a)
			#endif

			#undef BCM_MATCAP_REGION

			if (replaceWeight > 0.0001)
			{
				// Weighted average of the Replace regions, faded in by their combined coverage.
				albedo.rgb = lerp(albedo.rgb, matcapReplace / replaceWeight, saturate(replaceWeight));
			}
			emission += matcapAdd;
		#else
			half3 matcap = tex2D(_MatCapTex, matcapUV).rgb * _MatCapColor.rgb;
			matcap *= _MatCapPower; // BCM: matcap intensity/power
			#if defined(TCP2_MOBILE)
				emission += matcap * matcapMask;
			#else
				if (_MatCapType >= 1)
				{
					albedo.rgb = lerp(albedo.rgb, matcap.rgb, matcapMask);
				}
				else
				{
					emission += matcap * matcapMask;
				}
			#endif
		#endif
	#endif

	// Lighting

	// Occlusion
	#if defined(TCP2_OCCLUSION)
		#if defined(TCP2_MOBILE)
			half occlusion = tex2D(_OcclusionMap, mainTexcoord).a;
		#elif defined(BCM_OCCLUSION_ADV)
			// BCM: channel-mask + remap occlusion. Mask picks/blends RGBA channels via a dot product,
			// then the value is remapped from [0,1] into [RemapStart, RemapEnd].
			half4 occTex = tex2D(_OcclusionMap, mainTexcoord).rgba;
			half occlusion = dot(occTex, _OcclusionMapChannelMask);
			occlusion = lerp(_OcclusionRemapStart, _OcclusionRemapEnd, saturate(occlusion));
		#else
			half occlusion = 1.0;
			if (_OcclusionChannel >= 4)
			{
				occlusion = tex2D(_OcclusionMap, mainTexcoord).a;
			}
			else if (_OcclusionChannel >= 3)
			{
				occlusion = tex2D(_OcclusionMap, mainTexcoord).b;
			}
			else if (_OcclusionChannel >= 2)
			{
				occlusion = tex2D(_OcclusionMap, mainTexcoord).g;
			}
			else if (_OcclusionChannel >= 1)
			{
				occlusion = tex2D(_OcclusionMap, mainTexcoord).r;
			}
			else
			{
				occlusion = albedo.a;
			}
		#endif
		occlusion = lerp(1, occlusion, _OcclusionStrength);
	#else
		half occlusion = 1.0;
	#endif
	#if defined(HAS_DECAL_MAOS)
		occlusion = occlusion * decals.MAOSAlpha + decals.occlusion;
	#endif

	#if defined(TCP2_HYBRID_URP) && defined(_SCREEN_SPACE_OCCLUSION)
		float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.pos);
		AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(normalizedScreenSpaceUV);
		occlusion = min(occlusion, aoFactor.indirectAmbientOcclusion);
	#endif

	// Setup lighting environment (Built-In)
	#if !defined(TCP2_HYBRID_URP)
		half3 lightDir = normalize(UnityWorldSpaceLightDir(positionWS));
		half3 lightColor = _LightColor0.rgb;
		#if defined(SHADOW_CASTER_PASS)
			half atten = 1.0;
			half3 attenRGB = half3(1.0, 1.0, 1.0);
		#else
			TCP2_LIGHT_ATTENUATION(input, positionWS)
			#if defined(_RECEIVE_SHADOWS_OFF)
				half atten = attenuation;
				half3 attenRGB = attenuation.xxx;
			#else
				half shadowTerm = BCM_ShapeReceivedShadow(shadow);
				half atten = shadowTerm * attenuation;
				half3 attenRGB = BCM_TintReceivedShadow(shadowTerm) * attenuation;
			#endif
		#endif
	#endif

	#if !defined(TCP2_HYBRID_URP) && defined(UNITY_PASS_FORWARDBASE)
		UnityGI gi;
		UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
		gi.indirect.diffuse = 0;
		gi.indirect.specular = 0;
		gi.light.color = lightColor;
		gi.light.dir = lightDir;

		// Call GI (lightmaps/SH/reflections) lighting function
		UnityGIInput giInput;
		UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
		giInput.light = gi.light;
		giInput.worldPos = positionWS;
		giInput.worldViewDir = viewDirWS;
		giInput.atten = atten;
		#if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
			giInput.lightmapUV = input.lmap;
		#else
			giInput.lightmapUV = 0.0;
		#endif
		giInput.ambient.rgb = 0.0;
		giInput.probeHDR[0] = unity_SpecCube0_HDR;
		giInput.probeHDR[1] = unity_SpecCube1_HDR;
		#if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
			giInput.boxMin[0] = unity_SpecCube0_BoxMin; // .w holds lerp value for blending
		#endif
		#ifdef UNITY_SPECCUBE_BOX_PROJECTION
			giInput.boxMax[0] = unity_SpecCube0_BoxMax;
			giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
			giInput.boxMax[1] = unity_SpecCube1_BoxMax;
			giInput.boxMin[1] = unity_SpecCube1_BoxMin;
			giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
		#endif

		half3 shNormal = (_SingleIndirectColor > 0) ? viewDirWS : normalWS;
		#if defined(TCP2_REFLECTIONS)
			// GI: indirect diffuse & specular
			half smoothness = _ReflectionSmoothness;
			Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(smoothness, giInput.worldViewDir, normalWS, half3(0,0,0));
			gi = UnityGlobalIllumination(giInput, occlusion, shNormal, g);
		#else
			// GI: indirect diffuse only
			gi = UnityGlobalIllumination(giInput, occlusion, shNormal);
		#endif

		gi.light.color = _LightColor0.rgb; // remove attenuation, taken into account separately
	#endif

	#if defined(TCP2_HYBRID_URP)
		#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
			float4 shadowCoord = input.shadowCoord;
		#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
			float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
		#else
			float4 shadowCoord = float4(0, 0, 0, 0);
		#endif

		#if defined(USE_APV_PROBE_OCCLUSION)
			half4 shadowMask = probeOcclusion;
		#elif defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
			half4 shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
		#elif !defined (LIGHTMAP_ON)
			half4 shadowMask = unity_ProbesOcclusion;
		#else
			half4 shadowMask = half4(1, 1, 1, 1);
		#endif

		#if defined(DEBUG_DISPLAY)
			debugInputData.shadowMask = shadowMask;
		#endif

		#if URP_VERSION <= 7
			Light mainLight = GetMainLight(shadowCoord);
		#else
			Light mainLight = GetMainLight(shadowCoord, positionWS, shadowMask);
		#endif
	#endif

	// Ambient/indirect lighting
	#if defined(UNITY_PASS_FORWARDBASE)
		half3 indirectDiffuse = 0;
		#if !defined(TCP2_MOBILE)
		if (_IndirectIntensity > 0)
		#endif
		{
			#if defined(TCP2_HYBRID_URP)
					// Normal is required in case Directional lightmaps are baked
				#if defined(LIGHTMAP_ON) && defined(DYNAMICLIGHTMAP_ON)
					// Static & Dynamic Lightmap
					half3 bakedGI = SampleLightmap(input.staticLightmapUV, input.dynamicLightmapUV, normalWS);
					MixRealtimeAndBakedGI(mainLight, normalWS, bakedGI, half4(0, 0, 0, 0));
				#elif defined(LIGHTMAP_ON)
					// Static Lightmap
					half3 bakedGI = SampleLightmap(input.staticLightmapUV, 0, normalWS);
					MixRealtimeAndBakedGI(mainLight, normalWS, bakedGI, half4(0, 0, 0, 0));
				#elif defined(DYNAMICLIGHTMAP_ON)
					// Dynamic Lightmap
					half3 bakedGI = SampleLightmap(0, input.dynamicLightmapUV, normalWS);
					MixRealtimeAndBakedGI(mainLight, normalWS, bakedGI, half4(0, 0, 0, 0));
				#elif defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
					// Adaptive Probe Volumes APV
					#ifdef USE_APV_PROBE_OCCLUSION
						float4 probeOcclusion;
						half3 bakedGI = SampleProbeVolumePixel(0.0, positionWS, normalWS, viewDirWS, input.pos.xy, 1.0, probeOcclusion);
					#else
						half3 bakedGI = SampleProbeVolumePixel(0.0, positionWS, normalWS, viewDirWS, input.pos.xy);
					#endif
				#else
					// Sample SH fully per-pixel
					half3 bakedGI = SampleSH(_SingleIndirectColor > 0 ? viewDirWS : normalWS);
				#endif
				#if defined(DEBUG_DISPLAY)
					debugInputData.bakedGI = bakedGI;
				#endif
				indirectDiffuse = bakedGI * occlusion * albedo.rgb * _IndirectIntensity;
			#else
				indirectDiffuse = gi.indirect.diffuse * albedo.rgb * _IndirectIntensity;
			#endif
		}
	#endif

	#if defined(TCP2_HYBRID_URP)
		#if URP_VERSION >= 14
			uint meshRenderingLayers = GetMeshRenderingLayer();
		#elif URP_VERSION >= 12
			uint meshRenderingLayers = GetMeshRenderingLightLayer();
		#endif

		#if defined(_SCREEN_SPACE_OCCLUSION)
			mainLight.color *= aoFactor.directAmbientOcclusion;
		#endif

		#if URP_VERSION >= 12
			half3 lightDir = half3(0, 1, 0);
			half3 lightColor = half3(0, 0, 0);
			#if (URP_VERSION >= 14 && defined(_LIGHT_LAYERS)) || URP_VERSION <= 12
				if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
			#endif
				{
					lightDir = mainLight.direction;
					lightColor = mainLight.color.rgb;
				}
		#else
			half3 lightDir = mainLight.direction;
			half3 lightColor = mainLight.color.rgb;
		#endif
		half shadowTerm = BCM_ShapeReceivedShadow(mainLight.shadowAttenuation);
		half atten = shadowTerm * mainLight.distanceAttenuation;
		half3 attenRGB = BCM_TintReceivedShadow(shadowTerm) * mainLight.distanceAttenuation;
	#endif

	half ndl = dot(normalWS, lightDir);
	half ndlWrapped = ndl * 0.5 + 0.5;
	ndl = saturate(ndl);

	// BCM: "Affect Direct Light" - fold occlusion INTO the lighting term BEFORE the toon ramp step, the way
	// UTS's Shade-Position / Shading-Grade map works. Occluded areas cross the SAME shadow threshold as the
	// lit shadow, so they resolve to the SAME flat Shadow Color (_SColor) = one unified shadow. Going through
	// the step means overlap with the lit shadow does NOT compound (no darker patch), and it is not a washed-
	// out mid-tone either.
	#if defined(BCM_OCCLUSION_DIRECT)
		ndlWrapped *= occlusion;
	#endif

	// BCM: Color Maps (like UTS 1st/2nd Shade Map) - tint Highlight / Shadow / Middle-1 ramp colors by a
	// texture (multiply). Default maps are white = no change. Sampled with the main UV.
	half3 hColMapped   = _HColor.rgb    * tex2D(_HColorMap,    mainTexcoord).rgb;
	half3 sColMapped   = _SColor.rgb    * tex2D(_SColorMap,    mainTexcoord).rgb;
	half3 mid0Mapped   = _MidColor0.rgb * tex2D(_MidColor0Map, mainTexcoord).rgb;

	// Calculate ramp
	half3 ramp = CalculateRamp(ndlWrapped, sColMapped, mid0Mapped, hColMapped);

	// Apply attenuation
	ramp *= attenRGB;

	#if defined(TCP2_RIM_LIGHTING)
		#if defined(TCP2_RIM_LIGHTING_LIGHTMASK)
			half3 rimMask = ramp.xxx * lightColor.rgb;
		#else
			half3 rimMask = half3(1, 1, 1);
		#endif
	#endif

	// Shadow Albedo
	#if defined(TCP2_SHADOW_TEXTURE)
		half4 shadowAlbedo = tex2D(_ShadowBaseMap, mainTexcoord).rgba;
		albedo = lerp(shadowAlbedo, albedo, ramp.x);
	#endif

	// Highlight/shadow colors (use the color-map-tinted highlight)
	#if !defined(TCP2_SHADOW_LIGHT_COLOR)
		half3 highlightColor = hColMapped * lightColor.rgb;
	#else
		half3 highlightColor = hColMapped;
	#endif
	#if (defined(BCM_RAMP_COLOR) && defined(TCP2_RAMPTEXT)) || defined(BCM_RAMP_TRICOLOR)
		// BCM: Color Ramp mode. 'ramp' already holds the full gradient colors sampled from the
		// ramp texture (shadow color -> optional mid colors -> highlight color), so use it directly
		// instead of remapping between _SColor and _HColor.
		#if defined(UNITY_PASS_FORWARDBASE) || defined(DIRECTIONAL_COOKIE)
			// use gradient colors as-is (already attenuated above)
		#else
			// additive pass: bias by transition luminance so the shadow end doesn't add light
			half bcmRampLuma = saturate(dot(ramp, half3(0.3333, 0.3334, 0.3333)));
			ramp = highlightColor * bcmRampLuma;
		#endif
	#else
		#if defined(UNITY_PASS_FORWARDBASE) || defined(DIRECTIONAL_COOKIE)
			ramp = lerp(sColMapped, highlightColor, ramp);
		#else
			ramp = lerp(half3(0, 0, 0), highlightColor, ramp);
		#endif
	#endif

	#if defined(TCP2_SHADOW_LIGHT_COLOR)
		ramp *= lightColor.rgb;
	#endif

	#if defined(DEBUG_DISPLAY)
		// Lighting overlays:
		if (_DebugLightingMode == DEBUGLIGHTINGMODE_SHADOW_CASCADES)
		{
			albedo.rgb = CalculateDebugShadowCascadeColor(debugInputData);
		}
		else if (_DebugLightingMode == DEBUGLIGHTINGMODE_LIGHTING_WITHOUT_NORMAL_MAPS || _DebugLightingMode == DEBUGLIGHTINGMODE_LIGHTING_WITH_NORMAL_MAPS)
		{
			albedo.rgb = half3(1, 1, 1);
		}
	#endif

	// BCM: Light Received - per-material control over how much (and what color of) DIRECT light this
	// surface gets. _LightReceived is a 0..100 percentage (100 = full), _LightReceivedColor tints it.
	// Indirect/ambient (indirectDiffuse) is intentionally NOT scaled here - use _IndirectIntensity for that.
	half3 lightRecvMul = (_LightReceived * 0.01) * _LightReceivedColor.rgb;

	// BCM: additional (point/spot) lights are additive, so occlusion is applied by scaling their
	// contribution (the main light's occlusion is handled on 'ramp' above). Unchanged unless BCM_OCCLUSION_DIRECT.
	half3 addLightMul = lightRecvMul;
	#if defined(BCM_OCCLUSION_DIRECT)
		addLightMul *= occlusion;
	#endif

	// Output color (occlusion for the main light is already folded into 'ramp' above)
	half3 color = albedo.rgb * ramp * lightRecvMul;

	#if defined(DEBUG_DISPLAY)
		if (!IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_MAIN_LIGHT))
		{
			// Cancel main light for rendering debugger
			color = 0;
		}
	#endif

	// Apply ambient/indirect lighting
	#if defined(UNITY_PASS_FORWARDBASE)
		#if !defined(TCP2_MOBILE)
		if (_IndirectIntensity > 0)
		#endif
		{
			#if defined(TCP2_HYBRID_URP)
			IS_LIGHTING_FEATURE_ENABLED(DEBUGLIGHTINGFEATUREFLAGS_GLOBAL_ILLUMINATION)
			#endif
			color.rgb += indirectDiffuse;
		}
	#endif


	// Calculate N.V
	#if defined(TCP2_RIM_LIGHTING) || (defined(TCP2_REFLECTIONS) && defined(TCP2_REFLECTIONS_FRESNEL))
		#if defined(TCP2_MOBILE)
			half ndv = input.tangentWS.w;
		#else
			half ndv = 1 - max(0, dot(viewDirWS, normalWS));
		#endif
	#endif

	// Rim Lighting
	#if defined(TCP2_RIM_LIGHTING)
		#if defined(UNITY_PASS_FORWARDBASE) || defined(TCP2_RIM_LIGHTING_LIGHTMASK)
			half rim = smoothstep(_RimMin, _RimMax, ndv);
			IS_LIGHTING_FEATURE_ENABLED(DEBUGLIGHTINGFEATUREFLAGS_MAIN_LIGHT)
			emission.rgb += rimMask.rgb * rim * _RimColor.rgb;

			// BCM: Directional Rim (Light Direction). Second rim term masked by the main light
			// direction, so the rim appears on the light-facing side. _RimLightDir (0..1) blends
			// between an all-around rim (0) and a fully light-direction-masked rim (1).
			#if defined(BCM_RIM_LIGHTDIR)
				half rimLd = smoothstep(_RimMin2, _RimMax2, ndv);
				half rimLdMask = saturate(dot(normalWS, lightDir) * 0.5 + 0.5);
				rimLd *= lerp(1.0, rimLdMask, _RimLightDir);
				emission.rgb += rimMask.rgb * rimLd * _RimColor2.rgb;
			#endif
		#endif
	#endif

	// Specular
	#if defined(TCP2_SPECULAR)

		half specularMap = 1.0;
		#if defined(TCP2_MOBILE)
			specularMap *= tex2D(_SpecGlossMap, mainTexcoord).a;
		#else
			if (_SpecularMapType >= 5)
			{
				specularMap *= tex2D(_SpecGlossMap, mainTexcoord).a;
			}
			else if (_SpecularMapType >= 4)
			{
				specularMap *= tex2D(_SpecGlossMap, mainTexcoord).b;
			}
			else if (_SpecularMapType >= 3)
			{
				specularMap *= tex2D(_SpecGlossMap, mainTexcoord).g;
			}
			else if (_SpecularMapType >= 2)
			{
				specularMap *= tex2D(_SpecGlossMap, mainTexcoord).r;
			}
			else if (_SpecularMapType >= 1)
			{
				specularMap *= albedo.a;
			}
		#endif
		#if defined(HAS_DECAL_MAOS)
			specularMap = specularMap * decals.MAOSAlpha + decals.smoothness;
		#endif

		// BCM: rgb = tinted specular colour, a = the roughness/size map. Packed into one half4 so the
		// additional-light helper keeps its parameter count.
		half4 specMod = half4(BCM_SpecularTint(baseAlbedo), specularMap);

		half spec = CalculateSpecular(lightDir, viewDirWS, normalWS, specMod.a);
		IS_LIGHTING_FEATURE_ENABLED(DEBUGLIGHTINGFEATUREFLAGS_MAIN_LIGHT)
		emission.rgb += spec * atten * ndl * lightColor.rgb * specMod.rgb * _SpecularStrength;

		#if defined(DEBUG_DISPLAY)
			debugSurfaceData.specular = specMod.rgb;
		#endif

		#if defined(UNITY_PASS_META)
			meta_specular = specMod.a * specMod.rgb;
			meta_albedo += specMod.a * specMod.rgb * max(0.00001, _SpecularRoughness * _SpecularRoughness) * 0.5;
		#endif

	#endif

	// Meta pass
	#if defined(UNITY_PASS_META)
		#if defined(TCP2_HYBRID_URP)
			MetaInput metaInput;
		#else
			UnityMetaInput metaInput;
			UNITY_INITIALIZE_OUTPUT(UnityMetaInput, metaInput);
		#endif
		metaInput.Albedo = meta_albedo.rgb;
		metaInput.Emission = meta_emission.rgb;
		#if !defined(TCP2_HYBRID_URP)
			metaInput.SpecularColor = meta_specular.rgb;
		#endif

		#if defined(TCP2_HYBRID_URP)
			return MetaFragment(metaInput);
		#else
			return UnityMetaFragment(metaInput);
		#endif
	#endif

	// Additional lights loop
	#if defined(TCP2_HYBRID_URP) && defined(_ADDITIONAL_LIGHTS)
		uint pixelLightCount = GetAdditionalLightsCount();
		#if URP_VERSION >= 12
			#if USE_FORWARD_PLUS || USE_CLUSTER_LIGHT_LOOP
				// Additional directional lights in Forward+
				for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
				{
					#if URP_VERSION >= 171
						CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
					#else
						FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
					#endif

					Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);

					#ifdef _LIGHT_LAYERS
						if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
					#endif
					{
						ProcessAdditionalLight(light, normalWS, albedo.rgb, color, addLightMul
							#if !defined(TCP2_OUTLINE_PASS)
								, emission
							#endif
							#if defined(_SCREEN_SPACE_OCCLUSION)
								, aoFactor
							#endif
							#if defined(TCP2_SPECULAR)
								, specMod
								, viewDirWS
							#endif
							#if defined(TCP2_RIM_LIGHTING) && defined(TCP2_RIM_LIGHTING_LIGHTMASK)
								, rim
							#endif
						);
					}
				}

				// Data with dummy struct used in Forward+ macro (LIGHT_LOOP_BEGIN)
				#if !defined(_SCREEN_SPACE_OCCLUSION)
					float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.pos);
				#endif
				InputDataForwardPlusDummy inputData;
				inputData.normalizedScreenSpaceUV = normalizedScreenSpaceUV;
				inputData.positionWS = positionWS;
			#endif

			LIGHT_LOOP_BEGIN(pixelLightCount)
		#else
			for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
			{
		#endif

		#if URP_VERSION <= 7
				Light light = GetAdditionalLight(lightIndex, positionWS);
		#else
				Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
		#endif

		#if (URP_VERSION >= 12 && URP_VERSION < 14) || (URP_VERSION >= 14 && defined(_LIGHT_LAYERS))
			if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
		#endif
			{
				ProcessAdditionalLight(light, normalWS, albedo.rgb, color, addLightMul
					#if !defined(TCP2_OUTLINE_PASS)
						, emission
					#endif
					#if defined(_SCREEN_SPACE_OCCLUSION)
						, aoFactor
					#endif
					#if defined(TCP2_SPECULAR)
						, specMod
						, viewDirWS
					#endif
					#if defined(TCP2_RIM_LIGHTING) && defined(TCP2_RIM_LIGHTING_LIGHTMASK)
						, rim
					#endif
				);
			}
		#if URP_VERSION >= 12
			LIGHT_LOOP_END
		#else
			}
		#endif

		#ifdef _ADDITIONAL_LIGHTS_VERTEX
			IS_LIGHTING_FEATURE_ENABLED(DEBUGLIGHTINGFEATUREFLAGS_VERTEX_LIGHTING)
			color += input.vertexLights * albedo.rgb;
		#endif
	#endif

	// Environment Reflection
	#if defined(TCP2_REFLECTIONS)
		half3 reflections = half3(0, 0, 0);

		half reflectionRoughness = _ReflectionSmoothness;
		#if defined(DEBUG_DISPLAY)
			if (_DebugLightingMode == DEBUGLIGHTINGMODE_REFLECTIONS) reflectionRoughness = 1.0;
		#endif
		half reflectionMask = 1.0;
		#if defined(TCP2_MOBILE)
			reflectionRoughness *= tex2D(_ReflectionTex, mainTexcoord).a;
		#else
			if (_ReflectionMapType > 0)
			{
				if (_ReflectionMapType <= 1)
				{
					reflectionRoughness *= albedo.a;
				}
				else if (_ReflectionMapType <= 2)
				{
					reflectionRoughness *= tex2D(_ReflectionTex, mainTexcoord).r;
				}
				else if (_ReflectionMapType <= 3)
				{
					reflectionRoughness *= tex2D(_ReflectionTex, mainTexcoord).g;
				}
				else if (_ReflectionMapType <= 4)
				{
					reflectionRoughness *= tex2D(_ReflectionTex, mainTexcoord).b;
				}
				else if (_ReflectionMapType <= 5)
				{
					reflectionRoughness *= tex2D(_ReflectionTex, mainTexcoord).a;
				}
				else if (_ReflectionMapType <= 6)
				{
					reflectionMask *= albedo.a;
				}
				else if (_ReflectionMapType <= 7)
				{
					reflectionMask *= tex2D(_ReflectionTex, mainTexcoord).r;
				}
				else if (_ReflectionMapType <= 8)
				{
					reflectionMask *= tex2D(_ReflectionTex, mainTexcoord).g;
				}
				else if (_ReflectionMapType <= 9)
				{
					reflectionMask *= tex2D(_ReflectionTex, mainTexcoord).b;
				}
				else if (_ReflectionMapType <= 10)
				{
					reflectionMask *= tex2D(_ReflectionTex, mainTexcoord).a;
				}
			}
		#endif
		#if defined(HAS_DECAL_MAOS)
			reflectionRoughness = reflectionRoughness * decals.MAOSAlpha + decals.smoothness;
		#endif
		reflectionRoughness = 1 - reflectionRoughness;

		#if defined(TCP2_HYBRID_URP)
			half3 reflectVector = reflect(-viewDirWS, normalWS);
			#if USE_FORWARD_PLUS || USE_CLUSTER_LIGHT_LOOP
				half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, positionWS, reflectionRoughness, occlusion, normalizedScreenSpaceUV);
			#else
				half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, reflectionRoughness, occlusion);
			#endif
		#else
			half3 indirectSpecular = gi.indirect.specular;
		#endif
		half reflectionRoughness4 = max(pow(reflectionRoughness, 4), 6.103515625e-5);
		float surfaceReductionRefl = 1.0 / (reflectionRoughness4 + 1.0);
		reflections += indirectSpecular * reflectionMask * surfaceReductionRefl * _ReflectionColor.rgb;

		#if defined(TCP2_REFLECTIONS_FRESNEL)
			half fresnelTerm = smoothstep(_FresnelMin, _FresnelMax, ndv);
			reflections *= fresnelTerm;
		#endif

		IS_LIGHTING_FEATURE_ENABLED(DEBUGLIGHTINGFEATUREFLAGS_GLOBAL_ILLUMINATION)
		emission.rgb += reflections;
	#endif

	// Premultiply blending
	#if defined(_ALPHAPREMULTIPLY_ON)
		color.rgb *= alpha;
	#endif

	// Rendering Debugger
	#if defined(DEBUG_DISPLAY)
		debugInputData.normalWS = normalWS;
		debugInputData.viewDirectionWS = viewDirWS;
		debugInputData.shadowCoord = shadowCoord;
		debugInputData.fogCoord = input.worldPos.w;
		debugInputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.pos);
		#ifdef _ADDITIONAL_LIGHTS_VERTEX
		debugInputData.vertexLighting = input.vertexLights;
		#endif
		#if defined(_NORMALMAP)
		debugInputData.tangentToWorld = tangentToWorldMatrix;
		debugSurfaceData.normalTS = normalTS;
		#endif

		debugSurfaceData.albedo = albedo.rgb;
		debugSurfaceData.occlusion = occlusion;
		debugSurfaceData.alpha = alpha;

		// Material overrides:
		half4 debugColor;
		if (CanDebugOverrideOutputColor(debugInputData, debugSurfaceData, debugColor))
		{
			return debugColor;
		}

		// Lighting overlays:
		if (_DebugLightingMode == DEBUGLIGHTINGMODE_LIGHTING_WITHOUT_NORMAL_MAPS || _DebugLightingMode == DEBUGLIGHTINGMODE_LIGHTING_WITH_NORMAL_MAPS)
		{
			emission = 0;
		}
		#if defined(TCP2_REFLECTIONS)
			else if (_DebugLightingMode == DEBUGLIGHTINGMODE_REFLECTIONS || _DebugLightingMode == DEBUGLIGHTINGMODE_REFLECTIONS_WITH_SMOOTHNESS)
			{
				color.rgb = half3(0, 0, 0);
				emission.rgb = reflections;
			}
		#endif
	#endif

	color += emission;

	// Fog (BCM: _BCM_NoFog = 1 이면 안개 전 색을 그대로 쓴다)
	half3 colorNoFog = color;
	#if defined(TCP2_HYBRID_URP)
		color = MixFog(color, input.worldPos.w);
	#else
		UNITY_EXTRACT_FOG_FROM_WORLD_POS(input);
		UNITY_APPLY_FOG(_unity_fogCoord, color);
	#endif
	color = lerp(color, colorNoFog, _BCM_NoFog);

	#if defined(TCP2_HYBRID_URP) && URP_VERSION >= 14 && defined(_WRITE_RENDERING_LAYERS)
		#if UNITY_VERSION >= 60020000
			outRenderingLayers = EncodeMeshRenderingLayer();
		#else
			outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
		#endif
	#endif

	return half4(color, alpha);
}

//================================================================================================================================
//================================================================================================================================

// OUTLINE

//================================================================================================================================
//================================================================================================================================

#if defined(TCP2_OUTLINE_LIGHTING_MAIN) || (defined(TCP2_HYBRID_URP) && defined(TCP2_OUTLINE_LIGHTING_ALL)) || defined(TCP2_OUTLINE_LIGHTING_INDIRECT)
	#define TCP2_OUTLINE_LIGHTING
#endif

struct Attributes_Outline
{
	float4 vertex : POSITION;
	float3 normal : NORMAL;
#if defined(TCP2_UV1_AS_NORMALS) || defined(TCP2_OUTLINE_TEXTURED_VERTEX) || defined(TCP2_OUTLINE_TEXTURED_FRAGMENT) || defined(BCM_OUTLINE_COLORMAP)
	float4 texcoord0 : TEXCOORD0;
#endif
#if defined(NEEDS_TEXCOORD1)
	float4 texcoord1 : TEXCOORD1;
#endif
#if defined(TCP2_UV3_AS_NORMALS)
	float4 texcoord2 : TEXCOORD2;
#endif
#if defined(TCP2_UV4_AS_NORMALS)
	float4 texcoord3 : TEXCOORD3;
#endif
#if defined(TCP2_COLORS_AS_NORMALS) || defined(BCM_OUTLINE_VCOLOR_MASK)
	float4 vertexColor : COLOR;
#endif
#if defined(TCP2_TANGENT_AS_NORMALS) || (defined(TCP2_OUTLINE_LIGHTING_ALL) && defined(_ADDITIONAL_LIGHTS_VERTEX))
	float4 tangent : TANGENT;
#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

// BCM: per-vertex outline width mask, read from a vertex colour channel.
// Convention: the channel value IS the outline strength, so a vertex painted black (0) gets no
// outline. _OutlineMaskInvert flips that for meshes painted the other way round.
#if defined(BCM_OUTLINE_VCOLOR_MASK)
half BCM_OutlineVColorMask(half4 vcolor)
{
	int ch = (int)(_OutlineMaskChannel + 0.5);
	half m = vcolor.r;
	if (ch == 1) m = vcolor.g;
	else if (ch == 2) m = vcolor.b;
	else if (ch == 3) m = vcolor.a;
	if (_OutlineMaskInvert > 0.5) m = 1.0 - m;
	return saturate(m);
}
#endif

struct Varyings_Outline
{
	float4 pos       : SV_POSITION;
	float4 vcolor    : TEXCOORD0;
	float4 worldPos  : TEXCOORD1; // xyz = worldPos  w = fogFactor
#if defined(TCP2_OUTLINE_TEXTURED_FRAGMENT)
	float4 texcoord0 : TEXCOORD2;
#endif
#if defined(TCP2_OUTLINE_LIGHTING)
	float3 normal    : TEXCOORD3;
#endif
#if defined(TCP2_HYBRID_URP)
	#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
		float4 shadowCoord    : TEXCOORD4; // compute shadow coord per-vertex for the main light
	#endif
	#if defined(TCP2_HYBRID_URP) && defined(TCP2_OUTLINE_LIGHTING_ALL) && defined(_ADDITIONAL_LIGHTS_VERTEX)
		half3 vertexLights : TEXCOORD5;
	#endif
#else
	#if !defined(SHADOW_CASTER_PASS)
		UNITY_LIGHTING_COORDS(5,6)
	#endif
#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

Varyings_Outline vertex_outline (Attributes_Outline input)
{
	BCM_ApplyWindSway(input.vertex);   // BCM: 바람 — 아웃라인이 본체를 따라가야 한다

	Varyings_Outline output = (Varyings_Outline)0;

	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

	#if defined(TCP2_HYBRID_URP)
		VertexPositionInputs vertexInput = GetVertexPositionInputs(input.vertex.xyz);
		#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
			output.shadowCoord = GetShadowCoord(vertexInput);
		#endif
		float3 positionWS = vertexInput.positionWS;
	#else
		float3 positionWS = mul(UNITY_MATRIX_M, input.vertex).xyz;
		#if !defined(SHADOW_CASTER_PASS)
			UNITY_TRANSFER_LIGHTING(output, input.texcoord1.xy);
		#endif
	#endif

	#ifdef TCP2_COLORS_AS_NORMALS
		//Vertex Color for Normals
		float3 normal = (input.vertexColor.xyz*2) - 1;
	#elif TCP2_TANGENT_AS_NORMALS
		//Tangent for Normals
		float3 normal = input.tangent.xyz;
	#elif TCP2_UV1_AS_NORMALS || TCP2_UV2_AS_NORMALS || TCP2_UV3_AS_NORMALS || TCP2_UV4_AS_NORMALS
		#if TCP2_UV1_AS_NORMALS
			#define uvChannel texcoord0
		#elif TCP2_UV2_AS_NORMALS
			#define uvChannel texcoord1
		#elif TCP2_UV3_AS_NORMALS
			#define uvChannel texcoord2
		#elif TCP2_UV4_AS_NORMALS
			#define uvChannel texcoord3
		#endif

		#if TCP2_UV_NORMALS_FULL
		//UV for Normals, full
		float3 normal = input.uvChannel.xyz;
		#else
		//UV for Normals, compressed
		#if TCP2_UV_NORMALS_ZW
			#define ch1 z
			#define ch2 w
		#else
			#define ch1 x
			#define ch2 y
		#endif
		float3 n;
		//unpack uvs
		input.uvChannel.ch1 = input.uvChannel.ch1 * 255.0/16.0;
		n.x = floor(input.uvChannel.ch1) / 15.0;
		n.y = frac(input.uvChannel.ch1) * 16.0 / 15.0;
		//- get z
		n.z = input.uvChannel.ch2;
		//- transform
		n = n*2 - 1;
		float3 normal = n;
		#endif
	#else
		float3 normal = input.normal;
	#endif

	/// #if TCP2_ZSMOOTH_ON
		/// //Correct Z artefacts
		/// normal = UnityObjectToViewPos(normal);
		/// normal.z = -_ZSmooth;
	/// #endif

	// BCM: vertex colour outline mask. Applied to the offset DIRECTION, not to _OutlineWidth:
	// the Min/Max pixel-size paths below clamp the width with max(), which would restore a minimum
	// width on masked-out vertices and leave the outline visible. Scaling the direction cannot.
	// Where all three vertices of a triangle are masked out the shell collapses onto the surface and
	// the Cull Front back faces are rejected by the depth test, so nothing is drawn.
	half outlineMask = 1.0;
	#if defined(BCM_OUTLINE_VCOLOR_MASK)
		outlineMask = BCM_OutlineVColorMask(input.vertexColor);
	#endif

	#if !defined(SHADOW_CASTER_PASS)
		output.pos = UnityObjectToClipPos(input.vertex.xyz);
		normal = mul(UNITY_MATRIX_M, float4(normal, 0)).xyz;
		float2 clipNormals = normalize(mul(UNITY_MATRIX_VP, float4(normal,0)).xy) * outlineMask;
		#if defined(TCP2_OUTLINE_CONST_SIZE)
			float2 outlineWidth = (_OutlineWidth * output.pos.w) / (_ScreenParams.xy / 2.0);
			output.pos.xy += clipNormals.xy * outlineWidth;
		#elif defined(TCP2_OUTLINE_MIN_SIZE)
			float screenRatio = _ScreenParams.x / _ScreenParams.y;
			float2 outlineWidth = max(
				(_OutlineMinWidth * output.pos.w) / (_ScreenParams.xy / 2.0),
				(_OutlineWidth / 100) * float2(1.0, screenRatio)
			);
			output.pos.xy += clipNormals.xy * outlineWidth;
		#elif defined(TCP2_OUTLINE_MIN_MAX_SIZE)
			float screenRatio = _ScreenParams.x / _ScreenParams.y;
			float2 outlineWidth = max(
				(_OutlineMinWidth * output.pos.w) / (_ScreenParams.xy / 2.0),
		        (_OutlineWidth / 100) * float2(1.0, screenRatio)
		    );
			outlineWidth = min(
				(_OutlineMaxWidth * output.pos.w) / (_ScreenParams.xy / 2.0),
		        outlineWidth
		    );
			output.pos.xy += clipNormals.xy * outlineWidth;
		#else
			float screenRatio = _ScreenParams.x / _ScreenParams.y;
			output.pos.xy += clipNormals.xy * (_OutlineWidth / 100) * float2(1.0, screenRatio);
		#endif
	#else
		input.vertex = input.vertex + float4(normal,0) * _OutlineWidth * 0.01 * outlineMask;
	#endif

	output.vcolor.rgba = _OutlineColor.rgba;
	float4 clipPos = output.pos;

	#if defined(TCP2_OUTLINE_LIGHTING_ALL) && defined(_ADDITIONAL_LIGHTS_VERTEX)
		// Vertex lighting
		VertexNormalInputs vertexNormalInput = GetVertexNormalInputs(input.normal, input.tangent);
		output.vertexLights = VertexLighting(positionWS, vertexNormalInput.normalWS);
	#endif

	// World Position
	output.worldPos.xyz = positionWS;

	// Compute fog factor
	#if defined(TCP2_HYBRID_URP)
		output.worldPos.w = ComputeFogFactor(output.pos.z);
	#else
		UNITY_TRANSFER_FOG_COMBINED_WITH_WORLD_POS(output, output.pos);
	#endif

	// Lighting & Texture
	#if defined(TCP2_OUTLINE_LIGHTING)
		output.normal = normalize(UnityObjectToWorldNormal(input.normal));
	#endif

	#if defined(TCP2_OUTLINE_TEXTURED_VERTEX)
		half4 outlineTexture = tex2Dlod(_BaseMap, float4(input.texcoord0.xy * _BaseMap_ST.xy + _BaseMap_ST.zw, 0, _OutlineTextureLOD));
		output.vcolor *= outlineTexture;
	#endif

	// BCM: Outline Color Map. Tints the outline color using a dedicated map sampled by the mesh UV.
	#if defined(BCM_OUTLINE_COLORMAP)
		half4 outlineColorMap = tex2Dlod(_OutlineColorMap, float4(input.texcoord0.xy * _OutlineColorMap_ST.xy + _OutlineColorMap_ST.zw, 0, 0));
		output.vcolor.rgb *= outlineColorMap.rgb;
	#endif

	#if defined(TCP2_OUTLINE_TEXTURED_FRAGMENT)
		output.texcoord0.xy = input.texcoord0.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
		output.texcoord0.zw = input.texcoord0.zw;
	#endif

	// BCM: outline on/off gate. When the BCM_OUTLINE keyword is not enabled, collapse the
	// outline geometry to a degenerate (zero-area) primitive so the pass renders nothing.
	// This lets a single unified shader toggle the outline in both Built-in RP and URP.
	#if !defined(BCM_OUTLINE)
		output.pos = float4(0.0, 0.0, 0.0, 1.0);
	#endif

	return output;
}

float4 fragment_outline (Varyings_Outline input) : SV_Target
{
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
	BCM_SEE_THROUGH_CLIP(input.worldPos.xyz, input.pos.xy);   // 아웃라인도 같이 비워야 검은 테두리만 떠 있지 않다

	#if defined(LOD_FADE_CROSSFADE)
		const float dither = Dither4x4(input.pos.xy);
		const float ditherThreshold = unity_LODFade.x - CopySign(dither, unity_LODFade.x);
		clip(ditherThreshold);
	#endif

	#if defined(TCP2_OUTLINE_TEXTURED_FRAGMENT)
		// Manual mip map calculation so that we can apply a max value
		float2 dx = ddx(input.texcoord0.xy);
		float2 dy = ddy(input.texcoord0.xy);
		float delta = max(dot(dx, dx), dot(dy, dy));
		float mipMapLevel = max(0.5 * log2(delta), _OutlineTextureLOD);

		half4 albedo = tex2Dlod(_BaseMap, float4(input.texcoord0.xy, 0, mipMapLevel));
	#else
		half4 albedo = half4(1, 1, 1, 1);
	#endif

	// Output Color
	half4 outlineColor = half4(1, 1, 1, 1);

	#if defined(TCP2_OUTLINE_LIGHTING)

			#if URP_VERSION >= 14
				uint meshRenderingLayers = GetMeshRenderingLayer();
			#elif URP_VERSION >= 12
				uint meshRenderingLayers = GetMeshRenderingLightLayer();
			#endif

			float3 positionWS = input.worldPos.xyz;
			float3 normalWS = input.normal;
			#if defined(TCP2_HYBRID_URP)
				half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
			#else
				half3 viewDirWS = TCP2_SafeNormalize(UnityWorldSpaceViewDir(positionWS));
			#endif

		#if defined(TCP2_OUTLINE_LIGHTING_INDIRECT)
			// Indirect only
			outlineColor = half4(0, 0, 0, 1);
		#else
			// Main directional light
			#if defined(TCP2_HYBRID_URP)
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					float4 shadowCoord = input.shadowCoord;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				half4 shadowMask = half4(1, 1, 1, 1); // no baked lighting for outlines

				#if URP_VERSION <= 7
					Light mainLight = GetMainLight(shadowCoord);
				#else
					Light mainLight = GetMainLight(shadowCoord, positionWS, shadowMask);
				#endif

				#if defined(_SCREEN_SPACE_OCCLUSION)
					float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.pos);
					AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(normalizedScreenSpaceUV);
					mainLight.color *= aoFactor.directAmbientOcclusion;
				#endif

				#if URP_VERSION >= 12
					half3 lightDir = half3(0, 1, 0);
					half3 lightColor = half3(0, 0, 0);
					#if (URP_VERSION >= 14 && defined(_LIGHT_LAYERS)) || URP_VERSION <= 12
						if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
					#endif
					{
						lightDir = mainLight.direction;
						lightColor = mainLight.color.rgb;
					}
				#else
					half3 lightDir = mainLight.direction;
					half3 lightColor = mainLight.color.rgb;
				#endif
				half shadowTerm = BCM_ShapeReceivedShadow(mainLight.shadowAttenuation);
				half atten = shadowTerm * mainLight.distanceAttenuation;
				half3 attenRGB = BCM_TintReceivedShadow(shadowTerm) * mainLight.distanceAttenuation;
			#else
				half3 lightDir = normalize(UnityWorldSpaceLightDir(positionWS));
				half3 lightColor = _LightColor0.rgb;

				#if defined(SHADOW_CASTER_PASS)
					half atten = 1.0;
					half3 attenRGB = half3(1.0, 1.0, 1.0);
				#else
					TCP2_LIGHT_ATTENUATION(input, positionWS)
					#if defined(_RECEIVE_SHADOWS_OFF)
						half atten = attenuation;
						half3 attenRGB = attenuation.xxx;
					#else
						// Built-in outline deliberately keeps the raw shadow term (no strength/smoothing
						// shaping) as it always has; only the tint is added here.
						half atten = shadow * attenuation;
						half3 attenRGB = BCM_TintReceivedShadow(shadow) * attenuation;
					#endif
				#endif
			#endif

			half ndl = dot(normalWS, lightDir);
			half ndlWrapped = ndl * 0.5 + 0.5;

			// Calculate ramp (outline uses untinted ramp colors)
			half3 ramp = CalculateRamp(ndlWrapped, _SColor.rgb, _MidColor0.rgb, _HColor.rgb);

			// Apply attenuation
			ramp *= attenRGB;

			#if defined(TCP2_OUTLINE_TEXTURED_FRAGMENT) && defined(TCP2_SHADOW_TEXTURE)
				half4 shadowAlbedo = tex2Dlod(_BaseMap, float4(input.texcoord0.xy, 0, mipMapLevel));
				albedo = lerp(shadowAlbedo, albedo, ramp.x);
			#endif

			// Highlight/shadow colors
			ramp = lerp(_SColor.rgb, _HColor.rgb, ramp);

			// Apply ramp
			outlineColor.rgb *= lerp(half3(1,1,1), ramp, _DirectIntensityOutline) * lightColor;
		#endif

	#endif

	// Apply albedo
	outlineColor.rgb *= albedo.rgb;

	#if defined(TCP2_OUTLINE_LIGHTING)
		half occlusion = 1.0;

		// Additional lights loop
		#if defined(TCP2_HYBRID_URP) && defined(TCP2_OUTLINE_LIGHTING_ALL) && defined(_ADDITIONAL_LIGHTS)
			uint pixelLightCount = GetAdditionalLightsCount();

			#if URP_VERSION >= 12
				#if USE_FORWARD_PLUS || USE_CLUSTER_LIGHT_LOOP
					// Additional directional lights in Forward+
					for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
					{
						#if URP_VERSION >= 171
							CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
						#else
							FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
						#endif

						Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);

						#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
						#endif
						{
							ProcessAdditionalLight(light, normalWS.xyz, albedo.rgb, outlineColor.rgb, _DirectIntensityOutline
								#if defined(_SCREEN_SPACE_OCCLUSION)
									, aoFactor
								#endif
							);
						}
					}

					// Data with dummy struct used in Forward+ macro (LIGHT_LOOP_BEGIN)
					#if !defined(_SCREEN_SPACE_OCCLUSION)
						float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.pos);
					#endif
					InputDataForwardPlusDummy inputData;
					inputData.normalizedScreenSpaceUV = normalizedScreenSpaceUV;
					inputData.positionWS = positionWS;
				#endif

				LIGHT_LOOP_BEGIN(pixelLightCount)
			#else
				for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
				{
			#endif

			#if URP_VERSION <= 7
					Light light = GetAdditionalLight(lightIndex, positionWS);
			#else
					Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
			#endif

			#if (URP_VERSION >= 12 && URP_VERSION < 14) || (URP_VERSION >= 14 && defined(_LIGHT_LAYERS))
				if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
			#endif
				{
					ProcessAdditionalLight(light, normalWS.xyz, albedo.rgb, outlineColor.rgb, _DirectIntensityOutline
						#if defined(_SCREEN_SPACE_OCCLUSION)
							, aoFactor
						#endif
					);
				}
			#if URP_VERSION >= 12
				LIGHT_LOOP_END
			#else
				}
			#endif
		#endif

		#if defined(TCP2_OUTLINE_LIGHTING_ALL) && defined(_ADDITIONAL_LIGHTS_VERTEX)
			outlineColor.rgb += input.vertexLights * albedo.rgb;
		#endif

		// Apply ambient/indirect lighting
		#if defined(TCP2_HYBRID_URP)

			#if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
				// Adaptive Probe Volumes APV
				half3 bakedGI = SampleProbeVolumePixel(0.0, positionWS, normalWS, viewDirWS, input.pos.xy);
			#else
				// Sample SH fully per-pixel
				half3 bakedGI = SampleSH(_SingleIndirectColor > 0 ? viewDirWS : normalWS);
			#endif
			half3 indirectDiffuse = bakedGI * occlusion * albedo.rgb * _IndirectIntensityOutline;
			outlineColor.rgb += indirectDiffuse;
		#else
			half3 shNormal = (_SingleIndirectColor > 0) ? viewDirWS : normalWS;
			half3 bakedGI = ShadeSHPerPixel(shNormal, half3(0,0,0), positionWS);
			half3 indirectDiffuse = bakedGI * occlusion * albedo.rgb * _IndirectIntensityOutline;
			outlineColor.rgb += indirectDiffuse;
		#endif
	#endif

	outlineColor.rgba *= input.vcolor.rgba;

	// Fog (BCM: _BCM_NoFog = 1 이면 무시)
	half3 outlineNoFog = outlineColor.rgb;
	#if defined(TCP2_HYBRID_URP)
		outlineColor.rgb = MixFog(outlineColor.rgb, input.worldPos.w);
	#else
		UNITY_EXTRACT_FOG_FROM_WORLD_POS(input);
		UNITY_APPLY_FOG(_unity_fogCoord, outlineColor.rgb);
	#endif
	outlineColor.rgb = lerp(outlineColor.rgb, outlineNoFog, _BCM_NoFog);

	return outlineColor;
}