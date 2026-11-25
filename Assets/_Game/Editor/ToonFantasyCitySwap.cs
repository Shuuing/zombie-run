using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Toon Fantasy Nature 팩의 톤 셰이더를 도시 테스트 씬(Proto_City_Test)에 입히고(<see cref="ToToon"/>), 원래 BCM 셰이더로 되돌린다(<see cref="ToBcm"/>).
/// 2026-11-12 사용자 요청: "Toon Fantasy Nature 셰이더를 Proto_City_Test 에 적용, 기존 걸로 복구할 수 있게".
/// 11-13 정리: 메뉴는 없다 — 맵 두 벌 전환(<see cref="CityMapVersions"/>, Tools/좀비 오버런/맵 버전/)의 한 부분이다(처음 = BCM, 마지막 = 톤).
/// 외곽선 켜기·끄기 메뉴와 외곽선 없는 톤 셰이더는 뺐다(11-16) — 불투명 복사본은 늘 외곽선 셰이더다.
///
/// <para><b>원본 머티리얼은 건드리지 않는다.</b> BCM 머티리얼마다 톤 복사본(<c>…_TFF.mat</c>)을 만들어
/// <b>씬 렌더러의 머티리얼 칸만</b> 갈아 끼운다. 같은 머티리얼을 쓰는 다른 씬(필드·챕터 01)은 그대로다.
/// 복사본 .meta 의 userData 에 원본 GUID 를 적어 두므로, [원래대로] 는 기록 파일 없이 씬만 훑어 되돌린다
/// (적용 뒤에 복제한 오브젝트도 같이 돌아온다). 프리팹 인스턴스는 되돌린 칸의 오버라이드를 걷어 원래 상태로 둔다.</para>
///
/// <para>적용 직전 씬 파일을 <c>Assets/_Game/Scenes/Backup~/</c> 에 복사해 둔다(~ 폴더라 Unity 가 임포트하지 않는다).
/// 메뉴로 못 돌아오는 일이 생기면 그 파일을 씬 자리에 덮어쓰면 된다.</para>
///
/// <para>대상: 씬의 MeshRenderer 중 머티리얼 셰이더가 BCM/BCM_Toony_shader 인 칸. 제외: 캐릭터·무기(부모에 Animator),
/// 발광 머티리얼(가로등 전구·간판 — _EMISSION 켜짐), 숨은 런타임 오브젝트. 풀잎·물·데칼·빛 웅덩이는 전용 셰이더라 원래 대상이 아니다.
/// 알파 컷아웃(나무 잎 카드·덤불)은 식생 셰이더, 나머지는 외곽선 셰이더(메뉴로 끌 수 있다).</para>
///
/// 셰이더는 팩 원본이 아니라 <c>Assets/_Game/Art/Sujin/Shaders/Toon</c>(11-20 이름 Sujin/Toon · 11-23 자리 옮김) 의 복사본이다 — 조명 계산은 원본 그대로,
/// 색 곱·발광·컷아웃 값·컬링·투시(ProtoSeeThrough)·페이드 블렌드만 덧붙였다(그 폴더 README.md).
/// CityBuilder 로 도시를 다시 만들면 BCM 으로 돌아온다(그때는 [적용] 을 다시 실행).
/// </summary>
public static class ToonFantasyCitySwap
{
    internal const string Menu = "Tools/좀비 오버런/맵 버전/";   // 로그 문구용(메뉴는 CityMapVersions)
    const string ScenePath = "Assets/_Game/Scenes/Proto_City_Test.unity";
    const string BackupDir = "Assets/_Game/Scenes/Backup~";
    internal const string Root = "Assets/_Game/Art/Sujin/S_City";   // 11-23 사용자가 폴더를 옮기고 이름 바꿈(옛 Art/ToonFantasyCity)
    const string MatDir = Root + "/Materials";
    const string Tag = "tff-src:";   // 복사본 .meta userData = "tff-src:<원본 guid>:<localId>"

    const string BcmShader = "BCM/BCM_Toony_shader";
    const string ShOutline = "Sujin/Toon/Outline";            // 옛 이름 Toon/City/TFF_CustomToonOutline (11-20 이름 바꿈)
    const string ShVeg = "Sujin/Toon/Vegetation";              // 옛 이름 Toon/City/TFF_CustomToonVegetation

    // 팩 기준 머티리얼(램프·외곽선·바람 값을 여기서 가져온다)
    const string RefSolid = "TFF_Atlas_1A_D";
    const string RefVeg = "TFF_Atlas_Vegetation_1A_D";


    // ------------------------------------------------------------------------------------------------ 메뉴

