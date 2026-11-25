using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Proto_City_Test 의 조명·안개·후처리 두 벌 — 맵 두 벌 전환(<see cref="CityMapVersions"/>, Tools/좀비 오버런/맵 버전/)의 조명 부분. 메뉴는 없다.
///
/// <para><b>처음(11월 전)</b> = <c>Look_Original.asset</c> — 11-12 처음 [Demo 조명 적용] 직전에 적어 둔 원래 값(주변광·안개·스카이박스·해·전역 볼륨 프로필 Field_Look ·
/// 밤 어둠 막 ProtoNightOverlay · _GroundFog · _LightPools 켜짐).</para>
/// <para><b>마지막</b>(하늘 없음 — 11-16) = <c>Look_Final.asset</c> — 마지막에서 처음으로 넘어갈 때 그 순간의 조명을 적어 둔다(손으로 바꾼 값까지 그대로 돌아온다).
/// 아직 없으면 [밤] 프리셋(아래 <see cref="Night"/>, 11-12 사용자와 정한 2판)을 입힌다. 두 기록 모두 지우지 않는다.</para>
///
/// 11-13 정리 전에는 낮(Demo_Scene_Field) · 밤 · 밤 Lake_3 색감 세 프리셋과 [원래대로] 메뉴가 있었다 — 사용자가 두 벌만 남기라고 해서
/// 쓰던 [밤] 만 남겼다(지운 둘의 값과 이력은 Art/ToonFantasyCity/README.md, 프로필 복사본은 Backup~).
/// 머티리얼(셰이더) · 나무 · 물 · 풀과는 CityMapVersions 가 같이 돌린다. SceneLookSetup 메뉴를 다시 돌리면 안개 값을 그쪽 값으로 덮는다.
/// </summary>
public static class ToonFantasyCityLook
{
    const string OriginalPath = ToonFantasyCitySwap.Root + "/Look_Original.asset";
    const string FinalPath = ToonFantasyCitySwap.Root + "/Look_Final.asset";

    /// 조명·후처리 한 벌.
    class Preset
    {
        public string label;
        public Color ambSky, ambEquator, ambGround;
        public FogMode fogMode; public Color fogColor; public float fogStart, fogEnd, fogDensity;
        public string skybox, packProfile;   // 팩 에셋 이름 — ToonFantasyAssets.Find 로 찾는다. skybox 가 비면 하늘 없음
        public Color sun; public float sunIntensity;
        public Action<VolumeProfile> onCreate;    // 프로필 복사본을 처음 만들 때 도시용으로 손보는 것(이미 있으면 손본 값 유지)
        public Func<VolumeProfile, bool> migrate; // 이미 있는 복사본에서 이전 판 값 그대로인 것만 새 값으로(바꿨으면 true)
    }

