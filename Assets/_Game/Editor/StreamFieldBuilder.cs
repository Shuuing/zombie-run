// 배경 스트리밍 테스트 씬 생성 — Proto_Chapter01 을 열어 '박힌 배경'만 걷어내고
// ProtoFieldStreamer 를 얹어 Proto_Stream_Test 로 저장한다.
//
// **왜 새 씬인가.** 프로토 씬에는 플레이어·카메라·스폰·HUD 배선이 이미 다 되어 있다.
// 그걸 다시 만들 이유가 없고, 그렇다고 원본 씬의 배경을 지워버리면 기존 필드가 사라진다.
// 그래서 '열어서 → 배경만 지우고 → 다른 이름으로 저장' 한다. 원본은 손대지 않는다.
using System.Collections.Generic;
using Proto;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StreamFieldBuilder
{
    const string SrcScene = "Assets/_Game/Scenes/Proto_Chapter01.unity";
    const string DstScene = "Assets/_Game/Scenes/Proto_Stream_Test.unity";

    const string MatRoot   = "Assets/_Game/Art/Field/Materials";
    const string PropRoot  = "Assets/_Game/Art/Field/Props";
    const string WaterTest = "Assets/_Game/Art/Field/Water/test";

    // 컨테이너 모델 폴더·파일 이름. 원래 힉스필드 생성물(20260915103629_fbedd7e8)이었고, 사용자가 3ds Max 로
    // 박스 기반(≈100 tri)으로 다시 만들면서 2026-09-30 container_01 로 이름을 바꿨다(GUID 유지 — 씬 참조는 그대로).
    const string ContainerId = "container_01";

    // 배경 아트에 해당하는 루트. 이것만 지우고 나머지(플레이어·시스템·조명·포스트FX)는 남긴다.
    static readonly string[] KillRoots =
    {
        "test_water",            // 물 테스트 잔재
        "_ShallowWater_Test",    // 웅덩이는 스트리머가 직접 만든다
    };

    // Field_Chapter01 아래에서 지울 것. _Lighting 과 PlayerSpawn 은 남긴다.
    static readonly string[] KillFieldChildren =
    {
        "_Environment",          // 40×40 지면 5겹
        "_RoadMarkings",
        "_Water",                // 폭포/호수 디오라마 — 이번 테스트에서는 쓰지 않는다
        "_Bounds",               // **경계벽. 이걸 남기면 20m 에서 막혀 스트리밍을 볼 수 없다**
    };

    [MenuItem("Tools/좀비 오버런/배경 스트리밍 테스트 씬 생성")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene(SrcScene, OpenSceneMode.Single);

        int removed = 0;
        var roots = new List<GameObject>(scene.GetRootGameObjects());

        foreach (var go in roots)
        {
            if (go == null) continue;

            // 씬 루트에 떠 있는 힉스필드 원본 덩어리들. 이름이 타임스탬프라 규칙으로 잡는다.
            // (개당 10,808 tri — 스트리밍 측정에 섞이면 숫자를 못 읽는다.)
            // 이 중 하나는 컨테이너로 쓰지만, 그건 스트리머가 에셋에서 직접 인스턴스화한다.
            // 씬에 손으로 놓여 있던 것과는 별개다.
            if (IsTimestampName(go.name)) { Object.DestroyImmediate(go); removed++; continue; }

            if (System.Array.IndexOf(KillRoots, go.name) >= 0) { Object.DestroyImmediate(go); removed++; continue; }

            if (go.name.StartsWith("Field_"))
            {
                foreach (var child in KillFieldChildren)
                {
                    var t = go.transform.Find(child);
                    if (t != null) { Object.DestroyImmediate(t.gameObject); removed++; }
                }
                // _Props 는 남기되 내용물을 비운다 — ProtoObstacles 가 이름으로 찾고,
                // 스트리머가 그 아래에 종류별 그룹을 만들어 쓴다.
                var props = go.transform.Find("_Props");
                if (props != null)
                    for (int i = props.childCount - 1; i >= 0; i--)
                    { Object.DestroyImmediate(props.GetChild(i).gameObject); removed++; }
            }
        }

        // ── 스트리머 ────────────────────────────────────────────────────
        var host = new GameObject("_FieldStreamer");
        var fs = host.AddComponent<ProtoFieldStreamer>();

        // 지면 3톤. 잔디·아스팔트·차선은 세계관이 달라 더 이상 쓰지 않는다(폐차장 레퍼런스).
        fs.mBase     = Mat("M_Ground_Debris");     // 어두운 잔해밭
        fs.mShoulder = Mat("M_Ground_Gravel");     // 중간 톤 자갈 갓길
        fs.mPath     = Mat("M_Ground_DirtPath");   // 밝은 흙길
        fs.mAsh      = Mat("M_Ground_Ash");        // 아직 배치 안 함 — 알파 데칼이 있어야 쓸 만하다
        fs.mConcrete = Mat("M_Ground_Concrete");   // 건물 박스
        fs.mSplat    = EnsureSplatMaterial(fs);    // 위 네 장을 한 면에서 섞는다(비면 적층으로 돌아감)
        fs.buildingPrefabs = BuildingSetup.Configure();   // 비면 콘크리트 박스 건물로 돌아간다
        fs.mFoliage  = EnsureFoliageMaterial(fs);         // 비면 풀 카드를 안 심는다

        fs.mBarricade = Mat("M_Barricade_Wood");
        fs.mBarrier   = Mat("M_Barrier_Concrete");
        fs.mFence     = Mat("M_Fence_Plank");
        fs.mRubble    = Mat("M_Rubble_Concrete");
        fs.mBarrel    = Mat("M_Barrel_Rusty");
        fs.mTree      = Mat("M_Tree_Conifer");
        fs.mVan       = Mat("M_Van_Abandoned");

        fs.treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropRoot}/Tree_Conifer.obj");
        fs.vanPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropRoot}/Van_Abandoned.obj");
        fs.barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropRoot}/Barrel_Rusty.obj");

        // 컨테이너는 힉스필드 생성물 중 하나를 그대로 쓴다(14,616 tri — 나중에 줄인다).
        // FBX 안의 렌더러는 3ds Max 셰이더 머티리얼을 물고 들어오므로, 옆에 굽힌 .mat 을 쓴다.
        fs.containerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropRoot}/{ContainerId}/{ContainerId}.fbx");
        fs.mContainer      = AssetDatabase.LoadAssetAtPath<Material>($"{PropRoot}/{ContainerId}/{ContainerId}.mat");

        // 수심 10cm 웅덩이(Pond_010cm)가 쓰던 디스크 메시와 머티리얼을 그대로 재사용한다.
        fs.pondMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{WaterTest}/ShallowPond_Test.asset");
        fs.mWater   = AssetDatabase.LoadAssetAtPath<Material>($"{WaterTest}/M_Water_Shallow_Test.mat");

        WarnIfMissing(fs);

        // 지면 안개 시트는 40×40 아레나 크기로 깔려 있다. 월드가 커지면 안개가 가운데에만
        // 남아 "여기가 끝"처럼 보이므로, 테스트 씬에서는 끈다. 배경 판단을 흐리지 않기 위함이다.
        var fog = GameObject.Find("_GroundFog");
        if (fog != null) fog.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, DstScene);

        Debug.Log($"[스트리밍 테스트] {DstScene} 저장. 배경 오브젝트 {removed}개 제거.\n" +
                  $"  월드 {fs.worldHalfChunks * fs.chunkSize * 2f:F0}×{fs.worldHalfChunks * fs.chunkSize * 2f:F0}m / 청크 {fs.chunkSize:F0}m\n" +
                  "  Play 후 [ ] 로 미리 불러오는 반경을 바꾸고 HUD 숫자를 볼 것. \\ 로 HUD 토글.");
        Selection.activeGameObject = host;
    }

    const string SplatMatName = "M_Ground_Splat";
    const float  TileBoost    = 3f;   // 적층 시절 주기 대비 배율

    /// 지면 혼합 머티리얼을 만들거나(없을 때) 층 텍스처만 다시 물린다(있을 때).
    ///
    /// <para><b>잔해밭 머티리얼을 복제해서 만든다.</b> 툰 조명 값(램프·림·그림자 색)이 기존 지면과
    /// 같아야 바꾼 뒤에도 톤이 그대로다. 셰이더를 새로 짜지 않고 BCM 에 키워드를 얹은 이유와 같다.</para>
    /// <para>이미 있으면 조명·혼합 슬라이더는 건드리지 않는다 — 인스펙터에서 맞춘 값이 메뉴를 다시
    /// 돌린다고 날아가면 안 된다. 텍스처 연결만 층 머티리얼에서 다시 읽는다.</para>
    public static Material EnsureSplatMaterial(ProtoFieldStreamer fs)
    {
        if (fs.mBase == null) return null;
        string path = $"{MatRoot}/{SplatMatName}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool fresh = m == null;
        if (fresh)
        {
            m = new Material(fs.mBase) { name = SplatMatName };
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetTextureOffset("_BaseMap", Vector2.zero);
            // **층 머티리얼의 Tiling(현재 ×8)까지 나눠야 실제 주기다.** 적층 경로는 월드 UV 에
            // 머티리얼 Tiling 을 한 번 더 곱해서, 스트리머의 tileBase=9 는 실제로 9/8 = 1.1m 다.
            // 스플랫은 _BaseMap_ST 를 안 쓰므로 그걸 여기서 접어 넣지 않으면 알갱이가 8배로 커져
            // 평균색 한 장처럼 보인다(실제로 그렇게 나왔다).
            // 그리고 **적층 때 주기는 너무 잘았다**(사용자 피드백: "텍스처가 작아 칸이 작다").
            // 1m 안팎이면 카메라 17.76m 에서 반복 격자가 눈에 걸린다 → 약 3배로 키운다.
            m.SetVector("_SplatTiles", new Vector4(
                fs.tileBase     / Scale(fs.mBase)     * TileBoost,
                fs.tileAsh      / Scale(fs.mAsh)      * TileBoost,
                fs.tileShoulder / Scale(fs.mShoulder) * TileBoost,
                fs.tilePath     / Scale(fs.mPath)     * TileBoost));
            m.SetFloat("_SplatTile4", 3.5f);
            // 원래 풀 머티리얼의 형광 녹색 틴트(0.58/0.82/0.47)는 폐차장 톤에서 튄다.
            // 마른 풀 텍스처 그대로에 채도만 살짝 누른 올리브로 둔다.
            m.SetColor("_SplatTint4", new Color(0.80f, 0.86f, 0.62f));
            AssetDatabase.CreateAsset(m, path);
        }

        m.SetTexture("_BaseMap",   Tex(fs.mBase));
        m.SetTexture("_SplatTex1", Tex(fs.mAsh));
        m.SetTexture("_SplatTex2", Tex(fs.mShoulder));
        m.SetTexture("_SplatTex3", Tex(fs.mPath));
        m.SetTexture("_SplatTex4", Tex(Mat("M_Ground_Grass")));   // 풀 — 옛 40×40 필드 잔디 텍스처
        m.SetFloat("_UseGroundSplat", 1f);
        m.EnableKeyword("BCM_GROUND_SPLAT");
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static Texture Tex(Material m) { return m != null ? m.GetTexture("_BaseMap") : null; }
    static float Scale(Material m) { return m != null ? Mathf.Max(0.001f, m.GetTextureScale("_BaseMap").x) : 1f; }

    const string FoliageTex = "Assets/_Game/Art/Field/Foliage/T_Foliage_Atlas.png";
    const string FoliageMat = "Assets/_Game/Art/Field/Foliage/M_Foliage.mat";

    /// 풀 카드 머티리얼. 나무 머티리얼(툰 톤 + 바람)을 복제해 알파 컷아웃만 얹는다.
    /// 이미 있으면 텍스처 연결과 필수 스위치만 다시 켠다 — 바람·색 슬라이더는 인스펙터 값을 존중한다.
    public static Material EnsureFoliageMaterial(ProtoFieldStreamer fs)
    {
        // 텍스처: **밉맵 커버리지 보존**이 핵심. 안 켜면 밉이 내려갈수록 알파 평균이 컷오프 아래로 떨어져
        // 가는 풀잎이 멀리서 통째로 사라진다(알파 컷아웃 식물의 고전적인 함정).
        var ti = AssetImporter.GetAtPath(FoliageTex) as TextureImporter;
        if (ti == null) return null;
        if (!ti.mipMapsPreserveCoverage || !ti.alphaIsTransparency || ti.maxTextureSize != 1024 || ti.wrapMode != TextureWrapMode.Clamp)
        {
            ti.alphaIsTransparency = true;
            ti.mipMapsPreserveCoverage = true;
            ti.alphaTestReferenceValue = 0.45f;
            ti.maxTextureSize = 1024;
            ti.wrapMode = TextureWrapMode.Clamp;   // 아틀라스 칸 가장자리에서 반대편 칸이 새지 않게
            ti.SaveAndReimport();
        }

        var m = AssetDatabase.LoadAssetAtPath<Material>(FoliageMat);
        if (m == null)
        {
            m = fs.mTree != null ? new Material(fs.mTree) : new Material(Shader.Find("BCM/BCM_Toony_shader"));
            m.name = "M_Foliage";
            // 풀은 나무보다 가볍고 빠르게 흔들린다. 높이 1m 에서 최대(카드 최고 ≈1.3m).
            m.SetFloat("_WindStrength", 0.07f);
            m.SetFloat("_WindSpeed", 1.5f);
            m.SetFloat("_WindHeight", 1.0f);
            m.SetFloat("_WindBaseY", 0f);
            m.SetFloat("_WindTurbulence", 0.5f);
            m.SetFloat("_WindPhaseScale", 0.8f);
            // 아틀라스의 마른 풀이 바닥 풀밭(올리브 틴트)보다 샛노래서 스티커처럼 떴다 — 같은 올리브 쪽으로 누른다.
            m.SetColor("_BaseColor", new Color(0.80f, 0.90f, 0.70f));
            AssetDatabase.CreateAsset(m, FoliageMat);
        }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FoliageTex));
        m.SetTextureScale("_BaseMap", Vector2.one);
        m.SetTextureOffset("_BaseMap", Vector2.zero);
        m.SetFloat("_UseAlphaTest", 1f); m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cutoff", 0.45f);
        m.SetFloat("_Cull", 2f);            // 앞면만 — 카드가 전부 카메라를 향하게 만든다(ProtoFieldStreamer.Foliage)
        m.SetFloat("_UseOutline", 0f);      // 잎 가장자리마다 검은 선이 생기면 지저분하다
        m.SetFloat("_CastShadows", 0f);
        m.SetFloat("_UseWindSway", 1f); m.EnableKeyword("BCM_WIND_SWAY");
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    [MenuItem("Tools/좀비 오버런/풀 카드 머티리얼 연결")]
    public static void WireFoliage()
    {
        var fs = Object.FindFirstObjectByType<ProtoFieldStreamer>();
        if (fs == null) { Debug.LogWarning("[풀] 열린 씬에 ProtoFieldStreamer 가 없다."); return; }
        Undo.RecordObject(fs, "풀 카드 연결");
        fs.mFoliage = EnsureFoliageMaterial(fs);
        EditorUtility.SetDirty(fs);
        EditorSceneManager.MarkSceneDirty(fs.gameObject.scene);
        Debug.Log($"[풀] {fs.name} ← {(fs.mFoliage != null ? fs.mFoliage.name : "실패 (아틀라스 없음)")}");
    }

    /// 씬을 다시 만들지 않고 지금 열린 씬의 스트리머에만 혼합 머티리얼을 물린다.
    [MenuItem("Tools/좀비 오버런/지면 혼합(스플랫) 머티리얼 연결")]
    public static void WireSplat()
    {
        var fs = Object.FindFirstObjectByType<ProtoFieldStreamer>();
        if (fs == null) { Debug.LogWarning("[지면 혼합] 열린 씬에 ProtoFieldStreamer 가 없다."); return; }
        Undo.RecordObject(fs, "지면 혼합 연결");
        fs.mSplat = EnsureSplatMaterial(fs);
        EditorUtility.SetDirty(fs);
        EditorSceneManager.MarkSceneDirty(fs.gameObject.scene);
        Debug.Log($"[지면 혼합] {fs.name} ← {(fs.mSplat != null ? fs.mSplat.name : "실패 (mBase 없음)")}");
    }

    static Material Mat(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Material>($"{MatRoot}/{name}.mat");
    }

    /// 20260915143046_4115962e 처럼 앞 14자가 숫자인 이름 = 힉스필드 생성물.
    static bool IsTimestampName(string n)
    {
        if (n.Length < 15 || n[14] != '_') return false;
        for (int i = 0; i < 14; i++) if (n[i] < '0' || n[i] > '9') return false;
        return true;
    }

    /// 배선이 빠진 채로 Play 를 누르면 "지면이 안 보인다"만 남고 원인을 알 수 없다.
    /// 어떤 칸이 비었는지 이름으로 찍어준다.
    static void WarnIfMissing(ProtoFieldStreamer fs)
    {
        var missing = new List<string>();
        if (fs.mBase == null)          missing.Add("M_Ground_Debris");
        if (fs.mShoulder == null)      missing.Add("M_Ground_Gravel");
        if (fs.mPath == null)          missing.Add("M_Ground_DirtPath");
        if (fs.mConcrete == null)      missing.Add("M_Ground_Concrete");
        if (fs.treePrefab == null)     missing.Add("Tree_Conifer.obj");
        if (fs.vanPrefab == null)      missing.Add("Van_Abandoned.obj");
        if (fs.containerPrefab == null) missing.Add(ContainerId + ".fbx (컨테이너)");
        if (fs.pondMesh == null)   missing.Add("ShallowPond_Test.asset (얕은 물 테스트를 한 번 실행하면 생긴다)");
        if (fs.mWater == null)     missing.Add("M_Water_Shallow_Test.mat");
        if (missing.Count > 0)
            Debug.LogWarning("[스트리밍 테스트] 배선 못 한 에셋: " + string.Join(", ", missing));
    }
}
