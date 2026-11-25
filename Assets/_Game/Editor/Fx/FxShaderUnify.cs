using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 파티클 이펙트 셰이더를 하나(<see cref="ShaderName"/> = ZombieOverrun/FX/Particle, Art/VFX/Shaders/FX_Particle.shader)로 합친다 — 2026-11-20.
///
/// <para>옮기는 셰이더: URP Particles/Unlit · Lit(Unity 기본), Survivor Ace 원작 SurvivorAce/Effects/Particle · UVFlow,
/// 탕탕 원작 Effects/Particle · UVFlow · Turbulence · Custom/SheetBase_tranparent, 빌트인 Particles/Standard Unlit · Legacy Shaders/Particles/*.
/// 셰이더 그래프를 잃은 머티리얼(_MainSheet 텍스처가 남아 있으면)은 시트 첫 칸으로 살린다 — M_LightningRing 은 텍스처까지 지워져 11-20 에 손으로 채웠다.
/// MSFX 복사본 셰이더 ZombieOverrun/MSFX/PowDissolve · CommonParticleOneShot 도 11-20 저녁에 같이(셰이더의 MSFX 모드 — 사용자 정점 스트림).
/// 코드 이펙트 전용 셰이더(Proto/* · 화상 · 파편 디졸브 · 불꽃 테두리)는 그대로.</para>
///
/// <para>값을 옮기는 규칙(그림이 같게): URP 머티리얼은 이름이 같은 값을 그대로, 깊이 검사 LEqual(4) · 안개 받음.
/// 원작 머티리얼은 _MainTex → _BaseMap, _TintColor → _TintColor(HDR 칸 — URP 식 _BaseColor 는 흰색), _TwoSide → _Cull, _Ztest → _ZTest, 블렌드 그대로(알파 채널도 같은 값),
/// 안개 안 받음, UVFlow 의 흐름 · 마스크 · 회전(_Angele), Turbulence 의 일렁임(세기 = _Noise × 0.2) · 마스크(_MaskMap).
/// 큐(m_CustomRenderQueue)는 숫자 그대로. 쓰지 않는 옛 값(남은 _MainTex · 발광 텍스처 등)은 버린다 — 팩 텍스처 참조도 같이 끊긴다.</para>
///
/// <para>메뉴: Tools ▸ 좀비 오버런 ▸ FX 툴 ▸ 파티클 셰이더 하나로 합치기. 배치: <see cref="BatchSelfCheck"/> · <see cref="BatchShaderCheck"/> ·
/// (진단용 색 · MSFX 사각형 시험은 11-20 끝나고 걷어냄 — _Backup/20261120_FxShaderUnify/FxShaderUnify.before_cleanup.cs) · <see cref="BatchRun"/> — 전 렌더 → 변환 → 후 렌더 → 비교
/// (Library/FxShaderUnify/compare/*.png · report.txt).</para>
/// </summary>
public static class FxShaderUnify
{
    public const string ShaderName = "ZombieOverrun/FX/Particle";
    public static readonly string[] Roots = { "Assets/_Game", "Assets/_Reference/SurvivorAce", "Assets/_Reference/TangTang" };
    /// 있는 폴더만(_Reference 는 나중에 지울 예정 — 11-23). 없는 폴더를 FindAssets 에 넘기면 오류다.
    static string[] LiveRoots => Roots.Where(AssetDatabase.IsValidFolder).ToArray();
    static readonly string[] PrefabRoots = { "Assets/_Game/Art/VFX", "Assets/_Game/Art/Sujin", "Assets/_Game/Resources/VFX", "Assets/_Reference/SurvivorAce/Prefabs", "Assets/_Reference/TangTang/Prefabs" };
    const string OutDir = "Library/FxShaderUnify";

    enum Kind { None, Urp, Origin, OriginFlow, OriginTurb, OriginSheet, BuiltinStd, LegacyParticle, LostSheet, MsfxDissolve, MsfxOneShot }

    static Kind KindOf(string shaderName, Saved s)
    {
        switch (shaderName)
        {
            case "Universal Render Pipeline/Particles/Unlit":
            case "Universal Render Pipeline/Particles/Lit": return Kind.Urp;
            case "SurvivorAce/Effects/Particle":
            case "Effects/Particle": return Kind.Origin;
            case "SurvivorAce/Effects/UVFlow":
            case "Effects/UVFlow": return Kind.OriginFlow;
            case "Effects/Turbulence": return Kind.OriginTurb;
            case "Custom/SheetBase_tranparent": return Kind.OriginSheet;
            case "Particles/Standard Unlit": return Kind.BuiltinStd;
            case "ZombieOverrun/MSFX/PowDissolve": return Kind.MsfxDissolve;
            case "ZombieOverrun/MSFX/CommonParticleOneShot": return Kind.MsfxOneShot;
            case "(없음)":
            case "Hidden/InternalErrorShader": return s.T.ContainsKey("_MainSheet") && s.T["_MainSheet"].tex != null ? Kind.LostSheet : Kind.None;
        }
        if (shaderName.StartsWith("Legacy Shaders/Particles/") || shaderName.StartsWith("Mobile/Particles/")) return Kind.LegacyParticle;
        return Kind.None;
    }

    static Shader _unified;
    public static Shader Unified => _unified != null ? _unified : (_unified = Shader.Find(ShaderName));

