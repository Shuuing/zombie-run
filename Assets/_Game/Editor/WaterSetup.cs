// 폭포/호수 디오라마 셋업 — FBX + 머티리얼 + 충돌 볼륨을 하나의 프리팹으로 굽는다.
//
// 지형 자체는 Blender 가 만든다 (ArtSource/blender/make_waterfall.py → WaterfallLake.fbx).
// 그 FBX 를 다시 뽑을 때마다 손으로 머티리얼을 다시 물리고 충돌을 다시 놓는 일이 없도록,
// 이 메뉴가 프리팹을 통째로 재생성한다. FieldBuilder 는 이 프리팹만 배치한다.
using Proto;
using UnityEditor;
using UnityEngine;

public static class WaterSetup
{
    const string Root       = "Assets/_Game/Art/Field/Water";
    const string Fbx        = Root + "/WaterfallLake.fbx";
    const string PrefabPath = Root + "/WaterfallLake_Diorama.prefab";

    // 호수를 막을지. **기본 false — 플레이어가 물에 들어갈 수 있어야 한다는 결정.**
    // true 로 하면 림 바깥에서 막혀 물이 통과 불가능한 장애물이 된다.
    const bool BlockLake = false;

    // 절벽은 물이 아니라 4.5m 암벽이라 계속 막는다. 이것까지 통과시키려면 false.
    const bool BlockCliff = true;

    // 림(암반 테두리)을 타원으로 근사할 때의 여유. 1.0 이면 메시 바운즈에 딱 맞고,
    // 그러면 둥근 사각형의 모서리에서 플레이어가 암반에 0.4m 쯤 파묻힌다.
    // 반대로 크게 잡으면 잔디 위에 보이지 않는 벽이 생긴다. 1.04 가 양쪽 오차를 0.2m 대로 맞춘다.
    const float LakeFit = 1.04f;

    [MenuItem("Tools/좀비 오버런/물 디오라마 셋업")]
    public static void Build()
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (fbx == null)
        {
            Debug.LogError($"[Water] FBX 가 없다: {Fbx}\n" +
                           "  ArtSource/blender/make_waterfall.py 를 Blender 헤드리스로 먼저 돌릴 것");
            return;
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = "WaterfallLake";
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;

        // ---- 머티리얼 ----------------------------------------------------
        int matched = 0;
        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var m = MatFor(mr.name);
            if (m == null) { Debug.LogWarning($"[Water] 머티리얼을 못 정했다: {mr.name}"); continue; }
            mr.sharedMaterial = m;
            matched++;
        }

        // 폭포 셰이더 토글은 float 만 바꾸면 안 된다 — 키워드도 같이 켜야 한다
        FixKeywords();

        // ---- 충돌 볼륨 ----------------------------------------------------
        // 디오라마 전체 바운즈로 박스를 만들면 절벽 뒤 빈 공간까지 막히므로,
        // 막아야 할 파트(림·절벽)의 로컬 바운즈에서 직접 만든다.
        Bounds rim, cliff;
        bool hasRim   = LocalBounds(root, "Rock_Rim",   out rim);
        bool hasCliff = LocalBounds(root, "Rock_Cliff", out cliff);

        if (hasRim && BlockLake)
        {
            // 호수는 둥근 사각형이라 박스로 덮으면 네 모서리에 1m 넘는 보이지 않는 벽이 생긴다
            var b = AddBlocker(root, "Blocker_Lake", ProtoBlocker.Shape.Ellipse);
            b.center      = new Vector2(rim.center.x, rim.center.z);
            b.halfExtents = new Vector2(rim.extents.x * LakeFit, rim.extents.z * LakeFit);
        }

        if (hasCliff && BlockCliff)
        {
            // 절벽은 직선 벽이라 박스가 맞다. 다만 림과 겹치는 부분은 잘라낸다 —
            // 두 볼륨이 겹치면 밀어내기가 서로를 되받아쳐 지오메트리 안에 갇힌다.
            float zMin = cliff.min.z, zMax = cliff.max.z;
            if (hasRim && BlockLake)
            {
                if (cliff.center.z > rim.center.z) zMin = Mathf.Max(zMin, rim.max.z);
                else                               zMax = Mathf.Min(zMax, rim.min.z);
            }
            if (zMax - zMin > 0.2f)
            {
                var b = AddBlocker(root, "Blocker_Cliff", ProtoBlocker.Shape.Box);
                b.center      = new Vector2(cliff.center.x, (zMin + zMax) * 0.5f);
                b.halfExtents = new Vector2(cliff.extents.x, (zMax - zMin) * 0.5f);
            }
        }

