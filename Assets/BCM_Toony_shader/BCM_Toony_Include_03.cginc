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

	// BCM_02: planar ("blob") shadow. Declared here rather than in the pass so that EVERY pass of the
	// shader ends up with an identical UnityPerMaterial layout - that is what keeps the shader SRP
	// Batcher compatible. See the PlanarShadow section at the bottom of this file.
	half4 _PlanarShadowColor;
	half  _PlanarShadowStrength;
	half  _PlanarShadowGroundMode;
	float _PlanarShadowGroundHeight;
	half  _PlanarShadowBias;
	half  _PlanarShadowFadeMode;
	float _PlanarShadowFadeDistance;
	half  _PlanarShadowFalloff;
	float _PlanarShadowHeightFade;
	// Analytic blob shape (BCM_PLANAR_SHADOW_BLOB). Sizes are in OBJECT units so they follow the
	// object's scale and rotation the way the Lego stud grid does.
	float4 _PlanarShadowBlobSize;
	float4 _PlanarShadowBlobCenter;
	float  _PlanarShadowBlobHeight;
	half   _PlanarShadowRoundness;
	half   _PlanarShadowSoftness;
	half   _PlanarShadowGrow;
	float  _PlanarShadowMaxOffset;

	// BCM_02: LEGO stud contact shadow (BCM_STUD_SHADOW). Grid values are OBJECT space; the shadow
	// tuning values are in CELL units (i.e. multiples of _StudPitch), same convention as the original.
	float4 _StudCount;
	float4 _StudGridCenter;
	float  _StudPitch;
	float  _StudRadius;
	half   _StudEdgeSoftness;
	half   _StudMaskShrink;
	float  _StudFaceLevel;
	float  _StudShadowHeight;
	half   _StudShadowHeightScale;
	half   _StudShadowStrength;
	half4  _StudShadowColor;
	half   _StudShadowColorAmount;
	half   _StudShadowSoftness;
	half   _StudShadowGrow;
	half   _StudShadowMaxOffset;
	half   _StudRootAO;
	half   _StudRootAOWidth;

	// BCM: Multiply section (masked color multiply over the albedo)
	half4 _MultiplyColor;
	half _MultiplyMaskChannel;
	half _MultiplyStrength;

	// BCM_03: X-Ray Overlay depth push, in WORLD units - a distance, so float rather than half.
	// The rest of the feature (_ZTest, _StencilRef and friends) is render state only: it is consumed by
	// the Pass blocks in the .shader and never read from HLSL, which is why it is not declared here.
	// Same reason _Cull and _ZWrite are absent.
	float _XRayDepthPush;
CBUFFER_END

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

// Meta Pass
#if defined(UNITY_PASS_META) && !defined(TCP2_HYBRID_URP)
	#include "UnityMetaPass.cginc"
#endif

// BCM_03: X-Ray Overlay - optional vertex depth push.
//
// Clip position for a world position pulled _XRayDepthPush toward the viewer. UNITY_MATRIX_V row 2 is
// the view-space Z axis written in world space, and Unity's view space looks down -Z, so that row IS
// the "toward the camera" direction. Reading it off the view matrix rather than using
// (cameraPos - positionWS) is what makes this work under an ORTHOGRAPHIC camera, where every pixel
// shares one view direction and the vector to the camera position is meaningless.
//
// Only the rasterised position moves. Callers keep their original positionWS, so lighting, shadow
// sampling and fog are still evaluated where the mesh actually is - a push large enough to clear a
// fringe would otherwise visibly shift the shading on the eyebrow.
float4 BCM_XRayClipPos(float3 positionWS)
{
	positionWS += UNITY_MATRIX_V[2].xyz * _XRayDepthPush;
#if defined(TCP2_HYBRID_URP)
	return TransformWorldToHClip(positionWS);
#else
	return UnityWorldToClipPos(positionWS);
#endif
}

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