    /// 씬의 BCM 머티리얼 칸을 톤 복사본으로(없으면 만든다). 저장·Undo 묶기는 부르는 쪽(CityMapVersions)이.
    internal static string ToToon(Scene scene)
    {
        var sh = LoadShaders();
        if (sh == null) return "톤 셰이더 없음 — 그대로";
        var refSolid = FindPackMaterial(RefSolid);
        var refVeg = FindPackMaterial(RefVeg);
        var index = IndexToonMaterials();
        int slots = 0, renderers = 0, created = 0;
        foreach (var r in SceneRenderers(scene))
        {
            if (r.GetComponentInParent<Animator>(true) != null) continue;   // 캐릭터·무기
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null || m.shader == null || m.shader.name != BcmShader) continue;
                if (m.IsKeywordEnabled("_EMISSION")) continue;   // 가로등 전구·간판은 발광 그대로
                var toon = GetOrCreateToon(m, sh, refSolid, refVeg, index, ref created);
                if (toon == null) continue;
                mats[i] = toon; changed = true; slots++;
            }
            if (!changed) continue;
            Undo.RecordObject(r, "셰이더 톤");
            r.sharedMaterials = mats;
            if (PrefabUtility.IsPartOfPrefabInstance(r)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            renderers++;
        }
        if (created > 0) AssetDatabase.SaveAssets();
        return $"셰이더 톤 — 렌더러 {renderers}개 · 칸 {slots}개" + (created > 0 ? $"(복사본 새로 {created}개)" : "");
    }

    /// 씬의 톤 복사본 칸을 원래 BCM 머티리얼로. 프리팹 인스턴스는 원본과 같아진 칸의 오버라이드를 걷는다. 복사본은 남는다(다시 톤으로 갈 때 그대로 쓴다).
    internal static string ToBcm(Scene scene)
    {
        var cache = new Dictionary<Material, Material>();
        int slots = 0, renderers = 0, missing = 0;
        foreach (var r in SceneRenderers(scene))
        {
            var mats = r.sharedMaterials;
            var back = new List<int>();
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (!cache.TryGetValue(m, out var orig)) { orig = SourceOf(m); cache[m] = orig; }
                if (orig == null) { if (IsToonCopy(m)) missing++; continue; }
                mats[i] = orig; back.Add(i); slots++;
            }
            if (back.Count == 0) continue;
            Undo.RecordObject(r, "셰이더 BCM");
            r.sharedMaterials = mats;
            if (PrefabUtility.IsPartOfPrefabInstance(r))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                var src = PrefabUtility.GetCorrespondingObjectFromSource(r) as Renderer;   // 프리팹 원본과 같아진 칸은 오버라이드를 걷는다
                if (src != null)
                {
                    var srcMats = src.sharedMaterials;
                    var so = new SerializedObject(r);
                    foreach (int i in back)
                    {
                        if (i >= srcMats.Length || srcMats[i] != mats[i]) continue;
                        var p = so.FindProperty($"m_Materials.Array.data[{i}]");
                        if (p != null && p.prefabOverride) PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
                    }
                }
            }
            renderers++;
        }
        return $"셰이더 BCM — 렌더러 {renderers}개 · 칸 {slots}개" + (missing > 0 ? $"(원본 못 찾은 칸 {missing}개는 톤 그대로 — Backup~ 씬 백업으로)" : "");
    }

    /// 씬이 톤 상태인가 — 도시 바닥(_City/Ground)의 잔디 아닌 칸이 톤 복사본이면.
    internal static bool SceneIsToon(GameObject city)
    {
        var r = city != null ? city.transform.Find("Ground")?.GetComponent<MeshRenderer>() : null;
        if (r == null) return false;
        var ms = r.sharedMaterials;
        for (int i = 0; i < ms.Length; i++)
            if (i != ToonFantasyCityFlora.LawnSubmesh && IsToon(ms[i])) return true;
        return false;
    }

    // ------------------------------------------------------------------------------------------------ 씬

    internal static bool OpenCityScene(out Scene scene)
    {
        scene = SceneManager.GetActiveScene();
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Toon Fantasy 셰이더", "플레이 중에는 못 한다 — 플레이를 멈추고 실행.", "확인"); return false; }
        if (scene.path == ScenePath) return true;
        if (!EditorUtility.DisplayDialog("Toon Fantasy 셰이더", $"{Path.GetFileNameWithoutExtension(ScenePath)} 을 열고 진행할까?", "열기", "취소")) return false;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return scene.IsValid();
    }

    static IEnumerable<MeshRenderer> SceneRenderers(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                if ((r.gameObject.hideFlags & (HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy)) == 0)
                    yield return r;
    }

    /// 적용 직전 씬 파일 사본. 저장 안 된 수정이 있으면 먼저 저장할지 묻는다(취소하면 디스크 기준으로 복사).
    internal static string BackupScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.isDirty) EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        string proj = Path.GetDirectoryName(Application.dataPath);
        string dir = Path.Combine(proj, BackupDir);
        Directory.CreateDirectory(dir);
        string stem = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(ScenePath)}_beforeTFF_{DateTime.Now:yyyyMMdd_HHmmss}");
        string dst = stem + ".unity";
        for (int i = 2; File.Exists(dst); i++) dst = $"{stem}_{i}.unity";   // 같은 초에 두 번(셰이더 → 조명 연달아) 눌러도 안 겹치게
        File.Copy(Path.Combine(proj, ScenePath), dst, false);
        return dst.Replace('\\', '/');
    }

    // ------------------------------------------------------------------------------------------------ 머티리얼

    class Shaders { public Shader outline, veg; }   // 외곽선 없는 톤(Toon/City/TFF_CustomToon)은 11-16 정리로 지웠다 — 불투명은 늘 외곽선 셰이더

    static Shaders LoadShaders()
    {
        var s = new Shaders { outline = Shader.Find(ShOutline), veg = Shader.Find(ShVeg) };
        if (s.outline != null && s.veg != null) return s;
        EditorUtility.DisplayDialog("Toon Fantasy 셰이더",
            $"도시용 톤 셰이더를 못 찾았다:\n{ShOutline}\n{ShVeg}\n\n{Root}/Shaders 가 임포트됐는지, 콘솔에 셰이더 오류가 없는지 확인.", "확인");
        return null;
    }

    static Material FindPackMaterial(string name)
    {
        var m = ToonFantasyAssets.Find<Material>(name);   // 팩 원래 자리든 ToonFantasyCity/Materials/Pack 이든
        if (m == null) Debug.LogWarning($"[맵 버전] 팩 머티리얼 {name} 을 못 찾았다 — 램프·외곽선·바람은 셰이더 기본값.");
        return m;
    }

    /// 톤 복사본 폴더의 머티리얼을 원본 키(guid:localId)로 묶는다.
    static Dictionary<string, Material> IndexToonMaterials()
    {
        var d = new Dictionary<string, Material>();
        if (!AssetDatabase.IsValidFolder(MatDir)) return d;
        foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var imp = AssetImporter.GetAtPath(p);
            if (imp == null || imp.userData == null || !imp.userData.StartsWith(Tag)) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m != null) d[imp.userData.Substring(Tag.Length)] = m;
        }
        return d;
    }

    static string KeyOf(Material src)
    {
        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(src, out string guid, out long id) ? $"{guid}:{id}" : null;
    }

    static bool IsToonCopy(Material m)
    {
        string p = AssetDatabase.GetAssetPath(m);
        if (string.IsNullOrEmpty(p) || !p.StartsWith(MatDir)) return false;
        var imp = AssetImporter.GetAtPath(p);
        return imp != null && imp.userData != null && imp.userData.StartsWith(Tag);
    }

    /// 톤 복사본이면 원본(BCM) 머티리얼, 아니면 그대로 — 풀 두 벌 전환(CityGrassVersions)이 쓴다.
    internal static Material OriginalOf(Material m) => m == null ? null : SourceOf(m) ?? m;

    /// 원본 머티리얼의 톤 복사본(이미 만든 것만 — 새로 만들지 않는다). 없으면 null.
    internal static Material ToonCopyOf(Material src)
    {
        string key = src != null ? KeyOf(src) : null;
        return key != null && IndexToonMaterials().TryGetValue(key, out var m) ? m : null;
    }

    /// 머티리얼이 이 도구가 만든 톤 복사본인가.
    internal static bool IsToon(Material m) => m != null && IsToonCopy(m);

    /// 톤 복사본이면 원본 머티리얼, 아니면 null.
    static Material SourceOf(Material toon)
    {
        if (!IsToonCopy(toon)) return null;
        string key = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(toon)).userData.Substring(Tag.Length);
        int c = key.IndexOf(':');
        if (c < 0) return null;
        string path = AssetDatabase.GUIDToAssetPath(key.Substring(0, c));
        if (string.IsNullOrEmpty(path) || !long.TryParse(key.Substring(c + 1), out long id)) return null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))   // FBX 안에 박힌 머티리얼도 찾는다
            if (o is Material m && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out _, out long lid) && lid == id) return m;
        return null;
    }

    static Material GetOrCreateToon(Material src, Shaders sh, Material refSolid, Material refVeg,
                                    Dictionary<string, Material> index, ref int created)
    {
        string key = KeyOf(src);
        if (key == null) return null;   // 씬 안에만 있는 머티리얼(에셋 아님)은 건너뛴다
        if (index.TryGetValue(key, out var have) && have != null) return have;   // 이미 있으면 그대로(손본 값 유지)

        bool cutout = src.IsKeywordEnabled("_ALPHATEST_ON");
        // 불투명은 외곽선 셰이더로 만들어 외곽선 값까지 채운 뒤 바꿔 끼운다 — 머티리얼은 셰이더를 바꿔도
        // 저장된 값을 들고 있으므로, 나중에 [외곽선 켜기] 를 해도 팩 값(폭·색)이 그대로 살아난다.
        var m = new Material(cutout ? sh.veg : sh.outline) { name = src.name + "_TFF" };
        Fill(m, src, cutout, cutout ? refVeg : refSolid);

        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder(Path.GetDirectoryName(Root).Replace('\\', '/'), Path.GetFileName(Root));
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Root, "Materials");
        string path = AssetDatabase.GenerateUniqueAssetPath($"{MatDir}/{Sanitize(src.name)}_TFF.mat");
        AssetDatabase.CreateAsset(m, path);
        var imp = AssetImporter.GetAtPath(path);
        imp.userData = Tag + key;
        imp.SaveAndReimport();
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        index[key] = m;
        created++;
        return m;
    }

    /// BCM 값 → 팩 셰이더 값. 램프·외곽선·바람 노이즈는 팩 기준 머티리얼에서.
    static void Fill(Material m, Material src, bool cutout, Material packRef)
    {
        if (packRef != null)
        {
            CopyTex(packRef, m, "_TextureRamp");
            CopyFloat(packRef, m, "_OutlineWidth");
            CopyColor(packRef, m, "_OutlineColor");
            CopyTex(packRef, m, "_WindNoiseTexture");
            CopyFloat(packRef, m, "_WindScroll");
            CopyFloat(packRef, m, "_WindJitter");
        }
        if (m.HasProperty("_TextureRamp") && m.GetTexture("_TextureRamp") == null)
        {
            var ramp = FindTexture("TFF_Toon_Ramp_1B");
            if (ramp != null) m.SetTexture("_TextureRamp", ramp);
        }
        if (src.HasProperty("_BaseMap"))
        {
            m.SetTexture("_TextureSample", src.GetTexture("_BaseMap"));
            m.SetTextureScale("_TextureSample", src.GetTextureScale("_BaseMap"));
            m.SetTextureOffset("_TextureSample", src.GetTextureOffset("_BaseMap"));
        }
        if (src.HasProperty("_BaseColor")) m.SetColor("_BaseColor", src.GetColor("_BaseColor"));
        m.SetColor("_EmissionColor", src.IsKeywordEnabled("_EMISSION") && src.HasProperty("_EmissionColor") ? src.GetColor("_EmissionColor") : Color.black);
        if (src.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", src.GetFloat("_Cutoff"));
        if (src.HasProperty("_Cull")) m.SetFloat("_Cull", src.GetFloat("_Cull"));

        bool see = src.IsKeywordEnabled("BCM_SEE_THROUGH");
        m.SetFloat("_UseSeeThrough", see ? 1f : 0f);
        if (see) m.EnableKeyword("BCM_SEE_THROUGH"); else m.DisableKeyword("BCM_SEE_THROUGH");

        if (cutout)
        {
            m.EnableKeyword("_ALPHATEST_ON");   // ProtoSeeThrough 가 이 키워드로 잎 카드(디더)를 알아본다
            // 바람: 팩 셰이더는 노이즈 × 정점색. BCM 바람이 켜져 있던 것만 같은 세기로, 아니면 멈춤
            m.SetFloat("_WindStrength", src.IsKeywordEnabled("BCM_WIND_SWAY") && src.HasProperty("_WindStrength") ? src.GetFloat("_WindStrength") : 0f);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        m.SetFloat("_SrcBlend", 1f);
        m.SetFloat("_DstBlend", 0f);
        m.SetFloat("_ZWrite", 1f);
        m.enableInstancing = src.enableInstancing;
        m.doubleSidedGI = src.doubleSidedGI;
    }

    static void CopyTex(Material a, Material b, string p) { if (a.HasProperty(p) && b.HasProperty(p)) b.SetTexture(p, a.GetTexture(p)); }
    static void CopyFloat(Material a, Material b, string p) { if (a.HasProperty(p) && b.HasProperty(p)) b.SetFloat(p, a.GetFloat(p)); }
    static void CopyColor(Material a, Material b, string p) { if (a.HasProperty(p) && b.HasProperty(p)) b.SetColor(p, a.GetColor(p)); }

    static Texture FindTexture(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(g));
            if (t != null && t.name == name) return t;
        }
        return null;
    }

    static string Sanitize(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace('#', '_').Trim();
    }
}
