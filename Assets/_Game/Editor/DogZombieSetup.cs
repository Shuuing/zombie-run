// 좀비 오버런 (TANGTANG) — 좀비 개(zombie_dog_01 · plaguehound) 셋업
// RangedZombieSetup 과 같은 흐름이지만 4족 3ds Max Biped 리그라 Humanoid 가 안 된다 → Generic.
// 클립은 FBX 안의 것을 쓰지 않고 .anim 으로 복제해 쓴다: 이동량이 최상위 Dummy001 의 위치 키에
// 들어 있어서(달리기 0.8초에 +4m, 사망 때 뒤로 5m) 그대로 쓰면 몸이 원점에서 날아간다.
// 복제하면서 Dummy001 의 X/Z 를 첫 키 값으로 고정한다(이동은 코드가 담당). 재실행 가능.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class DogZombieSetup
{
    const string SrcDir   = "Assets/_Game/Art/monster/zombie_dog_01";
    const string AnimDir  = SrcDir + "/animation";
    const string ModelFbx = SrcDir + "/dog_01_set.fbx";
    const string MatPath  = SrcDir + "/plaguehound_00.mat";
    const string TexPath  = SrcDir + "/plaguehound_00.png";
    const string BaseMat  = "Assets/_Game/Art/monster/zombie1/tilemap_zombie1.mat";   // 키워드·큐·셰도우 설정을 그대로 물려받는다
    const string OutDir   = "Assets/_Game/Art/Enemies/ZombieDog";
    const string AcPath   = OutDir + "/AC_ZombieDog.controller";
    const string PrefabDir  = "Assets/_Game/Resources/Enemies";
    const string PrefabPath = PrefabDir + "/Zombie_Dog.prefab";
    const string MotionNode = "Dummy001";   // 이동량을 들고 있는 노드

    struct Clip
    {
        public string file, name; public bool loop;
        public Clip(string f, string n, bool l) { file = f; name = n; loop = l; }
    }

    static readonly Clip[] Clips =
    {
        new Clip("dog_01_run", "Dog_Run",  true),
        new Clip("dog_01_att", "Dog_Bite", false),
        new Clip("dog_01_die", "Dog_Die",  false),
    };

    [MenuItem("Tools/좀비 오버런/좀비 개 셋업")]
    public static void Setup()
    {
        EnsureFolder(OutDir);
        EnsureFolder(PrefabDir);

        // ---- 1. 모델 — Generic (경로 바인딩이라 아바타 없이 같은 계층이면 클립이 붙는다) ----
        var modelImp = AssetImporter.GetAtPath(ModelFbx) as ModelImporter;
        if (modelImp == null) { Debug.LogError($"[DogZombie] 모델 임포터 없음: {ModelFbx}"); return; }
        modelImp.animationType   = ModelImporterAnimationType.Generic;
        modelImp.importAnimation = false;
        modelImp.materialImportMode = ModelImporterMaterialImportMode.None;
        EditorUtility.SetDirty(modelImp);
        modelImp.SaveAndReimport();

        // ---- 2. 애니메이션 → 제자리 .anim ----------------------------------
        var loaded = new Dictionary<string, AnimationClip>();
        foreach (var c in Clips)
        {
            string path = $"{AnimDir}/{c.file}.fbx";
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogWarning($"[DogZombie] 없음: {path}"); continue; }
            imp.animationType   = ModelImporterAnimationType.Generic;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();

            var src = LoadClip(path);
            if (src == null) { Debug.LogWarning($"[DogZombie] 클립 로드 실패: {path}"); continue; }

            var clip = Object.Instantiate(src);
            clip.name = c.name;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.path != MotionNode) continue;
                if (b.propertyName != "m_LocalPosition.x" && b.propertyName != "m_LocalPosition.z") continue;
                var cv = AnimationUtility.GetEditorCurve(clip, b);
                float v0 = cv.Evaluate(0f);
                AnimationUtility.SetEditorCurve(clip, b, AnimationCurve.Constant(0f, clip.length, v0));
            }
            var st = AnimationUtility.GetAnimationClipSettings(clip);
            st.loopTime = c.loop;
            AnimationUtility.SetAnimationClipSettings(clip, st);

            string outPath = $"{OutDir}/{c.name}.anim";
            AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(clip, outPath);
            loaded[c.name] = clip;
            Debug.Log($"[DogZombie] {c.name,-9} {clip.length:F2}s loop={c.loop}");
        }

        // ---- 3. 컨트롤러 (AC_Zombie 와 같은 파라미터 이름 — ProtoZombie 가 그대로 쓴다) ----
        // 대기 클립이 없다: 개는 멈추면 바로 문다(물기 쿨 = 물기 클립 길이). Speed 는 받기만 한다.
        AssetDatabase.DeleteAsset(AcPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(AcPath);
        ac.AddParameter("Speed",  AnimatorControllerParameterType.Float);
        ac.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        var sm = ac.layers[0].stateMachine;

        AnimatorState run = null, bite = null, dead = null;
        if (loaded.ContainsKey("Dog_Run"))  { run  = sm.AddState("Run",  new Vector3(300,   0)); run.motion  = loaded["Dog_Run"]; sm.defaultState = run; }
        if (loaded.ContainsKey("Dog_Bite")) { bite = sm.AddState("Bite", new Vector3(560, -80)); bite.motion = loaded["Dog_Bite"]; }
        if (loaded.ContainsKey("Dog_Die"))  { dead = sm.AddState("Dead", new Vector3(560,  80)); dead.motion = loaded["Dog_Die"]; }

        if (bite != null)
        {
            var t = sm.AddAnyStateTransition(bite);
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            t.duration = 0.06f; t.hasExitTime = false; t.canTransitionToSelf = true;   // 붙어서 연속으로 물 때 처음부터 다시
            if (run != null)
            {
                var r = bite.AddTransition(run);
                r.hasExitTime = true; r.exitTime = 0.95f; r.duration = 0.08f;
            }
        }
        if (dead != null)
        {
            var t = sm.AddAnyStateTransition(dead);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            t.duration = 0.08f; t.hasExitTime = false; t.canTransitionToSelf = false;
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
        var srcModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFbx);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(srcModel);
        inst.name = "Zombie_Dog";
        if (mat != null)
            foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                r.sharedMaterials = arr;
                r.updateWhenOffscreen = false;
            }
        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        anim.runtimeAnimatorController = ac;
        anim.avatar = null;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullCompletely;
        if (inst.GetComponent<Proto.ProtoZombie>() == null) inst.AddComponent<Proto.ProtoZombie>();

        PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
        Object.DestroyImmediate(inst);
        AssetDatabase.SaveAssets();
        Debug.Log($"[DogZombie] 완료 — 클립 {loaded.Count}/{Clips.Length}, {AcPath}, {PrefabPath}");
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