#if defined(BCM_STUD_SHADOW)
// BCM_02: LEGO-style stud contact shadow, ported from Lego/StuddedBlock ("Directional Contact Shadow"
// + "Root Occlusion, rotation invariant"). The studs are a regular grid of cylinders whose positions
// are known up front, so the shadow can be solved analytically per pixel on the receiving top face -
// no shadow map involved, which matters because no practical shadow map resolution resolves a stud.
//
// Returns .x = ambient occlusion factor, .y = direct-light factor. Both 1 = clear.
//
// The two are kept apart, and NEITHER goes through the toon ramp step. Folding this into ndlWrapped
// looks tempting - it is how BCM_OCCLUSION_DIRECT unifies occlusion with the lit shadow - but the step
// quantises it: root AO of 0.38 is already enough to tip a whole ring past the ramp threshold, so the
// crescents swell into one flat blob covering the entire top face. The original multiplies its shadow
// straight into the direct term for exactly this reason, and that is what preserves the soft edge.
//
// One deliberate departure from the original: the grid frame here is the object's local X/Z axes
// PROJECTED onto the world horizontal plane, and the light's UP term is taken in world space. Pulling
// the light into raw object space - what the original does - assumes the mesh's own Y axis points up.
// That is false for anything imported flipped, and the blocks this was written for sit at rotation
// (0,180,180), i.e. their local Y points straight DOWN.
half2 BCM_StudContactShadow(float3 positionOS, half3 normalWS)
{
	// Only the upward-facing surface carries stud shadows. The ramp is deliberately wide: the original's
	// (n.y - 0.7) * 6.0 finishes inside 0.17 of normal.y, which cuts the shadow off abruptly where a
	// rounded block rim starts turning over and leaves a visible outline following the bevel. Spreading
	// it lets the shadow fade down the bevel instead of ending on a line.
	half topMask = saturate((normalWS.y - 0.35) * 2.0);
	if (topMask <= 0.0) return half2(1.0, 1.0);

	// ...and only the RECEIVING face, not the studs standing on it. A stud cap and its rim face up just
	// as much as the face does, so a normal test alone paints the ring shading onto the studs' own
	// borders. The original sidesteps this with a per-vertex stud flag baked into the mesh; nothing here
	// has one, and body+studs frequently share a single mesh so a per-material toggle cannot do it
	// either. Height along the object's UP axis separates them cleanly: the face sits at
	// _StudFaceLevel, the caps a whole stud height above it.
	//
	// The up axis is derived from the world, not assumed to be +Y in object space - these meshes are
	// imported at rotation (0,180,180), so their local Y points down.
	float3 upOS = mul((float3x3)unity_WorldToObject, float3(0.0, 1.0, 0.0));
	float  upLen = length(upOS);
	float  heightUp = (upLen > 1e-5) ? dot(positionOS, upOS / upLen) : positionOS.y;

	float faceH = max(_StudShadowHeight, 1e-4);
	topMask *= 1.0 - smoothstep(_StudFaceLevel + faceH * 0.15,
	                            _StudFaceLevel + faceH * 0.60, heightUp);
	if (topMask <= 0.0) return half2(1.0, 1.0);

	// Direction TOWARDS the main light (the URP branch maps this to _MainLightPosition).
	float3 L = normalize(_WorldSpaceLightPos0.xyz);

	// Object horizontal frame, so a yawed or flipped mesh still resolves correctly.
	float3 axXw = mul((float3x3)unity_ObjectToWorld, float3(1.0, 0.0, 0.0));
	float3 axZw = mul((float3x3)unity_ObjectToWorld, float3(0.0, 0.0, 1.0));
	float2 aX = float2(axXw.x, axXw.z); float sX = length(aX);
	float2 aZ = float2(axZw.x, axZw.z); float sZ = length(aZ);
	aX = (sX > 1e-4) ? aX / sX : float2(1.0, 0.0);
	aZ = (sZ > 1e-4) ? aZ / sZ : float2(0.0, 1.0);

	// g is a continuous cell coordinate; an integer value sits exactly on a stud centre. Clamping the
	// rounded index to the real stud range keeps the field bounded, so cells past the edge of the grid
	// cannot sprout phantom studs or phantom shadows off the side of the block.
	float  pitch = max(_StudPitch, 1e-4);
	float2 count = max(_StudCount.xy, 1.0);
	float2 g     = (positionOS.xz - _StudGridCenter.xy) / pitch + (count - 1.0) * 0.5;
	float2 loIdx = 0.0;
	float2 hiIdx = count - 1.0;

	float rN = _StudRadius / pitch;
	float hN = _StudShadowHeight * _StudShadowHeightScale / pitch;

	// Cap thrown along the light. The UP term is world space, the horizontal term is resolved onto the
	// object's own axes, so the offset stays correct under any yaw or flip. max() on the divisor both
	// guards the divide and flattens the offset for grazing light.
	float  lUp = L.y;
	float2 lXZ = float2(dot(L.xz, aX), dot(L.xz, aZ));
	float2 off = -(lXZ / max(lUp, 0.25)) * hN;
	float  offLen = length(off);
	off *= min(offLen, _StudShadowMaxOffset) / max(offLen, 1e-5);

	// A light at the horizon throws something that no longer reads as the stud's shadow; fade it out.
	half upLit = saturate(lUp * 3.0);

	// Distances to the nearest stud, taken as a MIN over the 3x3 cell neighbourhood rather than from
	// the single rounded-nearest cell.
	//
	// Sampling only round(g) is what the original does and it lays visible straight seams along the
	// cell mid-lines: the moment the chosen index flips, p = g - idx jumps by a whole cell, and because
	// the swept capsule is asymmetric about that boundary dS jumps with it - a genuine value
	// discontinuity, not just a gradient kink. min() over neighbours is continuous everywhere, and at a
	// mid-line both candidates evaluate equal so nothing shows. 3x3 rather than 2x2 because the sweep
	// reaches up to _StudShadowMaxOffset (0.9 cells) plus the shadow radius.
	float  dist0 = 1e9;   // to the stud centre  -> stud footprint + root ring
	float  dS    = 1e9;   // to the swept capsule -> the cast shadow
	float2 gRound = round(g);
	[unroll] for (int oy = -1; oy <= 1; oy++)
	{
		[unroll] for (int ox = -1; ox <= 1; ox++)
		{
			float2 idx = clamp(gRound + float2(ox, oy), loIdx, hiIdx);
			float2 p   = g - idx;
			dist0 = min(dist0, length(p));
			float hSeg = saturate(dot(p, off) / max(dot(off, off), 1e-6));
			dS = min(dS, length(p - off * hSeg));
		}
	}

	// The stud's own footprint. Cutting it out of the shadow is what turns a disc into the crescent
	// that reads as a stud standing on the surface.
	//
	// The cut-out radius is deliberately SMALLER than the stud. Inside it neither the shadow nor the
	// root ring is drawn - which is right, the stud covers that spot - but the disc has to stay strictly
	// under the stud rim. At rN with a soft edge it reaches rN + _StudEdgeSoftness, i.e. slightly PAST
	// the geometry, and the exposed sliver has no shading at all: it reads as a bright outline hugging
	// each stud. _StudMaskShrink pulls it back in. Erring inwards is free, since anything the stud
	// covers is invisible anyway.
	float rMask = max(rN - _StudMaskShrink, 0.01);
	half studMask = 1.0 - smoothstep(rMask - _StudEdgeSoftness, rMask + _StudEdgeSoftness, dist0);

	// dS is the distance to the swept footprint between the stud base and the offset cap, i.e. a
	// capsule - a plain offset disc would leave a gap at the stud root. Computed in the loop above.
	half shadow = 1.0 - smoothstep(rN + _StudShadowGrow - _StudShadowSoftness,
	                               rN + _StudShadowGrow + _StudShadowSoftness, dS);
	shadow *= topMask * upLit * (1.0 - studMask) * _StudShadowStrength;
	// Fade it out where the face turns away from the light - it has no business darkening a surface
	// that is already unlit.
	shadow *= saturate(dot(normalWS, L) * 2.0 + 0.35);

	// Root occlusion: the tight ring where the stud meets the face. Depends only on dist0, never on
	// the light, so rotating the block cannot drag it around.
	half root = (1.0 - smoothstep(rN, rN + _StudRootAOWidth, dist0)) * topMask * (1.0 - studMask);
	half ao   = 1.0 - root * _StudRootAO;

	// Ambient gets the root ring only; direct gets the ring AND the cast shadow.
	return half2(ao, saturate((1.0 - shadow) * ao));
}
#endif

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
#if defined(_NORMALMAP) || (defined(TCP2_MOBILE) && (defined(TCP2_RIM_LIGHTING) || (defined(TCP2_REFLECTIONS) && defined(TCP2_REFLECTIONS_FRESNEL)))) // if normalmap or (mobile + rim or fresnel)
	float4 tangentWS       : TEXCOORD2; /* w = ndv (mobile) */
