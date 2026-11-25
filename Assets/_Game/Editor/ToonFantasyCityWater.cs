using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Proto_City_Test 의 물웅덩이(_City/_Puddles, 원래 BCM/BCM_StylizedWater + M_Water_Shallow_Test) 두 벌 — 맵 두 벌 전환(<see cref="CityMapVersions"/>,
/// Tools/좀비 오버런/맵 버전/)의 물 부분. 메뉴는 없다.
///
/// <para><b>처음(11월 전)</b> = 원래 물 머티리얼(그리는 순서 3000 — 밤 어둠 막 위라 밤에도 웅덩이가 밝다).</para>
/// <para><b>마지막</b> = 원래 물과 같은 셰이더·같은 값(계단 진 청록·물결 선·흰 물가)인데 그리는 순서만 밤 어둠 막 아래(<see cref="WaterQueue"/>).
/// 2026-11-12 사용자가 데모 물(Toon Fantasy Puddle)을 보고 "너무 바다 처럼 반짝이는 텍스쳐가 너무 많은것같아" → 이 그림을 골랐다.
/// 복사본 <c>WaterMaterials/&lt;원본&gt;_Night.mat</c> — 갈 때마다 원본 값으로 다시 맞춘다(원본을 고치면 따라온다). .meta userData 에 원본(guid:localId)을 적어
/// 처음으로 갈 때 기록 파일 없이 되돌린다.</para>
/// 11-13 정리: 데모 물 그림(Toon/City/TFF_ToonWater · <c>_TFF.mat</c>)과 세 메뉴는 뺐다. 11-16 정리로 그 셰이더 파일도 지웠다.
/// CityBuilder 로 도시를 다시 지으면 웅덩이는 원래 물로 새로 생긴다 — 그때는 [마지막] 을 다시 누른다.
/// </summary>
public static class ToonFantasyCityWater
{
    const string Root = ToonFantasyCitySwap.Root;
    const string MatDir = Root + "/WaterMaterials";
    const string SrcShader = "BCM/BCM_StylizedWater";
    const string PuddleRoot = "_Puddles";
    const string Tag = "tff-water-src:";   // 복사본 .meta userData = "tff-water-src:<원본 guid>:<localId>"
    // 그리는 순서 — 밤 어둠 막(ProtoNightOverlay, Transparent-100 = 2900) 바로 아래. 2026-11-12 사용자 "어둠 아래로":
    // 원본 물(3000)은 막 위라 밤에도 웅덩이만 환했다. 바닥 데칼·그림자(2455·2470) 뒤, 탱크 자국(2899)·가로등 빛 웅덩이(2990, 가산) 앞.
    const int WaterQueue = 2890;

    /// 마지막 — 웅덩이 칸을 원래 그림 · 밤 어둠 아래 복사본으로. 저장·Undo 묶기는 부르는 쪽이.
    internal static string ToLast(Scene scene)
    {
        var targets = PuddleRenderers(scene);
        if (targets.Count == 0) return "물웅덩이 없음";
        var index = IndexCopies();
        int slots = 0, created = 0, lost = 0;
        foreach (var r in targets)
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                var src = SourceOf(m, out bool isCopy);
                if (!isCopy) { if (m.shader == null || m.shader.name != SrcShader) continue; src = m; }
                if (src == null) { lost++; continue; }   // 원본을 못 찾은 복사본
                var w = GetOrCreateOriginal(src, index, ref created);
                if (w == null || w == m) continue;
                mats[i] = w; changed = true; slots++;
            }
            if (!changed) continue;
            Undo.RecordObject(r, "물웅덩이");
            r.sharedMaterials = mats;
        }
        AssetDatabase.SaveAssets();
        return $"물웅덩이 원래 그림 · 밤 어둠 아래({WaterQueue}) — 칸 {slots}개 바꿈" + (created > 0 ? $"(복사본 새로 {created})" : "") + (lost > 0 ? $" · 원본 못 찾은 칸 {lost}" : "");
    }

    /// 처음 — 물 복사본 칸(이 도구가 만든 것 전부 — 예전 데모 물 복사본 포함)을 원래 물 머티리얼로.
    internal static string ToFirst(Scene scene)
    {
        int slots = 0, missing = 0;
        var cache = new Dictionary<Material, Material>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (!cache.TryGetValue(m, out var orig)) { orig = SourceOf(m, out bool isCopy); cache[m] = orig; if (isCopy && orig == null) missing++; }
                    if (orig == null) continue;
                    mats[i] = orig; changed = true; slots++;
                }
                if (!changed) continue;
                Undo.RecordObject(r, "물웅덩이");
                r.sharedMaterials = mats;
            }
        return $"물웅덩이 원래 물(밤 어둠 위) — 칸 {slots}개 바꿈" + (missing > 0 ? $" · 원본 못 찾은 복사본 {missing}" : "");
    }

    /// 웅덩이 칸이 모두 원래 물인가(처음).
    internal static bool IsFirst(Scene scene)
    {
        foreach (var r in PuddleRenderers(scene))
            foreach (var m in r.sharedMaterials)
                if (m != null) { SourceOf(m, out bool isCopy); if (isCopy) return false; }
        return true;
    }

    // ------------------------------------------------------------------------------------------------

    static List<MeshRenderer> PuddleRenderers(Scene scene)
    {
        var list = new List<MeshRenderer>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == PuddleRoot) list.AddRange(t.GetComponentsInChildren<MeshRenderer>(true));
        return list;
    }

    /// 복사본을 "원본 키|셰이더 이름" 으로 묶는다 — 한 원본에 그림마다 복사본이 하나씩 있다.
    static Dictionary<string, Material> IndexCopies()
    {
        var d = new Dictionary<string, Material>();
        if (!AssetDatabase.IsValidFolder(MatDir)) return d;
        foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var imp = AssetImporter.GetAtPath(p);
            if (imp == null || imp.userData == null || !imp.userData.StartsWith(Tag)) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m != null && m.shader != null) d[imp.userData.Substring(Tag.Length) + "|" + m.shader.name] = m;
        }
        return d;
    }

    static string KeyOf(Material src) =>
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(src, out string guid, out long lid) ? $"{guid}:{lid}" : null;   // 씬 안 머티리얼은 null

    static Material CreateCopy(Material m, Material src, string key, string suffix, Dictionary<string, Material> index, ref int created)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Root, "WaterMaterials");
        string path = AssetDatabase.GenerateUniqueAssetPath($"{MatDir}/{src.name}{suffix}.mat");
        AssetDatabase.CreateAsset(m, path);
        var imp = AssetImporter.GetAtPath(path);
        imp.userData = Tag + key;
        imp.SaveAndReimport();
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        index[key + "|" + m.shader.name] = m;
        created++;
        return m;
    }

    /// 원래 그림 — 원본과 같은 셰이더·값·키워드, 순서만 밤 어둠 막 아래. 누를 때마다 원본 값으로 다시 맞춘다.
    static Material GetOrCreateOriginal(Material src, Dictionary<string, Material> index, ref int created)
    {
        string key = KeyOf(src);
        if (key == null) return null;
        if (!index.TryGetValue(key + "|" + SrcShader, out var m) || m == null)
            m = CreateCopy(new Material(src) { name = src.name + "_Night" }, src, key, "_Night", index, ref created);
        m.shader = src.shader;
        m.CopyPropertiesFromMaterial(src);   // 값·키워드·순서까지 원본 그대로 — 순서만 아래에서 바꾼다
        m.shaderKeywords = src.shaderKeywords;
        m.renderQueue = WaterQueue;
        EditorUtility.SetDirty(m);
        return m;
    }


    /// 물 복사본이면 원본 머티리얼(못 찾으면 null), 아니면 null + isCopy false.
    static Material SourceOf(Material m, out bool isCopy)
    {
        isCopy = false;
        string p = AssetDatabase.GetAssetPath(m);
        if (string.IsNullOrEmpty(p)) return null;
        var imp = AssetImporter.GetAtPath(p);
        if (imp == null || imp.userData == null || !imp.userData.StartsWith(Tag)) return null;
        isCopy = true;
        string key = imp.userData.Substring(Tag.Length);
        int c = key.IndexOf(':');
        if (c < 0 || !long.TryParse(key.Substring(c + 1), out long id)) return null;
        string path = AssetDatabase.GUIDToAssetPath(key.Substring(0, c));
        if (string.IsNullOrEmpty(path)) return null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Material mm && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mm, out _, out long l) && l == id) return mm;
        return null;
    }
}
