// 좀비 오버런 (TANGTANG) — 원거리 좀비(zombie_02 · 생존자 옷) 셋업
// ZombieAnimSetup 과 같은 방식: Mixamo FBX 를 Humanoid 로 리임포트 → 클립 이름·루프 정리 →
// AnimatorController → 프리팹. 재실행 가능. 프리팹은 Resources 에 둔다 — 스폰 디렉터가
// 씬 참조 없이 Resources.Load 로 꺼낸다(씬마다 슬롯을 채우지 않아도 되게, 탱크와 같은 방식).
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class RangedZombieSetup
{
    const string SrcDir   = "Assets/_Game/Art/monster/zombie_02";
    const string AnimDir  = SrcDir + "/animation";
    const string ModelFbx = AnimDir + "/tilemap_survivor.fbx";   // 리그가 있는 쪽 (루트 fbx 는 본이 없는 정적 메시)
    const string MatPath  = SrcDir + "/tilemap_survivor.mat";
    const string TexPath  = SrcDir + "/tilemap_survivor.png";
    const string BaseMat  = "Assets/_Game/Art/monster/zombie1/tilemap_zombie1.mat";   // 키워드·큐·셰도우 설정을 그대로 물려받는다
    const string OutDir   = "Assets/_Game/Art/Enemies/ZombieRanged";
    const string AcPath   = OutDir + "/AC_ZombieRanged.controller";
    const string PrefabDir  = "Assets/_Game/Resources/Enemies";
    const string PrefabPath = PrefabDir + "/Zombie_Ranged.prefab";

    struct Clip
    {
        public string file, name; public bool loop;
        public Clip(string f, string n, bool l) { file = f; name = n; loop = l; }
    }

    static readonly Clip[] Clips =
    {
        new Clip("Zombie Idle",                  "Ranged_Idle",   true),
        new Clip("Zombie Run",                   "Ranged_Run",    true),
        new Clip("Standing 2H Magic Attack 01",  "Ranged_Cast",   false),
        new Clip("Zombie Death",                 "Ranged_Die",    false),
    };

    [MenuItem("Tools/좀비 오버런/원거리 좀비 셋업")]
    public static void Setup()
    {
        EnsureFolder(OutDir);
        EnsureFolder(PrefabDir);

        // ---- 1. 모델 → Humanoid 아바타 ----------------------------------
        var modelImp = AssetImporter.GetAtPath(ModelFbx) as ModelImporter;
        if (modelImp == null) { Debug.LogError($"[RangedZombie] 모델 임포터 없음: {ModelFbx}"); return; }
        modelImp.animationType   = ModelImporterAnimationType.Human;
        modelImp.avatarSetup     = ModelImporterAvatarSetup.CreateFromThisModel;
        modelImp.importAnimation = false;
        modelImp.materialImportMode = ModelImporterMaterialImportMode.None;
        EditorUtility.SetDirty(modelImp);
        modelImp.SaveAndReimport();

        var avatar = LoadAvatar(ModelFbx);
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        { Debug.LogError($"[RangedZombie] 아바타 실패 valid={avatar?.isValid} human={avatar?.isHuman}"); return; }

        // ---- 2. 애니메이션 -------------------------------------------------
        var loaded = new Dictionary<string, AnimationClip>();
        foreach (var c in Clips)
        {
            string path = $"{AnimDir}/{c.file}.fbx";
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogWarning($"[RangedZombie] 없음: {path}"); continue; }

            imp.animationType   = ModelImporterAnimationType.Human;
            imp.avatarSetup     = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar    = avatar;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.resampleCurves  = true;

            var defs = imp.defaultClipAnimations;
            if (defs != null && defs.Length > 0)
            {
                var a = defs[0];
                a.name = c.name;
                a.loopTime = c.loop;
                a.lockRootRotation = false;
                a.lockRootHeightY  = true;          // 제자리 재생 — 이동은 코드가 담당
                a.keepOriginalPositionXZ = false;
                imp.clipAnimations = new[] { a };
            }
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();

            var clip = LoadClip(path);
            if (clip == null) { Debug.LogWarning($"[RangedZombie] 클립 로드 실패: {path}"); continue; }
            loaded[c.name] = clip;
            Debug.Log($"[RangedZombie] {c.name,-12} {clip.length:F2}s loop={clip.isLooping} human={clip.humanMotion}");
        }

        // ---- 3. 컨트롤러 (AC_Zombie 와 같은 파라미터 이름 — ProtoZombie 가 그대로 쓴다) ----
        AssetDatabase.DeleteAsset(AcPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(AcPath);
        ac.AddParameter("Speed",  AnimatorControllerParameterType.Float);
        ac.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        var sm = ac.layers[0].stateMachine;

        BlendTree tree;
        var loco = ac.CreateBlendTreeInController("Locomotion", out tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (loaded.ContainsKey("Ranged_Idle")) tree.AddChild(loaded["Ranged_Idle"], 0f);
        // 달리기 임계값 = 원거리 좀비 이동속도(적 테이블 E_Ranged.moveSpeed). 그 속도에서 달리기 100%.
        if (loaded.ContainsKey("Ranged_Run"))  tree.AddChild(loaded["Ranged_Run"], 1.8f);
        sm.defaultState = loco;

        AnimatorState cast = null, dead = null;
        if (loaded.ContainsKey("Ranged_Cast")) { cast = sm.AddState("Cast", new Vector3(320, -80)); cast.motion = loaded["Ranged_Cast"]; }
        if (loaded.ContainsKey("Ranged_Die"))  { dead = sm.AddState("Dead", new Vector3(320,  80)); dead.motion = loaded["Ranged_Die"]; }

        if (cast != null)
        {
            var t = sm.AddAnyStateTransition(cast);
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");   // 사망 후 시전이 끼어들지 않게
            t.duration = 0.08f; t.hasExitTime = false; t.canTransitionToSelf = false;
            var r = cast.AddTransition(loco);
            r.hasExitTime = true; r.exitTime = 0.92f; r.duration = 0.12f;
        }
        if (dead != null)
        {
            var t = sm.AddAnyStateTransition(dead);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            t.duration = 0.12f; t.hasExitTime = false; t.canTransitionToSelf = false;
        }
        EditorUtility.SetDirty(ac);

        // ---- 4. 머티리얼 — zombie1 을 복제해 텍스처만 바꾼다 ---------------
        // 직접 만들면 _EMISSION(피격 플래시)·큐 2450·BCM_PLANAR_SHADOW(발밑 그림자)를 놓친다(ProtoZombie.SetSkin 주석).
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            AssetDatabase.CopyAsset(BaseMat, MatPath);
            mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        if (mat != null && tex != null)
        {
            foreach (var prop in new[] { "_BaseMap", "_SColorMap", "_OutlineColorMap" })
                if (mat.HasProperty(prop)) mat.SetTexture(prop, tex);
            EditorUtility.SetDirty(mat);
        }

        // ---- 5. 프리팹 ------------------------------------------------------
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFbx);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        inst.name = "Zombie_Ranged";
        if (mat != null)
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
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullCompletely;
        if (inst.GetComponent<Proto.ProtoZombie>() == null) inst.AddComponent<Proto.ProtoZombie>();

        PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
        Object.DestroyImmediate(inst);
        AssetDatabase.SaveAssets();
        Debug.Log($"[RangedZombie] 완료 — 클립 {loaded.Count}/{Clips.Length}, {AcPath}, {PrefabPath}");
    }

    static Avatar LoadAvatar(string path)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Avatar a) return a;
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