#endif
#if defined(_NORMALMAP)
	float4 bitangentWS     : TEXCOORD3;
#endif
#if defined(TCP2_MATCAP) && !defined(_NORMALMAP)
	float4 matcap          : TEXCOORD4;
#endif
#if defined(BCM_STUD_SHADOW)
	// The stud grid is authored in object space, so the receiving pixel has to arrive there too.
	// TEXCOORD8 because 0-7 are already spoken for by the branches below.
	float3 positionOS      : TEXCOORD8;
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
		#if defined(BCM_XRAY)
			// BCM_03: X-Ray Overlay. Guarded rather than folded into the line above so the common case
			// keeps using the clip position GetVertexPositionInputs has already computed.
			positionCS = BCM_XRayClipPos(positionWS);
		#endif
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
		#if defined(BCM_XRAY)
			float4 positionCS = BCM_XRayClipPos(positionWS); // BCM_03: X-Ray Overlay
		#else
			float4 positionCS = UnityWorldToClipPos(positionWS);
		#endif
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
	#if defined(BCM_STUD_SHADOW)
		output.positionOS = input.vertex.xyz;
	#endif

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

	half4 albedo = tex2D(_BaseMap, mainTexcoord).rgba;
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

	// BCM_02: stud contact shadow. The ambient half lands on occlusion here; the direct half is
	// multiplied into the ramp further down, deliberately AFTER the toon step (see the function).
	#if defined(BCM_STUD_SHADOW)
		half2 studTerm = BCM_StudContactShadow(input.positionOS, normalWS);
		occlusion *= studTerm.x;
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

	// BCM_02: stud contact shadow, direct half. Applied HERE - after CalculateRamp but before 'ramp'
	// is remapped between the shadow and highlight colours - so the crescent keeps its own soft edge
	// and still resolves towards _SColor rather than towards black.
	//
	// Colour is NOT applied here - see the _StudShadowColor block after the ramp is resolved. A
	// per-channel multiplier (the BCM_TintReceivedShadow trick) was tried first and is too weak: the
	// output stays bounded by _SColor..._HColor per channel, so on an orange block a blue tint only
	// moved the blue channel from 0.014 to 0.073. Measured, not guessed.
	#if defined(BCM_STUD_SHADOW)
		ramp *= studTerm.y;
	#endif

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

	// BCM_02: stud shadow colour, applied on the FINAL colour and before fog.
	//
	// It has to be this late to be an absolute colour. Two earlier spots were tried and measured:
	// a per-channel multiplier on 'ramp' stays bounded by _SColor.._HColor (a blue tint only lifted the
	// blue channel from 0.014 to 0.073 on an orange block), and lerping 'ramp' towards the colour is
	// then multiplied by albedo, so blue 0.95 against an orange albedo of 0.10 collapses back to 0.095.
	// Here nothing downstream can tint it. Specular and rim are overridden inside the crescent too,
	// which is what a stylised flat shadow wants.
	//
	// _StudShadowColorAmount is a SEPARATE property on purpose. It used to be the colour's alpha, which
	// is undiscoverable - a colour gets picked, the alpha stays 0, and the control looks broken.
	// 0 keeps the plain toon shadow tone, so materials made before this existed are untouched.
	#if defined(BCM_STUD_SHADOW)
		color.rgb = lerp(color.rgb, _StudShadowColor.rgb, (1.0 - studTerm.y) * _StudShadowColorAmount);
	#endif

	// Fog
	#if defined(TCP2_HYBRID_URP)
		color = MixFog(color, input.worldPos.w);
	#else
		UNITY_EXTRACT_FOG_FROM_WORLD_POS(input);
		UNITY_APPLY_FOG(_unity_fogCoord, color);
	#endif

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
	#if defined(BCM_XRAY)
		// BCM_03: X-Ray Overlay. The shell has to be pushed by the same amount as the body underneath
		// it, or the outline is depth-rejected by the hair the body just drew over.
		output.pos = BCM_XRayClipPos(positionWS);
	#else
		output.pos = UnityObjectToClipPos(input.vertex.xyz);
	#endif
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
	#if defined(BCM_STUD_SHADOW)
		output.positionOS = input.vertex.xyz;
	#endif
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

	// Fog
	#if defined(TCP2_HYBRID_URP)
		outlineColor.rgb = MixFog(outlineColor.rgb, input.worldPos.w);
	#else
		UNITY_EXTRACT_FOG_FROM_WORLD_POS(input);
		UNITY_APPLY_FOG(_unity_fogCoord, outlineColor.rgb);
	#endif

	return outlineColor;
}

