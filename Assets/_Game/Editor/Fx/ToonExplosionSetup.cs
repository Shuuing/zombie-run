using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Proto;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 카툰 폭발 — 사용자 레퍼런스 영상(2026-11-12, "영상에 나와있는데로 이펙트 똑같이")을 파티클로 짠다.
///
/// <para><b>영상 → 시스템</b> (시각은 섬광 = 0초, 영상 30fps 프레임 13 이 0초):</para>
/// <list type="bullet">
/// <item>0~0.15초 섬광 — Flash(빛 덩이) · FlashCore(세로 달걀 빛) · FlashBurst(첫 틀 햇살) · FlashRays(바닥 햇살) · GroundGlow(바닥 주황 빛, 1초).</item>
/// <item>0.03~0.5초 가시 불꽃 — Spikes(늘인 빌보드, 위로 V 자로 뻗는 연노랑/주황 날).</item>
/// <item>0.03~2.8초 불덩이 — Fireball · FireballTop · FireballBase(구체 메시 파티클 + <c>ZombieOverrun/ToonFireball</c>).
///       수명 따라 Custom1.x(열)이 식어 연노랑 → 주황 → 검붉은 연기로, Custom1.y(디졸브)가 구멍을 뚫어 실처럼 갈라지며 사라진다.
///       Base(바닥 고리)는 오래 빛나고 먼저 사라지고, Top(위)은 먼저 식고 위로 오르며 마지막에 사라진다.</item>
/// <item>0.04~1.2초 연기 고리 — SmokeRing(바닥을 따라 퍼지는 회색 뭉치) · Shards(바닥 둘레로 튀는 흰 지그재그 날).</item>
/// <item>불똥 — Sparks(터질 때 사방) · Embers(연기 속 떠오르는 티끌, 0.3~1.5초).</item>
/// <item>0.06~3.1초 바닥 용암 균열 — Cracks(<c>ZombieOverrun/ToonCrackDecal</c>, 가운데부터 퍼져 생기고 토막토막 꺼진다) · Wisps(끝무렵 가는 연기 줄).</item>
/// </list>
/// <para>크기: 배율 1 = 불덩이 지름 약 2.6m · 균열 지름 5m(로켓 판정 반경 2.4m 급). 카탈로그 <c>explosion_toon</c>(수명 3.4초)에 물린다 —
/// 기존 <c>explosion</c>(코드 그림)은 안 건드린다. 무기에 쓰려면 WFX 의 explode 슬롯에 이 프리팹을 놓는다.</para>
/// <para>텍스처: <c>ArtSource/VFX/bake_toon_explosion.py</c>. 에디터가 뜰 때 판(<see cref="Version"/>)이 오르면 다시 굽는다(표식 Library/ToonExplosion.applied).
/// 프리팹을 손으로 고쳤으면(파일이 표식보다 새것) 판이 올라도 머티리얼만 바꾸고 프리팹은 둔다 — 메뉴 '카툰 폭발 다시 굽기' 만 덮어쓴다.</para>
/// <para>미리보기: 메뉴 '카툰 폭발 미리보기 렌더' → <c>Library/ToonExplosionPreview/*.png</c>(영상 각도 · 게임 각도 12컷).</para>
/// </summary>
public static class ToonExplosionSetup
{
    public const string Dir = "Assets/_Game/Art/VFX/Explosion";
    public const string PrefabPath = Dir + "/FX_ToonExplosion.prefab";
    const string BallMeshPath = Dir + "/Mesh_ToonExploBall.asset";
    public const string CatalogId = "explosion_toon";
    public const float Lifetime = 3.4f;
    const string FireballShader = "ZombieOverrun/ToonFireball";
    const string CrackShader = "ZombieOverrun/ToonCrackDecal";
    static readonly string Marker = Path.Combine("Library", "ToonExplosion.applied");
    public static readonly string PreviewDir = Path.Combine("Library", "ToonExplosionPreview");

    /// 굽기 규칙의 판. v1(11-12): 첫 판 — 영상 재현.
    public const int Version = 1;

    // ── 색(영상에서 뽑음) ─────────────────────────────────────────────
    static readonly Color FlashWhite = new Color(1f, 0.93f, 0.78f);
    static readonly Color SpikeEnd = new Color(1f, 0.55f, 0.28f);
    static readonly Color SmokeWarm = new Color(0.74f, 0.56f, 0.5f);      // 영상 연기 고리: 불빛 받은 면 (172,138,121) · 몸 (111,76,69)
    static readonly Color SmokeGrey = new Color(0.44f, 0.37f, 0.36f);
    static readonly Color ShardWhite = new Color(0.93f, 0.87f, 0.81f);     // 영상 (166,137,121) — 불빛 받은 베이지 회색
    static readonly Color SparkHot = new Color(1f, 0.74f, 0.36f);
    static readonly Color SparkMid = new Color(1f, 0.45f, 0.14f);
    static readonly Color SparkEnd = new Color(0.85f, 0.22f, 0.07f);
    static readonly Color GlowOrange = new Color(1f, 0.34f, 0.1f);       // 가산 × 2 — 바닥이 노랗지 않고 주황으로

    // ── 자동 적용 ─────────────────────────────────────────────────────
    static bool _pending;
    static double _nextPoll;

    [InitializeOnLoadMethod]
    static void AutoApply()
    {
        if (Application.isBatchMode) return;   // 배치(임시 프로젝트 렌더)는 진입점이 직접 부른다
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        EditorApplication.delayCall += () => { if (NeedsBuild()) _pending = true; };
    }

    /// 2026-11-20: 카툰 폭발은 11-13 에 폴더째(프리팹 · 머티리얼 · 텍스처) 지워졌다. 텍스처가 없으면 자동 굽기는 쉰다 —
    /// 예전엔 에디터가 켜질 때마다 빈 Explosion 폴더를 다시 만들고 '텍스처가 없다' 오류를 냈다. 다시 쓰려면 텍스처를 굽고 메뉴로.
    static bool NeedsBuild() => File.Exists($"{Dir}/T_ToonExplo_Spike.png")
        && (MarkerVersion() < Version || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null);

