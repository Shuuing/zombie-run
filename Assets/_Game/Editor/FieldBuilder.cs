// 좀비 오버런 (TANGTANG) — 필드 빌더
// GDD §10 기준: 40×40m 사각 아레나, 챕터별로 장애물 배치와 조명만 교체.
// 결정론적(시드 고정) 배치이므로 몇 번 실행해도 같은 필드가 나온다.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FieldBuilder
{
    const string ArtRoot   = "Assets/_Game/Art/Field";
    const string MatRoot   = "Assets/_Game/Art/Field/Materials";
    const string ScenePath = "Assets/_Game/Scenes/Field_Chapter01.unity";
    const string ToonShader = "BCM/BCM_Toony_shader";

    // 물 디오라마 — 프리팹은 'Tools ▸ 좀비 오버런 ▸ 물 디오라마 셋업' 이 굽는다 (WaterSetup.cs).
    // 필드 씬 안에 넣어야 프로토 씬을 다시 생성해도 살아남는다. 예전엔 프로토 씬에만 손으로 놓여 있어서
    // '프로토타입 씬 생성' 을 돌리면 물이 통째로 사라졌다.
    const string WaterPrefab = "Assets/_Game/Art/Field/Water/WaterfallLake_Diorama.prefab";
    // 2026-09-03: 사용자가 씬에서 -17.92 로 옮긴 것을 기준값으로 확정.
    // 이 위치면 암반이 잔디 서쪽 끝(x=-20)을 4.57m 넘어간다 — 의도된 배치다.
    // 기준값을 안 바꾸면 '프로토타입 씬 생성' 이 필드를 다시 읽으면서 -13 으로 되돌린다.
    static readonly Vector3 WaterPos = new Vector3(-17.92f, 0f, 0f);
    const float WaterYaw   = 180f;   // 절벽이 카메라 반대편(먼 쪽)에 오게 한다. FBX 축 변환 때문에 로컬 -Z 가 절벽
    const float WaterClear = 1.6f;   // 물 바깥으로 프롭을 금지할 여유. 프롭 자체 크기를 감안한 값

    const float Arena     = 40f;   // 플레이 가능 영역 한 변
    const float RoadHalf  = 3.6f;  // 도로 반폭 (2차선 7.2m — 세로 화면에서 초목이 보이도록 좁힘)
    const float CurbW     = 0.7f;
    const float DirtHalf  = 7.2f;  // 흙 노면 반폭. 이 밖은 잔디
    const float ClearR    = 4.0f;  // 중앙 스폰 주변 프롭 금지 반경

    /// 프롭이 아스팔트 위에 놓일 수 있는지. 나무·울타리는 노면 밖에만.
    enum Zone { Any, OffRoad }

    // 프롭별 실제 스케일 (생성 메시가 ~1.9 유닛으로 정규화되어 있어 보정 필요)
    struct Prop
    {
        public string name; public float scale; public int count;
        public float minR, maxR;   // 아레나 중심으로부터의 배치 반경대
        public float minGap;       // 프롭 간 최소 간격
        public Zone zone;
        // 바람 흔들림 진폭(m). 0 = 정지물. 밑동은 고정되고 꼭대기가 이만큼 좌우로 흔들린다.
        // 흔들림이 1.0 으로 포화하는 높이는 메시에서 자동으로 재므로(GetPropHeight) 여기 안 쓴다.
        public float wind;
        public Prop(string n, float s, int c, float a, float b, float g, Zone z, float w = 0f)
        { name = n; scale = s; count = c; minR = a; maxR = b; minGap = g; zone = z; wind = w; }
    }

    // 새 프롭은 이 표에 한 줄 추가하면 끝이다. 나무·풀처럼 흔들려야 하는 것은
    // 맨 뒤 wind 값만 주면 되고(권장 0.10~0.15), 나머지는 생략하면 정지물이 된다.
    static readonly Prop[] Props =
    {
        //                              scale  개수  최소R  최대R  간격  존             바람
        new Prop("Van_Abandoned",       2.634f,  6,  3.5f, 15f, 7.0f, Zone.Any),
        new Prop("Barricade_Wood",      1.264f, 10,  5.0f, 17f, 4.5f, Zone.Any),
        new Prop("Fence_Plank",         1.266f, 12,  5.0f, 19f, 4.0f, Zone.OffRoad),
        new Prop("Barrier_Concrete",    1.054f,  8,  4.0f, 14f, 5.0f, Zone.Any),
        new Prop("Tree_Conifer",        2.371f, 22,  5.2f, 19f, 3.2f, Zone.OffRoad, 0.12f),
        new Prop("Rubble_Concrete",     1.330f, 14,  4.0f, 18f, 3.5f, Zone.Any),
        new Prop("Barrel_Rusty",        0.474f, 12,  4.0f, 16f, 2.5f, Zone.Any),
    };

    [MenuItem("Tools/좀비 오버런/필드 생성 (챕터 01)")]
    public static void Build()
    {
        EnsureFolders();

        // ---- 씬 초기화 -------------------------------------------------
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var root      = new GameObject("Field_Chapter01");
        var envRoot    = MakeChild(root, "_Environment");
        var decalRoot  = MakeChild(root, "_RoadMarkings");
        var waterRoot  = MakeChild(root, "_Water");
        var propRoot   = MakeChild(root, "_Props");
        var boundsRoot = MakeChild(root, "_Bounds");
        var lightRoot  = MakeChild(root, "_Lighting");

        // ---- 머티리얼 ---------------------------------------------------
        // 생성된 잔디 텍스처가 마른 갈색 위주라, 틴트로 올리브그린 쪽으로 밀어 도로·흙과 대비를 만든다
        var mGrass    = GroundMat("M_Ground_Grass",    "Ground_Grass",    10f, new Color(0.58f, 0.82f, 0.47f));
        var mDirt     = GroundMat("M_Ground_Dirt",     "Ground_Dirt",      8f, new Color(1.00f, 0.92f, 0.80f));
        var mAsphalt  = GroundMat("M_Ground_Asphalt",  "Ground_Asphalt",   6f);
        var mConcrete = GroundMat("M_Ground_Concrete", "Ground_Concrete",  6f);
        var mDash     = DecalMat("M_RoadLine_Dashed", "RoadLine_Dashed");
        var mSolid    = DecalMat("M_RoadLine_Solid",  "RoadLine_Solid");

        // ---- 지면: 바깥에서 안으로 레이어링 -----------------------------
        Ground(envRoot, "Ground_Grass",    Vector3.zero,                    new Vector2(Arena, Arena),            mGrass,    0.00f);
        Ground(envRoot, "Ground_Dirt",     Vector3.zero,                    new Vector2(DirtHalf * 2f, Arena),    mDirt,     0.01f);
        Ground(envRoot, "Curb_Left",       new Vector3(-RoadHalf - CurbW*0.5f, 0, 0), new Vector2(CurbW, Arena),   mConcrete, 0.02f);
        Ground(envRoot, "Curb_Right",      new Vector3( RoadHalf + CurbW*0.5f, 0, 0), new Vector2(CurbW, Arena),   mConcrete, 0.02f);
        Ground(envRoot, "Road_Asphalt",    Vector3.zero,                    new Vector2(RoadHalf * 2f, Arena),     mAsphalt,  0.03f);

        // ---- 도로 표시 ---------------------------------------------------
        Ground(decalRoot, "Line_Center", Vector3.zero,                       new Vector2(0.55f, Arena), mDash,  0.05f, new Vector2(1f, 10f));
        Ground(decalRoot, "Line_EdgeL",  new Vector3(-RoadHalf + 0.35f, 0, 0), new Vector2(0.28f, Arena), mSolid, 0.05f, new Vector2(1f, 20f));
        Ground(decalRoot, "Line_EdgeR",  new Vector3( RoadHalf - 0.35f, 0, 0), new Vector2(0.28f, Arena), mSolid, 0.05f, new Vector2(1f, 20f));

        // ---- 물 (호수/폭포) ---------------------------------------------
        // 프롭보다 먼저 놓는다. 실제로 차지한 영역을 재서 그 위에 프롭이 뿌려지지 않게 해야 한다.
        Rect noProp = PlaceWater(waterRoot);

        // ---- 프롭 배치 (결정론) -----------------------------------------
        var rng    = new System.Random(20260901);
        var placed = new List<Vector3>();
        int total  = 0, skipped = 0;

        foreach (var p in Props)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtRoot}/Props/{p.name}.obj");
            if (src == null) { Debug.LogWarning($"[FieldBuilder] 모델 없음: {p.name}.obj"); continue; }

            var mat   = PropMat(p);
            var group = MakeChild(propRoot, p.name);

            for (int i = 0; i < p.count; i++)
            {
                Vector3 pos = Vector3.zero;
                bool ok = false;
                for (int attempt = 0; attempt < 120 && !ok; attempt++)
                {
                    float ang = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                    float rad = Mathf.Lerp(p.minR, p.maxR, (float)rng.NextDouble());
                    pos = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);

                    if (pos.magnitude < ClearR) continue;
                    if (Mathf.Abs(pos.x) > Arena * 0.5f - 1.5f) continue;
                    if (Mathf.Abs(pos.z) > Arena * 0.5f - 1.5f) continue;
                    // 나무·울타리가 아스팔트 위에 서지 않게 한다
                    if (p.zone == Zone.OffRoad && Mathf.Abs(pos.x) < RoadHalf + CurbW + 0.6f) continue;
                    // 물 위에 프롭이 서지 않게 한다 (예전엔 밴·나무가 호수 한가운데 떠 있었다)
                    if (noProp.width > 0f && noProp.Contains(new Vector2(pos.x, pos.z))) continue;

                    ok = true;
                    foreach (var q in placed)
                        if ((q - pos).sqrMagnitude < p.minGap * p.minGap) { ok = false; break; }
                }
                if (!ok) { skipped++; continue; }
                placed.Add(pos);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(src, group.transform);
                go.name = $"{p.name}_{i:00}";
                float jitter = 0.9f + (float)rng.NextDouble() * 0.25f;
                go.transform.localScale = Vector3.one * p.scale * jitter;
                go.transform.rotation   = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                ApplyMaterial(go, mat);
                GroundTo(go, pos);
                go.isStatic = true;
                total++;
            }
        }

        // ---- 보이지 않는 경계벽 ------------------------------------------
        float h = 6f, t = 1f, half = Arena * 0.5f;
        Wall(boundsRoot, "Wall_N", new Vector3(0,  h*0.5f,  half), new Vector3(Arena, h, t));
        Wall(boundsRoot, "Wall_S", new Vector3(0,  h*0.5f, -half), new Vector3(Arena, h, t));
        Wall(boundsRoot, "Wall_E", new Vector3( half, h*0.5f, 0),  new Vector3(t, h, Arena));
        Wall(boundsRoot, "Wall_W", new Vector3(-half, h*0.5f, 0),  new Vector3(t, h, Arena));

        // ---- 조명 · 안개 · 색감 (§15 아트 규칙) --------------------------
        // 앰비언트/안개/그레이딩 수치는 SceneLookSetup.cs 한 곳에만 둔다.
        // 여기서 다시 쓰면 두 곳이 갈라지므로 인라인하지 말 것.
        // 태양 색·세기도 SceneLookSetup 이 덮어쓴다 (아래 값은 초기값).
        var sunGO = new GameObject("Sun");
        sunGO.transform.SetParent(lightRoot.transform);
        sunGO.transform.rotation = Quaternion.Euler(52f, -34f, 0f);
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1.00f, 0.97f, 0.90f);
        sun.intensity = 1.15f;
        sun.shadows = LightShadows.Soft;

        // ---- 카메라 (GDD §03: 피치 58°, 거리 15.5m, FOV 42) -------------
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(root.transform);
        const float pitch = 58f, dist = 22.2f;   // §03 기본값 15.5m → 가시 영역이 좁아 22.2m로 조정
        float rad2 = pitch * Mathf.Deg2Rad;
        camGO.transform.position = new Vector3(0f, Mathf.Sin(rad2) * dist, -Mathf.Cos(rad2) * dist);
        camGO.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        var cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 42f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane  = 90f;

        // ---- 색감 (Volume + 카메라 포스트 프로세싱 + 앰비언트/안개) ------
        // 카메라를 만든 **뒤에** 불러야 한다 — 이 함수가 씬의 게임 카메라를 찾아
        // UniversalAdditionalCameraData 를 붙이고 포스트 프로세싱을 켜기 때문.
        SceneLookSetup.Apply(lightRoot);

        // ---- 스폰 기준점 -------------------------------------------------
        var spawn = MakeChild(root, "PlayerSpawn");
        spawn.transform.position = Vector3.zero;

        // ---- 저장 --------------------------------------------------------
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FieldBuilder] 완료 — 프롭 {total}개 배치, {skipped}개 공간부족으로 생략. 씬: {ScenePath}");
    }

    /// 물 디오라마를 배치하고, 프롭을 금지할 월드 XZ 영역을 돌려준다.
    /// 프리팹이 없으면 빈 Rect (프롭 금지 없음).
    static Rect PlaceWater(GameObject parent)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPrefab);
        if (src == null)
        {
            Debug.LogWarning($"[FieldBuilder] 물 프리팹 없음: {WaterPrefab}  'Tools ▸ 좀비 오버런 ▸ 물 디오라마 셋업' 을 먼저 실행할 것");
            return new Rect();
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent.transform);
        go.name = "WaterfallLake";
        go.transform.position = WaterPos;
        go.transform.rotation = Quaternion.Euler(0f, WaterYaw, 0f);

        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Rect();
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

        var rect = Rect.MinMaxRect(b.min.x - WaterClear, b.min.z - WaterClear,
                                   b.max.x + WaterClear, b.max.z + WaterClear);
        Debug.Log($"[FieldBuilder] 물 배치 {WaterPos} / Y{WaterYaw}° — 프롭 금지 영역 x[{rect.xMin:F1}, {rect.xMax:F1}] z[{rect.yMin:F1}, {rect.yMax:F1}]");
        return rect;
    }

    // ================= helpers =================

    static void EnsureFolders()
    {
        foreach (var p in new[] { "Assets/_Game", "Assets/_Game/Scenes", "Assets/_Game/Art",
                                  "Assets/_Game/Art/Field", MatRoot })
        {
            if (!AssetDatabase.IsValidFolder(p))
            {
                int i = p.LastIndexOf('/');
                AssetDatabase.CreateFolder(p.Substring(0, i), p.Substring(i + 1));
            }
        }
    }

    static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static Shader Toon()
    {
        var s = Shader.Find(ToonShader);
        if (s == null) { s = Shader.Find("Universal Render Pipeline/Lit"); Debug.LogWarning("[FieldBuilder] 툰 셰이더를 찾지 못해 URP/Lit로 대체"); }
        return s;
    }

    static Material MakeMat(string assetName, Texture tex, bool alphaClip, Color? tint = null)
    {
        string path = $"{MatRoot}/{assetName}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Toon()); AssetDatabase.CreateAsset(mat, path); }
        else mat.shader = Toon();

        if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint ?? Color.white);
        if (alphaClip)
        {
            if (mat.HasProperty("_UseAlphaTest")) mat.SetFloat("_UseAlphaTest", 1f);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.35f);
            mat.EnableKeyword("_ALPHATEST_ON");
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material GroundMat(string matName, string texName, float tile, Color? tint = null)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot}/Textures/Ground/{texName}.jpg");
        if (tex == null) Debug.LogWarning($"[FieldBuilder] 텍스처 없음: {texName}.jpg");
        var m = MakeMat(matName, tex, false, tint);
        if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", new Vector2(tile, tile));
        return m;
    }

    static Material DecalMat(string matName, string texName)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot}/Textures/Decals/{texName}.png");
        if (tex == null) Debug.LogWarning($"[FieldBuilder] 데칼 텍스처 없음: {texName}.png");
        return MakeMat(matName, tex, true);
    }

    static Material PropMat(Prop p)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot}/Props/{p.name}_BaseMap.jpg");
        var mat = MakeMat($"M_{p.name}", tex, false);
        ApplyWind(mat, p);
        return mat;
    }

    /// 프롭의 배치 후 실제 월드 높이(m). 메시 바운즈 × 배치 스케일.
    /// 손으로 적은 상수를 안 쓰는 이유: 관목(0.8m)에 나무 높이(4.5m)를 그대로 주면
    /// 마스크가 0.8/4.5 의 제곱 = 0.03 이 되어 **거의 안 움직이는데 에러도 안 난다.**
    /// 조용히 틀리는 종류의 값이라 메시에서 직접 재는 게 맞다.
    static float GetPropHeight(Prop p)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtRoot}/Props/{p.name}.obj");
        if (src == null) return 0f;
        float h = 0f;
        foreach (var mf in src.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null) h = Mathf.Max(h, mf.sharedMesh.bounds.size.y);
        return h * p.scale;
    }

    /// 정점 바람 흔들림을 프롭 정의대로 켜거나 끈다 (BCM_WIND_SWAY — 뿌리 고정, 위로 갈수록 흔들림).
    ///
    /// **키워드와 값을 같이 세팅해야 한다** — 값만 넣으면 셰이더가 변형 코드를 컴파일 아웃해
    /// 조용히 안 움직인다. 물 셰이더 토글에서 이미 한 번 겪은 함정과 같은 종류다.
    /// 끌 때도 키워드를 명시적으로 꺼야 한다 — 진폭만 0 으로 두면 머티리얼에 키워드가 남아
    /// 정지물인데도 정점 변형 변종을 계속 컴파일한다.
    static void ApplyWind(Material mat, Prop p)
    {
        if (!mat.HasProperty("_UseWindSway"))
        {
            if (p.wind > 0f)
                Debug.LogWarning($"[FieldBuilder] 셰이더에 Wind Sway 가 없다 ({p.name}) — BCM_Toony_shader 확인 필요");
            return;
        }

        if (p.wind <= 0f)
        {
            mat.SetFloat("_UseWindSway", 0f);
            mat.DisableKeyword("BCM_WIND_SWAY");
            EditorUtility.SetDirty(mat);
            return;
        }

        float height = GetPropHeight(p);
        if (height <= 0.01f)
        {
            Debug.LogWarning($"[FieldBuilder] {p.name} 의 높이를 못 쟀다 — 바람을 건너뛴다 (.obj 확인)");
            return;
        }

        mat.SetFloat("_UseWindSway", 1f);
        mat.EnableKeyword("BCM_WIND_SWAY");
        mat.SetFloat("_WindHeight", height);
        mat.SetFloat("_WindBaseY", 0f);        // 아레나 지면이 y=0 이라 밑동이 여기서 고정된다
        mat.SetFloat("_WindStrength", p.wind);
        mat.SetFloat("_WindSpeed", 1.0f);
        mat.SetFloat("_WindTurbulence", 0.35f);
        mat.SetFloat("_WindPhaseScale", 0.35f);
        mat.SetVector("_WindDir", new Vector4(1f, 0f, 0.35f, 0f));
        EditorUtility.SetDirty(mat);
        Debug.Log($"[FieldBuilder] {p.name} 바람 ON — 높이 {height:F2}m, 진폭 {p.wind:F2}m");
    }

    /// 평면 쿼드를 XZ 평면에 깐다. tileOverride가 있으면 UV 반복을 개별 지정.
    static void Ground(GameObject parent, string name, Vector3 pos, Vector2 size,
                       Material mat, float yOffset, Vector2? tileOverride = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.position   = pos + new Vector3(0f, yOffset, 0f);
        go.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        Object.DestroyImmediate(go.GetComponent<Collider>());

        var r = go.GetComponent<MeshRenderer>();
        if (tileOverride.HasValue)
        {
            // 개별 타일링이 필요한 데칼은 머티리얼 인스턴스를 따로 만든다
            var inst = new Material(mat);
            inst.name = mat.name + "_" + name;
            AssetDatabase.CreateAsset(inst, $"{MatRoot}/{inst.name}.mat");
            if (inst.HasProperty("_BaseMap")) inst.SetTextureScale("_BaseMap", tileOverride.Value);
            r.sharedMaterial = inst;
        }
        else r.sharedMaterial = mat;

        go.isStatic = true;
    }

    static void Wall(GameObject parent, string name, Vector3 pos, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.position = pos;
        var bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        go.isStatic = true;
    }

    static void ApplyMaterial(GameObject go, Material mat)
    {
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            var arr = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
            for (int i = 0; i < arr.Length; i++) arr[i] = mat;
            r.sharedMaterials = arr;
        }
    }

    /// 프롭의 렌더러 바운즈 최하단을 y=0에 정확히 앉힌다.
    static void GroundTo(GameObject go, Vector3 xz)
    {
        go.transform.position = new Vector3(xz.x, 0f, xz.z);
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        go.transform.position += new Vector3(0f, -b.min.y, 0f);
    }
}