//==================================================================================================
//
// BCM_02: PLANAR ("BLOB") SHADOW
//
// Flattens a copy of the mesh onto a horizontal plane along the main directional light and blends it
// as a soft dark shape. It is a stylised fake, completely independent of the real-time shadow map, so
// both can be on at once.
//
// The point of it is the gradient: the shadow is at full strength where the object touches the ground
// and fades outwards, which the real shadow map cannot do (see BCM_ShapeReceivedShadow - a shadow map
// gives an occlusion term, not a blocker distance).
//
// Deliberately written against only the macros that BOTH pipelines guarantee (unity_ObjectToWorld,
// UNITY_MATRIX_VP, _WorldSpaceLightPos0 - which the top of this file maps to _MainLightPosition under
// URP), so the same code serves the URP and the Built-in pass with no branching.
//
//==================================================================================================

#if defined(BCM_PLANAR_SHADOW_PASS)

#if defined(BCM_PLANAR_SHADOW_RAMP)
	sampler2D _PlanarShadowRamp;
#endif

struct Attributes_PlanarShadow
{
	float4 vertex : POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings_PlanarShadow
{
	float4 positionCS : SV_POSITION;
	// xy = object-space XZ of this ground pixel, pivot at the origin (Blob shape only)
	// z  = fade coordinate, 0 at the contact point -> 1 at the fade distance (Mesh shape only)
	// w  = height fade multiplier (pivot rising above the ground plane)
	float4 data : TEXCOORD0;
	// xy = blob half extents in object units, zw = swept cap offset in object XZ (Blob shape only)
	float4 blob : TEXCOORD1;
};

Varyings_PlanarShadow vertex_planarshadow (Attributes_PlanarShadow input)
{
	Varyings_PlanarShadow output = (Varyings_PlanarShadow)0;
	UNITY_SETUP_INSTANCE_ID(input);

#if !defined(BCM_PLANAR_SHADOW)
	// Feature off. A shader keyword cannot remove a pass, so collapse the triangle to a point well
	// outside the clip volume instead - same trick the outline pass uses when BCM_OUTLINE is off.
	output.positionCS = float4(-2, -2, -2, 1);
	output.data = float4(0, 0, 1, 0);
	return output;
#else

	float3 positionWS = mul(unity_ObjectToWorld, float4(input.vertex.xyz, 1.0)).xyz;
	float3 originWS   = mul(unity_ObjectToWorld, float4(0.0, 0.0, 0.0, 1.0)).xyz;

	// Direction TOWARDS the main directional light.
	float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);

