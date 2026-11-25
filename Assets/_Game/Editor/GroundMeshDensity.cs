// 지면 판의 정점 밀도 — 안개가 거리대로 먹게 하는 전제 조건.
//
// URP 내장 안개는 **정점 셰이더에서** 계산돼 보간된다 (Lit 도, BCM 툰도
// `ComputeFogFactor(positionCS.z)` 를 vert 에서 부른다). 그래서 정점이 네 개뿐인
// 40m 판에서는 안개가 네 모서리에서만 평가되고 그 사이는 선형 보간된다 — 모서리가
// 전부 20~38m 라 **판 전체가 거의 같은 안개값으로 칠해진다.** 플레이어 발밑까지.
//
// 2026-09-15 에 실제로 이 증상을 쟀다: 안개 ON/OFF 차이 맵에서 지면(도로·잔디·흙)만
// 화면 전체가 균일하게 변하고 버스·나무·프롭은 전혀 안 변했다. 프롭은 정점이 많아
// 제 거리대로 안개를 받은 것이고, 지면만 뭉개진 것이다.
//
// 고치는 법은 하나뿐이다: 판을 잘게 나눈다. 셰이더로는 못 고친다 —
// 픽셀 단위 안개를 하려면 씬의 모든 셰이더(BCM 툰·물·캐릭터)를 손대야 한다.
//
// 32×32 한 장을 모두가 공유한다. 40m 판이면 칸이 1.25m 라 10m 폭의 가시 구간에
// 여덟 칸이 들어간다 — 보간 오차가 눈에 안 보이는 수준이다. 판 하나당 1,089 정점,
// 지면 전부 합쳐 1만 정점 안쪽이고 전부 정적 지오메트리다.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GroundMeshDensity
{
    const string MeshDir  = "Assets/_Game/Art/Field/Meshes";
    const string MeshPath = MeshDir + "/Quad_Subdiv32.asset";
    const int    Cells    = 32;

    /// 이 크기(월드 m) 미만인 판은 건드리지 않는다. 작은 판은 정점 안개로도 충분하고,
    /// VFX·데칼 쿼드까지 바꿔서 얻을 게 없다.
    const float MinWorldSize = 4f;

    [MenuItem("Tools/좀비 오버런/지면 판 세분화 (안개 보정)")]
    public static void RepairMenu()
    {
        int n = Repair();
        Debug.Log(n > 0
            ? $"[GroundMesh] 지면 판 {n}개를 {Cells}×{Cells} 로 교체했다. 씬 저장은 직접 할 것 (Ctrl+S)."
            : "[GroundMesh] 바꿀 판이 없다 — 이미 세분화됐거나 큰 Quad 가 없다.");
    }

    /// 열린 씬에서 내장 Quad 를 쓰는 큰 판을 찾아 세분화 메시로 갈아끼운다.
    /// 반환값은 교체한 개수. 스케일·UV·법선이 같으므로 보이는 모양은 바뀌지 않는다.
    public static int Repair()
    {
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        if (quad == null)
        {
            Debug.LogWarning("[GroundMesh] 내장 Quad 메시를 못 찾았다 — 세분화를 건너뛴다");
            return 0;
        }

        var subdiv = LoadOrBuild(quad);
        int changed = 0;

        int missing = 0;
        foreach (var mf in Object.FindObjectsOfType<MeshFilter>(true))
        {
            // 지면은 이제 위 세분화 메시 **에셋**에 의존한다. 그 에셋이 지워지면 지면이
            // 조용히 사라지고(스카이박스의 지면색만 남는다) 원인을 찾기 어렵다.
            // 여기서 한 번 짚어 준다 — '필드 생성' 을 다시 돌리면 복구된다.
            if (mf.sharedMesh == null) { missing++; continue; }

            if (mf.sharedMesh != quad) continue;

            var s = mf.transform.lossyScale;
            if (Mathf.Abs(s.x) < MinWorldSize && Mathf.Abs(s.y) < MinWorldSize) continue;

            Undo.RecordObject(mf, "지면 판 세분화");
            mf.sharedMesh = subdiv;
            EditorUtility.SetDirty(mf);
            changed++;
        }

        if (missing > 0)
            Debug.LogWarning($"[GroundMesh] 메시가 비어 있는 오브젝트 {missing}개 — {MeshPath} 가 지워졌을 수 있다. "
                           + "'Tools ▸ 좀비 오버런 ▸ 필드 생성' 으로 복구할 것.");

        return changed;
    }

    static Mesh LoadOrBuild(Mesh quad)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh != null && mesh.vertexCount == (Cells + 1) * (Cells + 1)) return mesh;

        mesh = Build(quad);

        if (!AssetDatabase.IsValidFolder(MeshDir))
            AssetDatabase.CreateFolder("Assets/_Game/Art/Field", "Meshes");

        AssetDatabase.CreateAsset(mesh, MeshPath);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    /// 내장 Quad 와 **완전히 같은 방향**의 격자를 만든다.
    /// 법선과 감기 방향을 눈대중으로 정하면 판이 통째로 뒤집혀 안 보이게 되므로,
    /// 원본에서 읽어 그대로 따라간다.
    static Mesh Build(Mesh quad)
    {
        var sv = quad.vertices;
        var sn = quad.normals;
        var st = quad.triangles;

        Vector3 normal = sn.Length > 0 ? sn[0] : Vector3.back;

        // 원본 첫 삼각형의 감기가 법선 기준 어느 쪽인지 판정한다.
        Vector3 a = sv[st[0]], b = sv[st[1]], c = sv[st[2]];
        bool srcPositive = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0f;

        // 아래 격자의 기본 순서 (i0, i1, i2) = (x,y) → (x+1,y) → (x,y+1) 는
        // 외적이 항상 +Z 다. 그 +Z 가 법선과 같은 쪽인지 보고, 원본과 부호가
        // 어긋나면 뒤집는다. **이 판정을 생략하고 눈대중으로 감으면 판이 통째로
        // 컬링돼 스카이박스의 지면색만 보인다 (2026-09-15 에 실제로 그랬다).**
        bool basePositive = Vector3.Dot(Vector3.forward, normal) > 0f;
        bool flip = srcPositive != basePositive;

        int side  = Cells + 1;
        var verts = new List<Vector3>(side * side);
        var norms = new List<Vector3>(side * side);
        var uvs   = new List<Vector2>(side * side);

        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            float u = (float)x / Cells;
            float v = (float)y / Cells;
            verts.Add(new Vector3(u - 0.5f, v - 0.5f, 0f));   // 내장 Quad 와 같은 1×1 크기
            norms.Add(normal);
            uvs.Add(new Vector2(u, v));
        }

        var tris = new List<int>(Cells * Cells * 6);
        for (int y = 0; y < Cells; y++)
        for (int x = 0; x < Cells; x++)
        {
            int i0 = y * side + x;
            int i1 = i0 + 1;
            int i2 = i0 + side;
            int i3 = i2 + 1;

            if (flip) { tris.Add(i0); tris.Add(i2); tris.Add(i1);   tris.Add(i2); tris.Add(i3); tris.Add(i1); }
            else      { tris.Add(i0); tris.Add(i1); tris.Add(i2);   tris.Add(i2); tris.Add(i1); tris.Add(i3); }
        }

        var mesh = new Mesh { name = "Quad_Subdiv" + Cells };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }
}
