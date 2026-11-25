// 좀비 오버런 (TANGTANG) — 보스 애니메이션 셋업
// ZombieAnimSetup 과 같은 절차: Mixamo FBX 를 Humanoid 로 리임포트하고, 클립 이름·루프를
// 정리한 뒤 블렌드 트리 기반 AnimatorController 와 프리팹을 만든다. 재실행 가능.
//
// 보스 리그는 좀비와 동일한 표준 Mixamo 골격(mixamorig:*, 본 46개)이라 블렌더 수정 없이
// 임포트 설정만으로 Humanoid 매핑이 된다.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BossAnimSetup
{
    const string AnimDir  = "Assets/_Game/Art/monster/boss/boss_001/animation";
    const string ModelFbx = AnimDir + "/boss_001.fbx";   // 리그가 있는 쪽 (상위 폴더 fbx 는 본이 없음)
    const string OutDir   = "Assets/_Game/Art/Enemies/Boss";
    const string AcPath   = OutDir + "/AC_Boss.controller";
    const string PrefabPath = OutDir + "/Boss_001.prefab";
    const string BossMat  = "Assets/_Game/Art/monster/boss/boss_001/tilemap_zombie1 1.mat";

    // 보스 폴더에 Idle 클립이 없다. Humanoid 클립은 아바타를 넘나들며 리타깃되므로
    // 좀비의 Idle 을 그대로 빌려 쓴다 (블렌드 트리의 Speed 0 지점).
    const string IdleBorrowFbx = "Assets/_Game/Art/monster/zombie1/animation/Zombie Idle.fbx";

    // 블렌드 임계값은 "이 클립이 자연스러워 보이는 실제 이동속도(m/s)"다. 좀비와 같은 계약을
    // 유지해 코드가 넣는 Speed 를 그대로 해석한다. 보스 이동속도가 코드에 정해지면 재조정할 것.
    const float SpeedWalk = 1.6f;
    const float SpeedRun  = 3.4f;

    struct Clip
    {
        public string file;   // fbx 파일명 (확장자 제외)
        public string name;   // 정리된 클립 이름
        public bool loop;
        public bool wire;     // 컨트롤러에 상태로 넣을지
        public Clip(string f, string n, bool l, bool w) { file = f; name = n; loop = l; wire = w; }
    }

    static readonly Clip[] Clips =
    {
        new Clip("Zombie Walk",     "Boss_Walk",     true,  true),
        new Clip("Zombie Running",  "Boss_Run",      true,  true),
        new Clip("Zombie Attack",   "Boss_Attack",   false, true),
        new Clip("Zombie Scream",   "Boss_Scream",   false, true),
        new Clip("Zombie Dying",    "Boss_Die",      false, true),
        // 나머지 5개는 좀비 모션인데 이것만 소총 모션이다. 의도가 확인되지 않아 Humanoid 로
        // 임포트만 해두고 컨트롤러에는 연결하지 않는다 (필요하면 wire=true 로 바꾸면 끝).
        new Clip("Rifle Start Run", "Boss_StartRun", false, false),
    };

    [MenuItem("Tools/좀비 오버런/보스 애니메이션 셋업")]
    public static void Setup()
    {
        EnsureFolder(OutDir);

        // ---- 1. 모델을 Humanoid 로 리임포트해 아바타 생성 -----------------
        var modelImp = AssetImporter.GetAtPath(ModelFbx) as ModelImporter;
        if (modelImp == null) { Debug.LogError($"[BossAnim] 모델 임포터를 찾지 못함: {ModelFbx}"); return; }

        modelImp.animationType   = ModelImporterAnimationType.Human;
        modelImp.avatarSetup     = ModelImporterAvatarSetup.CreateFromThisModel;
        modelImp.importAnimation = false;   // 모델 쪽 더미 클립(Take 001)은 필요 없다
        EditorUtility.SetDirty(modelImp);
        modelImp.SaveAndReimport();

        var avatar = LoadAvatar(ModelFbx);
        if (avatar == null) { Debug.LogError("[BossAnim] 아바타 생성 실패 — 본 매핑을 확인할 것"); return; }
        if (!avatar.isValid || !avatar.isHuman)
            Debug.LogWarning($"[BossAnim] 아바타 상태 주의: valid={avatar.isValid} human={avatar.isHuman}");
        else
            Debug.Log($"[BossAnim] 아바타 생성 OK: {avatar.name} (valid, human)");

        // ---- 2. 애니메이션 FBX 를 Humanoid + 아바타 복사로 리임포트 --------
        var loaded = new Dictionary<string, AnimationClip>();
        foreach (var c in Clips)
        {
            string path = $"{AnimDir}/{c.file}.fbx";
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogWarning($"[BossAnim] 없음: {path}"); continue; }

            imp.animationType   = ModelImporterAnimationType.Human;
            imp.avatarSetup     = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar    = avatar;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;  // 애니메이션 전용
            imp.resampleCurves  = true;

            var defs = imp.defaultClipAnimations;
            if (defs != null && defs.Length > 0)
            {
                var arr = new ModelImporterClipAnimation[1];
                arr[0] = defs[0];
                arr[0].name     = c.name;
                arr[0].loopTime = c.loop;
                arr[0].lockRootRotation = false;
                // 제자리 재생: 이동은 코드가 담당한다. XZ 는 Bake Into Pose 를 반드시 꺼야 한다 —
                // 켜면 이동량이 몸통 포즈로 들어가 메시만 미끄러졌다 루프마다 되돌아온다.
                arr[0].lockRootPositionXZ     = false;
                arr[0].keepOriginalPositionXZ = false;
                arr[0].lockRootHeightY        = true;
                imp.clipAnimations = arr;
            }
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();

            var clip = LoadClip(path);
            if (clip == null) { Debug.LogWarning($"[BossAnim] 클립 로드 실패: {path}"); continue; }
            loaded[c.name] = clip;
            Debug.Log($"[BossAnim] {c.name,-16} {clip.length:F2}s  loop={clip.isLooping}  human={clip.humanMotion}  wire={c.wire}");
        }

        // Idle 은 좀비에게서 빌린다 (Humanoid 리타깃)
        var idle = LoadClip(IdleBorrowFbx);
        if (idle == null) Debug.LogWarning($"[BossAnim] Idle 대체 클립을 찾지 못함: {IdleBorrowFbx}");

        // ---- 3. AnimatorController ----------------------------------------
        AssetDatabase.DeleteAsset(AcPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(AcPath);

        ac.AddParameter("Speed",  AnimatorControllerParameterType.Float);
        ac.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Scream", AnimatorControllerParameterType.Trigger);

        var sm = ac.layers[0].stateMachine;

        // 3-1. 이동 블렌드 트리 (Idle → Walk → Run)
        BlendTree tree;
        var loco = ac.CreateBlendTreeInController("Locomotion", out tree, 0);
        tree.blendType      = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (idle != null)             tree.AddChild(idle, 0f);
        if (Has(loaded, "Boss_Walk")) tree.AddChild(loaded["Boss_Walk"], SpeedWalk);
        if (Has(loaded, "Boss_Run"))  tree.AddChild(loaded["Boss_Run"],  SpeedRun);
        sm.defaultState = loco;

        // 3-2. 단발 상태들
        var attack = AddState(sm, "Attack", loaded, "Boss_Attack", new Vector3(320, -100));
        var scream = AddState(sm, "Scream", loaded, "Boss_Scream", new Vector3(320,  -10));
        var dead   = AddState(sm, "Dead",   loaded, "Boss_Die",    new Vector3(320,  110));

        // 3-3. 전이. 죽은 뒤에는 공격·포효가 끼어들지 못하게 IsDead 로 잠근다.
        OneShot(sm, attack, loco, "Attack", 0.08f);
        OneShot(sm, scream, loco, "Scream", 0.12f);

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
        if (src == null) { Debug.LogError("[BossAnim] 모델 프리팹 로드 실패"); return; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        inst.name = "Boss_001";

        var mat = AssetDatabase.LoadAssetAtPath<Material>(BossMat);
        if (mat == null) Debug.LogWarning($"[BossAnim] 보스 머티리얼 없음: {BossMat}");
        else
            foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                r.sharedMaterials = arr;
            }

        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        anim.runtimeAnimatorController = ac;
        anim.avatar = avatar;
        anim.applyRootMotion = false;               // 이동은 코드가 담당
        anim.cullingMode = AnimatorCullingMode.CullCompletely;  // 화면 밖이면 애니메이션 정지

        PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
        Object.DestroyImmediate(inst);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BossAnim] 완료 — 클립 {loaded.Count}/{Clips.Length}개, 컨트롤러 {AcPath}, 프리팹 {PrefabPath}");
    }

    // ================= helpers (ZombieAnimSetup 과 동일) =================

    static bool Has(Dictionary<string, AnimationClip> d, string k) => d.ContainsKey(k) && d[k] != null;

    static AnimatorState AddState(AnimatorStateMachine sm, string stateName,
                                  Dictionary<string, AnimationClip> clips, string clipKey, Vector3 pos)
    {
        if (!Has(clips, clipKey)) { Debug.LogWarning($"[BossAnim] 클립 없어 상태 생략: {clipKey}"); return null; }
        var st = sm.AddState(stateName, pos);
        st.motion = clips[clipKey];
        return st;
    }

    /// AnyState → 상태 (트리거, IsDead==false 일 때만) → 종료 시 Locomotion 복귀
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
