// 얕은 물 테스트용 웅덩이 — **임시**. 접촉 포말(WaterContactFoam)이 수심에 따라 어떻게 보이는지
// 비교하려고 만든 것이라 필드 아트가 아니다. 판단이 끝나면 '제거' 메뉴로 통째로 지울 것.
//
// 플레이어 y 는 ProtoPlayer 가 0 으로 고정하므로 **수심 = 수면 높이**다.
// 바닥을 파서 얕게 만드는 건 이 프로젝트에서는 의미가 없다 — 그래서 높이가 다른 웅덩이 3개를 놓는다.
using UnityEditor;
using UnityEngine;

public static class ShallowWaterTest
{
    const string RootName = "_ShallowWater_Test";
    const string Dir      = "Assets/_Game/Art/Field/Water/Test";
    const string MeshPath = Dir + "/ShallowPond_Test.asset";
    const string MatPath  = Dir + "/M_Water_Shallow_Test.mat";

    // 웅덩이 중심 XZ (프롭 배치를 훑어 비어 있는 동쪽 라인으로 골랐다) 와 수심
    static readonly Vector2[] Spots  = { new Vector2(-1.2f, -10f), new Vector2(3.0f, -10f), new Vector2(7.2f, -10f) };
    static readonly float[]   Depths = { 0.10f, 0.20f, 0.35f };   // 발목 / 정강이 / 무릎
    const float PondRadius = 1.7f;

