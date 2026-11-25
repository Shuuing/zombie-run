using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 캐릭터 테이블(<see cref="CharacterTable"/>)의 줄마다 <b>프리팹 배리언트</b>를 만들고 갱신한다.
///
/// <para><b>왜 배리언트인가.</b> 캐릭터들은 리그·애니메이터·`WeaponSocket` 배선이 전부 같고
/// <b>다른 것은 몸 머티리얼 1개와 주무기 1개</b>뿐이다. 복사본으로 만들면 베이스 프리팹을 고칠 때마다
/// (애니 레이어 추가, 소켓 수정) 캐릭터 수만큼 따라 고쳐야 하고 반드시 하나가 빠진다.
/// 배리언트는 그 둘만 오버라이드로 들고 나머지는 베이스를 따라간다.</para>
///
/// <para><b>왜 코드로 만드는가.</b> 손으로 만들면 "머티리얼을 어느 렌더러에 넣었는지"가
/// 기록되지 않는다. 몸 렌더러 이름이 <c>Object137</c> 이라는 사실은 눈으로 알 수 없고
/// (같은 소스 머티리얼을 face·eyebrows_01·1111 이 공유한다 — <see cref="CharacterMaterialSetup"/> 참조),
/// 캐릭터가 늘수록 그 실수가 늘어난다.</para>
///
/// <para><b>무엇을 어디서 읽나.</b> 예전에는 이 파일 안의 표(머티리얼·무기)가 진실이었다. 지금은
/// <c>Assets/_Game/Data/Characters/C_*.asset</c>(<see cref="CharacterDef"/>)이 진실이고 이 메뉴는 그것을 프리팹에 굽기만 한다.
/// 줄의 <c>prefab</c> 이 비어 있으면 베이스에서 새로 만들어 채우고, 있으면 오버라이드만 갱신한다 —
/// 씬에 놓인 인스턴스 참조가 끊기지 않으므로 <b>여러 번 돌려도 안전</b>하다.</para>
/// </summary>
public static class CharacterSetup
{
    const string BasePrefab = "Assets/_Game/Art/character/an/Prefabs/SushiGirl_Rifle.prefab";
    const string OutDir     = "Assets/_Game/Art/character/an/Prefabs/Characters";
    const string TablePath  = "Assets/_Game/Resources/" + CharacterTable.ResourcePath + ".asset";

    // 몸 렌더러. 이 이름이 바뀌면 아래 Apply 가 조용히 아무것도 안 하므로 로그로 잡는다.
    const string BodyRenderer = "Object137";

    [MenuItem("Tools/좀비 오버런/데이터 테이블/캐릭터 프리팹 만들기·갱신 (테이블 기준)")]
    public static void Build()
    {
        var table = AssetDatabase.LoadAssetAtPath<CharacterTable>(TablePath);
        if (table == null || table.roster == null) { Debug.LogError("[캐릭터] 캐릭터 테이블 없음: " + TablePath); return; }

        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab);
        if (basePrefab == null) { Debug.LogError("[캐릭터] 베이스 프리팹 없음: " + BasePrefab); return; }

        if (!AssetDatabase.IsValidFolder(OutDir))
            AssetDatabase.CreateFolder("Assets/_Game/Art/character/an/Prefabs", "Characters");

        int made = 0, updated = 0;
        for (int i = 0; i < table.roster.Length; i++)
        {
            var def = table.roster[i];
            if (def == null) { Debug.LogWarning($"[캐릭터] 테이블 {i + 1}번 칸이 비어 있다"); continue; }

            string path = def.prefab != null ? AssetDatabase.GetAssetPath(def.prefab) : OutDir + "/" + PrefabNameFor(def) + ".prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            if (!exists) { if (!CreateVariant(basePrefab, path)) continue; made++; } else updated++;
            Apply(path, def);

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (def.prefab != go) { def.prefab = go; EditorUtility.SetDirty(def); }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[캐릭터] 완료 — 새로 만든 것 " + made + "개, 갱신 " + updated + "개. " + OutDir);
    }

    /// 줄 에셋 이름 C_01_Rookie → 프리팹 이름 Char_01_Rookie. 규약이 다르면 에셋 이름 그대로.
    static string PrefabNameFor(CharacterDef def)
    {
        string n = def.name;
        return n.StartsWith("C_") ? "Char_" + n.Substring(2) : n;
    }

    /// <summary>
    /// 베이스 프리팹 인스턴스를 새 경로에 저장하면 배리언트가 된다.
    /// 인스턴스를 만들 씬이 필요한데 <b>사용자가 열어둔 씬을 더럽히면 안 되므로</b>
    /// 빈 씬을 추가로 열어 거기서만 만들고 닫는다.
    /// </summary>
    static bool CreateVariant(GameObject basePrefab, string path)
    {
        var tmp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, tmp);
        var saved = PrefabUtility.SaveAsPrefabAsset(inst, path);
        Object.DestroyImmediate(inst);
        EditorSceneManager.CloseScene(tmp, true);

        if (saved == null) { Debug.LogError("[캐릭터] 배리언트 생성 실패: " + path); return false; }
        if (PrefabUtility.GetPrefabAssetType(saved) != PrefabAssetType.Variant)
            Debug.LogWarning("[캐릭터] " + System.IO.Path.GetFileName(path) +
                             " 이 배리언트가 아니라 독립 프리팹으로 저장됐다 — 베이스 수정이 전파되지 않는다.");
        return true;
    }

    static void Apply(string path, CharacterDef def)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // 몸 머티리얼
            var body = FindRenderer(root, BodyRenderer);
            if (body == null)
                Debug.LogError("[캐릭터] 몸 렌더러 '" + BodyRenderer + "' 를 못 찾았다 — " + def.name +
                               " 의 몸 머티리얼이 적용되지 않았다. 베이스 프리팹의 렌더러 이름을 확인할 것.");
            else if (def.bodyMaterial == null)
                Debug.LogWarning("[캐릭터] " + def.name + " 의 bodyMaterial 이 비어 있다 — 베이스 몸 색 그대로");
            else
            {
                var arr = new Material[Mathf.Max(1, body.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = def.bodyMaterial;
                body.sharedMaterials = arr;
            }

            // 주무기
            var socket = root.GetComponentInChildren<WeaponSocket>(true);
            if (socket == null) Debug.LogWarning("[캐릭터] " + def.name + " 에 WeaponSocket 이 없다.");
            else if (def.weapon != null)
            {
                socket.weapon = def.weapon;
                // 손에 쥐지 않는 무기라면 legacy 필드도 비워 둔다. Equip 이 이미 건너뛰지만,
                // 프리팹 인스펙터에 소총이 남아 있으면 읽는 사람이 반드시 헷갈린다.
                if (def.weapon.mount != WeaponMount.Hand) socket.weaponPrefab = null;
            }
            else
            {
                // 손에 드는 무기가 없는 캐릭터. legacy 필드까지 비워야 한다 —
                // 안 비우면 Equip(null) 이 fallback 으로 소총을 물려 리퍼가 총을 든다.
                socket.weapon = null;
                socket.weaponPrefab = null;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("[캐릭터] " + System.IO.Path.GetFileNameWithoutExtension(path) + " (" + def.Label + ") ← " +
                      (def.bodyMaterial != null ? def.bodyMaterial.name : "머티리얼 없음") +
                      " · " + (def.weapon != null ? def.weapon.name : "무기 없음(블레이드/드론)"));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Renderer FindRenderer(GameObject root, string name)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            if (r.name == name) return r;
        return null;
    }
}