    // ── 밤(마지막): Demo_Scene_Lake_3(팩의 밤 데모)를 바탕으로. 2026-11-12 사용자: "밤씬인데 너무 낮인것같아. 밤씬으로 수정해줘".
    // 데모 값 그대로(주변광 ×1 · 달빛 1.6 · LUT 100% · 노출 +0.25 · 대비 30 · Bloom 기준 0.6/세기 1.5 · 비네트 0.375)는 도시에서 실패했다
    // (임시 프로젝트 렌더, 밤 어둠 막까지 얹어 화면 가운데·고리·가장자리 밝기를 원래 도시 밤 113/48/39 와 비교):
    //  - 데모는 달빛을 라이트맵에 구워 썼고(Baked) 도시는 실시간이다. 도시의 밤 어둠은 화면 막(ProtoNightOverlay)이 맡고 막의 '밝은 원' 안은
    //    밑그림이 그대로 보이므로, 밑그림까지 어두우면 캐릭터 둘레 4~6m(좀비가 보여야 하는 거리)가 안 보인다 → 고리 6~20 으로 떨어졌다.
    //  - Night LUT 가 어두운 쪽을 짙은 남색으로 눌러 고리·가장자리를 거의 검게 만들고, 밝은 보도는 네온 청록으로 번졌다(Bloom 0.6).
    // 1판: 주변광 데모 색 ×3 · 데모 달빛 색 1.2 · LUT 30% · Bloom 1/1.5 · 비네트 0.2 · 색수차 0.1 → 147/40/24.
    // 2판(사용자: "전체적으로 볼륨이 쎈것같아. 좀 줄여주고 너무 푸른빛이 많이 도는것같아"):
    //    주변광은 데모 색 ×3 을 60% 무채색 쪽으로 빼고 ×0.85, 달빛은 옅은 푸른 흰색(0.75, 0.82, 0.95) 1.1,
    //    후처리는 LUT 15% · Bloom 기준 1.1/세기 0.4 · 비네트 0.1 · 색수차 0 · 노출·대비 0 → 138/42/27. 고리 B−R ≈ 17(1판 35).
    // 안개는 데모 그대로 짙은 파랑 Exp2 0.0025 — 카메라 거리(18m)에선 거의 안 보이고 먼 곳만 잠긴다.
    const float NightAmbient = 3f, NightDesat = 0.6f, NightAmbientScale = 0.85f;
    static Color NightAmb(float r, float g, float b)
    {
        r *= NightAmbient; g *= NightAmbient; b *= NightAmbient;
        float y = 0.2126f * r + 0.7152f * g + 0.0722f * b;
        float k = NightAmbientScale;
        return new Color(Mathf.Lerp(r, y, NightDesat) * k, Mathf.Lerp(g, y, NightDesat) * k, Mathf.Lerp(b, y, NightDesat) * k, 1f);
    }
    static readonly Preset Night = new Preset
    {
        label = "밤(Demo_Scene_Lake_3 바탕, 2판)",
        ambSky = NightAmb(0.17520082f, 0.15312387f, 0.3490566f),
        ambEquator = NightAmb(0.4374321f, 0.3764706f, 0.81960785f),
        ambGround = NightAmb(0f, 0.28345233f, 0.49056602f),
        fogMode = FogMode.ExponentialSquared, fogColor = new Color(0.09545213f, 0.2191466f, 0.5188679f), fogStart = 20f, fogEnd = 185f, fogDensity = 0.0025f,
        skybox = null, packProfile = "TFF_Night_1",   // 하늘 없음(11-16 사용자: "하늘은 우선 사용안하니깐 지워죠" — 게임 카메라에 안 보였다) · 후처리 복사본은 ToonFantasyAssets.NightProfilePath(Lighting/TFF_City_Night)
        sun = new Color(0.75f, 0.82f, 0.95f), sunIntensity = 1.1f,   // 데모 (0.52, 0.65, 0.91) 1.6(구운 달빛)
        onCreate = p =>
        {
            if (p.TryGet(out ColorLookup lut)) Set(lut.contribution, 0.15f);                      // 데모 1
            if (p.TryGet(out ColorAdjustments ca)) { Set(ca.postExposure, 0f); Set(ca.contrast, 0f); }   // 데모 0.25 · 30
            if (p.TryGet(out Bloom b)) { Set(b.threshold, 1.1f); Set(b.intensity, 0.4f); }        // 데모 0.6 · 1.5
            if (p.TryGet(out Vignette v)) Set(v.intensity, 0.1f);                                 // 데모 0.375
            if (p.TryGet(out ChromaticAberration c)) Set(c.intensity, 0f);                        // 데모 0.1
        },
        migrate = p =>   // 1판 값 그대로인 것만 2판 값으로(직접 손본 값은 둔다)
        {
            bool ch = false;
            if (p.TryGet(out ColorLookup lut)) ch |= From(lut.contribution, 0.3f, 0.15f);
            if (p.TryGet(out Bloom b)) { ch |= From(b.threshold, 1f, 1.1f); ch |= From(b.intensity, 1.5f, 0.4f); }
            if (p.TryGet(out Vignette v)) ch |= From(v.intensity, 0.2f, 0.1f);
            if (p.TryGet(out ChromaticAberration c)) ch |= From(c.intensity, 0.1f, 0f);
            return ch;
        },
    };

    static void Set(VolumeParameter<float> prm, float v) { prm.overrideState = true; prm.value = v; }
    static bool From(VolumeParameter<float> prm, float from, float to)
    {
        if (!Mathf.Approximately(prm.value, from)) return false;
        prm.overrideState = true; prm.value = to;
        return true;
    }