    // ── 저장된 값 읽기(셰이더와 상관없이 머티리얼 파일에 남은 그대로) ──────────
    class Saved
    {
        public readonly Dictionary<string, float> F = new Dictionary<string, float>();
        public readonly Dictionary<string, Color> C = new Dictionary<string, Color>();
        public readonly Dictionary<string, (Texture tex, Vector2 scale, Vector2 offset)> T = new Dictionary<string, (Texture, Vector2, Vector2)>();

        public float Fl(string n, float def) => F.TryGetValue(n, out var v) ? v : def;
        public Color Col(string n, Color def) => C.TryGetValue(n, out var v) ? v : def;

        public static Saved Read(Material m)
        {
            var s = new Saved();
            var so = new SerializedObject(m);
            var sp = so.FindProperty("m_SavedProperties");
            var tex = sp.FindPropertyRelative("m_TexEnvs");
            for (int i = 0; tex != null && i < tex.arraySize; i++)
            {
                var e = tex.GetArrayElementAtIndex(i);
                var sec = e.FindPropertyRelative("second");
                s.T[e.FindPropertyRelative("first").stringValue] = (sec.FindPropertyRelative("m_Texture").objectReferenceValue as Texture,
                    sec.FindPropertyRelative("m_Scale").vector2Value, sec.FindPropertyRelative("m_Offset").vector2Value);
            }
            var fl = sp.FindPropertyRelative("m_Floats");
            for (int i = 0; fl != null && i < fl.arraySize; i++)
            {
                var e = fl.GetArrayElementAtIndex(i);
                s.F[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").floatValue;
            }
            var ints = sp.FindPropertyRelative("m_Ints");
            for (int i = 0; ints != null && i < ints.arraySize; i++)
            {
                var e = ints.GetArrayElementAtIndex(i);
                string k = e.FindPropertyRelative("first").stringValue;
                if (!s.F.ContainsKey(k)) s.F[k] = e.FindPropertyRelative("second").intValue;
            }
            var col = sp.FindPropertyRelative("m_Colors");
            for (int i = 0; col != null && i < col.arraySize; i++)
            {
                var e = col.GetArrayElementAtIndex(i);
                s.C[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").colorValue;
            }
            return s;
        }
    }

    static void Tex(Material d, string dst, Saved s, string src)
    {
        if (!s.T.TryGetValue(src, out var t)) return;
        d.SetTexture(dst, t.tex);
        d.SetTextureScale(dst, t.scale);
        d.SetTextureOffset(dst, t.offset);
    }

    static void Key(Material d, string kw, bool on, string toggle = null)
    {
        if (on) d.EnableKeyword(kw); else d.DisableKeyword(kw);
        if (toggle != null) d.SetFloat(toggle, on ? 1f : 0f);
    }

    // URP Particles/Unlit 의 기본값 — 머티리얼에 값이 없으면 이것이 쓰였다
    static readonly (string name, float def)[] UrpFloats =
    {
        ("_SrcBlend", 1f), ("_DstBlend", 0f), ("_SrcBlendAlpha", 1f), ("_DstBlendAlpha", 0f), ("_Cull", 2f), ("_ZWrite", 1f),
        ("_Surface", 0f), ("_Blend", 0f), ("_ColorMode", 0f), ("_Cutoff", 0.5f), ("_AlphaClip", 0f), ("_QueueOffset", 0f),
    };

    /// 머티리얼 하나를 통합 셰이더로. 옮겼으면 설명 한 줄, 대상이 아니면 null.
    static string ConvertOne(Material m, List<string> warnings)
    {
        var shader = m.shader;
        string from = shader != null ? shader.name : "(없음)";
        var s = Saved.Read(m);
        var kind = KindOf(from, s);
        if (kind == Kind.None) return null;

        var so = new SerializedObject(m);
        int rawQueue = so.FindProperty("m_CustomRenderQueue").intValue;
        var kws = new HashSet<string>(m.shaderKeywords ?? Array.Empty<string>());
        var d = new Material(Unified) { name = m.name };
        string note = kind.ToString();

        switch (kind)
        {
            case Kind.Urp:
            {
                Tex(d, "_BaseMap", s, "_BaseMap");
                d.SetColor("_BaseColor", s.Col("_BaseColor", Color.white));
                foreach (var (n, def) in UrpFloats) d.SetFloat(n, s.Fl(n, def));
                d.SetColor("_BaseColorAddSubDiff", s.Col("_BaseColorAddSubDiff", Color.clear));
                d.SetFloat("_ZTest", 4f);   // URP 파티클은 LEqual 고정
                d.SetFloat("_Fog", 1f);     // URP 파티클은 안개를 받는다
                Key(d, "_FLIPBOOKBLENDING_ON", kws.Contains("_FLIPBOOKBLENDING_ON"), "_FlipbookBlending");
                bool soft = kws.Contains("_SOFTPARTICLES_ON");
                if (soft)
                {
                    // URP 는 인스펙터가 계산해 둔 _SoftParticleFadeParams(가까이, 1/(멀리-가까이))로 그렸다 — 거기서 거리를 되찾는다
                    var p = s.Col("_SoftParticleFadeParams", Color.clear);
                    if (p.g > 0f && !float.IsInfinity(p.g))
                    {
                        d.SetFloat("_SoftParticlesNearFadeDistance", p.r);
                        d.SetFloat("_SoftParticlesFarFadeDistance", p.r + 1f / p.g);
                    }
                    else if (p.r <= 0f) { soft = false; warnings.Add($"{m.name}: 소프트 파티클 값이 0 이라 URP 에서도 효과가 없었다 — 끔"); }
                }
                Key(d, "_SOFTPARTICLES_ON", soft, "_SoftParticlesEnabled");
                bool camFade = false;
                if (kws.Contains("_FADING_ON") && s.Fl("_CameraFadingEnabled", 0f) > 0.5f)
                {
                    var c = s.Col("_CameraFadeParams", Color.clear);
                    if (c.g > 0f && !float.IsInfinity(c.g))
                    {
                        camFade = true;
                        d.SetFloat("_CameraNearFadeDistance", c.r);
                        d.SetFloat("_CameraFarFadeDistance", c.r + 1f / c.g);
                    }
                    else warnings.Add($"{m.name}: 카메라 흐려짐 값이 비어 있어 끔");
                }
                Key(d, "_FADING_ON", camFade, "_CameraFadingEnabled");
                bool emit = kws.Contains("_EMISSION");
                if (emit) { Tex(d, "_EmissionMap", s, "_EmissionMap"); d.SetColor("_EmissionColor", s.Col("_EmissionColor", Color.black)); }
                Key(d, "_EMISSION", emit, "_EmissionEnabled");
                foreach (var k in new[] { "_ALPHATEST_ON", "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON", "_COLOROVERLAY_ON", "_COLORCOLOR_ON", "_COLORADDSUBDIFF_ON" })
                    Key(d, k, kws.Contains(k));
                if (from.EndsWith("/Lit")) { note += " (빛 받던 Lit → 빛 없이)"; warnings.Add($"{m.name}: Particles/Lit 였다 — 조명 반응이 없어진다"); }
                if (s.Fl("_Surface", 0f) < 0.5f && rawQueue < 0) rawQueue = 2000;   // URP 불투명은 Geometry 큐
                break;
            }
            case Kind.Origin:
            case Kind.OriginFlow:
            case Kind.OriginTurb:
            case Kind.OriginSheet:
            {
                Tex(d, "_BaseMap", s, "_MainTex");
                d.SetColor("_BaseColor", Color.white);
                d.SetColor("_TintColor", s.Col("_TintColor", Color.white));   // 원작 [HDR] _TintColor — 같은 이름 · 같은 HDR 칸이라 숫자 그대로
                float src = s.Fl("_SrcBlend", 1f), dst = s.Fl("_DstBlend", 0f);
                d.SetFloat("_SrcBlend", src); d.SetFloat("_DstBlend", dst);
                d.SetFloat("_SrcBlendAlpha", src); d.SetFloat("_DstBlendAlpha", dst);   // 원작은 Blend 한 줄 — 알파 채널도 같은 값
                d.SetFloat("_Cull", s.Fl("_TwoSide", 0f));
                d.SetFloat("_ZTest", s.Fl("_Ztest", 2f));
                d.SetFloat("_ZWrite", 0f);
                d.SetFloat("_Fog", 0f);                                                 // 원작 셰이더는 안개를 안 받았다
                d.SetFloat("_Surface", 1f);
                d.SetFloat("_Blend", Mathf.Approximately(dst, 1f) ? 2f : 0f);
                if (kind == Kind.OriginFlow || kind == Kind.OriginTurb)
                    d.SetVector("_MainSpeed", s.Col("_MainSpeed", Color.clear));
                if (kind == Kind.OriginFlow)
                {
                    d.SetFloat("_MainRotation", s.Fl("_Angele", 0f));   // 탕탕 UVFlow 만 있던 값(Survivor Ace 판엔 없음 → 0)
                    bool mask = s.T.TryGetValue("_MaskTex", out var mt) && mt.tex != null;
                    if (mask) { Tex(d, "_MaskTex", s, "_MaskTex"); d.SetVector("_MaskSpeed", s.Col("_MaskSpeed", Color.clear)); }
                    Key(d, "_MASK_ON", mask, "_MaskEnabled");
                }
                if (kind == Kind.OriginTurb)
                {
                    bool noise = s.T.TryGetValue("_NoiseMap", out var nt) && nt.tex != null;
                    if (noise)
                    {
                        Tex(d, "_NoiseMap", s, "_NoiseMap");
                        d.SetVector("_NoiseSpeed", s.Col("_NoiseSpeed", Color.clear));
                        d.SetFloat("_NoiseStrength", s.Fl("_Noise", 1f) * 0.2f);
                    }
                    else warnings.Add($"{m.name}: 일렁임 텍스처가 없다 — 일렁임 끔");
                    Key(d, "_NOISE_ON", noise, "_NoiseEnabled");
                    bool mask = s.T.TryGetValue("_MaskMap", out var mt) && mt.tex != null;
                    if (mask) Tex(d, "_MaskTex", s, "_MaskMap");
                    Key(d, "_MASK_ON", mask, "_MaskEnabled");
                }
                if (kind == Kind.OriginSheet)
                {
                    // SheetBase: _U × _V 칸 중 _CurrentIndex 칸(음수면 0) — 왼쪽 위부터. 칸을 타일링 · 오프셋으로 옮긴다
                    float cols = Mathf.Max(1f, Mathf.Round(s.Fl("_U", 1f))), rows = Mathf.Max(1f, Mathf.Round(s.Fl("_V", 1f)));
                    float idx = Mathf.Max(0f, s.Fl("_CurrentIndex", -1f));
                    float frame = idx % (cols * rows), c = frame % cols, r = Mathf.Floor(frame / cols);
                    Vector2 sc = Vector2.one, of = Vector2.zero;
                    if (s.T.TryGetValue("_MainTex", out var mt)) { sc = mt.scale; of = mt.offset; }
                    d.SetTextureScale("_BaseMap", new Vector2(sc.x / cols, sc.y / rows));
                    d.SetTextureOffset("_BaseMap", new Vector2(sc.x * c / cols, sc.y * (rows - 1f - r) / rows) + of);
                }
                break;
            }
            case Kind.BuiltinStd:
            {
                Tex(d, "_BaseMap", s, "_MainTex");
                d.SetColor("_BaseColor", s.Col("_Color", Color.white));
                float src = s.Fl("_SrcBlend", 1f), dst = s.Fl("_DstBlend", 0f);
                d.SetFloat("_SrcBlend", src); d.SetFloat("_DstBlend", dst);
                d.SetFloat("_SrcBlendAlpha", 1f); d.SetFloat("_DstBlendAlpha", dst);
                d.SetFloat("_Cull", s.Fl("_Cull", 2f));
                d.SetFloat("_ZTest", 4f); d.SetFloat("_ZWrite", 0f); d.SetFloat("_Fog", 1f); d.SetFloat("_Surface", 1f);
                float mode = s.Fl("_Mode", 0f), cm = s.Fl("_ColorMode", 0f);
                Key(d, "_ALPHAPREMULTIPLY_ON", mode == 3f);
                Key(d, "_ALPHAMODULATE_ON", mode == 6f);
                Key(d, "_COLOROVERLAY_ON", cm == 3f);
                Key(d, "_COLORCOLOR_ON", cm == 4f);
                bool asd = cm == 1f || cm == 2f || cm == 5f;
                Key(d, "_COLORADDSUBDIFF_ON", asd);
                if (asd) d.SetColor("_BaseColorAddSubDiff", cm == 1f ? new Color(1, 0, 0, 0) : cm == 2f ? new Color(-1, 0, 0, 0) : new Color(-1, 1, 0, 0));
                d.SetFloat("_ColorMode", cm);
                note += " (URP 에서 분홍이던 빌트인 셰이더)";
                break;
            }
            case Kind.MsfxDissolve:
            {
                // MSFX PowDissolve(Amplify): 색 = 정점색 × _Base × (UV.w + 1), 알파 = 노이즈 디졸브. 알파 블렌드(색 SrcAlpha/1-SrcAlpha, 알파 One/1-SrcAlpha), 컬 Back, 안개 받음
                Tex(d, "_BaseMap", s, "_Base");
                if (s.T.TryGetValue("_Noise", out var nz)) d.SetTexture("_MaskTex", nz.tex);   // 비어 있으면 흰색(원본 기본값과 같음)
                var mt = s.Col("_MaskTiling", new Color(1, 1, 0, 0));
                d.SetTextureScale("_MaskTex", new Vector2(mt.r, mt.g));
                d.SetTextureOffset("_MaskTex", new Vector2(0f, 1f));                               // 원본: + (Custom1.y × 시간, 1)
                d.SetFloat("_Msfx", 1f); d.EnableKeyword("_MSFX_DISSOLVE");
                d.SetFloat("_SrcBlend", 5f); d.SetFloat("_DstBlend", 10f); d.SetFloat("_SrcBlendAlpha", 1f); d.SetFloat("_DstBlendAlpha", 10f);
                d.SetFloat("_Blend", 0f); d.SetFloat("_Surface", 1f);
                d.SetFloat("_Cull", 2f); d.SetFloat("_ZTest", 4f); d.SetFloat("_ZWrite", 0f); d.SetFloat("_Fog", 1f);
                note += " (MSFX 디졸브 — 정점 스트림 UV.zw · Custom1.xy)";
                break;
            }
            case Kind.MsfxOneShot:
            {
                // MSFX CommonParticleOneShot(Amplify): 텍스처 UV × Tiling + Offset + UV.z × Scroll, 경계 마스크(_Mask) · 고정 마스크, 밝기 × (1 + Custom1.x). 알파 블렌드, 컬 끔, 안개 받음
                if (s.T.TryGetValue("_Main", out var mn)) d.SetTexture("_BaseMap", mn.tex);       // [NoScaleOffset] — 타일링은 _Tiling · _Offset
                var tl = s.Col("_Tiling", new Color(1, 1, 0, 0)); var of = s.Col("_Offset", Color.clear);
                d.SetTextureScale("_BaseMap", new Vector2(tl.r, tl.g)); d.SetTextureOffset("_BaseMap", new Vector2(of.r, of.g));
                d.SetVector("_CustomScroll", s.Col("_Scroll", new Color(1, 0, 0, 0)));
                bool mask = s.Fl("_Mask", 1f) > 0.5f;
                if (mask)
                {
                    if (s.T.TryGetValue("_NoiseMask", out var nm)) d.SetTexture("_MaskTex", nm.tex);
                    var mtl = s.Col("_MaskTiling", Color.clear); var mof = s.Col("_MaskOffset", Color.clear);
                    d.SetTextureScale("_MaskTex", new Vector2(mtl.r, mtl.g)); d.SetTextureOffset("_MaskTex", new Vector2(mof.r, mof.g));
                    d.SetVector("_CustomMaskScroll", s.Col("_MaskScroll", Color.clear));
                    d.SetFloat("_Feather", s.Fl("_Feather", 0f));
                }
                Key(d, "_MASK_ON", mask, "_MaskEnabled");
                Tex(d, "_StaticMask", s, "_StaticMask");
                d.SetVector("_StaticSmooth", s.Col("_SmoothStep", new Color(0, 1, 0, 0)));
                d.SetFloat("_Msfx", 2f); d.EnableKeyword("_MSFX_ONESHOT");
                d.SetFloat("_SrcBlend", 5f); d.SetFloat("_DstBlend", 10f); d.SetFloat("_SrcBlendAlpha", 1f); d.SetFloat("_DstBlendAlpha", 10f);
                d.SetFloat("_Blend", 0f); d.SetFloat("_Surface", 1f);
                d.SetFloat("_Cull", 0f); d.SetFloat("_ZTest", 4f); d.SetFloat("_ZWrite", 0f); d.SetFloat("_Fog", 1f);
                if (s.Fl("_Stretch", 0f) > 0.5f || s.Fl("_LimitUV", 0f) > 0.5f)
                    warnings.Add($"{m.name}: MSFX 원샷의 Stretch · LimitUV 가 켜져 있었다 — 통합 셰이더엔 없는 기능(꺼진 채로 옮김)");
                note += " (MSFX 원샷 — 정점 스트림 UV.zw · Custom1.xzw)";
                break;
            }
            case Kind.LegacyParticle:
            {
                // 빌트인 레거시 파티클: 텍스처 × 색 × 정점색 × 2, 블렌드는 이름으로(Additive = SrcAlpha One, 그 밖 = 알파), 컬 끔, 안개 없음(URP 에선 안 걸림)
                Tex(d, "_BaseMap", s, "_MainTex");
                d.SetColor("_BaseColor", s.Col("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)));   // 보통 색 — 감마 → 리니어 그대로
                d.SetColor("_TintColor", new Color(2f, 2f, 2f, 2f));                                   // 레거시의 × 2 (알파까지)
                bool add = from.Contains("Additive");
                d.SetFloat("_SrcBlend", 5f); d.SetFloat("_DstBlend", add ? 1f : 10f);
                d.SetFloat("_SrcBlendAlpha", 5f); d.SetFloat("_DstBlendAlpha", add ? 1f : 10f);
                d.SetFloat("_Blend", add ? 2f : 0f); d.SetFloat("_Surface", 1f);
                d.SetFloat("_Cull", 0f); d.SetFloat("_ZTest", 4f); d.SetFloat("_ZWrite", 0f); d.SetFloat("_Fog", 0f);
                note += " (빌트인 레거시 파티클)";
                break;
            }
            case Kind.LostSheet:
            {
                // 지워진 팩 셰이더 그래프(4×4 시트 · 회전 · 세기 · 디졸브). 디졸브는 없이 시작 칸 하나를 회전 · 세기 그대로
                var sheet = s.T["_MainSheet"];
                var size = s.Col("_SheetSize", new Color(1, 1, 0, 0));
                float cols = Mathf.Max(1f, size.r), rows = Mathf.Max(1f, size.g);
                float frame = Mathf.Max(0f, s.Fl("_StartFrame", 0f)) % (cols * rows), c = frame % cols, r = Mathf.Floor(frame / cols);
                d.SetTexture("_BaseMap", sheet.tex);
                d.SetTextureScale("_BaseMap", new Vector2(1f / cols, 1f / rows));
                d.SetTextureOffset("_BaseMap", new Vector2(c / cols, (rows - 1f - r) / rows));
                d.SetFloat("_MainRotation", s.Fl("_Rot", 0f));
                float k = s.Fl("_Intensity", 1f);
                d.SetColor("_TintColor", new Color(k, k, k, s.Fl("_Opacity", 1f)));   // 세기는 곱하기 값(리니어) — HDR 칸으로
                float src = s.Fl("_SrcBlend", 5f), dst = s.Fl("_DstBlend", 10f);
                d.SetFloat("_SrcBlend", src); d.SetFloat("_DstBlend", dst); d.SetFloat("_SrcBlendAlpha", 1f); d.SetFloat("_DstBlendAlpha", dst);
                d.SetFloat("_Cull", s.Fl("_Cull", 0f)); d.SetFloat("_ZTest", s.Fl("_ZTest", 4f)); d.SetFloat("_ZWrite", 0f); d.SetFloat("_Fog", 1f);
                note += " (셰이더 그래프가 지워져 분홍이던 것 — 시트 한 칸으로)";
                warnings.Add($"{m.name}: 원래 셰이더 그래프가 없어 시트 한 칸 · 디졸브 없이 살림");
                break;
            }
        }

        d.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.CopySerialized(d, m);
        m.renderQueue = rawQueue;
        Object.DestroyImmediate(d);
        EditorUtility.SetDirty(m);
        return $"{AssetDatabase.GetAssetPath(m)} | {from} → {note} | 큐 {rawQueue}";
    }

    /// 대상 머티리얼을 전부 옮긴다. 옮긴 줄 목록.
    public static List<string> ConvertAll(List<string> warnings)
    {
        var done = new List<string>();
        var paths = new List<string>();
        if (Unified == null) throw new Exception($"{ShaderName} 셰이더를 못 찾았다");
        if (ShaderUtil.ShaderHasError(Unified))
            throw new Exception($"{ShaderName} 에 컴파일 오류: " + string.Join(" / ", ShaderUtil.GetShaderMessages(Unified).Select(x => x.message)));
        foreach (var guid in AssetDatabase.FindAssets("t:Material", LiveRoots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == Unified) continue;
            string line = ConvertOne(m, warnings);
            if (line != null) { done.Add(line); paths.Add(path); }
        }
        AssetDatabase.SaveAssets();
        // 바꾼 머티리얼을 디스크에서 다시 읽는다 — CopySerialized 직후 메모리의 키워드 상태가 덜 반영된 채 그려진 적이 있다(11-20, 저장본은 정상)
        foreach (var path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return done;
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/파티클 셰이더 하나로 합치기 (ZombieOverrun/FX/Particle)")]
    static void ConvertMenu()
    {
        if (!EditorUtility.DisplayDialog("파티클 셰이더 합치기",
                "_Game · _Reference 의 파티클 머티리얼 중 URP Particles · Survivor Ace · 탕탕 원작 셰이더를 쓰는 것을 " + ShaderName +
                " 로 옮긴다. 값은 그림이 같도록 옮기고 쓰지 않는 옛 값은 버린다. 새로 가져온 원작 이펙트에 쓰면 된다.", "옮기기", "그만")) return;
        var warnings = new List<string>();
        var done = ConvertAll(warnings);
        Debug.Log($"[FX 셰이더] {done.Count}개 옮김\n" + string.Join("\n", done) + (warnings.Count > 0 ? "\n주의:\n" + string.Join("\n", warnings) : ""));
    }

    // ── 전후 렌더 비교 ─────────────────────────────────────────────

    /// 배치 입구: Unity.exe -batchmode -projectPath ... -executeMethod FxShaderUnify.BatchRun -logFile ... (그래픽 장치가 있어야 한다 — -nographics 금지)
    public static void BatchRun()
    {
        int exit = 0;
        try { RunWithCheck(); }
        catch (Exception e) { Debug.LogError("[FX 셰이더] 실패: " + e); exit = 1; }
        EditorApplication.Exit(exit);
    }

    /// 배치 점검: 바꾼 뒤 상태를 같은 순서로 두 번 렌더해 비교(셰이더가 아닌 차이 — 시간 흐름 UV · 서브 이미터 순서 — 를 가려낸다)
    /// + 대상 폴더 머티리얼의 셰이더 목록 · 오류 셰이더. Library/FxShaderUnify/selfcheck.txt · compare_self/*.png
    public static void BatchSelfCheck()
    {
        int exit = 0;
        try
        {
            Directory.CreateDirectory(OutDir);
            var sb = new StringBuilder();
            var warnings = new List<string>();
            var done = ConvertAll(warnings);
            sb.AppendLine($"남은 것 옮김 {done.Count}개");
            foreach (var l in done) sb.AppendLine("  " + l);
            foreach (var w in warnings) sb.AppendLine("  주의: " + w);
            var count = new Dictionary<string, int>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", LiveRoots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                string n = m == null || m.shader == null ? "(없음)" : m.shader.name;
                count[n] = count.TryGetValue(n, out var c) ? c + 1 : 1;
                if (n == "Hidden/InternalErrorShader" || n == "(없음)") sb.AppendLine("오류 셰이더: " + path);
            }
            sb.AppendLine($"{ShaderName} 오류: {(Unified == null ? "없음(못 찾음)" : ShaderUtil.ShaderHasError(Unified).ToString())}");
            sb.AppendLine("셰이더별 머티리얼 수:");
            foreach (var kv in count.OrderByDescending(x => x.Value)) sb.AppendLine($"  {kv.Value}\t{kv.Key}");

            var prefabs = PrefabRoots.Where(AssetDatabase.IsValidFolder)
                .SelectMany(r => AssetDatabase.FindAssets("t:Prefab", new[] { r }))
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Where(p => { var go = AssetDatabase.LoadAssetAtPath<GameObject>(p); return go != null && go.GetComponentInChildren<ParticleSystem>(true) != null; })
                .OrderBy(p => p).ToList();
            var log = new StringBuilder();
            var a = Render(prefabs, null, log);
            var b = Render(prefabs, a, log);
            sb.AppendLine("\n같은 상태 두 번 렌더 — 이름\t평균차\t12넘는 픽셀\t최대차");
            string dir = Path.Combine(OutDir, "compare_self");
            Directory.CreateDirectory(dir);
            for (int i = 0; i < a.Count; i++)
            {
                var df = Diff(a[i], b[i]);
                sb.AppendLine($"{Path.GetFileNameWithoutExtension(a[i].path)}\t{df.mean:0.00}\t{df.overPct:0.00}%\t{df.max}");
                var sheet = new Texture2D(W * a[i].frames.Count, H, TextureFormat.RGBA32, false);
                for (int f = 0; f < a[i].frames.Count; f++) sheet.SetPixels32(f * W, 0, W, H, a[i].frames[f]);
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(dir, Path.GetFileNameWithoutExtension(a[i].path) + ".png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
            }
            File.WriteAllText(Path.Combine(OutDir, "selfcheck.txt"), sb.ToString());
            Debug.Log("[FX 셰이더] 점검 끝 — " + OutDir + "/selfcheck.txt");
        }
        catch (Exception e) { Debug.LogError("[FX 셰이더] 점검 실패: " + e); exit = 1; }
        EditorApplication.Exit(exit);
    }

    static (double mean, double overPct, int max) Diff(Shot a, Shot b)
    {
        double sum = 0; int over = 0, total = 0, max = 0;
        for (int f = 0; f < a.frames.Count; f++)
            for (int k = 0; k < a.frames[f].Length; k++)
            {
                var p = a.frames[f][k]; var q = b.frames[f][k];
                int dr = Math.Abs(p.r - q.r), dg = Math.Abs(p.g - q.g), db = Math.Abs(p.b - q.b), dm = Math.Max(dr, Math.Max(dg, db));
                sum += (dr + dg + db) / 3.0; total++;
                if (dm > 12) over++;
                if (dm > max) max = dm;
            }
        return (sum / Math.Max(1, total), 100.0 * over / Math.Max(1, total), max);
    }

    /// 배치 점검(가벼움): _Game 셰이더마다 컴파일 오류 · 그 셰이더를 쓰는 머티리얼 수 → Library/FxShaderUnify/shadercheck.txt
    public static void BatchShaderCheck()
    {
        int exit = 0;
        var sb = new StringBuilder();
        try
        {
            Directory.CreateDirectory(OutDir);
            var use = new Dictionary<Shader, int>();
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game", "Assets/_Reference" }.Where(AssetDatabase.IsValidFolder).ToArray()))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m == null || m.shader == null) continue;
                use[m.shader] = use.TryGetValue(m.shader, out var c) ? c + 1 : 1;
                if (m.shader.name == "Hidden/InternalErrorShader" && !AssetDatabase.GUIDToAssetPath(g).EndsWith(".fbx")) sb.AppendLine("오류 셰이더 머티리얼: " + AssetDatabase.GUIDToAssetPath(g));
            }
            foreach (var g in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (sh == null) continue;
                var msgs = ShaderUtil.GetShaderMessages(sh).Where(x => x.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToList();
                sb.AppendLine($"{(ShaderUtil.ShaderHasError(sh) || msgs.Count > 0 ? "오류" : "ok")}\t{sh.name}\t머티리얼 {(use.TryGetValue(sh, out var n) ? n : 0)}\t{path}");
                foreach (var x in msgs.Take(3)) sb.AppendLine($"    {x.message} ({x.file}:{x.line})");
            }
        }
        catch (Exception e) { sb.AppendLine("실패: " + e); exit = 1; }
        File.WriteAllText(Path.Combine(OutDir, "shadercheck.txt"), sb.ToString());
        EditorApplication.Exit(exit);
    }

    class Shot { public string path; public float[] times; public Vector3 center; public float dist; public List<Color32[]> frames = new List<Color32[]>(); }
    const int W = 256, H = 160;

    public static void RunWithCheck()
    {
        Directory.CreateDirectory(OutDir);
        var prefabs = PrefabRoots.Where(AssetDatabase.IsValidFolder)
            .SelectMany(r => AssetDatabase.FindAssets("t:Prefab", new[] { r }))
            .Select(AssetDatabase.GUIDToAssetPath).Distinct()
            .Where(p => { var go = AssetDatabase.LoadAssetAtPath<GameObject>(p); return go != null && go.GetComponentInChildren<ParticleSystem>(true) != null; })
            .OrderBy(p => p).ToList();
        var log = new StringBuilder();
        log.AppendLine($"프리팹 {prefabs.Count}개");

        var before = Render(prefabs, null, log);
        var warnings = new List<string>();
        var done = ConvertAll(warnings);
        File.WriteAllText(Path.Combine(OutDir, "converted.txt"), string.Join("\n", done) + "\n\n주의:\n" + string.Join("\n", warnings));
        log.AppendLine($"옮긴 머티리얼 {done.Count}개, 주의 {warnings.Count}");
        var after = Render(prefabs, before, log);

        string cmpDir = Path.Combine(OutDir, "compare");
        Directory.CreateDirectory(cmpDir);
        var rows = new List<string>();
        for (int i = 0; i < before.Count; i++)
        {
            var b = before[i]; var a = after[i];
            double sum = 0; int over = 0, total = 0, max = 0;
            var sheet = new Texture2D(W * b.frames.Count, H * 3, TextureFormat.RGBA32, false);
            for (int f = 0; f < b.frames.Count; f++)
            {
                var pb = b.frames[f]; var pa = a.frames[f];
                var diff = new Color32[pb.Length];
                for (int k = 0; k < pb.Length; k++)
                {
                    int dr = Math.Abs(pb[k].r - pa[k].r), dg = Math.Abs(pb[k].g - pa[k].g), db = Math.Abs(pb[k].b - pa[k].b);
                    int dm = Math.Max(dr, Math.Max(dg, db));
                    sum += (dr + dg + db) / 3.0; total++;
                    if (dm > 12) over++;
                    if (dm > max) max = dm;
                    byte v = (byte)Mathf.Min(255, dm * 4);
                    diff[k] = new Color32(v, v, v, 255);
                }
                sheet.SetPixels32(f * W, H * 2, W, H, pb);
                sheet.SetPixels32(f * W, H, W, H, pa);
                sheet.SetPixels32(f * W, 0, W, H, diff);
            }
            sheet.Apply();
            string name = Path.GetFileNameWithoutExtension(b.path);
            File.WriteAllBytes(Path.Combine(cmpDir, name + ".png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            rows.Add($"{name}\t{sum / Math.Max(1, total):0.00}\t{100.0 * over / Math.Max(1, total):0.00}%\t{max}\t{b.path}");
        }
        File.WriteAllText(Path.Combine(OutDir, "report.txt"), "이름\t평균차(0-255)\t12넘는 픽셀\t최대차\t경로\n" + string.Join("\n", rows));
        File.WriteAllText(Path.Combine(OutDir, "log.txt"), log.ToString());
        Debug.Log($"[FX 셰이더] 비교 끝 — {OutDir}/report.txt · compare/*.png");
    }

    static List<Shot> Render(List<string> prefabs, List<Shot> framing, StringBuilder log, bool forceOn = false)
    {
        var shots = new List<Shot>();
        bool asyncWas = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        var pru = new PreviewRenderUtility();
        var temp = new List<Object>();
        try
        {
            var cam = pru.camera;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.215f, 0.25f, 1f);
            cam.nearClipPlane = 0.02f; cam.farClipPlane = 500f;
            cam.allowHDR = false;
            cam.fieldOfView = 35f;
            pru.lights[0].intensity = 0.9f; pru.lights[1].intensity = 0.3f;
            var gm = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave };
            gm.SetColor("_BaseColor", new Color(0.24f, 0.26f, 0.3f, 1f));
            temp.Add(gm);

            for (int i = 0; i < prefabs.Count; i++)
            {
                var shot = new Shot { path = prefabs[i] };
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[i]);
                var world = new GameObject("FxUnifyWorld");
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Object.DestroyImmediate(ground.GetComponent<Collider>());
                ground.transform.SetParent(world.transform, false);
                ground.transform.localScale = new Vector3(10f, 1f, 10f);
                ground.GetComponent<MeshRenderer>().sharedMaterial = gm;
                var fx = Object.Instantiate(prefab);
                fx.transform.SetParent(world.transform, false);
                fx.transform.localPosition = Vector3.zero;
                fx.SetActive(true);
                if (forceOn)
                {
                    // 꺼진 층(오브젝트 · 렌더러 · 방출)까지 켜서 그린다 — 꺼진 채 쓰이는 머티리얼도 비교하려고
                    foreach (var tr in fx.GetComponentsInChildren<Transform>(true)) tr.gameObject.SetActive(true);
                    foreach (var rr in fx.GetComponentsInChildren<Renderer>(true)) rr.enabled = true;
                    foreach (var ps0 in fx.GetComponentsInChildren<ParticleSystem>(true)) { var em0 = ps0.emission; em0.enabled = true; }
                }
                pru.AddSingleGO(world);
                var all = fx.GetComponentsInChildren<ParticleSystem>(true);
                for (int k = 0; k < all.Length; k++) { all[k].useAutoRandomSeed = false; all[k].randomSeed = (uint)(1234 + k * 17); }

                if (framing == null)
                {
                    float dur = 0f;
                    foreach (var p in all)
                    {
                        var main = p.main;
                        dur = Mathf.Max(dur, (main.loop ? 1.2f : main.duration) + main.startDelay.constantMax + main.startLifetime.constantMax);
                    }
                    dur = Mathf.Clamp(dur, 0.3f, 2.5f);
                    shot.times = new[] { 0.08f * dur, 0.22f * dur, 0.42f * dur, 0.7f * dur };
                    var bounds = ParticleBounds(all, shot.times);
                    shot.center = bounds.center;
                    float radius = Mathf.Clamp(bounds.extents.magnitude, 0.25f, 30f);
                    shot.dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                }
                else { shot.times = framing[i].times; shot.center = framing[i].center; shot.dist = framing[i].dist; }

                var rot = Quaternion.Euler(45f, 20f, 0f);
                cam.transform.SetPositionAndRotation(shot.center - rot * Vector3.forward * shot.dist, rot);
                foreach (float t in shot.times)
                {
                    foreach (var p in all) p.Simulate(t + 0.0001f, false, true, false);
                    var tex = Capture(pru, W, H);
                    shot.frames.Add(tex.GetPixels32());
                    Object.DestroyImmediate(tex);
                }
                Object.DestroyImmediate(world);
                shots.Add(shot);
            }
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = asyncWas;
            pru.Cleanup();
            foreach (var o in temp) if (o != null) Object.DestroyImmediate(o);
        }
        log.AppendLine($"렌더 {(framing == null ? "전" : "후")} {shots.Count}개");
        return shots;
    }

    static Bounds ParticleBounds(ParticleSystem[] all, float[] times)
    {
        bool has = false;
        var b = new Bounds();
        var buf = new ParticleSystem.Particle[1024];
        foreach (float t in new[] { times[1], times[2] })
        {
            foreach (var p in all) p.Simulate(t + 0.0001f, false, true, false);
            foreach (var p in all)
            {
                int n = p.GetParticles(buf);
                bool world = p.main.simulationSpace == ParticleSystemSimulationSpace.World;
                for (int i = 0; i < n; i++)
                {
                    var pos = world ? buf[i].position : p.transform.TransformPoint(buf[i].position);
                    float size = buf[i].GetCurrentSize3D(p).magnitude * 0.5f * (world ? 1f : p.transform.lossyScale.x);
                    var pb = new Bounds(pos, Vector3.one * Mathf.Max(0.05f, size * 2f));
                    if (!has) { b = pb; has = true; } else b.Encapsulate(pb);
                }
            }
        }
        if (!has) b = new Bounds(new Vector3(0f, 0.5f, 0f), Vector3.one * 2f);
        return b;
    }

    static Texture2D Capture(PreviewRenderUtility pru, int w, int h)
    {
        pru.BeginStaticPreview(new Rect(0, 0, w, h));
        pru.Render(true);
        var tex = pru.EndStaticPreview();
        if (tex.width == w && tex.height == h) return tex;
        var rt = RenderTexture.GetTemporary(w, h, 0);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        outTex.ReadPixels(new Rect(0, 0, w, h), 0, 0); outTex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(tex);
        return outTex;
    }
}
