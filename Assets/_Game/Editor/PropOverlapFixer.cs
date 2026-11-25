// 프롭 충돌 볼륨 겹침 정리.
//
// 왜 필요한가: `ProtoObstacles.Resolve` 는 충돌 박스를 **순차로** 적용한다. 두 박스가 겹치면
// A 가 Z축으로 밀어낸 결과가 B 안이고, B 가 X축으로 밀어낸 결과가 다시 A 안이 되어
// 침투가 패스당 몇 %씩만 줄며 이동체가 옆으로 기어간다(하드 락은 아니지만 강제로 끌린다).
// 프롭을 손으로 배치하면 이 겹침이 계속 생기므로, 검사와 정리를 메뉴로 만들어 둔다.
//
// 하는 일: 씬의 프롭 OBB 를 `ProtoObstacles` 와 **같은 방식으로** 만들고(같은 shrink 표를 쓴다),
// 겹친 쌍을 최소 이동 벡터(MTV)만큼 떼어놓는 완화 반복을 돌린다. 회전·스케일·Y 는 건드리지 않는다.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PropOverlapFixer
{
    // 어느 반지름을 기준으로 떼어놓을지. 박스는 **이동체 반지름만큼 부풀려져** 판정되므로
    // 이 값이 클수록 프롭 사이를 더 벌려야 한다.
    //
    // **반드시 가장 큰 이동체(보스)를 기준으로 해야 한다.** 플레이어(0.32)로 맞췄더니
    // 플레이어·잡좀비는 완벽해졌는데 보스는 미수렴 0점 → 57점으로 오히려 나빠졌다.
    // 좁은 틈이 플레이어에겐 통로지만 보스에겐 양쪽 볼륨이 겹치는 렌즈가 되기 때문이다.
    // 하드코딩하지 않고 적 테이블의 보스 줄(E_MidBoss.bodyRadius)을 그대로 읽어 값이 갈라지지 않게 한다.
    // (최종 보스는 1.3배 더 크지만 울타리 안에서만 싸우므로 프롭 사이를 지나지 않는다.)
    static float MoverRadius
    {
        get
        {
            var t = Resources.Load<EnemyTable>(EnemyTable.ResourcePath);
            return t != null && t.midBoss != null ? t.midBoss.BodyRadius : 0.55f;
        }
    }
    // 딱 붙는 상태(접점)도 부동소수 오차로 겹침이 되므로 여유를 준다.
    const float Margin = 0.10f;

    const int   MaxPasses = 400;
    const float Relax     = 0.5f;   // 한 번에 MTV 전체를 밀면 반대편으로 튕겨 진동한다
    // **절대 여유분.** Relax 만으로 밀면 분리량이 침투량의 절반씩 줄어들어 0 에 무한히
    // 가까워지기만 하고 경계를 넘지 못한다(실제로 "겹침 0.00m" 로 400패스를 다 썼다).
    // 매 패스에 고정량을 더해야 유한 패스 안에 확실히 떨어진다.
    const float Bite = 0.02f;
    // 겹침 판정 허용 오차. 이보다 작은 침투는 부동소수 잡음으로 본다.
    const float Eps = 0.001f;

    // 배치 제약 — 정리 과정에서 프롭이 엉뚱한 데로 밀려나지 않게 한다.
    const float ArenaHalf   = 18.5f;  // 중심이 이 밖으로 나가지 않게
    const float SpawnClear  = 3.0f;   // 스폰 주변으로 밀어 넣지 않는다
    const float OffRoadMinX = 4.9f;   // 나무·울타리는 노면 밖만 (RoadHalf 3.6 + Curb 0.7 + 0.6)

    // 노면에 서면 안 되는 그룹 (FieldBuilder 의 Zone.OffRoad 와 같은 규칙)
    static readonly HashSet<string> OffRoad = new HashSet<string> { "Tree_Conifer", "Fence_Plank" };

    // ProtoObstacles.Shrink 와 **같은 값이어야 한다.** 여기만 다르면 정리해도 런타임에서 여전히 겹친다.
    static readonly Dictionary<string, float> Shrink = new Dictionary<string, float>
    {
        { "Van_Abandoned",    0.90f },
        { "Barrier_Concrete", 0.90f },
        { "Barricade_Wood",   0.85f },
        { "Fence_Plank",      0.85f },
        { "Barrel_Rusty",     0.80f },
        { "Rubble_Concrete",  0.70f },
        { "Tree_Conifer",     0.32f },
    };
    const float DefaultShrink = 0.85f;

    class Item
    {
        public Transform tr;
        public string group;
        public Vector2 pos;          // XZ 중심 (박스 중심, 피봇이 아니다)
        public Vector2 pivotOffset;  // 박스 중심 - 피봇. 이동 후 피봇을 되계산할 때 쓴다
        public Vector2 half;
        public Vector2 axX, axZ;
        public bool movable = true;
        public Vector2 start;
    }

    [MenuItem("Tools/좀비 오버런/프롭 겹침 정리")]
    public static void Fix()
    {
        var items = Collect();
        if (items == null) return;

        // 런타임(ProtoObstacles)이 실제로 보는 겹침 수
        int before = CountOverlaps(items, MoverRadius);
        // 정리 목표 기준 — 여유폭까지 확보해야 하므로 이쪽이 더 많이 잡힌다
        int beforeTarget = CountOverlaps(items, MoverRadius + Margin);
        if (beforeTarget == 0)
        {
            Debug.Log($"[겹침정리] 겹치는 쌍이 없다 (프롭 {items.Count}개, 여유 {Margin}m 확보됨). 이동 없음.");
            return;
        }

        var movers = new List<Object>();
        foreach (var it in items) if (it.movable) movers.Add(it.tr);
        Undo.RecordObjects(movers.ToArray(), "프롭 겹침 정리");

        int passes = Relaxate(items);
        int after       = CountOverlaps(items, MoverRadius);
        int afterTarget = CountOverlaps(items, MoverRadius + Margin);

        // 실제 트랜스폼에 반영 — XZ 만. Y 를 건드리면 접지(밑면 y=0)가 깨진다.
        int moved = 0;
        var report = new System.Text.StringBuilder();
        foreach (var it in items)
        {
            float d = Vector2.Distance(it.pos, it.start);
            if (d < 0.005f) continue;
            Vector2 pivot = it.pos - it.pivotOffset;
            var p = it.tr.position;
            it.tr.position = new Vector3(pivot.x, p.y, pivot.y);
            EditorUtility.SetDirty(it.tr);
            moved++;
            report.AppendLine($"    {it.tr.name}: {d:F2}m 이동 → ({pivot.x:F2}, {pivot.y:F2})");
        }

        Debug.Log($"[겹침정리] 런타임 겹침 {before} → {after} 쌍"
                + $" / 여유 {Margin}m 기준 {beforeTarget} → {afterTarget} 쌍"
                + $" / 완화 {passes}패스 / 프롭 {moved}개 이동\n{report}"
                + (after > 0 ? "\n  **남은 겹침은 제약(노면·아레나·스폰) 때문에 못 떼어놨다 — 아래 경고의 쌍을 손으로 옮길 것**" : ""));

        if (after > 0) LogRemaining(items, MoverRadius);
        Debug.Log("[겹침정리] 씬 저장은 직접 할 것 (Ctrl+S). 되돌리려면 Ctrl+Z.");
    }

    static List<Item> Collect()
    {
        var root = GameObject.Find("_Props");
        if (root == null) { Debug.LogError("[겹침정리] '_Props' 를 찾지 못했다."); return null; }

        var items = new List<Item>();
        foreach (Transform group in root.transform)
        {
            float shrink;
            if (!Shrink.TryGetValue(group.name, out shrink)) shrink = DefaultShrink;

            foreach (Transform inst in group)
            {
                var it = MakeItem(inst, group.name, shrink);
                if (it != null) items.Add(it);
            }
        }

        // 물 절벽 등 손으로 놓은 블로커는 **고정**이다. 세트피스라 옮기면 아트가 깨진다.
        foreach (var bl in Object.FindObjectsOfType<Proto.ProtoBlocker>())
        {
            var t = bl.transform;
            Vector3 s = t.lossyScale;
            float hx = bl.halfExtents.x * Mathf.Abs(s.x);
            float hz = bl.halfExtents.y * Mathf.Abs(s.z);
            if (hx < 0.05f || hz < 0.05f) continue;
            Vector3 w = t.TransformPoint(new Vector3(bl.center.x, 0f, bl.center.y));
            float yaw = t.eulerAngles.y * Mathf.Deg2Rad;
            items.Add(new Item
            {
                tr = t, group = "BLOCKER", movable = false,
                pos = new Vector2(w.x, w.z), start = new Vector2(w.x, w.z),
                pivotOffset = Vector2.zero,
                half = new Vector2(hx, hz),
                axX = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw)),   // 유니티 Euler Y 규약 — ProtoObstacles.Box 와 같다
                axZ = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)),
            });
        }
        return items;
    }

    /// ProtoObstacles.TryMakeBox 와 같은 계산. 여기가 갈라지면 정리 결과가 런타임과 안 맞는다.
    static Item MakeItem(Transform inst, string group, float shrink)
    {
        bool any = false;
        Bounds local = new Bounds();
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            Matrix4x4 toInst = inst.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Vector3 c = toInst.MultiplyPoint3x4(mb.center);
            Vector3 e = mb.extents;
            Vector3 ex = new Vector3(
                Mathf.Abs(toInst.m00)*e.x + Mathf.Abs(toInst.m01)*e.y + Mathf.Abs(toInst.m02)*e.z,
                Mathf.Abs(toInst.m10)*e.x + Mathf.Abs(toInst.m11)*e.y + Mathf.Abs(toInst.m12)*e.z,
                Mathf.Abs(toInst.m20)*e.x + Mathf.Abs(toInst.m21)*e.y + Mathf.Abs(toInst.m22)*e.z);
            var bb = new Bounds(c, ex * 2f);
            if (!any) { local = bb; any = true; } else local.Encapsulate(bb);
        }
        if (!any) return null;

        Vector3 scale = inst.lossyScale;
        Vector3 worldCenter = inst.TransformPoint(local.center);
        float hx = local.extents.x * Mathf.Abs(scale.x) * shrink;
        float hz = local.extents.z * Mathf.Abs(scale.z) * shrink;
        if (hx < 0.05f || hz < 0.05f) return null;

        float yaw = inst.eulerAngles.y * Mathf.Deg2Rad;
        var center = new Vector2(worldCenter.x, worldCenter.z);
        return new Item
        {
            tr = inst, group = group,
            pos = center, start = center,
            pivotOffset = center - new Vector2(inst.position.x, inst.position.z),
            half = new Vector2(hx, hz),
            axX = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw)),   // 유니티 Euler Y 규약: 인스턴스 로컬 x 축 = (cos, -sin) (XZ)
            axZ = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)),
        };
    }

    /// 부풀린 두 OBB 의 최소 이동 벡터. 겹치지 않으면 false.
    ///
    /// `inflate` 를 두 용도로 나눠 쓴다:
    ///  - 정리 목표: MoverRadius + Margin  → 떼어놓고 나면 Margin 만큼 진짜 여유가 남는다
    ///  - 런타임 검증: MoverRadius        → ProtoObstacles 가 실제로 보는 겹침
    static bool Mtv(Item a, Item b, float inflate, out Vector2 mtv)
    {
        mtv = Vector2.zero;
        Vector2 d = b.pos - a.pos;
        Vector2[] axes = { a.axX, a.axZ, b.axX, b.axZ };

        float best = float.MaxValue;
        Vector2 bestAxis = Vector2.zero;

        for (int k = 0; k < 4; k++)
        {
            Vector2 L = axes[k];
            float ra = (a.half.x + inflate) * Mathf.Abs(Vector2.Dot(L, a.axX))
                     + (a.half.y + inflate) * Mathf.Abs(Vector2.Dot(L, a.axZ));
            float rb = (b.half.x + inflate) * Mathf.Abs(Vector2.Dot(L, b.axX))
                     + (b.half.y + inflate) * Mathf.Abs(Vector2.Dot(L, b.axZ));
            float dist = Vector2.Dot(L, d);
            float pen = ra + rb - Mathf.Abs(dist);
            if (pen <= Eps) return false;             // 분리축을 찾았다 = 안 겹침
            if (pen < best)
            {
                best = pen;
                // a 에서 b 를 향하는 방향으로 정렬
                bestAxis = dist < 0f ? -L : L;
            }
        }
        mtv = bestAxis * best;
        return true;
    }

    static int CountOverlaps(List<Item> items, float inflate)
    {
        int n = 0;
        for (int i = 0; i < items.Count; i++)
        for (int j = i + 1; j < items.Count; j++)
        {
            Vector2 m;
            if (Mtv(items[i], items[j], inflate, out m)) n++;
        }
        return n;
    }

    static void LogRemaining(List<Item> items, float inflate)
    {
        for (int i = 0; i < items.Count; i++)
        for (int j = i + 1; j < items.Count; j++)
        {
            Vector2 m;
            if (Mtv(items[i], items[j], inflate, out m))
                Debug.LogWarning($"[겹침정리] 남은 겹침: {items[i].tr.name} × {items[j].tr.name} "
                               + $"(겹침 {m.magnitude:F3}m, 위치 {items[i].pos} / {items[j].pos})");
        }
    }

    static int Relaxate(List<Item> items)
    {
        int pass = 0;
        for (; pass < MaxPasses; pass++)
        {
            bool any = false;
            for (int i = 0; i < items.Count; i++)
            for (int j = i + 1; j < items.Count; j++)
            {
                var a = items[i]; var b = items[j];
                if (!a.movable && !b.movable) continue;

                Vector2 m;
                if (!Mtv(a, b, MoverRadius + Margin, out m)) continue;
                any = true;

                // 작은 프롭이 더 많이 움직인다 — 밴 같은 큰 랜드마크는 제자리에 두는 편이
                // 아트 의도를 덜 망친다. 한쪽이 고정이면 다른 쪽이 전부 감당한다.
                float wa = a.half.x * a.half.y, wb = b.half.x * b.half.y;
                float ta, tb;
                if (!a.movable)      { ta = 0f; tb = 1f; }
                else if (!b.movable) { ta = 1f; tb = 0f; }
                else                 { float s = wa + wb; ta = wb / s; tb = wa / s; }

                Vector2 push = m.normalized * (m.magnitude * Relax + Bite);
                if (ta > 0f) a.pos = Constrain(a, a.pos - push * ta);
                if (tb > 0f) b.pos = Constrain(b, b.pos + push * tb);
            }
            if (!any) break;
        }
        return pass;
    }

    /// 아레나 안, 노면 규칙, 스폰 여유를 지키도록 위치를 되돌린다.
    static Vector2 Constrain(Item it, Vector2 p)
    {
        p.x = Mathf.Clamp(p.x, -ArenaHalf, ArenaHalf);
        p.y = Mathf.Clamp(p.y, -ArenaHalf, ArenaHalf);

        // 나무·울타리는 노면 위로 오면 안 된다. 원래 있던 쪽 차선 밖으로 되민다.
        if (OffRoad.Contains(it.group) && Mathf.Abs(p.x) < OffRoadMinX)
            p.x = Mathf.Sign(it.start.x == 0f ? 1f : it.start.x) * OffRoadMinX;

        // 스폰 주변을 비워 둔다
        float r = p.magnitude;
        if (r < SpawnClear && r > 1e-4f) p = p / r * SpawnClear;

        return p;
    }
}
