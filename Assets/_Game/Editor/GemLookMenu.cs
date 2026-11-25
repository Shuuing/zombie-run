using System.IO;
using Proto;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 경험치 젬 모양 버전 메뉴(2026-11-18 사용자 "물고기 1, 2 버전으로 나눠서 적용할 수 있게").
/// <c>Tools/좀비 오버런/젬 모양/1 물고기</c> · <c>2 금색 물고기</c> — <c>Resources/Data/GemLook.asset</c> 의 버전만 바꾼다(체크 = 지금 버전).
/// 씬은 건드리지 않는다. 플레이 중에 바꾸면 다음에 떨어지는 젬부터.
/// <c>금색 물고기 메시 · 머티리얼 다시 만들기</c> — <see cref="GoldFishMesh"/> 로 메시 에셋을 다시 굽는다(GUID 유지). 머티리얼은 없을 때만 만든다(손본 값 보존).
/// </summary>
public static class GemLookMenu
{
    public const string AssetPath = "Assets/_Game/Resources/Data/GemLook.asset";
    public const string FishDir = "Assets/_Game/Art/Pickups/GoldFish";
    public const string MeshPath = FishDir + "/GoldFish_Mesh.asset";
    public const string MatPath = FishDir + "/M_GoldFish.mat";

    const string MenuFish = "Tools/좀비 오버런/젬 모양/1 물고기";
    const string MenuGold = "Tools/좀비 오버런/젬 모양/2 금색 물고기";

    [MenuItem(MenuFish, false, 1)]
    static void SetFish() => SetVersion(ProtoGemLook.Version.Fish);

    [MenuItem(MenuFish, true)]
    static bool SetFishCheck() { Menu.SetChecked(MenuFish, CurrentInAsset() == ProtoGemLook.Version.Fish); return true; }

    [MenuItem(MenuGold, false, 2)]
    static void SetGold() => SetVersion(ProtoGemLook.Version.GoldFish);

    [MenuItem(MenuGold, true)]
    static bool SetGoldCheck() { Menu.SetChecked(MenuGold, CurrentInAsset() == ProtoGemLook.Version.GoldFish); return true; }

    [MenuItem("Tools/좀비 오버런/젬 모양/금색 물고기 메시 · 머티리얼 다시 만들기", false, 20)]
    public static void BakeGoldFish()
    {
        EnsureFolder(FishDir);

        // 메시 — 이미 있으면 내용만 덮어써 GUID(참조)를 지킨다
        var built = GoldFishMesh.Build();
        built.name = "GoldFish_Mesh";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null) { AssetDatabase.CreateAsset(built, MeshPath); mesh = built; }
        else { EditorUtility.CopySerialized(built, mesh); mesh.name = "GoldFish_Mesh"; EditorUtility.SetDirty(mesh); Object.DestroyImmediate(built); }

        // 머티리얼 — 없을 때만(아트가 색을 손봤으면 그대로 둔다)
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            var sh = Shader.Find("Proto/GoldFish");
            if (sh == null) { Debug.LogError("[젬 모양] Proto/GoldFish 셰이더를 못 찾았다 — Art/Shaders/Proto_GoldFish.shader 확인"); return; }
            mat = new Material(sh) { name = "M_GoldFish" };
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        var look = LoadOrCreate();
        look.goldFishMesh = mesh;
        look.goldFishMaterial = mat;
        look.goldFishAspect = GoldFishMesh.Aspect;   // 메시 몸 비율과 짝(2판 1.3)
        EditorUtility.SetDirty(look);
        AssetDatabase.SaveAssets();
        ProtoGemLook.Reload();
        Debug.Log($"[젬 모양] 금색 물고기 메시 {mesh.vertexCount}정점 · 머티리얼 {MatPath} → {AssetPath}");
    }

    /// batchmode 진입점(임시 프로젝트에서 굽기).
    public static void BakeGoldFishBatch()
    {
        // 임시 프로젝트에서 셰이더 기본값을 바꾼 뒤 굽는 길 — 머티리얼 값을 셰이더 기본값으로 되돌린다(GUID 는 그대로)
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        var sh = Shader.Find("Proto/GoldFish");
        if (mat != null && sh != null)
        {
            var fresh = new Material(sh);
            mat.shader = sh;
            mat.CopyPropertiesFromMaterial(fresh);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(mat);
        }
        BakeGoldFish();
    }

    static void SetVersion(ProtoGemLook.Version v)
    {
        var look = LoadOrCreate();
        if (v == ProtoGemLook.Version.GoldFish && (look.goldFishMesh == null || look.goldFishMaterial == null)) BakeGoldFish();
        Undo.RecordObject(look, "젬 모양 버전");
        look.version = v;
        EditorUtility.SetDirty(look);
        AssetDatabase.SaveAssets();
        ProtoGemLook.Reload();
        Debug.Log($"[젬 모양] {(v == ProtoGemLook.Version.Fish ? "1 물고기" : "2 금색 물고기")} — 다음에 떨어지는 젬부터 ({AssetPath})");
    }

    static ProtoGemLook.Version? CurrentInAsset()
    {
        var look = AssetDatabase.LoadAssetAtPath<ProtoGemLook>(AssetPath);
        return look != null ? look.version : (ProtoGemLook.Version?)null;
    }

    static ProtoGemLook LoadOrCreate()
    {
        var look = AssetDatabase.LoadAssetAtPath<ProtoGemLook>(AssetPath);
        if (look != null) return look;
        EnsureFolder(Path.GetDirectoryName(AssetPath).Replace('\\', '/'));
        look = ScriptableObject.CreateInstance<ProtoGemLook>();
        AssetDatabase.CreateAsset(look, AssetPath);
        return look;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
