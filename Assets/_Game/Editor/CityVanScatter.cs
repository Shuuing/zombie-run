// 좀비 오버런 (TANGTANG) — 도시 테스트 씬 도로에 버려진 승합차 흩뿌리기 (사용자 요청 2026-10-13).
// CityBuilder 가 놓는 6대(블록 가장자리 주차)와 별개로, 도로 한가운데에 비스듬히 멈춰 선 차를 더한다.
// 재실행 가능 — 이 메뉴가 놓은 것(이름 Van_Scatter_*)만 지우고 다시 놓는다.
// 충돌은 ProtoObstacles 가 _Props/Van_Abandoned 아래를 그대로 읽으므로 따로 할 일이 없다.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CityVanScatter
{
    const string PropRoot = "Assets/_Game/Art/Field/Props";
    const string FieldMat = "Assets/_Game/Art/Field/Materials";
    const string Prefix   = "Van_Scatter_";

    // CityBuilder 와 같은 도시 격자 (N=3, 블록 36, 도로 12) — 바꾸면 같이 바꿀 것
    const int   N = 3;
    const float Block = 36f, Road = 12f, RoadY = -0.12f;
    const float Scale = 2.634f;            // CityBuilder 주차 차와 같은 크기
    static float Half => (N * Block + (N + 1) * Road) * 0.5f;
    static float RoadCenter(int k) => -Half + Road * 0.5f + k * (Block + Road);

    const int   Count = 16;
    const float MinGap = 7f;               // 다른 프롭(차·컨테이너·나무 등)과 최소 간격
    const float StartClear = 12f;          // 플레이어 시작점(원점) 주변은 비운다
    const int   Seed = 20261013;

    [MenuItem("Tools/좀비 오버런/도시: 버려진 승합차 뿌리기")]
    public static void Scatter()
    {
        var props = GameObject.Find("_Props");
        if (props == null) { Debug.LogError("[VanScatter] _Props 가 없다 — 도시 테스트 씬(Proto_City_Test)을 열고 실행할 것"); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropRoot}/Van_Abandoned.obj");
        var mat    = AssetDatabase.LoadAssetAtPath<Material>($"{FieldMat}/M_Van_Abandoned.mat");
        if (prefab == null) { Debug.LogError("[VanScatter] Van_Abandoned.obj 없음"); return; }

        var group = props.transform.Find("Van_Abandoned");
        if (group == null) { group = new GameObject("Van_Abandoned").transform; group.SetParent(props.transform, false); }

        // 지난번에 뿌린 것만 지운다
        for (int i = group.childCount - 1; i >= 0; i--)
            if (group.GetChild(i).name.StartsWith(Prefix)) Undo.DestroyObjectImmediate(group.GetChild(i).gameObject);

        // 이미 있는 프롭 자리(XZ) — 겹치지 않게
        var taken = new List<Vector2>();
        foreach (Transform g in props.transform)
            foreach (Transform inst in g)
                taken.Add(new Vector2(inst.position.x, inst.position.z));

        var rng = new System.Random(Seed);
        int placed = 0;
        for (int attempt = 0; attempt < 400 && placed < Count; attempt++)
        {
            // 가로 도로(z 고정) 또는 세로 도로(x 고정) 하나를 고른다
            bool alongX = rng.NextDouble() < 0.5;
            float lane = RoadCenter(rng.Next(N + 1)) + (float)(rng.NextDouble() * 2 - 1) * (Road * 0.5f - 2.2f);
            float t = (float)(rng.NextDouble() * 2 - 1) * (Half - 4f);
            Vector3 pos = alongX ? new Vector3(t, RoadY, lane) : new Vector3(lane, RoadY, t);

            // 교차로 한가운데는 피한다 — 길이 완전히 막히면 좀비 동선이 이상해진다
            bool inCross = false;
            for (int k = 0; k <= N && !inCross; k++)
                if (Mathf.Abs((alongX ? pos.x : pos.z) - RoadCenter(k)) < Road * 0.5f + 1f) inCross = true;
            if (inCross) continue;

            if (new Vector2(pos.x, pos.z).magnitude < StartClear) continue;
            bool near = false;
            foreach (var p in taken) if ((p - new Vector2(pos.x, pos.z)).sqrMagnitude < MinGap * MinGap) { near = true; break; }
            if (near) continue;

            // 차선 방향 ±25°, 다섯 대 중 하나는 크게 틀어져 있다(사고로 멈춘 차)
            float roadYaw = alongX ? 90f : 0f;
            if (rng.NextDouble() < 0.5) roadYaw += 180f;
            float yaw = roadYaw + (rng.NextDouble() < 0.2 ? (float)(rng.NextDouble() * 2 - 1) * 70f
                                                          : (float)(rng.NextDouble() * 2 - 1) * 25f);

            Place(group, prefab, mat, pos, yaw, $"{Prefix}{placed:D2}");
            taken.Add(new Vector2(pos.x, pos.z));
            placed++;
        }

        EditorSceneManager.MarkSceneDirty(props.scene);
        EditorSceneManager.SaveScene(props.scene);
        FootprintBaker.BakeOpenScene();   // 새로 놓은 차의 충돌 발자국도 굽는다(폰 정적 배칭 대비)
        Debug.Log($"[VanScatter] 승합차 {placed}대를 도로에 뿌렸다 (그룹 {group.name}, 기존 포함 {group.childCount}대)");
    }

    /// CityBuilder.PlaceProp 과 같은 규칙: 모델 루트 회전은 살리고 yaw 만 얹고, 밑면을 y 에 앉힌다.
    static void Place(Transform group, GameObject prefab, Material mat, Vector3 pos, float yaw, string name)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        go.name = name;
        go.transform.localScale = Vector3.one * Scale;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;
        go.transform.position = pos;
        if (mat != null) foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            go.transform.position += new Vector3(0f, pos.y - b.min.y, 0f);
        }
        go.isStatic = true;
        Undo.RegisterCreatedObjectUndo(go, "Scatter Van");
    }
}
