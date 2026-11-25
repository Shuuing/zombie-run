using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 도시 맵 두 벌 — 메뉴 <c>Tools/좀비 오버런/맵 버전/</c> [처음 (11월 전)] · [마지막]. 지금 쪽에 체크 표시.
/// 2026-11-13 사용자: "내가 말한 처음 버전은 나무랑 꽃이랑 등등, 맵 전체지역에 있는 걸 다 말하는거야. 툴에있는 다른 버전은 정리해주고,
/// 두가지 버전만 만들어줘. 처음과 마지막버전으로".
///
/// <para>
/// | 부분 | 처음(11월 전) | 마지막 |
/// | 셰이더 | BCM 원본 | 톤 복사본(ToonFantasyCitySwap) |
/// | 조명·안개·후처리 | Look_Original(11-12 처음 바꾸기 직전 기록, Unity 기본 하늘) | Look_Final(마지막에서 넘어갈 때 기록, 없으면 [밤] 프리셋) · 하늘 없음(11-16) |
/// | 나무·덤불·꽃 | 잎 카드 FBX | 데모 팩 식생(ToonFantasyCityFlora) |
/// | 물웅덩이 | 원래 물(밤 어둠 위) | 원래 그림 · 밤 어둠 아래(ToonFantasyCityWater) |
/// | 풀 | 구운 풀잎 + 도시 풀 카드 전부 · 원래 크기 | 팩 풀 패치 7판 + 공원 풀 카드만(CityGrassVersions) |
/// </para>
///
/// <para>어느 쪽이든 지우지 않고 켜고 끄거나 머티리얼 칸만 바꾼다 — 몇 번을 오가도 같다. 누를 때마다 씬을 저장한다(Ctrl+Z 한 번에 취소).
/// 마지막에서 처음으로 처음 넘어갈 때(마지막 조명 기록이 아직 없을 때) 씬 백업을 하나 남긴다(Assets/_Game/Scenes/Backup~).
/// 테스트 잔디 블록(CityTestLawn, 10-20) · 비석 같은 프롭은 11월 전에도 있던 것이라 두 벌 모두 같다.
/// 11-13 정리 전 메뉴(Toon Fantasy 셰이더/ 14개 · 풀/ 3개 · 도시 꽃 카드/ 4개)는 모두 이것으로 바뀌었다.</para>
/// </summary>
public static class CityMapVersions
{
    const string MenuRoot = "Tools/좀비 오버런/맵 버전/";
    const string MenuFirst = MenuRoot + "처음 (11월 전)";
    const string MenuLast = MenuRoot + "마지막";

    public enum Version { Mixed, First, Last }

    [MenuItem(MenuFirst, false, 1)] static void UseFirst() => Switch(Version.First);
    [MenuItem(MenuLast, false, 2)] static void UseLast() => Switch(Version.Last);
    [MenuItem(MenuFirst, true)] static bool UseFirstCheck() { Mark(); return true; }
    [MenuItem(MenuLast, true)] static bool UseLastCheck() { Mark(); return true; }

    static void Mark()
    {
        var c = Current(out _);
        Menu.SetChecked(MenuFirst, c == Version.First);
        Menu.SetChecked(MenuLast, c == Version.Last);
    }

    /// 지금 씬이 어느 쪽인가 — 다섯 부분이 모두 같은 쪽이면 그 쪽, 아니면 섞임. parts = 부분마다 처음/마지막.
    public static Version Current(out string parts)
    {
        parts = "";
        var scene = SceneManager.GetActiveScene();
        var city = GameObject.Find("_City");
        if (city == null) return Version.Mixed;
        var flags = new (string name, bool last)[]
        {
            ("셰이더", ToonFantasySwapIsToon(city)),
            ("조명", !ToonFantasyCityLook.IsFirst(scene)),
            ("나무·덤불·꽃", ToonFantasyCityFlora.PlantsAreLast(scene)),
            ("물웅덩이", !ToonFantasyCityWater.IsFirst(scene)),
            ("풀", CityGrassVersions.Current() == CityGrassVersions.Version.Final),
        };
        int lastCount = 0;
        var sb = new List<string>();
        foreach (var f in flags) { if (f.last) lastCount++; sb.Add($"{f.name} {(f.last ? "마지막" : "처음")}"); }
        parts = string.Join(" · ", sb);
        return lastCount == flags.Length ? Version.Last : lastCount == 0 ? Version.First : Version.Mixed;
    }

    static bool ToonFantasySwapIsToon(GameObject city) => ToonFantasyCitySwap.SceneIsToon(city);

    public static void Switch(Version v)
    {
        if (v == Version.Mixed || !ToonFantasyCitySwap.OpenCityScene(out var scene)) return;
        var city = GameObject.Find("_City");
        if (city == null) { EditorUtility.DisplayDialog("맵 버전", "_City 가 없다 — 도시 테스트 씬(Proto_City_Test)에서 실행.", "확인"); return; }
        bool last = v == Version.Last;
        if (last && !CityGrassVersions.HasFinal(city) &&
            !EditorUtility.DisplayDialog("맵 버전", "마지막 풀(팩 풀 패치)이 아직 구워져 있지 않다. 굽고 넘어갈까?(1분 안쪽)", "굽고 넘어가기", "취소")) return;
        Current(out string before);
        string backup = !last && !ToonFantasyCityLook.HasFinalRecord ? ToonFantasyCitySwap.BackupScene() : null;

        Undo.SetCurrentGroupName(last ? "맵 버전: 마지막" : "맵 버전: 처음");
        int group = Undo.GetCurrentGroup();
        var lines = new List<string>();
        if (last)
        {
            lines.Add(ToonFantasyCitySwap.ToToon(scene));          // 셰이더 먼저 — 풀 재질·들국화가 씬 셰이더 상태를 따른다
            lines.Add(ToonFantasyCityLook.ToLast(scene));
            lines.Add(ToonFantasyCityFlora.PlantsToLast(scene));
            lines.Add(ToonFantasyCityWater.ToLast(scene));
            if (CityGrassVersions.HasFinal(city)) lines.Add("풀 마지막 — " + CityGrassVersions.Apply(city, CityGrassVersions.Version.Final));
            else lines.Add(CityGrassVersions.Bake(city, out string baked) ? baked : "풀 패치 굽기 실패 — 콘솔 확인");
        }
        else
        {
            lines.Add(ToonFantasyCityLook.ToFirst(scene));         // 지금(마지막) 조명을 먼저 기록해 둔다
            lines.Add(ToonFantasyCitySwap.ToBcm(scene));           // 프리팹 오버라이드를 걷는 일을 먼저 — 뒤의 켜고 끄기가 되돌려지지 않게
            lines.Add(ToonFantasyCityFlora.PlantsToFirst(scene));
            lines.Add(ToonFantasyCityWater.ToFirst(scene));
            lines.Add("풀 처음 — " + CityGrassVersions.Apply(city, CityGrassVersions.Version.Original));
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var now = Current(out string after);
        Debug.Log($"[맵 버전] {(last ? "마지막" : "처음 (11월 전)")} — 지금 {(now == Version.Mixed ? "섞임" : now == Version.Last ? "마지막" : "처음")}({after}) · 전({before})\n - " +
                  string.Join("\n - ", lines) + (backup != null ? $"\n씬 백업: {backup}" : ""));
    }
}