    [MenuItem("Tools/좀비 오버런/얕은 물 테스트 (임시) 추가")]
    public static void Add()
    {
        Remove(false);

        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/_Game/Art/Field/Water", "Test");

        var mesh = BuildPond(PondRadius);
        AssetDatabase.CreateAsset(mesh, MeshPath);
        var mat = BuildMaterial();

        var root = new GameObject(RootName);
        for (int i = 0; i < Spots.Length; i++)
        {
            var go = new GameObject($"Pond_{Mathf.RoundToInt(Depths[i] * 100):D3}cm");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(Spots[i].x, Depths[i], Spots[i].y);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        Undo.RegisterCreatedObjectUndo(root, "얕은 물 테스트 추가");
        Selection.activeGameObject = root;
        EditorSceneMarkDirty();
        Debug.Log($"[얕은물] 웅덩이 {Spots.Length}개 배치 — 수심 {string.Join(" / ", System.Array.ConvertAll(Depths, d => $"{d * 100:F0}cm"))}\n" +
                  "  호수는 55cm 다. 막는 볼륨은 안 넣었으니 그냥 걸어 들어가면 된다.\n" +
                  "  임시물이다 — 'Tools ▸ 좀비 오버런 ▸ 얕은 물 테스트 (임시) 제거' 로 지운다.");
    }

    [MenuItem("Tools/좀비 오버런/얕은 물 테스트 (임시) 제거")]
    public static void RemoveMenu() => Remove(true);

    static void Remove(bool log)
    {
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        AssetDatabase.DeleteAsset(MeshPath);
        if (old != null) EditorSceneMarkDirty();
        if (log) Debug.Log(old != null ? "[얕은물] 제거했다" : "[얕은물] 지울 게 없다");
    }

    static void EditorSceneMarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    /// 원형 수면 한 장. 정점 컬러에 셰이더가 읽는 값을 굽는다 —
    /// R = 깊이(0 물가 ~ 1 중앙), G = 물가 포말, B = 흐름 좌표. (make_waterfall.py 의 build_lake 와 같은 규칙)
    static Mesh BuildPond(float radius)
    {
        const int Rings = 8, Segs = 40;
        var verts = new Vector3[Rings * Segs + 1];
        var cols  = new Color[verts.Length];
        var uvs   = new Vector2[verts.Length];

        // 중앙 정점
        verts[0] = Vector3.zero;
        cols[0]  = new Color(1f, 0f, 0.5f, 1f);
        uvs[0]   = Vector2.zero;

        for (int r = 0; r < Rings; r++)
        {
            float t = (r + 1) / (float)Rings;          // 0(안) ~ 1(가장자리)
            for (int s = 0; s < Segs; s++)
            {
                float a = s / (float)Segs * Mathf.PI * 2f;
                // 가장자리를 살짝 일그러뜨린다 — 정원이면 CG 같다
                float wob = 1f + Mathf.Sin(a * 3f) * 0.06f + Mathf.Sin(a * 7f + 1.3f) * 0.035f;
                float rr  = radius * t * wob;
                int i = 1 + r * Segs + s;
                verts[i] = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                float f = 1f - t;                       // 0 물가 ~ 1 중앙
                cols[i] = new Color(Mathf.Pow(f, 0.75f), Mathf.Max(0f, 1f - f * 6f), (Mathf.Sin(a) + 1f) * 0.5f, 1f);
                uvs[i]  = new Vector2(verts[i].x * 0.22f, verts[i].z * 0.22f);
            }
        }

        var tris = new System.Collections.Generic.List<int>();
        for (int s = 0; s < Segs; s++)                  // 중앙 팬
            tris.AddRange(new[] { 0, 1 + (s + 1) % Segs, 1 + s });
        for (int r = 0; r < Rings - 1; r++)
            for (int s = 0; s < Segs; s++)
            {
                int a = 1 + r * Segs + s, b = 1 + r * Segs + (s + 1) % Segs;
                int c = a + Segs, d = b + Segs;
                tris.AddRange(new[] { a, b, d, a, d, c });
            }

        var m = new Mesh { name = "ShallowPond_Test" };
        m.vertices = verts; m.colors = cols; m.uv = uvs;
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static Material BuildMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("BCM/BCM_StylizedWater"));
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        // 얕은 물이라 호수보다 훨씬 투명하다 — 바닥(잔디)이 비쳐야 얕아 보인다
        mat.SetColor("_ShallowColor", new Color(0.35f, 0.88f, 0.95f, 0.20f));
        mat.SetColor("_DeepColor",    new Color(0.05f, 0.62f, 0.74f, 0.42f));
        mat.SetColor("_FoamColor",    new Color(0.96f, 0.99f, 1f, 1f));
        mat.SetColor("_RippleColor",  new Color(0.82f, 0.96f, 0.98f, 1f));
        mat.SetVector("_FlowDir", new Vector4(0f, 1f, 0f, 0f));

        mat.SetFloat("_ColorSteps", 4f);
        mat.SetFloat("_RippleScale", 13f); mat.SetFloat("_RippleWidth", 0.028f);
        mat.SetFloat("_RippleStretch", 3f); mat.SetFloat("_RippleAmount", 0.15f);
        mat.SetFloat("_RippleSpeed", 0.22f); mat.SetFloat("_FlowSpeed", 0.3f);
        mat.SetFloat("_FoamWidth", 0.16f); mat.SetFloat("_FoamWobble", 0.3f); mat.SetFloat("_FoamSpeed", 0.9f);
        mat.SetFloat("_LightTint", 0.35f); mat.SetFloat("_Glossy", 0.4f);

        // 접촉 포말 — 호수와 같은 값이라야 수심만 놓고 비교가 된다
        mat.SetFloat("_UseContactFoam", 1f);
        mat.SetFloat("_ContactWidth", 0.45f);  mat.SetFloat("_ContactInner", 0.25f);
        mat.SetFloat("_ContactWobble", 0.3f);  mat.SetFloat("_ContactWobScale", 7f);
        mat.SetFloat("_ContactSpeed", 1.1f);   mat.SetFloat("_ContactYRange", 0.7f);
        mat.SetFloat("_ContactStrength", 1f);

        // [Toggle] 은 float 과 키워드가 따로 논다 — 둘 다 세팅해야 켜진다
        mat.EnableKeyword("BCM_WATER_CONTACT");
        mat.EnableKeyword("BCM_WATER_LIGHTTINT");
        mat.SetFloat("_UseLightTint", 1f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
