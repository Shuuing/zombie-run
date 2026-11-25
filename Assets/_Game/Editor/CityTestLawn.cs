using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 테스트용 잔디 블록 — 도시 동쪽 블록(공원 오른쪽, x 30~66 · z -18~18)의 건물·나무·컨테이너를 치우고 잔디를 깐다.
/// 2026-10-20 사용자 요청: "필드 한쪽에 건물 제거하고 풀밭으로, 테스트용".
///
/// <para><b>되돌릴 수 있게</b> 프롭은 지우지 않고 꺼진 보관함(Field_Chapter01/_TestLawnRemoved)으로 옮긴다.
/// ProtoObstacles 는 _Props 만 훑으므로 옮긴 프롭은 길을 막지 않는다. [되돌리기] 가 제자리로 돌려놓는다.</para>
///
/// <para>잔디는 공원 잔디와 같은 머티리얼(M_City_Grass, 월드 UV 3.5m)에 연석 테두리. 높이 0.006 — 주차장(≤0.004)보다 위.
/// 만든 뒤 풀잎을 다시 굽는다(<see cref="CityGrassBaker"/> 가 _City/_TestLawn 을 잔디로 읽는다).</para>
///
/// CityBuilder 로 도시를 다시 만들면 이 블록도 원래대로 돌아온다(그때는 메뉴를 다시 실행).
/// </summary>
public static class CityTestLawn
{
    public const string LawnName = "_TestLawn";
    const string HolderName = "_TestLawnRemoved";
    const string MatDir = "Assets/_Game/Art/City/Materials";

    // CityBuilder 레이아웃: 블록 36m, 도로 12m, 보도 3m → 동쪽 가운데 블록 = x 30~66, z -18~18
    static readonly Rect Block = Rect.MinMaxRect(30f, -18f, 66f, 18f);
    const float Side = 3f, Curb = 0.3f, LawnY = 0.006f, CurbY = 0.007f, TileGrass = 3.5f;
    static readonly Color32 LawnColor = new Color32(40, 35, 25, 215);   // 공원 잔디 정점 컬러 평균(깨끗한 잔디)

    [MenuItem("Tools/좀비 오버런/테스트 잔디 블록/만들기 (동쪽 블록)")]
    public static void Apply()
    {
        var city = GameObject.Find("_City");
        var props = GameObject.Find("_Props");
        if (city == null || props == null) { Debug.LogWarning("[테스트 잔디] _City 또는 _Props 없음 — 도시 테스트 씬을 열고 실행"); return; }

        int moved = MoveProps(props.transform);
        BuildLawn(city.transform);

        var map = city.GetComponent<Proto.CityMap>();
        CityGrassBaker.Bake(city.transform, map != null ? map.halfSize : 86f);
        EditorSceneManager.MarkSceneDirty(city.scene);
        EditorSceneManager.SaveScene(city.scene);
        Debug.Log($"[테스트 잔디] 동쪽 블록 프롭 {moved}개를 보관함으로 옮기고 잔디를 깔았다. 되돌리기: Tools/좀비 오버런/테스트 잔디 블록/되돌리기");
    }

    [MenuItem("Tools/좀비 오버런/테스트 잔디 블록/되돌리기")]
    public static void Revert()
    {
        var city = GameObject.Find("_City");
        var props = GameObject.Find("_Props");
        if (city == null || props == null) return;

        int back = 0;
        var holder = props.transform.parent != null ? props.transform.parent.Find(HolderName) : null;
        if (holder != null)
        {
            foreach (Transform g in new List<Transform>(Children(holder)))
            {
                var dst = props.transform.Find(g.name);
                if (dst == null) { dst = new GameObject(g.name).transform; dst.SetParent(props.transform, false); }
                foreach (Transform inst in new List<Transform>(Children(g))) { inst.SetParent(dst, true); back++; }
            }
            Object.DestroyImmediate(holder.gameObject);
        }
        var lawn = city.transform.Find(LawnName);
        if (lawn != null) Object.DestroyImmediate(lawn.gameObject);

        var map = city.GetComponent<Proto.CityMap>();
        CityGrassBaker.Bake(city.transform, map != null ? map.halfSize : 86f);
        EditorSceneManager.MarkSceneDirty(city.scene);
        EditorSceneManager.SaveScene(city.scene);
        Debug.Log($"[테스트 잔디] 되돌림 — 프롭 {back}개 제자리, 잔디 제거.");
    }

