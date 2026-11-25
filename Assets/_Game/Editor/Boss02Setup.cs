// 좀비 오버런 (TANGTANG) — 최종 보스(boss_02, 망치) 셋업. 프로토 테스트용 (사용자 요청 2026-10-13).
// BossAnimSetup(boss_001)과 같은 절차: Mixamo FBX → Humanoid, 클립 정리, 컨트롤러, 프리팹.
// 다른 점: ① 걷기·달리기 클립이 없어 boss_001 것을 빌린다(Humanoid 리타깃)
//          ② 따로 떨어져 있는 망치(weapon_01)를 오른손 본에 붙인다
//          ③ 프리팹을 Resources 에 둬서 씬 배선 없이 ProtoSpawnDirector 가 집어 간다.
// 재실행 가능.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class Boss02Setup
{
    const string Dir      = "Assets/_Game/Art/monster/boss/boss_02";
    const string ModelFbx = Dir + "/boss_02.fbx";
    const string AnimDir  = Dir + "/animation";
    const string BodyTex  = Dir + "/boss_02_tex/boss_02_d.png";
    const string OutDir   = "Assets/_Game/Art/Enemies/Boss";
    const string AcPath   = OutDir + "/AC_Boss02.controller";
    const string BodyMatPath   = OutDir + "/M_Boss02.mat";
    const string HammerMatPath = OutDir + "/M_Boss02_Hammer.mat";
    public const string PrefabPath = "Assets/_Game/Resources/Enemies/Boss_Final.prefab";

    // 셰이더·외곽선 설정은 boss_001 머티리얼을 그대로 복제한다 — 같은 화면에 서는 보스끼리 룩이 같아야 한다.
    const string RefMat = "Assets/_Game/Art/monster/boss/boss_001/tilemap_zombie1 1.mat";

    const string IdleFbx = "Assets/_Game/Art/monster/zombie1/animation/Zombie Idle.fbx";
    const string WalkFbx = "Assets/_Game/Art/monster/boss/boss_001/animation/Zombie Walk.fbx";
    const string RunFbx  = "Assets/_Game/Art/monster/boss/boss_001/animation/Zombie Running.fbx";

    // 프리팹 루트 배율. 원본 키 약 3.2m → 2.6m(boss_001 과 같은 키). 최종 보스는 스폰 때 ×1.3 이 더 붙는다.
    const float RootScale = 0.82f;

    // ── 망치 ────────────────────────────────────────────────────────
    // 원본 망치: 손잡이가 모델 +Y 축(-1.44 ~ 1.47), 머리가 1.47 ~ 2.93, 때리는 면이 ±Z.
    // 길이 4.4m 라 캐릭터(3.2m)보다 크다 → 0.7배(약 3.1m, 캐릭터 키 정도 — 콘티 참고).
    public const float HammerScale = 0.7f;
    const float GripY = -1.05f;          // 손이 쥐는 손잡이 높이(원본 모델 좌표) — 끝에서 0.4m 위
    public const float HeadCenterY = 2.2f;   // 망치 머리 중심(원본 모델 좌표) — 이펙트 기준점

    struct Clip
    {
        public string file, name; public bool loop;
        public Clip(string f, string n, bool l) { file = f; name = n; loop = l; }
    }

    static readonly Clip[] Clips =
    {
        new Clip("Standing Melee Attack Downward", "Boss2_Slam",     false),
        new Clip("Standing Melee Run Jump Attack", "Boss2_JumpSlam", false),
        new Clip("Standing Taunt Battlecry",       "Boss2_Roar",     false),
        new Clip("Dying",                          "Boss2_Die",      false),
        // 이동 — 사용자가 2026-10-13 추가. 이게 없으면 boss_001 걷기·달리기를 빌려 쓴다(아래 폴백).
        new Clip("Unarmed Walk Forward",           "Boss2_Walk",     true),
        new Clip("Zombie Run",                     "Boss2_Run",      true),
    };

    [MenuItem("Tools/좀비 오버런/최종 보스(boss_02) 셋업")]
    public static void Setup()
    {
        EnsureFolder(OutDir);
        EnsureFolder("Assets/_Game/Resources/Enemies");

        // ---- 1. 모델 → Humanoid 아바타 ----------------------------------
        var modelImp = AssetImporter.GetAtPath(ModelFbx) as ModelImporter;
        if (modelImp == null) { Debug.LogError($"[Boss02] 모델 임포터 없음: {ModelFbx}"); return; }
        modelImp.animationType   = ModelImporterAnimationType.Human;
        modelImp.avatarSetup     = ModelImporterAvatarSetup.CreateFromThisModel;
        modelImp.importAnimation = false;
        // 머티리얼은 아래에서 직접 만들고, FBX 의 재질 슬롯도 그것으로 리맵한다(4단계 뒤).
        // None 으로 두면 FBX 를 씬에 바로 놓거나 미리보기할 때 회색이 된다(2026-10-13 "텍스처가 안 나온다").
        modelImp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        EditorUtility.SetDirty(modelImp);
        modelImp.SaveAndReimport();

        var avatar = LoadAvatar(ModelFbx);
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        { Debug.LogError($"[Boss02] 아바타 생성 실패 (valid={avatar?.isValid} human={avatar?.isHuman})"); return; }

        // ---- 2. 클립 -----------------------------------------------------
        var loaded = new Dictionary<string, AnimationClip>();
        foreach (var c in Clips)
        {
            string path = $"{AnimDir}/{c.file}.fbx";
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogWarning($"[Boss02] 없음: {path}"); continue; }
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
                // 제자리 재생 — 이동은 코드가 한다(점프 공격의 도약도 코드가 포물선으로 옮긴다).
                // XZ 는 루트 모션으로 보내서 버리고(Bake 끔), 높이는 포즈에 남겨 몸이 실제로 뛰어오르게 한다.
                a.lockRootPositionXZ     = false;
                a.keepOriginalPositionXZ = false;
                a.lockRootHeightY        = true;
                a.keepOriginalPositionY  = true;
                imp.clipAnimations = new[] { a };
            }
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();
            var clip = LoadClip(path);
            if (clip != null) { loaded[c.name] = clip; Debug.Log($"[Boss02] {c.name,-16} {clip.length:F2}s"); }
        }
        var idle = LoadClip(IdleFbx);
        var walk = loaded.TryGetValue("Boss2_Walk", out var w2) && w2 != null ? w2 : LoadClip(WalkFbx);
        var run  = loaded.TryGetValue("Boss2_Run",  out var r2) && r2 != null ? r2 : LoadClip(RunFbx);
        Debug.Log($"[Boss02] 이동 클립 — 걷기 {walk?.name}, 달리기 {run?.name}");

        // ---- 3. 컨트롤러 ---------------------------------------------------
        AssetDatabase.DeleteAsset(AcPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(AcPath);
        ac.AddParameter("Speed",      AnimatorControllerParameterType.Float);
        ac.AddParameter("IsDead",     AnimatorControllerParameterType.Bool);
        ac.AddParameter("Attack",     AnimatorControllerParameterType.Trigger);   // 제자리 내려찍기
        ac.AddParameter("JumpAttack", AnimatorControllerParameterType.Trigger);   // 도약 내려찍기
        ac.AddParameter("Scream",     AnimatorControllerParameterType.Trigger);   // 등장 포효
        var sm = ac.layers[0].stateMachine;

        var loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (idle != null) tree.AddChild(idle, 0f);
        // 임계값 = 클립이 자연스러운 실제 이동 속도(m/s). 최종 보스 크기(0.82 × 1.3)에서 잰 값 —
        // 걷기 1.56m/1.37s, 달리기 2.78m/0.80s 를 크기 배율로 환산(2026-10-13). 코드가 넣는 Speed 와 맞아야 발이 안 미끄러진다.
        if (walk != null) tree.AddChild(walk, walk.name == "Boss2_Walk" ? 1.48f : 1.6f);
        if (run  != null) tree.AddChild(run,  run.name  == "Boss2_Run"  ? 4.51f : 3.4f);
        sm.defaultState = loco;

        var slam  = AddState(sm, "Attack",     loaded, "Boss2_Slam",     new Vector3(320, -150));
        var jump  = AddState(sm, "JumpAttack", loaded, "Boss2_JumpSlam", new Vector3(320,  -60));
        var roar  = AddState(sm, "Scream",     loaded, "Boss2_Roar",     new Vector3(320,   30));
        var dead  = AddState(sm, "Dead",       loaded, "Boss2_Die",      new Vector3(320,  120));
        OneShot(sm, slam, loco, "Attack",     0.10f);
        OneShot(sm, jump, loco, "JumpAttack", 0.10f);
        OneShot(sm, roar, loco, "Scream",     0.15f);
        if (dead != null)
        {
            var t = sm.AddAnyStateTransition(dead);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            t.duration = 0.12f; t.hasExitTime = false; t.canTransitionToSelf = false;
        }
        EditorUtility.SetDirty(ac);

        // ---- 4. 머티리얼 ---------------------------------------------------
        var refMat  = AssetDatabase.LoadAssetAtPath<Material>(RefMat);
        var bodyTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BodyTex);
        var bodyMat = MakeMat(BodyMatPath, refMat, bodyTex, Color.white);
        // 망치는 텍스처가 없다. 이 툰 셰이더는 밝은 면·그림자 면 색을 텍스처 슬롯에서도 읽어서
        // 텍스처를 비우면 흰색이 된다 — 어두운 쇠색 텍스처를 하나 만들어 모든 슬롯에 넣는다.
        var hammerTex = SolidTex(OutDir + "/T_Boss02_Hammer.png", new Color(0.36f, 0.34f, 0.33f));
        var hammerMat = MakeMat(HammerMatPath, refMat, hammerTex, Color.white);

        // FBX 재질 슬롯 리맵: 몸 "01 - Default", 망치 "07 - Default"(원본 3ds Max 이름).
        // 이름이 바뀌어도 맞도록, 망치 렌더러가 쓰는 슬롯 이름을 실제로 읽어서 나머지는 전부 몸으로 보낸다.
        {
            string hammerSlot = null;
            var fbxGo = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFbx);
            var wr = fbxGo != null ? fbxGo.transform.Find("weapon_01")?.GetComponent<Renderer>() : null;
            if (wr != null && wr.sharedMaterial != null) hammerSlot = wr.sharedMaterial.name;
            bool changed = false;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ModelFbx))
            {
                if (!(o is Material fm)) continue;
                var target = fm.name == hammerSlot ? hammerMat : bodyMat;
                modelImp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), fm.name), target);
                changed = true;
            }
            if (changed) { EditorUtility.SetDirty(modelImp); modelImp.SaveAndReimport(); }
            Debug.Log($"[Boss02] FBX 재질 리맵 — 망치 슬롯 '{hammerSlot}', 나머지는 몸");
        }

        // ---- 5. 프리팹 -----------------------------------------------------
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFbx);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = "Boss_Final";
        inst.transform.position = Vector3.zero;

        foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            r.sharedMaterials = Fill(r.sharedMaterials.Length, bodyMat);

        var hand = FindDeep(inst.transform, "mixamorig:RightHand");
        var index1 = FindDeep(inst.transform, "mixamorig:RightHandIndex1");
        var weapon = inst.transform.Find("weapon_01");
        if (hand == null || weapon == null) { Debug.LogError("[Boss02] 오른손 본 또는 weapon_01 없음"); Object.DestroyImmediate(inst); return; }
        weapon.GetComponent<Renderer>().sharedMaterials = Fill(1, hammerMat);
        AttachHammer(inst.transform, hand, index1, weapon);

        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        anim.runtimeAnimatorController = ac;
        anim.avatar = avatar;
        anim.applyRootMotion = false;
        // 보스는 한 마리고 공격 타이밍이 애니메이션과 맞물린다 — 화면 밖에서도 멈추면 안 된다
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (inst.GetComponent<Proto.ProtoZombie>() == null) inst.AddComponent<Proto.ProtoZombie>();
        var brain = inst.GetComponent<Proto.ProtoBossBrain>();
        if (brain == null) brain = inst.AddComponent<Proto.ProtoBossBrain>();
        brain.hammerHead = FindDeep(inst.transform, "HammerHead");

        inst.transform.localScale = Vector3.one * RootScale;
        PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
        Object.DestroyImmediate(inst);
        AssetDatabase.SaveAssets();
        // 컨트롤러를 지우고 같은 경로에 다시 만들기 때문에, 메모리에 올라 있던 프리팹이 옛 컨트롤러를 붙잡고
        // **컨트롤러 없음(T포즈)** 으로 스폰되는 일이 있었다(2026-10-13). 둘 다 강제로 다시 읽힌다.
        AssetDatabase.ImportAsset(AcPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
        var check = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<Animator>();
        if (check == null || check.runtimeAnimatorController == null)
            Debug.LogError("[Boss02] 프리팹의 애니메이터 컨트롤러가 비어 있다 — 보스가 T포즈로 나온다");
        Debug.Log($"[Boss02] 완료 — 클립 {loaded.Count}/{Clips.Length}, 프리팹 {PrefabPath}");
    }

    /// 망치를 오른손 본 밑으로 옮긴다. 바인드 포즈(T포즈)에서 월드 좌표로 맞춘 뒤 부모만 바꾼다.
    /// 쥐는 축: 손 본의 -X(엄지 쪽)로 머리가 향하고, 때리는 면(±Z)은 손 본 +Y(손목→주먹) 방향 —
    /// 위에서 내려찍을 때 주먹이 향하는 쪽이 망치 머리가 날아가는 쪽이다.
    static void AttachHammer(Transform root, Transform hand, Transform index1, Transform weapon)
    {
        var grip = new GameObject("HammerGrip").transform;
        grip.SetParent(hand, false);
        // 손바닥 중심 — 손목과 검지 뿌리 사이
        Vector3 palm = index1 != null ? Vector3.Lerp(hand.position, index1.position, 0.5f) : hand.position;
        Vector3 up  = (hand.rotation * Vector3.left).normalized;   // 망치 머리 방향
        Vector3 fwd = (hand.rotation * Vector3.up).normalized;     // 때리는 면 방향
        fwd = Vector3.ProjectOnPlane(fwd, up).normalized;
        grip.SetPositionAndRotation(palm, Quaternion.LookRotation(fwd, up));

        // 망치 원본은 루트 기준 회전(270,0,0)이 구워져 있다. 메시 좌표 그대로 쓰려고 중간 노드를 둔다:
        // HammerGrip(손) → Hammer(모델 공간 축: +Y 손잡이, ±Z 면) → weapon_01(원래 로컬 회전 유지)
        var hammer = new GameObject("Hammer").transform;
        hammer.SetParent(grip, false);
        hammer.localRotation = Quaternion.identity;
        // 본에 배율이 들어 있으면(Mixamo 0.01 골격 등) 로컬 값이 그만큼 커진다 — 월드 크기 기준으로 맞춘다
        float ls = Mathf.Max(0.0001f, grip.lossyScale.x);
        hammer.localScale = Vector3.one * (HammerScale / ls);
        hammer.localPosition = new Vector3(0f, -GripY * HammerScale / ls, 0f);   // 손잡이 쥐는 점이 손바닥에 오게

        var savedLocalRot = weapon.localRotation;   // 루트 아래에서의 회전(모델 공간 → 메시)
        weapon.SetParent(hammer, false);
        weapon.localPosition = Vector3.zero;
        weapon.localRotation = savedLocalRot;
        weapon.localScale = Vector3.one;

        var head = new GameObject("HammerHead").transform;
        head.SetParent(hammer, false);
        head.localPosition = new Vector3(0f, HeadCenterY, 0f);
    }

    static Material MakeMat(string path, Material reference, Texture2D baseTex, Color tint)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = reference != null ? new Material(reference) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        else if (reference != null) { m.shader = reference.shader; m.CopyPropertiesFromMaterial(reference); }

        // 참조 머티리얼의 기본 텍스처를 쓰던 슬롯(BaseMap·외곽선 컬러맵 등)을 전부 갈아 끼운다
        Texture oldBase = reference != null && reference.HasProperty("_BaseMap") ? reference.GetTexture("_BaseMap") : null;
        foreach (var name in m.GetTexturePropertyNames())
        {
            var t = m.GetTexture(name);
            if (t != null && t == oldBase) m.SetTexture(name, baseTex);
        }
        if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Texture2D SolidTex(string path, Color c)
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (t != null) return t;
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) tex.SetPixel(x, y, c);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material[] Fill(int n, Material m) { var a = new Material[Mathf.Max(1, n)]; for (int i = 0; i < a.Length; i++) a[i] = m; return a; }

    static Transform FindDeep(Transform t, string name)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
        return null;
    }

    static AnimatorState AddState(AnimatorStateMachine sm, string stateName, Dictionary<string, AnimationClip> clips, string key, Vector3 pos)
    {
        if (!clips.TryGetValue(key, out var clip) || clip == null) { Debug.LogWarning($"[Boss02] 클립 없음: {key}"); return null; }
        var st = sm.AddState(stateName, pos);
        st.motion = clip;
        return st;
    }

    static void OneShot(AnimatorStateMachine sm, AnimatorState st, AnimatorState back, string trigger, float dur)
    {
        if (st == null) return;
        var t = sm.AddAnyStateTransition(st);
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        t.duration = dur; t.hasExitTime = false; t.canTransitionToSelf = false;
        var r = st.AddTransition(back);
        r.hasExitTime = true; r.exitTime = 0.95f; r.duration = dur;
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
