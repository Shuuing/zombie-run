using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 맵 마지막 버전이 쓰는 에셋 자리 — <c>Assets/_Game/Art/ToonFantasyCity/</c> 아래 주제 폴더. 메뉴는 없다.
///
/// <para>2026-11-16 사용자: "원본 Toon Fantasy 셰이더는 지워주고, 지금 마지막으로 사용하는것만 정리해줘. 하늘은 우선 사용안하니깐 지워죠. 사용안하는건 우선 다 삭제해줘"
/// → 한 번 쓰는 정리 메뉴로 했다(11:02): 하늘 빼기 · 남길 팩 머티리얼 11개를 도시 셰이더로 · 마지막이 쓰는 팩 에셋 74개를 아래로 옮김(GUID 그대로) ·
/// Toon Fantasy Nature 팩 폴더 통째로와 안 쓰는 도시 셰이더 2개를 윈도 휴지통으로. 렌더러 1,741개 분홍 0 · 원본 잃은 프리팹 0.
/// 정리가 끝나 그 메뉴와 코드는 지웠다(README 의 [폴더] 에 순서가 적혀 있다).</para>
/// <code>
/// ToonFantasyCity/
///   Trees/    Prefabs · Models          데모 나무(Oak · Birch · Aspen, LOD · 콜라이더 모델)
///   Bushes/   Prefabs · Models          데모 덤불
///   Flowers/  Prefabs · Models · Materials · Textures   화초 자리 꽃 패치 + 풀 굽기의 색 꽃
///   Grass/    Prefabs · Models · Materials · Textures · 구운 풀 패치 메시 · 잔디 풀 지도
///   Lighting/ 도시 밤 후처리(TFF_City_Night) · LUT
///   Textures/ 여러 묶음이 같이 쓰는 것 — 아틀라스 · 램프 · 노이즈
///   (셰이더는 11-23 Assets/_Game/Art/Sujin/Shaders/Toon 으로 옮겼다 — Sujin/Toon/Outline · Vegetation · Grass + SujinToon.hlsl, 11-20 옛 Toon/City/TFF_*)
///   Materials/ BCM → 톤 복사본 + 팩 기준 머티리얼 2개(TFF_Atlas_1A_D · Vegetation — 램프·외곽선 값을 가져오는 것)
///   FloraMaterials/ · WaterMaterials/    데모 나무 · 물웅덩이 복사본
/// </code>
/// 코드는 팩에서 온 에셋을 <b>경로가 아니라 이름으로</b> 찾는다(<see cref="Find{T}"/>) — 폴더를 또 옮겨도 그대로 된다.
/// </summary>
public static class ToonFantasyAssets
{
    const string Root = ToonFantasyCitySwap.Root;

    // 우리 결과물 — 새 자리가 있으면 거기, 없고 예전 자리(루트)에 있으면 거기
    internal static string GrassMeshPath => Pick(Root + "/Grass/TFF_CityGrassPatches.asset", Root + "/TFF_CityGrassPatches.asset");
    internal static string LawnMaskPath => Pick(Root + "/Grass/TFF_GrassMask_Lawn.asset", Root + "/TFF_GrassMask_Lawn.asset");
    internal static string NightProfilePath => Pick(Root + "/Lighting/TFF_City_Night.asset", Root + "/TFF_City_Night.asset");

    static string Pick(string now, string before) =>
        AssetDatabase.LoadMainAssetAtPath(now) != null || AssetDatabase.LoadMainAssetAtPath(before) == null ? now : before;

    static readonly Dictionary<string, Object> _found = new Dictionary<string, Object>();

    /// 이름(확장자 없이)이 꼭 같은 에셋 — ToonFantasyCity 아래를 먼저, 없으면 프로젝트 전체.
    /// GameObject 는 프리팹을 모델(FBX)보다 먼저 고른다 — 데모 나무·덤불은 프리팹과 FBX 이름이 같다(TFF_Oak_Tree_01A.prefab · .fbx).
    internal static T Find<T>(string name) where T : Object
    {
        string key = typeof(T).Name + ":" + name;
        if (_found.TryGetValue(key, out var hit) && hit != null) return (T)hit;
        T best = null; int bestScore = int.MinValue;
        foreach (var g in AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) != name) continue;
            var o = AssetDatabase.LoadAssetAtPath<T>(p);
            if (o == null) continue;
            int score = (p.StartsWith(Root) ? 1 : 0) + (typeof(T) == typeof(GameObject) && p.EndsWith(".prefab") ? 10 : 0);
            if (score > bestScore) { best = o; bestScore = score; }
        }
        if (best != null) _found[key] = best;
        return best;
    }
}
