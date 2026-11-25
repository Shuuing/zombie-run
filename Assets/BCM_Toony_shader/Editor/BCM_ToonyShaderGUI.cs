// BCM_Toony_shader - standalone material inspector
// No external asset dependency. Lives in an Editor folder so it compiles into the default editor assembly.
// Referenced by the shader via:  CustomEditor "BCM.ToonyShaderGUI"

using System;
using UnityEditor;
using UnityEngine;

namespace BCM
{
	public class ToonyShaderGUI : ShaderGUI
	{
		// ---- keyword-enum group definitions (index -> keyword, "" = no keyword) ----
		static readonly string[] RampTypeLabels   = { "Default", "Crisp", "Bands", "Bands Crisp", "Texture", "Tri-Color" };
		static readonly string[] RampTypeKeywords = { "", "TCP2_RAMP_CRISP", "TCP2_RAMP_BANDS", "TCP2_RAMP_BANDS_CRISP", "TCP2_RAMPTEXT", "BCM_RAMP_TRICOLOR" };

		static readonly string[] SpecTypeLabels   = { "GGX", "Stylized", "Crisp" };
		static readonly string[] SpecTypeKeywords = { "", "TCP2_SPECULAR_STYLIZED", "TCP2_SPECULAR_CRISP" };

		static readonly string[] OutlineTexLabels   = { "Disabled", "Vertex Shader", "Pixel Shader" };
		static readonly string[] OutlineTexKeywords = { "", "TCP2_OUTLINE_TEXTURED_VERTEX", "TCP2_OUTLINE_TEXTURED_FRAGMENT" };

		static readonly string[] OutlinePixelLabels   = { "Disabled", "Constant", "Minimum", "Min Max" };
		static readonly string[] OutlinePixelKeywords = { "", "TCP2_OUTLINE_CONST_SIZE", "TCP2_OUTLINE_MIN_SIZE", "TCP2_OUTLINE_MIN_MAX_SIZE" };

		static readonly string[] NormalsSrcLabels   = { "Normal", "Vertex Colors", "Tangents", "UV1", "UV2", "UV3", "UV4" };
		static readonly string[] NormalsSrcKeywords = { "", "TCP2_COLORS_AS_NORMALS", "TCP2_TANGENT_AS_NORMALS", "TCP2_UV1_AS_NORMALS", "TCP2_UV2_AS_NORMALS", "TCP2_UV3_AS_NORMALS", "TCP2_UV4_AS_NORMALS" };

		static readonly string[] NormalsUVLabels   = { "Full XYZ", "Compressed XY", "Compressed ZW" };
		static readonly string[] NormalsUVKeywords = { "TCP2_UV_NORMALS_FULL", "", "TCP2_UV_NORMALS_ZW" };

		static readonly string[] OutlineLightLabels   = { "Disabled", "Main Directional Light", "All Lights (URP)", "Indirect Only" };
		static readonly string[] OutlineLightKeywords = { "", "TCP2_OUTLINE_LIGHTING_MAIN", "TCP2_OUTLINE_LIGHTING_ALL", "TCP2_OUTLINE_LIGHTING_INDIRECT" };

		static readonly string[] RenderingModeLabels = { "Opaque", "Cutout", "Fade", "Transparent" };
		static readonly string[] MobileModeLabels    = { "Disabled", "Mobile" };

		MaterialEditor _editor;
		MaterialProperty[] _props;

		// A paste rewrites the material directly, which would leave the MaterialProperty array we are
		// currently drawing from stale. So the button only records the action and we run it once all
		// sections have been drawn.
		Action _deferred;

		public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
		{
			_editor = materialEditor;
			_props = properties;
			var mat = materialEditor.target as Material;

			EditorGUIUtility.labelWidth = 0f;
			ToonyClipboard.BeginFrame();

			DrawClipboardBar();
			DrawTopSettings(mat);
			Boxed(() => DrawTransparency(mat));
			Boxed(() => DrawBase(mat));
			Boxed(() => DrawToggleSection("Multiply", "_UseMultiply", "BCM_MULTIPLY", DrawMultiply, mat, ToonySections.Multiply));
			Boxed(() => DrawRamp(mat));
			Boxed(() => DrawToggleSection("Normal Mapping", "_UseNormalMap", "_NORMALMAP", DrawNormal, mat, ToonySections.Normal));
			Boxed(() => DrawToggleSection("Specular", "_UseSpecular", "TCP2_SPECULAR", DrawSpecular, mat, ToonySections.Specular));
			Boxed(() => DrawToggleSection("Emission", "_UseEmission", "_EMISSION", DrawEmission, mat, ToonySections.Emission));
			Boxed(() => DrawToggleSection("Rim Lighting", "_UseRim", "TCP2_RIM_LIGHTING", DrawRim, mat, ToonySections.Rim));
			Boxed(() => DrawToggleSection("MatCap", "_UseMatCap", "TCP2_MATCAP", DrawMatCap, mat, ToonySections.MatCap));
			Boxed(() => DrawGI(mat));
			Boxed(() => DrawOutline(mat));
			// Only BCM_Toony_shader_02 declares these; on every other shader they draw nothing.
			Boxed(() => DrawPlanarShadow(mat));
			Boxed(() => DrawToggleSection("Stud Contact Shadow", "_UseStudShadow", "BCM_STUD_SHADOW", DrawStudShadow, mat, ToonySections.StudShadow));
			// BCM_Toony_shader_03 only. Tested outside Boxed(), which draws its frame whether or not the
			// body puts anything in it - the earlier shaders would otherwise show an empty panel.
			if (mat != null && mat.HasProperty("_XRayRole")) Boxed(() => DrawXRay(mat));
			Boxed(() => DrawOptions(mat));

			EditorGUILayout.Space();
			materialEditor.RenderQueueField();
			materialEditor.EnableInstancingField();
			materialEditor.DoubleSidedGIField();

			if (_deferred != null)
			{
				var run = _deferred;
				_deferred = null;
				run();
			}
		}

		// ==========================================================================================
		// Section copy / paste
		// ==========================================================================================

		void DrawClipboardBar()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField(ToonyClipboard.Describe(), EditorStyles.miniLabel);

				using (new EditorGUI.DisabledScope(_editor.targets.Length > 1))
					if (GUILayout.Button(new GUIContent("Copy All",
							"Copy every section of this material. Disabled while several materials are selected."),
						EditorStyles.miniButtonLeft, GUILayout.Width(62)))
						ToonyClipboard.Copy(_editor.target as Material, ToonySections.All);

				using (new EditorGUI.DisabledScope(!ToonyClipboard.HasAny()))
					if (GUILayout.Button(new GUIContent("Paste All",
							"Apply every section held in the clipboard to all selected materials."),
						EditorStyles.miniButtonMid, GUILayout.Width(62)))
						_deferred = () => PasteSections(null);

