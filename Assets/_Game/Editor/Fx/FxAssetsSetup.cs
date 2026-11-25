using System.Linq;
using UnityEditor;
using UnityEngine;
using Proto;

/// <summary>
/// FX 카탈로그 · 액션 FX 시트 에셋을 만들거나 빠진 항목을 채운다.
/// <b>있는 항목은 건드리지 않는다</b> — 아트가 에디터에서 옮긴 시각·오프셋이 메뉴를 다시 눌러도 남는다.
///
/// 처음 채우는 값은 ProtoBossBrain 인스펙터 상수(프리팹에 저장된 값)에서 그대로 가져온다.
/// 그래서 시트를 만든 직후의 보스는 시트가 없을 때와 똑같이 움직인다 — 여기서부터 아트가 옮기기 시작한다.
/// </summary>
public static class FxAssetsSetup
{
    public const string CatalogPath = "Assets/_Game/Resources/Data/FxCatalog.asset";
    public const string SheetPath   = "Assets/_Game/Resources/Data/ActionFxSheet.asset";
    const string BossPrefabPath = "Assets/_Game/Resources/Enemies/Boss_Final.prefab";
    const string BossAnimDir    = "Assets/_Game/Art/monster/boss/boss_02/animation/";
    const string VfxDir         = "Assets/_Game/Resources/VFX/";

    /// 코드가 부르는 ID 중 카탈로그에 꼭 있어야 하는 것. 하나라도 빠져 있으면 에디터가 뜰 때 채운다 —
    /// 코드에서 새 효과를 부르기 시작했는데 카탈로그가 옛것이면 아무것도 안 나오는 일을 막는다.
    static readonly string[] RequiredIds =
    {
        FxIds.MuzzleFlash, FxIds.HitSpark, FxIds.KillBurst, FxIds.GemPickup,
        FxIds.EvolveBeam, FxIds.VanHit, FxIds.VanBlast, FxIds.VanAppearDust,
        FxIds.HeliCrashPop, FxIds.HeliCrashImpact, FxIds.TankBlast, FxIds.TankDebrisPop, FxIds.UltCombust,
        FxIds.RifleMuzzle, FxIds.RifleHit, FxIds.RifleKill,   // 루키 소총 전용 세트 (2026-11-04)
        // (FxIds.FlamethrowerJet — 11-23 화염방사기 MSFX 복사본을 지우며 필수에서 뺐다. 화염은 inferno_jet)
        FxIds.ZombieGore,                                      // 처치 피 터짐 (2026-11-05)
        FxIds.IntroLanding,                                    // 시작 연출 낙하산 착지 (2026-11-16)
        FxIds.TankCannonHit,                                   // 탱크 대포 착탄 폭발 (2026-11-17)
        FxIds.GemSparkle, FxIds.GemTrail,                      // 물고기 젬 반짝이 · 흡수 꼬리 (2026-11-18)
        FxIds.OrbitBladeTrail,                                 // 회전 블레이드 톱날 꼬리 (2026-11-18)
    };

