using Proto;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// FX 쇼케이스 씬을 굽는다 — 바닥 한 장 · 게임 카메라(ProtoCameraFollow, 같은 각도) · 밤 어둠(ProtoNightOverlay) ·
/// 인게임 색감(SceneLookSetup 의 Volume 프로파일 + 앰비언트·안개) · 턴테이블 위 더미 좀비 · FxShowcase 구동기.
/// 필드 씬을 쓰지 않는 이유: 켜는 데 오래 걸리고 스트리밍·프롭이 효과와 무관하게 끼어든다. 효과는 "같은 카메라·같은 어둠" 이면 충분히 판단된다.
/// </summary>
public static class FxShowcaseSceneBuilder
{
    public const string ScenePath = "Assets/_Game/Scenes/FX_Showcase.unity";
    const string DummyPrefab   = "Assets/_Game/Art/Enemies/Zombie/Zombie_Walker.prefab";
    const string NightMaterial = "Assets/_Game/Art/City/Materials/M_City_NightOverlay.mat";
    const string GroundMatName = "M_Ground_Asphalt";
    const string LookProfile   = "Assets/_Game/Art/PostFX/Field_Look.asset";   // SceneLookSetup.ProfilePath 와 같다

    [MenuItem("Tools/좀비 오버런/FX 툴/쇼케이스 씬 만들기")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ── 바닥 ─────────────────────────────────────────────────────
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(6f, 1f, 6f);   // 60m — 카메라가 보는 땅(세로 ~20m)보다 넉넉히
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        var groundMat = FindMaterial(GroundMatName);
        if (groundMat != null) ground.GetComponent<Renderer>().sharedMaterial = groundMat;
        else Debug.LogWarning($"[FX 쇼케이스] 바닥 머티리얼 {GroundMatName} 을 못 찾았다 — 기본 재질로 둔다 ('필드 생성' 을 돌리면 생긴다)");

        // ── 무대 ─────────────────────────────────────────────────────
        var root = new GameObject("_FxShowcase");
        var show = root.AddComponent<FxShowcase>();
        var stage = new GameObject("Stage").transform;
        stage.SetParent(root.transform, false);
        var table = new GameObject("Turntable").AddComponent<ProtoTurntable>();
        table.transform.SetParent(stage, false);
        table.degreesPerSecond = 20f;
        table.spinning = true;

        var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefab);
        GameObject dummy = null;
        if (dummyPrefab != null)
        {
            dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab);
            dummy.name = "Dummy_Zombie";
            dummy.transform.SetParent(table.transform, false);
            dummy.transform.localPosition = Vector3.zero;
            dummy.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 카메라 쪽을 본다
        }
        else Debug.LogWarning($"[FX 쇼케이스] 더미 좀비 프리팹이 없다: {DummyPrefab} — 더미 없이 무대 중심에 효과를 낸다");

        show.stage = stage;
        show.turntable = table;
        show.dummy = dummy;

        // ── 카메라 — 게임과 같은 추적 컴포넌트, 타깃만 무대 ────────────
        var camGO = GameObject.FindWithTag("MainCamera");
        if (camGO == null) { camGO = new GameObject("Main Camera") { tag = "MainCamera" }; camGO.AddComponent<Camera>(); }
        var cam = camGO.GetComponent<Camera>();
        cam.fieldOfView = 42f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 90f;
        var follow = camGO.AddComponent<ProtoCameraFollow>();
        follow.target = stage;

        var nightMat = AssetDatabase.LoadAssetAtPath<Material>(NightMaterial);
        if (nightMat != null)
        {
            var night = camGO.AddComponent<ProtoNightOverlay>();
            night.overlayMaterial = nightMat;
            night.target = stage;
            show.night = night;
        }
        else Debug.LogWarning($"[FX 쇼케이스] 밤 어둠 머티리얼이 없다: {NightMaterial} — 밤 없이 굽는다 ('도시 생성' 을 돌리면 생긴다)");

        // ── 색감 — 필드와 같은 Volume 프로파일 · 앰비언트 · 안개 · 태양 ──
        // 프로파일은 있는 것을 그대로 쓴다. BuildProfile 은 상수로 다시 구워 손으로 만진 슬라이더를 덮어쓴다 — 없을 때만.
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookProfile);
        if (profile == null) profile = SceneLookSetup.BuildProfile();
        var volGO = new GameObject("_PostFX");
        var vol = volGO.AddComponent<Volume>();
        vol.isGlobal = true; vol.priority = 0f; vol.weight = 1f; vol.sharedProfile = profile;
        var acd = cam.GetComponent<UniversalAdditionalCameraData>();
        if (acd == null) acd = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        acd.renderPostProcessing = true;
        acd.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        SceneLookSetup.ApplyEnvironment();
        cam.backgroundColor = RenderSettings.fogColor;
        cam.clearFlags = CameraClearFlags.SolidColor;

        var sun = Object.FindObjectOfType<Light>();
        if (sun != null) sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 저장 ─────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Selection.activeObject = root;
        Debug.Log($"[FX 쇼케이스] 저장 — {ScenePath}. Play 를 누르면 보스 내려찍기부터 반복 재생한다. 더미 {(dummy != null ? "있음" : "없음")} · 밤 {(nightMat != null ? "있음" : "없음")}");
    }

    /// 있으면 열고, 없으면 굽는다. FX 툴 창의 [▶ 무대] 버튼은 아래 OpenAndPlay 를 부른다.
    [MenuItem("Tools/좀비 오버런/FX 툴/쇼케이스 씬 열기")]
    public static void OpenOrBuild()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) { Build(); return; }
        if (IsShowcaseOpen()) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/쇼케이스 열고 플레이")]
    public static void OpenAndPlay()
    {
        if (EditorApplication.isPlaying) return;
        OpenOrBuild();
        if (IsShowcaseOpen()) EditorApplication.isPlaying = true;
    }

    static bool IsShowcaseOpen()
    {
        var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        return s.IsValid() && s.path == ScenePath;
    }

    static Material FindMaterial(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Material"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        return null;
    }
}