	// A light at the horizon sends the projection to infinity, so clamp how shallow it may get.
	// 0.15 is roughly 8.6 degrees above the horizon.
	float lightY = lightDir.y;
	lightY = (lightY >= 0.0) ? max(lightY, 0.15) : min(lightY, -0.15);

	// Ground Mode: 0 = absolute world Y, 1 = the object pivot with the same field as an offset.
	// Pivot is the default so the plane follows the caster with no per-scene setup.
	float groundY = (_PlanarShadowGroundMode < 0.5)
		? _PlanarShadowGroundHeight
		: originWS.y + _PlanarShadowGroundHeight;

	// The caster sits entirely under the plane, so there is nothing to project onto. Bail out instead
	// of falling through to the clamp below: that would flatten the mesh in place and lift it up to
	// the plane, which reads as a slab floating above the object.
	if (originWS.y < groundY - 0.001)
	{
		output.positionCS = float4(-2, -2, -2, 1);
		output.data = float4(0, 0, 1, 0);
		return output;
	}

	half heightFade = 1.0;
	if (_PlanarShadowHeightFade > 1e-4)
		heightFade = saturate(1.0 - max(originWS.y - groundY, 0.0) / _PlanarShadowHeightFade);

#if defined(BCM_PLANAR_SHADOW_BLOB)