        // ---- 프리팹 저장 ---------------------------------------------------
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Water] 프리팹 갱신 — 머티리얼 {matched}개, 호수 막기 {(hasRim && BlockLake ? "O" : "X")}, 절벽 막기 {(hasCliff && BlockCliff ? "O" : "X")}\n  {PrefabPath}");
    }

    static ProtoBlocker AddBlocker(GameObject root, string name, ProtoBlocker.Shape shape)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var b = go.AddComponent<ProtoBlocker>();
        b.shape = shape;
        return b;
    }

    /// 파트의 메시 바운즈를 루트 로컬 공간으로 옮겨 돌려준다.
    static bool LocalBounds(GameObject root, string partName, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            if (!mf.name.StartsWith(partName)) continue;
            var mb = mf.sharedMesh.bounds;
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Vector3 c = toRoot.MultiplyPoint3x4(mb.center);
            Vector3 e = mb.extents;
            Vector3 ex = new Vector3(
                Mathf.Abs(toRoot.m00) * e.x + Mathf.Abs(toRoot.m01) * e.y + Mathf.Abs(toRoot.m02) * e.z,
                Mathf.Abs(toRoot.m10) * e.x + Mathf.Abs(toRoot.m11) * e.y + Mathf.Abs(toRoot.m12) * e.z,
                Mathf.Abs(toRoot.m20) * e.x + Mathf.Abs(toRoot.m21) * e.y + Mathf.Abs(toRoot.m22) * e.z);
            var bb = new Bounds(c, ex * 2f);
            if (!any) { bounds = bb; any = true; } else bounds.Encapsulate(bb);
        }
        return any;
    }

    static Material MatFor(string meshName)
    {
        string file;
        if (meshName.StartsWith("Water_Lake"))        file = "M_Water_Lake";
        else if (meshName.StartsWith("Water_Fall"))   file = "M_Water_Fall";
        else if (meshName.StartsWith("Water_Splash")) file = "M_Water_Splash";
        else if (meshName.StartsWith("Lake_Bed"))     file = "M_Water_Bed";
        else if (meshName.StartsWith("Rock_"))        file = "M_Water_Rock";
        else return null;
        return AssetDatabase.LoadAssetAtPath<Material>($"{Root}/{file}.mat");
    }

    /// [Toggle(KEYWORD)] 프로퍼티는 float 값과 셰이더 키워드가 따로 논다.
    /// 머티리얼을 코드로 만졌거나 셰이더를 다시 임포트하면 키워드만 빠져 폭포가 호수처럼 보인다.
    static void FixKeywords()
    {
        foreach (var name in new[] { "M_Water_Lake", "M_Water_Fall", "M_Water_Splash" })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/{name}.mat");
            if (m == null) continue;
            Toggle(m, "_WaterfallMode",  "BCM_WATER_FALL");
            Toggle(m, "_UseLightTint",   "BCM_WATER_LIGHTTINT");
            Toggle(m, "_UsePatternTex",  "BCM_WATER_PATTERNTEX");
            Toggle(m, "_UseSurfaceTex",  "BCM_WATER_SURFACETEX");
            Toggle(m, "_UseShoreline",   "BCM_WATER_SHORELINE");
            Toggle(m, "_UseWaves",       "BCM_WATER_WAVES");
            Toggle(m, "_UseContactFoam", "BCM_WATER_CONTACT");
            EditorUtility.SetDirty(m);
        }
    }

    static void Toggle(Material m, string prop, string keyword)
    {
        if (!m.HasProperty(prop)) return;
        if (m.GetFloat(prop) > 0.5f) m.EnableKeyword(keyword);
        else                         m.DisableKeyword(keyword);
    }
}