				using (new EditorGUI.DisabledScope(!ToonyClipboard.HasAny()))
					if (GUILayout.Button(new GUIContent("×", "Clear the clipboard."),
						EditorStyles.miniButtonRight, GUILayout.Width(22)))
						ToonyClipboard.Clear();
			}
		}

		// Copy/paste pair drawn on the right-hand side of a section header.
		void SectionButtons(string sectionId)
		{
			var section = ToonySections.Get(sectionId);
			if (section == null) return;

			using (new EditorGUI.DisabledScope(_editor.targets.Length > 1))
				if (GUILayout.Button(new GUIContent("Copy",
						"Copy " + section.Label + " (values + keywords). Disabled while several materials are selected."),
					EditorStyles.miniButtonLeft, GUILayout.Width(42)))
					ToonyClipboard.Copy(_editor.target as Material, new[] { section });

			using (new EditorGUI.DisabledScope(!ToonyClipboard.Has(sectionId)))
				if (GUILayout.Button(new GUIContent("Paste",
						"Apply the copied " + section.Label + " values to all selected materials."),
					EditorStyles.miniButtonRight, GUILayout.Width(42)))
				{
					var ids = new[] { sectionId };
					_deferred = () => PasteSections(ids);
				}
		}

		// sectionIds == null pastes everything the clipboard holds.
		void PasteSections(string[] sectionIds)
		{
			var targets = Array.ConvertAll(_editor.targets, t => t as Material);
			Undo.RecordObjects(_editor.targets, "Paste BCM_Toony Settings");

			foreach (var id in ToonyClipboard.Paste(targets, sectionIds))
				PostApply(id, targets);

			foreach (var m in targets) if (m != null) EditorUtility.SetDirty(m);
			_editor.Repaint();
		}

		// Some sections carry state that does not live in a shader property: the render queue and
		// RenderType tag for transparency, the outline pass toggle, and the planar shadow's own render
		// queue bump. Re-derive those after a paste.
		void PostApply(string sectionId, Material[] targets)
		{
			switch (sectionId)
			{
				case ToonySections.PlanarShadow:
					// _UsePlanarShadow pastes as a plain float, but the queue bump it implies does not
					// travel with it. Without this, pasting an enabled planar shadow leaves the material
					// on the Geometry queue and the pass draws before the ground it blends onto.
					foreach (var m in targets)
					{
						if (m == null || !m.HasProperty("_UsePlanarShadow")) continue;
						bool psOn = m.GetFloat("_UsePlanarShadow") > 0.5f;
						m.renderQueue = psOn ? kPlanarShadowQueue : -1;
					}
					break;

				case ToonySections.Transparency:
					foreach (var m in targets)
					{
						if (m == null || !m.HasProperty("_RenderingMode")) continue;
						ApplyRenderingModeTo(m, Mathf.RoundToInt(m.GetFloat("_RenderingMode")));
					}
					break;

				case ToonySections.XRay:
					// The stencil state pastes as plain floats, but the Overlay role's queue bump and its
					// Depth Write override do not travel with them. Without this, a pasted overlay lands on
					// the Geometry queue and is drawn before the hair it is supposed to cover, which looks
					// exactly like the feature not working at all.
					foreach (var m in targets)
					{
						if (m == null || !m.HasProperty("_XRayRole")) continue;
						int role = Mathf.RoundToInt(m.GetFloat("_XRayRole"));
						// Re-deriving from the role would also undo any hand-tuning of the raw state, so
						// only the two things that live outside the property block are restored here.
						if (role == 1)
						{
							SetFloatIf(m, "_ZWrite", 0f);
							m.renderQueue = kXRayOverlayQueue;
						}
						else if (role == 2)
						{
							m.renderQueue = kXRayMaskQueue;
						}
						else if (m.HasProperty("_RenderingMode"))
						{
							ApplyRenderingModeTo(m, Mathf.RoundToInt(m.GetFloat("_RenderingMode")));
							if (m.HasProperty("_UsePlanarShadow") && m.GetFloat("_UsePlanarShadow") > 0.5f)
								m.renderQueue = kPlanarShadowQueue;
						}
					}
					break;

				case ToonySections.Outline:
					foreach (var m in targets)
					{
						if (m == null) continue;
						bool on = m.HasProperty("_UseOutline") && m.GetFloat("_UseOutline") > 0.5f;
						SetKeyword(m, "BCM_OUTLINE", on);
						m.SetShaderPassEnabled("Outline", on);
					}
					break;
			}
		}

		// ==========================================================================================
		// Sections
		// ==========================================================================================

		void DrawTopSettings(Material mat)
		{
			var mobile = FindProp("_UseMobileMode");
			if (mobile != null)
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUI.BeginChangeCheck();
					int idx = Mathf.RoundToInt(mobile.floatValue) == 1 ? 1 : 0;
					idx = EditorGUILayout.Popup("Mobile Mode", idx, MobileModeLabels);
					if (EditorGUI.EndChangeCheck())
					{
						mobile.floatValue = idx;
						SetKeyword(mat, "TCP2_MOBILE", idx == 1);
					}
					SectionButtons(ToonySections.Mobile);
				}
			}
		}

		void DrawTransparency(Material mat)
		{
			Header("Transparency", ToonySections.Transparency);
			var rmode = FindProp("_RenderingMode");
			if (rmode != null)
			{
				EditorGUI.BeginChangeCheck();
				int idx = Mathf.Clamp(Mathf.RoundToInt(rmode.floatValue), 0, RenderingModeLabels.Length - 1);
				idx = EditorGUILayout.Popup("Rendering Mode", idx, RenderingModeLabels);
				if (EditorGUI.EndChangeCheck())
				{
					rmode.floatValue = idx;
					ApplyRenderingMode(mat, idx);
				}
			}
			ShaderPropIfExists("_Cull", "Render Face");
			ShaderPropIfExists("_ZWrite", "Depth Write");
			var alphaClip = FindProp("_UseAlphaTest");
			if (alphaClip != null)
			{
				_editor.ShaderProperty(alphaClip, "Alpha Clipping");
				if (alphaClip.floatValue > 0.5f) ShaderPropIfExists("_Cutoff", "Alpha Cutoff");
			}
		}

		void DrawBase(Material mat)
		{
			Header("Base", ToonySections.Base);
			var baseCol = FindProp("_BaseColor");
			var baseMap = FindProp("_BaseMap");
			if (baseCol != null && baseMap != null)
				_editor.TexturePropertySingleLine(new GUIContent("Albedo"), baseMap, baseCol);
			_editor.TextureScaleOffsetProperty(baseMap);
			ColorWithMap("Highlight Color", "_HColorMap", "_HColor");
			ColorWithMap("Shadow Color", "_SColorMap", "_SColor");
			ShaderPropIfExists("_ShadowColorLightAtten", "Main Light affects Shadow Color");

			var shadowTex = FindProp("_UseShadowTexture");
			if (shadowTex != null)
			{
				_editor.ShaderProperty(shadowTex, "Enable Shadow Albedo Texture");
				if (shadowTex.floatValue > 0.5f)
				{
					EditorGUI.indentLevel++;
					ShaderPropTexIfExists("_ShadowBaseMap", "Shadow Albedo");
					EditorGUI.indentLevel--;
				}
			}
		}

		// BCM: masked color multiply over the albedo. Header/keyword handled by DrawToggleSection.
		void DrawMultiply(Material mat)
		{
			var map = FindProp("_MultiplyMap");
			if (map != null)
			{
				_editor.TexturePropertySingleLine(new GUIContent("Mask"), map);
				ShaderPropIfExists("_MultiplyMaskChannel", "Mask Channel");
			}
			ShaderPropIfExists("_MultiplyColor", "Color");
			ShaderPropIfExists("_MultiplyStrength", "Strength");
			EditorGUILayout.HelpBox("Mask defaults to white (whole surface). Color multiplies the albedo where the mask channel = 1; white color = no change.", MessageType.None);
		}

		void DrawRamp(Material mat)
		{
			Header("Ramp Shading", ToonySections.Ramp);
			int rampType = KeywordEnumPopup(mat, "Ramp Type", "_RampType", RampTypeLabels, RampTypeKeywords, RampTypeGroup());

			bool isTexture  = rampType == 4;
			bool isTriColor = rampType == 5;
			bool isBands    = rampType == 2 || rampType == 3;
			bool isCrispy   = rampType == 1 || rampType == 3;

			if (isTriColor)
			{
				DrawTriColorBands(mat);
			}
			else if (isTexture)
			{
				EditorGUI.indentLevel++;
				var ramp = FindProp("_Ramp");
				if (ramp != null) _editor.TexturePropertySingleLine(new GUIContent("Ramp Texture (RGB)"), ramp);
				ShaderPropIfExists("_RampScale", "Scale");
				ShaderPropIfExists("_RampOffset", "Offset");
				bool useColors = mat.IsKeywordEnabled("BCM_RAMP_COLOR");
				EditorGUI.BeginChangeCheck();
				useColors = EditorGUILayout.ToggleLeft("Use Ramp Texture Colors Directly", useColors);
				if (EditorGUI.EndChangeCheck())
					foreach (Material m in _editor.targets) SetKeyword(m, "BCM_RAMP_COLOR", useColors);
				EditorGUI.indentLevel--;
			}
			else
			{
				ShaderPropIfExists("_RampThreshold", "Threshold");
				if (!isCrispy) ShaderPropIfExists("_RampSmoothing", "Smoothing");
				if (isBands)
				{
					ShaderPropIfExists("_RampBands", "Bands Count");
					if (rampType == 2) ShaderPropIfExists("_RampBandsSmoothing", "Bands Smoothing");
				}
			}
		}

		const int MaxMiddleColors = 6;

		// Multi-color ramp: Shadow color (from Base) -> a dynamic list of middle colors -> Highlight
		// color (from Base). Each band has a % width. Use +/- to add or remove middle colors.
		void DrawTriColorBands(Material mat)
		{
			EditorGUILayout.HelpBox("Ordered from shadow to light: Shadow (from Base) → middle colors → Highlight (from Base). Each % is the band width from the shadow side; values are relative. Use + to add a middle color.", MessageType.None);

			var countProp = FindProp("_MidCount");
			int count = countProp != null ? Mathf.Clamp(Mathf.RoundToInt(countProp.floatValue), 0, MaxMiddleColors) : 1;

			ShaderPropIfExists("_HighPercent", "Highlight %");

			for (int i = 0; i < count; i++)
			{
				if (i == 0) ColorWithMap("Middle 1", "_MidColor0Map", "_MidColor0");
				else ShaderPropIfExists("_MidColor" + i, "Middle " + (i + 1));
				var pct = FindProp("_MidPct" + i);
				if (pct != null)
				{
					EditorGUI.indentLevel++;
					_editor.ShaderProperty(pct, "Middle " + (i + 1) + " %");
					EditorGUI.indentLevel--;
				}
			}

			ShaderPropIfExists("_ShadowPercent", "Shadow %");

			// + / - buttons
			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();
				using (new EditorGUI.DisabledScope(count >= MaxMiddleColors))
					if (GUILayout.Button("+ Add Middle Color", GUILayout.Width(160))) SetMidCount(count + 1);
				using (new EditorGUI.DisabledScope(count <= 0))
					if (GUILayout.Button("−", GUILayout.Width(28))) SetMidCount(count - 1);
			}

			// Actual normalized split
			float total = GetPct("_ShadowPercent") + GetPct("_HighPercent");
			for (int i = 0; i < count; i++) total += GetPct("_MidPct" + i);
			if (total > 0.0001f)
			{
				var parts = new System.Collections.Generic.List<string>();
				parts.Add($"Shadow {Mathf.RoundToInt(GetPct("_ShadowPercent") / total * 100)}%");
				for (int i = 0; i < count; i++) parts.Add($"Mid{i + 1} {Mathf.RoundToInt(GetPct("_MidPct" + i) / total * 100)}%");
				parts.Add($"Highlight {Mathf.RoundToInt(GetPct("_HighPercent") / total * 100)}%");
				EditorGUILayout.LabelField("Actual split", string.Join("  •  ", parts), EditorStyles.miniLabel);
			}

			ShaderPropIfExists("_RampSmoothing", "Edge Smoothing");
		}

		float GetPct(string name)
		{
			var p = FindProp(name);
			return p != null ? p.floatValue : 0f;
		}

		void SetMidCount(int c)
		{
			c = Mathf.Clamp(c, 0, MaxMiddleColors);
			foreach (Material m in _editor.targets)
				if (m.HasProperty("_MidCount")) m.SetFloat("_MidCount", c);
			var prop = FindProp("_MidCount");
			if (prop != null) prop.floatValue = c;
		}

		void DrawNormal(Material mat)
		{
			var bump = FindProp("_BumpMap");
			var scale = FindProp("_BumpScale");
			if (bump != null) _editor.TexturePropertySingleLine(new GUIContent("Normal Map"), bump, scale);
			_editor.TextureScaleOffsetProperty(bump);
		}

		void DrawSpecular(Material mat)
		{
			KeywordEnumPopup(mat, "Type", "_SpecularType", SpecTypeLabels, SpecTypeKeywords, SpecTypeGroup());
			bool ggx = !mat.IsKeywordEnabled("TCP2_SPECULAR_STYLIZED") && !mat.IsKeywordEnabled("TCP2_SPECULAR_CRISP");
			bool crisp = mat.IsKeywordEnabled("TCP2_SPECULAR_CRISP");
			bool stylized = mat.IsKeywordEnabled("TCP2_SPECULAR_STYLIZED");
			ShaderPropIfExists("_SpecularColor", "Color");
			ShaderPropIfExists("_SpecularStrength", "Strength");

			// Albedo Tint - lets a multi-coloured albedo drive the highlight colour per pixel
			var albedoTint = FindProp("_SpecularAlbedoTint");
			if (albedoTint != null)
			{
				_editor.ShaderProperty(albedoTint, "Albedo Tint");
				if (albedoTint.floatValue > 0f)
				{
					EditorGUI.indentLevel++;
					ShaderPropIfExists("_SpecularPreserveBrightness", "Preserve Brightness");
					EditorGUILayout.HelpBox("The highlight takes its colour from the albedo, so a red decal on a " +
						"blue surface gets a red highlight. Preserve Brightness keeps only the hue, so dark colours " +
						"still get a full-strength highlight (pure black falls back to white).", MessageType.None);
					EditorGUI.indentLevel--;
				}
			}
			if (ggx) ShaderPropIfExists("_SpecularRoughness", "Roughness");
			if (crisp || stylized) ShaderPropIfExists("_SpecularToonSize", "Size");
			if (stylized) ShaderPropIfExists("_SpecularToonSmoothness", "Smoothing");

			var mapType = FindProp("_SpecularMapType");
			if (mapType != null)
			{
				_editor.ShaderProperty(mapType, "Specular Map");
				if (mapType.floatValue >= 2f)
				{
					EditorGUI.indentLevel++;
					ShaderPropTexIfExists("_SpecGlossMap", "Specular Texture");
					EditorGUI.indentLevel--;
				}
			}
		}

		void DrawEmission(Material mat)
		{
			var chan = FindProp("_EmissionChannel");
			if (chan != null) _editor.ShaderProperty(chan, "Texture Channel");
			if (chan == null || chan.floatValue < 5f)
				ShaderPropTexIfExists("_EmissionMap", "Texture");
			ShaderPropIfExists("_EmissionColor", "Color");
		}

		void DrawRim(Material mat)
		{
			ShaderPropIfExists("_RimColor", "Color");
			ShaderPropIfExists("_RimMin", "Min");
			ShaderPropIfExists("_RimMax", "Max");
			ShaderPropIfExists("_UseRimLightMask", "Light-based Mask");

			// Directional Rim (Light Direction)
			var rimDir = FindProp("_UseRimDir");
			if (rimDir != null)
			{
				_editor.ShaderProperty(rimDir, "Light Direction"); // [Toggle(BCM_RIM_LIGHTDIR)] sets the keyword
				if (rimDir.floatValue > 0.5f)
				{
					EditorGUI.indentLevel++;
					ShaderPropIfExists("_RimLightDir", "Light Direction Rim Light");
					ShaderPropIfExists("_RimColor2", "Color");
					ShaderPropIfExists("_RimMin2", "Min");
					ShaderPropIfExists("_RimMax2", "Max");
					EditorGUI.indentLevel--;
				}
			}
		}

		void DrawMatCap(Material mat)
		{
			var channel = FindProp("_UseMatCapChannel");
			bool channelOn = channel != null && channel.floatValue > 0.5f;
			bool mobile = mat != null && mat.IsKeywordEnabled("TCP2_MOBILE");

			// The channel path overrides all four base settings. Grey them out rather than leaving them
			// editable, otherwise "I changed Color and nothing happened" is invisible in the inspector.
			EditorGUI.BeginDisabledGroup(channelOn && !mobile);
			ShaderPropIfExists("_MatCapType", "MatCap Blending");
			ShaderPropTexIfExists("_MatCapTex", "Texture");
			ShaderPropIfExists("_MatCapColor", "Color");
			ShaderPropIfExists("_MatCapPower", "Power");
			EditorGUI.EndDisabledGroup();

			// The mask is shared between both paths, so it is drawn wherever it is currently reachable:
			// up here in single-matcap mode, at the bottom of the channel block otherwise.
			if (!channelOn) DrawMatCapMask();

			if (channel == null) return;

			EditorGUILayout.Space();
			_editor.ShaderProperty(channel, "Channel Texture");
			if (!channelOn) return;

			EditorGUI.indentLevel++;
			ShaderPropTexIfExists("_MatCapChannelTex", "Channel Texture");
			DrawChannelTexValidation();

			if (mobile)
			{
				EditorGUILayout.HelpBox(
					"Mobile 모드에서는 Channel Texture가 무시되고 위의 기본 MatCap이 사용됩니다.",
					MessageType.Warning);
			}

			DrawMatCapChannel("R", "Channel - R");
			DrawMatCapChannel("G", "Channel - G");
			DrawMatCapChannel("B", "Channel - B");

			// A is opt-in because a channel map with no alpha channel reads as alpha = 1 everywhere.
			var useA = FindProp("_UseMatCapChannelA");
			if (useA != null)
			{
				EditorGUILayout.Space();
				_editor.ShaderProperty(useA, "Enable A Channel");
				if (useA.floatValue > 0.5f)
				{
					if (!ChannelTexHasAlpha())
					{
						EditorGUILayout.HelpBox(
							"Channel Texture에 알파 채널이 없습니다. 알파가 전체 1로 읽혀서 A MatCap이 모델 전체에 " +
							"적용되고 R/G/B 가중치가 깎입니다. 알파 채널을 추가하거나 이 옵션을 끄세요.",
							MessageType.Error);
					}
					DrawMatCapChannel("A", "Channel - A");
				}
			}

			EditorGUILayout.Space();
			DrawMatCapMask();
			EditorGUI.indentLevel--;
		}

		void DrawMatCapChannel(string suffix, string title)
		{
			if (FindProp("_MatCapTex" + suffix) == null) return;
			EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
			ShaderPropIfExists("_MatCapType" + suffix, "MatCap Blending");
			ShaderPropTexIfExists("_MatCapTex" + suffix, "Texture");
			ShaderPropIfExists("_MatCapColor" + suffix, "Color");
			ShaderPropIfExists("_MatCapPower" + suffix, "Power");
		}

		void DrawMatCapMask()
		{
			var mask = FindProp("_UseMatCapMask");
			if (mask == null) return;
			_editor.ShaderProperty(mask, "Enable Mask");
			if (mask.floatValue > 0.5f)
			{
				EditorGUI.indentLevel++;
				ShaderPropTexIfExists("_MatCapMask", "Mask Texture");
				ShaderPropIfExists("_MatCapMaskChannel", "Texture Channel");
				EditorGUI.indentLevel--;
			}
		}

		// Per-platform tabs can override the format with an explicit compressed one, which also hides the
		// Compression dropdown in the importer UI. Those overrides have to be validated separately.
		static readonly string[] ValidatedPlatforms = { "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps" };

		static bool IsUncompressedFormat(TextureImporterFormat f)
		{
			return f == TextureImporterFormat.RGBA32 || f == TextureImporterFormat.ARGB32 ||
			       f == TextureImporterFormat.RGB24  || f == TextureImporterFormat.RGBA64 ||
			       f == TextureImporterFormat.RGBAHalf;
		}

		TextureImporter ChannelTexImporter()
		{
			var prop = FindProp("_MatCapChannelTex");
			var tex = prop != null ? prop.textureValue as Texture2D : null;
			if (tex == null) return null;
			return AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
		}

		bool ChannelTexHasAlpha()
		{
			var importer = ChannelTexImporter();
			if (importer == null) return true; // cannot tell - do not cry wolf
			return importer.DoesSourceTextureHaveAlpha() &&
			       importer.alphaSource == TextureImporterAlphaSource.FromInput;
		}

		// The channel map carries data, not colour, but Unity imports it as colour by default. Every one
		// of these defaults silently corrupts the region weights, so surface them with a one-click fix.
		void DrawChannelTexValidation()
		{
			var prop = FindProp("_MatCapChannelTex");
			if (prop != null && prop.textureValue == null)
			{
				EditorGUILayout.HelpBox(
					"Channel Texture가 비어 있습니다. 영역이 지정되지 않아 MatCap이 표시되지 않습니다.",
					MessageType.Warning);
				return;
			}

			var importer = ChannelTexImporter();
			if (importer == null) return; // built-in or procedural texture, nothing to validate

			var issues = new System.Collections.Generic.List<string>();
			if (importer.sRGBTexture)
				issues.Add("sRGB (Color Texture) 끄기 - 켜져 있으면 채널값이 감마 변환되어 영역 경계가 밀립니다.");
			if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
				issues.Add("Alpha Source = Input Texture Alpha - 아니면 A 채널 영역이 사라집니다.");
			if (importer.alphaIsTransparency)
				issues.Add("Alpha Is Transparency 끄기 - 알파 0 영역의 RGB를 침식시켜 다른 채널을 오염시킵니다.");
			if (importer.textureCompression != TextureImporterCompression.Uncompressed)
				issues.Add("Default 탭 Compression = None - 블록 압축은 채널 간 상관을 깨서 경계에 없던 채널값을 만듭니다.");

			foreach (var platform in ValidatedPlatforms)
			{
				var ps = importer.GetPlatformTextureSettings(platform);
				if (!ps.overridden) continue;
				bool bad = ps.format == TextureImporterFormat.Automatic
					? ps.textureCompression != TextureImporterCompression.Uncompressed
					: !IsUncompressedFormat(ps.format);
				if (bad)
					issues.Add(platform + " 탭이 Override 되어 Format = " + ps.format +
					           " 압축을 강제합니다 -> RGBA 32 bit 로 바꿔야 합니다. " +
					           "(Format이 Automatic이 아니면 Compression 드롭다운은 숨겨집니다)");
			}

			if (issues.Count == 0) return;

			EditorGUILayout.HelpBox(
				"Channel Texture 임포트 설정을 고쳐야 합니다:\n· " + string.Join("\n· ", issues.ToArray()),
				MessageType.Warning);
			if (GUILayout.Button("Fix Import Settings"))
			{
				importer.sRGBTexture = false;
				importer.alphaSource = TextureImporterAlphaSource.FromInput;
				importer.alphaIsTransparency = false;
				importer.textureCompression = TextureImporterCompression.Uncompressed;
				foreach (var platform in ValidatedPlatforms)
				{
					var ps = importer.GetPlatformTextureSettings(platform);
					if (!ps.overridden) continue;
					ps.format = TextureImporterFormat.RGBA32;
					ps.textureCompression = TextureImporterCompression.Uncompressed;
					ps.crunchedCompression = false;
					importer.SetPlatformTextureSettings(ps);
				}
				importer.SaveAndReimport();
			}
		}

		void DrawGI(Material mat)
		{
			Header("Global Illumination", ToonySections.GI);
			EditorGUILayout.LabelField("Direct Light", EditorStyles.miniBoldLabel);
			ShaderPropIfExists("_LightReceived", "Light Received (%)");
			ShaderPropIfExists("_LightReceivedColor", "Light Tint");
			EditorGUILayout.LabelField("Indirect Diffuse", EditorStyles.miniBoldLabel);
			var indirect = FindProp("_IndirectIntensity");
			if (indirect != null)
			{
				_editor.ShaderProperty(indirect, "Strength");
				if (indirect.floatValue > 0f) ShaderPropIfExists("_SingleIndirectColor", "Single Indirect Color");
			}

			DrawToggleSection("Indirect Specular (Environment Reflections)", "_UseReflections", "TCP2_REFLECTIONS", (m) =>
			{
				ShaderPropIfExists("_ReflectionColor", "Color");
				ShaderPropIfExists("_ReflectionSmoothness", "Smoothness");
				var refMap = FindProp("_ReflectionMapType");
				if (refMap != null)
				{
					_editor.ShaderProperty(refMap, "Reflection Map");
					int t = Mathf.RoundToInt(refMap.floatValue);
					if (t != 0 && t != 1 && t != 6)
					{
						EditorGUI.indentLevel++;
						ShaderPropTexIfExists("_ReflectionTex", "Reflection Texture");
						EditorGUI.indentLevel--;
					}
				}
				var fresnel = FindProp("_UseFresnelReflections");
				if (fresnel != null)
				{
					_editor.ShaderProperty(fresnel, "Fresnel Reflections");
					if (fresnel.floatValue > 0.5f)
					{
						ShaderPropIfExists("_FresnelMin", "Fresnel Min");
						ShaderPropIfExists("_FresnelMax", "Fresnel Max");
					}
				}
			}, mat, ToonySections.Reflections);

			// Occlusion (labels match the reference image; uses the channel-mask + remap path)
			var occUse = FindProp("_UseOcclusion");
			if (occUse != null)
			{
				Header("Occlusion", ToonySections.Occlusion);
				EditorGUI.showMixedValue = occUse.hasMixedValue;
				EditorGUI.BeginChangeCheck();
				bool occOn = occUse.floatValue > 0.5f;
				occOn = EditorGUILayout.ToggleLeft("_UseOcclusion (on/off Occlusion completely)", occOn);
				if (EditorGUI.EndChangeCheck())
				{
					occUse.floatValue = occOn ? 1f : 0f;
					foreach (Material mm in _editor.targets)
					{
						SetKeyword(mm, "TCP2_OCCLUSION", occOn);
						SetKeyword(mm, "BCM_OCCLUSION_ADV", occOn);
					}
				}
				EditorGUI.showMixedValue = false;

				if (occOn)
				{
					// keep the channel-mask + remap path active whenever occlusion is on
					foreach (Material mm in _editor.targets)
						if (!mm.IsKeywordEnabled("BCM_OCCLUSION_ADV")) SetKeyword(mm, "BCM_OCCLUSION_ADV", true);

					EditorGUI.indentLevel++;
					ShaderPropIfExists("_OcclusionDirect", "Affect Direct Light");
					ShaderPropIfExists("_OcclusionStrength", "_OcclusionStrength");
					ShaderPropTexIfExists("_OcclusionMap", "_OcclusionMap");
					ShaderPropIfExists("_OcclusionMapChannelMask", "_OcclusionMapChannelMask");
					ShaderPropIfExists("_OcclusionRemapStart", "_OcclusionRemapStart");
					ShaderPropIfExists("_OcclusionRemapEnd", "_OcclusionRemapEnd");
					EditorGUI.indentLevel--;
				}
			}
		}

		void DrawOutline(Material mat)
		{
			var use = FindProp("_UseOutline");
			if (use == null) return;

			Header("Outline", ToonySections.Outline);
			EditorGUI.BeginChangeCheck();
			bool on = use.floatValue > 0.5f;
			on = EditorGUILayout.ToggleLeft("Enable Outline", on, EditorStyles.boldLabel);
			if (EditorGUI.EndChangeCheck())
			{
				use.floatValue = on ? 1f : 0f;
				foreach (Material m in _editor.targets)
				{
					SetKeyword(m, "BCM_OUTLINE", on);
					m.SetShaderPassEnabled("Outline", on); // URP outline pass (LightMode "Outline")
				}
			}
			else
			{
				// keep keyword in sync in case it drifted
				SetKeyword(mat, "BCM_OUTLINE", on);
			}

			if (!on) return;

			EditorGUI.indentLevel++;
			ShaderPropIfExists("_OutlineColor", "Color");
			int texType = KeywordEnumPopup(mat, "Texture", "_OutlineTextureType", OutlineTexLabels, OutlineTexKeywords, OutlineTexGroup());
			if (texType >= 1) ShaderPropIfExists("_OutlineTextureLOD", "Texture LOD");
			ShaderPropIfExists("_OutlineWidth", "Width");
			int pixelType = KeywordEnumPopup(mat, "Pixel Size", "_OutlinePixelSizeType", OutlinePixelLabels, OutlinePixelKeywords, OutlinePixelGroup());
			if (pixelType == 2 || pixelType == 3) ShaderPropIfExists("_OutlineMinWidth", "Minimum Width (Pixels)");
			if (pixelType == 3) ShaderPropIfExists("_OutlineMaxWidth", "Maximum Width (Pixels)");
			int nrmSrc = KeywordEnumPopup(mat, "Outline Normals Source", "_NormalsSource", NormalsSrcLabels, NormalsSrcKeywords, NormalsSrcGroup());
			if (nrmSrc >= 3) KeywordEnumPopup(mat, "UV Data Type", "_NormalsUVType", NormalsUVLabels, NormalsUVKeywords, NormalsUVGroup());
			int light = KeywordEnumPopup(mat, "Lighting", "_OutlineLightingType", OutlineLightLabels, OutlineLightKeywords, OutlineLightGroup());
			if (light >= 1)
			{
				if (light == 1 || light == 2) ShaderPropIfExists("_DirectIntensityOutline", "Direct Strength");
				ShaderPropIfExists("_IndirectIntensityOutline", "Indirect Strength");
			}

			// Outline Color Map
			var colMap = FindProp("_UseOutlineColorMap");
			if (colMap != null)
			{
				_editor.ShaderProperty(colMap, "Outline Color Map"); // [Toggle(BCM_OUTLINE_COLORMAP)] sets the keyword
				if (colMap.floatValue > 0.5f)
				{
					EditorGUI.indentLevel++;
					var cm = FindProp("_OutlineColorMap");
					if (cm != null)
					{
						_editor.TexturePropertySingleLine(new GUIContent("Color Map"), cm);
						_editor.TextureScaleOffsetProperty(cm);
					}
					EditorGUI.indentLevel--;
				}
			}

			// Vertex Color Mask - per-vertex outline on/off, painted in the DCC tool
			var vcMask = FindProp("_UseOutlineVColorMask");
			if (vcMask != null)
			{
				_editor.ShaderProperty(vcMask, "Vertex Color Mask"); // [Toggle(BCM_OUTLINE_VCOLOR_MASK)] sets the keyword
				if (vcMask.floatValue > 0.5f)
				{
					EditorGUI.indentLevel++;
					ShaderPropIfExists("_OutlineMaskChannel", "Mask Channel");
					ShaderPropIfExists("_OutlineMaskInvert", "Invert (white = no outline)");
					EditorGUILayout.HelpBox("The channel value is the outline width multiplier: vertices painted " +
						"black get no outline, white get the full width. Check the paint with the " +
						"BCM/Debug/Vertex Color shader (Outline Mask Preview) and verify the data with " +
						"Tools > BCM > Inspect Vertex Colors.", MessageType.None);
					EditorGUI.indentLevel--;
				}
			}
			EditorGUI.indentLevel--;
		}

		// ==========================================================================================
		// X-Ray Overlay  (BCM_Toony_shader_03 only - returns immediately on the earlier shaders)
		// ==========================================================================================
		//
		// "Bangs show-through": an eyebrow / eye strip that keeps drawing after the hair physically in
		// front of it has already been drawn, so an anime character keeps a readable expression under a
		// heavy fringe. The effect is impossible to reach from shading - those fragments are killed by the
		// depth test before any lighting runs - so the whole thing is render state, and the four roles
		// below are presets over it. Keeping it out of the keyword system is deliberate: the shader is
		// close to Unity's 64-keywords-per-shader ceiling, and ZTest/stencil can be driven straight from
		// material properties at no keyword cost at all.
		//
		// Mask bit 5 (32): URP reserves the low nibble of the stencil buffer for its own material-type and
		// light masks and leaves bits 4-7 to the user. The planar shadow pass in this shader owns bit 7
		// (128), so 16 / 32 / 64 are the ones still free.
		const int kXRayMaskBit      = 32;
		// Inside the opaque range (<= 2500), so the overlay still draws in the opaque pass and is not
		// subject to transparent sorting - it only has to land after the hair, which sits on Geometry
		// (2000) or AlphaTest (2450).
		const int kXRayOverlayQueue = 2460;
		// The Mask role has the opposite requirement, and it is not optional. A stencil write only lands
		// where the DEPTH test passed as well, so a face sharing the hair's queue never stamps the bit
		// under the fringe: opaque geometry in one queue is sorted front-to-back, the hair is nearer, the
		// hair goes first, and the face then loses the depth test exactly where the mask was needed.
		// Measured, not assumed - at a shared queue the overlay came back fully hidden.
		const int kXRayMaskQueue    = 1990;

		static readonly string[] XRayRoleLabels =
		{
			"Off",
			"Overlay - eyebrow / eye",
			"Mask - face",
			"Blocker - hand, prop",
		};

		void DrawXRay(Material mat)
		{
			var roleProp = FindProp("_XRayRole");
			if (roleProp == null) return;

			Header("X-Ray Overlay", ToonySections.XRay);

			int role = Mathf.Clamp(Mathf.RoundToInt(roleProp.floatValue), 0, XRayRoleLabels.Length - 1);
			EditorGUI.BeginChangeCheck();
			EditorGUI.showMixedValue = roleProp.hasMixedValue;
			role = EditorGUILayout.Popup(new GUIContent("Role",
				"What this material is in the show-through setup. Overlay on its own already produces the "
				+ "effect; Mask and Blocker only start to matter once something other than hair can get "
				+ "between the camera and the face."), role, XRayRoleLabels);
			EditorGUI.showMixedValue = false;
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObjects(_editor.targets, "Set X-Ray Role");
				roleProp.floatValue = role;
				foreach (Material m in _editor.targets) ApplyXRayRole(m, role);
			}

			EditorGUI.indentLevel++;

			switch (role)
			{
				case 1:
					EditorGUILayout.HelpBox(
						"Depth test Always, no depth write, render queue " + kXRayOverlayQueue + ". Put this on "
						+ "the eyebrow mesh only - it has to be its own submesh with its own material, because "
						+ "the whole material draws on top. An eyebrow painted into the face texture cannot be "
						+ "separated this way.", MessageType.None);

					// Whether the overlay is confined to the face mask is stored in the render state
					// itself, so it needs no property of its own.
					var comp = FindProp("_StencilComp");
					bool masked = comp != null &&
						Mathf.RoundToInt(comp.floatValue) == (int)UnityEngine.Rendering.CompareFunction.Equal;

					EditorGUI.BeginChangeCheck();
					bool wantMasked = EditorGUILayout.Toggle(new GUIContent("Restrict To Face Mask",
						"On: the overlay can only appear where a 'Mask - face' material stamped bit "
						+ kXRayMaskBit + ", so it stops punching through hands and props. Needs at least one "
						+ "Mask material in the scene - with none, nothing stamps the bit and the overlay "
						+ "disappears entirely."), masked);
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObjects(_editor.targets, "Set X-Ray Mask Restriction");
						foreach (Material m in _editor.targets) ApplyXRayOverlayMask(m, wantMasked);
					}

					if (mat != null && mat.HasProperty("_ZWrite") && mat.GetFloat("_ZWrite") > 0.5f)
						EditorGUILayout.HelpBox(
							"Depth Write is on. The overlay draws at a depth that lost the real depth test, so "
							+ "writing it corrupts the depth buffer for everything after it. Turn Depth Write "
							+ "off in Transparency.", MessageType.Warning);

					if (mat != null && mat.renderQueue < kXRayOverlayQueue)
						EditorGUILayout.HelpBox(
							"Render Queue is " + mat.renderQueue + ". The overlay paints over what is already in "
							+ "the buffer, so it has to be drawn after the hair - " + kXRayOverlayQueue
							+ " is the expected value. Hair on a Fade/Transparent material (queue 3000) draws "
							+ "later still and will cover the overlay; give that hair a Cutout material instead.",
							MessageType.Warning);
					break;

				case 2:
					EditorGUILayout.HelpBox(
						"Stamps bit " + kXRayMaskBit + " over the visible face, and moves this material to render "
						+ "queue " + kXRayMaskQueue + " so it is drawn BEFORE the hair. That order is the whole "
						+ "trick: a stencil write only lands where the depth test passed too, so a face sharing "
						+ "the hair's queue is drawn second, loses the depth test under the fringe and stamps "
						+ "nothing exactly where the mask was needed. Drawn first it stamps the full silhouette, "
						+ "and hair keeps the bit rather than clearing it.", MessageType.None);

					if (mat != null && mat.renderQueue != kXRayMaskQueue)
						EditorGUILayout.HelpBox(
							"Render Queue is " + mat.renderQueue + ", but the mask has to be drawn before the hair - "
							+ kXRayMaskQueue + " is the expected value. At the hair's own queue the overlay "
							+ "disappears completely.", MessageType.Warning);
					break;

				case 3:
					EditorGUILayout.HelpBox(
						"Clears bit " + kXRayMaskBit + " wherever this material is in front. The Mask role alone "
						+ "nearly covers this - a hand drawn before the face blocks the face's stencil write - "
						+ "but opaque draw order inside one queue is sorted per renderer and is not a "
						+ "guarantee. Use this on anything that has to reliably hide the face.",
						MessageType.None);
					break;
			}

			// Independent of the roles: works with an ordinary LEqual depth test, so it needs no stencil
			// setup and no queue change - useful for thin bangs, and the standard fix for an eyebrow
			// modelled flush on the face z-fighting with it.
			EditorGUILayout.Space();
			var pushToggle = FindProp("_UseXRayPush");
			if (pushToggle != null)
			{
				EditorGUI.BeginChangeCheck();
				bool push = pushToggle.floatValue > 0.5f;
				push = EditorGUILayout.Toggle(new GUIContent("Depth Push",
					"Pulls the rasterised position toward the camera. Independent of the role above and "
					+ "usable on its own."), push);
				if (EditorGUI.EndChangeCheck())
				{
					pushToggle.floatValue = push ? 1f : 0f;
					foreach (Material m in _editor.targets) SetKeyword(m, "BCM_XRAY", push);
				}

				if (pushToggle.floatValue > 0.5f)
				{
					EditorGUI.indentLevel++;
					ShaderPropIfExists("_XRayDepthPush", "Push Toward Camera");
					EditorGUILayout.HelpBox(
						"World units. Only the rasterised position moves - lighting, shadows and fog are still "
						+ "evaluated where the mesh actually is. Push further than the hair is thick and the "
						+ "overlay starts poking out through the nose instead.", MessageType.None);
					EditorGUI.indentLevel--;
				}
			}

			// The role writes all of these; they are here for hand-tuning and for reading back what a
			// role actually did.
			EditorGUILayout.Space();
			_xrayStateOpen = EditorGUILayout.Foldout(_xrayStateOpen, "Raw Render State", true);
			if (_xrayStateOpen)
			{
				EditorGUI.indentLevel++;
				ShaderPropIfExists("_ZTest", "Depth Test");
				ShaderPropIfExists("_StencilComp", "Stencil Comparison");
				ShaderPropIfExists("_StencilPassOp", "Stencil Pass Op");
				ShaderPropIfExists("_StencilRef", "Stencil Reference");
				ShaderPropIfExists("_StencilReadMask", "Stencil Read Mask");
				ShaderPropIfExists("_StencilWriteMask", "Stencil Write Mask");
				EditorGUI.indentLevel--;
			}

			EditorGUI.indentLevel--;
		}

		bool _xrayStateOpen;

		static void ApplyXRayRole(Material m, int role)
		{
			if (m == null || !m.HasProperty("_XRayRole")) return;

			const int kAlways  = (int)UnityEngine.Rendering.CompareFunction.Always;
			const int kLEqual  = (int)UnityEngine.Rendering.CompareFunction.LessEqual;
			const int kKeep    = (int)UnityEngine.Rendering.StencilOp.Keep;
			const int kReplace = (int)UnityEngine.Rendering.StencilOp.Replace;
			const int kZero    = (int)UnityEngine.Rendering.StencilOp.Zero;

			SetFloatIf(m, "_XRayRole", role);
			SetFloatIf(m, "_StencilComp", kAlways);
			SetFloatIf(m, "_StencilReadMask", 255f);

			if (role == 1)
			{
				// Overlay. Never writes the stencil buffer (write mask 0) and never writes depth: it draws
				// at a depth that lost the real test, so writing it would corrupt the depth buffer for
				// everything after it - including any depth-based post effect.
				SetFloatIf(m, "_ZTest", kAlways);
				SetFloatIf(m, "_ZWrite", 0f);
				SetFloatIf(m, "_StencilRef", 0f);
				SetFloatIf(m, "_StencilWriteMask", 0f);
				SetFloatIf(m, "_StencilPassOp", kKeep);
				m.renderQueue = kXRayOverlayQueue;
				return;
			}

			SetFloatIf(m, "_ZTest", kLEqual);

			switch (role)
			{
				case 2: // Mask - stamp the bit
					SetFloatIf(m, "_StencilRef", kXRayMaskBit);
					SetFloatIf(m, "_StencilWriteMask", kXRayMaskBit);
					SetFloatIf(m, "_StencilPassOp", kReplace);
					break;

				case 3: // Blocker - clear the bit (Zero is masked by the write mask, so only bit 5 moves)
					SetFloatIf(m, "_StencilRef", 0f);
					SetFloatIf(m, "_StencilWriteMask", kXRayMaskBit);
					SetFloatIf(m, "_StencilPassOp", kZero);
					break;

				default: // Off - the state the shader ships with, i.e. never touch the stencil buffer
					SetFloatIf(m, "_StencilRef", 0f);
					SetFloatIf(m, "_StencilWriteMask", 255f);
					SetFloatIf(m, "_StencilPassOp", kKeep);
					break;
			}

			// Leaving the Overlay role has to give the queue and Depth Write back, and only the rendering
			// mode knows what they should be - a plain reset to the shader's queue would drop a Cutout
			// material from 2450 to 2000. The planar shadow owns the queue when it is on.
			if (m.HasProperty("_RenderingMode"))
				ApplyRenderingModeTo(m, Mathf.RoundToInt(m.GetFloat("_RenderingMode")));
			if (m.HasProperty("_UsePlanarShadow") && m.GetFloat("_UsePlanarShadow") > 0.5f)
				m.renderQueue = kPlanarShadowQueue;

			// Applied last: the mask has to beat the hair to the depth buffer, so its queue is not
			// negotiable and overrides whatever the rendering mode just picked.
			if (role == 2) m.renderQueue = kXRayMaskQueue;
		}

		static void ApplyXRayOverlayMask(Material m, bool masked)
		{
			if (m == null || !m.HasProperty("_StencilComp")) return;

			SetFloatIf(m, "_StencilComp", masked
				? (int)UnityEngine.Rendering.CompareFunction.Equal
				: (int)UnityEngine.Rendering.CompareFunction.Always);
			SetFloatIf(m, "_StencilRef", masked ? kXRayMaskBit : 0f);
			SetFloatIf(m, "_StencilReadMask", masked ? kXRayMaskBit : 255f);
		}

		void DrawOptions(Material mat)
		{
			Header("Options", ToonySections.Options);
			ShaderPropIfExists("_CastShadows", "Cast Shadows");
			var recv = FindProp("_ReceiveShadowsOff");
			if (recv != null)
			{
				_editor.ShaderProperty(recv, "Receive Shadows");
				// _ReceiveShadowsOff uses [ToggleOff]: value 1 = receiving shadows ON.
				if (recv.floatValue > 0.5f)
				{
					EditorGUI.indentLevel++;
					ShaderPropIfExists("_ReceiveShadowStrength", "Shadow Strength");
					ShaderPropIfExists("_ReceiveShadowSmoothing", "Shadow Smoothing (edge)");
					// Black = plain multiply, i.e. the behaviour from before this property existed.
					ShaderPropIfExists("_ReceiveShadowColor", "Shadow Color");
					EditorGUI.indentLevel--;
				}
			}

			// BCM: Wind Sway. Drawn under Options rather than as its own ToonySections entry so the
			// section enum (and everyone's saved foldout state) stays untouched.
			var wind = FindProp("_UseWindSway");
			if (wind != null)
			{
				EditorGUILayout.Space(4f);
				_editor.ShaderProperty(wind, new GUIContent("Wind Sway · 바람 흔들림 (정점)",
					"밑동은 고정되고 위로 갈수록 흔들리는 정점 변형. 나무·풀처럼 땅에 박힌 프롭 전용."));
				if (wind.floatValue > 0.5f)
				{
					EditorGUI.indentLevel++;
					WindProp("_WindStrength", "Strength · 진폭 (m)",
						"꼭대기가 좌우로 움직이는 최대 거리(m). 나무 0.10~0.15 권장. 0.3 을 넘기면 고무처럼 휜다.");
					WindProp("_WindSpeed", "Speed · 속도",
						"흔들리는 빠르기. 1.0 이 산들바람 정도.");
					// The mask reaches full sway at this world height above Base Y. Set it to the
					// prop's real height, or the top will only get part of the movement.
					WindProp("_WindHeight", "Full Height · 최대 높이 (m)",
						"이 높이에서 흔들림이 최대가 된다. 프롭의 실제 높이를 넣을 것. " +
						"너무 크게 주면 꼭대기도 잘 안 흔들리고, 작게 주면 중간부터 위가 통째로 밀린다. " +
						"FieldBuilder 로 놓는 프롭은 메시에서 자동 계산되므로 손댈 필요 없다.");
					WindProp("_WindBaseY", "Base Y · 고정 높이 (월드)",
						"밑동이 고정되는 월드 Y 좌표. 지면이 y=0 이면 0. " +
						"바위 위처럼 띄워 놓은 프롭은 이 값을 그 밑면 높이로 올려야 밑동이 고정된다.");
					WindProp("_WindTurbulence", "Turbulence · 잔떨림",
						"빠른 2차 파동의 비율. 0 이면 사인 하나라 기계적으로 왕복한다.");
					WindProp("_WindDir", "Direction · 바람 방향 (XZ)",
						"바람이 부는 방향. X 와 Z 만 쓰고 Y 는 무시한다.");
					WindProp("_WindPhaseScale", "Phase Variation · 위상 차",
						"프롭마다 흔들리는 타이밍이 어긋나는 정도. 0 이면 전부 똑같이 동시에 흔들린다.");
					EditorGUI.indentLevel--;
				}
			}

			// BCM: See Through. Values are global (ProtoSeeThrough); the material only opts in.
			var seeThrough = FindProp("_UseSeeThrough");
			if (seeThrough != null)
			{
				EditorGUILayout.Space(4f);
				_editor.ShaderProperty(seeThrough, new GUIContent("See Through · 캐릭터 가림 투시",
					"이 물체가 카메라와 캐릭터 사이에 오면 캐릭터 주변만 디더로 비친다(완전히 사라지지 않음). " +
					"반지름·최소 불투명도는 ProtoSeeThrough 컴포넌트 값(전역)을 따른다. 건물·컨테이너처럼 큰 가림막에만 켤 것."));
			}

			// BCM: Ground Splat. Same placement rule as Wind Sway (keeps the section enum untouched).
			var splat = FindProp("_UseGroundSplat");
			if (splat != null)
			{
				EditorGUILayout.Space(4f);
				_editor.ShaderProperty(splat, new GUIContent("Ground Splat · 지면 텍스처 혼합",
					"Albedo 가 바닥층, 정점 컬러 R/G/B 가 1/2/3층의 덮임 정도(0.5 = 경계). " +
					"정점 컬러를 굽는 메시(ProtoFieldStreamer 지면) 전용 — 일반 메시에 켜면 전부 흰 정점이라 3층만 보인다."));
				if (splat.floatValue > 0.5f)
				{
					EditorGUI.indentLevel++;
					ShaderPropTexIfExists("_SplatTex1", "Layer 1 (R)");
					ShaderPropTexIfExists("_SplatTex2", "Layer 2 (G)");
					ShaderPropTexIfExists("_SplatTex3", "Layer 3 (B)");
					ShaderPropTexIfExists("_SplatTex4", "Grass (A) · 풀");
					WindProp("_SplatTile4", "Grass Tile Size · 풀 반복 주기 (m)", "풀 텍스처 한 장이 덮는 크기(m).");
					WindProp("_SplatTint4", "Grass Tint · 풀 색 보정", "풀 텍스처에 곱하는 색. 흰색이면 원본 그대로.");
					WindProp("_SplatStrength", "Layer Max Opacity · 층별 최대 불투명도",
						"x=1층(R) y=2층(G) z=3층(B) w=풀(A). 1 이면 꽉 덮고, 낮추면 가장 진한 곳도 바탕이 비쳐 얼룩이 아니라 번진 그라데이션이 된다.");
					WindProp("_SplatTiles", "Tile Size · 반복 주기 (m)",
						"x=Albedo y=1층 z=2층 w=3층. 층마다 다르게 둬야 알갱이 크기가 달라 '다른 땅'으로 읽힌다. " +
						"UV 는 월드 XZ 라 Albedo 의 Tiling/Offset 은 무시된다.");
					WindProp("_SplatSharpness", "Edge Softness · 경계 부드러움",
						"작을수록 경계가 딱 끊긴다. 0.05~0.12 권장.");
					WindProp("_SplatHeightBlend", "Height Blend · 알갱이 스밈",
						"위층 텍스처의 밝은 알갱이가 경계 밖으로 먼저 올라오는 정도. 0 이면 매끈한 그라데이션.");
					WindProp("_SplatNoise", "Edge Noise · 경계 흔들림",
						"월드 노이즈로 경계선을 들쭉날쭉하게 민다. 0 이면 정점 격자대로 매끈한 곡선.");
					WindProp("_SplatNoiseScale", "Edge Noise Scale · 흔들림 잔 정도",
						"클수록 자잘하게, 작을수록 크게 출렁인다.");
					EditorGUI.indentLevel--;
				}
			}
		}

		// ==========================================================================================
		// Helpers
		// ==========================================================================================

		// per-property keyword groups (the full set of keywords toggled together for that enum)
		string[] RampTypeGroup()     => new[] { "TCP2_RAMPTEXT", "TCP2_RAMP_CRISP", "TCP2_RAMP_BANDS", "TCP2_RAMP_BANDS_CRISP", "BCM_RAMP_TRICOLOR" };
		string[] SpecTypeGroup()     => new[] { "TCP2_SPECULAR_STYLIZED", "TCP2_SPECULAR_CRISP" };
		string[] OutlineTexGroup()   => new[] { "TCP2_OUTLINE_TEXTURED_VERTEX", "TCP2_OUTLINE_TEXTURED_FRAGMENT" };
		string[] OutlinePixelGroup() => new[] { "TCP2_OUTLINE_CONST_SIZE", "TCP2_OUTLINE_MIN_SIZE", "TCP2_OUTLINE_MIN_MAX_SIZE" };
		string[] NormalsSrcGroup()   => new[] { "TCP2_COLORS_AS_NORMALS", "TCP2_TANGENT_AS_NORMALS", "TCP2_UV1_AS_NORMALS", "TCP2_UV2_AS_NORMALS", "TCP2_UV3_AS_NORMALS", "TCP2_UV4_AS_NORMALS" };
		string[] NormalsUVGroup()    => new[] { "TCP2_UV_NORMALS_FULL", "TCP2_UV_NORMALS_ZW" };
		string[] OutlineLightGroup() => new[] { "TCP2_OUTLINE_LIGHTING_MAIN", "TCP2_OUTLINE_LIGHTING_ALL", "TCP2_OUTLINE_LIGHTING_INDIRECT" };

		int KeywordEnumPopup(Material mat, string label, string propName, string[] labels, string[] keywords, string[] group)
		{
			var prop = FindProp(propName);
			if (prop == null) return 0;
			EditorGUI.BeginChangeCheck();
			EditorGUI.showMixedValue = prop.hasMixedValue;
			int idx = Mathf.Clamp(Mathf.RoundToInt(prop.floatValue), 0, labels.Length - 1);
			idx = EditorGUILayout.Popup(label, idx, labels);
			EditorGUI.showMixedValue = false;
			if (EditorGUI.EndChangeCheck())
			{
				prop.floatValue = idx;
				foreach (Material m in _editor.targets) ApplyKeywordGroup(m, keywords, group, idx);
			}
			return idx;
		}

		static void ApplyKeywordGroup(Material m, string[] keywords, string[] group, int index)
		{
			// turn every keyword of this group off, then enable the one for the selected index
			foreach (var kw in group) m.DisableKeyword(kw);
			if (index >= 0 && index < keywords.Length && !string.IsNullOrEmpty(keywords[index]))
				m.EnableKeyword(keywords[index]);
		}

		// Draws a single bold toggle that acts as the section header (no duplicate title line).
		void DrawToggleSection(string header, string toggleProp, string keyword, Action<Material> content, Material mat, string sectionId = null)
		{
			var prop = FindProp(toggleProp);
			if (prop == null) return;

			EditorGUILayout.Space();
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUI.showMixedValue = prop.hasMixedValue;
				EditorGUI.BeginChangeCheck();
				bool on = prop.floatValue > 0.5f;
				on = EditorGUILayout.ToggleLeft(header, on, EditorStyles.boldLabel);
				if (EditorGUI.EndChangeCheck())
				{
					prop.floatValue = on ? 1f : 0f;
					foreach (Material m in _editor.targets) SetKeyword(m, keyword, on);
				}
				EditorGUI.showMixedValue = false;

				if (sectionId != null) SectionButtons(sectionId);
			}

			if (prop.floatValue > 0.5f)
			{
				EditorGUI.indentLevel++;
				content(mat);
				EditorGUI.indentLevel--;
			}
		}

		// BCM_02 only (the property does not exist on BCM_Toony_shader, so this returns immediately).
		// Mirrors DrawToggleSection but also owns the render queue: the planar shadow pass alpha-blends
		// onto the colour buffer, so the ground must already be drawn. Opaque objects sharing a queue are
		// sorted front-to-back, which does not guarantee that, hence the bump off the Geometry queue.
		const int kPlanarShadowQueue = 2450;

		void DrawPlanarShadow(Material mat)
		{
			var prop = FindProp("_UsePlanarShadow");
			if (prop == null) return;

			EditorGUILayout.Space();
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUI.showMixedValue = prop.hasMixedValue;
				EditorGUI.BeginChangeCheck();
				bool on = prop.floatValue > 0.5f;
				on = EditorGUILayout.ToggleLeft("Planar Shadow", on, EditorStyles.boldLabel);
				if (EditorGUI.EndChangeCheck())
				{
					prop.floatValue = on ? 1f : 0f;
					foreach (Material m in _editor.targets)
					{
						SetKeyword(m, "BCM_PLANAR_SHADOW", on);
						m.renderQueue = on ? kPlanarShadowQueue : -1; // -1 = take the queue from the shader
					}
				}
				EditorGUI.showMixedValue = false;

				SectionButtons(ToonySections.PlanarShadow);
			}

			if (prop.floatValue <= 0.5f) return;

			EditorGUI.indentLevel++;

			ShaderPropIfExists("_PlanarShadowColor", "Color");
			ShaderPropIfExists("_PlanarShadowStrength", "Strength");
			ShaderPropIfExists("_PlanarShadowGroundMode", "Ground Mode");
			var groundMode = FindProp("_PlanarShadowGroundMode");
			var groundHeight = FindProp("_PlanarShadowGroundHeight");
			bool worldY = groundMode != null && groundMode.floatValue < 0.5f;
			ShaderPropIfExists("_PlanarShadowGroundHeight", worldY ? "Ground Height (World Y)" : "Pivot Offset");

			// Projecting onto a fixed plane that the caster does not stand on is the easy way to get a
			// shadow floating in mid-air, so call it out against the actual selected object.
			if (worldY && groundHeight != null)
			{
				var go = Selection.activeGameObject;
				var rend = go != null ? go.GetComponent<Renderer>() : null;
				if (rend != null && System.Array.IndexOf(rend.sharedMaterials, mat) >= 0)
				{
					float pivotY = rend.transform.position.y;
					if (pivotY < groundHeight.floatValue - 0.001f)
						EditorGUILayout.HelpBox(
							"'" + go.name + "' sits at Y " + pivotY.ToString("0.###") + ", below the ground plane at Y "
							+ groundHeight.floatValue.ToString("0.###") + ", so the shadow is culled. Lower Ground Height "
							+ "to the floor, or switch to Object Pivot.", MessageType.Warning);
				}
			}

			ShaderPropIfExists("_PlanarShadowBias", "Bias");

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Shape", EditorStyles.miniBoldLabel);
			var blobProp = FindProp("_PlanarShadowBlob");
			bool blob = blobProp != null && blobProp.floatValue > 0.5f;
			if (blobProp != null)
			{
				EditorGUI.BeginChangeCheck();
				bool b = EditorGUILayout.Toggle(new GUIContent("Analytic Blob Shape",
					"On: a rounded-box distance field swept along the light, with a soft parameterised edge. "
					+ "Off: project the real mesh, which is accurate in outline but always hard-edged."), blob);
				if (EditorGUI.EndChangeCheck())
				{
					blobProp.floatValue = b ? 1f : 0f;
					foreach (Material m in _editor.targets) SetKeyword(m, "BCM_PLANAR_SHADOW_BLOB", b);
					blob = b;
				}
			}

			if (blob)
			{
				ShaderPropIfExists("_PlanarShadowBlobSize", "Half Extents (X, Z)");
				ShaderPropIfExists("_PlanarShadowBlobCenter", "Center Offset (X, Z)");
				ShaderPropIfExists("_PlanarShadowBlobHeight", "Sweep Height");
				ShaderPropIfExists("_PlanarShadowRoundness", "Roundness");
				ShaderPropIfExists("_PlanarShadowSoftness", "Edge Softness");
				ShaderPropIfExists("_PlanarShadowGrow", "Grow");
				ShaderPropIfExists("_PlanarShadowMaxOffset", "Max Offset");

				var go = Selection.activeGameObject;
				var rend = go != null ? go.GetComponent<Renderer>() : null;
				using (new EditorGUI.DisabledScope(rend == null))
					if (GUILayout.Button(new GUIContent("Fit To Mesh Bounds",
						"Sizes the blob from the selected object's mesh bounds. Object space, so it keeps "
						+ "working when the object is scaled or yawed.")))
						FitBlobToBounds(rend);
			}
			else
			{
				ShaderPropIfExists("_PlanarShadowFadeMode", "Fade Mode");
				ShaderPropIfExists("_PlanarShadowFadeDistance", "Fade Distance");
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Gradient", EditorStyles.miniBoldLabel);

			var useRamp = FindProp("_UsePlanarShadowRamp");
			bool ramp = useRamp != null && useRamp.floatValue > 0.5f;
			if (!ramp) ShaderPropIfExists("_PlanarShadowFalloff", "Fade Falloff");

			if (useRamp != null)
			{
				EditorGUI.BeginChangeCheck();
				bool r = EditorGUILayout.Toggle("Use Gradient Ramp", ramp);
				if (EditorGUI.EndChangeCheck())
				{
					useRamp.floatValue = r ? 1f : 0f;
					foreach (Material m in _editor.targets) SetKeyword(m, "BCM_PLANAR_SHADOW_RAMP", r);
				}

				if (r)
				{
					ShaderPropTexIfExists("_PlanarShadowRamp", "Gradient Ramp");
					EditorGUILayout.HelpBox(
						"Alpha is read left (contact) to right (outer edge) and REPLACES the procedural "
						+ "falloff, so the ramp has to reach alpha 0 on the right or the shadow keeps a hard edge.",
						MessageType.None);
				}
			}

			EditorGUILayout.Space();
			ShaderPropIfExists("_PlanarShadowHeightFade", "Height Fade Distance");

			if (mat != null && mat.renderQueue != kPlanarShadowQueue)
				EditorGUILayout.HelpBox(
					"Render Queue is " + mat.renderQueue + ". The planar shadow blends onto the ground and has "
					+ "to be drawn after it - " + kPlanarShadowQueue + " is the expected value.",
					MessageType.Warning);

			EditorGUI.indentLevel--;
		}

		// BCM_02 only. LEGO stud shadows painted on the object's own top face - unrelated to the planar
		// (ground) shadow above, and the two can be on at the same time.
		void DrawStudShadow(Material mat)
		{
			EditorGUILayout.LabelField("Stud Grid (object space)", EditorStyles.miniBoldLabel);
			ShaderPropIfExists("_StudCount", "Stud Count (X, Z)");
			ShaderPropIfExists("_StudPitch", "Stud Pitch");
			ShaderPropIfExists("_StudGridCenter", "Grid Center (X, Z)");
			ShaderPropIfExists("_StudRadius", "Stud Radius");
			ShaderPropIfExists("_StudEdgeSoftness", "Stud Edge Softness");
			ShaderPropIfExists("_StudMaskShrink", "Stud Mask Shrink");
			ShaderPropIfExists("_StudShadowHeight", "Stud Height");
			ShaderPropIfExists("_StudFaceLevel", "Receiving Face Level");
			{
				var go = Selection.activeGameObject;
				var rend = go != null ? go.GetComponent<Renderer>() : null;
				using (new EditorGUI.DisabledScope(rend == null))
					if (GUILayout.Button(new GUIContent("Read Face Level From Mesh",
						"Measures the selected object's highest point along its up axis and subtracts the stud "
						+ "height, which lands on the face the studs stand on. Needed because studs face up too "
						+ "and would otherwise get the shading painted on their own rims.")))
						FitStudFaceLevel(rend);
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Contact Shadow (cell units)", EditorStyles.miniBoldLabel);
			ShaderPropIfExists("_StudShadowStrength", "Strength");
			ShaderPropIfExists("_StudShadowColor", "Shadow Color");
			ShaderPropIfExists("_StudShadowColorAmount", "Shadow Color Amount");
			var studColAmt = FindProp("_StudShadowColorAmount");
			if (studColAmt != null && studColAmt.floatValue <= 0.001f)
				EditorGUILayout.HelpBox(
					"Shadow Color Amount is 0, so the crescent uses the toon Shadow Color and the colour above "
					+ "does nothing. Raise it to 1 to make a fully shadowed pixel exactly that colour.",
					MessageType.Info);
			ShaderPropIfExists("_StudShadowSoftness", "Softness");
			ShaderPropIfExists("_StudShadowGrow", "Grow");
			ShaderPropIfExists("_StudShadowMaxOffset", "Max Offset");
			ShaderPropIfExists("_StudShadowHeightScale", "Shadow Height Scale");

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Root Occlusion (rotation invariant)", EditorStyles.miniBoldLabel);
			ShaderPropIfExists("_StudRootAO", "Strength");
			ShaderPropIfExists("_StudRootAOWidth", "Width");

			EditorGUILayout.HelpBox(
				"The grid is measured on the RECEIVING mesh's own object space. If the studs are a separate "
				+ "object, these values still describe where the studs sit relative to this mesh's pivot.",
				MessageType.None);
		}

		// Finds the height of the widest upward-facing plateau along the object's UP axis - that is the
		// face the studs stand on. "highest point minus one stud height" was tried first and is wrong for
		// a body-only mesh: there is no stud in it, so subtracting lands below the face and the feature
		// silently switches itself off. Measuring the plateau works for both a body+studs mesh (the caps
		// are narrow, the face is wide) and a body-only one (the face IS the top).
		// The up axis comes from the transform, never assumed to be +Y - these meshes import flipped.
		void FitStudFaceLevel(Renderer rend)
		{
			var smr = rend as SkinnedMeshRenderer;
			var mf = rend.GetComponent<MeshFilter>();
			Mesh mesh = smr != null ? smr.sharedMesh : (mf != null ? mf.sharedMesh : null);
			var level = FindProp("_StudFaceLevel");
			if (mesh == null || level == null) return;

			var studHProp = FindProp("_StudShadowHeight");
			float studH = Mathf.Max(studHProp != null ? studHProp.floatValue : 0.1f, 1e-4f);

			Vector3 upOS = rend.transform.InverseTransformDirection(Vector3.up).normalized;
			var verts = mesh.vertices;
			var norms = mesh.normals;
			bool haveNormals = norms.Length == verts.Length;

			// Widest upward-facing coverage found within a band around a given height.
			Func<float, float> coverageAt = h =>
			{
				float r = 0f;
				for (int i = 0; i < verts.Length; i++)
				{
					if (haveNormals && Vector3.Dot(norms[i].normalized, upOS) < 0.5f) continue;
					float hh = Vector3.Dot(verts[i], upOS);
					if (Mathf.Abs(hh - h) > studH * 0.25f) continue;
					r = Mathf.Max(r, (verts[i] - upOS * hh).magnitude);
				}
				return r;
			};

			float top = float.NegativeInfinity;
			for (int i = 0; i < verts.Length; i++)
			{
				if (haveNormals && Vector3.Dot(norms[i].normalized, upOS) < 0.5f) continue;
				top = Mathf.Max(top, Vector3.Dot(verts[i], upOS));
			}
			if (float.IsNegativeInfinity(top)) return;

			// Does this mesh carry its own studs? If a stud height below the top there is a WIDER
			// upward-facing surface, the top is the stud caps and the face is that lower one. Otherwise
			// the top already is the face. Comparing the two beats "top minus stud height", which is
			// wrong for a body-only mesh and quietly switches the whole feature off.
			bool studsInMesh = coverageAt(top - studH) > coverageAt(top) * 1.15f;
			level.floatValue = studsInMesh ? top - studH : top;
		}

		// Writes the mesh's object-space bounds straight into the field: extents as the half size, the
		// bounds centre as the offset. Both are needed - a mesh whose geometry sits off to one side of
		// its pivot would otherwise get its blob somewhere it does not belong.
		void FitBlobToBounds(Renderer rend)
		{
			var smr = rend as SkinnedMeshRenderer;
			var mf = rend.GetComponent<MeshFilter>();
			Mesh mesh = smr != null ? smr.sharedMesh : (mf != null ? mf.sharedMesh : null);
			if (mesh == null) return;

			var b = mesh.bounds;
			var size = FindProp("_PlanarShadowBlobSize");
			var centre = FindProp("_PlanarShadowBlobCenter");
			var height = FindProp("_PlanarShadowBlobHeight");
			if (size != null) size.vectorValue = new Vector4(b.extents.x, b.extents.z, 0f, 0f);
			if (centre != null) centre.vectorValue = new Vector4(b.center.x, b.center.z, 0f, 0f);
			if (height != null) height.floatValue = Mathf.Max(b.size.y, 0.01f);
		}

		void Header(string title, string sectionId = null, bool space = true)
		{
			if (space) EditorGUILayout.Space();
			if (sectionId == null)
			{
				EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
				return;
			}
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
				SectionButtons(sectionId);
			}
		}

		// Wraps a section in a bordered box so it is visually separated from the next one.
		// Purely cosmetic - does not touch any shader property or keyword.
		void Boxed(Action body)
		{
			EditorGUILayout.Space(2f);
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				body();
			}
		}

		// Color swatch + texture map on one line (like UTS 1st/2nd Shade Map). Map multiplies the color.
		void ColorWithMap(string label, string mapName, string colorName)
		{
			var map = FindProp(mapName);
			var col = FindProp(colorName);
			if (map != null && col != null) _editor.TexturePropertySingleLine(new GUIContent(label), map, col);
			else if (col != null) _editor.ShaderProperty(col, label);
		}

		void ShaderPropIfExists(string name, string label)
		{
			var p = FindProp(name);
			if (p != null) _editor.ShaderProperty(p, label);
		}

		/// BCM: Wind Sway only. Same as ShaderPropIfExists but takes a tooltip, so the label can stay
		/// short enough not to clip while the full Korean explanation lives on hover.
		void WindProp(string name, string label, string tooltip)
		{
			var p = FindProp(name);
			if (p != null) _editor.ShaderProperty(p, new GUIContent(label, tooltip));
		}

		void ShaderPropTexIfExists(string name, string label)
		{
			var p = FindProp(name);
			if (p != null) _editor.TexturePropertySingleLine(new GUIContent(label), p);
		}

		MaterialProperty FindProp(string name)
		{
			return FindProperty(name, _props, false);
		}

		static void SetKeyword(Material m, string keyword, bool state)
		{
			if (state) m.EnableKeyword(keyword);
			else m.DisableKeyword(keyword);
		}

		void ApplyRenderingMode(Material mat, int mode)
		{
			foreach (Material m in _editor.targets) ApplyRenderingModeTo(m, mode);
		}

		static void ApplyRenderingModeTo(Material m, int mode)
		{
			switch (mode)
			{
				case 0: // Opaque
					m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
					m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
					SetFloatIf(m, "_ZWrite", 1f);
					SetKeyword(m, "_ALPHATEST_ON", false);
					SetKeyword(m, "_ALPHAPREMULTIPLY_ON", false);
					SetFloatIf(m, "_UseAlphaTest", 0f);
					m.SetOverrideTag("RenderType", "Opaque");
					m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
					break;
				case 1: // Cutout
					m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
					m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
					SetFloatIf(m, "_ZWrite", 1f);
					SetKeyword(m, "_ALPHATEST_ON", true);
					SetKeyword(m, "_ALPHAPREMULTIPLY_ON", false);
					SetFloatIf(m, "_UseAlphaTest", 1f);
					m.SetOverrideTag("RenderType", "TransparentCutout");
					m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
					break;
				case 2: // Fade
					m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
					m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					SetFloatIf(m, "_ZWrite", 0f);
					SetKeyword(m, "_ALPHATEST_ON", false);
					SetKeyword(m, "_ALPHAPREMULTIPLY_ON", false);
					SetFloatIf(m, "_UseAlphaTest", 0f);
					m.SetOverrideTag("RenderType", "Transparent");
					m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
					break;
				case 3: // Transparent (premultiplied)
					m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
					m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					SetFloatIf(m, "_ZWrite", 0f);
					SetKeyword(m, "_ALPHATEST_ON", false);
					SetKeyword(m, "_ALPHAPREMULTIPLY_ON", true);
					SetFloatIf(m, "_UseAlphaTest", 0f);
					m.SetOverrideTag("RenderType", "Transparent");
					m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
					break;
			}
		}

		static void SetFloatIf(Material m, string name, float value)
		{
			if (m.HasProperty(name)) m.SetFloat(name, value);
		}
	}
}
