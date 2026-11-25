// BCM_Toony_shader - per-section copy/paste for the material inspector.
// Lifts one section's values (properties + keywords) off a material so they can be
// stamped onto another material that uses the same shader.
//
// Storage: the payload is JSON, written to BOTH
//   - SessionState  -> survives script recompiles inside this editor session
//   - the OS clipboard -> can be pasted into another Unity instance, or into chat
// Texture references are stored as asset GUIDs, so they only resolve inside the
// project they were copied from. A texture whose GUID cannot be resolved is left
// untouched on the target rather than cleared - see ApplySection.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BCM
{
	// One copyable unit of the inspector: the properties and keywords that belong together.
	public class ToonySection
	{
		public readonly string Id;
		public readonly string Label;
		public readonly string[] Props;
		public readonly string[] Keywords;

		public ToonySection(string id, string label, string[] props, string[] keywords)
		{
			Id = id;
			Label = label;
			Props = props ?? new string[0];
			Keywords = keywords ?? new string[0];
		}
	}

	// The section table. Order here is the order used by Copy All.
	// Every property/keyword the inspector can touch must appear in exactly one section,
	// otherwise Copy All silently leaves it behind.
	public static class ToonySections
	{
		public const string Mobile       = "mobile";
		public const string Transparency = "transparency";
		public const string Base         = "base";
		public const string Multiply     = "multiply";
		public const string Ramp         = "ramp";
		public const string Normal       = "normal";
		public const string Specular     = "specular";
		public const string Emission     = "emission";
		public const string Rim          = "rim";
		public const string MatCap       = "matcap";
		public const string GI           = "gi";
		public const string Reflections  = "reflections";
		public const string Occlusion    = "occlusion";
		public const string Outline      = "outline";
		public const string PlanarShadow = "planarshadow";
		public const string StudShadow   = "studshadow";
		public const string XRay         = "xray";
		public const string Options      = "options";

		public static readonly ToonySection[] All =
		{
			new ToonySection(Mobile, "Mobile Mode",
				new[] { "_UseMobileMode" },
				new[] { "TCP2_MOBILE" }),

			new ToonySection(Transparency, "Transparency",
				new[] { "_RenderingMode", "_Cull", "_ZWrite", "_UseAlphaTest", "_Cutoff", "_SrcBlend", "_DstBlend" },
				new[] { "_ALPHATEST_ON", "_ALPHAPREMULTIPLY_ON" }),

			new ToonySection(Base, "Base",
				new[] { "_BaseColor", "_BaseMap", "_HColor", "_HColorMap", "_SColor", "_SColorMap",
						"_ShadowColorLightAtten", "_UseShadowTexture", "_ShadowBaseMap" },
				new[] { "TCP2_SHADOW_LIGHT_COLOR", "TCP2_SHADOW_TEXTURE" }),

			new ToonySection(Multiply, "Multiply",
				new[] { "_UseMultiply", "_MultiplyMap", "_MultiplyMaskChannel", "_MultiplyColor",
						"_MultiplyStrength" },
				new[] { "BCM_MULTIPLY" }),

			new ToonySection(Ramp, "Ramp Shading",
				new[] { "_RampType", "_Ramp", "_RampScale", "_RampOffset", "_RampThreshold", "_RampSmoothing",
						"_RampBands", "_RampBandsSmoothing", "_ShadowPercent", "_HighPercent", "_MidCount",
						"_MidColor0", "_MidColor0Map", "_MidColor1", "_MidColor2", "_MidColor3", "_MidColor4", "_MidColor5",
						"_MidPct0", "_MidPct1", "_MidPct2", "_MidPct3", "_MidPct4", "_MidPct5" },
				new[] { "TCP2_RAMPTEXT", "TCP2_RAMP_CRISP", "TCP2_RAMP_BANDS", "TCP2_RAMP_BANDS_CRISP",
						"BCM_RAMP_TRICOLOR", "BCM_RAMP_COLOR" }),

			new ToonySection(Normal, "Normal Mapping",
				new[] { "_UseNormalMap", "_BumpMap", "_BumpScale" },
				new[] { "_NORMALMAP" }),

			new ToonySection(Specular, "Specular",
				new[] { "_UseSpecular", "_SpecularType", "_SpecularColor", "_SpecularStrength", "_SpecularToonSize",
						"_SpecularToonSmoothness", "_SpecularRoughness", "_SpecularMapType", "_SpecGlossMap",
						"_SpecularAlbedoTint", "_SpecularPreserveBrightness" },
				new[] { "TCP2_SPECULAR", "TCP2_SPECULAR_STYLIZED", "TCP2_SPECULAR_CRISP" }),

			new ToonySection(Emission, "Emission",
				new[] { "_UseEmission", "_EmissionChannel", "_EmissionMap", "_EmissionColor" },
				new[] { "_EMISSION" }),

			new ToonySection(Rim, "Rim Lighting",
				new[] { "_UseRim", "_RimColor", "_RimMin", "_RimMax", "_UseRimLightMask",
						"_UseRimDir", "_RimLightDir", "_RimColor2", "_RimMin2", "_RimMax2" },
				new[] { "TCP2_RIM_LIGHTING", "TCP2_RIM_LIGHTING_LIGHTMASK", "BCM_RIM_LIGHTDIR" }),

			new ToonySection(MatCap, "MatCap",
				new[] { "_UseMatCap", "_MatCapType", "_MatCapTex", "_MatCapColor", "_MatCapPower",
						"_UseMatCapMask", "_MatCapMask", "_MatCapMaskChannel",
						"_UseMatCapChannel", "_MatCapChannelTex",
						"_MatCapTypeR", "_MatCapTexR", "_MatCapColorR", "_MatCapPowerR",
						"_MatCapTypeG", "_MatCapTexG", "_MatCapColorG", "_MatCapPowerG",
						"_MatCapTypeB", "_MatCapTexB", "_MatCapColorB", "_MatCapPowerB",
						"_UseMatCapChannelA", "_MatCapTypeA", "_MatCapTexA", "_MatCapColorA", "_MatCapPowerA" },
				new[] { "TCP2_MATCAP", "TCP2_MATCAP_MASK", "BCM_MATCAP_CHANNEL", "BCM_MATCAP_CHANNEL_A" }),

			new ToonySection(GI, "Global Illumination",
				new[] { "_LightReceived", "_LightReceivedColor", "_IndirectIntensity", "_SingleIndirectColor" },
				null),

			new ToonySection(Reflections, "Indirect Specular",
				new[] { "_UseReflections", "_ReflectionColor", "_ReflectionSmoothness", "_ReflectionMapType",
						"_ReflectionTex", "_UseFresnelReflections", "_FresnelMin", "_FresnelMax" },
				new[] { "TCP2_REFLECTIONS", "TCP2_REFLECTIONS_FRESNEL" }),

			new ToonySection(Occlusion, "Occlusion",
				new[] { "_UseOcclusion", "_OcclusionDirect", "_OcclusionStrength", "_OcclusionMap", "_OcclusionChannel",
						"_UseOcclusionAdv", "_OcclusionMapChannelMask", "_OcclusionRemapStart", "_OcclusionRemapEnd" },
				new[] { "TCP2_OCCLUSION", "BCM_OCCLUSION_DIRECT", "BCM_OCCLUSION_ADV" }),

			new ToonySection(Outline, "Outline",
				new[] { "_UseOutline", "_OutlineColor", "_OutlineTextureType", "_OutlineTextureLOD", "_OutlineWidth",
						"_OutlinePixelSizeType", "_OutlineMinWidth", "_OutlineMaxWidth", "_NormalsSource", "_NormalsUVType",
						"_OutlineLightingType", "_OutlineLightingTypeURP", "_DirectIntensityOutline",
						"_IndirectIntensityOutline", "_UseOutlineColorMap", "_OutlineColorMap",
						"_UseOutlineVColorMask", "_OutlineMaskChannel", "_OutlineMaskInvert" },
				new[] { "BCM_OUTLINE", "TCP2_OUTLINE_TEXTURED_VERTEX", "TCP2_OUTLINE_TEXTURED_FRAGMENT",
						"TCP2_OUTLINE_CONST_SIZE", "TCP2_OUTLINE_MIN_SIZE", "TCP2_OUTLINE_MIN_MAX_SIZE",
						"TCP2_COLORS_AS_NORMALS", "TCP2_TANGENT_AS_NORMALS", "TCP2_UV1_AS_NORMALS", "TCP2_UV2_AS_NORMALS",
						"TCP2_UV3_AS_NORMALS", "TCP2_UV4_AS_NORMALS", "TCP2_UV_NORMALS_FULL", "TCP2_UV_NORMALS_ZW",
						"TCP2_OUTLINE_LIGHTING_MAIN", "TCP2_OUTLINE_LIGHTING_ALL", "TCP2_OUTLINE_LIGHTING_INDIRECT",
						"BCM_OUTLINE_COLORMAP", "BCM_OUTLINE_VCOLOR_MASK" }),

			// Declared only by BCM_Toony_shader_02. ApplySection skips properties the target does not
			// have, so listing them here is harmless on the other shaders.
			new ToonySection(PlanarShadow, "Planar Shadow",
				new[] { "_UsePlanarShadow", "_PlanarShadowColor", "_PlanarShadowStrength",
						"_PlanarShadowGroundMode", "_PlanarShadowGroundHeight", "_PlanarShadowBias",
						"_PlanarShadowBlob", "_PlanarShadowBlobSize", "_PlanarShadowBlobCenter",
						"_PlanarShadowBlobHeight", "_PlanarShadowRoundness", "_PlanarShadowSoftness",
						"_PlanarShadowGrow", "_PlanarShadowMaxOffset",
						"_PlanarShadowFadeMode", "_PlanarShadowFadeDistance", "_PlanarShadowFalloff",
						"_PlanarShadowHeightFade", "_UsePlanarShadowRamp", "_PlanarShadowRamp" },
				new[] { "BCM_PLANAR_SHADOW", "BCM_PLANAR_SHADOW_BLOB", "BCM_PLANAR_SHADOW_RAMP" }),

			new ToonySection(StudShadow, "Stud Contact Shadow",
				new[] { "_UseStudShadow", "_StudCount", "_StudPitch", "_StudGridCenter", "_StudRadius",
						"_StudEdgeSoftness", "_StudMaskShrink", "_StudFaceLevel",
						"_StudShadowHeight", "_StudShadowHeightScale", "_StudShadowStrength",
						"_StudShadowColor", "_StudShadowColorAmount", "_StudShadowSoftness",
						"_StudShadowGrow", "_StudShadowMaxOffset",
						"_StudRootAO", "_StudRootAOWidth" },
				new[] { "BCM_STUD_SHADOW" }),

			// Declared only by BCM_Toony_shader_03. The role drives every value under it, but the raw
			// render state is listed too so a hand-tuned material survives a copy/paste intact.
			new ToonySection(XRay, "X-Ray Overlay",
				new[] { "_XRayRole", "_UseXRayPush", "_XRayDepthPush",
						"_ZTest", "_StencilRef", "_StencilComp", "_StencilPassOp",
						"_StencilReadMask", "_StencilWriteMask" },
				new[] { "BCM_XRAY" }),

			new ToonySection(Options, "Options",
				new[] { "_CastShadows", "_ReceiveShadowsOff", "_ReceiveShadowStrength", "_ReceiveShadowSmoothing",
						"_ReceiveShadowColor" },
				new[] { "_RECEIVE_SHADOWS_OFF" }),
		};

		public static ToonySection Get(string id)
		{
			foreach (var s in All) if (s.Id == id) return s;
			return null;
		}
	}

	// ---- serialized payload -------------------------------------------------------------------
	// 'type' is a short string rather than a ShaderPropertyType so the JSON stays readable
	// and does not break if Unity renumbers the enum.

	[Serializable]
	public class ToonyPropVal
	{
		public string name;
		public string type = "f";           // f = float/range, c = color, v = vector, t = texture
		public float f;
		public Color c = Color.white;
		public Vector4 v;
		public string texGuid = "";
		public long texLocalId;
		public Vector4 texST = new Vector4(1f, 1f, 0f, 0f);   // scale.xy, offset.xy
	}

	[Serializable]
	public class ToonyKwVal
	{
		public string kw;
		public bool on;
	}

	[Serializable]
	public class ToonySectionVal
	{
		public string id;
		public string label;
		public List<ToonyPropVal> props = new List<ToonyPropVal>();
		public List<ToonyKwVal> kws = new List<ToonyKwVal>();
	}

	[Serializable]
	public class ToonyClipData
	{
		public string marker = ToonyClipboard.Marker;
		public string shader = "";
		public List<ToonySectionVal> sections = new List<ToonySectionVal>();
	}

	public static class ToonyClipboard
	{
		public const string Marker = "BCM_TOONY_CLIP_V1";
		const string SessionKey = "BCM.Toony.SectionClipboard";

		// Reading EditorGUIUtility.systemCopyBuffer hits the OS clipboard, and the inspector asks
		// "is section X in the clipboard?" once per section per repaint. So the payload is resolved
		// at most once per repaint (BeginFrame invalidates) and the JSON parse is memoized on top.
		static ToonyClipData _frameData;
		static bool _frameValid;
		static string _cachedRaw;
		static ToonyClipData _cachedData;

		// Call once at the top of OnGUI.
		public static void BeginFrame()
		{
			_frameValid = false;
		}

		// ---- copy ----

		public static void Copy(Material mat, ToonySection[] sections)
		{
			if (mat == null || mat.shader == null) return;

			var data = new ToonyClipData { shader = mat.shader.name };
			foreach (var s in sections)
			{
				if (s == null) continue;
				var sv = new ToonySectionVal { id = s.Id, label = s.Label };

				foreach (var name in s.Props)
				{
					if (!mat.HasProperty(name)) continue;
					int idx = mat.shader.FindPropertyIndex(name);
					if (idx < 0) continue;

					var pv = new ToonyPropVal { name = name };
					switch (mat.shader.GetPropertyType(idx))
					{
						case UnityEngine.Rendering.ShaderPropertyType.Color:
							pv.type = "c";
							pv.c = mat.GetColor(name);
							break;
						case UnityEngine.Rendering.ShaderPropertyType.Vector:
							pv.type = "v";
							pv.v = mat.GetVector(name);
							break;
						case UnityEngine.Rendering.ShaderPropertyType.Texture:
							pv.type = "t";
							var tex = mat.GetTexture(name);
							string guid;
							long localId;
							if (tex != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(tex, out guid, out localId))
							{
								pv.texGuid = guid;
								pv.texLocalId = localId;
							}
							var sc = mat.GetTextureScale(name);
							var of = mat.GetTextureOffset(name);
							pv.texST = new Vector4(sc.x, sc.y, of.x, of.y);
							break;
						default:   // Float, Range, Int
							pv.type = "f";
							pv.f = mat.GetFloat(name);
							break;
					}
					sv.props.Add(pv);
				}

				foreach (var kw in s.Keywords)
					sv.kws.Add(new ToonyKwVal { kw = kw, on = mat.IsKeywordEnabled(kw) });

				data.sections.Add(sv);
			}

			Write(data);
		}

		// ---- paste ----

		// Applies the clipboard to every target. sectionIds == null means "everything in the clipboard".
		// Returns the ids that were actually applied so the caller can run per-section fixups.
		public static string[] Paste(Material[] targets, string[] sectionIds)
		{
			var data = Read();
			if (data == null || targets == null) return new string[0];

			var applied = new List<string>();
			var unresolved = new List<string>();

			foreach (var sv in data.sections)
			{
				if (sectionIds != null && Array.IndexOf(sectionIds, sv.id) < 0) continue;
				foreach (var m in targets)
				{
					if (m == null) continue;
					ApplySection(m, sv, unresolved);
				}
				applied.Add(sv.id);
			}

			if (unresolved.Count > 0)
				Debug.LogWarning("[BCM_Toony] These textures were not found in this project and were left unchanged: " +
								 string.Join(", ", unresolved.ToArray()));

			return applied.ToArray();
		}

		static void ApplySection(Material m, ToonySectionVal sv, List<string> unresolved)
		{
			foreach (var p in sv.props)
			{
				if (!m.HasProperty(p.name)) continue;
				switch (p.type)
				{
					case "c":
						m.SetColor(p.name, p.c);
						break;
					case "v":
						m.SetVector(p.name, p.v);
						break;
					case "t":
						if (string.IsNullOrEmpty(p.texGuid))
						{
							m.SetTexture(p.name, null);   // source had no texture -> clear
						}
						else
						{
							var tex = LoadTex(p.texGuid, p.texLocalId);
							// Only overwrite when the reference resolved. Pasting a payload from
							// another project would otherwise wipe the target's texture.
							if (tex != null) m.SetTexture(p.name, tex);
							else if (!unresolved.Contains(p.name)) unresolved.Add(p.name);
						}
						m.SetTextureScale(p.name, new Vector2(p.texST.x, p.texST.y));
						m.SetTextureOffset(p.name, new Vector2(p.texST.z, p.texST.w));
						break;
					default:
						m.SetFloat(p.name, p.f);
						break;
				}
			}

			foreach (var k in sv.kws)
			{
				if (string.IsNullOrEmpty(k.kw)) continue;
				if (k.on) m.EnableKeyword(k.kw);
				else m.DisableKeyword(k.kw);
			}
		}

		static Texture LoadTex(string guid, long localId)
		{
			var path = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrEmpty(path)) return null;

			// Sub-assets share a GUID, so match on the local file id when there is more than one.
			foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
			{
				var t = o as Texture;
				if (t == null) continue;
				string g;
				long id;
				if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(t, out g, out id) && id == localId) return t;
			}
			return AssetDatabase.LoadAssetAtPath<Texture>(path);
		}

		// ---- queries used by the inspector ----

		public static bool HasAny()
		{
			var d = Read();
			return d != null && d.sections.Count > 0;
		}

		public static bool Has(string sectionId)
		{
			var d = Read();
			if (d == null) return false;
			foreach (var s in d.sections) if (s.id == sectionId) return true;
			return false;
		}

		// Short status line for the inspector's top bar.
		public static string Describe()
		{
			var d = Read();
			if (d == null || d.sections.Count == 0) return "Clipboard: empty";
			if (d.sections.Count == 1) return "Clipboard: " + d.sections[0].label;
			return "Clipboard: " + d.sections.Count + " sections";
		}

		public static void Clear()
		{
			SessionState.EraseString(SessionKey);
			if (Parse(EditorGUIUtility.systemCopyBuffer) != null) EditorGUIUtility.systemCopyBuffer = "";
			_cachedRaw = null;
			_cachedData = null;
			_frameData = null;
			_frameValid = true;
		}

		// ---- storage ----

		static void Write(ToonyClipData data)
		{
			// Compact, not pretty-printed: Copy All covers 117 properties and JsonUtility always
			// emits every field of ToonyPropVal, so pretty-printing doubled the payload for no gain.
			string json = JsonUtility.ToJson(data);
			SessionState.SetString(SessionKey, json);
			EditorGUIUtility.systemCopyBuffer = json;
			_cachedRaw = json;
			_cachedData = data;
			_frameData = data;
			_frameValid = true;
		}

		static ToonyClipData Read()
		{
			if (!_frameValid)
			{
				_frameData = ReadUncached();
				_frameValid = true;
			}
			return _frameData;
		}

		// The OS clipboard wins when it holds one of our payloads - that is what makes
		// "copy in project A, paste in project B" work. Otherwise fall back to SessionState,
		// which still holds our payload after the user copies unrelated text.
		static ToonyClipData ReadUncached()
		{
			var fromOs = Parse(EditorGUIUtility.systemCopyBuffer);
			if (fromOs != null) return fromOs;
			return Parse(SessionState.GetString(SessionKey, ""));
		}

		static ToonyClipData Parse(string json)
		{
			if (string.IsNullOrEmpty(json) || json.IndexOf(Marker, StringComparison.Ordinal) < 0) return null;
			if (json == _cachedRaw) return _cachedData;

			ToonyClipData d = null;
			try { d = JsonUtility.FromJson<ToonyClipData>(json); }
			catch { d = null; }
			if (d == null || d.marker != Marker || d.sections == null) d = null;

			_cachedRaw = json;
			_cachedData = d;
			return d;
		}
	}
}