    /// 이름으로 찾는 밤 요소(CityBuilder 가 만드는 이름).
    static readonly string[] NightObjects = { "_GroundFog", "_LightPools" };

    // ================================================================================================ 두 벌

    /// 처음(11월 전) 조명으로. 지금이 원래 조명이 아니면 먼저 지금 것을 마지막 기록(Look_Final)으로 적는다. 저장·Undo 묶기는 부르는 쪽이.
    internal static string ToFirst(Scene scene)
    {
        var orig = AssetDatabase.LoadAssetAtPath<ToonFantasyLookSnapshot>(OriginalPath);
        if (orig == null) { Save(Capture(scene), OriginalPath); return "조명 — 원래 그대로(바꾼 적이 없어 지금 값을 처음 기록으로 적음)"; }
        string note = "";
        if (!IsLike(scene, orig)) { Save(Capture(scene), FinalPath); note = " · 지금 조명을 마지막 기록으로 적음"; }
        return "조명 처음(11월 전 기록 " + orig.takenAt + ") — " + RestoreFrom(orig) + note;
    }

    /// 마지막 조명으로 — 마지막 기록이 있으면 그대로, 없으면 [밤] 프리셋. 처음 기록이 없으면 지금(처음) 값을 먼저 적는다.
    internal static string ToLast(Scene scene)
    {
        var orig = AssetDatabase.LoadAssetAtPath<ToonFantasyLookSnapshot>(OriginalPath);
        if (orig == null) Save(Capture(scene), OriginalPath);
        var fin = AssetDatabase.LoadAssetAtPath<ToonFantasyLookSnapshot>(FinalPath);
        if (fin != null) return "조명 마지막(기록 " + fin.takenAt + ") — " + RestoreFrom(fin);
        return "조명 마지막 — " + ApplyPreset(Night, scene) + " (마지막 기록이 없어 [밤] 프리셋)";
    }

    /// 마지막 조명 기록이 있는가 — 없으면 처음으로 처음 넘어가는 것이다(맵 버전이 그때 씬 백업을 남긴다).
    internal static bool HasFinalRecord => AssetDatabase.LoadAssetAtPath<ToonFantasyLookSnapshot>(FinalPath) != null;

    /// 지금 조명이 처음(원래) 쪽인가 — 전역 볼륨 프로필이 원래 기록과 같으면. 기록이 없으면 바꾼 적이 없으니 처음.
    internal static bool IsFirst(Scene scene)
    {
        var orig = AssetDatabase.LoadAssetAtPath<ToonFantasyLookSnapshot>(OriginalPath);
        return orig == null || IsLike(scene, orig);
    }

    static bool IsLike(Scene scene, ToonFantasyLookSnapshot snap)
    {
        var want = new Dictionary<string, VolumeProfile>();
        foreach (var s in snap.volumes) want[s.id] = s.profile;
        foreach (var v in All<Volume>(scene))
            if (v.isGlobal && want.TryGetValue(Id(v), out var p) && v.sharedProfile != p) return false;
        return Mathf.Approximately(RenderSettings.fogDensity, snap.fogDensity) && RenderSettings.fogMode == (FogMode)snap.fogMode;
    }

    // ================================================================================================ 안쪽

