// 좀비 오버런 (TANGTANG) — 입모양 기준 칸 굽기 (2026-10-28).
//
// 입 메시는 읽기 불가라 빌드에서 UV 를 못 읽는다. 에디터에서 UV 로 기준 칸을 재서 입 렌더러에 ProtoMouthCell 로 붙여 둔다.
// 입 머티리얼(Character_Mouth)을 쓰는 프리팹을 전부 훑는다. 새 캐릭터를 들이면 다시 돌릴 것.
using Proto;
using UnityEditor;
using UnityEngine;

public static class MouthCellBaker
{
    const string MouthMat = "Assets/_Game/Art/character/txt/Character_Mouth.mat";
    const int Grid = 8;

    [MenuItem("Tools/좀비 오버런/입모양 기준 칸 굽기")]
    public static void BakeAll()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MouthMat);
        if (mat == null) { Debug.LogError("[입모양] Character_Mouth.mat 없음"); return; }
        int n = 0;
        // 기본 프리팹을 먼저 굽는다. 변형(Char_01~06 은 SushiGirl_Rifle 의 변형)을 먼저 구우면 변형에 하나 붙은 뒤
        // 기본에 또 붙어서 변형 쪽에 두 개가 된다.
        var paths = new System.Collections.Generic.List<string>();
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" })) paths.Add(AssetDatabase.GUIDToAssetPath(g));
        paths.Sort((a, b) => IsVariant(a).CompareTo(IsVariant(b)));
        foreach (var path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            bool dirty = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial != mat) continue;
                if (!TryCell(r, out var cell)) { Debug.LogWarning($"[입모양] {path} {r.name}: UV 를 못 읽음"); continue; }
                var mc = r.GetComponent<ProtoMouthCell>();
                if (mc == null) mc = r.gameObject.AddComponent<ProtoMouthCell>();
                if (mc.cell != cell) { mc.cell = cell; }
                dirty = true;
                Debug.Log($"[입모양] {path} {r.name} → 칸 {cell.x},{cell.y}");
                n++;
            }
            if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log($"[입모양] 기준 칸 {n}개 굽기 완료");
    }

    static bool IsVariant(string path) =>
        PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(path)) == PrefabAssetType.Variant;

    /// UV 바운드의 중심이 든 칸. 열은 왼쪽부터, 행은 위쪽부터 센다.
    static bool TryCell(Renderer r, out Vector2Int cell)
    {
        cell = Vector2Int.zero;
        var mf = r.GetComponent<MeshFilter>();
        Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh : (mf != null ? mf.sharedMesh : null);
        if (m == null) return false;
        using (var arr = Mesh.AcquireReadOnlyMeshData(m))
        {
            var d = arr[0];
            if (d.vertexCount == 0) return false;
            var uv = new Unity.Collections.NativeArray<Vector2>(d.vertexCount, Unity.Collections.Allocator.Temp);
            d.GetUVs(0, uv);
            Vector2 mn = uv[0], mx = uv[0];
            for (int i = 1; i < uv.Length; i++) { mn = Vector2.Min(mn, uv[i]); mx = Vector2.Max(mx, uv[i]); }
            uv.Dispose();
            Vector2 c = (mn + mx) * 0.5f;
            cell = new Vector2Int(Mathf.FloorToInt(c.x * Grid), Grid - 1 - Mathf.FloorToInt(c.y * Grid));
        }
        return true;
    }
}
