using UnityEditor;
using UnityEngine;

/// <summary>
/// 브레이커(cha_02) 머리 흔들림 켜기/끄기 — 2026-10-21 사용자: "보고 이상하면 원래대로 되돌릴 수 있게".
/// Cha02_Rifle 프리팹의 ProtoHairSpring 을 켜고 끈다. 꺼도 머리카락 뼈는 머리에 고정돼 원래 모습 그대로다.
/// 뼈까지 완전히 되돌리려면 ArtSource/character_02/cha_02_rigged_before_hair.fbx 로 cha_02_rigged.fbx 를 덮어쓴다.
/// </summary>
public static class BreakerHairSpringMenu
{
    const string PrefabPath = "Assets/_Game/Art/character/character_02/Cha02_Rifle.prefab";
    const string Root = "Tools/좀비 오버런/브레이커 머리 흔들림/";

    [MenuItem(Root + "켜기")] static void On() => Set(true);
    [MenuItem(Root + "끄기")] static void Off() => Set(false);

    [MenuItem(Root + "켜기", true)] static bool OnValid() => State() != true;   // 아직 안 붙었거나(null) 꺼져 있으면
    [MenuItem(Root + "끄기", true)] static bool OffValid() => State() == true;

    static bool? State()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var c = go != null ? go.GetComponent<Proto.ProtoHairSpring>() : null;
        return c == null ? (bool?)null : c.enabled;
    }

    static void Set(bool on)
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var c = root.GetComponent<Proto.ProtoHairSpring>();
        if (c == null) c = root.AddComponent<Proto.ProtoHairSpring>();
        c.enabled = on;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[머리 흔들림] 브레이커 {(on ? "켜짐" : "꺼짐")}");
    }
}
