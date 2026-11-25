using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 도시 풀잎 굽기 — 지면이 칠한 풀 자리에 풀잎(정점 5개짜리 삼각형 3개)을 심어 8m 블록마다 메시 하나로 합친다.
///
/// <para><b>풀 자리:</b> CityGround 메시를 위에서 격자(0.125m)로 래스터화해 맨 위 면을 읽는다.
/// 공원 잔디(서브메시 3) = 빽빽한 잔디, 그 밖은 정점 컬러 A(GroundCover 의 풀 층, 0.5 가 경계) = 길가 잡초(드문드문·짧게·마른 색).
/// 연석 윗면은 풀 없음. 프롭(건물·차·컨테이너) 바닥은 뺀다 — 길가 풀 층이 건물 밑동에서 진해서, 안 빼면 건물 안에 풀이 난다.</para>
///
/// <para><b>블록:</b> 8m — 화면(약 22×12m) 밖 블록은 컬링된다. 블록당 정점 6.5만(16비트 인덱스) 아래.
/// 정점 데이터는 Proto/Grass 셰이더 머리 주석 참고. 런타임 비용은 렌더러뿐(스크립트가 매 프레임 하는 일 없음).</para>
///
/// <para>도시를 다시 만들면(CityBuilder.Build) 같이 다시 굽는다. 메뉴로 따로 굽기도 된다.</para>
/// </summary>
public static class CityGrassBaker
{
    const string MeshAsset = "Assets/_Game/Art/City/CityGrass.asset";
    const string MatPath   = "Assets/_Game/Art/City/Materials/M_City_GrassBlades.mat";
    const string MaskAsset = "Assets/_Game/Art/City/CityGrassMask.asset";   // 런타임 풀 지도(ProtoGrassKick)
    const string RootName  = "_GrassBlades";
    const int    LawnSubmesh = 3, CurbSubmesh = 2;

    const float Cell  = 0.125f;     // 풀 자리 격자(m)
    const float Block = 8f;         // 메시 블록(m)
    const float LawnDensity = 80f;  // 잔디 풀잎/m²
    const float WeedDensity = 0.45f;// 길가 잡초 = 잔디 밀도 × 이것
    const int   Seed = 20261020;

    [MenuItem("Tools/좀비 오버런/도시 풀잎 굽기")]
    public static void BakeMenu()
    {
        var city = GameObject.Find("_City");
        if (city == null) { Debug.LogWarning("[풀잎] _City 없음 — 도시 테스트 씬을 열고 실행"); return; }
        var map = city.GetComponent<Proto.CityMap>();
        Bake(city.transform, map != null ? map.halfSize : 86f);
        EditorSceneManager.MarkSceneDirty(city.scene);
        EditorSceneManager.SaveScene(city.scene);
    }

    class Mask
    {
        public int n; public float half;
        public float[] w, y; public bool[] lawn;
        public int Index(float x, float z)
        {
            int ix = Mathf.FloorToInt((x + half) / Cell), iz = Mathf.FloorToInt((z + half) / Cell);
            return ix < 0 || iz < 0 || ix >= n || iz >= n ? -1 : iz * n + ix;
        }
    }

