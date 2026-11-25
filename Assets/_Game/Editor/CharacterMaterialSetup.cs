// 플레이어 캐릭터 머티리얼 지정.
//
// 왜 필요한가: 04_unity_sushi_girl.fbx 는 텍스처가 없는 FBX 내장 기본 머티리얼
// (09 - Default / 10 - Default / 08 - Default / mouth)을 쓴다. AnimTest.unity 에는
// 올바른 머티리얼이 들어가 있지만 그건 "씬 오버라이드"라서 프리팹에는 저장되지 않았고,
// 프리팹을 인스턴스화하는 다른 씬(프로토타입 등)은 전부 텍스처 없는 상태로 나온다.
//
// 왜 FBX 임포터 리맵으로 안 되는가: face / eyebrows_01 / 1111 세 렌더러가 소스 머티리얼
// '09 - Default' 를 공유한다. 임포터 리맵은 소스 머티리얼 이름 단위라 이 셋을 갈라놓을 수
// 없다. 그래서 프리팹에 렌더러별로 직접 지정한다.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CharacterMaterialSetup
{
    const string PrefabPath = "Assets/_Game/Art/character/an/Prefabs/SushiGirl_Rifle.prefab";
    const string TxtDir     = "Assets/_Game/Art/character/txt";

    // 매핑 근거: AnimTest.unity 의 SushiGirl_Rifle 오버라이드가 정답이고,
    // eyebrows_01 만 그 씬에서도 미지정이라 이름이 일치하는 eyebrows_xray 를 넣었다.
    static readonly Dictionary<string, string> Map = new Dictionary<string, string>
    {
        { "face",                "face 1.mat" },
        { "Object137",           "body_02.mat" },
        { "Object150",           "hair 1.mat" },
        { "sd_rnd_bcm01_mouth",  "Character_Mouth.mat" },
        { "eyebrows_01",         "eyebrows_xray.mat" },
        // "1111" 은 의도적으로 비워둔다 — 20 tris, 바운즈 중심이 몸에서 Z+8.16 떨어진
        // 정체 불명 지오메트리다. AnimTest 에서도 기본 머티리얼이라 원작자도 손대지 않았다.
        // 무엇인지 확인되기 전에 임의로 머티리얼을 넣거나 끄지 않는다.
    };

    [MenuItem("Tools/좀비 오버런/캐릭터 머티리얼 적용")]
    public static void Apply()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null) { Debug.LogError($"[CharMat] 프리팹을 열 수 없다: {PrefabPath}"); return; }

        int done = 0, missing = 0;
        var unmapped = new List<string>();

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            string matFile;
            if (!Map.TryGetValue(r.name, out matFile)) { unmapped.Add(r.name); continue; }

            string path = $"{TxtDir}/{matFile}";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { Debug.LogWarning($"[CharMat] 머티리얼 없음: {path}"); missing++; continue; }

            var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < arr.Length; i++) arr[i] = mat;
            r.sharedMaterials = arr;
            done++;
            Debug.Log($"[CharMat] {r.name,-22} → {matFile}");
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();

        string un = unmapped.Count > 0 ? string.Join(", ", unmapped) : "없음";
        Debug.Log($"[CharMat] 완료 — 적용 {done}개, 머티리얼 누락 {missing}개, 미지정 렌더러: {un}");
    }
}