    static void Poll()
    {
        if (!_pending || EditorApplication.timeSinceStartup < _nextPoll) return;
        _nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (FxCatalogEdit.Catalog == null) { _nextPoll += 2.0; return; }   // 카탈로그를 FxAssetsSetup 이 아직 안 만들었다
        _pending = false;
        try
        {
            bool keep = HandEdited();
            if (keep) Debug.LogWarning($"[카툰 폭발] 프리팹을 손으로 고친 흔적이 있어 v{Version} 은 머티리얼만 바꾼다. 파티클까지 새 판으로: 메뉴 'Tools/좀비 오버런/FX 툴/카툰 폭발 다시 굽기' — 손편집은 사라진다.");
            Debug.Log($"[카툰 폭발] 굽기 v{MarkerVersion()} → v{Version}{(keep ? " (머티리얼만)" : "")}");
            if (Build(rebuildPrefab: !keep)) WriteMarker();
        }
        catch (Exception e) { Debug.LogError("[카툰 폭발] 굽기 실패 — 메뉴 '카툰 폭발 다시 굽기' 로: " + e); WriteMarker(); }
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/카툰 폭발 다시 굽기 (덮어쓰기)")]
    public static void RebuildMenu()
    {
        if (File.Exists(PrefabPath.Replace('/', Path.DirectorySeparatorChar)) &&
            !EditorUtility.DisplayDialog("카툰 폭발 다시 굽기", "FX_ToonExplosion 프리팹을 코드 값으로 덮어쓴다. 손으로 고친 값은 사라진다.", "덮어쓰기", "취소")) return;
        if (Build(rebuildPrefab: true)) { WriteMarker(); Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/카툰 폭발 미리보기 렌더")]
    public static void PreviewMenu()
    {
        var files = RenderPreview();
        if (files.Count > 0) EditorUtility.RevealInFinder(files[0]);
    }

    /// 배치 진입점(에디터가 닫혀 있을 때): Unity.exe -batchmode -projectPath … -executeMethod ToonExplosionSetup.BatchBuild -quit
    public static void BatchBuild()
    {
        if (Build(rebuildPrefab: true)) WriteMarker();
        RenderPreview();
    }

    static bool HandEdited()
    {
        string p = PrefabPath.Replace('/', Path.DirectorySeparatorChar);
        if (!File.Exists(p) || !File.Exists(Marker)) return false;
        return File.GetLastWriteTimeUtc(p) > File.GetLastWriteTimeUtc(Marker).AddSeconds(5);
    }

    static int MarkerVersion()
    {
        try { return File.Exists(Marker) && int.TryParse(File.ReadAllText(Marker).Trim(), out int v) ? v : 0; }
        catch (Exception) { return 0; }
    }

    static void WriteMarker()
    {
        try { File.WriteAllText(Marker, Version.ToString()); } catch (Exception) { }
    }

    // ── 굽기 ─────────────────────────────────────────────────────────
    struct Mats { public Material fireball, cracks, spike, shard, puff, glow, flash, ember, wisp; }

    public static bool Build(bool rebuildPrefab = true)
    {
        EnsureFolder(Dir);
        var tex = new Dictionary<string, Texture2D>();
        foreach (var n in new[] { "Spike", "Shard", "Flash", "Glow", "Puff", "Ember", "Cracks" })
        {
            var t = ImportTex($"{Dir}/T_ToonExplo_{n}.png", linear: n == "Cracks");
            if (t == null) { Debug.LogError($"[카툰 폭발] 텍스처가 없다: {Dir}/T_ToonExplo_{n}.png — 'python -E ArtSource/VFX/bake_toon_explosion.py' 로 굽는다"); return false; }
            tex[n] = t;
        }
        var fireSh = Shader.Find(FireballShader);
        var crackSh = Shader.Find(CrackShader);
        if (fireSh == null || crackSh == null) { Debug.LogError("[카툰 폭발] 셰이더를 못 찾았다(컴파일 오류?) — Art/VFX/Shaders/ToonFireball · ToonCrackDecal"); return false; }

        var m = new Mats
        {
            fireball = ShaderMat("M_ToonExplo_Fireball", fireSh, null, 2990),   // 밤 어둠 막(2900) 뒤 · 다른 투명(3000)보다 먼저 — 깊이를 써서 앞 연기가 안 덮인다
            cracks = ShaderMat("M_ToonExplo_Cracks", crackSh, tex["Cracks"], 3000),
            spike = Mat("M_ToonExplo_Spike", tex["Spike"], additive: false, boost: 1.35f),
            shard = Mat("M_ToonExplo_Shard", tex["Shard"], additive: false, boost: 1f),
            puff = Mat("M_ToonExplo_Puff", tex["Puff"], additive: false, boost: 1f, queue: 2980),   // 불덩이(2990)보다 먼저 — 불이 늘 연기 위에(영상처럼 불이 이긴다)
            glow = Mat("M_ToonExplo_Glow", tex["Glow"], additive: true, boost: 2f),
            flash = Mat("M_ToonExplo_Flash", tex["Flash"], additive: true, boost: 1.6f),
            ember = Mat("M_ToonExplo_Ember", tex["Ember"], additive: true, boost: 2.2f),
            wisp = Mat("M_ToonExplo_Wisp", tex["Glow"], additive: false, boost: 1f),
        };
        AssetDatabase.SaveAssets();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (rebuildPrefab || prefab == null)
        {
            var root = new GameObject("FX_ToonExplosion");
            try
            {
                Assemble(root, m);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        if (prefab == null) { Debug.LogError("[카툰 폭발] 프리팹 저장 실패"); return false; }
        Register(prefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"[카툰 폭발] {PrefabPath} 굽기 끝 (v{Version}) — 카탈로그 '{CatalogId}'");
        return true;
    }

    /// 카탈로그 항목 — 없으면 만들고, 프리팹 칸이 비었으면 채운다. 사용자가 바꾼 값(수명 · 상한 · 프리팹)은 안 건드린다.
    static void Register(GameObject prefab)
    {
        var cat = FxCatalogEdit.Catalog;
        if (cat == null) return;
        var e = cat.entries.FirstOrDefault(x => x != null && x.id == CatalogId);
        bool changed = false;
        if (e == null)
        {
            e = new FxCatalog.Entry
            {
                id = CatalogId,
                note = "타격감/세리머니 — 카툰 폭발(사용자 레퍼런스 영상 11-12). 섬광 · 가시 불꽃 · 구체 불덩이(식어서 연기 → 디졸브) · 연기 고리 · 흰 파편 · 불똥 · 바닥 용암 균열. " +
                       "자리 = 폭심(바닥), scale 1 = 불덩이 지름 2.6m · 균열 5m. radius 는 안 쓴다(scale 로 맞춘다). 굽기 ToonExplosionSetup.",
                lifetime = Lifetime,
                poolSize = 4,
                maxAlive = 6,
                previewOffset = Vector3.zero,
            };
            cat.entries.Add(e);
            changed = true;
        }
        if (e.prefab == null) { e.prefab = prefab; changed = true; }
        if (changed)
        {
            EditorUtility.SetDirty(cat);
            FxCatalog.Reload();
        }
    }

    // ── 조립 ─────────────────────────────────────────────────────────
    static void Assemble(GameObject root, Mats m)
    {
        // 루트 = 빈 시스템(그리지 않음) — 씬에서 루트를 고르면 자식까지 한 번에 재생된다
        var rootPs = Sys(root, null, ParticleSystemRenderMode.Billboard, 0f, 1, 0f);
        { var main = rootPs.main; main.duration = Lifetime; var em = rootPs.emission; em.enabled = false; }
        root.GetComponent<ParticleSystemRenderer>().enabled = false;
        root.AddComponent<ToonExplosionAnchor>();   // 불덩이 셰이더에 바닥 · 배율(_Ground) — 메시 파티클은 월드 좌표라 셰이더가 루트를 모른다

        // ① 섬광 ──────────────────────────────────────────────────────
        var flash = Sys(Child(root, "Flash", new Vector3(0f, 0.9f, 0f)), m.glow, ParticleSystemRenderMode.Billboard, -2f, 2, 0f);
        Burst(flash, 1); Life(flash, 0.14f, 0.14f); Size(flash, 4.5f, 4.5f); StartColor(flash, FlashWhite);
        SizeOver(flash, (0f, 0.55f), (0.25f, 1f), (1f, 1.15f));
        ColorOver(flash, new[] { (Color.white, 0f), (new Color(1f, 0.8f, 0.55f), 1f) }, new[] { (1f, 0f), (0.8f, 0.4f), (0f, 1f) });

        var core = Sys(Child(root, "FlashCore", new Vector3(0f, 0.95f, 0f)), m.glow, ParticleSystemRenderMode.Billboard, -2.5f, 2, 0f);
        Burst(core, 1); Life(core, 0.1f, 0.1f); StartColor(core, new Color(1f, 0.97f, 0.9f));
        { var main = core.main; main.startSize3D = true; main.startSizeX = 1.1f; main.startSizeY = 2.1f; main.startSizeZ = 1f; }
        ColorOver(core, new[] { (Color.white, 0f), (Color.white, 1f) }, new[] { (1f, 0f), (0.9f, 0.5f), (0f, 1f) });

        var rays = Sys(Child(root, "FlashRays", new Vector3(0f, 0.05f, 0f)), m.flash, ParticleSystemRenderMode.HorizontalBillboard, 2f, 2, 0f);
        Burst(rays, 1); Life(rays, 0.17f, 0.17f); Size(rays, 5.5f, 5.5f); StartColor(rays, new Color(1f, 0.86f, 0.62f)); RandomRotation(rays);
        SizeOver(rays, (0f, 0.6f), (1f, 1.2f));
        ColorOver(rays, new[] { (Color.white, 0f), (Color.white, 1f) }, new[] { (1f, 0f), (0.7f, 0.4f), (0f, 1f) });

        var gglow = Sys(Child(root, "GroundGlow", new Vector3(0f, 0.04f, 0f)), m.glow, ParticleSystemRenderMode.HorizontalBillboard, 2.5f, 2, 0f);
        Burst(gglow, 1); Life(gglow, 1.1f, 1.1f); Size(gglow, 7f, 7f); StartColor(gglow, GlowOrange);
        ColorOver(gglow, new[] { (Color.white, 0f), (Color.white, 1f) }, new[] { (0.9f, 0f), (0.7f, 0.15f), (0.3f, 0.5f), (0f, 1f) });

        // ② 가시 불꽃 ────────────────────────────────────────────────
        // 영상: 터지는 첫 틀에 이미 다 뻗어 있고(0.03초) 제자리에서 가늘어지며 꺼진다(0.4초).
        // 함정(11-12 렌더): 늘인 빌보드는 **머리가 입자 자리**이고 날이 뒤로 끌린다 — 가운데서 쏘면 날이 불덩이 · 바닥 속에 묻힌다.
        // 그래서 반구 **겉면**(반경 1.7~2.4m)에서 바깥으로 느리게 낸다 → 날이 가운데 근처에서 바깥까지 걸친다(길이 = 크기 × 3.6).
        // 반구를 옆으로 눌러(모양 배율 0.6) 위로 긴 V — 영상은 옆보다 위로 높이 뻗는다
        var spikes = Sys(Child(root, "Spikes", new Vector3(0f, 0.4f, 0f)), m.spike, ParticleSystemRenderMode.Stretch, -1f, 24, 0.02f);
        Burst(spikes, 16); Life(spikes, 0.32f, 0.5f); Speed(spikes, 1f, 2.2f); Size(spikes, 0.75f, 1.15f);
        Shape(spikes, ParticleSystemShapeType.Hemisphere, 0f, 3.2f, rotation: new Vector3(-90f, 0f, 0f), thickness: 0.2f);
        { var sh = spikes.shape; sh.scale = new Vector3(0.75f, 0.75f, 1f); }
        Stretch(spikes, 3.1f, 0f); Flip(spikes, 0f, 0.5f);
        SizeOver(spikes, (0f, 1f), (0.45f, 0.75f), (1f, 0.25f));
        ColorOver(spikes, new[] { (new Color(1f, 0.92f, 0.78f), 0f), (new Color(1f, 0.72f, 0.48f), 0.4f), (SpikeEnd, 1f) }, new[] { (1f, 0f), (0.95f, 0.5f), (0f, 1f) });

        // 첫 틀 햇살 — 영상 0.03초의 사방으로 뻗는 흰 섬광(화면을 보는 빌보드)
        var burst = Sys(Child(root, "FlashBurst", new Vector3(0f, 0.9f, 0f)), m.flash, ParticleSystemRenderMode.Billboard, -3f, 2, 0.01f);
        Burst(burst, 1); Life(burst, 0.12f, 0.12f); Size(burst, 7f, 7f); StartColor(burst, new Color(1f, 0.82f, 0.6f)); RandomRotation(burst);
        SizeOver(burst, (0f, 0.7f), (1f, 1.15f));
        ColorOver(burst, new[] { (Color.white, 0f), (Color.white, 1f) }, new[] { (1f, 0f), (0.6f, 0.5f), (0f, 1f) });

        // ③ 불덩이(구체) ──────────────────────────────────────────────
        var sphere = BallMesh();
        var ball = Sys(Child(root, "Fireball", new Vector3(0f, 0.55f, 0f)), m.fireball, ParticleSystemRenderMode.Mesh, 0f, 24, 0.03f);
        Fireball(ball, sphere, count: 20, life: (2.1f, 2.4f), speed: (0.3f, 1f), size: (0.62f, 0.9f));
        Shape(ball, ParticleSystemShapeType.Hemisphere, 0f, 1.05f, rotation: new Vector3(-90f, 0f, 0f));
        SizeOver(ball, (0f, 0.5f), (0.02f, 0.9f), (0.35f, 1.05f), (1f, 1.18f));   // 영상: 0.03초에 이미 거의 다 큼
        Rise(ball, (0f, 0f), (0.3f, 0.25f), (1f, 0.55f));
        Heat(ball,
            new[] { (0f, 1.2f), (0.05f, 1.08f), (0.15f, 0.95f), (0.25f, 0.75f), (0.35f, 0.5f), (0.45f, 0.28f), (0.55f, 0.08f), (0.68f, -0.15f), (1f, -0.45f) },
            new[] { (0f, 1.1f), (0.05f, 0.98f), (0.15f, 0.85f), (0.25f, 0.65f), (0.35f, 0.4f), (0.45f, 0.18f), (0.55f, -0.02f), (0.68f, -0.25f), (1f, -0.55f) },
            new[] { (0f, 0f), (0.55f, 0f), (0.72f, 0.32f), (0.88f, 0.7f), (1f, 1.02f) },
            new[] { (0f, 0f), (0.6f, 0f), (0.76f, 0.35f), (0.9f, 0.75f), (1f, 1.02f) });

        // 위 혹 — 반구가 아니라 공 모양으로 넓게(영상 불덩이는 원뿔이 아니라 둥근 지붕)
        var top = Sys(Child(root, "FireballTop", new Vector3(0f, 1.5f, 0f)), m.fireball, ParticleSystemRenderMode.Mesh, 0f, 18, 0.04f);
        Fireball(top, sphere, count: 14, life: (2.4f, 2.7f), speed: (0.3f, 0.8f), size: (0.65f, 0.95f));
        Shape(top, ParticleSystemShapeType.Sphere, 0f, 1.05f, thickness: 0.4f);
        SizeOver(top, (0f, 0.45f), (0.03f, 0.88f), (0.35f, 1.05f), (1f, 1.25f));
        Rise(top, (0f, 0.1f), (0.4f, 0.5f), (1f, 1.0f));   // 영상: 끝무렵 위 연기가 올라가며 실처럼 남는다
        Heat(top,
            new[] { (0f, 1.15f), (0.05f, 1.02f), (0.15f, 0.88f), (0.25f, 0.66f), (0.35f, 0.4f), (0.45f, 0.18f), (0.58f, -0.05f), (1f, -0.45f) },
            new[] { (0f, 1.05f), (0.05f, 0.92f), (0.15f, 0.78f), (0.25f, 0.56f), (0.35f, 0.32f), (0.45f, 0.1f), (0.58f, -0.12f), (1f, -0.55f) },
            new[] { (0f, 0f), (0.6f, 0f), (0.75f, 0.3f), (0.9f, 0.65f), (1f, 1.02f) },
            new[] { (0f, 0f), (0.65f, 0f), (0.8f, 0.32f), (0.92f, 0.7f), (1f, 1.02f) });

        var bse = Sys(Child(root, "FireballBase", new Vector3(0f, 0.3f, 0f)), m.fireball, ParticleSystemRenderMode.Mesh, 0f, 18, 0.03f);
        Fireball(bse, sphere, count: 14, life: (1.7f, 2.0f), speed: (0.6f, 1.4f), size: (0.55f, 0.8f));
        Shape(bse, ParticleSystemShapeType.Cone, 80f, 1.1f, rotation: new Vector3(-90f, 0f, 0f), thickness: 0.4f);
        SizeOver(bse, (0f, 0.5f), (0.02f, 0.92f), (0.4f, 1.05f), (1f, 1.1f));
        Heat(bse,
            new[] { (0f, 1.12f), (0.08f, 1.02f), (0.3f, 0.8f), (0.5f, 0.5f), (0.65f, 0.25f), (0.8f, 0f), (1f, -0.25f) },
            new[] { (0f, 1.02f), (0.08f, 0.92f), (0.3f, 0.7f), (0.5f, 0.4f), (0.65f, 0.15f), (0.8f, -0.1f), (1f, -0.35f) },
            new[] { (0f, 0f), (0.5f, 0f), (0.7f, 0.4f), (0.88f, 0.85f), (1f, 1.02f) },
            new[] { (0f, 0f), (0.55f, 0f), (0.74f, 0.45f), (0.9f, 0.9f), (1f, 1.02f) });

        // ④ 연기 고리 · 흰 파편 ─────────────────────────────────────────
        var smoke = Sys(Child(root, "SmokeRing", new Vector3(0f, 0.65f, 0f)), m.puff, ParticleSystemRenderMode.Billboard, 0f, 32, 0.04f);
        Burst(smoke, 22); Life(smoke, 0.8f, 1.3f); Speed(smoke, 4f, 8f); Size(smoke, 1.6f, 2.6f);
        Shape(smoke, ParticleSystemShapeType.Cone, 84f, 0.9f, rotation: new Vector3(-90f, 0f, 0f), thickness: 0f);
        Drag(smoke, 1.2f); RandomRotation(smoke); Spin(smoke, 40f); StartColor(smoke, Color.white);
        SizeOver(smoke, (0f, 0.5f), (0.3f, 1f), (1f, 1.4f));
        ColorOver(smoke, new[] { (SmokeWarm, 0f), (new Color(0.55f, 0.41f, 0.37f), 0.3f), (SmokeGrey, 1f) }, new[] { (0f, 0f), (0.95f, 0.06f), (0.7f, 0.5f), (0f, 1f) });

        var shards = Sys(Child(root, "Shards", new Vector3(0f, 0.2f, 0f)), m.shard, ParticleSystemRenderMode.Stretch, -0.5f, 24, 0.05f);
        Burst(shards, 11); Life(shards, 0.9f, 1.3f); Speed(shards, 3f, 5f); Size(shards, 0.85f, 1.3f);
        // 고리 테두리에서 바깥 + 위로(원뿔 46° = 44° 들림) — 영상의 왕관처럼 바깥으로 비스듬히 선 날. 머리가 바깥, 날은 안쪽으로 끌린다
        Shape(shards, ParticleSystemShapeType.Cone, 46f, 2.3f, rotation: new Vector3(-90f, 0f, 0f), thickness: 0f);
        Stretch(shards, 2.2f, 0f); Drag(shards, 2f); Flip(shards, 0f, 0.5f); StartColor(shards, ShardWhite);
        SizeOver(shards, (0f, 0.6f), (0.1f, 1f), (0.6f, 0.85f), (1f, 0f));
        ColorOver(shards, new[] { (Color.white, 0f), (new Color(0.95f, 1f, 1.05f), 1f) }, new[] { (1f, 0f), (0.9f, 0.7f), (0f, 1f) });

        // ⑤ 불똥 ─────────────────────────────────────────────────────
        var sparks = Sys(Child(root, "Sparks", new Vector3(0f, 0.5f, 0f)), m.ember, ParticleSystemRenderMode.Stretch, -1.5f, 64, 0.02f);
        Burst(sparks, 45); Life(sparks, 0.45f, 1.3f); Speed(sparks, 4f, 13f); Size(sparks, 0.05f, 0.11f); Gravity(sparks, 0.5f);
        Shape(sparks, ParticleSystemShapeType.Hemisphere, 0f, 0.4f, rotation: new Vector3(-90f, 0f, 0f));
        Stretch(sparks, 2.2f, 0.04f); Drag(sparks, 0.6f); StartColor(sparks, Color.white);
        ColorOver(sparks, new[] { (SparkHot, 0f), (SparkMid, 0.4f), (SparkEnd, 1f) }, new[] { (1f, 0f), (1f, 0.6f), (0f, 1f) });

        var embers = Sys(Child(root, "Embers", new Vector3(0f, 1.2f, 0f)), m.ember, ParticleSystemRenderMode.Billboard, -1f, 80, 0.3f);
        { var main = embers.main; main.duration = 1.5f; }
        Rate(embers, 42f); Life(embers, 0.6f, 1.3f); Speed(embers, 0.1f, 0.6f); Size(embers, 0.04f, 0.08f); StartColor(embers, SparkMid);
        Shape(embers, ParticleSystemShapeType.Sphere, 0f, 1.1f);
        Rise(embers, (0f, 0.4f), (1f, 0.9f)); Noise(embers, 0.4f, 0.6f, 0.3f);
        ColorOver(embers, new[] { (Color.white, 0f), (Color.white, 1f) }, new[] { (0f, 0f), (1f, 0.15f), (0.8f, 0.7f), (0f, 1f) });

        // ⑥ 바닥 용암 균열 · 끝 연기 줄 ─────────────────────────────────
        var cracks = Sys(Child(root, "Cracks", new Vector3(0f, 0.03f, 0f)), m.cracks, ParticleSystemRenderMode.HorizontalBillboard, 3f, 2, 0.06f);
        Burst(cracks, 1); Life(cracks, 3f, 3f); Size(cracks, 5f, 5f); RandomRotation(cracks); StartColor(cracks, Color.white);
        // 알파 = 셰이더의 "남은 양" — 0 → 1(0.36초, 가운데부터 퍼지며 생긴다) → 1 유지 → 0(토막토막 꺼진다)
        ColorOver(cracks, new[] { (Color.white, 0f), (new Color(1f, 0.86f, 0.76f), 0.6f), (new Color(0.92f, 0.62f, 0.52f), 1f) }, new[] { (0f, 0f), (1f, 0.12f), (1f, 0.6f), (0f, 1f) });

        var wisps = Sys(Child(root, "Wisps", new Vector3(0f, 0.1f, 0f)), m.wisp, ParticleSystemRenderMode.Stretch, 0f, 16, 1.5f);
        { var main = wisps.main; main.duration = 0.8f; }
        Rate(wisps, 9f); Life(wisps, 0.8f, 1.3f); Speed(wisps, 0f, 0f); Size(wisps, 0.06f, 0.1f); StartColor(wisps, new Color(0.62f, 0.6f, 0.62f));
        Shape(wisps, ParticleSystemShapeType.Cone, 0f, 0.8f, rotation: new Vector3(-90f, 0f, 0f));
        Stretch(wisps, 1f, 0.35f);
        Rise(wisps, (0f, 0.9f), (1f, 1.5f));
        ColorOver(wisps, new[] { (Color.white, 0f), (Color.white, 1f) }, new[] { (0f, 0f), (0.35f, 0.2f), (0f, 1f) });
    }

    /// 구체 불덩이 공통 — 메시 파티클 + 셰이더 스트림(순서 = ToonFireball 의 TEXCOORD 자리).
    static void Fireball(ParticleSystem ps, Mesh sphere, int count, (float a, float b) life, (float a, float b) speed, (float a, float b) size)
    {
        Burst(ps, count); Life(ps, life.a, life.b); Speed(ps, speed.a, speed.b); Size(ps, size.a, size.b); Drag(ps, 1.5f);
        StartColor(ps, Color.white);
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = sphere;
        r.alignment = ParticleSystemRenderSpace.Local;
        // TEXCOORD0 = Custom1.xy(열 · 디졸브) + StableRandom.x + SizeX, TEXCOORD1 = Center.xyz — ToonFireball 의 Attributes 와 같은 순서
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.Custom1XY, ParticleSystemVertexStream.StableRandomX, ParticleSystemVertexStream.SizeX,
            ParticleSystemVertexStream.Center,
        });
        var main = ps.main;
        main.startRotation3D = true;   // 모양은 무늬(시드)가 바꾼다 — 회전은 메시 극점 모임만 숨긴다
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
    }

    /// Custom1.x = 열(1.1 → −0.5, 식으며 연기로), Custom1.y = 디졸브(0 → 1) — 두 곡선 사이 무작위.
    /// 덩어리 안 높이(위부터 식고 아래부터 사라진다)는 셰이더가 루트 바닥(_Ground, ToonExplosionAnchor)에서 잰다.
    static void Heat(ParticleSystem ps, (float t, float v)[] heatA, (float t, float v)[] heatB, (float t, float v)[] dissA, (float t, float v)[] dissB)
    {
        var cd = ps.customData;
        cd.enabled = true;
        cd.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
        cd.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 2);
        cd.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(1f, Linear(heatA), Linear(heatB)));
        cd.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(1f, Linear(dissA), Linear(dissB)));
    }