    public static void Bake(Transform city, float half)
    {
        var mask = new Mask { half = half, n = Mathf.CeilToInt(half * 2f / Cell) };
        int cells = mask.n * mask.n;
        mask.w = new float[cells]; mask.y = new float[cells]; mask.lawn = new bool[cells];
        for (int i = 0; i < cells; i++) mask.y[i] = float.NegativeInfinity;

        Raster(mask, city.Find("Ground"), true);
        Raster(mask, city.Find("ParkingLots"), false);
        Raster(mask, city.Find(CityTestLawn.LawnName), false, true);   // 테스트 잔디 블록(있으면) — 서브메시 0 = 잔디, 1 = 연석
        int cut = CutProps(mask);

        // ── 풀잎 심기 ──
        var rng = new System.Random(Seed);
        float spacing = 1f / Mathf.Sqrt(LawnDensity);
        int per = Mathf.CeilToInt(Block / spacing);
        int blocks = Mathf.CeilToInt(half * 2f / Block);
        var meshes = new List<Mesh>();
        int blades = 0, verts = 0;
        var b = new Builder();
        for (int bz = 0; bz < blocks; bz++)
            for (int bx = 0; bx < blocks; bx++)
            {
                b.Clear();
                float x0 = -half + bx * Block, z0 = -half + bz * Block;
                for (int gz = 0; gz < per; gz++)
                    for (int gx = 0; gx < per; gx++)
                    {
                        float px = x0 + (gx + (float)rng.NextDouble()) * spacing;
                        float pz = z0 + (gz + (float)rng.NextDouble()) * spacing;
                        int idx = mask.Index(px, pz);
                        if (idx < 0) continue;
                        float w = mask.w[idx];
                        if (w <= 0f) continue;
                        bool lawn = mask.lawn[idx];
                        if (rng.NextDouble() > (lawn ? w : w * WeedDensity)) continue;
                        b.Blade(new Vector3(px, mask.y[idx], pz), lawn, w, rng);
                    }
                if (b.Count == 0) continue;
                var m = b.Build($"Grass_{bx}_{bz}");
                meshes.Add(m);
                blades += b.Count; verts += m.vertexCount;
            }

        // ── 저장: 메시는 에셋 하나에 묶는다 ──
        if (AssetDatabase.LoadAssetAtPath<Object>(MeshAsset) != null) AssetDatabase.DeleteAsset(MeshAsset);
        if (meshes.Count > 0)
        {
            AssetDatabase.CreateAsset(meshes[0], MeshAsset);
            for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], MeshAsset);
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            var sh = Shader.Find("Proto/Grass");
            if (sh == null) { Debug.LogError("[풀잎] Proto/Grass 셰이더 없음"); return; }
            mat = new Material(sh) { name = "M_City_GrassBlades" };
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        AssetDatabase.SaveAssets();

        // ── 씬: _City/_GrassBlades/Grass_x_z ──
        var old = city.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(RootName).transform;
        root.SetParent(city, false);
        root.position = Vector3.zero; root.rotation = Quaternion.identity;   // 정점이 월드 좌표다
        root.gameObject.AddComponent<Proto.ProtoGrassBlades>().mask = BuildRuntimeMask(mask);
        foreach (var m in meshes)
        {
            var go = new GameObject(m.name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            // 정적 배칭은 켜지 않는다 — 블록 메시가 이미 합쳐져 있고, 합치면 블록 단위 컬링이 사라진다
        }
        Debug.Log($"[풀잎] 블록 {meshes.Count}개, 풀잎 {blades:N0}개, 정점 {verts:N0}개 (프롭 바닥 {cut}개 제외). 저장: {MeshAsset}");
    }

