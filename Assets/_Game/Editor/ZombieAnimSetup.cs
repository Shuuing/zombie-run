// 좀비 오버런 (TANGTANG) — 좀비 애니메이션 셋업
// Mixamo FBX를 Humanoid로 리임포트하고, 클립 이름·루프를 정리한 뒤
// 블렌드 트리 기반 AnimatorController와 프리팹을 만든다. 재실행 가능.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ZombieAnimSetup
{
    const string AnimDir  = "Assets/_Game/Art/monster/zombie1/animation";
    const string ModelFbx = AnimDir + "/zombie_basic01.fbx";   // 리그가 있는 쪽 (루트 fbx는 본이 없음)
    const string OutDir   = "Assets/_Game/Art/Enemies/Zombie";
    const string AcPath   = OutDir + "/AC_Zombie.controller";
    const string PrefabPath = OutDir + "/Zombie_Walker.prefab";

    // GDD §08 기준 이동속도: 워커 1.6 / 러너 3.4 m/s
    const float SpeedWalk = 1.6f;
    const float SpeedRun  = 3.4f;

    struct Clip
    {
        public string file;   // fbx 파일명 (확장자 제외)
        public string name;   // 정리된 클립 이름
        public bool loop;
        public Clip(string f, string n, bool l) { file = f; name = n; loop = l; }
    }

    static readonly Clip[] Clips =
    {
        new Clip("Zombie Idle",     "Zombie_Idle",    true),
        new Clip("Zombie Walk",     "Zombie_Walk",    true),
        new Clip("Zombie Running",  "Zombie_Run",     true),
        new Clip("Zombie Attack",   "Zombie_Attack",  false),
        new Clip("Zombie Biting",   "Zombie_Bite",    false),
        new Clip("Zombie Stand Up", "Zombie_StandUp", false),
        new Clip("Zombie Dying",    "Zombie_Die",     false),
    };

    [MenuItem("Tools/좀비 오버런/좀비 애니메이션 셋업")]
    public static void Setup()
    {
        EnsureFolder(OutDir);

        // ---- 1. 모델을 Humanoid로 리임포트해 아바타 생성 -----------------
        var modelImp = AssetImporter.GetAtPath(ModelFbx) as ModelImporter;
        if (modelImp == null) { Debug.LogError($"[ZombieAnim] 모델 임포터를 찾지 못함: {ModelFbx}"); return; }

        modelImp.animationType   = ModelImporterAnimationType.Human;
        modelImp.avatarSetup     = ModelImporterAvatarSetup.CreateFromThisModel;
        modelImp.importAnimation = false;   // 모델 쪽 더미 클립은 필요 없다
        EditorUtility.SetDirty(modelImp);
        modelImp.SaveAndReimport();

        var avatar = LoadAvatar(ModelFbx);
        if (avatar == null) { Debug.LogError("[ZombieAnim] 아바타 생성 실패 — 본 매핑을 확인할 것"); return; }
        if (!avatar.isValid || !avatar.isHuman)
            Debug.LogWarning($"[ZombieAnim] 아바타 상태 주의: valid={avatar.isValid} human={avatar.isHuman}");

        // ---- 2. 애니메이션 FBX를 Humanoid + 아바타 복사로 리임포트 --------
        var loaded = new Dictionary<string, AnimationClip>();
        foreach (var c in Clips)
        {
            string path = $"{AnimDir}/{c.file}.fbx";
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogWarning($"[ZombieAnim] 없음: {path}"); continue; }

            imp.animationType   = ModelImporterAnimationType.Human;
            imp.avatarSetup     = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar    = avatar;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;  // 애니메이션 전용
            imp.resampleCurves  = true;

            // 기본 클립 정의를 가져와 이름·루프만 바꾼다 (프레임 구간 보존)
            var defs = imp.defaultClipAnimations;
            if (defs != null && defs.Length > 0)
            {
                var arr = new ModelImporterClipAnimation[1];
                arr[0] = defs[0];
                arr[0].name     = c.name;
                arr[0].loopTime = c.loop;
                arr[0].lockRootRotation = false;
                // 제자리 재생: 이동은 코드가 담당하므로 루트 XZ 이동을 죽인다
                arr[0].lockRootHeightY  = true;
                arr[0].keepOriginalPositionXZ = false;
                imp.clipAnimations = arr;
            }
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();

            var clip = LoadClip(path);
            if (clip == null) { Debug.LogWarning($"[ZombieAnim] 클립 로드 실패: {path}"); continue; }
            loaded[c.name] = clip;
            Debug.Log($"[ZombieAnim] {c.name,-16} {clip.length:F2}s  loop={clip.isLooping}  human={clip.humanMotion}");
        }

        // ---- 3. AnimatorController ----------------------------------------
        AssetDatabase.DeleteAsset(AcPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(AcPath);

        ac.AddParameter("Speed",  AnimatorControllerParameterType.Float);
        ac.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Bite",   AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Spawn",  AnimatorControllerParameterType.Trigger);

        var sm = ac.layers[0].stateMachine;

        // 3-1. 이동 블렌드 트리 (Idle → Walk → Run)
        BlendTree tree;
        var loco = ac.CreateBlendTreeInController("Locomotion", out tree, 0);
        tree.blendType      = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (Has(loaded, "Zombie_Idle")) tree.AddChild(loaded["Zombie_Idle"], 0f);
        if (Has(loaded, "Zombie_Walk")) tree.AddChild(loaded["Zombie_Walk"], SpeedWalk);
        if (Has(loaded, "Zombie_Run"))  tree.AddChild(loaded["Zombie_Run"],  SpeedRun);
        sm.defaultState = loco;

        // 3-2. 단발 상태들
        var attack  = AddState(sm, "Attack",  loaded, "Zombie_Attack",  new Vector3(320, -120));
        var bite    = AddState(sm, "Bite",    loaded, "Zombie_Bite",    new Vector3(320,  -40));
        var standUp = AddState(sm, "StandUp", loaded, "Zombie_StandUp", new Vector3(320,   40));
        var dead    = AddState(sm, "Dead",    loaded, "Zombie_Die",     new Vector3(320,  140));

        // 3-3. 전이. 죽은 뒤에는 공격/기상 애니메이션이 끼어들지 못하게 IsDead로 잠근다.
        OneShot(sm, attack,  loco, "Attack", 0.08f);
        OneShot(sm, bite,    loco, "Bite",   0.08f);
        OneShot(sm, standUp, loco, "Spawn",  0.10f);

        if (dead != null)
        {
            var toDead = sm.AddAnyStateTransition(dead);
            toDead.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            toDead.duration = 0.12f;
            toDead.hasExitTime = false;
            toDead.canTransitionToSelf = false;
        }

        EditorUtility.SetDirty(ac);

        // ---- 4. 프리팹 ------------------------------------------------------
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFbx);
        if (src == null) { Debug.LogError("[ZombieAnim] 모델 프리팹 로드 실패"); return; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        inst.name = "Zombie_Walker";

        // 툰 머티리얼 적용 (임포트 기본 머티리얼 대신 §15 아트 방향에 맞춤)
        var zMat = ZombieMaterial();
        if (zMat != null)
            foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = zMat;
                r.sharedMaterials = arr;
            }

        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        anim.runtimeAnimatorController = ac;
        anim.avatar = avatar;
        anim.applyRootMotion = false;               // 이동은 스폰 디렉터/이동 코드가 담당
        anim.cullingMode = AnimatorCullingMode.CullCompletely;  // 화면 밖 좀비는 애니메이션 정지

        PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
        Object.DestroyImmediate(inst);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ZombieAnim] 완료 — 클립 {loaded.Count}/{Clips.Length}개, 컨트롤러 {AcPath}, 프리팹 {PrefabPath}");
    }

    // ================= helpers =================

    static bool Has(Dictionary<string, AnimationClip> d, string k) => d.ContainsKey(k) && d[k] != null;

    static AnimatorState AddState(AnimatorStateMachine sm, string stateName,
                                  Dictionary<string, AnimationClip> clips, string clipKey, Vector3 pos)
    {
        if (!Has(clips, clipKey)) { Debug.LogWarning($"[ZombieAnim] 클립 없어 상태 생략: {clipKey}"); return null; }
        var st = sm.AddState(stateName, pos);
        st.motion = clips[clipKey];
        return st;
    }

    /// AnyState → 상태 (트리거, IsDead==false일 때만) → 종료 시 Locomotion 복귀
    static void OneShot(AnimatorStateMachine sm, AnimatorState st, AnimatorState back,
                        string trigger, float dur)
    {
        if (st == null) return;
        var t = sm.AddAnyStateTransition(st);
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        t.duration = dur;
        t.hasExitTime = false;
        t.canTransitionToSelf = false;

        var r = st.AddTransition(back);
        r.hasExitTime = true;
        r.exitTime = 0.90f;
        r.duration = dur;
    }

    /// 좀비 머티리얼.
    /// 프로젝트에 이미 세팅된 tilemap_zombie1.mat를 그대로 쓴다 (BCM_Toony_shader_03 +
    /// _SColorMap + 밝은 청색 셰도우 틴트가 이미 잡혀 있음). 직접 만들면 셰도우 컬러를
    /// 잘못 잡아 좀비가 어둡게 렌더링된다.
    static Material ZombieMaterial()
    {
        const string projectMat = "Assets/_Game/Art/monster/zombie1/tilemap_zombie1.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(projectMat);
        if (existing != null) return existing;

        Debug.LogWarning($"[ZombieAnim] {projectMat} 없음 — 대체 머티리얼을 생성한다");
        const string matPath = OutDir + "/M_Zombie.mat";
        var shader = Shader.Find("BCM/BCM_Toony_shader_03")
                  ?? Shader.Find("BCM/BCM_Toony_shader")
                  ?? Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogWarning("[ZombieAnim] 셰이더를 찾지 못함"); return null; }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
        else mat.shader = shader;

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/monster/zombie1/tilemap_zombie1.png");
        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap"))   mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_SColorMap")) mat.SetTexture("_SColorMap", tex);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_SColor"))    mat.SetColor("_SColor", new Color(0.85f, 0.90f, 1.00f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Avatar LoadAvatar(string path)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Avatar a) return a;
        return null;
    }

    static AnimationClip LoadClip(string path)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        return null;
    }

    static void EnsureFolder(string folder)
    {
        var parts = folder.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