    static IEnumerable<Transform> Children(Transform t) { foreach (Transform c in t) yield return c; }

    /// 블록 안(렌더러 바운드 중심 기준 — 건물은 피벗이 원점이라 위치로는 못 고른다) 프롭을 꺼진 보관함으로.
    static int MoveProps(Transform props)
    {
        var parent = props.parent;
        var holder = parent.Find(HolderName);
        if (holder == null)
        {
            holder = new GameObject(HolderName).transform;
            holder.SetParent(parent, false);
            holder.gameObject.SetActive(false);
        }
        int moved = 0;
        foreach (Transform g in new List<Transform>(Children(props)))
        {
            foreach (Transform inst in new List<Transform>(Children(g)))
            {
                if (!CenterOf(inst, out var c) || !Block.Contains(new Vector2(c.x, c.z))) continue;
                var dst = holder.Find(g.name);
                if (dst == null) { dst = new GameObject(g.name).transform; dst.SetParent(holder, false); }
                inst.SetParent(dst, true);
                moved++;
            }
        }
        return moved;
    }

    static bool CenterOf(Transform t, out Vector3 c)
    {
        bool any = false; Bounds b = default;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
        c = any ? b.center : t.position;
        return any;
    }

    /// 보도 링 안쪽 전체에 잔디(서브메시 0) + 연석 테두리(서브메시 1).
    static void BuildLawn(Transform city)
    {
        var old = city.Find(LawnName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Rect edge = Rect.MinMaxRect(Block.xMin + Side, Block.yMin + Side, Block.xMax - Side, Block.yMax - Side);
        Rect grass = Rect.MinMaxRect(edge.xMin + Curb, edge.yMin + Curb, edge.xMax - Curb, edge.yMax - Curb);

        var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color32>(); var n = new List<Vector3>();
        var lawnTris = new List<int>(); var curbTris = new List<int>();
        // 잔디는 1.5m 격자 — 정점 조명·안개가 한 장짜리보다 고르게 먹는다(공원 잔디 GridXZ 와 같은 간격)
        int nx = Mathf.CeilToInt(grass.width / 1.5f), nz = Mathf.CeilToInt(grass.height / 1.5f);
        for (int iz = 0; iz <= nz; iz++)
            for (int ix = 0; ix <= nx; ix++)
            {
                float x = Mathf.Lerp(grass.xMin, grass.xMax, ix / (float)nx), z = Mathf.Lerp(grass.yMin, grass.yMax, iz / (float)nz);
                v.Add(new Vector3(x, LawnY, z)); uv.Add(new Vector2(x, z) / TileGrass); col.Add(LawnColor); n.Add(Vector3.up);
            }
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                int a = iz * (nx + 1) + ix, b = a + 1, c = a + nx + 1, d = c + 1;
                lawnTris.AddRange(new[] { a, c, b, b, c, d });
            }
        void CurbQuad(float x0, float z0, float x1, float z1)
        {
            int i = v.Count;
            v.Add(new Vector3(x0, CurbY, z0)); v.Add(new Vector3(x1, CurbY, z0)); v.Add(new Vector3(x0, CurbY, z1)); v.Add(new Vector3(x1, CurbY, z1));
            for (int k = 0; k < 4; k++) { uv.Add(new Vector2(v[i + k].x, v[i + k].z)); col.Add(new Color32(0, 0, 0, 0)); n.Add(Vector3.up); }
            curbTris.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 });
        }
        CurbQuad(edge.xMin, edge.yMin, edge.xMax, edge.yMin + Curb);
        CurbQuad(edge.xMin, edge.yMax - Curb, edge.xMax, edge.yMax);
        CurbQuad(edge.xMin, edge.yMin + Curb, edge.xMin + Curb, edge.yMax - Curb);
        CurbQuad(edge.xMax - Curb, edge.yMin + Curb, edge.xMax, edge.yMax - Curb);

        var mesh = new Mesh { name = "TestLawn" };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetNormals(n);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(lawnTris, 0); mesh.SetTriangles(curbTris, 1);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        const string meshPath = "Assets/_Game/Art/City/TestLawn.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        var go = new GameObject(LawnName);
        go.transform.SetParent(city, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[]
        {
            AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_City_Grass.mat"),
            AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_City_Curb.mat"),
        };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }
}