    static string ApplyPreset(Preset p, Scene scene)
    {
        var profile = EnsureProfile(p);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = p.ambSky;
        RenderSettings.ambientEquatorColor = p.ambEquator;
        RenderSettings.ambientGroundColor = p.ambGround;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = p.fogMode;
        RenderSettings.fogColor = p.fogColor;
        RenderSettings.fogStartDistance = p.fogStart;
        RenderSettings.fogEndDistance = p.fogEnd;
        RenderSettings.fogDensity = p.fogDensity;
        var sky = string.IsNullOrEmpty(p.skybox) ? null : ToonFantasyAssets.Find<Material>(p.skybox);
        RenderSettings.skybox = sky;
        int lights = 0, vols = 0;
        foreach (var l in All<Light>(scene))
        {
            if (l.type != LightType.Directional) continue;
            Undo.RecordObject(l, "조명");
            l.color = p.sun; l.intensity = p.sunIntensity;
            lights++;
        }
        if (profile != null)
            foreach (var v in All<Volume>(scene))
            {
                if (!v.isGlobal) continue;
                Undo.RecordObject(v, "후처리");
                v.sharedProfile = profile;
                vols++;
            }
        // 밤 요소(어둠 막 · 바닥 안개 · 빛 웅덩이)는 처음 기록의 상태로(밤 프리셋이라 켠다)
        var orig = AssetDatabase.LoadAssetAtPath<ToonFantasyLookSnapshot>(OriginalPath);
        if (orig != null) SetNightThings(orig);
        DynamicGI.UpdateEnvironment();
        return $"{p.label}: 주변광 3색 · 안개 {p.fogMode} {p.fogDensity} · 방향광 {lights}개 ×{p.sunIntensity} · 전역 볼륨 {vols}개 → {(profile != null ? profile.name : "그대로")}";
    }

    static string RestoreFrom(ToonFantasyLookSnapshot snap)
    {
        RenderSettings.ambientMode = (AmbientMode)snap.ambientMode;
        RenderSettings.ambientSkyColor = snap.ambientSky;
        RenderSettings.ambientEquatorColor = snap.ambientEquator;
        RenderSettings.ambientGroundColor = snap.ambientGround;
        RenderSettings.ambientIntensity = snap.ambientIntensity;
        RenderSettings.skybox = snap.skybox;
        RenderSettings.fog = snap.fog;
        RenderSettings.fogMode = (FogMode)snap.fogMode;
        RenderSettings.fogColor = snap.fogColor;
        RenderSettings.fogStartDistance = snap.fogStart;
        RenderSettings.fogEndDistance = snap.fogEnd;
        RenderSettings.fogDensity = snap.fogDensity;
        var missing = new List<string>();
        foreach (var s in snap.lights)
        {
            var l = Resolve<Light>(s.id);
            if (l == null) { missing.Add(s.name); continue; }
            Undo.RecordObject(l, "조명");
            l.color = s.color; l.intensity = s.intensity;
        }
        foreach (var s in snap.volumes)
        {
            var v = Resolve<Volume>(s.id);
            if (v == null) { missing.Add(s.name); continue; }
            Undo.RecordObject(v, "후처리");
            v.sharedProfile = s.profile;
        }
        missing.AddRange(SetNightThings(snap));
        DynamicGI.UpdateEnvironment();
        var vol = snap.volumes.Count > 0 && snap.volumes[0].profile != null ? snap.volumes[0].profile.name : "?";
        return $"주변광·안개·하늘·해 {snap.lights.Count}개·볼륨 {vol}" + (missing.Count > 0 ? $" · 못 찾은 오브젝트(그대로): {string.Join(", ", missing)}" : "");
    }

    static List<string> SetNightThings(ToonFantasyLookSnapshot snap)
    {
        var missing = new List<string>();
        foreach (var s in snap.nightOverlays)
        {
            var n = Resolve<Proto.ProtoNightOverlay>(s.id);
            if (n == null) { missing.Add(s.name); continue; }
            if (n.enabled != s.on) { Undo.RecordObject(n, "밤 어둠"); n.enabled = s.on; }
        }
        foreach (var s in snap.objects)
        {
            var go = Resolve<GameObject>(s.id);
            if (go == null) { missing.Add(s.name); continue; }
            if (go.activeSelf != s.on) { Undo.RecordObject(go, "밤 요소"); go.SetActive(s.on); }
        }
        return missing;
    }