    // ── 파티클 도우미(InfernoFlameSetup 과 같은 모양) ────────────────────
    static GameObject Child(GameObject parent, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        return go;
    }

    /// 원샷 · 로컬 공간 · Hierarchy 스케일(ctx.scale 이 루트 배율로 전부 키운다). 큐는 머티리얼, 같은 큐 안 앞뒤는 fudge.
    static ParticleSystem Sys(GameObject go, Material mat, ParticleSystemRenderMode mode, float fudge, int max, float delay)
    {
        var ps = go.GetComponent<ParticleSystem>();
        if (ps == null) ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1f; main.loop = false; main.playOnAwake = true; main.prewarm = false;
        main.startDelay = delay;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = max;
        main.stopAction = ParticleSystemStopAction.None;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f);
        var em = ps.emission;
        em.enabled = true; em.rateOverTime = 0f; em.rateOverDistance = 0f;
        em.SetBursts(new ParticleSystem.Burst[0]);
        var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = mode;
        r.alignment = ParticleSystemRenderSpace.View;
        r.sortingFudge = fudge;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.minParticleSize = 0f;
        r.maxParticleSize = 5f;   // 기본 0.5(화면 몫) — 가까운 카메라에서 섬광 · 균열이 잘린다
        return ps;
    }

    static void Life(ParticleSystem ps, float a, float b) { var m = ps.main; m.startLifetime = Range(a, b); }
    static void Speed(ParticleSystem ps, float a, float b) { var m = ps.main; m.startSpeed = Range(a, b); }
    static void Size(ParticleSystem ps, float a, float b) { var m = ps.main; m.startSize = Range(a, b); }
    static void Gravity(ParticleSystem ps, float g) { var m = ps.main; m.gravityModifier = g; }
    static void StartColor(ParticleSystem ps, Color c) { var m = ps.main; m.startColor = new ParticleSystem.MinMaxGradient(c); }
    static ParticleSystem.MinMaxCurve Range(float a, float b) => Mathf.Approximately(a, b) ? new ParticleSystem.MinMaxCurve(a) : new ParticleSystem.MinMaxCurve(a, b);

    static void Burst(ParticleSystem ps, int count)
    {
        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    static void Rate(ParticleSystem ps, float perSecond)
    {
        var em = ps.emission; em.enabled = true; em.rateOverTime = perSecond;
    }

    /// thickness = 반경 두께(1 = 속까지, 0 = 겉면/테두리만). 원뿔 + 테두리만 = 고리에서 바깥으로 기울어 퍼진다(각도 85° ≈ 바닥을 따라).
    /// 함정(11-12): Circle 모양을 눕히면(-90° X) 속도에 아래 성분이 섞였다 — 바닥 고리는 원뿔 테두리로 낸다.
    static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float angle, float radius, Vector3 rotation = default, float thickness = 1f)
    {
        var sh = ps.shape;
        sh.enabled = true; sh.shapeType = type;
        sh.angle = angle; sh.radius = radius; sh.radiusThickness = thickness; sh.arc = 360f;
        sh.position = Vector3.zero; sh.rotation = rotation; sh.scale = Vector3.one;
        sh.randomDirectionAmount = 0f; sh.sphericalDirectionAmount = 0f;
    }

    /// 끌림 — 속도 곱(제곱 끌림): 빨리 튀어 나가 금방 선다. (Unity 끌림은 지수 감쇠가 아니다 — InfernoFlameSetup.Drag 주석)
    static void Drag(ParticleSystem ps, float d)
    {
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true; lim.separateAxes = false;
        lim.limit = new ParticleSystem.MinMaxCurve(1000f); lim.dampen = 0f;
        lim.drag = new ParticleSystem.MinMaxCurve(d);
        lim.multiplyDragByParticleSize = false; lim.multiplyDragByParticleVelocity = true;
    }

    static void RandomRotation(ParticleSystem ps) { var m = ps.main; m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); }

    static void Spin(ParticleSystem ps, float degPerSec)
    {
        var rol = ps.rotationOverLifetime; rol.enabled = true; rol.separateAxes = false;
        float r = degPerSec * Mathf.Deg2Rad;
        rol.z = new ParticleSystem.MinMaxCurve(-r, r);
    }

    /// 입자 몫만큼 좌우(위아래) 뒤집기 — 한 장짜리 텍스처의 반복을 숨긴다.
    /// 함정(11-12): 늘인 빌보드는 U 가 진행 방향이라 x(U) 를 뒤집으면 날 끝 · 뿌리가 바뀐다 — 늘인 것은 y(V, 폭 방향)만 뒤집는다.
    static void Flip(ParticleSystem ps, float x, float y) { ps.GetComponent<ParticleSystemRenderer>().flip = new Vector3(x, y, 0f); }

    static void Stretch(ParticleSystem ps, float length, float velocity)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = length; r.velocityScale = velocity; r.cameraVelocityScale = 0f;
    }

    static void SizeOver(ParticleSystem ps, params (float t, float v)[] keys)
    {
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.separateAxes = false;
        sol.size = new ParticleSystem.MinMaxCurve(1f, Curve(keys));
    }

    static void ColorOver(ParticleSystem ps, (Color c, float t)[] colors, (float a, float t)[] alphas)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(colors.Select(k => new GradientColorKey(k.c, k.t)).ToArray(), alphas.Select(k => new GradientAlphaKey(k.a, k.t)).ToArray());
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    /// 수명 따라 위로(월드 m/s). 속도-수명 x · y · z 는 같은 모드여야 한다 — 셋 다 곡선.
    static void Rise(ParticleSystem ps, params (float t, float v)[] y)
    {
        var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
        var zero = Curve((0f, 0f), (1f, 0f));
        v.x = new ParticleSystem.MinMaxCurve(1f, zero);
        v.y = new ParticleSystem.MinMaxCurve(1f, Curve(y));
        v.z = new ParticleSystem.MinMaxCurve(1f, zero);
        v.speedModifier = new ParticleSystem.MinMaxCurve(1f);
    }

    static void Noise(ParticleSystem ps, float strength, float frequency, float scroll)
    {
        var nz = ps.noise;
        nz.enabled = true; nz.separateAxes = false;
        nz.strength = new ParticleSystem.MinMaxCurve(strength);
        nz.frequency = frequency; nz.scrollSpeed = new ParticleSystem.MinMaxCurve(scroll);
        nz.damping = true; nz.octaveCount = 2; nz.octaveMultiplier = 0.5f; nz.octaveScale = 2f;
        nz.quality = ParticleSystemNoiseQuality.Medium;
    }

    static AnimationCurve Curve(params (float t, float v)[] keys)
    {
        var c = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    /// 꺾은선 곡선 — 열 · 디졸브는 오버슈트가 없어야 한다(부드러운 접선은 0 아래/1 위로 튄다).
    static AnimationCurve Linear(params (float t, float v)[] keys)
    {
        var c = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Linear);
        }
        return c;
    }

    /// 불덩이 혹 메시 — 아이코스피어 2단(정점 162 · 삼각형 320, 반지름 0.5). 내장 구(768 삼각형)의 반도 안 되는데
    /// 셰이더가 노이즈로 울퉁불퉁하게 밀고 카툰 색 계단이 면을 가려 차이가 안 보인다 — 그만큼 혹을 더 많이 · 작게 쓴다(영상은 혹 40개쯤).
    /// 같은 경로 에셋을 고쳐 쓴다(GUID 유지 — 프리팹 참조 그대로).
    static Mesh BallMesh()
    {
        var verts = new List<Vector3>();
        var cache = new Dictionary<long, int>();
        int Mid(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int i)) return i;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            cache[key] = verts.Count - 1;
            return verts.Count - 1;
        }
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        foreach (var v in new[] { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                                  new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                                  new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) })
            verts.Add(v.normalized);
        var tris = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                                   3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        for (int s = 0; s < 2; s++)
        {
            var next = new List<int>(tris.Count * 4);
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            tris = next;
        }
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BallMeshPath);
        bool create = mesh == null;
        if (create) mesh = new Mesh { name = "Mesh_ToonExploBall" };
        mesh.Clear();
        mesh.SetVertices(verts.Select(v => v * 0.5f).ToList());
        mesh.SetNormals(verts);
        mesh.SetUVs(0, verts.Select(v => new Vector2(0.5f + Mathf.Atan2(v.z, v.x) / (2f * Mathf.PI), 0.5f + Mathf.Asin(Mathf.Clamp(v.y, -1f, 1f)) / Mathf.PI)).ToList());
        // 감기 방향: 위 표는 (b−a)×(c−a) 가 바깥 — Unity 앞면 규칙과 같다(뒤집으면 안이 보인다, 11-12 렌더로 확인)
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        if (create) AssetDatabase.CreateAsset(mesh, BallMeshPath);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // ── 텍스처 · 머티리얼 ─────────────────────────────────────────────
    static Texture2D ImportTex(string path, bool linear)
    {
        if (!File.Exists(path)) return null;
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); imp = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (imp != null)
        {
            var comp = linear ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            bool dirty = imp.textureType != TextureImporterType.Default || imp.alphaIsTransparency == linear || imp.wrapMode != TextureWrapMode.Clamp ||
                         !imp.mipmapEnabled || imp.sRGBTexture == linear || imp.textureCompression != comp;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaIsTransparency = !linear;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = true;
                imp.sRGBTexture = !linear;          // 균열은 데이터(R 거리 · G 순서 · B 그을음) — 감마를 타면 문턱이 어긋난다
                imp.textureCompression = comp;      // 데이터는 압축 안 함(BC 블록이 가는 균열 거리값을 뭉갠다)
                imp.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material ShaderMat(string name, Shader shader, Texture2D tex, int queue)
    {
        string path = $"{Dir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = m == null;
        if (create) m = new Material(shader) { name = name };
        else if (m.shader != shader) m.shader = shader;
        if (tex != null && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        m.renderQueue = queue;
        m.enableInstancing = false;
        if (create) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return m;
    }

    /// URP Particles/Unlit — 알파 블렌드 또는 가산. 큐 3000(연기만 2980 — 불덩이 아래). 가산 · 밝은 날은 _BaseColor 로 1 을 넘겨 블룸(입자 색은 8비트).
    static Material Mat(string name, Texture2D tex, bool additive, float boost, int queue = 3000)
    {
        string path = $"{Dir}/{name}.mat";
        var shader = Shader.Find(FxShaderUnify.ShaderName);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = m == null;
        if (create) m = new Material(shader) { name = name };
        else if (m.shader != shader) m.shader = shader;
        m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        m.SetColor("_BaseColor", new Color(boost, boost, boost, 1f));
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        if (m.HasProperty("_ColorMode")) m.SetFloat("_ColorMode", 0f);
        if (m.HasProperty("_FlipbookBlending")) m.SetFloat("_FlipbookBlending", 0f);
        if (m.HasProperty("_SoftParticlesEnabled")) m.SetFloat("_SoftParticlesEnabled", 0f);
        if (m.HasProperty("_CameraFadingEnabled")) m.SetFloat("_CameraFadingEnabled", 0f);
        if (m.HasProperty("_EmissionEnabled")) m.SetFloat("_EmissionEnabled", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        foreach (var k in new[] { "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON", "_ALPHATEST_ON", "_EMISSION", "_FLIPBOOKBLENDING_ON", "_SOFTPARTICLES_ON", "_FADING_ON", "_COLORCOLOR_ON", "_COLOROVERLAY_ON", "_COLORADDSUBDIFF_ON" })
            m.DisableKeyword(k);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        // URP 파티클 셰이더 GUI(ValidateMaterial)가 큐를 3000 + _QueueOffset 으로 되돌린다 — 오프셋으로 건다(11-12 렌더로 확인)
        if (m.HasProperty("_QueueOffset")) m.SetFloat("_QueueOffset", queue - 3000);
        m.renderQueue = queue;
        if (create) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    // ── 미리보기 렌더 ─────────────────────────────────────────────────
    /// 숨은 미리보기 씬에 프리팹을 세워 시각마다 시뮬레이션하고 두 각도로 찍는다(블룸 없음).
    /// video.png = 레퍼런스 영상과 비슷한 낮은 각도 12컷, game.png = 게임 카메라(피치 58°) 6컷. 돌려주는 값 = 저장한 파일들.
    public static List<string> RenderPreview() => RenderPreview(Vector3.zero, "");

    /// origin = 폭발을 놓을 자리(바닥 높이를 바꿔 셰이더 높이 항이 자리와 무관한지 본다), tag = 파일 이름 뒤에 붙는 말.
    public static List<string> RenderPreview(Vector3 origin, string tag)
    {
        var saved = new List<string>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogWarning("[카툰 폭발] 미리보기 — 프리팹이 없다"); return saved; }
        Directory.CreateDirectory(PreviewDir);
        var log = new StringBuilder();
        var temp = new List<Object>();
        bool asyncWas = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;   // 첫 렌더가 컴파일 중 대역(하늘색) 셰이더로 찍히지 않게
        var pru = new PreviewRenderUtility();
        try
        {
            var cam = pru.camera;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.215f, 0.25f, 1f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
            cam.allowHDR = false;
            pru.lights[0].intensity = 0f; pru.lights[1].intensity = 0f;

            var world = new GameObject("ToonExplosionPreviewWorld");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.transform.SetParent(world.transform, false);
            ground.transform.localPosition = origin;
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            var gm = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave };
            temp.Add(gm);
            gm.SetColor("_BaseColor", new Color(0.24f, 0.26f, 0.3f, 1f));
            ground.GetComponent<MeshRenderer>().sharedMaterial = gm;
            var fx = (GameObject)Object.Instantiate(prefab, world.transform);
            fx.transform.localPosition = origin;
            var anchor = fx.GetComponent<ToonExplosionAnchor>();
            if (anchor != null) anchor.Apply(true);
            pru.AddSingleGO(world);
            var all = fx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var p in all) { p.useAutoRandomSeed = false; p.randomSeed = 1234; }

            float[] times = { 0.0f, 0.05f, 0.12f, 0.22f, 0.35f, 0.55f, 0.8f, 1.15f, 1.55f, 1.95f, 2.4f, 2.85f };
            const int tw = 480, th = 300;
            var sheetVideo = new Texture2D(tw * 4, th * 3, TextureFormat.RGBA32, false);
            var sheetGame = new Texture2D(tw * 3, th * 2, TextureFormat.RGBA32, false);
            int gi = 0;
            for (int i = 0; i < times.Length; i++)
            {
                float t = times[i];
                foreach (var p in all) p.Simulate(t + 0.0001f, false, true, false);
                if (i == 7) log.AppendLine(Extents(all));
                // 영상 각도 — 피치 18°, 거리 10m(영상처럼 불덩이가 화면 폭의 3할쯤)
                cam.fieldOfView = 30f;
                var rot = Quaternion.Euler(18f, 0f, 0f);
                cam.transform.SetPositionAndRotation(origin + new Vector3(0f, 1.3f, 0f) - rot * Vector3.forward * 10f, rot);
                var shot = Capture(pru, tw, th);
                sheetVideo.SetPixels((i % 4) * tw, (2 - i / 4) * th, tw, th, shot.GetPixels());
                Object.DestroyImmediate(shot);
                if (i % 2 == 0 && gi < 6)
                {
                    // 게임 카메라 — 피치 58°, 세로 FOV 42°, 17.8m 를 가운데만 확대(같은 화면 크기 비율)
                    cam.fieldOfView = 21f;
                    rot = Quaternion.Euler(58f, 0f, 0f);
                    cam.transform.SetPositionAndRotation(origin + new Vector3(0f, 0.6f, 0f) - rot * Vector3.forward * 17.8f, rot);
                    shot = Capture(pru, tw, th);
                    sheetGame.SetPixels((gi % 3) * tw, (1 - gi / 3) * th, tw, th, shot.GetPixels());
                    Object.DestroyImmediate(shot);
                    gi++;
                }
                log.AppendLine($"t={t:0.00} alive=" + string.Join(" ", all.Where(p => p.particleCount > 0).Select(p => $"{p.name}:{p.particleCount}")));
            }
            sheetVideo.Apply(); sheetGame.Apply();
            string a = Path.Combine(PreviewDir, $"video{tag}.png"), b = Path.Combine(PreviewDir, $"game{tag}.png");
            File.WriteAllBytes(a, sheetVideo.EncodeToPNG());
            File.WriteAllBytes(b, sheetGame.EncodeToPNG());
            Object.DestroyImmediate(sheetVideo); Object.DestroyImmediate(sheetGame);
            saved.Add(a); saved.Add(b);
            foreach (var sh in new[] { Shader.Find(FireballShader), Shader.Find(CrackShader) })
            {
                if (sh == null) { log.AppendLine("shader missing"); continue; }
                log.AppendLine($"{sh.name}: supported={sh.isSupported} hasError={ShaderUtil.ShaderHasError(sh)}");
                foreach (var msg in ShaderUtil.GetShaderMessages(sh)) log.AppendLine($"  [{msg.severity}] {msg.message} ({msg.file}:{msg.line})");
            }
            Debug.Log($"[카툰 폭발] 미리보기 → {PreviewDir}/video.png · game.png");
        }
        catch (Exception e) { log.AppendLine("EXCEPTION " + e); Debug.LogError("[카툰 폭발] 미리보기 실패: " + e); }
        finally
        {
            ShaderUtil.allowAsyncCompilation = asyncWas;
            pru.Cleanup();   // AddSingleGO 로 넣은 세계도 같이 파괴된다
            foreach (var o in temp) if (o != null) Object.DestroyImmediate(o);
            try { File.WriteAllText(Path.Combine(PreviewDir, "log.txt"), log.ToString()); } catch (Exception) { }
        }
        return saved;
    }

    static string Extents(ParticleSystem[] all)
    {
        var sb = new StringBuilder("extents@1.15s:");
        var buf = new ParticleSystem.Particle[256];
        foreach (var p in all)
        {
            int n = p.GetParticles(buf);
            if (n == 0) continue;
            var b = new Bounds(p.transform.TransformPoint(buf[0].position), Vector3.zero);
            for (int i = 1; i < n; i++) b.Encapsulate(p.transform.TransformPoint(buf[i].position));
            sb.Append($" {p.name}[{b.min.x:0.0}..{b.max.x:0.0}, y {b.min.y:0.0}..{b.max.y:0.0}]");
        }
        return sb.ToString();
    }

    static Texture2D Capture(PreviewRenderUtility pru, int w, int h)
    {
        pru.BeginStaticPreview(new Rect(0, 0, w, h));
        pru.Render(true);
        var tex = pru.EndStaticPreview();
        if (tex.width == w && tex.height == h) return tex;
        var rt = RenderTexture.GetTemporary(w, h, 0);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        outTex.ReadPixels(new Rect(0, 0, w, h), 0, 0); outTex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(tex);
        return outTex;
    }
}