	// ---- Analytic blob shape -------------------------------------------------------------------
	// Same idea as Lego/StuddedBlock's directional contact shadow: instead of projecting the mesh and
	// inheriting its razor-sharp silhouette, the shape is a signed distance field evaluated per pixel,
	// so the edge softness is a free parameter instead of being fixed by the geometry.
	//
	// Evaluated in OBJECT space, again like the Lego stud grid: the shape stays glued to the object
	// when it yaws while the light offset counter-rotates, which leaves the shadow world-anchored.
	// The field's frame is the object's local X/Z axes PROJECTED onto the world horizontal plane, and
	// every quantity below is in WORLD units. Running the whole thing in raw object space - which is
	// what Lego/StuddedBlock can afford, because its blocks are never imported flipped - breaks as
	// soon as a mesh arrives rotated: the blocks in this scene sit at (0,180,180), so their local Y
	// points straight DOWN. The light's object-space Y then goes negative, the divide guard clamps it
	// to a bogus positive, and the sweep comes out in a meaningless direction.
	float3 axXw = mul((float3x3)unity_ObjectToWorld, float3(1.0, 0.0, 0.0));
	float3 axZw = mul((float3x3)unity_ObjectToWorld, float3(0.0, 0.0, 1.0));
	float2 aX = float2(axXw.x, axXw.z);
	float2 aZ = float2(axZw.x, axZw.z);
	float  sX = length(aX);
	float  sZ = length(aZ);
	// An axis standing vertically has no footprint direction to contribute; fall back to world axes.
	aX = (sX > 1e-4) ? aX / sX : float2(1.0, 0.0);
	aZ = (sZ > 1e-4) ? aZ / sZ : float2(0.0, 1.0);

	// Half extents and centre carried into world units along those axes.
	float2 e = float2(_PlanarShadowBlobSize.x * max(sX, 1e-4), _PlanarShadowBlobSize.y * max(sZ, 1e-4));
	float2 centreWS = mul(unity_ObjectToWorld,
		float4(_PlanarShadowBlobCenter.x, 0.0, _PlanarShadowBlobCenter.y, 1.0)).xz;

	// Push the cap along the light, derived in world space where up is unambiguous, then expressed in
	// the field frame. max() on the divisor guards the divide and stops a grazing light from throwing
	// the blob to infinity.
	float  sweepW = _PlanarShadowBlobHeight * length(mul((float3x3)unity_ObjectToWorld, float3(0.0, 1.0, 0.0)));
	float2 offWS  = -(lightDir.xz / max(lightDir.y, 0.25)) * sweepW;
	float  offLen = length(offWS);
	offWS *= min(offLen, _PlanarShadowMaxOffset) / max(offLen, 1e-5);
	offLen = min(offLen, _PlanarShadowMaxOffset);
	float2 off = float2(dot(offWS, aX), dot(offWS, aZ));

	// The mesh is only a canvas for the SDF here, so flatten it straight down rather than projecting
	// along the light, then inflate it. The soft edge has to reach zero BEFORE the geometry border,
	// otherwise the mesh silhouette clips it and puts the hard edge straight back. Everything is
	// relative to the field's centre, not the pivot, so an off-centre mesh keeps its blob under itself.
	float2 relWS = positionWS.xz - centreWS;
	float  lenWS = length(relWS);
	float2 dirWS = (lenWS > 1e-4) ? relWS / lenWS : float2(1.0, 0.0);

	// Furthest the field can possibly reach from its centre. The 1.5 slack covers the gap between a
	// polygon fan and the circle it inscribes - a cube only has four directions to expand along.
	float need = (length(e) + offLen + _PlanarShadowGrow + _PlanarShadowSoftness * 2.0 + 0.02) * 1.5;
	float2 canvasXZ = centreWS + dirWS * max(lenWS + 0.05, need);

	float3 canvasWS = float3(canvasXZ.x, groundY + _PlanarShadowBias, canvasXZ.y);
	float2 rel = canvasXZ - centreWS;

	output.positionCS = mul(UNITY_MATRIX_VP, float4(canvasWS, 1.0));
	output.data = float4(dot(rel, aX), dot(rel, aZ), 0.0, heightFade);
	output.blob = float4(e, off);
	return output;

#else

	// ---- Projected mesh shape ------------------------------------------------------------------
	// Distance travelled along the light ray to reach the plane. Clamped at 0 so the parts of a mesh
	// that dip below the plane are flattened onto it rather than projected back up.
	float travel = max((positionWS.y - groundY) / lightY, 0.0);

	float3 projWS = positionWS - lightDir * travel;
	projWS.y = groundY + _PlanarShadowBias;