    static ToonFantasyLookSnapshot Capture(Scene scene)
    {
        var s = ScriptableObject.CreateInstance<ToonFantasyLookSnapshot>();
        s.scenePath = scene.path;
        s.takenAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        s.ambientMode = (int)RenderSettings.ambientMode;
        s.ambientSky = RenderSettings.ambientSkyColor;
        s.ambientEquator = RenderSettings.ambientEquatorColor;
        s.ambientGround = RenderSettings.ambientGroundColor;
        s.ambientIntensity = RenderSettings.ambientIntensity;
        s.skybox = RenderSettings.skybox;
        s.fog = RenderSettings.fog;
        s.fogMode = (int)RenderSettings.fogMode;
        s.fogColor = RenderSettings.fogColor;
        s.fogStart = RenderSettings.fogStartDistance;
        s.fogEnd = RenderSettings.fogEndDistance;
        s.fogDensity = RenderSettings.fogDensity;
        foreach (var l in All<Light>(scene))
            if (l.type == LightType.Directional)
                s.lights.Add(new ToonFantasyLookSnapshot.LightState { id = Id(l), name = l.name, color = l.color, intensity = l.intensity });
        foreach (var v in All<Volume>(scene))
            if (v.isGlobal)
                s.volumes.Add(new ToonFantasyLookSnapshot.VolumeState { id = Id(v), name = v.name, profile = v.sharedProfile });
        foreach (var n in All<Proto.ProtoNightOverlay>(scene))
            s.nightOverlays.Add(new ToonFantasyLookSnapshot.ToggleState { id = Id(n), name = n.name, on = n.enabled });
        foreach (var go in NightGameObjects(scene))
            s.objects.Add(new ToonFantasyLookSnapshot.ToggleState { id = Id(go), name = go.name, on = go.activeSelf });
        return s;
    }

    /// 기록 에셋에 쓴다 — 이미 있으면 내용만 덮는다(GUID 유지).
    static void Save(ToonFantasyLookSnapshot s, string path)
    {
        EnsureFolder();
        var have = AssetDatabase.LoadAssetAtPath<ToonFantasyLookSnapshot>(path);
        if (have == null) AssetDatabase.CreateAsset(s, path);
        else { EditorUtility.CopySerialized(s, have); have.name = Path.GetFileNameWithoutExtension(path); EditorUtility.SetDirty(have); Object.DestroyImmediate(s); }
        AssetDatabase.SaveAssets();
    }

    /// 팩 후처리 프로필의 복사본. 원본 팩 에셋은 안 건드린다. 이미 있으면 그대로 쓰되(손본 값 유지) 이전 판 값만 옮긴다.
    static VolumeProfile EnsureProfile(Preset preset)
    {
        string cityProfile = ToonFantasyAssets.NightProfilePath;
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(cityProfile);
        if (p == null)
        {
            var pack = ToonFantasyAssets.Find<VolumeProfile>(preset.packProfile);
            if (pack == null)
            {
                Debug.LogWarning($"[맵 버전] 팩 후처리 프로필 {preset.packProfile} 이 없다 — 후처리는 그대로 둔다.");
                return null;
            }
            EnsureFolder();
            string dir = Path.GetDirectoryName(cityProfile).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace('\\', '/'), Path.GetFileName(dir));
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(pack), cityProfile);
            p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(cityProfile);
            if (p == null) return null;
            if (preset.onCreate != null)
            {
                preset.onCreate(p);
                foreach (var c in p.components) EditorUtility.SetDirty(c);
                EditorUtility.SetDirty(p);
                AssetDatabase.SaveAssets();
            }
        }
        else if (preset.migrate != null)
        {
            foreach (var c in p.components) Undo.RecordObject(c, "후처리 값 갱신");
            if (preset.migrate(p))
            {
                foreach (var c in p.components) EditorUtility.SetDirty(c);
                EditorUtility.SetDirty(p);
                AssetDatabase.SaveAssets();
            }
        }
        return p;
    }

    static void EnsureFolder()
    {
        string root = ToonFantasyCitySwap.Root;
        if (!AssetDatabase.IsValidFolder(root))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(root).Replace('\\', '/'), Path.GetFileName(root));
    }

    static IEnumerable<T> All<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren<T>(true))
                if ((c.gameObject.hideFlags & HideFlags.DontSaveInEditor) == 0) yield return c;
    }

    static IEnumerable<GameObject> NightGameObjects(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (Array.IndexOf(NightObjects, t.name) >= 0) yield return t.gameObject;
    }

    static string Id(Object o) => GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();

    static T Resolve<T>(string id) where T : Object
    {
        if (string.IsNullOrEmpty(id) || !GlobalObjectId.TryParse(id, out var gid)) return null;
        return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as T;
    }
}