    /// 런타임 풀 지도(0.5m 칸) — 풀 조각 튀기(ProtoGrassKick)가 "여기가 풀밭인가"를 읽는다.
    /// 굽기 격자 4×4 칸을 평균 낸다. 밀도 = 실제로 심은 비율(잔디 w, 잡초 w × WeedDensity).
    static Proto.ProtoGrassMask BuildRuntimeMask(Mask mask)
    {
        const float RCell = 0.5f;
        int k = Mathf.RoundToInt(RCell / Cell);
        int n = Mathf.CeilToInt(mask.n / (float)k);
        var dens = new byte[n * n];
        var lawnB = new byte[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float sum = 0f; int lawnCount = 0, cnt = 0;
                for (int dz = 0; dz < k; dz++)
                    for (int dx = 0; dx < k; dx++)
                    {
                        int ix = x * k + dx, iz = z * k + dz;
                        if (ix >= mask.n || iz >= mask.n) continue;
                        int i = iz * mask.n + ix;
                        cnt++;
                        float w = Mathf.Max(0f, mask.w[i]);
                        if (w <= 0f) continue;
                        if (mask.lawn[i]) { sum += w; lawnCount++; }
                        else sum += w * WeedDensity;
                    }
                int o = z * n + x;
                dens[o] = (byte)Mathf.Clamp(Mathf.RoundToInt(sum / Mathf.Max(1, cnt) * 255f), 0, 255);
                lawnB[o] = (byte)(lawnCount * 2 >= cnt ? 1 : 0);
            }

        var asset = AssetDatabase.LoadAssetAtPath<Proto.ProtoGrassMask>(MaskAsset);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<Proto.ProtoGrassMask>();
            AssetDatabase.CreateAsset(asset, MaskAsset);
        }
        asset.half = mask.half; asset.cell = RCell; asset.n = n;
        asset.density = dens; asset.lawn = lawnB;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    // ── 래스터화: 위를 보는 면만, 칸마다 가장 높은 면이 이긴다 ──
    /// lawnMesh = 서브메시 0 이 통째로 잔디인 메시(테스트 잔디 블록). 나머지 서브메시는 풀 없음.
    static void Raster(Mask mask, Transform t, bool ground, bool lawnMesh = false)
    {
        if (t == null) return;
        var mf = t.GetComponent<MeshFilter>();
        var mesh = mf != null ? mf.sharedMesh : null;
        if (mesh == null) return;
        if (!mesh.isReadable) { Debug.LogWarning($"[풀잎] {mesh.name} 읽기 불가 — 건너뜀"); return; }
        var l2w = t.localToWorldMatrix;
        var vs = mesh.vertices;
        var cs = mesh.colors32;
        var wv = new Vector3[vs.Length];
        for (int i = 0; i < vs.Length; i++) wv[i] = l2w.MultiplyPoint3x4(vs[i]);
        bool hasColor = cs != null && cs.Length == vs.Length;

        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var tris = mesh.GetTriangles(s);
            for (int k = 0; k < tris.Length; k += 3)
            {
                int i0 = tris[k], i1 = tris[k + 1], i2 = tris[k + 2];
                Vector3 a = wv[i0], b = wv[i1], c = wv[i2];
                Vector3 nrm = Vector3.Cross(b - a, c - a);
                if (nrm.sqrMagnitude < 1e-10f || Mathf.Abs(nrm.normalized.y) < 0.7f) continue;   // 옆면(연석 턱)

                float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                int ix0 = Mathf.Max(0, Mathf.FloorToInt((minX + mask.half) / Cell)), ix1 = Mathf.Min(mask.n - 1, Mathf.FloorToInt((maxX + mask.half) / Cell));
                int iz0 = Mathf.Max(0, Mathf.FloorToInt((minZ + mask.half) / Cell)), iz1 = Mathf.Min(mask.n - 1, Mathf.FloorToInt((maxZ + mask.half) / Cell));
                float det = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(det) < 1e-9f) continue;

                for (int iz = iz0; iz <= iz1; iz++)
                    for (int ix = ix0; ix <= ix1; ix++)
                    {
                        float px = -mask.half + (ix + 0.5f) * Cell, pz = -mask.half + (iz + 0.5f) * Cell;
                        float w0 = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / det;
                        float w1 = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / det;
                        float w2 = 1f - w0 - w1;
                        if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                        float y = a.y * w0 + b.y * w1 + c.y * w2;
                        int idx = iz * mask.n + ix;
                        if (y <= mask.y[idx]) continue;
                        mask.y[idx] = y;
                        bool lawn = lawnMesh ? s == 0 : ground && s == LawnSubmesh;
                        mask.lawn[idx] = lawn;
                        if (lawn) mask.w[idx] = 1f;
                        else if (lawnMesh || (ground && s == CurbSubmesh)) mask.w[idx] = 0f;
                        else if (hasColor)
                        {
                            float al = (cs[i0].a * w0 + cs[i1].a * w1 + cs[i2].a * w2) / 255f;
                            mask.w[idx] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.66f, al));   // 지면 셰이더의 풀 층 경계(0.5)
                        }
                        else mask.w[idx] = 0f;
                    }
            }
        }
    }

    /// 프롭 바닥(ProtoObstacles 와 같은 측정)을 풀 자리에서 뺀다. 나무는 줄기만.
    static int CutProps(Mask mask)
    {
        var props = GameObject.Find("_Props");
        if (props == null) return 0;
        int count = 0;
        foreach (Transform group in props.transform)
        {
            float shrink = group.name.StartsWith("Tree") ? 0.32f : 1f;
            foreach (Transform inst in group)
            {
                if (!Proto.ProtoObstacles.MeasureFootprint(inst, out var c, out var h, out var yaw, out _)) continue;
                h *= shrink;
                float cos = Mathf.Cos(yaw), sin = Mathf.Sin(yaw), r = h.magnitude;
                int ix0 = Mathf.Max(0, Mathf.FloorToInt((c.x - r + mask.half) / Cell)), ix1 = Mathf.Min(mask.n - 1, Mathf.FloorToInt((c.x + r + mask.half) / Cell));
                int iz0 = Mathf.Max(0, Mathf.FloorToInt((c.y - r + mask.half) / Cell)), iz1 = Mathf.Min(mask.n - 1, Mathf.FloorToInt((c.y + r + mask.half) / Cell));
                for (int iz = iz0; iz <= iz1; iz++)
                    for (int ix = ix0; ix <= ix1; ix++)
                    {
                        float dx = -mask.half + (ix + 0.5f) * Cell - c.x, dz = -mask.half + (iz + 0.5f) * Cell - c.y;
                        // yaw 틀로 돌려 사각형 안인지(ProtoObstacles 박스와 같은 회전 규칙)
                        float lx = dx * cos + dz * sin, lz = -dx * sin + dz * cos;
                        if (Mathf.Abs(lx) <= h.x && Mathf.Abs(lz) <= h.y) mask.w[iz * mask.n + ix] = 0f;
                    }
                count++;
            }
        }
        return count;
    }

    class Builder
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector2> _uv0 = new List<Vector2>(), _uv1 = new List<Vector2>();
        readonly List<Vector3> _uv2 = new List<Vector3>();
        readonly List<Color32> _col = new List<Color32>();
        readonly List<int> _tri = new List<int>();
        public int Count;

        public void Clear() { _v.Clear(); _uv0.Clear(); _uv1.Clear(); _uv2.Clear(); _col.Clear(); _tri.Clear(); Count = 0; }

        public void Blade(Vector3 root, bool lawn, float w, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            // 카메라가 ~18m 위 — 가늘면 잔디 텍스처 위의 점으로만 보여 밟힌 자국이 안 읽혔다(2026-10-20 확인). 넓고 조금 길게.
            float H = Mathf.Lerp(0.26f, 0.48f, R()) * (lawn ? 1f : Mathf.Lerp(0.55f, 0.85f, R())) * Mathf.Lerp(0.7f, 1f, w);
            float wid = Mathf.Lerp(0.07f, 0.11f, R());
            float yaw = R() * Mathf.PI * 2f, leanYaw = R() * Mathf.PI * 2f;
            var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            // 위에서 내려다보는 카메라라 꼿꼿하면 풀잎이 점으로 보인다 — 처음부터 조금씩 기울여 둔다
            var lean = new Vector3(Mathf.Cos(leanYaw), 0f, Mathf.Sin(leanYaw)) * H * Mathf.Lerp(0.15f, 0.45f, R());
            float rnd = R();

            // 색: 밝기 ±, 가끔 누렇게
            float bright = Mathf.Lerp(0.78f, 1f, R());
            Color tint = (R() < 0.12f ? new Color(1f, 0.92f, 0.62f) : Color.white) * bright;
            var col = new Color32(B(tint.r), B(tint.g), B(tint.b), (byte)(lawn ? 255 : 0));

            int i = _v.Count;
            Add(root - side * wid * 0.5f, -1f, 0f);
            Add(root + side * wid * 0.5f, 1f, 0f);
            Add(root + lean * 0.25f + Vector3.up * H * 0.5f - side * wid * 0.32f, -1f, 0.5f);
            Add(root + lean * 0.25f + Vector3.up * H * 0.5f + side * wid * 0.32f, 1f, 0.5f);
            Add(root + lean + Vector3.up * H, 0f, 1f);
            _tri.Add(i); _tri.Add(i + 2); _tri.Add(i + 1);
            _tri.Add(i + 1); _tri.Add(i + 2); _tri.Add(i + 3);
            _tri.Add(i + 2); _tri.Add(i + 4); _tri.Add(i + 3);
            Count++;

            void Add(Vector3 p, float s, float h)
            {
                _v.Add(p); _uv0.Add(new Vector2(s, h)); _uv1.Add(new Vector2(root.x, root.z));
                _uv2.Add(new Vector3(H, rnd, root.y)); _col.Add(col);
            }
        }

        static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            m.indexFormat = _v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(_v);
            m.SetUVs(0, _uv0);
            m.SetUVs(1, _uv1);
            m.SetUVs(2, _uv2);
            m.SetColors(_col);
            m.SetTriangles(_tri, 0);
            m.RecalculateBounds();
            var bb = m.bounds; bb.Expand(new Vector3(0.9f, 0.3f, 0.9f));   // 휘어서 옆으로 나가는 만큼
            m.bounds = bb;
            return m;
        }
    }
}
