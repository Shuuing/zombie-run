using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 모델에서 총구(총열 끝)를 추정한다. 런타임(<c>ProtoShooter</c>)과 그립 툴이 같은 계산을 쓰려고 뺐다.
///
/// <para><b>가장 긴 축이 총열</b>이고, 그 축의 두 끝 중 <b>손에서 먼 쪽</b>이 총구다 — 정면 기준으로 고르면
/// 자세에 따라 앞뒤가 뒤집힌다(바인드 포즈에서는 팔이 옆으로 뻗어 있다).</para>
///
/// <para><paramref name="useVertices"/> 면 끝에서 길이의 4% 안에 든 정점들의 중심을 쓴다 — 총구 구멍(총열 단면)의
/// 중심에 가깝다. 아니면 바운드 끝 면의 중심(예전 방식)이라 상자 한가운데라서 총열 아래·위로 어긋날 수 있다.
/// 정점 읽기는 에디터에선 항상 되지만 빌드에선 Read/Write 가 꺼진 메시면 안 되므로, 런타임은
/// <see cref="WeaponModelDef.muzzlePoint"/> 에 저장된 값을 먼저 쓴다.</para>
/// </summary>
public static class MuzzleFinder
{
    /// <summary>
    /// 총열 방향(무기 로컬, 단위 벡터) — 무기의 가장 긴 축 중 <paramref name="muzzleLocal"/> 쪽.
    /// 바운드만 보므로 빌드에서도 된다. 화염이 몸 정면이 아니라 총열을 따라 나가게 할 때 쓴다.
    /// </summary>
    public static Vector3 BarrelAxis(Transform w, Vector3 muzzleLocal)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var r in w.GetComponentsInChildren<Renderer>(true))
        {
            Mesh me = null;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null) me = mf.sharedMesh;
            if (r is SkinnedMeshRenderer sk) me = sk.sharedMesh;
            if (me == null) continue;
            Bounds mb = me.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                var lp = w.InverseTransformPoint(r.transform.TransformPoint(c));
                if (!any) { b = new Bounds(lp, Vector3.zero); any = true; } else b.Encapsulate(lp);
            }
        }
        if (!any) return Vector3.forward;
        Vector3 size = b.size;
        int ax = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
        var dir = Vector3.zero;
        dir[ax] = muzzleLocal[ax] >= b.center[ax] ? 1f : -1f;
        return dir;
    }

    public static bool TryFind(Transform w, Vector3 gripLocal, bool useVertices, out Vector3 local)
    {
        local = Vector3.zero;
        var pts = new List<Vector3>();
        bool verts = false;
        foreach (var r in w.GetComponentsInChildren<Renderer>(true))
        {
            Mesh me = null;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null) me = mf.sharedMesh;
            if (r is SkinnedMeshRenderer sk) me = sk.sharedMesh;
            if (me == null) continue;

            if (useVertices && (me.isReadable || Application.isEditor))
            {
                foreach (var v in me.vertices) pts.Add(w.InverseTransformPoint(r.transform.TransformPoint(v)));
                verts = true;
                continue;
            }
            Bounds mb = me.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                pts.Add(w.InverseTransformPoint(r.transform.TransformPoint(c)));
            }
        }
        if (pts.Count == 0) return false;

        var b = new Bounds(pts[0], Vector3.zero);
        foreach (var p in pts) b.Encapsulate(p);
        Vector3 size = b.size;
        int ax = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
        bool maxEnd = Mathf.Abs(b.max[ax] - gripLocal[ax]) >= Mathf.Abs(b.min[ax] - gripLocal[ax]);
        float end = maxEnd ? b.max[ax] : b.min[ax];

        if (!verts)
        {
            local = b.center;
            local[ax] = end;
            return true;
        }

        // 끝에서 길이의 4% 안쪽 정점들의 중심 = 총열 끝 단면의 중심
        float band = size[ax] * 0.04f;
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var p in pts)
            if (Mathf.Abs(p[ax] - end) <= band) { sum += p; n++; }
        local = n > 0 ? sum / n : b.center;
        local[ax] = end;
        return true;
    }
}