	// Fade coordinate.
	//
	// IMPORTANT: both modes are derived from the PROJECTED GROUND POSITION only, never from per-vertex
	// data. Front and back faces of the mesh land on the same spot of the plane and the stencil keeps
	// whichever rasterises first, so any quantity that differs between those two surfaces (per-vertex
	// travel being the obvious one) tears the blob into hard-edged patches.
	float dist;
	if (_PlanarShadowFadeMode < 0.5)
	{
		// Radial: circular gradient centred on the pivot.
		dist = length(projWS.xz - originWS.xz);
	}
	else
	{
		// Contact: measured along the direction the shadow stretches away from the object. The
		// footprint under the object stays at full strength (the dot product goes negative there and
		// clamps to 0) and the shadow only fades as it reaches out - which is what reads as a real
		// contact shadow rather than a circular blob.
		float2 lightXZ = lightDir.xz;
		float  lenXZ   = length(lightXZ);
		dist = (lenXZ > 1e-4)
			? max(dot(projWS.xz - originWS.xz, -lightXZ / lenXZ), 0.0)
			: length(projWS.xz - originWS.xz); // light straight down: no stretch axis, fall back to radial
	}

	float fade = saturate(dist / max(_PlanarShadowFadeDistance, 1e-4));

	output.positionCS = mul(UNITY_MATRIX_VP, float4(projWS, 1.0));
	output.data = float4(0.0, 0.0, fade, heightFade);
	return output;

#endif // BCM_PLANAR_SHADOW_BLOB
#endif // BCM_PLANAR_SHADOW
}

half4 fragment_planarshadow (Varyings_PlanarShadow input) : SV_Target
{
	half3 color = _PlanarShadowColor.rgb;
	half  alpha = _PlanarShadowColor.a * _PlanarShadowStrength * input.data.w;

#if defined(BCM_PLANAR_SHADOW_BLOB)

	float2 p   = input.data.xy;   // object-space ground position, pivot at the origin
	float2 e   = input.blob.xy;   // half extents
	float2 off = input.blob.zw;   // swept cap offset

	// Sweep the shape from its base to the offset cap, i.e. a capsule rather than just the offset
	// footprint. A plain offset copy detaches from the object and leaves a gap at the contact point -
	// the same reason Lego/StuddedBlock sweeps its stud shadow instead of offsetting a disc.
	float  h = saturate(dot(p, off) / max(dot(off, off), 1e-6));
	float2 q = p - off * h;

	// Rounded-box SDF. Roundness 0 is a hard rectangle, 1 collapses the corners all the way in and
	// reads as a stadium/ellipse.
	float  rr = min(e.x, e.y) * _PlanarShadowRoundness;
	float2 d  = abs(q) - e + rr;
	float  sd = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - rr;

	// The whole point of the analytic shape: the edge width is a parameter, not the mesh silhouette.
	alpha *= 1.0 - smoothstep(_PlanarShadowGrow - _PlanarShadowSoftness,
	                          _PlanarShadowGrow + _PlanarShadowSoftness, sd);

	// Contact gradient, measured from the object's own FOOTPRINT rather than from its centre: full
	// strength everywhere the object actually sits, fading only across the swept extension.
	// Normalising by the sweep alone (i.e. using h) collapses as soon as the object is wide relative
	// to the sweep - a half-extent of 0.5 with a 0.6 sweep is already at h ~ 0.83 by the time it
	// reaches the object's edge, so the only part not hidden under the object is the part that has
	// already faded to nothing, and the shadow reads as missing entirely.
	float2 d0  = abs(p) - e + rr;
	float  sd0 = length(max(d0, 0.0)) + min(max(d0.x, d0.y), 0.0) - rr;
	half fadeCoord = saturate(sd0 / max(length(off), 1e-4));

#else
	half fadeCoord = input.data.z;
#endif

#if defined(BCM_PLANAR_SHADOW_RAMP)
	// The artist curve replaces the procedural falloff rather than multiplying it, otherwise the two
	// would compound and no ramp could ever produce a hard edge.
	half4 ramp = tex2D(_PlanarShadowRamp, float2(fadeCoord, 0.5));
	color *= ramp.rgb;
	alpha *= ramp.a;
#else
	alpha *= pow(saturate(1.0 - fadeCoord), _PlanarShadowFalloff);
#endif

	// Kill fully transparent fragments BEFORE they reach the stencil. This matters far more than the
	// fill it saves: the stencil op runs on every surviving fragment, so without this the blob's
	// inflated canvas - which reaches well past the visible shadow - would stamp its ref across a wide
	// empty disc and block every OTHER object's shadow that falls inside it. Symptom is only the first
	// object of a group getting a shadow at all.
	clip(alpha - 0.002);

	return half4(color, saturate(alpha));
}

#endif // BCM_PLANAR_SHADOW_PASS