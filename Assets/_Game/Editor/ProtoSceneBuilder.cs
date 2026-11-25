// 프로토타입 플레이 씬 빌더.
// Field_Chapter01(필드 아트)을 열고 그 위에 플레이어·시스템을 얹어 Proto_Chapter01로 저장한다.
// 필드 씬을 건드리지 않으므로 FieldBuilder를 다시 돌려도 프로토 씬은 남는다.
using Proto;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProtoSceneBuilder
{
    const string FieldScene  = "Assets/_Game/Scenes/Field_Chapter01.unity";
    const string ProtoScene  = "Assets/_Game/Scenes/Proto_Chapter01.unity";
    // 캐릭터 6종이 생긴 뒤로 플레이어 기본값은 1번(루키)이다. 베이스 프리팹
    // (SushiGirl_Rifle)을 그대로 쓰면 몸 머티리얼이 body_02 로 나와 2번 캐릭터처럼 보인다.
    const string PlayerFbx   = "Assets/_Game/Art/character/an/Prefabs/Characters/Char_01_Rookie.prefab";
    const string CharDir     = "Assets/_Game/Art/character/an/Prefabs/Characters";
    const string Zombie2Mat  = "Assets/_Game/Art/monster/zombie1/tilemap_zombie2.mat";
    const string ZombiePfb   = "Assets/_Game/Art/Enemies/Zombie/Zombie_Walker.prefab";
    const string BossPfb     = "Assets/_Game/Art/Enemies/Boss/Boss_001.prefab";
    const float  PlayerHeight = 1.70f;

    [MenuItem("Tools/좀비 오버런/프로토타입 씬 생성")]
    public static void Build()
    {
        var field = AssetDatabase.LoadAssetAtPath<SceneAsset>(FieldScene);
        if (field == null) { Debug.LogError($"[Proto] 필드 씬이 없다: {FieldScene}  먼저 '필드 생성'을 실행할 것"); return; }

        var scene = EditorSceneManager.OpenScene(FieldScene, OpenSceneMode.Single);

        // 검증용으로 뒀던 테스트 좀비 제거
        var testHolder = GameObject.Find("_ZombieTest");
        if (testHolder != null) Object.DestroyImmediate(testHolder);
        var oldSystems = GameObject.Find("_ProtoSystems");
        if (oldSystems != null) Object.DestroyImmediate(oldSystems);
        var oldPlayer = GameObject.Find("Player");
        if (oldPlayer != null) Object.DestroyImmediate(oldPlayer);

        // ── 플레이어 ─────────────────────────────────────────────────────
        var playerSrc = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFbx);
        if (playerSrc == null) { Debug.LogError($"[Proto] 플레이어 프리팹 없음: {PlayerFbx}"); return; }

        var playerRoot = new GameObject("Player");
        playerRoot.transform.position = Vector3.zero;

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(playerSrc);
        visual.name = "Visual";
        visual.transform.SetParent(playerRoot.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        // 실측 높이로 스케일 보정
        var rs = visual.GetComponentsInChildren<Renderer>(true);
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            if (b.size.y > 0.01f)
            {
                float s = PlayerHeight / b.size.y;
                visual.transform.localScale = Vector3.one * s;
                Debug.Log($"[Proto] 플레이어 실측 높이 {b.size.y:F2}m → 스케일 {s:F3}");
            }
        }

        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(playerRoot.transform, false);
        muzzle.localPosition = new Vector3(0.18f, 1.05f, 0.45f);

        var pPlayer  = playerRoot.AddComponent<ProtoPlayer>();
        var pShooter = playerRoot.AddComponent<ProtoShooter>();
        pShooter.muzzle = muzzle;
        // 화염 머티리얼 배선은 없앴다 — 화염은 파티클(Resources/VFX/FX_FlameJet)만 쓰고
        // ProtoFlameCone 이 스스로 불러온다.

        playerRoot.AddComponent<ProtoSupportGear>();   // 카드로 얻는 보조 장비 (§07)

        // 캐릭터 교체 테스트 패널 (임시 — §04 스쿼드 순차 교체가 들어오면 이 컴포넌트는 지운다)
        AttachCharacterSwitcher(playerRoot);

        // 발밑 링 — 좀비 더미에 묻혀도 내 위치가 읽히게 (GDD §15)
        var ring = new GameObject("GroundRing");
        ring.transform.SetParent(playerRoot.transform, false);
        ring.AddComponent<ProtoGroundRing>();

        // 물에 들어갔을 때 수면에 남는 자국 (수면 셰이더가 그린다)
        var foam = playerRoot.AddComponent<WaterContactFoam>();
        foam.mode       = WaterContactFoam.Mode.Both;      // 파문 + 깊은 물에서만 몸 둘레 띠
        foam.bandRadius = 0.30f;   // 띠를 켤 때 쓰는 값 — 수면 0.55m 높이의 몸통 실측 0.258 + 여유
        foam.priority   = 10;

        // ── 카메라 ───────────────────────────────────────────────────────
        var camGO = GameObject.FindWithTag("MainCamera");
        if (camGO == null)
        {
            camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = camGO.AddComponent<Camera>();
            c.fieldOfView = 42f; c.nearClipPlane = 0.3f; c.farClipPlane = 90f;
        }
        camGO.transform.SetParent(null, true);
        var follow = camGO.GetComponent<ProtoCameraFollow>();
        if (follow == null) follow = camGO.AddComponent<ProtoCameraFollow>();
        follow.target = playerRoot.transform;

        // ── 시스템 ───────────────────────────────────────────────────────
        var sys = new GameObject("_ProtoSystems");
        var run       = sys.AddComponent<ProtoRun>();
        var director  = sys.AddComponent<ProtoSpawnDirector>();
        var gemPool   = sys.AddComponent<ProtoGemPool>();
        var hud       = sys.AddComponent<ProtoHUD>();
        var obstacles = sys.AddComponent<ProtoObstacles>();
        obstacles.blockEnemies = true;

        var zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePfb);
        if (zombiePrefab == null)
            Debug.LogError($"[Proto] 좀비 프리팹 없음: {ZombiePfb}  먼저 '좀비 애니메이션 셋업'을 실행할 것");

        var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPfb);
        if (bossPrefab == null)
            Debug.LogWarning($"[Proto] 보스 프리팹 없음: {BossPfb}  '보스 애니메이션 셋업'을 실행할 것 (보스 없이도 런은 돌아간다)");

        director.zombiePrefab = zombiePrefab;
        director.zombie2Material = AssetDatabase.LoadAssetAtPath<Material>(Zombie2Mat);   // 임시(테스트)
        director.bossPrefab   = bossPrefab;
        director.player       = playerRoot.transform;
        director.viewCamera   = camGO.GetComponent<Camera>();

        gemPool.player = pPlayer;
        gemPool.gemSprites = LoadGemSprites();   // 임시 — 배열을 비우면 기존 큐브로 돌아간다

        run.player   = pPlayer;
        run.director = director;
        run.gems     = gemPool;
        run.gearOrbitBlade   = AssetDatabase.LoadAssetAtPath<WeaponModelDef>("Assets/_Game/Data/Weapons/W_OrbitBlade.asset");
        run.gearSupportDrone = AssetDatabase.LoadAssetAtPath<WeaponModelDef>("Assets/_Game/Data/Weapons/W_SupportDrone.asset");
        run.gearGrenade      = AssetDatabase.LoadAssetAtPath<WeaponModelDef>("Assets/_Game/Data/Weapons/W_Grenade.asset");
        run.gearHeli         = AssetDatabase.LoadAssetAtPath<WeaponModelDef>("Assets/_Game/Data/Weapons/W_HeliStrike.asset");
        if (run.gearOrbitBlade == null || run.gearSupportDrone == null || run.gearGrenade == null || run.gearHeli == null)
            Debug.LogWarning("[Proto] 보조 장비 에셋을 못 찾았다 — 그 카드는 후보에 안 뜬다.");

        hud.run      = run;
        hud.player   = pPlayer;
        hud.director = director;

        // ── 저장 ─────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ProtoScene);
        AssetDatabase.Refresh();

        Debug.Log($"[Proto] 완료 — {ProtoScene} 저장. Play 버튼으로 바로 테스트 가능.");
    }

    /// <summary>
    /// 지금 열려 있는 씬에 <b>새로 생긴 배선만</b> 얹는다 (교체 패널 · 보조 장비 · 카드 에셋).
    /// 씬을 통째로 다시 굽지 않아도 되게 따로 뺐다 — `프로토타입 씬 생성` 은 프롭 배치까지
    /// 다시 뿌리므로, 컴포넌트 하나 붙이려고 돌리면 씬이 필요 이상으로 달라진다.
    /// 여러 번 돌려도 안전하다.
    /// </summary>
    [MenuItem("Tools/좀비 오버런/프로토 씬 배선 갱신 (현재 씬)")]
    public static void RefreshOpenSceneWiring()
    {
        var player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[Proto] 현재 씬에서 'Player' 를 못 찾았다. 프로토 씬을 열고 실행할 것.");
            return;
        }

        AttachCharacterSwitcher(player);

        if (player.GetComponent<ProtoSupportGear>() == null)
        {
            player.AddComponent<ProtoSupportGear>();
            Debug.Log("[Proto] 보조 장비 컴포넌트를 붙였다.");
        }

        var dir2 = Object.FindObjectOfType<ProtoSpawnDirector>();
        if (dir2 != null && dir2.zombie2Material == null)
        {
            dir2.zombie2Material = AssetDatabase.LoadAssetAtPath<Material>(Zombie2Mat);
            EditorUtility.SetDirty(dir2);
            Debug.Log("[Proto] 좀비2 스킨 배선 = " + (dir2.zombie2Material != null));
        }

        var gems = Object.FindObjectOfType<ProtoGemPool>();
        if (gems != null && (gems.gemSprites == null || gems.gemSprites.Length == 0))
        {
            gems.gemSprites = LoadGemSprites();
            EditorUtility.SetDirty(gems);
            Debug.Log("[Proto] 젬 스프라이트 배선 = " + (gems.gemSprites != null ? gems.gemSprites.Length : 0) + "장");
        }

        var run = Object.FindObjectOfType<ProtoRun>();
        if (run == null) Debug.LogWarning("[Proto] 씬에 ProtoRun 이 없다 — 보조 장비 카드를 배선하지 못했다.");
        else
        {
            run.gearOrbitBlade   = AssetDatabase.LoadAssetAtPath<WeaponModelDef>("Assets/_Game/Data/Weapons/W_OrbitBlade.asset");
            run.gearSupportDrone = AssetDatabase.LoadAssetAtPath<WeaponModelDef>("Assets/_Game/Data/Weapons/W_SupportDrone.asset");
            run.gearGrenade      = AssetDatabase.LoadAssetAtPath<WeaponModelDef>("Assets/_Game/Data/Weapons/W_Grenade.asset");
            run.gearHeli         = AssetDatabase.LoadAssetAtPath<WeaponModelDef>("Assets/_Game/Data/Weapons/W_HeliStrike.asset");
            EditorUtility.SetDirty(run);
            Debug.Log("[Proto] 보조 장비 카드 배선 — 블레이드=" + (run.gearOrbitBlade != null) +
                      " 드론=" + (run.gearSupportDrone != null) + " 수류탄=" + (run.gearGrenade != null) + " 헬기=" + (run.gearHeli != null));
        }

        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("[Proto] 배선 갱신 완료. 씬 저장(Ctrl+S)은 직접 할 것.");
    }

    /// <summary>
    /// 경험치 젬에 쓸 임시 스프라이트(물고기 3종). XP 낮은 순서 = 배열 순서다.
    /// 없으면 빈 배열을 돌려주고, 그러면 젬 풀이 기존 큐브로 동작한다.
    /// </summary>
    static Texture2D[] LoadGemSprites()
    {
        string[] names = { "1", "2", "3" };
        var list = new System.Collections.Generic.List<Texture2D>();
        foreach (var n in names)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/" + n + ".png");
            if (t != null) list.Add(t);
        }
        if (list.Count != names.Length)
            Debug.LogWarning("[Proto] 젬 스프라이트를 " + list.Count + "/" + names.Length + "장만 찾았다.");
        return list.ToArray();
    }

    /// <summary>
    /// 캐릭터 교체 패널을 플레이어에 붙인다. 명단은 씬에 복사하지 않는다 — 패널이 런타임에
    /// 캐릭터 테이블(Resources/Data/CharacterTable)을 읽으므로, 테이블을 고치면 씬을 다시 굽지 않아도 반영된다.
    /// 여기서는 테이블이 있는지와 몇 명이 프리팹까지 갖췄는지만 확인해 로그로 남긴다.
    /// </summary>
    public static ProtoCharacterSwitcher AttachCharacterSwitcher(GameObject playerRoot)
    {
        var sw = playerRoot.GetComponent<ProtoCharacterSwitcher>();
        if (sw == null) sw = playerRoot.AddComponent<ProtoCharacterSwitcher>();
        sw.roster = null;   // 예전 씬에 프리팹 배열이 남아 있어도 테이블이 이긴다

        var table = AssetDatabase.LoadAssetAtPath<CharacterTable>("Assets/_Game/Resources/" + CharacterTable.ResourcePath + ".asset");
        if (table == null || table.roster == null || table.roster.Length == 0)
        {
            Debug.LogWarning("[Proto] 캐릭터 테이블이 없거나 비어 있다 — 교체 패널이 빈다. Tools/좀비 오버런/데이터 테이블/검증 을 돌릴 것.");
            return sw;
        }
        var names = new System.Collections.Generic.List<string>();
        int ready = 0;
        foreach (var c in table.roster)
        {
            if (c == null) { names.Add("(빈 칸)"); continue; }
            names.Add(c.Label + (c.prefab != null ? "" : "(프리팹 없음)"));
            if (c.prefab != null) ready++;
        }
        Debug.Log($"[Proto] 캐릭터 교체 패널 — 테이블 {table.roster.Length}명, 프리팹 갖춘 캐릭터 {ready}명 ({string.Join(", ", names)})");
        return sw;
    }
}
