using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 효과 <b>템플릿 프리팹</b>을 코드로 굽는다 — 공통(총구 화염 · 피격 불똥 · 처치 터짐 · 젬 흡수)과 무기별(루키 소총 …).
/// 아트가 손대기 전의 출발점이다: 자리·방향·크기 규약(+Z = 방향, scale 1 = 기본 덩치, Hierarchy 스케일)이 맞춰져 있고,
/// 머티리얼은 기존 화염 분사 머티리얼(URP Particles Unlit)을 복제해 쓴다. 텍스처는 부드러운 점 · 링 · 4갈래 별 · 연기 뭉치 네 장뿐이다.
///
/// <b>있는 것은 건드리지 않는다</b> — 아트가 프리팹을 고친 뒤 메뉴를 다시 눌러도 그대로다. 처음부터 다시 받으려면 파일을 지우고 다시 누른다.
/// 생성물: 공통은 Assets/_Game/Art/VFX/Common/FX_*.prefab, 무기별은 Art/VFX/Weapons/&lt;무기&gt;/FX_&lt;무기&gt;_*.prefab.
/// 머티리얼(M_Fx_*)·텍스처(T_Fx_*)는 Art/VFX/Common 에 모아 두고 전부가 같이 쓴다(2026-11-20 폴더 정리 — 예전엔 Art/VFX 바로 아래).
/// 셰이더는 ZombieOverrun/FX/Particle(<see cref="FxShaderUnify"/>) — URP Particles/Unlit 과 속성 이름이 같아 아래 규칙이 그대로 먹는다.
///
/// 새 무기 세트를 붙이는 법: <see cref="Templates"/> 에 (이름, 폴더, 조립 함수) 한 줄 + 조립 함수 하나.
/// 카탈로그 항목은 FxAssetsSetup.FillCatalog 에, 무기가 그것을 쓰게 하는 것은 WFX_* 에셋의 fxId.
/// </summary>
public static class FxTemplatePrefabs
{
    public const string Root = "Assets/_Game/Art/VFX";
    public const string Dir = Root + "/Common";
    public const string WeaponDir = Root + "/Weapons";
    /// 2026-11-23 사용자 작업 폴더 — 11월 이후 이펙트는 종류별로 여기(템플릿 프리팹도). 머티리얼 · 텍스처(M_Fx_* · T_Fx_*)는 그대로 Dir(Common).
    public const string SVfx = "Assets/_Game/Art/Sujin/S_VFX";
    const string SourceMat = "Assets/_Game/Resources/VFX/M_FlameJet_Tinted.mat";   // FX/Particle(예전 URP Particles Unlit) · 가산 · 큐 3000
    const int GlowQueue = 3100;    // 밤 어둠 막(2900) 뒤 — 어둠 속에서 빛나 보이게 (ProtoGunFx 와 같은 규칙)
    const int SmokeQueue = 2850;   // 연기·먼지는 빛나지 않는다 — 밤 어둠 막(2900) **앞**에 그려 밤이면 같이 어두워지게(3000 이면 밤에 회색으로 떠 보인다)
    // 피(M_Fx_Blood*)는 처치 공통 폭발(fx_zombie_death, Survivor Ace 머티리얼 큐 3000) **뒤**에 — 같은 자리에서 같이 터질 때
    // 밝은 불 밑에 깔려 피가 안 보였다(2026-11-13 사용자 "피 터지는 것도 같이 터지게"). 그 전부터 피 머티리얼은 3000 이었다
    const int BloodQueue = 3010;

    /// 이 파일이 만드는 머티리얼. 설정 규칙(Harden)을 적용할 대상
    static readonly string[] GeneratedMats = { "M_Fx_Glow", "M_Fx_Ring", "M_Fx_Star4", "M_Fx_Smoke", "M_Fx_Blood", "M_Fx_BloodDrop", "M_Fx_BloodMist", "M_Fx_BloodSheet", "M_Fx_Debris", "M_Fx_DustRing" };
    static readonly string[] AlphaMats = { "M_Fx_Smoke", "M_Fx_Blood", "M_Fx_BloodDrop", "M_Fx_BloodMist", "M_Fx_BloodSheet", "M_Fx_Debris", "M_Fx_DustRing" };   // 피·먼지·잔돌은 빛나지 않는다 — 알파 블렌드, 밤 어둠 막 앞 큐

    /// 조립 함수가 바뀌어 옛 프리팹에 없는 자식이 생긴 템플릿 — (이름, 표식 자식). 에디터가 뜰 때 FxAssetsSetup.AutoCreate 가 보고 대화상자 없이 덮어쓴다.
    static readonly (string name, string marker)[] Outdated = { ("FX_ZombieGore", "_v6"), ("FX_IntroLanding", "_v1") };   // 조립을 고치면 표식 이름을 올리고(_v4 …) 조립 함수의 Child 도 같이

    /// 표식 자식이 없는 옛 템플릿을 코드 값으로 다시 굽는다. 돌려주는 값 = 다시 구운 수.
    public static int RebuildOutdated()
    {
        int n = 0;
        foreach (var (name, marker) in Outdated)
        {
            string path = PathOf(name);
            var prefab = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            if (prefab == null || prefab.transform.Find(marker) != null) continue;
            if (Build(name, overwrite: true, out _) != null) { n++; Debug.Log($"[FX 툴] 템플릿 {name} 이 옛 모양이라 다시 구웠다(표식 자식 {marker} 없음)"); }
        }
        return n;
    }

    /// 가산(빛나는) 머티리얼의 _BaseColor 배율. 입자 색은 8비트라 1 을 넘길 수 없어 블룸 문턱(Field_Look: threshold 1)을 못 넘는다 —
    /// 밝기는 머티리얼이 들어야 한다. 1.6 이면 심·섬광의 중심만 문턱을 넘어 살짝 번지고 가장자리는 그대로다(옛 화염 복제본은 2.2 + 주황 발광 4.2 였다).
    const float GlowBoost = 1.6f;

    /// 카메라(피치 58° · 17.8m · 세로 FOV 42°, 1080p)에서 1m ≈ 79px. 탄피(7cm ≈ 5.5px)가 "보이는 하한"이라 적혀 있다(ProtoGunFx) —
    /// 불똥·파편의 폭은 이 아래로 내리지 않는다. 그보다 얇으면 2~3px 선이 되어 안 보인다.
    const float MinSpark = 0.06f;

    struct Template { public string name, dir; public Action<GameObject> build; }

    /// 템플릿 이름 → 저장 폴더 · 조립 함수. 메뉴는 이 순서로 만든다.
    static readonly Template[] Templates =
    {
        // ── 공통 — 전용 세트가 없는 무기가 쓰는 기본 모양 ──
        new Template { name = "FX_MuzzleFlash", dir = Dir, build = BuildMuzzleFlash },
        new Template { name = "FX_HitSpark",    dir = SVfx + "/Hit", build = BuildHitSpark },
        new Template { name = "FX_KillBurst",   dir = SVfx + "/Hit", build = BuildKillBurst },
        new Template { name = "FX_GemPickup",   dir = SVfx + "/Pickup", build = BuildGemPickup },
        new Template { name = "FX_ZombieGore",  dir = SVfx + "/Hit", build = BuildZombieGore },   // 처치 피 터짐 (2026-11-05)
        new Template { name = "FX_IntroLanding", dir = SVfx + "/_Unused", build = BuildIntroLanding }, // 시작 연출 낙하산 착지 (2026-11-16)

        // ── 루키 탕탕 소총 (2026-11-04, 무기별 세트 1호) — 예광탄 노랑(ProtoGunFx.TracerColor) 계열로 통일 ──
        new Template { name = "FX_Rifle_Muzzle", dir = SVfx + "/_Unused/Rifle_Template", build = BuildRifleMuzzle },
        new Template { name = "FX_Rifle_Hit",    dir = SVfx + "/_Unused/Rifle_Template", build = BuildRifleHit },
        new Template { name = "FX_Rifle_Kill",   dir = SVfx + "/_Unused/Rifle_Template", build = BuildRifleKill },
    };

    /// 템플릿 이름 전부(메뉴 순서).
    public static string[] All => Array.ConvertAll(Templates, t => t.name);