    /// 에셋이 없거나 필수 ID 가 빠져 있으면 에디터가 뜰 때(스크립트 리로드 직후) 한 번 채운다 — 씬 배선 없이 바로 돌게.
    /// 다 있으면 아무것도 하지 않는다(채우기는 메뉴로).
    [InitializeOnLoadMethod]
    static void AutoCreate()
    {
        EditorApplication.delayCall += () =>
        {
            var cat = AssetDatabase.LoadAssetAtPath<FxCatalog>(CatalogPath);
            bool need = cat == null || AssetDatabase.LoadAssetAtPath<ActionFxSheet>(SheetPath) == null;
            if (!need) foreach (var id in RequiredIds) if (cat.Find(id) == null) { need = true; break; }
            // 조립 함수가 바뀐 템플릿(표식 자식 없음)은 대화상자 없이 다시 굽고, 길어진 효과의 카탈로그 수명도 맞춘다
            int rebuilt = FxTemplatePrefabs.RebuildOutdated();
            EnsureOverlayLayer();
            if (cat != null && Upgrades(cat) > 0) EditorUtility.SetDirty(cat);
            if (rebuilt > 0 || (cat != null && EditorUtility.IsDirty(cat))) { AssetDatabase.SaveAssets(); Proto.ProtoFx.Clear(); }
            if (!need && FxTemplatePrefabs.MaterialsNeedFix()) { FxTemplatePrefabs.HardenMaterials(); return; }   // 머티리얼 규칙만 어긋났으면 그것만
            if (need) CreateOrUpdate();
        };
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/카탈로그·시트 만들기 (빠진 항목 채우기)")]
    public static void CreateOrUpdate()
    {
        FxTemplatePrefabs.EnsureAll();   // 카탈로그가 가리킬 템플릿 프리팹부터(없는 것만)
        var cat = LoadOrCreate<FxCatalog>(CatalogPath);
        var sheet = LoadOrCreate<ActionFxSheet>(SheetPath);
        int added = FillCatalog(cat) + FillSheet(sheet);
        int wfx = FillWeaponFx();
        EditorUtility.SetDirty(cat);
        EditorUtility.SetDirty(sheet);
        AssetDatabase.SaveAssets();
        FxCatalog.Reload();
        ActionFxSheet.Reload();
        Debug.Log($"[FX 툴] 카탈로그 {cat.entries.Count}항목 · 시트 {sheet.actions.Count}동작 (새로 채운 것 {added}) · 무기 연출 세트 새로 {wfx}개 — {CatalogPath}, {SheetPath}, {WeaponFxDir}");
        Selection.activeObject = sheet;
    }

    // ── 무기 연출 세트 ─────────────────────────────────────────────
    const string WeaponFxDir = "Assets/_Game/Data/Weapons/Fx";

    [MenuItem("Tools/좀비 오버런/FX 툴/무기 연출 에셋 만들기 (빠진 무기만)")]
    public static void CreateWeaponFx()
    {
        int n = FillWeaponFx();
        AssetDatabase.SaveAssets();
        Debug.Log($"[FX 툴] 무기 연출 세트 새로 {n}개 — {WeaponFxDir} (이미 물려 있는 무기는 그대로)");
    }

    /// WeaponStats 마다 연출 세트(WFX_*)를 하나씩 만들어 fx 에 물린다. <b>이미 물려 있으면 건드리지 않는다.</b>
    /// 처음 값은 예전 ProtoShooter 코드 상수(캐논 1.35 · 샷건 1.25 · 탄피 1.2/1.5 · 로켓은 총구 없음 등)를 그대로 옮긴 것 —
    /// 만든 직후의 게임은 전과 같이 보이고, 여기서부터 아트가 무기마다 바꾼다.
    static int FillWeaponFx()
    {
        int made = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:WeaponStats"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var st = AssetDatabase.LoadAssetAtPath<WeaponStats>(p);
            if (st == null || st.fx != null) continue;
            EnsureFolder(WeaponFxDir);
            string name = st.name.StartsWith("WS_") ? st.name.Substring(3) : st.name;
            string path = $"{WeaponFxDir}/WFX_{name}.asset";
            var set = AssetDatabase.LoadAssetAtPath<WeaponFxSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<WeaponFxSet>();
                Preset(set, st);
                AssetDatabase.CreateAsset(set, path);
                made++;
            }
            st.fx = set;
            EditorUtility.SetDirty(st);
        }
        return made;
    }

    /// 예전 코드 분기와 같은 값.
    static void Preset(WeaponFxSet s, WeaponStats st)
    {
        bool cannon = st.SfxKey == "cannon";
        switch (st.mode)
        {
            case FireMode.Single:
                s.muzzle.scale = cannon ? 1.35f : st.beam ? 1.2f : 1f;
                s.casingScale = cannon ? 1.2f : 1f;
                s.casingPortBack = cannon ? 0.55f : 0.35f;
                break;
            case FireMode.Shotgun:
                s.muzzle.scale = 1.25f;
                s.casingScale = 1.5f;
                break;
            case FireMode.Rocket:      // 발사체 모델이 곧 연출, 착탄은 폭발
                s.muzzle.fxId = null; s.casingScale = 0f; s.hit.fxId = null;
                break;
            case FireMode.Cone:        // 불줄기가 연출. 틱마다 불똥이 튀면 소음
                s.muzzle.fxId = null; s.casingScale = 0f; s.hit.fxId = null;
                break;
            case FireMode.Orbit:
            case FireMode.Companion:   // 드론은 총구 연출 없이 쏜다(FireExternal). 블레이드는 작은 불똥
                s.muzzle.fxId = null; s.casingScale = 0f; s.hit.scale = 0.8f;
                if (st.mode == FireMode.Orbit) s.trail = new WeaponFxSet.Slot(FxIds.OrbitBladeTrail);   // 톱날 꼬리(2026-11-18)
                break;
            case FireMode.HeliCall:    // 헬기 기관포 — 예전 ProtoHeliGunship 상수: 총구 1.8, 착탄 불똥 0.55
                s.muzzle.scale = 1.8f; s.casingScale = 0f; s.hit.scale = 0.55f;
                break;
            default:                   // 폭격 · 수류탄 — 폭발이 연출
                s.muzzle.fxId = null; s.casingScale = 0f; s.hit.fxId = null;
                break;
        }
        if (st.projectileModel != null) { s.muzzle.fxId = null; s.casingScale = 0f; }   // 예전 규칙: 발사체 모델이 있으면 총구 연출 없음
        bool explodes = st.aoeRadius > 0.01f || st.mode == FireMode.Grenade || st.mode == FireMode.Airstrike || st.airstrikeEvery > 0f;
        if (!explodes) s.explode.fxId = null;
        s.note = "'카탈로그·시트 만들기' 가 예전 코드 상수에서 옮겨 만든 값. 자유롭게 고칠 것 — 다시 눌러도 덮어쓰지 않는다.";
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/카탈로그 선택")]
    static void SelectCatalog() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<FxCatalog>(CatalogPath);

    [MenuItem("Tools/좀비 오버런/FX 툴/시트 선택")]
    static void SelectSheet() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<ActionFxSheet>(SheetPath);

    // ── 카탈로그 ───────────────────────────────────────────────────
    static int FillCatalog(FxCatalog cat)
    {
        int added = 0;
        // 코드 그림(프리팹 없음) — 보스 콘티 4컷
        added += Add(cat, "boss_windup_dust",   "세리머니·보스 1컷 준비 — 발밑 먼지",                    builtin: "boss_windup_dust",   life: 1.5f);
        added += Add(cat, "boss_hammer_flames", "보스 1컷 준비 — 망치에 불 (스팬, 소켓 HammerHead)",      builtin: "boss_hammer_flames", life: 0f);
        added += Add(cat, "boss_hammer_trail",  "보스 2컷 휘두르기 — 궤적 (스팬, 소켓 HammerHead)",       builtin: "boss_hammer_trail",  life: 0f);
        added += Add(cat, "boss_impact",        "보스 3·4컷 충돌 — 섬광·화구·링·파편·균열·연기. 흔들림·소리 포함 (Target 앵커, 반경 = 판정)", builtin: "boss_impact", life: 1.5f);
        // 코드 그림 — 공용
        added += Add(cat, "explosion",    "타격감 — 로켓 착탄 폭발. 링 테두리 = 판정 반경(§15)",  builtin: "explosion",    life: 1.2f);
        added += Add(cat, "levelup_beam", "세리머니 — 레벨업 빛기둥",                           builtin: "levelup_beam", life: 2f);
        // 프리팹 예시 — 이미 있는 Resources/VFX 프리팹. 프리팹 경로가 동작하는지 보여 주는 자리
        added += Add(cat, "swap_landing", "타격감 — 캐릭터 교체 착지 먼지 (프리팹 예시)", prefab: Prefab("FX_SwapLanding"), life: 2f);
        added += Add(cat, "flame_jet",    "무기 — 화염 분사 (프리팹 예시, 스팬으로 켜고 끈다)", prefab: Prefab("FX_FlameJet"),    life: 0f);
        added += Add(cat, "burn_body",    "정보 — 화상 중 표시 (프리팹 예시, 스팬)",            prefab: Prefab("FX_BurnBody"),    life: 0f);

        // ── 빈도 높은 타격감 (2026-10-30 프리팹 교체 1차) — 모양은 Art/VFX 의 템플릿 프리팹. 아트가 그 프리팹을 고치거나 다른 프리팹으로 바꾼다 ──
        // 총구 화염만 코드 그림이 이미 있어 기본은 그대로 두고, 템플릿(FX_MuzzleFlash)은 옆에 만들어만 둔다 — 프리팹 슬롯에 끌어 넣으면 바뀐다.
        added += Add(cat, FxIds.MuzzleFlash, "타격감 — 총구 화염. 자리 = 총구, +Z = 총열 방향, scale 1 = 소총(캐논 1.35 · 샷건 1.25 · 필사기 더 큼, big). 캐논은 초당 12발 — 수명 0.1 · 동시 8. 템플릿: Art/VFX/Common/FX_MuzzleFlash",
                     builtin: FxIds.MuzzleFlash, life: 0.1f, pool: 8, maxAlive: 8, onTop: true);
        added += Add(cat, FxIds.HitSpark, "타격감 — 피격 불똥. 자리 = 맞은 몸통(가슴 높이의 맞은 쪽), +Z = 총알이 날아온 쪽(튀는 방향), scale = 덩치(치명타 ×1.5, big). 흰 플래시·소리는 코드(ProtoConfig.HitFlash*). 90마리 물량전 — 동시 20",
                     prefab: Template("FX_HitSpark"), life: 0.4f, pool: 24, maxAlive: 20, previewOffset: new Vector3(0f, 0.95f, 0f), onTop: true);
        added += Add(cat, FxIds.KillBurst, "타격감 — 처치 터짐. 자리 = 몸 가운데, scale = 덩치, big = 보스·엘리트. 사망 애니·소리(zdie)는 코드",
                     prefab: Template("FX_KillBurst"), life: 0.8f, pool: 12, maxAlive: 12, previewOffset: new Vector3(0f, 0.8f, 0f));
        added += Add(cat, FxIds.GemPickup, "보상 — 젬 흡수 순간. 자리 = 젬, scale = 젬 등급(작음 0.8 · 중간 1 · 큼 1.4). 소리(gem 사다리)는 코드",
                     prefab: Template("FX_GemPickup"), life: 0.6f, pool: 12, maxAlive: 12, previewOffset: new Vector3(0f, 0.35f, 0f));
        added += Add(cat, FxIds.ZombieGore, "타격감 — 처치 피 터짐(팩 피 시트 터짐 · 핏방울 줄기 · 가닥 · 안개 · 바닥에 흩어진 핏자국은 몸 조각이 녹을 때 같이 사라진다, 1.4~2.4초). 자리 = 몸 가운데, +Z = 맞은 방향, scale = 덩치. 몸 조각은 코드 그림(ZombieGibs — [무기] 탭 파편 줄). 보스·불타 죽은 적은 안 낸다",
                     prefab: Template("FX_ZombieGore"), life: ZombieGoreLife, pool: 16, maxAlive: 16, previewOffset: new Vector3(0f, 0.8f, 0f));
        added += Add(cat, FxIds.IntroLanding, "세리머니 — 시작 연출 낙하산 착지(2026-11-16): Survivor Ace 보스 낙하 연기 복사본(MFX_Landing_Smoke — 만화풍 연기 뭉치가 터져 고리로 퍼짐 · 빛 · 속도선, 푸른 흰 회색, 0.6초, 임시 — 출시 전 교체). 자리 = 착지 지점(바닥), scale 1 = 캐릭터. 흔들림 · 찌그러짐 · 소리(land)는 ProtoIntro. 비우면 예전 교체 착지 먼지(FX_SwapLanding). 우리 템플릿: Art/VFX/Common/FX_IntroLanding",
                     prefab: IntroLandingPrefab(), life: 1.4f, pool: 1, maxAlive: 1);
        added += Upgrades(cat);

        // ── 무기별 — 루키 탕탕 소총 (2026-11-04, 1호). 공통 ID 와 따로 두는 이유: 소총만 고쳐도 다른 무기는 그대로다.
        //    WFX_Rifle_TangTang 의 슬롯이 이 ID 들을 가리킨다. 모양은 Art/VFX/Weapons/Rifle 의 템플릿 — 아트가 그 프리팹을 고치거나 바꾼다 ──
        added += Add(cat, FxIds.RifleMuzzle, "무기·소총 — 총구: 심·4갈래 별·불줄기·불똥·연기. 자리 = 총구, +Z = 총열, scale 1 = 소총. 연기가 0.5s 남아 수명 0.55. 폴백 = 공통 코드 그림. 템플릿: Art/VFX/Weapons/Rifle/FX_Rifle_Muzzle",
                     prefab: Template("FX_Rifle_Muzzle"), builtin: FxIds.RifleMuzzle, life: 0.55f, pool: 6, maxAlive: 6, previewOffset: new Vector3(0f, 1.1f, 0.45f), onTop: true);
        added += Add(cat, FxIds.RifleHit, "무기·소총 — 피격 불똥 + 먼지 한 뭉치. 자리 = 착탄점, +Z = 총알이 온 쪽, scale = 덩치(치명타 ×1.5). 물량전 — 동시 20. 템플릿: FX_Rifle_Hit",
                     prefab: Template("FX_Rifle_Hit"), life: 0.4f, pool: 24, maxAlive: 20, previewOffset: new Vector3(0f, 0.95f, 0f), onTop: true);
        added += Add(cat, FxIds.RifleKill, "무기·소총 — 처치: 섬광·바닥 링·파편·반짝임, 예광탄 노랑. 자리 = 몸 가운데, scale = 덩치. 템플릿: FX_Rifle_Kill",
                     prefab: Template("FX_Rifle_Kill"), life: 0.8f, pool: 8, maxAlive: 10, previewOffset: new Vector3(0f, 0.8f, 0f));
        // ── 화염방사기 (2026-11-05): 화염 줄기는 ProtoFx.Play 로 내지 않는다 — WFX_Flamethrower.jet 이 이 ID 를 가리키고 ProtoFlamePack 이 프리팹을 총구에 세워 방출을 여닫는다(스팬).
        //    프리팹은 FxPackImport 가 팩 RedFlameThrower 를 복사해 채운다. 루트 스케일은 런타임이 사거리로 맞춘다(루트의 속도 × 수명 기준). ──
        // (flamethrower_jet 항목 — 11-23 지움: 팩 복사본을 안 쓴다. 다시 쓰려면 메뉴 '화염방사기에 MasterStylizedFX 입히기'가 항목을 만든다)

        // ── 세리머니 · 환경 · 탈것 · 필사기 (2026-10-30 프리팹 교체 2차) — 전부 코드 그림 폴백. 프리팹을 넣으면 그걸로 바뀐다 ──
        added += Add(cat, FxIds.EvolveBeam,      "세리머니 — 진화 빛기둥(금색, color 로 넘어온다). 자리 = 캐릭터 발",                   builtin: FxIds.EvolveBeam,      life: 2f);
        added += Add(cat, FxIds.VanHit,          "환경 — 승합차 피격 불똥. 자리 = 맞은 자리(높이 0.6~1.4), +Z = 위",                      builtin: FxIds.VanHit,          life: 0.3f, pool: 8, maxAlive: 8);
        added += Add(cat, FxIds.VanBlast,        "환경 — 승합차 폭발. radius = 판정 반경(링 테두리), big. 흔들림·소리 포함(코드 그림)", builtin: FxIds.VanBlast,        life: 1.5f);
        added += Add(cat, FxIds.VanAppearDust,   "환경 — 승합차 재생성 먼지. 자리 = 차 중심",                                             builtin: FxIds.VanAppearDust,   life: 1.5f);
        added += Add(cat, FxIds.HeliCrashPop,    "환경 — 추락 헬기의 작은 폭발(엔진 피격 1.1 · 잔해 1.6). radius, quiet(흔들림 없음)",    builtin: FxIds.HeliCrashPop,    life: 1.2f, pool: 6, maxAlive: 6);
        added += Add(cat, FxIds.HeliCrashImpact, "환경 — 헬기 추락 충돌. radius = 판정 반경, big. 흔들림(Huge)·소리 포함(코드 그림)",      builtin: FxIds.HeliCrashImpact, life: 1.5f);
        added += Add(cat, FxIds.TankBlast,       "탈것 — 탱크 파괴 폭발. radius = 판정 반경, big. 흔들림(Heavy)은 코드가 따로",             builtin: FxIds.TankBlast,       life: 1.2f);
        added += Add(cat, FxIds.TankDebrisPop,   "탈것 — 탱크 잔해 작은 폭발(1.4m, 2회)",                                                builtin: FxIds.TankDebrisPop,   life: 1.2f, pool: 4, maxAlive: 4);
        // 탱크 대포 착탄(2026-11-17) — Survivor Ace 복사본이 있으면 그것(항상 위에 — 큰 빛 판이 땅에 걸려 잘린다), 폴더를 지웠으면 코드 그림(예전 공용 착탄 폭발과 같은 그림)
        var cannonHit = AssetDatabase.LoadAssetAtPath<GameObject>(TankCannonHitSA);
        added += Add(cat, FxIds.TankCannonHit,   "[임시·Survivor Ace 추출본 — 출시 전 교체] 탈것 — 탱크 대포 착탄 폭발(프리팹 FX_Death_XiaoGuai_002 = 원작 이펙트 표 210008 일반 폭발). 자리 = 포탄이 터진 점, scale = ProtoTank.CannonHitFxScale, radius 는 코드 그림만 쓴다. 폭발음 · 풀은 코드(ProtoExplosion.Aftermath), 흔들림 없음(quiet). 비우면 예전 공용 착탄 폭발 코드 그림",
                     prefab: cannonHit, builtin: FxIds.TankCannonHit, life: 0.9f, pool: 4, maxAlive: 6, previewOffset: new Vector3(0f, 0.9f, 0f), onTop: cannonHit != null);
        added += Add(cat, FxIds.UltCombust,      "필사기 — 파이로 연소. 화상 적마다 radius 폭발, 첫 발만 흔들림(quiet 로 넘어온다)",     builtin: FxIds.UltCombust,      life: 1.2f, pool: 8, maxAlive: 8);
        // 물고기 젬 반짝이 · 흡수 꼬리(2026-11-18 사용자 "탕탕에서 어울리는 이펙트 가져와서") — 탕탕 복사본. 폴더를 지웠으면 빈 항목(아무것도 안 나온다)
        added += Add(cat, FxIds.GemSparkle, "[임시·탕탕 추출본 — 출시 전 교체] 보상 — 떨어진 물고기 젬 둘레 반짝이(스팬). 프리팹 FX_FishSparkle = 탕탕 드롭 아이템 연출 FX_DiaoLuo_Color 의 별 반짝임만(깜빡이며 살짝 솟는 별 — 11-18 오후 크기 ×0.6 · 노랑~금 두 갈래 색). ProtoGemPool 이 떨어질 때 켜고 매 프레임 물고기를 따라 옮기며(MoveTo), 자석에 걸리면 Stop. scale = 젬 등급(0.8 · 1 · 1.4). 물고기가 수십 마리라 동시 64",
                     prefab: AssetDatabase.LoadAssetAtPath<GameObject>(GemSparkleTT), life: 1f, pool: 40, maxAlive: 64, previewOffset: new Vector3(0f, 0.35f, 0f));
        added += Add(cat, FxIds.GemTrail, "[임시·탕탕 추출본 — 출시 전 교체] 보상 — 자석에 끌려오는 물고기 젬 꼬리(스팬). 프리팹 FX_FishTrail = 탕탕 얼음 불사조 화살 Bullet_WhistleArrowIcePhoenix 의 꼬리 띠(가산, 0.3초) + 물방울 점 — 11-18 오후 노란 텍스처 복사본(_Gold, 색상 210°→47°). 띠는 머리(물고기 쪽)를 가늘고 투명하게 시작해 물고기 뒤에서 퍼진다(잘린 단면 없앰). ProtoGemPool 이 자석에 걸릴 때 켜고(꼬리 비움 · 굵기 = 물고기 키 × trailWidthPerFishHeight · 길이 = trailTime 으로 덮어씀) 따라 옮기다 먹히면 방출을 끄고 Stop(detach). scale = 젬 등급",
                     prefab: AssetDatabase.LoadAssetAtPath<GameObject>(GemTrailTT), life: 1f, pool: 24, maxAlive: 48, previewOffset: new Vector3(0f, 0.7f, 0f));
        // 회전 블레이드 톱날 꼬리(2026-11-18 사용자 "회전 톱니에 궤적 이펙트") — 우리 그림. 프리팹이 없으면 OrbitBladeTrailSetup 으로 굽는다
        var orbitTrail = AssetDatabase.LoadAssetAtPath<GameObject>(OrbitBladeTrailSetup.PrefabPath);
        if (orbitTrail == null && cat.Find(FxIds.OrbitBladeTrail) == null) orbitTrail = OrbitBladeTrailSetup.Build();
        added += Add(cat, FxIds.OrbitBladeTrail, "무기·회전 블레이드 — 톱날 꼬리(스팬). 프리팹 FX_OrbitBlade_Trail = 하늘색 빛 띠 + 흰 심 띠 + 쇳불똥(OrbitBladeTrailSetup). ProtoSupportGear 가 톱날마다 피벗(+Z = 궤도 접선)에 켜고 띠 층(Custom 공간)을 궤도 루트에 묶는다(BindSimulationSpace) — 플레이어가 달려도 원을 그린다. scale = WFX_OrbitBlade.trail.scale",
                     prefab: orbitTrail, life: 0.5f, pool: 6, maxAlive: 12, previewOffset: new Vector3(0f, 0.8f, 0f));
        return added;
    }

    /// 물고기 젬 반짝이 · 꼬리 — 탕탕 복사본(2026-11-18, Sujin/S_VFX/Shared/TangTang/README.md). 카탈로그에 항목이 없을 때만 채운다.
    const string GemSparkleTT = "Assets/_Game/Art/Sujin/S_VFX/Pickup/FX_FishSparkle.prefab";   // 11-23 옮김(옛 _Reference/TangTang/Prefabs/Pickup)
    const string GemTrailTT = "Assets/_Game/Art/Sujin/S_VFX/Pickup/FX_FishTrail.prefab";

    /// 바닥 핏자국이 조각 수명 상한(ZombieGibs.Life 1.8 × 1.2 = 2.16초) + 떨어지는 시간차 0.25초까지 남는다 — 그보다 짧으면 풀로 돌아가며 뚝 끊긴다.
    /// 5.2(11-05, 바닥 피 4.8초) → 2.6(11-06 사용자 "파편 사라질 때 같이"). 템플릿의 GroundLife() 와 짝.
    const float ZombieGoreLife = 2.6f;

    /// 낙하산 착지 그림 — Survivor Ace 복사본(2026-11-16 사용자 "Survivor Ace 에서 가져와서 쓸 수 있는 걸로", Source~/import_landing.py)이 있으면 그것,
    /// 없으면(폴더를 지웠으면) 우리 템플릿 FX_IntroLanding.
    const string IntroLandingSA = "Assets/_Game/Art/Sujin/S_VFX/Character/MFX_Landing_Smoke.prefab";   // 11-23 옮김(옛 _Reference/SurvivorAce/Prefabs/Appear)
    /// 탱크 대포 착탄 그림 — Survivor Ace FX_Death_XiaoGuai_002 원본 복사(2026-11-17, Source~/import_xiaoguai.py). 카탈로그에 항목이 없을 때만 채운다.
    const string TankCannonHitSA = "Assets/_Game/Art/Sujin/S_VFX/Explosion/FX_Death_XiaoGuai_002.prefab";   // 11-23 옮김
    static GameObject IntroLandingPrefab()
    {
        var sa = AssetDatabase.LoadAssetAtPath<GameObject>(IntroLandingSA);
        return sa != null ? sa : Template("FX_IntroLanding");
    }

    /// 이미 있는 항목이 코드보다 뒤처졌으면 맞춘다(수명이 길어진 효과). 돌려주는 값 = 고친 수.
    static int Upgrades(FxCatalog cat)
    {
        int n = 0;
        if (cat.upgrade < 3)
        {
            // 3단계(2026-11-16): 낙하산 착지가 우리 템플릿으로 먼저 채워졌으면 Survivor Ace 복사본으로 한 번 바꾼다(사용자가 SA 것을 골랐다). 창에서 다시 바꾸면 그대로 둔다
            var land = cat.Find(FxIds.IntroLanding);
            var sa = AssetDatabase.LoadAssetAtPath<GameObject>(IntroLandingSA);
            if (land != null && sa != null && (land.prefab == null || land.prefab.name == "FX_IntroLanding"))
            {
                land.prefab = sa; n++;
                Debug.Log("[FX 툴] 카탈로그 손질 3단계 — 낙하산 착지(intro_landing)를 Survivor Ace 복사본 MFX_Landing_Smoke 로");
            }
            if (cat.upgrade >= 2) { cat.upgrade = 3; n++; }   // 2단계 전(새 카탈로그)이면 아래가 2 로 올린 뒤 다음 로드에 3
        }
        var e = cat.Find(FxIds.ZombieGore);
        if (e != null && e.lifetime < ZombieGoreLife) { e.lifetime = ZombieGoreLife; n++; }
        if (cat.upgrade < 2 && e != null && e.lifetime > ZombieGoreLife)
        {
            // 2단계(2026-11-06): 처치 피 수명 5.2 → 2.6 — 바닥 피를 몸 조각과 같이 거둔다(사용자 "파편 사라질 때 같이"). 한 번만 — 창에서 늘리면 그대로 둔다
            e.lifetime = ZombieGoreLife; n++;
            Debug.Log($"[FX 툴] 카탈로그 손질 2단계 — 처치 피(zombie_gore) 수명을 {ZombieGoreLife}초로 (바닥 피가 몸 조각과 같이 사라진다)");
        }
        if (cat.upgrade < 2) { cat.upgrade = 2; n++; }
        if (cat.upgrade < 1)
        {
            // 1단계(2026-11-05): 총구 화염·피격 불똥은 항상 위에(사용자 "발사 이펙트가 총보다 밑에 — 총 위로, 피격도 적 위로"). 한 번만 — 창에서 끄면 그대로 둔다
            foreach (var id in OnTopIds) { var o = cat.Find(id); if (o != null && !o.onTop) { o.onTop = true; n++; } }
            cat.upgrade = 1; n++;
            Debug.Log("[FX 툴] 카탈로그 손질 1단계 — 총구 화염·피격 불똥 4종을 \"항상 위에\"로 (오버레이 카메라)");
        }
        return n;
    }

    /// 항상 위에 그릴 기본 항목 — 총구 화염 · 피격 불똥(공통 + 소총).
    static readonly string[] OnTopIds = { FxIds.MuzzleFlash, FxIds.HitSpark, FxIds.RifleMuzzle, FxIds.RifleHit };
    const int OverlayLayerSlot = 30;   // 비어 있으면 여기, 아니면 31→8 중 빈 자리

    /// 오버레이 레이어(FxOverlay) 를 TagManager 에 만든다 — 런타임 FxOverlay 가 이름으로 찾는다. 있으면 아무것도 안 한다.
    static bool EnsureOverlayLayer()
    {
        if (LayerMask.NameToLayer(Proto.FxOverlay.LayerName) >= 0) return false;
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) { Debug.LogWarning("[FX 툴] TagManager 를 못 열었다 — FxOverlay 레이어를 손으로 만들 것"); return false; }
        var so = new SerializedObject(assets[0]);
        var layers = so.FindProperty("layers");
        if (layers == null || layers.arraySize < 32) return false;
        int idx = -1;
        if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(OverlayLayerSlot).stringValue)) idx = OverlayLayerSlot;
        else for (int i = 31; i >= 8; i--) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { idx = i; break; }
        if (idx < 0) { Debug.LogWarning("[FX 툴] 빈 레이어가 없다 — FxOverlay 레이어를 못 만든다(총구·피격 항상 위에 그리기 불가)"); return false; }
        layers.GetArrayElementAtIndex(idx).stringValue = Proto.FxOverlay.LayerName;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log($"[FX 툴] 레이어 {idx} = {Proto.FxOverlay.LayerName} 를 만들었다 — 총구 화염·피격 불똥을 항상 위에 그리는 오버레이 카메라용");
        return true;
    }

    static int Add(FxCatalog cat, string id, string note, string builtin = null, GameObject prefab = null, float life = 2f,
                   FxCatalog.Clock clock = FxCatalog.Clock.Game, int pool = 4, int maxAlive = 0, Vector3 previewOffset = default, bool onTop = false)
    {
        if (cat.Find(id) != null) return 0;
        cat.entries.Add(new FxCatalog.Entry
        {
            id = id, note = note, builtin = builtin, prefab = prefab, lifetime = life, clock = clock,
            poolSize = pool, maxAlive = maxAlive, previewOffset = previewOffset, onTop = onTop,
        });
        return 1;
    }

    static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(VfxDir + name + ".prefab");
    static GameObject Template(string name) => FxTemplatePrefabs.Ensure(name);

    // ── 시트 ───────────────────────────────────────────────────────
    static int FillSheet(ActionFxSheet sheet)
    {
        int added = 0;
        var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        if (sheet.defaultPreviewPrefab == null) sheet.defaultPreviewPrefab = boss;

        // 기본 시각은 브레인 인스펙터 값(프리팹에 저장된 것). 프리팹이 없으면 클래스 기본값
        ProtoBossBrain b = boss != null ? boss.GetComponent<ProtoBossBrain>() : null;
        GameObject tmp = null;
        if (b == null) { tmp = new GameObject("tmp") { hideFlags = HideFlags.HideAndDontSave }; b = tmp.AddComponent<ProtoBossBrain>(); }
        try
        {
            if (sheet.Find(ProtoBossBrain.SlamKey) == null)
            {
                var a = new ActionFxSheet.Entry
                {
                    key = ProtoBossBrain.SlamKey,
                    clip = Clip("Standing Melee Attack Downward"),
                    previewPrefab = boss,
                    previewTarget = new Vector3(b.slamOffset.x, 0f, b.slamOffset.y),
                    previewRadius = b.slamRadius,
                    previewScale = FinalBossScale(),
                };
                a.events.AddRange(BossCut(b.slamWindupAt, b.slamSwingAt, b.slamImpactAt, b.slamEndAt));
                sheet.actions.Add(a); added++;
            }
            if (sheet.Find(ProtoBossBrain.JumpKey) == null)
            {
                var a = new ActionFxSheet.Entry
                {
                    key = ProtoBossBrain.JumpKey,
                    clip = Clip("Standing Melee Run Jump Attack"),
                    previewPrefab = boss,
                    // 미리보기는 몸이 제자리라(전진은 코드가 한다) 착지점을 대략 전진 거리만큼 앞에 둔다
                    previewTarget = new Vector3(b.jumpHeadOffset.x, 0f, b.jumpHeadOffset.y + 4f),
                    previewRadius = b.jumpRadius,
                    previewScale = FinalBossScale(),
                };
                a.events.AddRange(BossCut(b.jumpWindupAt, b.jumpSwingAt, b.jumpImpactAt, b.jumpEndAt));
                sheet.actions.Add(a); added++;
            }
        }
        finally { if (tmp != null) Object.DestroyImmediate(tmp); }
        return added;
    }

    /// 최종 보스 덩치 — 적 테이블(E_FinalBoss.scale). 테이블이 없으면 예전 상수 1.3.
    static float FinalBossScale()
    {
        var t = Resources.Load<EnemyTable>(EnemyTable.ResourcePath);
        return t != null && t.finalBoss != null ? t.finalBoss.scale : 1.3f;
    }

    /// 콘티 4컷을 이벤트로. ProtoBossBrain.Phases() 가 하던 것과 같은 시각·자리.
    static ActionFxSheet.FxEvent[] BossCut(float windupAt, float swingAt, float impactAt, float endAt)
    {
        return new[]
        {
            new ActionFxSheet.FxEvent { name = "발밑 먼지",   kind = ActionFxSheet.Kind.OneShot, time = windupAt,
                                        fxId = "boss_windup_dust",   anchor = ActionFxSheet.Anchor.Root, follow = false },
            new ActionFxSheet.FxEvent { name = "망치 불",     kind = ActionFxSheet.Kind.Span, time = windupAt, endTime = impactAt,
                                        fxId = "boss_hammer_flames", anchor = ActionFxSheet.Anchor.Socket, socket = "HammerHead", follow = true },
            new ActionFxSheet.FxEvent { name = "휘두름 궤적", kind = ActionFxSheet.Kind.Span, time = swingAt, endTime = impactAt + 0.12f,
                                        fxId = "boss_hammer_trail",  anchor = ActionFxSheet.Anchor.Socket, socket = "HammerHead", follow = true },
            // 충돌 = 피해 판정 시각. 흔들림·소리는 boss_impact 코드 그림 안에 있어 여기선 비운다
            new ActionFxSheet.FxEvent { name = "충돌",        kind = ActionFxSheet.Kind.OneShot, time = impactAt,
                                        fxId = "boss_impact",        anchor = ActionFxSheet.Anchor.Target, follow = false, marker = "impact" },
            new ActionFxSheet.FxEvent { name = "동작 끝",     kind = ActionFxSheet.Kind.Marker, time = endAt, marker = "end" },
        };
    }

    static AnimationClip Clip(string fbxName)
    {
        string path = BossAnimDir + fbxName + ".fbx";
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"));
        if (clip == null) Debug.LogWarning($"[FX 툴] 클립을 못 찾았다: {path} — 시트의 clip 을 직접 지정할 것");
        return clip;
    }

    // ── 공통 ───────────────────────────────────────────────────────
    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a != null) return a;
        EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        a = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }
}
