// BCM_Toony_shader
// Standalone hybrid toon shader (Built-in RP + URP) derived from the Toony Colors Pro 2 - Hybrid Shader.
// Self-contained: only needs BCM_Toony_Include.cginc next to this file. No external asset required.
//
// - Built-in Render Pipeline and Universal Render Pipeline are both supported (one SubShader each).
// - URP version is auto-detected from UNITY_VERSION so the same file works across URP 10 -> 17+ without regeneration.
// - Custom inspector (BCM.ToonyShaderGUI) is optional; if missing, Unity falls back to the default material editor.

Shader "BCM/BCM_Toony_shader"
{
	Properties
	{
		[Enum(Front, 2, Back, 1, Both, 0)] _Cull ("Render Face", Float) = 2.0
		[ToggleUI] _ZWrite ("Depth Write", Float) = 1.0
		[Toggle(_ALPHATEST_ON)] _UseAlphaTest ("Alpha Clipping", Float) = 0
		_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5

	//# Base
		[MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
		[MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
		_HColor ("Highlight Color", Color) = (1,1,1,1)
		[NoScaleOffset] _HColorMap ("Highlight Color Map", 2D) = "white" {}
		_SColor ("Shadow Color", Color) = (0.2,0.2,0.2,1)
		[NoScaleOffset] _SColorMap ("Shadow Color Map", 2D) = "white" {}
		[Toggle(TCP2_SHADOW_LIGHT_COLOR)] _ShadowColorLightAtten ("Main Light affects Shadow Color", Float) = 1
		[Toggle(TCP2_SHADOW_TEXTURE)] _UseShadowTexture ("Enable Shadow Albedo Texture", Float) = 0
		[NoScaleOffset] _ShadowBaseMap ("Shadow Albedo", 2D) = "gray" {}

	//# Multiply
		// BCM: tint a masked region of the albedo by a color (albedo *= color where mask = 1).
		[Toggle(BCM_MULTIPLY)] _UseMultiply ("Multiply", Float) = 0
		[NoScaleOffset] _MultiplyMap ("Multiply Mask", 2D) = "white" {}
		[Enum(R,0,G,1,B,2,A,3)] _MultiplyMaskChannel ("Mask Channel", Float) = 0
		_MultiplyColor ("Multiply Color", Color) = (1,1,1,1)
		_MultiplyStrength ("Strength", Range(0,1)) = 1

	//# Ramp Shading
		// Ramp Type keyword enum (handled by BCM.ToonyShaderGUI): Default / Crisp / Bands / Bands Crisp / Texture
		[HideInInspector] _RampType ("Ramp Type", Float) = 0
		[TCP2Gradient] _Ramp ("Ramp Texture (RGB)", 2D) = "gray" {}
		_RampScale ("Scale", Float) = 1.0
		_RampOffset ("Offset", Float) = 0.0
		[PowerSlider(0.415)] _RampThreshold ("Threshold", Range(0.01,1)) = 0.75
		_RampSmoothing ("Smoothing", Range(0,1)) = 0.1
		[IntRange] _RampBands ("Bands Count", Range(1,20)) = 4
		_RampBandsSmoothing ("Bands Smoothing", Range(0,1)) = 0.1
		// Multi-color ramp: Shadow + up to 6 middle colors + Highlight, each with a band % width.
		_ShadowPercent ("Shadow %", Range(0,100)) = 60
		_HighPercent ("Highlight %", Range(0,100)) = 20
		[HideInInspector] _MidCount ("Middle Color Count", Float) = 1
		_MidColor0 ("Middle Color 1", Color) = (0.6,0.6,0.6,1)
		[NoScaleOffset] _MidColor0Map ("Middle Color 1 Map", 2D) = "white" {}
		_MidColor1 ("Middle Color 2", Color) = (0.6,0.6,0.6,1)
		_MidColor2 ("Middle Color 3", Color) = (0.6,0.6,0.6,1)
		_MidColor3 ("Middle Color 4", Color) = (0.6,0.6,0.6,1)
		_MidColor4 ("Middle Color 5", Color) = (0.6,0.6,0.6,1)
		_MidColor5 ("Middle Color 6", Color) = (0.6,0.6,0.6,1)
		_MidPct0 ("Middle 1 %", Range(0,100)) = 20
		_MidPct1 ("Middle 2 %", Range(0,100)) = 20
		_MidPct2 ("Middle 3 %", Range(0,100)) = 20
		_MidPct3 ("Middle 4 %", Range(0,100)) = 20
		_MidPct4 ("Middle 5 %", Range(0,100)) = 20
		_MidPct5 ("Middle 6 %", Range(0,100)) = 20

	//# Normal Mapping
		[Toggle(_NORMALMAP)] _UseNormalMap ("Normal Mapping", Float) = 0
		_BumpMap ("Normal Map", 2D) = "bump" {}
		_BumpScale ("Scale", Range(-1,1)) = 1

	//# Specular
		[Toggle(TCP2_SPECULAR)] _UseSpecular ("Specular", Float) = 0
		// Specular Type keyword enum: GGX / Stylized / Crisp
		[HideInInspector] _SpecularType ("Type", Float) = 0
		[HDR] _SpecularColor ("Color", Color) = (0.75,0.75,0.75,1)
		// BCM: specular intensity, independent of color and roughness
		_SpecularStrength ("Strength", Range(0,4)) = 1
		// BCM: tint the highlight by the surface colour so a multi-coloured albedo gets per-pixel
		// specular colour instead of one uniform colour. 0 = uniform, i.e. the original behaviour.
		_SpecularAlbedoTint ("Albedo Tint", Range(0,1)) = 0
		[ToggleUI] _SpecularPreserveBrightness ("Preserve Brightness", Float) = 0
		[PowerSlider(5.0)] _SpecularToonSize ("Size", Range(0.001,1)) = 0.25
		_SpecularToonSmoothness ("Smoothing", Range(0,1)) = 0.05
		_SpecularRoughness ("Roughness", Range(0,1)) = 0.5
		[Enum(Disabled,0,Albedo Alpha,1,Custom R,2,Custom G,3,Custom B,4,Custom A,5)] _SpecularMapType ("Specular Map", Float) = 0
		[NoScaleOffset] _SpecGlossMap ("Specular Texture", 2D) = "white" {}

	//# Emission
		[Toggle(_EMISSION)] _UseEmission ("Emission", Float) = 0
		[Enum(No Texture,5,R,0,G,1,B,2,A,3,RGB,4)] _EmissionChannel ("Texture Channel", Float) = 4
		_EmissionMap ("Texture", 2D) = "white" {}
		[HDR] _EmissionColor ("Color", Color) = (1,1,0,1)

	//# Rim Lighting
		[Toggle(TCP2_RIM_LIGHTING)] _UseRim ("Rim Lighting", Float) = 0
		[HDR] _RimColor ("Color", Color) = (0.8,0.8,0.8,0.5)
		_RimMin ("Min", Range(0,2)) = 0.5
		_RimMax ("Max", Range(0,2)) = 1
		[Toggle(TCP2_RIM_LIGHTING_LIGHTMASK)] _UseRimLightMask ("Light-based Mask", Float) = 1
		// Directional Rim (Light Direction) - added feature
		[Toggle(BCM_RIM_LIGHTDIR)] _UseRimDir ("Light Direction", Float) = 0
		_RimLightDir ("Light Direction Rim Light", Range(0,1)) = 1
		[HDR] _RimColor2 ("Directional Rim Color", Color) = (0.8,0.8,0.8,0.5)
		_RimMin2 ("Directional Min", Range(0,2)) = 0.5
		_RimMax2 ("Directional Max", Range(0,2)) = 1

	//# MatCap
		[Toggle(TCP2_MATCAP)] _UseMatCap ("MatCap", Float) = 0
		[Enum(Additive,0,Replace,1)] _MatCapType ("MatCap Blending", Float) = 0
		[NoScaleOffset] _MatCapTex ("Texture", 2D) = "black" {}
		[HDR] _MatCapColor ("Color", Color) = (1,1,1,1)
		_MatCapPower ("Power", Range(0,4)) = 1
		[Toggle(TCP2_MATCAP_MASK)] _UseMatCapMask ("Enable Mask", Float) = 0
		[NoScaleOffset] _MatCapMask ("Mask Texture", 2D) = "black" {}
		[Enum(R,0,G,1,B,2,A,3)] _MatCapMaskChannel ("Texture Channel", Float) = 0
		// BCM: RGBA region map -> four independent per-region matcaps. Each channel is a weight, not an
		// index, so a region border cross-fades instead of resolving to a bogus "combination" region.
		// When enabled it fully replaces every base MatCap setting above (the Enable Mask is shared).
		[Toggle(BCM_MATCAP_CHANNEL)] _UseMatCapChannel ("Channel Texture", Float) = 0
		[NoScaleOffset] _MatCapChannelTex ("Channel Texture", 2D) = "black" {}
		[Enum(Additive,0,Replace,1)] _MatCapTypeR ("MatCap Blending R", Float) = 0
		[NoScaleOffset] _MatCapTexR ("Texture R", 2D) = "black" {}
		[HDR] _MatCapColorR ("Color R", Color) = (1,1,1,1)
		_MatCapPowerR ("Power R", Range(0,4)) = 1
		[Enum(Additive,0,Replace,1)] _MatCapTypeG ("MatCap Blending G", Float) = 0
		[NoScaleOffset] _MatCapTexG ("Texture G", 2D) = "black" {}
		[HDR] _MatCapColorG ("Color G", Color) = (1,1,1,1)
		_MatCapPowerG ("Power G", Range(0,4)) = 1
		[Enum(Additive,0,Replace,1)] _MatCapTypeB ("MatCap Blending B", Float) = 0
		[NoScaleOffset] _MatCapTexB ("Texture B", 2D) = "black" {}
		[HDR] _MatCapColorB ("Color B", Color) = (1,1,1,1)
		_MatCapPowerB ("Power B", Range(0,4)) = 1
		// Opt-in: a channel map authored without an alpha channel reads as alpha = 1 on every texel, which
		// would smear the A matcap over the whole mesh and halve the R/G/B weights via normalisation.
		[Toggle(BCM_MATCAP_CHANNEL_A)] _UseMatCapChannelA ("Enable A Channel", Float) = 0
		[Enum(Additive,0,Replace,1)] _MatCapTypeA ("MatCap Blending A", Float) = 0
		[NoScaleOffset] _MatCapTexA ("Texture A", 2D) = "black" {}
		[HDR] _MatCapColorA ("Color A", Color) = (1,1,1,1)
		_MatCapPowerA ("Power A", Range(0,4)) = 1

	//# Direct Light (per-material control over received direct lighting)
		// 100 = full light received, 50 = only 50% (darker). Ambient/GI is separate (see _IndirectIntensity).
		_LightReceived ("Light Received (%)", Range(0,100)) = 100
		_LightReceivedColor ("Light Tint", Color) = (1,1,1,1)

	//# Global Illumination - Indirect Diffuse
		_IndirectIntensity ("Strength", Range(0,1)) = 1
		[ToggleUI] _SingleIndirectColor ("Single Indirect Color", Float) = 0

	//# Indirect Specular (Environment Reflections)
		[Toggle(TCP2_REFLECTIONS)] _UseReflections ("Indirect Specular (Environment Reflections)", Float) = 0
		_ReflectionColor ("Color", Color) = (1,1,1,1)
		_ReflectionSmoothness ("Smoothness", Range(0,1)) = 0.5
		[Enum(Disabled,0,Albedo Alpha (Smoothness),1,Custom R (Smoothness),2,Custom G (Smoothness),3,Custom B (Smoothness),4,Custom A (Smoothness),5,Albedo Alpha (Mask),6,Custom R (Mask),7,Custom G (Mask),8,Custom B (Mask),9,Custom A (Mask),10)] _ReflectionMapType ("Reflection Map", Float) = 0
		[NoScaleOffset] _ReflectionTex ("Reflection Texture", 2D) = "white" {}
		[Toggle(TCP2_REFLECTIONS_FRESNEL)] _UseFresnelReflections ("Fresnel Reflections", Float) = 1
		_FresnelMin ("Fresnel Min", Range(0,2)) = 0
		_FresnelMax ("Fresnel Max", Range(0,2)) = 1.5

	//# Occlusion
		[Toggle(TCP2_OCCLUSION)] _UseOcclusion ("Occlusion", Float) = 0
		// BCM: when on, the occlusion map also darkens DIRECT lighting (not just ambient/GI),
		// so the occlusion texture reads regardless of ambient level. Non-PBR, off by default.
		[Toggle(BCM_OCCLUSION_DIRECT)] _OcclusionDirect ("Affect Direct Light", Float) = 0
		_OcclusionStrength ("Strength", Range(0.0, 1.0)) = 1.0
		[NoScaleOffset] _OcclusionMap ("Texture", 2D) = "white" {}
		[Enum(Albedo Alpha,0,Custom R,1,Custom G,2,Custom B,3,Custom A,4)] _OcclusionChannel ("Texture Channel", Float) = 0
		// Advanced occlusion: channel mask (dot with RGBA) + value remap
		[Toggle(BCM_OCCLUSION_ADV)] _UseOcclusionAdv ("Advanced (Channel Mask + Remap)", Float) = 0
		_OcclusionMapChannelMask ("Occlusion Channel Mask", Vector) = (1,0,0,0)
		_OcclusionRemapStart ("Occlusion Remap Start", Range(0,1)) = 0
		_OcclusionRemapEnd ("Occlusion Remap End", Range(0,1)) = 1

	//# Outline
		[ToggleUI] _UseOutline ("Outline", Float) = 0
		[HDR] _OutlineColor ("Color", Color) = (0,0,0,1)
		// Outline Texture keyword enum: Disabled / Vertex Shader / Pixel Shader
		[HideInInspector] _OutlineTextureType ("Texture", Float) = 0
		_OutlineTextureLOD ("Texture LOD", Range(0,8)) = 5
		_OutlineWidth ("Width", Range(0,10)) = 1
		// Outline Pixel Size keyword enum: Disabled / Constant / Minimum / Min Max
		[HideInInspector] _OutlinePixelSizeType ("Pixel Size", Float) = 0
		_OutlineMinWidth ("Minimum Width (Pixels)", Float) = 1
		_OutlineMaxWidth ("Maximum Width (Pixels)", Float) = 1
		// Outline Normals Source keyword enum: Normal / Vertex Colors / Tangents / UV1..UV4
		[HideInInspector] _NormalsSource ("Outline Normals Source", Float) = 0
		// Outline Normals UV Type keyword enum: Full XYZ / Compressed XY / Compressed ZW
		[HideInInspector] _NormalsUVType ("UV Data Type", Float) = 0
		// Outline Lighting keyword enum (BiRP: Disabled/Main/Indirect, URP: Disabled/Main/All/Indirect)
		[HideInInspector] _OutlineLightingType ("Lighting", Float) = 0
		[HideInInspector] _OutlineLightingTypeURP ("Lighting (URP)", Float) = 0
		_DirectIntensityOutline ("Direct Strength", Range(0,1)) = 1
		_IndirectIntensityOutline ("Indirect Strength", Range(0,1)) = 0
		// Outline Color Map - tints the outline with a texture (added feature)
		[Toggle(BCM_OUTLINE_COLORMAP)] _UseOutlineColorMap ("Outline Color Map", Float) = 0
		_OutlineColorMap ("Outline Color Map", 2D) = "white" {}
		// Vertex Color Outline Mask - per-vertex control over where the outline appears (added feature).
		// The channel value is the outline strength, so vertices painted black get no outline.
		[Toggle(BCM_OUTLINE_VCOLOR_MASK)] _UseOutlineVColorMask ("Vertex Color Mask", Float) = 0
		[Enum(R,0,G,1,B,2,Alpha,3)] _OutlineMaskChannel ("Mask Channel", Float) = 0
		[ToggleUI] _OutlineMaskInvert ("Invert Mask", Float) = 0

	//# Options
		[ToggleUI] _CastShadows ("Cast Shadows", Float) = 1
		[ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadowsOff ("Receive Shadows", Float) = 1
		// BCM: shape the received real-time shadow (only relevant when Receive Shadows is on)
		_ReceiveShadowStrength ("Receive Shadow Strength", Range(0,1)) = 1
		_ReceiveShadowSmoothing ("Receive Shadow Smoothing", Range(0,2)) = 0
		// BCM: tint of the received shadow. Black is the classic plain multiply, so it reproduces the
		// pre-color behaviour exactly - see BCM_TintReceivedShadow.
		_ReceiveShadowColor ("Receive Shadow Color", Color) = (0,0,0,1)

	//# Wind Sway
		// BCM: 정점 바람 흔들림. 뿌리(_WindBaseY)는 고정이고 _WindHeight 까지 올라가며 변형량이 커진다.
		// 나무·풀처럼 밑이 땅에 박힌 프롭 전용. 기본 꺼짐이라 켜지 않은 머티리얼의 비용은 0이다.
		[Toggle(BCM_WIND_SWAY)] _UseWindSway ("Wind Sway", Float) = 0
		_WindStrength ("Wind Strength (m)", Range(0,1)) = 0.12
		_WindSpeed ("Wind Speed", Range(0,6)) = 1.1
		// 변형량이 1.0 에 도달하는 높이(월드 m). 나무 실제 높이를 넣으면 꼭대기가 최대로 흔들린다.
		_WindHeight ("Wind Full Height (m)", Float) = 4.0
		// 고정될 밑면의 월드 Y. 이 아레나는 지면이 y=0 이라 0 이 맞다.
		_WindBaseY ("Wind Base Y (world)", Float) = 0
		// 2차 잔떨림 비율. 0 이면 사인 하나라 기계적으로 왕복한다.
		_WindTurbulence ("Wind Turbulence", Range(0,1)) = 0.35
		_WindDir ("Wind Direction (XZ)", Vector) = (1,0,0.35,0)
		// 월드 XZ → 위상 변환 배율. 크면 나무마다 위상이 크게 어긋나고, 0 이면 전부 같이 흔들린다.
		_WindPhaseScale ("Wind Phase Variation", Range(0,2)) = 0.35

	//# Ground Splat
		// BCM: 지면 텍스처 혼합. _BaseMap 이 바닥층, 정점 컬러 R/G/B 가 1/2/3층의 덮임 정도.
		// 기본 꺼짐 — 켜지 않은 머티리얼은 정점 컬러도 안 읽고 비용이 0이다.
		[Toggle(BCM_GROUND_SPLAT)] _UseGroundSplat ("Ground Splat", Float) = 0
		_SplatTex1 ("Splat Layer 1 (R)", 2D) = "white" {}
		_SplatTex2 ("Splat Layer 2 (G)", 2D) = "white" {}
		_SplatTex3 ("Splat Layer 3 (B)", 2D) = "white" {}
		_SplatTex4 ("Splat Grass (A)", 2D) = "white" {}
		_SplatTile4 ("Grass Tile Size m", Float) = 3.5
		_SplatTint4 ("Grass Tint", Color) = (1,1,1,1)
		_SplatStrength ("Layer Max Opacity (R,G,B,A)", Vector) = (1,1,1,1)
		// BCM: 1 이면 거리 안개를 안 받는다. 인트로 비행기·낙하산처럼 카메라 가까이서 안개 구간에 걸리는 연출용(ProtoIntro 가 런타임에 켬)
		[HideInInspector] _BCM_NoFog ("No Fog", Float) = 0
		_SplatTiles ("Tile Size m (Base,1,2,3)", Vector) = (9,13,8,6)
		_SplatSharpness ("Edge Softness", Range(0.005,0.5)) = 0.08
		_SplatHeightBlend ("Height Blend", Range(0,2)) = 0.4
		_SplatNoise ("Edge Noise", Range(0,1.5)) = 0.5
		_SplatNoiseScale ("Edge Noise Scale", Range(0.1,4)) = 1

	//# See Through
		// BCM: 캐릭터를 가리면 그 부분만 디더로 비친다. 수치는 전역(C# ProtoSeeThrough)이 정한다 — 머티리얼은 켜고 끄기만.
		[Toggle(BCM_SEE_THROUGH)] _UseSeeThrough ("See Through (occluder)", Float) = 0

		[HideInInspector] _RenderingMode ("rendering mode", Float) = 0.0
		[HideInInspector] _SrcBlend ("blending source", Float) = 1.0
		[HideInInspector] _DstBlend ("blending destination", Float) = 0.0
		[HideInInspector] _UseMobileMode ("Mobile mode", Float) = 0
	}


	//================================================================================================================================
	//
	// UNIVERSAL RENDER PIPELINE
	//
	//================================================================================================================================

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			"IgnoreProjector" = "True"
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
		}

		Blend [_SrcBlend] [_DstBlend]
		ZWrite [_ZWrite]
		Cull [_Cull]

		HLSLINCLUDE
			#define fixed half
			#define fixed2 half2
			#define fixed3 half3
			#define fixed4 half4

			// BCM_URP is defined here only when the URP package is installed (managed automatically by
			// Editor/BCM_PipelineConfigurator.cs). When it is not defined, this whole SubShader compiles
			// harmless stubs so the shader imports cleanly in Built-in RP projects.
			#include "BCM_PipelineConfig.cginc"

		#if defined(BCM_URP)
			#define TCP2_HYBRID_URP

			// Auto-detect URP version from the Unity version so this shader is portable across projects
			// without needing to be regenerated. Biased low at boundaries to stay compile-safe.
			#ifndef URP_VERSION
				#if UNITY_VERSION >= 60000100
					#define URP_VERSION 171
				#elif UNITY_VERSION >= 60000000
					#define URP_VERSION 17
				#elif UNITY_VERSION >= 202220
					#define URP_VERSION 14
				#elif UNITY_VERSION >= 202120
					#define URP_VERSION 12
				#elif UNITY_VERSION >= 202020
					#define URP_VERSION 10
				#else
					#define URP_VERSION 8
				#endif
			#endif
		#endif
		ENDHLSL

		Pass
		{
			Name "Main"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 3.0

			#pragma vertex Vertex
			#pragma shader_feature_local_vertex BCM_WIND_SWAY
			#pragma fragment Fragment
			#pragma shader_feature_local BCM_GROUND_SPLAT
			#pragma shader_feature_local BCM_SEE_THROUGH

			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			// -------------------------------------
			// Material keywords
			#pragma shader_feature_local _ _RECEIVE_SHADOWS_OFF

			// -------------------------------------
			// Universal Render Pipeline keywords (declared unconditionally; unused ones become harmless idle variants)
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT
			#pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile_fragment _ _LIGHT_LAYERS
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
			#pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
			#pragma multi_compile _ _FORWARD_PLUS
			#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

			// -------------------------------------
			// Unity keywords
			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

			//--------------------------------------
			// BCM Toony keywords
			#pragma shader_feature_local TCP2_MOBILE
			#pragma shader_feature_local_fragment _ TCP2_RAMPTEXT TCP2_RAMP_CRISP TCP2_RAMP_BANDS TCP2_RAMP_BANDS_CRISP BCM_RAMP_TRICOLOR
			#pragma shader_feature_local_fragment BCM_RAMP_COLOR
			#pragma shader_feature_local_fragment TCP2_SHADOW_LIGHT_COLOR
			#pragma shader_feature_local_fragment TCP2_SHADOW_TEXTURE
			#pragma shader_feature_local_fragment TCP2_SPECULAR
			#pragma shader_feature_local_fragment _ TCP2_SPECULAR_STYLIZED TCP2_SPECULAR_CRISP
			#pragma shader_feature_local TCP2_RIM_LIGHTING
			#pragma shader_feature_local TCP2_RIM_LIGHTING_LIGHTMASK
			#pragma shader_feature_local_fragment BCM_RIM_LIGHTDIR
			#pragma shader_feature_local TCP2_REFLECTIONS
			#pragma shader_feature_local TCP2_REFLECTIONS_FRESNEL
			#pragma shader_feature_local TCP2_MATCAP
			#pragma shader_feature_local_fragment TCP2_MATCAP_MASK
			#pragma shader_feature_local_fragment BCM_MATCAP_CHANNEL
			#pragma shader_feature_local_fragment BCM_MATCAP_CHANNEL_A
			#pragma shader_feature_local_fragment TCP2_OCCLUSION
			#pragma shader_feature_local_fragment BCM_OCCLUSION_ADV
			#pragma shader_feature_local_fragment BCM_OCCLUSION_DIRECT
			#pragma shader_feature_local _NORMALMAP
			#pragma shader_feature_local_fragment _ALPHATEST_ON
			#pragma shader_feature_local_fragment _EMISSION
			#pragma shader_feature_local_fragment BCM_MULTIPLY

			// This is using an existing keyword to separate fade/transparent behaviors
			#pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON

		#if defined(BCM_URP)
			#include "BCM_Toony_Include.cginc"
		#else
			#include "BCM_Toony_URPStub.cginc"
		#endif

			ENDHLSL
		}

		//--------------------------------------------------------------------------------------------------------------------------------

		// Outline
		// NOTE: URP does not render arbitrary LightMode tags. Using "SRPDefaultUnlit" makes URP's
		// forward renderer draw this pass automatically alongside "UniversalForward" - no RenderObjects
		// renderer feature required (keeps the shader standalone). Materials with the outline disabled
		// collapse the geometry to a degenerate primitive (see BCM_OUTLINE gate in the include), so this
		// pass is harmless when the outline is off.
		Pass
		{
			Name "Outline"
			Tags { "LightMode" = "SRPDefaultUnlit" }
			Cull Front

			HLSLPROGRAM
			#pragma target 3.0

			#pragma vertex vertex_outline
			#pragma shader_feature_local_vertex BCM_WIND_SWAY
			#pragma fragment fragment_outline
			#pragma shader_feature_local BCM_SEE_THROUGH

			#pragma multi_compile_instancing
			#pragma multi_compile_fog

			// -------------------------------------
			// Material keywords
			#pragma shader_feature_local _ _RECEIVE_SHADOWS_OFF

			// -------------------------------------
			// Universal Render Pipeline keywords
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT
			#pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_fragment _ _LIGHT_LAYERS
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
			#pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
			#pragma multi_compile _ _FORWARD_PLUS
			#pragma multi_compile _ _CLUSTER_LIGHT_LOOP

			// -------------------------------------
			// Unity keywords
			#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

			// -------------------------------------
			// BCM Toony keywords
			#pragma shader_feature_local _ TCP2_COLORS_AS_NORMALS TCP2_TANGENT_AS_NORMALS TCP2_UV1_AS_NORMALS TCP2_UV2_AS_NORMALS TCP2_UV3_AS_NORMALS TCP2_UV4_AS_NORMALS
			#pragma shader_feature_local _ TCP2_UV_NORMALS_FULL TCP2_UV_NORMALS_ZW
			#pragma shader_feature_local_vertex _ TCP2_OUTLINE_CONST_SIZE TCP2_OUTLINE_MIN_SIZE TCP2_OUTLINE_MIN_MAX_SIZE
			#pragma shader_feature_local _ TCP2_OUTLINE_TEXTURED_VERTEX TCP2_OUTLINE_TEXTURED_FRAGMENT
			#pragma shader_feature_local _ TCP2_OUTLINE_LIGHTING_MAIN TCP2_OUTLINE_LIGHTING_ALL TCP2_OUTLINE_LIGHTING_INDIRECT
			#pragma shader_feature_local_fragment _ TCP2_SHADOW_TEXTURE
			#pragma shader_feature_local BCM_OUTLINE
			#pragma shader_feature_local BCM_OUTLINE_COLORMAP
			#pragma shader_feature_local_vertex BCM_OUTLINE_VCOLOR_MASK

			#define TCP2_OUTLINE_PASS
		#if defined(BCM_URP)
			#include "BCM_Toony_Include.cginc"
		#else
			#include "BCM_Toony_URPStub.cginc"
		#endif

			ENDHLSL
		}

		//--------------------------------------------------------------------------------------------------------------------------------

		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On
			ZTest LEqual
			Cull [_Cull]

			HLSLPROGRAM
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 2.0

			#pragma vertex ShadowPassVertex
			#pragma shader_feature_local_vertex BCM_WIND_SWAY
			#pragma fragment ShadowPassFragment

			#pragma multi_compile_instancing
			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
			#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

			#pragma shader_feature_local_fragment _ALPHATEST_ON

			#define SHADOW_CASTER_PASS
		#if defined(BCM_URP)
			#include "BCM_Toony_Include.cginc"

			float3 _LightDirection;
			float3 _LightPosition;

			struct Attributes_Shadow
			{
				float4 positionOS   : POSITION;
				float3 normalOS     : NORMAL;
				float2 texcoord     : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings_Shadow
			{
				float2 uv           : TEXCOORD0;
				float4 positionCS   : SV_POSITION;
			};

			float4 GetShadowPositionHClip(Attributes_Shadow input)
			{
				float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
				float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
					float3 lightDirectionWS = normalize(_LightPosition - positionWS);
				#else
					float3 lightDirectionWS = _LightDirection;
				#endif

				float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

				#if UNITY_REVERSED_Z
					positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
				#else
					positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
				#endif

				return positionCS;
			}

			Varyings_Shadow ShadowPassVertex(Attributes_Shadow input)
			{
				BCM_ApplyWindSway(input.positionOS);   // BCM: 바람 — 그림자가 나무를 따라가야 한다

				Varyings_Shadow output = (Varyings_Shadow)0;
				UNITY_SETUP_INSTANCE_ID(input);

				output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
				output.positionCS = GetShadowPositionHClip(input);
				return output;
			}

			half4 ShadowPassFragment(Varyings_Shadow input) : SV_TARGET
			{
				// BCM: Cast Shadows toggle - discard so no depth is written to the shadow map (no shadow cast)
				clip(_CastShadows - 0.5);

				#if defined(_ALPHATEST_ON)
					half4 albedo = tex2D(_BaseMap, input.uv.xy).rgba;
					albedo.rgb *= _BaseColor.rgb;
					half alpha = albedo.a * _BaseColor.a;
					clip(alpha - _Cutoff);
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					const float dither = Dither4x4(input.positionCS.xy);
					const float ditherThreshold = unity_LODFade.x - CopySign(dither, unity_LODFade.x);
					clip(ditherThreshold);
				#endif

				return 0;
			}
		#else
			#include "BCM_Toony_URPStub.cginc"
		#endif

			ENDHLSL
		}

		//--------------------------------------------------------------------------------------------------------------------------------

		Pass
		{
			Name "DepthOnly"
			Tags { "LightMode" = "DepthOnly" }

			ZWrite On
			ColorMask 0
			Cull [_Cull]

			HLSLPROGRAM

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 2.0

			#pragma vertex DepthOnlyVertex
			#pragma shader_feature_local_vertex BCM_WIND_SWAY
			#pragma fragment DepthOnlyFragment
			#pragma shader_feature_local BCM_SEE_THROUGH

			#pragma multi_compile_instancing
			#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

			#pragma shader_feature_local_fragment _ALPHATEST_ON

		#if defined(BCM_URP)
			#include "BCM_Toony_Include.cginc"

			struct Attributes_Depth
			{
				float4 position     : POSITION;
				float2 texcoord     : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings_Depth
			{
				float2 uv           : TEXCOORD0;
				float4 positionCS   : SV_POSITION;
			#if defined(BCM_SEE_THROUGH)
				float3 positionWS   : TEXCOORD1;
			#endif
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings_Depth DepthOnlyVertex(Attributes_Depth input)
			{
				// BCM: 바람. 이 패스는 SSAO 가 쓰는 깊이를 그린다(URP-Balanced-Renderer 의 SSAO,
				// Source=Depth). 안 밀면 나무 주변 AO 가 제자리에 남아 그림자 얼룩이 따라오지 않는다.
				BCM_ApplyWindSway(input.position);

				Varyings_Depth output = (Varyings_Depth)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
				output.positionCS = TransformObjectToHClip(input.position.xyz);
				#if defined(BCM_SEE_THROUGH)
					output.positionWS = TransformObjectToWorld(input.position.xyz);
				#endif
				return output;
			}

			half4 DepthOnlyFragment(Varyings_Depth input) : SV_TARGET
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				#if defined(BCM_SEE_THROUGH)
					// SSAO 가 읽는 깊이도 같이 비운다 — 안 그러면 투시된 자리의 캐릭터에 건물 AO 얼룩이 앉는다.
					BCM_SeeThroughClip(input.positionWS, input.positionCS.xy);
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					const float dither = Dither4x4(input.positionCS.xy);
					const float ditherThreshold = unity_LODFade.x - CopySign(dither, unity_LODFade.x);
					clip(ditherThreshold);
				#endif

				half4 albedo = tex2D(_BaseMap, input.uv.xy).rgba;
				albedo.rgb *= _BaseColor.rgb;
				half alpha = albedo.a * _BaseColor.a;

				#if defined(_ALPHATEST_ON)
					clip(alpha - _Cutoff);
				#endif

				return 0;
			}
		#else
			#include "BCM_Toony_URPStub.cginc"
		#endif

			ENDHLSL
		}

		//--------------------------------------------------------------------------------------------------------------------------------

		Pass
		{
			Name "DepthNormals"
			Tags { "LightMode" = "DepthNormals" }

			ZWrite On
			Cull [_Cull]

			HLSLPROGRAM

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 2.0

			#pragma vertex DepthNormalsVertex
			#pragma shader_feature_local_vertex BCM_WIND_SWAY
			#pragma fragment DepthNormalsFragment

			#pragma multi_compile_instancing
			#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS

			#pragma shader_feature_local_fragment _ALPHATEST_ON

			#define DEPTH_NORMALS_PASS
		#if defined(BCM_URP)
			#include "BCM_Toony_Include.cginc"

			struct Attributes_Depth
			{
				float4 position     : POSITION;
				float2 texcoord     : TEXCOORD0;
				float3 normal       : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings_Depth
			{
				float2 uv           : TEXCOORD0;
				float4 positionCS   : SV_POSITION;
				float3 normalWS     : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings_Depth DepthNormalsVertex(Attributes_Depth input)
			{
				BCM_ApplyWindSway(input.position);   // BCM: 바람

				Varyings_Depth output = (Varyings_Depth)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
				output.positionCS = TransformObjectToHClip(input.position.xyz);
				float3 normalWS = TransformObjectToWorldNormal(input.normal);
				output.normalWS = NormalizeNormalPerVertex(normalWS);
				return output;
			}

			half4 DepthNormalsFragment(
				Varyings_Depth input
#ifdef _WRITE_RENDERING_LAYERS
	#if UNITY_VERSION >= 60020000
				, out uint outRenderingLayers : SV_Target1
	#else
				, out float4 outRenderingLayers : SV_Target1
	#endif
#endif
				) : SV_TARGET
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				#if defined(LOD_FADE_CROSSFADE)
					const float dither = Dither4x4(input.positionCS.xy);
					const float ditherThreshold = unity_LODFade.x - CopySign(dither, unity_LODFade.x);
					clip(ditherThreshold);
				#endif

				half4 albedo = tex2D(_BaseMap, input.uv.xy).rgba;
				half alpha = albedo.a * _BaseColor.a;

				#if defined(_ALPHATEST_ON)
					clip(alpha - _Cutoff);
				#endif

				#if URP_VERSION >= 14 && defined(_WRITE_RENDERING_LAYERS)
					#if UNITY_VERSION >= 60020000
						outRenderingLayers = EncodeMeshRenderingLayer();
					#else
						outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
					#endif
				#endif

				#if URP_VERSION >= 12
					return float4(input.normalWS.xyz, 0.0);
				#else
					return float4(PackNormalOctRectEncode(TransformWorldToViewDir(input.normalWS, true)), 0.0, 0.0);
				#endif
			}
		#else
			#include "BCM_Toony_URPStub.cginc"
		#endif

			ENDHLSL
		}

		//--------------------------------------------------------------------------------------------------------------------------------

		Pass
		{
			Name "Meta"
			Tags { "LightMode"="Meta" }

			Cull Off

			HLSLPROGRAM
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 3.0

			#pragma vertex Vertex
			#pragma shader_feature_local_vertex BCM_WIND_SWAY
			#pragma fragment Fragment

			//--------------------------------------
			// BCM Toony keywords
			#pragma shader_feature_local TCP2_MOBILE
			#pragma shader_feature_local_fragment TCP2_SPECULAR
			#pragma shader_feature_local_fragment _ALPHATEST_ON
			#pragma shader_feature_local_fragment _EMISSION
			#pragma shader_feature_local_fragment BCM_MULTIPLY

			#ifndef UNITY_PASS_META
				#define UNITY_PASS_META
			#endif

		#if defined(BCM_URP)
			#undef UNITY_SHOULD_SAMPLE_SH
			#define UNITY_SHOULD_SAMPLE_SH 0

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
			#include "BCM_Toony_Include.cginc"
		#else
			#include "BCM_Toony_URPStub.cginc"
		#endif

			ENDHLSL
		}

	}


	//================================================================================================================================
	//
	// BUILT-IN RENDER PIPELINE
	//
	//================================================================================================================================

	SubShader
	{
		Tags
		{
			"IgnoreProjector" = "True"
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
		}

		Blend [_SrcBlend] [_DstBlend]
		ZWrite [_ZWrite]
		Cull [_Cull]

		CGINCLUDE
			// Note: CG code is only used for the built-in render pipeline
			#include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"
			#include "UnityStandardUtils.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"
		ENDCG

		Pass
		{
			Name "Main"
			Tags { "LightMode"="ForwardBase" }

			CGPROGRAM
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 3.0

			#pragma vertex Vertex
			#pragma shader_feature_local_vertex BCM_WIND_SWAY
			#pragma fragment Fragment

			#pragma multi_compile_fog
			#pragma multi_compile_instancing
			#pragma multi_compile_fwdbase noshadowmask nodynlightmap nolightmap

			// -------------------------------------
			// Material keywords
			#pragma shader_feature_local _ _RECEIVE_SHADOWS_OFF

			// -------------------------------------
			// Unity keywords
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

			//--------------------------------------
			// BCM Toony keywords
			#pragma shader_feature_local TCP2_MOBILE
			#pragma shader_feature_local_fragment _ TCP2_RAMPTEXT TCP2_RAMP_CRISP TCP2_RAMP_BANDS TCP2_RAMP_BANDS_CRISP BCM_RAMP_TRICOLOR
			#pragma shader_feature_local_fragment BCM_RAMP_COLOR
			#pragma shader_feature_local_fragment TCP2_SHADOW_LIGHT_COLOR
			#pragma shader_feature_local_fragment TCP2_SHADOW_TEXTURE
			#pragma shader_feature_local_fragment TCP2_SPECULAR
			#pragma shader_feature_local_fragment _ TCP2_SPECULAR_STYLIZED TCP2_SPECULAR_CRISP
			#pragma shader_feature_local TCP2_RIM_LIGHTING
			#pragma shader_feature_local TCP2_RIM_LIGHTING_LIGHTMASK
			#pragma shader_feature_local_fragment BCM_RIM_LIGHTDIR
			#pragma shader_feature_local TCP2_REFLECTIONS
			#pragma shader_feature_local TCP2_REFLECTIONS_FRESNEL
			#pragma shader_feature_local TCP2_MATCAP
			#pragma shader_feature_local_fragment TCP2_MATCAP_MASK
			#pragma shader_feature_local_fragment BCM_MATCAP_CHANNEL
			#pragma shader_feature_local_fragment BCM_MATCAP_CHANNEL_A
			#pragma shader_feature_local_fragment TCP2_OCCLUSION
			#pragma shader_feature_local_fragment BCM_OCCLUSION_ADV
			#pragma shader_feature_local_fragment BCM_OCCLUSION_DIRECT
			#pragma shader_feature_local _NORMALMAP
			#pragma shader_feature_local_fragment _ALPHATEST_ON
			#pragma shader_feature_local_fragment _EMISSION
			#pragma shader_feature_local_fragment BCM_MULTIPLY

			// This is using an existing keyword to separate fade/transparent behaviors
			#pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON

			#define UNITY_INSTANCED_SH
			#include "UnityShaderVariables.cginc"
			#include "UnityShaderUtilities.cginc"

			#undef UNITY_SHOULD_SAMPLE_SH
			#define UNITY_SHOULD_SAMPLE_SH 1
			#include "AutoLight.cginc"

			#if !defined(UNITY_PASS_FORWARDBASE)
				#define UNITY_PASS_FORWARDBASE
			#endif

			#include "BCM_Toony_Include.cginc"

			ENDCG
		}

		Pass
		{
			Name "Main"
			Tags { "LightMode"="ForwardAdd" }

			Blend [_SrcBlend] One
			Fog { Color (0,0,0,0) } // in additive pass fog should be black
			ZWrite Off
			ZTest LEqual

			CGPROGRAM
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 3.0

			#pragma vertex Vertex
			#pragma shader_feature_local_vertex BCM_WIND_SWAY
			#pragma fragment Fragment

			#pragma multi_compile_fog
			#pragma multi_compile_instancing
			#pragma multi_compile_fwdadd_fullshadows

			// -------------------------------------
			// Material keywords
			#pragma shader_feature_local _ _RECEIVE_SHADOWS_OFF

			// -------------------------------------
			// Unity keywords
			#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

			//--------------------------------------
			// BCM Toony keywords
			#pragma shader_feature_local TCP2_MOBILE
			#pragma shader_feature_local_fragment _ TCP2_RAMPTEXT TCP2_RAMP_CRISP TCP2_RAMP_BANDS TCP2_RAMP_BANDS_CRISP BCM_RAMP_TRICOLOR
			#pragma shader_feature_local_fragment BCM_RAMP_COLOR
			#pragma shader_feature_local_fragment TCP2_SHADOW_LIGHT_COLOR
			#pragma shader_feature_local_fragment TCP2_SHADOW_TEXTURE
			#pragma shader_feature_local_fragment TCP2_SPECULAR
			#pragma shader_feature_local_fragment _ TCP2_SPECULAR_STYLIZED TCP2_SPECULAR_CRISP
			#pragma shader_feature_local TCP2_RIM_LIGHTING
			#pragma shader_feature_local TCP2_RIM_LIGHTING_LIGHTMASK
			#pragma shader_feature_local_fragment BCM_RIM_LIGHTDIR
			#pragma shader_feature_local _NORMALMAP
			#pragma shader_feature_local_fragment _ALPHATEST_ON
			#pragma shader_feature_local_fragment _EMISSION
			#pragma shader_feature_local_fragment BCM_MULTIPLY

			// This is using an existing keyword to separate fade/transparent behaviors
			#pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON

			#define UNITY_INSTANCED_SH
			#include "UnityShaderVariables.cginc"
			#include "UnityShaderUtilities.cginc"
			#include "AutoLight.cginc"

			#include "BCM_Toony_Include.cginc"

			ENDCG
		}

		// ShadowCaster & Depth Pass
		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			CGPROGRAM
			#pragma vertex vertex_shadow
			#pragma shader_feature_local_vertex BCM_WIND_SWAY
			#pragma fragment fragment_shadow
			#pragma target 2.0

			#pragma multi_compile_shadowcaster
			#pragma multi_compile_instancing
			#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

			#pragma shader_feature_local_fragment _ALPHATEST_ON

			#define SHADOW_CASTER_PASS
			#include "BCM_Toony_Include.cginc"

			struct Varyings_Shadow
			{
				V2F_SHADOW_CASTER;
				float2 uv : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings_Shadow vertex_shadow (appdata_base v)
			{
				BCM_ApplyWindSway(v.vertex);   // BCM: 바람 (빌트인 경로 — 이 프로젝트는 URP 라 안 타지만 갈라두지 않는다)

				Varyings_Shadow o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
				o.uv = TRANSFORM_TEX(v.texcoord, _BaseMap);
				return o;
			}

			float4 fragment_shadow (Varyings_Shadow i) : SV_Target
			{
				// BCM: Cast Shadows toggle - discard so no depth is written to the shadow map (no shadow cast)
				clip(_CastShadows - 0.5);

				#if defined(_ALPHATEST_ON)
					half4 albedo = tex2D(_BaseMap, i.uv.xy).rgba;
					albedo.rgb *= _BaseColor.rgb;
					half alpha = albedo.a * _BaseColor.a;
					clip(alpha - _Cutoff);
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					const float dither = Dither4x4(i.pos.xy);
					const float ditherThreshold = unity_LODFade.x - CopySign(dither, unity_LODFade.x);
					clip(ditherThreshold);
				#endif

				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDCG
		}

		Pass
		{
			Name "Meta"
			Tags { "LightMode"="Meta" }

			Cull Off

			CGPROGRAM
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 3.0

			#pragma vertex Vertex
			#pragma shader_feature_local_vertex BCM_WIND_SWAY
			#pragma fragment Fragment

			//--------------------------------------
			// BCM Toony keywords
			#pragma shader_feature_local TCP2_MOBILE
			#pragma shader_feature_local_fragment TCP2_SPECULAR
			#pragma shader_feature_local_fragment _ALPHATEST_ON
			#pragma shader_feature_local_fragment _EMISSION
			#pragma shader_feature_local_fragment BCM_MULTIPLY

			#undef UNITY_SHOULD_SAMPLE_SH
			#define UNITY_SHOULD_SAMPLE_SH 0

			#if !defined(UNITY_PASS_META)
				#define UNITY_PASS_META
			#endif
			#include "BCM_Toony_Include.cginc"

			ENDCG
		}

		// Outline
		Pass
		{
			Name "Outline"
			Tags { "LightMode"="ForwardBase" }
			Cull Front

			CGPROGRAM
			#pragma target 3.0

			#pragma vertex vertex_outline
			#pragma shader_feature_local_vertex BCM_WIND_SWAY
			#pragma fragment fragment_outline

			#pragma multi_compile_instancing
			#pragma multi_compile_fog

			// -------------------------------------
			// Material keywords
			#pragma shader_feature_local _ _RECEIVE_SHADOWS_OFF

			// -------------------------------------
			// Unity keywords
			#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

			// -------------------------------------
			// BCM Toony keywords
			#pragma shader_feature_local _ TCP2_COLORS_AS_NORMALS TCP2_TANGENT_AS_NORMALS TCP2_UV1_AS_NORMALS TCP2_UV2_AS_NORMALS TCP2_UV3_AS_NORMALS TCP2_UV4_AS_NORMALS
			#pragma shader_feature_local _ TCP2_UV_NORMALS_FULL TCP2_UV_NORMALS_ZW
			#pragma shader_feature_local_vertex _ TCP2_OUTLINE_CONST_SIZE TCP2_OUTLINE_MIN_SIZE TCP2_OUTLINE_MIN_MAX_SIZE
			#pragma shader_feature_local _ TCP2_OUTLINE_TEXTURED_VERTEX TCP2_OUTLINE_TEXTURED_FRAGMENT
			#pragma shader_feature_local _ TCP2_OUTLINE_LIGHTING_MAIN TCP2_OUTLINE_LIGHTING_INDIRECT
			#pragma shader_feature_local_fragment _ TCP2_SHADOW_TEXTURE
			#pragma shader_feature_local BCM_OUTLINE
			#pragma shader_feature_local BCM_OUTLINE_COLORMAP
			#pragma shader_feature_local_vertex BCM_OUTLINE_VCOLOR_MASK

			#define TCP2_OUTLINE_PASS
			#include "BCM_Toony_Include.cginc"

			ENDCG
		}
	}

	FallBack "Hidden/InternalErrorShader"
	CustomEditor "BCM.ToonyShaderGUI"
}