    /// 이 이름의 템플릿이 저장될(된) 경로. 모르는 이름이면 null.
    public static string PathOf(string name)
    {
        int k = Array.FindIndex(Templates, t => t.name == name);
        return k < 0 ? null : $"{Templates[k].dir}/{name}.prefab";
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/템플릿 프리팹 만들기 (빠진 것만)")]
    public static void EnsureAll()
    {
        HardenMaterials();   // 이미 있는 머티리얼도 규칙대로(발광 · 플립북 · 블렌드 · 큐)
        int made = 0;
        foreach (var t in Templates) if (Ensure(t.name, out bool created) != null && created) made++;
        AssetDatabase.SaveAssets();
        Debug.Log($"[FX 툴] 템플릿 프리팹 {Templates.Length}개 확인 (새로 만든 것 {made}) — {Dir}, {WeaponDir}/*");
    }

    // 다시 굽기 — 코드 값으로 **덮어쓴다**. 같은 경로에 저장하므로 GUID 가 그대로라 카탈로그·WFX 참조는 안 끊긴다.
    // 프리팹을 손으로 고친 게 있으면 사라지므로 묻고 한다. 코드의 값을 손본 뒤 화면에 반영하는 길이다.
    [MenuItem("Tools/좀비 오버런/FX 툴/소총 템플릿 다시 굽기 (덮어쓰기)")]
    public static void RebuildRifle() => Rebuild("FX_Rifle_Muzzle", "FX_Rifle_Hit", "FX_Rifle_Kill");

    [MenuItem("Tools/좀비 오버런/FX 툴/공통 템플릿 다시 굽기 (덮어쓰기)")]
    public static void RebuildCommon() => Rebuild("FX_MuzzleFlash", "FX_HitSpark", "FX_KillBurst", "FX_GemPickup", "FX_ZombieGore", "FX_IntroLanding");

    public static void Rebuild(params string[] names)
    {
        if (!EditorUtility.DisplayDialog("템플릿 다시 굽기",
                $"{string.Join(", ", names)}\n\n코드 값으로 덮어쓴다. 프리팹을 손으로 고친 게 있으면 사라진다(카탈로그·무기 세트의 참조는 그대로). 계속할까?",
                "덮어쓴다", "취소")) return;
        HardenMaterials();
        int n = 0;
        foreach (var name in names) if (Build(name, overwrite: true, out _) != null) n++;
        AssetDatabase.SaveAssets();
        Proto.ProtoFx.Clear();   // 풀에 남은 옛 인스턴스를 버린다 — 플레이 중이면 다음 발부터 새 프리팹
        Debug.Log($"[FX 툴] 템플릿 {n}개 다시 구움 — {string.Join(", ", names)}");
    }

    public static GameObject Ensure(string name) => Ensure(name, out _);
    public static GameObject Ensure(string name, out bool created) => Build(name, overwrite: false, out created);

    static GameObject Build(string name, bool overwrite, out bool created)
    {
        created = false;
        int k = Array.FindIndex(Templates, t => t.name == name);
        if (k < 0) { Debug.LogWarning($"[FX 툴] 모르는 템플릿 이름: {name}"); return null; }
        var tp = Templates[k];
        string path = $"{tp.dir}/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && !overwrite) return existing;
        EnsureFolder(tp.dir);

        var go = new GameObject(name);
        try
        {
            tp.build(go);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);   // 있던 자리에 저장 = 덮어쓰기(GUID 유지)
            created = true;
            return prefab;
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    // ── 공통 템플릿 ─────────────────────────────────────────────────
    /// 총구 화염: 카메라를 보는 섬광 한 장 + 총열(+Z)로 뻗는 불줄기. scale 1 = 소총.
    static void BuildMuzzleFlash(GameObject root)
    {
        var flash = MakeSystem(root, "Flash", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Billboard);
        OneShot(flash, duration: 0.2f, life: (0.05f, 0.07f), size: (0.55f, 0.7f), count: (1, 1), space: ParticleSystemSimulationSpace.Local);
        StartColor(flash, new Color(1f, 0.85f, 0.5f, 1f));
        RandomRotation(flash);
        Fade(flash, new Color(1f, 0.95f, 0.8f), new Color(1f, 0.6f, 0.2f), shrink: true);

        var jet = MakeSystem(Child(root, "Jet"), "Jet", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Stretch);
        OneShot(jet, duration: 0.2f, life: (0.06f, 0.1f), size: (0.1f, 0.16f), count: (3, 5), space: ParticleSystemSimulationSpace.World,
                speed: (6f, 10f));
        Cone(jet, angle: 7f, radius: 0.02f);
        Stretch(jet, velocityScale: 0.02f, lengthScale: 3f);
        Fade(jet, Color.white, new Color(1f, 0.55f, 0.15f), shrink: true);
    }

    /// 피격 불똥: 맞은 자리에서 +Z(총알이 온 쪽)로 튀는 작은 불똥 + 짧은 섬광. scale = 덩치(치명타 ×1.5).
    static void BuildHitSpark(GameObject root)
    {
        var sparks = MakeSystem(root, "Sparks", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Stretch);
        OneShot(sparks, duration: 0.4f, life: (0.15f, 0.3f), size: (0.05f, 0.09f), count: (4, 7), space: ParticleSystemSimulationSpace.World,
                speed: (3f, 6f), gravity: 1.2f);
        Cone(sparks, angle: 32f, radius: 0.03f);
        Stretch(sparks, velocityScale: 0.03f, lengthScale: 2f);
        Fade(sparks, new Color(1f, 0.92f, 0.65f), new Color(1f, 0.4f, 0.1f), shrink: true);

        var flash = MakeSystem(Child(root, "Flash"), "Flash", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Billboard);
        OneShot(flash, duration: 0.2f, life: (0.06f, 0.09f), size: (0.3f, 0.4f), count: (1, 1), space: ParticleSystemSimulationSpace.World);
        Fade(flash, new Color(1f, 0.9f, 0.7f, 0.9f), new Color(1f, 0.5f, 0.2f, 0f), shrink: false);
    }

    /// 처치 터짐: 바닥과 평행한 링이 퍼지고, 사방으로 불똥. scale = 덩치. 보스·엘리트는 코드가 big 으로 부르지만 프리팹은 scale 로만 커진다.
    static void BuildKillBurst(GameObject root)
    {
        var ring = MakeSystem(root, "Ring", Additive("M_Fx_Ring", Ring()), ParticleSystemRenderMode.HorizontalBillboard);
        OneShot(ring, duration: 0.5f, life: (0.3f, 0.3f), size: (1.6f, 1.6f), count: (1, 1), space: ParticleSystemSimulationSpace.World);
        StartColor(ring, new Color(1f, 0.6f, 0.3f, 0.9f));
        Grow(ring, from: 0.15f, to: 1f);
        Fade(ring, new Color(1f, 0.8f, 0.5f, 0.9f), new Color(1f, 0.4f, 0.2f, 0f), shrink: false);

        var debris = MakeSystem(Child(root, "Debris"), "Debris", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Stretch);
        OneShot(debris, duration: 0.8f, life: (0.35f, 0.6f), size: (0.08f, 0.14f), count: (8, 12), space: ParticleSystemSimulationSpace.World,
                speed: (2f, 4.5f), gravity: 1.3f);
        Sphere(debris, radius: 0.15f);
        Stretch(debris, velocityScale: 0.04f, lengthScale: 1.5f);
        Fade(debris, new Color(1f, 0.85f, 0.5f), new Color(0.9f, 0.3f, 0.1f), shrink: true);

        var flash = MakeSystem(Child(root, "Flash"), "Flash", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Billboard);
        OneShot(flash, duration: 0.2f, life: (0.1f, 0.12f), size: (0.9f, 1.1f), count: (1, 1), space: ParticleSystemSimulationSpace.World);
        Fade(flash, new Color(1f, 0.9f, 0.7f, 0.8f), new Color(1f, 0.5f, 0.2f, 0f), shrink: false);
    }

    /// 처치 피 터짐(2026-11-05, 같은 날 "더 리얼하게 · 바닥에 흩어졌다 사라지게"로 2판): 루트 = 가슴 높이(0.8 × 덩치), +Z = 맞은 방향.
    /// 터짐은 팩(MasterStylizedFX)의 피 스프라이트 시트 <c>CommonBloodSplash</c>(4×4, 0~11 프레임: 퍼지다 흩어진다)를 복사해(T_MSFX_BloodSplash — 원본은 안 건드린다)
    /// 맞은 쪽으로 2~3장 돌리고, 핏방울 줄기(맞은 쪽 + 사방) · 긴 피 가닥 · 피 안개를 겹친다. 바닥(-0.78)은 시트의 흩어진 프레임(5~9)을 고정 그림으로
    /// 4~6장 흩뿌리고(떨어져 닿는 시간차 0.05~0.25초) 가운데 큰 웅덩이 한 장 — 몸 조각이 녹는 때(<see cref="Proto.ZombieGibs.Life"/> ±20%)에 맞춰 알파로 사라진다(v6, 11-06 사용자
    /// "파편 사라질 때 같이"). 바닥 피는 반투명(<see cref="GroundAlpha"/>) — 뭉친 적이 줄줄이 죽어도 바닥이 덜 지저분하게. 피는 빛나지 않으므로 전부 알파 블렌드.
    /// 팩이 없으면 시트 대신 얼룩 텍스처 한 장(T_Fx_Splat)으로 떨어진다. 몸 조각은 코드 그림(ZombieGibs).
    static void BuildZombieGore(GameObject root)
    {
        Child(root, "_v6");   // 판 표식(RebuildOutdated) — v3: 시트 시작 프레임 정규화 · v4: 바닥 피를 크고 진하게 + 단색 핏자국 겹침(사용자 "여전히 안 보인다") · v5: 검붉게 + 핏자국 수 반으로(사용자 "피 색 더 어둡게, 핏자국 갯수 줄여") · v6: 바닥 피 수명을 몸 조각(ZombieGibs.Life)에 맞추고 옅게(11-06 사용자 "파편 사라질 때 같이, 투명도 빼줘 — 뭉쳐 있으면 지저분")
        var sheetTex = BloodSheet();
        var sheetMat = AlphaBlend("M_Fx_BloodSheet", sheetTex);
        var dropMat = AlphaBlend("M_Fx_BloodDrop", Dot());

        // 터지는 핏덩이 — 시트 애니(퍼지다 흩어진다)를 맞은 쪽으로
        var splash = MakeSystem(root, "Splash", sheetMat, ParticleSystemRenderMode.Billboard);
        OneShot(splash, duration: 0.3f, life: (0.42f, 0.55f), size: (0.9f, 1.3f), count: (2, 3), space: ParticleSystemSimulationSpace.World,
                speed: (0.6f, 1.6f), gravity: 0.6f);
        Cone(splash, angle: 25f, radius: 0.05f);
        Tilt(splash, 30f);
        ColorRange(splash, BloodBright, BloodMid);
        if (_bloodSheetGrid) Sheet(splash, 4, 4, firstFrame: 0, lastFrame: 11, animate: true);
        Fade(splash, Color.white, WithAlpha(Color.white, 0.7f), shrink: false);

        // 핏방울 줄기 — 맞은 쪽으로 쏟아지는 작은 줄
        var jet = MakeSystem(Child(root, "Jet"), "Jet", dropMat, ParticleSystemRenderMode.Stretch);
        OneShot(jet, duration: 0.5f, life: (0.35f, 0.6f), size: (MinSpark, 0.11f), count: (16, 22), space: ParticleSystemSimulationSpace.World,
                speed: (4f, 8f), gravity: 2.5f);
        Cone(jet, angle: 32f, radius: 0.06f);
        Stretch(jet, velocityScale: 0.035f, lengthScale: 2.2f);
        ColorRange(jet, BloodBright, BloodDark);
        Fade(jet, BloodBright, BloodDark, shrink: true);

        // 사방으로 튀는 핏방울
        var spray = MakeSystem(Child(root, "Spray"), "Spray", dropMat, ParticleSystemRenderMode.Stretch);
        OneShot(spray, duration: 0.5f, life: (0.4f, 0.7f), size: (MinSpark, 0.1f), count: (14, 20), space: ParticleSystemSimulationSpace.World,
                speed: (2.5f, 5f), gravity: 2.2f);
        Sphere(spray, radius: 0.12f);
        Stretch(spray, velocityScale: 0.03f, lengthScale: 1.6f);
        ColorRange(spray, BloodBright, BloodDark);
        Fade(spray, BloodBright, BloodDark, shrink: true);

        // 긴 피 가닥 — 몇 가닥 멀리
        var strands = MakeSystem(Child(root, "Strands"), "Strands", dropMat, ParticleSystemRenderMode.Stretch);
        OneShot(strands, duration: 0.4f, life: (0.3f, 0.45f), size: (0.04f, MinSpark), count: (4, 7), space: ParticleSystemSimulationSpace.World,
                speed: (6f, 9f), gravity: 3f);
        Cone(strands, angle: 45f, radius: 0.05f);
        Stretch(strands, velocityScale: 0.07f, lengthScale: 5f);
        ColorRange(strands, BloodBright, BloodDark);
        Fade(strands, BloodBright, BloodDark, shrink: true);

        // 피 안개 — 두 장, 커지며 옅어진다
        var mist = MakeSystem(Child(root, "Mist"), "Mist", AlphaBlend("M_Fx_BloodMist", Smoke()), ParticleSystemRenderMode.Billboard);
        OneShot(mist, duration: 0.5f, life: (0.4f, 0.5f), size: (0.7f, 1f), count: (2, 2), space: ParticleSystemSimulationSpace.World);
        Sphere(mist, radius: 0.15f);
        RandomRotation(mist);
        StartColor(mist, WithAlpha(BloodDark, 0.5f));
        Grow(mist, from: 0.6f, to: 1.4f);
        Fade(mist, WithAlpha(BloodDark, 0.5f), WithAlpha(BloodDark, 0f), shrink: false);

        // 바닥 — 발밑(-0.77, 바닥 겹침 피하려 3cm 위)에 흩어진 핏자국: 시트의 흩어진 프레임을 고정 그림으로, 떨어져 닿는 시간차를 두고, 몸 조각과 같은 때에 사라진다.
        // v4: 시트 칸은 그림이 칸의 반쯤이라 크게(0.6~1.2m), 색은 밝은 빨강(시트 자체가 빨강·검붉음이라 곱하면 진해진다) — 어두운 아스팔트 · 밤 어둠 위에서도 읽히게
        // v6: 수명 3.5~4.5 → 조각 수명(1.8 ±20%)에 맞춤 — 조각은 녹아 없어졌는데 바닥 피만 2~3초 더 남아 있었다. 알파 1 → GroundAlpha.
        var (groundLifeMin, groundLifeMax) = GroundLife();
        var ground = Child(root, "GroundSplats");
        ground.transform.localPosition = new Vector3(0f, -0.77f, 0f);
        var splats = MakeSystem(ground, "GroundSplats", sheetMat, ParticleSystemRenderMode.HorizontalBillboard);
        OneShot(splats, duration: 0.5f, life: (groundLifeMin, groundLifeMax), size: (0.6f, 1.2f), count: (4, 6), space: ParticleSystemSimulationSpace.World);
        Disc(splats, radius: 0.9f);
        Staggered(splats, (0.05f, 2, 3), (0.25f, 2, 3));   // 떨어져 닿는 시간차 — startDelay 는 시스템 단위라 입자마다 못 준다. v5: 8~12 → 4~6
        RandomRotation(splats);
        ColorRange(splats, WithAlpha(GroundRed, GroundAlpha), WithAlpha(BloodBright, GroundAlpha));
        if (_bloodSheetGrid) Sheet(splats, 4, 4, firstFrame: 5, lastFrame: 9, animate: false);
        Fade(splats, Color.white, WithAlpha(Color.white, 0f), shrink: false);

        // 바닥 — 단색 핏자국(얼룩 텍스처 한 장짜리, 시트와 상관없이 반드시 보이는 것) 2~3장을 겹친다(v5: 5~7 → 2~3). v6: 알파 0.95 → GroundAlpha
        var stains = MakeSystem(Child(ground, "GroundStains"), "GroundStains", AlphaBlend("M_Fx_Blood", Splat()), ParticleSystemRenderMode.HorizontalBillboard);
        OneShot(stains, duration: 0.4f, life: (groundLifeMin, groundLifeMax), size: (0.45f, 0.8f), count: (2, 3), space: ParticleSystemSimulationSpace.World);
        Disc(stains, radius: 0.7f);
        Staggered(stains, (0.05f, 1, 2), (0.25f, 1, 1));
        RandomRotation(stains);
        ColorRange(stains, WithAlpha(BloodMid, GroundAlpha), WithAlpha(BloodBright, GroundAlpha));
        Fade(stains, Color.white, WithAlpha(Color.white, 0f), shrink: false);

        // 바닥 — 가운데 큰 웅덩이(얼룩 한 장짜리), 천천히 퍼지고 조각이 다 녹을 때쯤 사라진다(수명 상한 쪽 — 마지막까지 남는 건 웅덩이 하나). v6: 알파 0.95 → GroundAlpha
        var pool = MakeSystem(Child(ground, "Pool"), "Pool", AlphaBlend("M_Fx_Blood", Splat()), ParticleSystemRenderMode.HorizontalBillboard);
        OneShot(pool, duration: 0.2f, life: (Mathf.Lerp(groundLifeMin, groundLifeMax, 0.5f), groundLifeMax), size: (1.2f, 1.5f), count: (1, 1), space: ParticleSystemSimulationSpace.World);
        RandomRotation(pool);
        StartColor(pool, WithAlpha(BloodMid, GroundAlpha));
        Grow(pool, from: 0.6f, to: 1f);
        Fade(pool, Color.white, WithAlpha(Color.white, 0f), shrink: false);
    }

    /// 바닥 피(핏자국·단색 얼룩·웅덩이)의 불투명도(v6, 11-06 사용자 "투명도 빼줘 — 몹들이 뭉쳐 있을 때 피가 터져 있으니 지저분"). 이전 0.95~1.
    /// 겹친 자국은 알파 블렌드라 겹칠수록 진해지므로 한 장은 이만큼만 — 뭉친 적 여럿이 죽어도 바닥이 새까맣게 덮이지 않는다.
    const float GroundAlpha = 0.6f;

    /// 바닥 피 입자의 수명 범위 = 몸 조각(<see cref="Proto.ZombieGibs"/>)의 수명 범위(Life ±20%) — 조각이 녹는 때와 같이 옅어져 사라진다.
    /// Fade 는 수명의 60% 부터 옅어지니 조각이 녹기 시작하는 때(마지막 DissolveTime)와 겹친다. 조각 수명(Life)을 코드에서 바꾸면 템플릿을 다시 구워야 따라온다.
    static (float, float) GroundLife() => (Proto.ZombieGibs.Life * 0.8f, Proto.ZombieGibs.Life * 1.2f);

    // 피 색 — 검붉게(v5, 사용자 "좀 더 어둡게, 적붉은색"). 시트(밝은 빨강 1,0.2,0.2 · 가장자리 0.6,0.1,0.1)와 얼룩(흰색)에 곱해지므로 입자 색이 곧 최종 밝기의 상한.
    static readonly Color BloodBright = new Color(0.40f, 0.025f, 0.03f, 1f);   // v4: 0.62, 0.04, 0.05
    static readonly Color BloodMid    = new Color(0.30f, 0.018f, 0.025f, 1f);  // v4: 0.5, 0.03, 0.04
    static readonly Color GroundRed   = new Color(0.50f, 0.04f, 0.05f, 1f);    // v4: 0.95, 0.12, 0.12 — 시트에 곱해 바닥에서 읽히는 밝기
    static readonly Color BloodDark   = new Color(0.20f, 0.01f, 0.015f, 1f);   // v4: 0.36, 0.02, 0.03

    // ── 팩 텍스처 복사 ─────────────────────────────────────────────
    const string PackBloodSheet = "Assets/Soo_VFX/MasterStylizedFX/Textures/Common/CommonBloodSplash.png";   // 11-12: 팩이 Soo_VFX 아래로 옮겨졌다
    static bool _bloodSheetGrid;

    /// 팩의 피 스프라이트 시트(4×4) — 복사본 T_MSFX_BloodSplash. 팩이 없으면 얼룩 한 장(T_Fx_Splat)으로 떨어지고 _bloodSheetGrid 가 false.
    static Texture2D BloodSheet()
    {
        var t = PackTex(PackBloodSheet, "T_MSFX_BloodSplash");
        _bloodSheetGrid = t != null;
        return t != null ? t : Splat();
    }

    /// 팩(MasterStylizedFX) 텍스처를 우리 폴더로 복사해 쓴다(원본은 안 건드린다 — 사용자 규칙 "복사해서 이름 바꿔"). 복사본이 있으면 그것. 팩이 없으면 null.
    static Texture2D PackTex(string src, string newName)
    {
        string dst = $"{Dir}/{newName}{Path.GetExtension(src)}";
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        if (t != null) return t;
        if (!File.Exists(src)) { Debug.LogWarning($"[FX 툴] 팩 텍스처가 없다 — 대신 만든 텍스처를 쓴다: {src}"); return null; }
        EnsureFolder(Dir);
        if (!AssetDatabase.CopyAsset(src, dst)) { Debug.LogWarning($"[FX 툴] 팩 텍스처 복사 실패: {src}"); return null; }
        var imp = AssetImporter.GetAtPath(dst) as TextureImporter;
        if (imp != null)
        {
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            if (imp.maxTextureSize > 1024) imp.maxTextureSize = 1024;   // 2048 은 피 한 장에 과하다
            imp.SaveAndReimport();
        }
        Debug.Log($"[FX 툴] 팩 텍스처 복사 {src} → {dst}");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
    }

    /// 젬 흡수: 위로 솟는 반짝임 + 짧은 섬광. scale = 젬 등급(0.8 · 1 · 1.4).
    static void BuildGemPickup(GameObject root)
    {
        var sparkle = MakeSystem(root, "Sparkle", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Billboard);
        OneShot(sparkle, duration: 0.6f, life: (0.3f, 0.45f), size: (0.06f, 0.11f), count: (6, 9), space: ParticleSystemSimulationSpace.World,
                speed: (1f, 2.2f), gravity: -0.3f);
        Cone(sparkle, angle: 28f, radius: 0.08f, up: true);
        Fade(sparkle, new Color(0.65f, 0.95f, 1f), Color.white, shrink: true);

        var flash = MakeSystem(Child(root, "Flash"), "Flash", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Billboard);
        OneShot(flash, duration: 0.2f, life: (0.08f, 0.1f), size: (0.35f, 0.45f), count: (1, 1), space: ParticleSystemSimulationSpace.World);
        Fade(flash, new Color(0.8f, 1f, 1f, 0.9f), new Color(0.4f, 0.8f, 1f, 0f), shrink: false);
    }

    // ── 루키 탕탕 소총 ───────────────────────────────────────────────
    // 팔레트: 흰빛 심 → 예광탄 노랑 → 주황 → 잉걸. 총구·피격·처치가 같은 네 색을 써서 "같은 총이 쏜 것"으로 읽힌다.
    // 소총은 0.35 초마다 한 발 — 효과 하나하나는 짧고 작게, 대신 연기·먼지가 조금 남아 발사 리듬을 이어 준다.
    static readonly Color RifleHot    = new Color(1f, 0.97f, 0.85f);   // 가장 뜨거운 심
    static readonly Color RifleYellow = new Color(1f, 0.86f, 0.45f);   // = ProtoGunFx.TracerColor
    static readonly Color RifleOrange = new Color(1f, 0.5f, 0.15f);
    static readonly Color RifleEmber  = new Color(0.9f, 0.25f, 0.06f);
    static readonly Color RifleSmoke  = new Color(0.42f, 0.4f, 0.38f);    // 낮 아스팔트 위에서 '연기'로 읽히는 어두운 회색(밝으면 김처럼 보인다)
    static readonly Color RifleDust   = new Color(0.4f, 0.33f, 0.3f);     // 좀비 몸에서 튀는 먼지 — 살짝 붉은 갈색

    /// 소총 총구: 심(흰빛 점) + 4갈래 별 + 총열(+Z)로 짧게 뻗는 불줄기 + 앞으로 흩어지는 불똥 + 0.5 초 남는 연기.
    /// 자리 = 총구, +Z = 총열, scale 1 = 소총. 섬광은 0.08 초 안에 꺼진다 — 연사 간격보다 짧아야 한 발 한 발이 깜빡인다.
    static void BuildRifleMuzzle(GameObject root)
    {
        // 심 — 총구에 딱 붙은 흰빛 점. 한 발마다 무작위로 돌아간다(매 발 같은 모양이면 도장 찍은 것처럼 보인다).
        // 수명 0.06~0.08: 30fps 폰에서도 두 프레임은 보인다(0.04 면 한 프레임에 났다 사라져 깜빡임이 빠진다)
        var core = MakeSystem(root, "Core", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Billboard);
        OneShot(core, duration: 0.15f, life: (0.06f, 0.08f), size: (0.32f, 0.42f), count: (1, 1), space: ParticleSystemSimulationSpace.Local);
        RandomRotation(core);
        Fade(core, RifleHot, RifleYellow, shrink: true);

        // 별 — 4갈래. 심보다 조금 크고 조금 오래. 가시가 가늘어 "탕" 하는 순간이 또렷하다
        var star = MakeSystem(Child(root, "Star"), "Star", Additive("M_Fx_Star4", Star4()), ParticleSystemRenderMode.Billboard);
        OneShot(star, duration: 0.15f, life: (0.07f, 0.1f), size: (0.6f, 0.8f), count: (1, 1), space: ParticleSystemSimulationSpace.Local);
        RandomRotation(star);
        Fade(star, RifleYellow, RifleOrange, shrink: true);

        // 불줄기 — 총열로 짧게. 공통 총구 화염보다 짧고 가늘다(소총은 캐논이 아니다)
        var jet = MakeSystem(Child(root, "Jet"), "Jet", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Stretch);
        OneShot(jet, duration: 0.15f, life: (0.06f, 0.09f), size: (0.08f, 0.12f), count: (3, 5), space: ParticleSystemSimulationSpace.World,
                speed: (7f, 12f));
        Cone(jet, angle: 5f, radius: 0.015f);
        Stretch(jet, velocityScale: 0.018f, lengthScale: 2.5f);
        Fade(jet, RifleHot, RifleOrange, shrink: true);

        // 불똥 — 총구 앞으로 흩어지며 살짝 떨어진다. 섬광이 꺼진 뒤에도 0.2 초쯤 보여 "쐈다"가 남는다. 폭은 보이는 하한(MinSpark) 이상
        var sparks = MakeSystem(Child(root, "Sparks"), "Sparks", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Stretch);
        OneShot(sparks, duration: 0.3f, life: (0.15f, 0.25f), size: (MinSpark, 0.08f), count: (3, 6), space: ParticleSystemSimulationSpace.World,
                speed: (4f, 8f), gravity: 1.2f);
        Cone(sparks, angle: 16f, radius: 0.02f);
        Stretch(sparks, velocityScale: 0.03f, lengthScale: 1.8f);
        Fade(sparks, RifleYellow, RifleEmber, shrink: true);

        // 연기 — 알파 블렌드 회색 뭉치 2~3개. 앞으로 느리게 밀려나며 살짝 떠오르고(음의 중력) 커지며 옅어진다. 카탈로그 수명(0.55)은 이것에 맞춘다.
        // 빛나지 않는 머티리얼(큐 2850)이라 밤이면 같이 어두워진다
        var smoke = MakeSystem(Child(root, "Smoke"), "Smoke", AlphaBlend("M_Fx_Smoke", Smoke()), ParticleSystemRenderMode.Billboard);
        OneShot(smoke, duration: 0.5f, life: (0.35f, 0.5f), size: (0.16f, 0.22f), count: (2, 3), space: ParticleSystemSimulationSpace.World,
                speed: (1f, 1.6f), gravity: -0.12f);
        Cone(smoke, angle: 14f, radius: 0.03f);
        RandomRotation(smoke);
        Spin(smoke, degreesPerSecond: 40f);
        Grow(smoke, from: 0.6f, to: 1.8f);
        Fade(smoke, WithAlpha(RifleSmoke, 0.3f), WithAlpha(RifleSmoke, 0f), shrink: false);
    }

    /// 소총 피격: 맞은 자리에서 +Z(총알이 온 쪽)로 튀는 불똥 + 작은 섬광 + 먼지 한 뭉치.
    /// 자리 = 착탄점, scale = 덩치(치명타 ×1.5). 흰 피격 플래시·소리는 코드가 몸에 따로 준다 — 여기선 자리 표시만.
    static void BuildRifleHit(GameObject root)
    {
        // 불똥 — 폭은 보이는 하한(MinSpark) 이상. 공통 피격보다 수가 많고 더 빠르고 무겁다(더 "탁" 하고 떨어진다)
        var sparks = MakeSystem(root, "Sparks", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Stretch);
        OneShot(sparks, duration: 0.35f, life: (0.12f, 0.28f), size: (MinSpark, 0.09f), count: (5, 8), space: ParticleSystemSimulationSpace.World,
                speed: (3.5f, 7f), gravity: 1.6f);
        Cone(sparks, angle: 34f, radius: 0.02f);
        Stretch(sparks, velocityScale: 0.03f, lengthScale: 2.2f);
        Fade(sparks, RifleHot, RifleEmber, shrink: true);

        var flash = MakeSystem(Child(root, "Flash"), "Flash", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Billboard);
        OneShot(flash, duration: 0.15f, life: (0.06f, 0.08f), size: (0.22f, 0.3f), count: (1, 1), space: ParticleSystemSimulationSpace.World);
        RandomRotation(flash);
        Fade(flash, WithAlpha(RifleHot, 0.9f), WithAlpha(RifleOrange, 0f), shrink: false);

        // 먼지 — 맞은 자리에서 +Z 로 살짝 밀려나며 흩어진다. 90마리 물량전에서도 한두 장이라 싸다
        var puff = MakeSystem(Child(root, "Puff"), "Puff", AlphaBlend("M_Fx_Smoke", Smoke()), ParticleSystemRenderMode.Billboard);
        OneShot(puff, duration: 0.35f, life: (0.22f, 0.32f), size: (0.12f, 0.16f), count: (1, 2), space: ParticleSystemSimulationSpace.World,
                speed: (0.8f, 1.4f));
        Cone(puff, angle: 30f, radius: 0.02f);
        RandomRotation(puff);
        Grow(puff, from: 0.7f, to: 1.6f);
        Fade(puff, WithAlpha(RifleDust, 0.45f), WithAlpha(RifleDust, 0f), shrink: false);
    }

    /// 소총 처치: 섬광 + 바닥 링 + 사방 파편 + 위로 솟는 반짝임. 자리 = 몸 가운데, scale = 덩치.
    /// 공통 처치 터짐과 뼈대는 같고 색이 예광탄 노랑이다 — "소총으로 잡았다"가 색으로 읽힌다.
    static void BuildRifleKill(GameObject root)
    {
        var flash = MakeSystem(root, "Flash", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Billboard);
        OneShot(flash, duration: 0.2f, life: (0.08f, 0.1f), size: (0.75f, 0.95f), count: (1, 1), space: ParticleSystemSimulationSpace.World);
        Fade(flash, WithAlpha(RifleHot, 0.85f), WithAlpha(RifleYellow, 0f), shrink: false);

        var ring = MakeSystem(Child(root, "Ring"), "Ring", Additive("M_Fx_Ring", Ring()), ParticleSystemRenderMode.HorizontalBillboard);
        OneShot(ring, duration: 0.4f, life: (0.26f, 0.26f), size: (1.3f, 1.3f), count: (1, 1), space: ParticleSystemSimulationSpace.World);
        Grow(ring, from: 0.2f, to: 1f);
        Fade(ring, WithAlpha(RifleYellow, 0.85f), WithAlpha(RifleOrange, 0f), shrink: false);

        var debris = MakeSystem(Child(root, "Debris"), "Debris", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Stretch);
        OneShot(debris, duration: 0.8f, life: (0.3f, 0.55f), size: (0.07f, 0.12f), count: (10, 14), space: ParticleSystemSimulationSpace.World,
                speed: (2.5f, 5f), gravity: 1.5f);
        Sphere(debris, radius: 0.15f);
        Stretch(debris, velocityScale: 0.04f, lengthScale: 1.6f);
        Fade(debris, RifleYellow, RifleEmber, shrink: true);

        // 반짝임 — 위로 느리게 솟는 작은 점. 처치 = 보상이라는 신호(젬 흡수와 같은 결). 폭은 보이는 하한 이상
        var sparkle = MakeSystem(Child(root, "Sparkle"), "Sparkle", Additive("M_Fx_Glow", Dot()), ParticleSystemRenderMode.Billboard);
        OneShot(sparkle, duration: 0.7f, life: (0.3f, 0.5f), size: (MinSpark, 0.09f), count: (6, 9), space: ParticleSystemSimulationSpace.World,
                speed: (1.5f, 3f), gravity: 0.5f);
        Cone(sparkle, angle: 40f, radius: 0.1f, up: true);
        Fade(sparkle, RifleHot, RifleYellow, shrink: true);
    }

    // ── 시작 연출 — 낙하산 착지 (2026-11-16) ───────────────────────────
    // 사용자: "캐릭터가 낙하산 타고 내려올 때 착지 이펙트 만들어줘". 그전엔 캐릭터 교체용 작은 먼지(FX_SwapLanding, Unity Particle Pack)를 같이 썼다.
    // 밤 장면이라 먼지는 빛나지 않는다 — 전부 알파 블렌드(큐 2850, 밤 어둠 막 앞)라 캐릭터 둘레 빛 원 안에서만 읽히고 밖으로 갈수록 같이 어두워진다.
    // 텍스처는 기존 T_Fx_Smoke(동글동글한 혹 — 테두리 선·잔결 없음, 사용자 취향) · T_Fx_Ring · T_Fx_SoftDot 만 쓴다.
    static readonly Color LandDustLight = new Color(0.74f, 0.76f, 0.82f);   // 밤 먼지 — 살짝 푸른 회색(어둠 막·밤 막이 곱해져 화면에선 이보다 어둡다)
    static readonly Color LandDustMid   = new Color(0.58f, 0.60f, 0.67f);
    static readonly Color LandPebble    = new Color(0.56f, 0.55f, 0.59f);   // 잔돌 — 밝은 회색 부스러기(어두운 바닥 위에서 읽히게 — 아스팔트색이면 밤에 안 보인다)

    /// 낙하산 착지: 루트 = 착지 지점(바닥 y 0), 회전 없음, scale 1 = 캐릭터(키 1.7m).
    /// ① 바닥을 따라 사방으로 퍼지는 먼지 고리(끌림으로 빨리 멈추고 커지며 옅어진다, 반경 ≈ 2m)
    /// ② 가운데서 피어오르는 먼지 몇 뭉치 ③ 위로 튀었다 떨어지는 잔돌 ④ 바닥에 납작하게 퍼지는 먼지 충격파 한 번(0.35초).
    /// 흔들림 · 찌그러짐 · 소리(land)는 ProtoIntro.Land 가 낸다.
    static void BuildIntroLanding(GameObject root)
    {
        Child(root, "_v1");   // 판 표식(RebuildOutdated) — 조립을 고치면 _v2 로 올리고 Outdated 목록도 같이
        var smoke = AlphaBlend("M_Fx_Smoke", Smoke());

        // ① 먼지 고리 — 원 둘레에서 바깥으로 고르게(BurstSpread) 터져 끌림으로 멈춘다. 퍼프 가운데를 0.3m 띄워 바닥에 반쯤 묻히지 않게
        var ringGo = Child(root, "DustRing");
        ringGo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        var ring = MakeSystem(ringGo, "DustRing", smoke, ParticleSystemRenderMode.Billboard);
        OneShot(ring, duration: 0.3f, life: (0.75f, 1.1f), size: (0.6f, 1.1f), count: (16, 20), space: ParticleSystemSimulationSpace.World,
                speed: (4.5f, 9f));
        FlatRing(ring, radius: 0.3f, even: true);
        Drag(ring, 1.2f);               // 0.25초에 ≈ 1.1m, 1초에 ≈ 1.9~2.2m (시작 반경 0.3 포함)
        Lift(ring, 0.35f);              // 멈춘 뒤 천천히 떠오른다
        RandomRotation(ring);
        Spin(ring, 50f);
        ColorRange(ring, WithAlpha(LandDustLight, 0.78f), WithAlpha(LandDustMid, 0.7f));
        GrowFast(ring, 0.45f, 1.1f, 1.6f);
        Fade(ring, Color.white, WithAlpha(Color.white, 0f), shrink: false);

        // ② 가운데 피어오르는 먼지 — 발밑에서 위·옆으로 조금, 크게 부풀며 옅어진다
        var billowGo = Child(root, "Billow");
        billowGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        var billow = MakeSystem(billowGo, "Billow", smoke, ParticleSystemRenderMode.Billboard);
        OneShot(billow, duration: 0.3f, life: (0.8f, 1.1f), size: (0.9f, 1.25f), count: (4, 5), space: ParticleSystemSimulationSpace.World,
                speed: (0.8f, 1.8f));
        Cone(billow, angle: 60f, radius: 0.25f, up: true);
        Drag(billow, 1.5f);
        Lift(billow, 0.45f);
        RandomRotation(billow);
        Spin(billow, 35f);
        ColorRange(billow, WithAlpha(LandDustLight, 0.6f), WithAlpha(LandDustMid, 0.55f));
        GrowFast(billow, 0.55f, 1.15f, 1.5f);
        Fade(billow, Color.white, WithAlpha(Color.white, 0f), shrink: false);

        // ③ 잔돌 — 낮게 비스듬히 튀어 먼지 고리 바깥(≈ 1.7~3.9m)까지 날았다 떨어진다(바닥 아래로 들어가면 깊이에 가려 사라진다).
        //    처음엔 거의 수직(42°)이라 1m 안에서 오르내리며 먼지 뭉치에 묻혀 안 보였다(임시 프로젝트 렌더) — 65° 로 눕히고, 먼지보다 나중에 그린다(sortingFudge)
        var debrisGo = Child(root, "Pebbles");
        debrisGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        var debris = MakeSystem(debrisGo, "Pebbles", AlphaBlend("M_Fx_Debris", Dot()), ParticleSystemRenderMode.Billboard);
        OneShot(debris, duration: 0.3f, life: (0.45f, 0.6f), size: (0.11f, 0.18f), count: (9, 13), space: ParticleSystemSimulationSpace.World,
                speed: (6f, 9f), gravity: 1.6f);
        Cone(debris, angle: 65f, radius: 0.35f, up: true);
        debrisGo.GetComponent<ParticleSystemRenderer>().sortingFudge = -20f;
        ColorRange(debris, WithAlpha(LandPebble, 1f), WithAlpha(LandDustMid * 0.8f, 1f));
        Fade(debris, Color.white, WithAlpha(Color.white, 0.9f), shrink: false);

        // ④ 먼지 충격파 — 바닥에 누운 링이 0.35초에 반경 ≈ 2.1m 까지 퍼지며 사라진다. 빛나는 선이 아니라 먼지색(알파 블렌드)
        var waveGo = Child(root, "ShockWave");
        waveGo.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        var wave = MakeSystem(waveGo, "ShockWave", AlphaBlend("M_Fx_DustRing", Ring()), ParticleSystemRenderMode.HorizontalBillboard);
        OneShot(wave, duration: 0.3f, life: (0.38f, 0.38f), size: (5.5f, 5.5f), count: (1, 1), space: ParticleSystemSimulationSpace.World);
        StartColor(wave, WithAlpha(LandDustLight, 0.7f));
        GrowFast(wave, 0.12f, 0.75f, 1f);
        Fade(wave, Color.white, WithAlpha(Color.white, 0f), shrink: false);
    }

    /// 바닥에 누운 원 둘레에서 바깥으로(반지름 방향) 내보낸다. even 이면 버스트를 둘레에 고르게 나눈다(뭉치지 않게) — 거리는 속도 무작위로 흩어진다.
    static void FlatRing(ParticleSystem ps, float radius, bool even)
    {
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = radius;
        sh.radiusThickness = 0f;                  // 둘레에서
        sh.rotation = new Vector3(90f, 0f, 0f);   // 원은 기본이 XY 평면 — 눕힌다
        sh.arcMode = even ? ParticleSystemShapeMultiModeValue.BurstSpread : ParticleSystemShapeMultiModeValue.Random;
        sh.randomDirectionAmount = 0.15f;         // 고르게 나누되 완전한 타원으로 보이지 않게 조금 흐트러뜨린다
    }

    /// 끌림(빠를수록 세게) — 터지고 빨리 멈추는 먼지. 이동 거리 ≈ ln(1 + 끌림 · v₀ · t) / 끌림.
    /// 함정(2026-11-16 임시 프로젝트 실측): '속도 곱하기'를 끄면 끌림은 그냥 일정한 감속(m/s²)이다 — 끌림 4 · 7m/s 가 1초에 5m 를 갔다.
    /// 곱하기를 켜면 끌림 1.2 · 7m/s 가 0.1초 0.76m → 0.25초 1.13m → 0.9초 1.93m(시작 반경 0.3 포함)로 터지고 멈춘다.
    static void Drag(ParticleSystem ps, float drag)
    {
        var lv = ps.limitVelocityOverLifetime;
        lv.enabled = true;
        lv.limit = new ParticleSystem.MinMaxCurve(100f);   // 상한은 안 쓴다 — 끌림만
        lv.drag = new ParticleSystem.MinMaxCurve(drag);
        lv.multiplyDragByParticleSize = false;
        lv.multiplyDragByParticleVelocity = true;
    }

    /// 월드 위쪽으로 일정한 속도(m/s)를 더한다 — 멈춘 먼지가 천천히 떠오른다.
    static void Lift(ParticleSystem ps, float up)
    {
        var v = ps.velocityOverLifetime;
        v.enabled = true;
        v.space = ParticleSystemSimulationSpace.World;
        v.x = new ParticleSystem.MinMaxCurve(0f);
        v.y = new ParticleSystem.MinMaxCurve(up);
        v.z = new ParticleSystem.MinMaxCurve(0f);
    }

    /// 크기: 처음 1/4 에 from → mid 로 빨리 부풀고, 나머지 동안 to 까지 천천히(먼지가 터졌다가 퍼지는 모양).
    static void GrowFast(ParticleSystem ps, float from, float mid, float to)
    {
        var c = new AnimationCurve(new Keyframe(0f, from), new Keyframe(0.25f, mid), new Keyframe(1f, to));
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, c);
    }

    // ── 파티클 조립 ───────────────────────────────────────────────
    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static ParticleSystem MakeSystem(GameObject go, string name, Material mat, ParticleSystemRenderMode mode)
    {
        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = mode;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.alignment = ParticleSystemRenderSpace.View;
        return ps;
    }

    /// 원샷 공통: 버스트 한 번, 반복 없음, Hierarchy 스케일(ProtoFx 가 루트 localScale 로 덩치를 곱한다).
    static void OneShot(ParticleSystem ps, float duration, (float, float) life, (float, float) size, (int, int) count,
                        ParticleSystemSimulationSpace space, (float, float)? speed = null, float gravity = 0f)
    {
        var m = ps.main;
        m.duration = duration;
        m.loop = false;
        m.playOnAwake = true;          // 씬에 끌어다 놓아도 바로 보이게. ProtoFx 는 어차피 Clear + Play 로 다시 튼다
        m.startLifetime = new ParticleSystem.MinMaxCurve(life.Item1, life.Item2);
        m.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
        m.startSpeed = speed.HasValue ? new ParticleSystem.MinMaxCurve(speed.Value.Item1, speed.Value.Item2) : new ParticleSystem.MinMaxCurve(0f);
        m.startColor = new ParticleSystem.MinMaxGradient(Color.white);
        m.gravityModifier = gravity;
        m.simulationSpace = space;
        m.scalingMode = ParticleSystemScalingMode.Hierarchy;
        m.maxParticles = Mathf.Max(8, count.Item2 * 2);
        m.stopAction = ParticleSystemStopAction.None;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count.Item1, (short)count.Item2) });

        var sh = ps.shape;
        sh.enabled = false;   // 기본은 한 점. 방향이 있으면 Cone/Sphere 가 켠다
    }

    static void StartColor(ParticleSystem ps, Color c)
    {
        var m = ps.main;
        m.startColor = new ParticleSystem.MinMaxGradient(c);
    }

    /// 입자마다 무작위 각도로 시작 — 매 발 같은 모양이면 도장 찍은 것처럼 보인다.
    static void RandomRotation(ParticleSystem ps)
    {
        var m = ps.main;
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
    }

    /// 사는 동안 천천히 돈다(연기). 방향은 입자마다 무작위.
    static void Spin(ParticleSystem ps, float degreesPerSecond)
    {
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.separateAxes = false;
        float r = degreesPerSecond * Mathf.Deg2Rad;
        rol.z = new ParticleSystem.MinMaxCurve(-r, r);
    }

    static void Cone(ParticleSystem ps, float angle, float radius, bool up = false)
    {
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = angle;
        sh.radius = radius;
        sh.rotation = up ? new Vector3(-90f, 0f, 0f) : Vector3.zero;   // 기본은 +Z(= ProtoFx 가 넘긴 방향)
    }

    static void Sphere(ParticleSystem ps, float radius)
    {
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = radius;
    }

    static void Stretch(ParticleSystem ps, float velocityScale, float lengthScale)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = velocityScale;
        r.lengthScale = lengthScale;
    }

    /// 색은 a → b 로, 알파는 1 → 0 으로(b 의 알파가 0 이면 그대로). shrink 면 크기도 1 → 0.
    static void Fade(ParticleSystem ps, Color a, Color b, bool shrink)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                  new[] { new GradientAlphaKey(a.a, 0f), new GradientAlphaKey(Mathf.Min(a.a, b.a > 0f ? b.a : a.a), 0.6f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(g);
        if (shrink)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
        }
    }

    /// 스프라이트 시트(격자). animate 면 수명 동안 first→last 프레임을 한 번 돌리고, 아니면 first~last 중 무작위 한 프레임을 고정으로 쓴다(흩어진 핏자국).
    static void Sheet(ParticleSystem ps, int tilesX, int tilesY, int firstFrame, int lastFrame, bool animate)
    {
        var ts = ps.textureSheetAnimation;
        ts.enabled = true;
        ts.mode = ParticleSystemAnimationMode.Grid;
        ts.numTilesX = tilesX; ts.numTilesY = tilesY;
        ts.animation = ParticleSystemAnimationType.WholeSheet;
        ts.cycleCount = 1;
        // 함정(11-05): startFrame 도 frameOverTime 처럼 **0~1 정규화**(× 전체 프레임)다 — 인스펙터만 프레임 수로 보여 준다.
        // 프레임 번호를 그대로 넣으면 1 을 넘어 마지막 칸(이 시트는 빈 칸)에 고정돼 아무것도 안 보인다(바닥 피가 안 보이던 원인).
        float total = tilesX * tilesY;
        if (animate)
        {
            ts.startFrame = new ParticleSystem.MinMaxCurve(firstFrame / total);
            ts.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, (lastFrame - firstFrame + 1) / total));
        }
        else
        {
            ts.startFrame = new ParticleSystem.MinMaxCurve(firstFrame / total, (lastFrame + 0.999f) / total);
            ts.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        }
    }

    /// 바닥에 누운 원 안 무작위 자리(속도 0 이면 그 자리에 놓인다) — 흩어진 핏자국.
    static void Disc(ParticleSystem ps, float radius)
    {
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = radius;
        sh.radiusThickness = 1f;
        sh.rotation = new Vector3(90f, 0f, 0f);   // 원은 기본이 XY 평면 — 눕힌다
    }

    /// 버스트를 여러 시각에 나눠 낸다(핏방울이 떨어져 닿는 시간차). OneShot 의 0초 버스트를 대신한다.
    static void Staggered(ParticleSystem ps, params (float time, int min, int max)[] bursts)
    {
        var list = new ParticleSystem.Burst[bursts.Length];
        for (int i = 0; i < bursts.Length; i++) list[i] = new ParticleSystem.Burst(bursts[i].time, (short)bursts[i].min, (short)bursts[i].max);
        ps.emission.SetBursts(list);
    }

    /// 시작 색을 두 색 사이 무작위로.
    static void ColorRange(ParticleSystem ps, Color a, Color b)
    {
        var m = ps.main;
        m.startColor = new ParticleSystem.MinMaxGradient(a, b);
    }

    /// 시작 각도를 ±deg 안에서 무작위로(도장 찍은 것처럼 안 보이게, 그러나 뒤집히진 않게).
    static void Tilt(ParticleSystem ps, float deg)
    {
        var m = ps.main;
        float r = deg * Mathf.Deg2Rad;
        m.startRotation = new ParticleSystem.MinMaxCurve(-r, r);
    }

    static void Grow(ParticleSystem ps, float from, float to)
    {
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
    }

    static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    // ── 머티리얼 · 텍스처 ─────────────────────────────────────────
    /// 가산 파티클 머티리얼(빛나는 것 — 섬광·불똥·링). 밤 어둠 막 뒤 큐 3100.
    static Material Additive(string name, Texture2D tex) => Mat(name, tex, additive: true);
    /// 가산 점 머티리얼(M_Fx_Glow · 빛나는 점) — 다른 조립(FxPackImport 의 잔불)도 쓴다. 없으면 만든다.
    public static Material GlowDot() => Additive("M_Fx_Glow", Dot());

    /// 알파 블렌드 파티클 머티리얼(빛나지 않는 것 — 연기·먼지). 발광을 끄고 보통 투명 큐 3000.
    static Material AlphaBlend(string name, Texture2D tex) => Mat(name, tex, additive: false);

    /// 기존 화염 분사 머티리얼(URP Particles Unlit)을 복제해 텍스처를 바꾸고 <see cref="Harden"/> 규칙을 건다 —
    /// 소프트 파티클·페이드 같은 설정을 코드로 다시 맞추는 것보다 확실하다. 없으면 셰이더에서 새로 만든다.
    static Material Mat(string name, Texture2D tex, bool additive)
    {
        string path = $"{Dir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;

        var src = AssetDatabase.LoadAssetAtPath<Material>(SourceMat);
        if (src != null) m = new Material(src);
        else
        {
            var sh = Shader.Find(FxShaderUnify.ShaderName);
            if (sh == null) sh = Shader.Find("Sprites/Default");
            m = new Material(sh);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        m.name = name;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        Harden(m, additive);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/FX 머티리얼 설정 고치기 (발광 · 플립북 · 블렌드)")]
    public static void HardenMaterialsMenu()
    {
        int n = HardenMaterials();
        Debug.Log(n > 0 ? $"[FX 툴] 머티리얼 {n}개 설정을 규칙대로 고쳤다" : "[FX 툴] 머티리얼 설정은 모두 규칙대로다");
    }

    /// 생성된 머티리얼(M_Fx_*)이 규칙에서 벗어났나 — 에디터가 켜질 때 FxAssetsSetup.AutoCreate 가 보고 고친다.
    public static bool MaterialsNeedFix()
    {
        foreach (var n in GeneratedMats)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/{n}.mat");
            if (m == null) continue;
            bool emits = m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.001f;
            bool flip = m.IsKeywordEnabled("_FLIPBOOKBLENDING_ON") || (m.HasProperty("_FlipbookBlending") && m.GetFloat("_FlipbookBlending") > 0.5f);
            bool additive = Array.IndexOf(AlphaMats, n) < 0;
            bool dim = additive && m.HasProperty("_BaseColor") && m.GetColor("_BaseColor") != BaseColorFor(true);   // 블룸 배율이 안 들어갔다
            if (emits || flip || dim) return true;
        }
        return false;
    }

    /// 있는 머티리얼 전부에 규칙을 다시 건다. 바뀐 개수를 돌려준다.
    public static int HardenMaterials()
    {
        int n = 0;
        foreach (var name in GeneratedMats)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/{name}.mat");
            if (m == null) continue;
            if (!Harden(m, additive: Array.IndexOf(AlphaMats, name) < 0)) continue;
            EditorUtility.SetDirty(m);
            Debug.Log($"[FX 툴] 머티리얼 {name} 설정 고침 (발광 끔 · 플립북 끔 · 블렌드 · 큐)");
            n++;
        }
        if (n > 0) AssetDatabase.SaveAssets();
        return n;
    }

    /// <summary>
    /// 머티리얼 설정 규칙. 바뀐 게 있으면 true.
    ///
    /// <para><b>왜 필요한가.</b> 화염 머티리얼을 복제하면서 <b>주황 발광(_EmissionColor 4.2 / 1.45 / 0.1)</b>과 플립북 블렌딩이 따라왔다.
    /// 발광이 켜진 채면 흰 심·파란 젬 할 것 없이 모든 입자에 주황이 더해져 과하게 밝다 — 입자 색은 startColor · colorOverLifetime 이 정해야 한다.
    /// 플립북은 생성한 파티클이 UV2 · 블렌드 스트림을 안 넘기므로 끈다.</para>
    ///
    /// <para><b>키워드만 끄면 되살아난다.</b> URP 는 머티리얼을 임포트할 때 float 값(_FlipbookBlending)과 GI 플래그에서 키워드를 다시 만든다.
    /// 그래서 값 · GI 플래그 · 키워드를 같이 바꾼다(처음 만든 M_Fx_Smoke 가 키워드만 꺼서 _EMISSION 이 남아 있던 이유).</para>
    /// </summary>
    static bool Harden(Material m, bool additive)
    {
        bool changed = false;
        void F(string p, float v) { if (m.HasProperty(p) && !Mathf.Approximately(m.GetFloat(p), v)) { m.SetFloat(p, v); changed = true; } }
        void C(string p, Color v) { if (m.HasProperty(p) && m.GetColor(p) != v) { m.SetColor(p, v); changed = true; } }
        void K(string k, bool on) { if (m.IsKeywordEnabled(k) != on) { if (on) m.EnableKeyword(k); else m.DisableKeyword(k); changed = true; } }

        // 발광 끄기
        F("_EmissionEnabled", 0f); C("_EmissionColor", Color.black); K("_EMISSION", false);
        if (m.globalIlluminationFlags != MaterialGlobalIlluminationFlags.EmissiveIsBlack) { m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack; changed = true; }
        // 플립북 블렌딩 끄기
        F("_FlipbookBlending", 0f); K("_FLIPBOOKBLENDING_ON", false);
        // 색 모드 = 곱하기(텍스처 × 입자 색). 화염 머티리얼은 Color 모드라 그대로 두면 텍스처 색을 무시한다
        F("_ColorMode", 0f); K("_COLORCOLOR_ON", false); K("_COLOROVERLAY_ON", false); K("_COLORADDSUBDIFF_ON", false);
        // 밝기 — 빛나는 것만 GlowBoost 배(블룸 문턱용). 연기·먼지는 1
        C("_BaseColor", BaseColorFor(additive));
        C("_Color", BaseColorFor(additive));   // 옛 이름 — URP 셰이더가 숨김 프로퍼티로 같이 들고 있다. 없으면 건너뛴다
        // 블렌드 — URP Particles Unlit: _Blend 0 = 알파, 2 = 가산. SrcBlend 5 = SrcAlpha, DstBlend 1 = One / 10 = OneMinusSrcAlpha
        F("_Surface", 1f); F("_ZWrite", 0f);
        K("_ALPHAPREMULTIPLY_ON", false); K("_ALPHAMODULATE_ON", false);
        int queue;
        if (additive)
        {
            F("_Blend", 2f); F("_SrcBlend", 5f); F("_DstBlend", 1f); F("_SrcBlendAlpha", 1f); F("_DstBlendAlpha", 1f);
            queue = GlowQueue;
        }
        else
        {
            F("_Blend", 0f); F("_SrcBlend", 5f); F("_DstBlend", 10f); F("_SrcBlendAlpha", 1f); F("_DstBlendAlpha", 10f);
            queue = m.name.StartsWith("M_Fx_Blood") ? BloodQueue : SmokeQueue;
            // URP 파티클 셰이더는 임포트 · 인스펙터 때 큐를 3000 + _QueueOffset 으로 다시 계산한다 — 큐 숫자만 바꾸면 3000 으로 되돌아간다
            if (queue == BloodQueue) F("_QueueOffset", BloodQueue - 3000);
        }
        if (m.renderQueue != queue) { m.renderQueue = queue; changed = true; }
        return changed;
    }

    static Color BaseColorFor(bool additive) => additive ? new Color(GlowBoost, GlowBoost, GlowBoost, 1f) : Color.white;

    static float R(float x, float y) => Mathf.Sqrt(x * x + y * y);

    /// 울퉁불퉁한 얼룩 — 핏덩이 · 바닥 피. 가장자리를 각도별 사인 세 겹으로 흔든다(도장 찍은 원으로 안 보이게).
    static Texture2D Splat() => Tex("T_Fx_Splat", (x, y) =>
    {
        float a = Mathf.Atan2(y, x);
        float edge = 0.55f + 0.12f * Mathf.Sin(3f * a + 0.7f) + 0.09f * Mathf.Sin(5f * a + 2.1f) + 0.06f * Mathf.Sin(8f * a + 4f);
        return Mathf.Clamp01((edge - R(x, y)) / 0.08f);
    });

    /// 부드러운 점 — 섬광·불똥·반짝임 전부.
    static Texture2D Dot() => Tex("T_Fx_SoftDot", (x, y) => Mathf.Pow(Mathf.Clamp01(1f - R(x, y)), 1.6f));

    /// 얇은 링 — 처치 바닥 링.
    static Texture2D Ring() => Tex("T_Fx_Ring", (x, y) => { float k = Mathf.Clamp01(1f - Mathf.Abs(R(x, y) - 0.76f) / 0.16f); return k * k; });

    /// 4갈래 별 — 총구 섬광. 가운데는 꽉 찬 심, 가시는 가늘고 길다.
    static Texture2D Star4() => Tex("T_Fx_Star4", (x, y) =>
    {
        float r = R(x, y);
        float ang = Mathf.Atan2(y, x);
        float spike = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2f)), 12f);
        float arm = Mathf.Clamp01(1f - r / (0.18f + 0.82f * spike));
        float core = Mathf.Clamp01(1f - r / 0.22f);
        return Mathf.Clamp01(arm * arm * 1.2f + core);
    }, size: 128);

    /// 연기 뭉치 — 부드러운 점 몇 개를 겹쳐 가장자리를 울퉁불퉁하게. 알파 블렌드용.
    static Texture2D Smoke() => Tex("T_Fx_Smoke", (x, y) =>
    {
        float a = 0f;
        for (int i = 0; i < SmokeBlobs.Length; i++)
        {
            float dx = x - SmokeBlobs[i].x, dy = y - SmokeBlobs[i].y;
            float r = Mathf.Sqrt(dx * dx + dy * dy) / SmokeBlobs[i].z;
            a = Mathf.Max(a, Mathf.Pow(Mathf.Clamp01(1f - r), 1.4f));
        }
        return Mathf.Clamp01(a * 0.95f);
    }, size: 128);

    // (x, y, 반지름) — 텍스처 좌표 -1~1
    static readonly Vector3[] SmokeBlobs =
    {
        new Vector3(0f, 0f, 0.62f), new Vector3(0.28f, 0.18f, 0.5f), new Vector3(-0.3f, 0.12f, 0.46f),
        new Vector3(0.1f, -0.3f, 0.48f), new Vector3(-0.15f, 0.32f, 0.4f),
    };

    /// 흰색 + 알파(x, y ∈ -1~1 의 함수) 텍스처를 PNG 로 굽는다. 있으면 그대로 쓴다.
    static Texture2D Tex(string name, Func<float, float, float> alphaOfXY, int size = 64)
    {
        string path = $"{Dir}/{name}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        EnsureFolder(Dir);

        int n = size;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alphaOfXY(dx, dy))));
            }
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.sRGBTexture = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
