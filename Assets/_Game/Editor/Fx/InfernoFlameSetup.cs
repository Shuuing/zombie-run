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
/// 인페르노(화염방사기 진화) 화염 — 컨셉 아트를 <b>파티클로</b> 짠다(2026-11-06 사용자: "첨부 이미지(컨셉 아트) 불 이펙트로, Texture Sheet 애니메이션 말고 파티클로").
///
/// <para><b>컨셉 → 시스템.</b> 텍스처는 전부 한 장짜리 모양(플립북 칸 없음, <c>ArtSource/VFX/bake_inferno_textures.py</c>)이고 Texture Sheet Animation 모듈은 하나도 안 쓴다.
/// 움직임 · 색 변화는 크기 · 회전 · 수명 색 · 노이즈 · 끌림이 만든다.</para>
/// <list type="bullet">
/// <item>루트(본체) — 불 뭉치(알파, v2 부터 노이즈로 깎은 사실적인 텍스처). 총구에서 노란-흰색으로 가늘게 나와 주황 → 붉은 주황으로 부풀고 끝에서 검붉게 식는다(끌림으로 끝에서 느려지며 모인다).</item>
/// <item>Shade — 본체 뒤의 더 어둡고 큰 붉은 뭉치(컨셉의 아랫면 · 바깥 말림 그늘). Core — 가산 노란-흰 심(블룸). Head — 총구 불(멈춘 뒤 잠깐 남는다).</item>
/// <item>Licks — 가장자리 불꽃 혀(가산, 위로 핀다). Embers — 둘레로 튀는 작은 불꽃 조각(가산 혀). Sparks — 불똥 점. Fragments — 끝에서 · 멈출 때 떨어지는 불덩이 조각.</item>
/// <item>Smoke — 검은 연기(방출 0 — ProtoFlamePack 이 불 입자가 수명 tipAge 를 넘는 자리에서 낸다 = 불끝이 연기가 된다).</item>
/// <item>Charge… — 쏘기 시작할 때 한 번(총구 섬광 · 불똥 · 링 = 컨셉 1. Charge). Ground… — 바닥 빛 · 바닥 불 고리 · 바닥 불꽃(컨셉 Additional Effects).</item>
/// </list>
/// <para>흐름(Charge → Start → Sustain → Fade Out)은 런타임 <see cref="ProtoFlamePack"/>(방출 램프 · 끝 연기 · 멈출 때 연기/파편/불똥 · 사그라들기)과
/// 프리팹 루트의 <see cref="ProtoFlameJetTuning"/>(그 값들 + 점화 · 바닥 자리)이 맡는다. 값은 <b>미터</b>(사거리 6m 에서 루트 배율 ≈ 1).</para>
///
/// <para>적용: 카탈로그 <c>inferno_jet</c>(스팬, 수명 0) 프리팹 = <see cref="PrefabPath"/>, <c>WFX_Inferno.jet</c> = inferno_jet(비어 있을 때만).
/// 에디터가 뜰 때 판(<see cref="Version"/>)이 오르면 다시 굽는다(표식 Library/InfernoFlame.applied). 되돌리기: WFX_Inferno.jet 을 비우면 예전 ProtoFlameCone.
/// 프리팹을 에디터에서 손으로 고쳤으면(파일이 표식보다 새것) 판이 올라도 <b>텍스처 · 머티리얼만</b> 바꾸고 프리팹은 그대로 둔다
/// (표식 Library/InfernoFlame.handedited — 메뉴 '인페르노 화염 다시 굽기' 만 지우고 프리팹까지 새로 굽는다).</para>
///
/// <para><b>미리보기</b>: 메뉴 '인페르노 화염 미리보기 렌더' 또는 <c>Library/InfernoPreview/request</c> 파일 — 숨은 미리보기 씬에서 같은 프리팹을 ProtoFlamePack 으로 세워
/// 점화 · 시작 · 지속 · 사그라듦을 시뮬레이션하고 <c>Library/InfernoPreview/inferno_preview.png</c> 로 찍는다(블룸 없음 — 게임에선 심이 더 빛난다).</para>
/// </summary>
public static class InfernoFlameSetup
{
    // 2026-11-23 사용자 작업 폴더(Art/Sujin/VFX/Weapon/Inferno)로 옮겼다(옛 Art/VFX/Weapons/Inferno). 자리는 프리팹 GUID 로 찾는다 —
    // 폴더를 또 옮겨도 따라가고, 옮기는 도중(에디터가 아직 옛 자리로 알 때)에 "프리팹이 없다"며 새로 굽지 않게.
    const string PrefabGuid = "cf81262d319599e49bb2b7438dd47f6d";
    const string DefaultDir = "Assets/_Game/Art/Sujin/S_VFX/Weapon/Inferno";
    /// 텍스처 T_Inferno_* 자리 — 11-25 부터 Sujin 텍스처는 S_Textures 한 곳(ArtSource/VFX/bake_inferno_textures.py 도 여기에 굽는다).
    const string TexDir = "Assets/_Game/Art/Sujin/S_Textures/VFX";
    public static string Dir
    {
        get
        {
            string p = AssetDatabase.GUIDToAssetPath(PrefabGuid);
            return !string.IsNullOrEmpty(p) && File.Exists(p) ? Path.GetDirectoryName(p).Replace('\\', '/') : DefaultDir;
        }
    }
    public static string PrefabPath => Dir + "/FX_Inferno_Jet.prefab";
    const string FxSetPath = "Assets/_Game/Data/Weapons/Fx/WFX_Inferno.asset";
    const string StatsPath = "Assets/_Game/Data/Weapons/Stats/WS_Inferno.asset";
    static readonly string Marker = Path.Combine("Library", "InfernoFlame.applied");
    static readonly string HandEditedMarker = Path.Combine("Library", "InfernoFlame.handedited");
    static readonly string PreviewDir = Path.Combine("Library", "InfernoPreview");
    static readonly string PreviewRequest = Path.Combine("Library", "InfernoPreview", "request");

    /// 굽기 규칙의 판. 올리면 에디터가 뜰 때 프리팹 · 머티리얼을 다시 굽는다(같은 경로 — GUID 유지, 카탈로그 · 세트 참조 그대로).
    /// v1(11-06): 첫 판 — 컨셉 아트 화염(본체 · 그늘 · 심 · 총구 · 혀 · 조각 · 불똥 · 파편 · 연기 · 점화 3 · 바닥 3).
    /// v2(11-06): 사용자 "텍스쳐가 너무 2D 그림 — 좀 더 리얼하게" → 텍스처를 노이즈로 다시 굽고(bake_inferno_textures.py v2),
    ///            그을음 · 연기를 보라에서 숯 회색으로, 불 뭉치 머티리얼 밝기 1.45(어두운 골이 생긴 만큼), 혀는 가산, 입자 반은 뒤집어 같은 모양이 덜 보이게.
    /// v3(11-06): 사용자 "너무 실사 느낌, 좀 더 캐쥬얼하게" → 텍스처를 v1(카툰 그림) · v2(실사) 사이로 — 동글동글한 혹 덩어리 · 매끈한 불꽃,
    ///            부드러운 2톤 명암(빛 방향 없음 — 입자가 돌고 뒤집혀도 맞다), 테두리 선 · 잔결 없음. <b>텍스처 · 머티리얼만</b> 바꾼다:
    ///            불 뭉치 밝기 1.1(밝아진 텍스처에 맞춰), 연기는 머티리얼 색으로 밝은 보랏빛 회색(시작 색 × 1.5/1.42/1.65).
    ///            사용자가 프리팹을 손으로 고쳐 둔 상태(점화 3종 · 바닥 불꽃 끔)라 손편집 보호 경로로 프리팹은 그대로 둔다.
    /// v4(11-06): 사용자 "불꽃과 연기 텍스쳐에 검은색 테두리 같은 건 빼줘, 경계면은 페더 먹은 것처럼 그라데이션, 불씨 같은 건 서클로" →
    ///            불 뭉치 · 연기 · 혀 = 모양 마스크를 가우스로 흐린 페더 + 가장자리는 늘 밝게, 새 T_Inferno_Ember(페더 먹은 원) · M_Inferno_EmberDot(가산),
    ///            Embers · Fragments 가 그 원을 쓴다(Fragments 크기 0.3~0.6 → 0.12~0.26), 바닥 불 텍스처의 불씨도 동그란 원.
    ///            손편집 프리팹에는 <see cref="PatchPrefab"/> 로 그 두 렌더러 머티리얼(과 기본값 그대로인 Fragments 크기)만 바꾼다.
    /// v5(11-06): 사용자 "불씨가 너무 물방울 모양, 좀 더 티끌처럼"(메인 에디터는 프리팹 모드가 열려 있어 v4 원 연결이 미뤄진 채 옛 물방울 혀를 보고 있었다) →
    ///            T_Inferno_Ember = 작고 단단한 심 + 옅은 번짐(티끌), M_Inferno_EmberDot 밝기 2.0, Embers 0.05~0.12m · 초당 40, Fragments 0.04~0.1m · 멈출 때 36개.
    ///            좁은 고침은 프리팹 모드가 열려 있으면 그 열린 프리팹에 바로 넣는다(되돌리기 가능) — 미루면 사용자가 바뀐 걸 못 본다.
    public const int Version = 5;

    // ── 색(컨셉 아트에서) ─────────────────────────────────────────────
    static readonly Color HotWhite = new Color(1f, 0.92f, 0.7f);      // v2: 덜 하얗게 — 반투명일 때 회색 바닥 위에서 누렇게(올리브) 뜨지 않게
    static readonly Color Yellow = new Color(1f, 0.8f, 0.32f);
    static readonly Color Amber = new Color(1f, 0.72f, 0.2f);
    static readonly Color Orange = new Color(1f, 0.52f, 0.12f);
    static readonly Color RedOrange = new Color(0.94f, 0.34f, 0.09f);
    static readonly Color Ember = new Color(0.75f, 0.22f, 0.1f);
    static readonly Color Soot = new Color(0.13f, 0.1f, 0.09f);       // 불끝 — 그을음(연기로 이어진다). v1 은 컨셉의 보랏빛, v2 부터 실제 연료 그을음
    static readonly Color SmokeA = new Color(0.2f, 0.185f, 0.18f);    // 연기 시작 색 — 화면 색은 × M_Inferno_Smoke 색(SmokeTint, v3 밝은 보랏빛 회색). v1 은 보랏빛 회색, v2 는 짙은 숯 회색
    static readonly Color SmokeB = new Color(0.34f, 0.32f, 0.3f);
    static readonly Color SmokeTint = new Color(1.5f, 1.42f, 1.65f);  // v3 — 프리팹(시작 색)을 안 건드리고 머티리얼로 캐주얼한 밝은 연기

    /// ProtoFlamePack 에 넘길 맞춤값(프리팹 루트 ProtoFlameJetTuning). 미터 단위 · 인페르노 판정 45° · 사거리 6m 기준.
    static ProtoFlameJetTuning.Knobs Knobs() => new ProtoFlameJetTuning.Knobs
    {
        visibleFraction = 0.53f,   // ProtoFlamePack 은 "속도 × 수명"(속도 배수 전)을 잰다 — 본체가 꽉 찬 끝(수명 72%)은 그 0.48 지점, 그게 사거리의 0.9 에 오게(불덩이 반지름이 나머지)
        muzzleForward = 0f,
        coneFraction = 0.75f,      // 판정 45° → 보이는 부채꼴 34°(입자 크기가 더해져 판정 폭과 비슷하게 읽힌다)
        flatten = 0.4f,
        fadeOut = 0.4f, snapOff = 0.1f, fadeDrag = 2.5f, fadeRise = 3f,
        rampIn = 0.12f, muzzleLinger = 0.4f,
        tipAge = 0.68f,            // 불끝 — 알파가 꺾이기 직전(0.74)에 연기가 핀다
        tipSmokeChance = 0.45f,
        stopSmoke = 8, stopFragments = 36, stopSparks = 30,   // v5 파편 = 티끌이라 16 → 36
        fragmentSpeed = 3f, sparkSpeed = 6f,
    };

    // ── 자동 적용 ─────────────────────────────────────────────────────
    static bool _pendingBuild, _pendingPatch;
    static double _nextPoll;

    [InitializeOnLoadMethod]
    static void AutoApply()
    {
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        EditorApplication.delayCall += () => { if (NeedsBuild()) _pendingBuild = true; else if (PatchNeeded()) _pendingPatch = true; };
    }

    static bool NeedsBuild()
    {
        if (AssetDatabase.LoadAssetAtPath<WeaponFxSet>(FxSetPath) == null) return false;   // 진화 무기 에셋이 없는 프로젝트
        return MarkerVersion() < Version || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null || LinkBroken();
    }

    /// 에디터 틱 — 플레이 · 컴파일 중이 아니면 밀린 굽기와 미리보기 요청을 처리한다(플레이 중 굽기는 끝난 뒤로 미룬다).
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < _nextPoll) return;
        _nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (_pendingBuild)
        {
            _pendingBuild = false;
            if (FxCatalogEdit.Catalog == null) { _pendingBuild = true; _nextPoll += 2.0; return; }   // 카탈로그를 FxAssetsSetup 이 아직 안 만들었다
            try
            {
                bool keepPrefab = HandEdited();
                if (keepPrefab)
                {
                    try { File.WriteAllText(HandEditedMarker, DateTime.Now.ToString("s")); } catch (Exception) { }
                    Debug.LogWarning($"[인페르노] 프리팹을 손으로 고친 흔적이 있어 v{Version} 은 텍스처 · 머티리얼만 바꾼다(프리팹 그대로). " +
                                     "파티클 값까지 새 판으로 하려면 메뉴 'Tools/좀비 오버런/FX 툴/인페르노 화염 다시 굽기 (파티클)' — 손편집은 사라진다.");
                }
                Debug.Log($"[인페르노] 화염 굽기 v{MarkerVersion()} → v{Version}{(keepPrefab ? " (텍스처 · 머티리얼만)" : "")}");
                if (Build(rebuildPrefab: !keepPrefab)) { WriteMarker(); RequestPreview(); }
                if (keepPrefab && PatchNeeded()) _pendingPatch = true;   // 손편집 프리팹 — 이 판에 꼭 필요한 렌더러 값만 따로(프리팹 모드가 닫혀 있을 때)
            }
            catch (Exception e) { Debug.LogError("[인페르노] 화염 굽기 실패 — 메뉴 '인페르노 화염 다시 굽기' 로: " + e); WriteMarker(); }
            return;
        }
        if (_pendingPatch)
        {
            _pendingPatch = false;
            try { if (PatchPrefab()) RequestPreview(); }
            catch (Exception e) { Debug.LogError("[인페르노] 손편집 프리팹 좁은 고침 실패: " + e); }
            return;
        }
        if (File.Exists(PreviewRequest))
        {
            try { File.Delete(PreviewRequest); } catch (Exception) { }
            try { RenderPreview(); }
            catch (Exception e) { Debug.LogError("[인페르노] 미리보기 렌더 실패: " + e); WritePreviewLog("FAILED\n" + e); }
        }
    }

    static void RequestPreview()
    {
        try { Directory.CreateDirectory(PreviewDir); File.WriteAllText(PreviewRequest, DateTime.Now.ToString("s")); } catch (Exception) { }
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/인페르노 화염 다시 굽기 (파티클)")]
    public static void RebuildMenu()
    {
        ClearHandEdited();
        if (Build()) { WriteMarker(); RenderPreview(); EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)); }
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/인페르노 화염 미리보기 렌더")]
    public static void PreviewMenu()
    {
        RenderPreview();
        EditorUtility.RevealInFinder(Path.Combine(PreviewDir, "inferno_preview.png"));
    }

    /// 배치 진입점(에디터가 닫혀 있을 때): Unity -batchmode -executeMethod InfernoFlameSetup.BatchRebuild
    public static void BatchRebuild()
    {
        ClearHandEdited();
        if (Build()) { WriteMarker(); RenderPreview(); }
    }

    /// 프리팹 파일이 마지막 굽기(표식)보다 새것이면 사람이 에디터에서 고친 것으로 본다(굽기는 저장 → 표식 순서라 표식이 늘 뒤).
    /// 한 번 걸리면 .handedited 표식이 남아 다음 판에서도 프리팹을 지키고, 메뉴 다시 굽기만 그 표식을 지운다(11-06 MSFX 복사본 덮어쓰기 사고 교훈).
    static bool HandEdited()
    {
        if (File.Exists(HandEditedMarker)) return true;
        if (!File.Exists(Marker) || !File.Exists(PrefabPath)) return false;
        return File.GetLastWriteTimeUtc(PrefabPath) > File.GetLastWriteTimeUtc(Marker).AddSeconds(5);
    }

    // 불씨(티끌) 기준값 — 조립 · 좁은 고침이 같이 쓴다. 옛 생성 기본값(OldX)일 때만 고친다(사람이 바꾼 값은 둔다).
    static readonly Vector2 EmberSize = new Vector2(0.05f, 0.12f);
    const float EmberRate = 40f;
    static readonly Vector2 FragSize = new Vector2(0.04f, 0.1f);
    static readonly Vector2[] OldEmberSizes = { new Vector2(0.15f, 0.32f) };
    static readonly Vector2[] OldFragSizes = { new Vector2(0.3f, 0.6f), new Vector2(0.12f, 0.26f) };
    const float OldEmberRate = 24f;
    const int OldStopFragments = 16, StopFragments = 36;

    /// 손편집 프리팹에 이 판이 꼭 넣어야 하는 값이 아직 없나 — 옛 생성 기본값 그대로인 것만 본다.
    static bool PatchNeeded()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        return prefab != null && Patch(prefab, null, dryRun: true).Count > 0;
    }

    static bool IsOld(ParticleSystem.MinMaxCurve c, Vector2[] olds)
    {
        if (c.mode != ParticleSystemCurveMode.TwoConstants) return false;
        foreach (var o in olds)
            if (Mathf.Approximately(c.constantMin, o.x) && Mathf.Approximately(c.constantMax, o.y)) return true;
        return false;
    }

    /// root 아래 Embers · Fragments(와 루트 맞춤값)를 티끌 불씨로. dryRun 이면 바꿀 것만 센다. undo 가 있으면 되돌리기 기록(열린 프리팹 모드).
    static List<string> Patch(GameObject root, Material dot, bool dryRun, string undo = null)
    {
        var changed = new List<string>();
        void Rec(Object o) { if (!dryRun && undo != null) Undo.RecordObject(o, undo); }
        foreach (var r in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            bool isEmbers = r.name == "Embers", isFrags = r.name == "Fragments";
            if (!isEmbers && !isFrags) continue;
            var ps = r.GetComponent<ParticleSystem>();
            string oldMat = isEmbers ? "M_Inferno_Ember" : "M_Inferno_Tongue";
            if (r.sharedMaterial != null && r.sharedMaterial.name == oldMat)
            {
                changed.Add($"{r.name} 머티리얼 → 티끌 불씨");
                if (!dryRun) { Rec(r); r.sharedMaterial = dot; }
            }
            var main = ps.main;
            Vector2 want = isEmbers ? EmberSize : FragSize;
            if (IsOld(main.startSize, isEmbers ? OldEmberSizes : OldFragSizes))
            {
                changed.Add($"{r.name} 크기 {main.startSize.constantMin}~{main.startSize.constantMax} → {want.x}~{want.y}");
                if (!dryRun) { Rec(ps); main.startSize = new ParticleSystem.MinMaxCurve(want.x, want.y); }
            }
            if (isEmbers)
            {
                var em = ps.emission;
                if (em.rateOverTime.mode == ParticleSystemCurveMode.Constant && Mathf.Approximately(em.rateOverTime.constant, OldEmberRate))
                {
                    changed.Add($"Embers 초당 {OldEmberRate} → {EmberRate}");
                    if (!dryRun) { Rec(ps); em.rateOverTime = EmberRate; }
                }
            }
        }
        var tune = root.GetComponent<ProtoFlameJetTuning>();
        if (tune != null && tune.knobs != null && tune.knobs.stopFragments == OldStopFragments)
        {
            changed.Add($"멈출 때 파편 {OldStopFragments} → {StopFragments}");
            if (!dryRun) { Rec(tune); tune.knobs.stopFragments = StopFragments; }
        }
        return changed;
    }

    /// 손으로 고친 프리팹에 넣는 좁은 고침 — 불씨(Embers · Fragments)만 티끌로(<see cref="Patch"/>). 다른 값(끈 오브젝트 · 고친 수치)은 그대로.
    /// 프리팹 모드로 열려 있으면 그 열린 프리팹에 넣는다(Ctrl+Z 로 되돌릴 수 있고, 자동 저장이 켜져 있으면 저장된다) — 닫힐 때까지 미루면
    /// 사용자는 옛 모양을 계속 본다(v4 에서 실제로 그랬다). 닫혀 있으면 파일에서 열어 고치고 저장한다.
    public static bool PatchPrefab()
    {
        var dot = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/M_Inferno_EmberDot.mat");
        if (dot == null) { Debug.LogWarning("[인페르노] M_Inferno_EmberDot 이 아직 없다 — 굽기 뒤에 다시"); return false; }
        List<string> changed;
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == PrefabPath)
        {
            changed = Patch(stage.prefabContentsRoot, dot, dryRun: false, undo: "인페르노 불씨 티끌로");
            if (changed.Count > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stage.scene);
            if (changed.Count > 0) Debug.Log("[인페르노] 열려 있는 프리팹 모드에 좁은 고침(Ctrl+Z 로 되돌리기, 저장은 프리팹 모드의 Save/자동 저장): " + string.Join(", ", changed));
            return changed.Count > 0;
        }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            changed = Patch(root, dot, dryRun: false);
            if (changed.Count > 0) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        if (changed.Count > 0) Debug.Log("[인페르노] 손으로 고친 프리팹에 좁은 고침(다른 값은 그대로): " + string.Join(", ", changed));
        return changed.Count > 0;
    }

    static void ClearHandEdited()
    {
        try { if (File.Exists(HandEditedMarker)) File.Delete(HandEditedMarker); } catch (Exception) { }
    }

    static int MarkerVersion()
    {
        if (!File.Exists(Marker)) return 0;
        var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(Marker), @"v(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    static void WriteMarker()
    {
        try { File.WriteAllText(Marker, $"v{Version} {DateTime.Now:s}"); } catch (Exception) { }
    }

    static bool LinkBroken()
    {
        var cat = FxCatalogEdit.Catalog;
        if (cat == null) return false;
        var e = cat.Find(FxIds.InfernoJet);
        return e == null || e.prefab == null;
    }

    // ── 굽기 ─────────────────────────────────────────────────────────
    /// 텍스처 임포트 → 머티리얼 → 프리팹 → 카탈로그 · 세트. 카탈로그나 세트가 없으면 false(다음 로드에).
    /// rebuildPrefab false = 손으로 고친 프리팹은 두고 텍스처 · 머티리얼(같은 GUID)만 바꾼다.
    public static bool Build(bool rebuildPrefab = true)
    {
        EnsureFolder(Dir);
        var tex = new Dictionary<string, Texture2D>();
        foreach (var n in new[] { "Puff", "Tongue", "Smoke", "Glow", "Dot", "Ember", "GroundFire", "Ring" })
        {
            var t = ImportTex($"{TexDir}/T_Inferno_{n}.png");
            if (t == null) { Debug.LogError($"[인페르노] 텍스처가 없다: {TexDir}/T_Inferno_{n}.png — 'python -E ArtSource/VFX/bake_inferno_textures.py' 로 굽는다"); return false; }
            tex[n] = t;
        }
        var mats = new Mats
        {
            flame = Mat("M_Inferno_Flame", tex["Puff"], additive: false, boost: 1.1f),   // v3 텍스처 명암 0.7~1(v2 는 0.18~1 이라 1.45) — 노란-흰 단계는 1 을 넘겨 블룸
            tongue = Mat("M_Inferno_Tongue", tex["Tongue"], additive: false, boost: 1f),
            smoke = Mat("M_Inferno_Smoke", tex["Smoke"], additive: false, boost: 1f, tint: SmokeTint),
            core = Mat("M_Inferno_Core", tex["Glow"], additive: true, boost: 1.4f),
            ember = Mat("M_Inferno_Ember", tex["Tongue"], additive: true, boost: 1.4f),      // 가산 불꽃 혀(Licks · GroundFlames)
            emberDot = Mat("M_Inferno_EmberDot", tex["Ember"], additive: true, boost: 2f),   // 티끌 불씨(Embers · Fragments) — 작아서 밝게(v4 1.4)
            spark = Mat("M_Inferno_Spark", tex["Dot"], additive: true, boost: 2.2f),
            groundFire = Mat("M_Inferno_GroundFire", tex["GroundFire"], additive: true, boost: 1.1f),
            groundGlow = Mat("M_Inferno_GroundGlow", tex["Dot"], additive: true, boost: 1f),
            ring = Mat("M_Inferno_Ring", tex["Ring"], additive: true, boost: 1.6f),
        };

        GameObject prefab = rebuildPrefab ? null : AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            var go = new GameObject("FX_Inferno_Jet");
            try
            {
                Assemble(go, mats);
                prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);   // 있던 자리 = 덮어쓰기(GUID 유지)
            }
            finally { Object.DestroyImmediate(go); }
        }
        if (prefab == null) { Debug.LogError("[인페르노] 프리팹 저장 실패: " + PrefabPath); return false; }

        bool ok = true;
        var cat = FxCatalogEdit.Catalog;
        if (cat != null)
        {
            var e = cat.Find(FxIds.InfernoJet);
            if (e == null)
            {
                e = new FxCatalog.Entry
                {
                    id = FxIds.InfernoJet, lifetime = 0f, poolSize = 1, maxAlive = 0, previewOffset = new Vector3(0f, 1.1f, 0.45f),
                    note = "무기·인페르노(화염방사기 진화) — 화염 줄기(스팬, ProtoFlamePack 이 세트 jet 슬롯으로 세운다). 모양 = 컨셉 아트를 파티클로(InfernoFlameSetup, 시트 애니 없음)",
                };
                cat.entries.Add(e);
            }
            e.prefab = prefab;
            e.lifetime = 0f;
            EditorUtility.SetDirty(cat);
        }
        else { Debug.LogWarning("[인페르노] 카탈로그가 아직 없다 — 다음 로드에 다시 잇는다"); ok = false; }

        var set = AssetDatabase.LoadAssetAtPath<WeaponFxSet>(FxSetPath);
        if (set != null)
        {
            if (set.jet == null) set.jet = new WeaponFxSet.Slot();
            if (string.IsNullOrEmpty(set.jet.fxId)) set.jet.fxId = FxIds.InfernoJet;   // 비어 있을 때만 — 손으로 바꿔 둔 것은 존중
            if (set.jet.scale <= 0f) set.jet.scale = 1f;

            EditorUtility.SetDirty(set);
        }
        else { Debug.LogWarning($"[인페르노] 연출 세트가 없다: {FxSetPath}"); ok = false; }

        AssetDatabase.SaveAssets();
        FxCatalog.Reload();
        ProtoFx.Clear();
        if (FxShowcase.Instance != null) FxShowcase.Instance.Reload();
        Debug.Log($"[인페르노] 화염 v{Version} 구움 — {PrefabPath}, 카탈로그 {FxIds.InfernoJet}, WFX_Inferno.jet = {(set != null ? set.jet.fxId : "(세트 없음)")}. " +
                  "FX 툴 [무기] 탭에서 '인페르노' 를 골라 쏜다. 맞춤값은 프리팹 루트 ProtoFlameJetTuning.");
        return ok;
    }

    class Mats { public Material flame, tongue, smoke, core, ember, emberDot, spark, groundFire, groundGlow, ring; }

    // ── 조립 ─────────────────────────────────────────────────────────
    // 축: +Z = 불줄기(총열), +Y = 위. 값은 미터(루트 배율 ≈ 1). 정렬은 같은 큐(3000) 안에서 sortingFudge — 작을수록 앞.
    static void Assemble(GameObject root, Mats m)
    {
        var tune = root.AddComponent<ProtoFlameJetTuning>();
        tune.knobs = Knobs();

        // 본체(루트) — 불 뭉치. 총구에서 0.2m 로 나와 끝에서 1.5~2m, 속도 배수로 끝에서 느려지며 뭉친다. 반은 뒤집어(같은 모양 반복이 덜 보이게)
        var body = Sys(root, m.flame, ParticleSystemRenderMode.Billboard, 0f, 260);
        Life(body, 0.55f, 0.68f); Speed(body, 12f, 15f); Size(body, 1.25f, 1.75f);
        Shape(body, ParticleSystemShapeType.Cone, angle: 14f, radius: 0.06f);
        Rate(body, 70f, perMeter: 3f);
        Inherit(body, 0.5f);
        SpinRandom(body, 85f);
        SizeOver(body, (0f, 0.14f), (0.12f, 0.36f), (0.4f, 0.76f), (0.75f, 1f), (1f, 1.2f));
        ColorOver(body, new[] { (HotWhite, 0f), (Yellow, 0.1f), (Amber, 0.3f), (Orange, 0.52f), (RedOrange, 0.74f), (Ember, 0.9f), (Soot, 1f) },
                  new[] { (0f, 0f), (0.6f, 0.05f), (1f, 0.12f), (1f, 0.72f), (0.45f, 0.88f), (0f, 1f) });   // 갓 나온 작은 뭉치는 옅게 — 총구 쪽은 가산 심 줄기가 앞선다(v2)
        Motion(body, Slow, (0f, 0f), (0.5f, 1f), (1f, 4f));
        Noise(body, 0.9f, 0.5f, 0.8f, (0f, 0f), (0.3f, 0.5f), (1f, 1f));
        StartColorRange(body, Color.white, new Color(1f, 0.94f, 0.86f));
        body.GetComponent<ParticleSystemRenderer>().sortMode = ParticleSystemSortMode.YoungestInFront;
        Flip(body, true);

        // 그늘 — 본체 뒤의 더 크고 어두운 붉은 뭉치(아랫면 · 바깥 말림). 조금 아래에서 조금 넓게
        var shade = Sys(Child(root, "Shade"), m.flame, ParticleSystemRenderMode.Billboard, 2f, 160);
        Life(shade, 0.6f, 0.72f); Speed(shade, 11f, 14f); Size(shade, 1.4f, 1.9f);
        Shape(shade, ParticleSystemShapeType.Cone, angle: 17f, radius: 0.08f, pos: new Vector3(0f, -0.06f, 0f));
        Rate(shade, 34f, perMeter: 1.5f);
        Inherit(shade, 0.5f); SpinRandom(shade, 70f);
        SizeOver(shade, (0f, 0.2f), (0.15f, 0.45f), (0.45f, 0.82f), (0.8f, 1f), (1f, 1.15f));
        ColorOver(shade, new[] { (new Color(0.78f, 0.4f, 0.1f), 0f), (new Color(0.72f, 0.28f, 0.07f), 0.25f), (new Color(0.6f, 0.19f, 0.05f), 0.55f), (new Color(0.42f, 0.13f, 0.05f), 0.8f), (new Color(0.24f, 0.1f, 0.07f), 0.93f), (Soot, 1f) },
                  new[] { (0f, 0f), (0.3f, 0.08f), (1f, 0.2f), (1f, 0.7f), (0.4f, 0.88f), (0f, 1f) });   // 총구 쪽 작은 검붉은 뭉치가 튀지 않게(v2)
        Motion(shade, Slow, (0f, 0f), (0.5f, 1.4f), (1f, 5f));
        Noise(shade, 0.9f, 0.45f, 0.7f, (0f, 0f), (0.3f, 0.5f), (1f, 1f));
        shade.GetComponent<ParticleSystemRenderer>().sortMode = ParticleSystemSortMode.YoungestInFront;
        Flip(shade, true);

        // 심 — 가산 노란-흰 빛(블룸). 좁게(콘이 아니라 상자 + 무작위 방향 — ProtoFlamePack 이 콘만 판정 폭으로 넓힌다), 불줄기 앞 절반에서 꺼진다
        var core = Sys(Child(root, "Core"), m.core, ParticleSystemRenderMode.Billboard, -3f, 120);
        Life(core, 0.3f, 0.4f); Speed(core, 12f, 15f); Size(core, 0.7f, 1.0f);
        BoxShape(core, new Vector3(0.1f, 0.1f, 0f), Vector3.zero);
        Rate(core, 60f, perMeter: 2f);
        Inherit(core, 0.5f); SpinRandom(core, 60f);
        Motion(core, Slow);
        SizeOver(core, (0f, 0.25f), (0.35f, 0.8f), (1f, 1.1f));
        ColorOver(core, new[] { (new Color(1f, 1f, 0.92f), 0f), (new Color(1f, 0.86f, 0.48f), 0.4f), (new Color(1f, 0.55f, 0.16f), 1f) },
                  new[] { (0f, 0f), (0.85f, 0.06f), (0.6f, 0.45f), (0f, 1f) });
        Noise(core, 0.3f, 0.6f, 0.8f);

        // 총구 불 — 총에 붙어(로컬) 짧게. 멈춘 뒤 ProtoFlamePack 이 muzzleLinger 동안 가늘게 끈다(컨셉 4. Fade Out 의 총구 불)
        var head = Sys(Child(root, "Head"), m.core, ParticleSystemRenderMode.Billboard, -4f, 40);
        Local(head);
        Life(head, 0.1f, 0.16f); Speed(head, 3f, 5f); Size(head, 0.3f, 0.48f);
        BoxShape(head, new Vector3(0.06f, 0.06f, 0f), Vector3.zero);
        Rate(head, 40f);
        SpinRandom(head, 90f);
        SizeOver(head, (0f, 0.5f), (1f, 1f));
        ColorOver(head, new[] { (new Color(1f, 0.97f, 0.82f), 0f), (new Color(1f, 0.75f, 0.3f), 1f) }, new[] { (0.9f, 0f), (0f, 1f) });

        // 불꽃 혀 — 본체 가장자리 뒤에서 위로 핀다. 가산(빛나는 가닥 — v1 은 알파 카툰 혀). 위를 보게(±40°), 좌우만 뒤집는다(위아래를 뒤집으면 불이 아래로 핀다)
        var licks = Sys(Child(root, "Licks"), m.ember, ParticleSystemRenderMode.Billboard, 1f, 80);
        Life(licks, 0.25f, 0.45f); Speed(licks, 9f, 13f); Size(licks, 0.35f, 0.6f);
        Shape(licks, ParticleSystemShapeType.Cone, angle: 18f, radius: 0.1f, pos: new Vector3(0f, 0.1f, 0f));
        Rate(licks, 20f, perMeter: 1f);
        Inherit(licks, 0.5f);
        Tilt(licks, 40f);
        SizeOver(licks, (0f, 0.15f), (0.45f, 0.85f), (0.8f, 0.75f), (1f, 0.3f));
        ColorOver(licks, new[] { (new Color(1f, 0.82f, 0.4f), 0f), (new Color(1f, 0.55f, 0.14f), 0.35f), (new Color(0.9f, 0.32f, 0.06f), 1f) },
                  new[] { (0f, 0f), (0.55f, 0.1f), (0.45f, 0.65f), (0f, 1f) });
        Flip(licks, false);
        Motion(licks, Slow, (0f, 1f), (1f, 6f));

        // 불꽃 조각 — 둘레로 튀어 떠오르는 작은 가산 혀(컨셉의 주변 불꽃). 불줄기 앞 3.5m 부피에서
        var embers = Sys(Child(root, "Embers"), m.emberDot, ParticleSystemRenderMode.Billboard, -5f, 120);   // v4 원 불씨(v3 까지 가산 혀)
        Life(embers, 0.4f, 0.85f); Speed(embers, 2f, 5f); Size(embers, EmberSize.x, EmberSize.y);   // v5 티끌(v4 까지 0.15~0.32)
        Shape(embers, ParticleSystemShapeType.ConeVolume, angle: 34f, radius: 0.2f, pos: new Vector3(0f, 0f, 0.4f), length: 3.8f, randomDir: 0.6f);
        { var sh = embers.shape; sh.radiusThickness = 0.25f; }   // 바깥 껍질에서 — 불 안에 묻히지 않게
        Rate(embers, EmberRate);
        Drag(embers, 1.5f); Gravity(embers, -0.35f);
        Tilt(embers, 35f);
        SizeOver(embers, (0f, 1f), (1f, 0.3f));
        ColorOver(embers, new[] { (new Color(1f, 0.75f, 0.35f), 0f), (new Color(1f, 0.48f, 0.12f), 0.5f), (new Color(0.7f, 0.15f, 0.05f), 1f) },
                  new[] { (0f, 0f), (1f, 0.1f), (1f, 0.7f), (0f, 1f) });
        Noise(embers, 1f, 0.8f, 1f);
        Flip(embers, false);

        // 불똥 — 작은 점(늘림). 분사 중 조금, 멈출 때 ProtoFlamePack 이 몸통 자리에서 한꺼번에
        var sparks = Sys(Child(root, "Sparks"), m.spark, ParticleSystemRenderMode.Stretch, -5f, 200);
        Stretch(sparks, 1.2f, 0.03f);
        Life(sparks, 0.25f, 0.6f); Speed(sparks, 4f, 9f); Size(sparks, 0.06f, 0.1f);
        Shape(sparks, ParticleSystemShapeType.ConeVolume, angle: 24f, radius: 0.15f, pos: new Vector3(0f, 0f, 0.3f), length: 3f, randomDir: 0.4f);
        Rate(sparks, 30f);
        Drag(sparks, 2f); Gravity(sparks, -0.2f);
        SizeOver(sparks, (0f, 1f), (1f, 0.4f));
        ColorOver(sparks, new[] { (new Color(1f, 0.78f, 0.42f), 0f), (new Color(1f, 0.45f, 0.12f), 0.6f), (new Color(0.8f, 0.2f, 0.05f), 1f) },
                  new[] { (1f, 0f), (1f, 0.6f), (0f, 1f) });

        // 파편 — 불덩이 조각. 분사 중 끝 쪽에서 조금, 멈출 때 몸통에서(컨셉 4. Fade Out 의 조각난 불)
        var frags = Sys(Child(root, "Fragments"), m.emberDot, ParticleSystemRenderMode.Billboard, -1f, 100);   // v4 원 불씨(v3 까지 알파 혀)
        Life(frags, 0.3f, 0.55f); Speed(frags, 1f, 3f); Size(frags, FragSize.x, FragSize.y);   // v5 티끌(v4 0.12~0.26, 그 전 0.3~0.6)
        Shape(frags, ParticleSystemShapeType.ConeVolume, angle: 18f, radius: 0.2f, pos: new Vector3(0f, 0f, 2.5f), length: 2.5f);
        Rate(frags, 0f);   // 분사 중엔 안 낸다(끝에 떨어진 갈색 덩어리로 보였다) — 멈출 때 ProtoFlamePack 이 몸통에서
        Drag(frags, 1.5f); Gravity(frags, -0.25f); Tilt(frags, 35f);
        SizeOver(frags, (0f, 0.6f), (0.3f, 1f), (1f, 0.2f));
        Flip(frags, false);
        ColorOver(frags, new[] { (new Color(1f, 0.85f, 0.4f), 0f), (new Color(1f, 0.55f, 0.15f), 0.4f), (new Color(0.9f, 0.3f, 0.08f), 0.8f), (new Color(0.5f, 0.15f, 0.12f), 1f) },
                  new[] { (0f, 0f), (1f, 0.08f), (1f, 0.75f), (0f, 1f) });

        // 연기 — 방출 0. ProtoFlamePack 이 불끝(수명 tipAge)과 멈출 때 불 입자 자리 · 속도 · 크기로 낸다. 불 뒤에(fudge 4) — 끝에서 넘쳐 위로 피어오른다
        var smoke = Sys(Child(root, "Smoke"), m.smoke, ParticleSystemRenderMode.Billboard, 4f, 160);
        Life(smoke, 0.9f, 1.2f); Speed(smoke, 0f, 0f); Size(smoke, 1.2f, 1.6f);
        Rate(smoke, 0f);
        Drag(smoke, 2.5f); Gravity(smoke, -0.3f); SpinRandom(smoke, 45f);
        SizeOver(smoke, (0f, 0.7f), (0.4f, 1.05f), (1f, 1.6f));
        StartColorRange(smoke, SmokeA, SmokeB);
        ColorOver(smoke, new[] { (new Color(1f, 0.75f, 0.6f), 0f), (Color.white, 0.3f), (new Color(0.9f, 0.9f, 0.95f), 1f) },
                  new[] { (0f, 0f), (0.85f, 0.15f), (0.7f, 0.55f), (0f, 1f) });
        Noise(smoke, 0.5f, 0.4f, 0.5f);
        Flip(smoke, true);

        // 점화(컨셉 1. Charge) — 쏘기 시작하는 순간 한 번. ProtoFlameJetTuning 이 버스트 수를 읽어 Emit 한다
        var flash = Sys(Child(root, "ChargeFlash"), m.core, ParticleSystemRenderMode.Billboard, -6f, 8);
        Local(flash);
        Life(flash, 0.14f, 0.2f); Speed(flash, 0.5f, 1.5f); Size(flash, 0.8f, 1.2f);
        BoxShape(flash, new Vector3(0.04f, 0.04f, 0f), Vector3.zero);
        Burst(flash, 3);
        SizeOver(flash, (0f, 0.3f), (0.3f, 1f), (1f, 1.2f));
        ColorOver(flash, new[] { (new Color(1f, 0.96f, 0.8f), 0f), (new Color(1f, 0.6f, 0.2f), 1f) }, new[] { (1f, 0f), (0f, 1f) });

        var cspark = Sys(Child(root, "ChargeSparks"), m.spark, ParticleSystemRenderMode.Stretch, -6f, 32);
        Stretch(cspark, 1.4f, 0.04f);
        Life(cspark, 0.15f, 0.35f); Speed(cspark, 3f, 7f); Size(cspark, 0.06f, 0.09f);
        Shape(cspark, ParticleSystemShapeType.Cone, angle: 40f, radius: 0.04f);
        Burst(cspark, 20);
        Drag(cspark, 2f);
        ColorOver(cspark, new[] { (new Color(1f, 0.95f, 0.7f), 0f), (new Color(1f, 0.55f, 0.15f), 1f) }, new[] { (1f, 0f), (1f, 0.5f), (0f, 1f) });

        var cring = Sys(Child(root, "ChargeRing"), m.ring, ParticleSystemRenderMode.Billboard, -6f, 4);
        Local(cring);
        cring.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;   // 총열을 감싸는 고리
        Life(cring, 0.22f, 0.22f); Speed(cring, 1.5f, 1.5f); Size(cring, 0.5f, 0.5f);
        NoShape(cring);
        Burst(cring, 1);
        SizeOver(cring, (0f, 0.4f), (1f, 2.2f));
        ColorOver(cring, new[] { (new Color(1f, 0.85f, 0.5f), 0f), (new Color(1f, 0.55f, 0.2f), 1f) }, new[] { (0.9f, 0f), (0f, 1f) });

        // 바닥(컨셉 Additional Effects) — ProtoFlameJetTuning 이 소유자 발 높이로 내린다. 바닥 빛은 불줄기 아래 따뜻한 빛, 불 고리는 끝 쪽 바닥에 남아 타다 꺼진다
        var gglow = Sys(Child(root, "GroundGlow"), m.groundGlow, ParticleSystemRenderMode.HorizontalBillboard, 6f, 40);
        Life(gglow, 0.18f, 0.3f); Speed(gglow, 0f, 0f); Size(gglow, 2f, 3f);
        BoxShape(gglow, new Vector3(1.6f, 0f, 4.2f), new Vector3(0f, 0f, 2.6f));
        Rate(gglow, 30f);
        StartColorRange(gglow, new Color(1f, 0.42f, 0.08f), new Color(1f, 0.55f, 0.15f));
        ColorOver(gglow, new[] { (Color.white, 0f), (Color.white, 1f) }, new[] { (0f, 0f), (0.45f, 0.3f), (0f, 1f) });

        var gfire = Sys(Child(root, "GroundFire"), m.groundFire, ParticleSystemRenderMode.HorizontalBillboard, 5f, 40);
        Life(gfire, 1f, 1.6f); Speed(gfire, 0f, 0f); Size(gfire, 0.9f, 1.5f);
        BoxShape(gfire, new Vector3(2.6f, 0f, 2.8f), new Vector3(0f, 0f, 4.6f));
        Rate(gfire, 9f);
        SpinRandom(gfire, 20f);
        SizeOver(gfire, (0f, 0.4f), (0.25f, 1f), (1f, 1.25f));
        Flip(gfire, true);
        ColorOver(gfire, new[] { (new Color(1f, 0.85f, 0.45f), 0f), (new Color(1f, 0.55f, 0.15f), 0.4f), (new Color(0.9f, 0.35f, 0.08f), 0.8f), (new Color(0.5f, 0.15f, 0.04f), 1f) },
                  new[] { (0f, 0f), (0.9f, 0.1f), (0.6f, 0.55f), (0f, 1f) });

        var gflames = Sys(Child(root, "GroundFlames"), m.ember, ParticleSystemRenderMode.Billboard, 3f, 40);
        Life(gflames, 0.35f, 0.6f); Speed(gflames, 0f, 0f); Size(gflames, 0.35f, 0.6f);
        BoxShape(gflames, new Vector3(2.6f, 0f, 2.8f), new Vector3(0f, 0.22f, 4.6f));
        Rate(gflames, 14f);
        Tilt(gflames, 18f);
        Rise(gflames, (0f, 1f), (1f, 1f));
        SizeOver(gflames, (0f, 0.4f), (0.3f, 1f), (1f, 0.5f));
        Flip(gflames, false);
        ColorOver(gflames, new[] { (new Color(1f, 0.8f, 0.4f), 0f), (new Color(1f, 0.45f, 0.1f), 0.5f), (new Color(0.7f, 0.15f, 0.05f), 1f) },
                  new[] { (0f, 0f), (1f, 0.15f), (0.8f, 0.6f), (0f, 1f) });
    }

    // ── 파티클 헬퍼 ───────────────────────────────────────────────────
    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    /// 반복 · 월드 공간 · Hierarchy 스케일 · 이동 속도는 Transform 으로(걸은 거리당 방출이 돈다). 렌더러 큐는 머티리얼(3000), 앞뒤는 fudge.
    static ParticleSystem Sys(GameObject go, Material mat, ParticleSystemRenderMode mode, float fudge, int max)
    {
        var ps = go.GetComponent<ParticleSystem>();
        if (ps == null) ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 2f; main.loop = true; main.playOnAwake = true; main.prewarm = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
        main.maxParticles = max;
        main.stopAction = ParticleSystemStopAction.None;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
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
        r.maxParticleSize = 3f;   // 기본 0.5(화면 몫) — 가까운 카메라(무대)에서 큰 불덩이가 잘린다
        return ps;
    }

    static void Local(ParticleSystem ps) { var m = ps.main; m.simulationSpace = ParticleSystemSimulationSpace.Local; }
    static void Life(ParticleSystem ps, float a, float b) { var m = ps.main; m.startLifetime = Range(a, b); }
    static void Speed(ParticleSystem ps, float a, float b) { var m = ps.main; m.startSpeed = Range(a, b); }
    static void Size(ParticleSystem ps, float a, float b) { var m = ps.main; m.startSize = Range(a, b); }
    static void Gravity(ParticleSystem ps, float g) { var m = ps.main; m.gravityModifier = g; }
    static ParticleSystem.MinMaxCurve Range(float a, float b) => Mathf.Approximately(a, b) ? new ParticleSystem.MinMaxCurve(a) : new ParticleSystem.MinMaxCurve(a, b);

    static void StartColorRange(ParticleSystem ps, Color a, Color b) { var m = ps.main; m.startColor = new ParticleSystem.MinMaxGradient(a, b); }

    static void Rate(ParticleSystem ps, float perSecond, float perMeter = 0f)
    {
        var em = ps.emission; em.enabled = true; em.rateOverTime = perSecond; em.rateOverDistance = perMeter;
    }

    static void Burst(ParticleSystem ps, int count)
    {
        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float angle, float radius, Vector3 pos = default, float length = 1f, float randomDir = 0f)
    {
        var sh = ps.shape;
        sh.enabled = true; sh.shapeType = type;
        sh.angle = angle; sh.radius = radius; sh.radiusThickness = 1f; sh.arc = 360f; sh.length = length;
        sh.position = pos; sh.rotation = Vector3.zero; sh.scale = Vector3.one;
        sh.randomDirectionAmount = randomDir; sh.sphericalDirectionAmount = 0f;
    }

    /// 상자 — 방향은 늘 +Z. 함정(11-06 측정): 무작위 방향(randomDirectionAmount)은 모양 크기를 곱하기 <b>전에</b> 섞여서, z 크기가 0 인 상자에 넣으면 옆으로만 튄다 — 상자엔 0.
    static void BoxShape(ParticleSystem ps, Vector3 size, Vector3 pos, float randomDir = 0f)
    {
        var sh = ps.shape;
        sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
        sh.position = pos; sh.rotation = Vector3.zero; sh.scale = size;
        sh.boxThickness = Vector3.one;   // 1 = 부피. 0(겉면)이면 면의 법선 쪽으로 튀어 심이 총구 위아래 · 옆으로 흩어졌다(11-06 미리보기)
        sh.randomDirectionAmount = randomDir; sh.sphericalDirectionAmount = 0f;
    }

    static void NoShape(ParticleSystem ps) { var sh = ps.shape; sh.enabled = false; }

    static void Inherit(ParticleSystem ps, float k)
    {
        var iv = ps.inheritVelocity; iv.enabled = true; iv.mode = ParticleSystemInheritVelocityMode.Initial; iv.curve = new ParticleSystem.MinMaxCurve(k);
    }

    /// 끌림 — <b>속도 곱(제곱 끌림)</b>: dv/dt ≈ −d·v². 빨리 튀어 나가 금방 선다(불똥 · 조각 · 연기).
    /// 함정(11-06 임시 프로젝트에서 잼): Unity 끌림은 지수 감쇠가 아니다 — 속도 곱을 끄면 <b>등감속</b>(매초 d 씩 줄 뿐, 끌림 2 로 0.68초에 9% 감속),
    /// 켜면 제곱 감속이다. ProtoFlamePack.Travel 의 v/d·(1−e^(−dt)) 는 둘 다와 다르다 — 그래서 불줄기(루트)엔 끌림을 안 쓰고 <see cref="Motion"/> 의 속도 배수로 늦춘다.
    static void Drag(ParticleSystem ps, float d)
    {
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true; lim.separateAxes = false;
        lim.limit = new ParticleSystem.MinMaxCurve(1000f); lim.dampen = 0f;
        lim.drag = new ParticleSystem.MinMaxCurve(d);
        lim.multiplyDragByParticleSize = false; lim.multiplyDragByParticleVelocity = true;
    }

    /// 불줄기 속도 배수(수명 따라) — 총구에서 빠르게 나가 끝에서 느려지며 뭉친다. 평균 ≈ 0.55 라 이동 거리 = 속도 × 수명 × 0.55,
    /// 루트 배율에 정확히 비례한다(1 → 1.13 배율에서 6.03 → 6.82m, 11-06 측정) — ProtoFlamePack 의 "속도 × 수명" 맞추기가 그대로 맞는다(visibleFraction 이 그 몫).
    static readonly (float t, float v)[] Slow = { (0f, 1f), (0.3f, 0.75f), (0.6f, 0.4f), (1f, 0.15f) };

    static void SpinRandom(ParticleSystem ps, float degPerSec)
    {
        var m = ps.main; m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var rol = ps.rotationOverLifetime; rol.enabled = true; rol.separateAxes = false;
        float r = degPerSec * Mathf.Deg2Rad;
        rol.z = new ParticleSystem.MinMaxCurve(-r, r);
    }

    /// 위를 보게(±deg) — 불꽃 혀는 뒤집히면 불로 안 읽힌다.
    /// 렌더러 뒤집기 — 입자 반을 좌우(그리고 둥근 모양이면 위아래도) 뒤집어 한 장짜리 텍스처의 반복을 숨긴다(시트 애니 없이 모양 수를 늘리는 값싼 방법).
    static void Flip(ParticleSystem ps, bool vertical)
    {
        ps.GetComponent<ParticleSystemRenderer>().flip = new Vector3(0.5f, vertical ? 0.5f : 0f, 0f);
    }

    static void Tilt(ParticleSystem ps, float deg)
    {
        var m = ps.main; float r = deg * Mathf.Deg2Rad; m.startRotation = new ParticleSystem.MinMaxCurve(-r, r);
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

    /// 수명 따라 위로(월드 m/s). 함정(11-05): 속도-수명 x · y · z 는 같은 모드여야 한다 — 셋 다 곡선.
    static void Rise(ParticleSystem ps, params (float t, float v)[] y) => Motion(ps, null, y);

    /// 속도-수명 모듈 한 번에 — slow = 속도 배수 곡선(null 이면 1), y = 위로 곡선(없으면 0). 속도 배수는 위로 속도에도 곱해진다(끝에서 0.15 — 그만큼 크게 적는다).
    static void Motion(ParticleSystem ps, (float t, float v)[] slow, params (float t, float v)[] y)
    {
        var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
        var zero = Curve((0f, 0f), (1f, 0f));
        v.x = new ParticleSystem.MinMaxCurve(1f, zero);
        v.y = new ParticleSystem.MinMaxCurve(1f, y != null && y.Length > 0 ? Curve(y) : zero);
        v.z = new ParticleSystem.MinMaxCurve(1f, zero);
        v.speedModifier = slow != null ? new ParticleSystem.MinMaxCurve(1f, Curve(slow)) : new ParticleSystem.MinMaxCurve(1f);
    }

    static void Stretch(ParticleSystem ps, float length, float velocity)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = length; r.velocityScale = velocity; r.cameraVelocityScale = 0f;
    }

    static void Noise(ParticleSystem ps, float strength, float frequency, float scroll, params (float t, float v)[] over)
    {
        var nz = ps.noise;
        nz.enabled = true; nz.separateAxes = false;
        nz.strength = over != null && over.Length > 0 ? new ParticleSystem.MinMaxCurve(strength, Curve(over)) : new ParticleSystem.MinMaxCurve(strength);
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

    // ── 텍스처 · 머티리얼 ─────────────────────────────────────────────
    static Texture2D ImportTex(string path)
    {
        if (!File.Exists(path)) return null;
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); imp = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (imp != null)
        {
            bool dirty = imp.textureType != TextureImporterType.Default || !imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp ||
                         !imp.mipmapEnabled || !imp.sRGBTexture || imp.textureCompression != TextureImporterCompression.CompressedHQ;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = true;
                imp.sRGBTexture = true;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// URP Particles/Unlit — 알파 블렌드(불 뭉치 · 혀 · 연기) 또는 가산(심 · 불똥 · 바닥 빛). 큐는 모두 3000(같은 큐여야 렌더러 fudge 가 앞뒤를 정한다 — 11-05 교훈).
    /// 가산은 _BaseColor 로 밝기를 올린다(입자 색은 8비트라 HDR 불가 — 블룸 문턱 1 을 머티리얼이 넘긴다). 있으면 그 자리에서 고친다(GUID 유지).
    static Material Mat(string name, Texture2D tex, bool additive, float boost, Color? tint = null)
    {
        string path = $"{Dir}/{name}.mat";
        var shader = Shader.Find(FxShaderUnify.ShaderName);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = m == null;
        if (create) m = new Material(shader) { name = name };
        else if (m.shader != shader) m.shader = shader;
        m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        var c = tint ?? Color.white;
        m.SetColor("_BaseColor", new Color(boost * c.r, boost * c.g, boost * c.b, 1f));
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);   // 링(총열 축) · 바닥(누운 면)이 뒤에서도 보이게
        if (m.HasProperty("_ColorMode")) m.SetFloat("_ColorMode", 0f);
        if (m.HasProperty("_FlipbookBlending")) m.SetFloat("_FlipbookBlending", 0f);
        if (m.HasProperty("_SoftParticlesEnabled")) m.SetFloat("_SoftParticlesEnabled", 0f);
        if (m.HasProperty("_CameraFadingEnabled")) m.SetFloat("_CameraFadingEnabled", 0f);
        if (m.HasProperty("_EmissionEnabled")) m.SetFloat("_EmissionEnabled", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        foreach (var k in new[] { "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON", "_ALPHATEST_ON", "_EMISSION", "_FLIPBOOKBLENDING_ON", "_SOFTPARTICLES_ON", "_FADING_ON", "_COLORCOLOR_ON", "_COLOROVERLAY_ON", "_COLORADDSUBDIFF_ON" })
            m.DisableKeyword(k);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        m.renderQueue = 3000;
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
    // 숨은 미리보기 씬에 바닥 · 캐릭터 대역 · 총구를 세우고 게임과 같은 ProtoFlamePack.Configure 로 프리팹을 맞춘 뒤,
    // ProtoFlamePack.LateUpdate 를 흉내 내며(방출 램프 · 끝 연기 · 멈출 때 연기/파편/불똥 · 사그라듦 · 총구 불 남김) 1/60 초씩 Simulate 해서 찍는다.
    const float Dt = 1f / 60f;
    const float OnFor = 1.3f;

    struct Shot { public string label; public float t; public bool side; public int w, h; public Texture2D tex; }

    public static void RenderPreview()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var st = AssetDatabase.LoadAssetAtPath<WeaponStats>(StatsPath);
        if (prefab == null || st == null) { Debug.LogWarning("[인페르노] 미리보기 — 프리팹이나 WS_Inferno 가 없다"); return; }
        Directory.CreateDirectory(PreviewDir);
        FxCatalog.Reload();
        var log = new StringBuilder();
        var pru = new PreviewRenderUtility();
        GameObject world = null;
        try
        {
            var cam = pru.camera;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.17f, 0.17f, 0.21f, 1f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
            cam.allowHDR = false;

            world = new GameObject("InfernoPreviewWorld");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Prop(world, PrimitiveType.Plane, new Vector3(3f, 0f, 0f), new Vector3(3f, 1f, 2f), new Color(0.26f, 0.26f, 0.3f), unlit);
            Prop(world, PrimitiveType.Capsule, new Vector3(-0.2f, 0.85f, 0f), new Vector3(0.5f, 0.85f, 0.5f), new Color(0.85f, 0.5f, 0.62f), unlit);   // 캐릭터 대역 1.7m
            Prop(world, PrimitiveType.Cube, new Vector3(0.18f, 0.95f, 0f), new Vector3(0.5f, 0.12f, 0.12f), new Color(0.3f, 0.3f, 0.32f), unlit);       // 총
            var owner = new GameObject("Owner");
            owner.transform.SetParent(world.transform, false);
            owner.transform.rotation = Quaternion.LookRotation(Vector3.right);   // 불은 +X 로(옆에서 보면 왼쪽 → 오른쪽, 컨셉처럼)
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(owner.transform, false);
            muzzle.localPosition = new Vector3(0f, 0.95f, 0.43f);
            var packGo = new GameObject("_FlamePack");
            packGo.transform.SetParent(owner.transform, false);
            pru.AddSingleGO(world);

            var pack = packGo.AddComponent<ProtoFlamePack>();
            pack.Configure(st, muzzle);   // 게임과 같은 세우기 · 맞추기(루트 배율 · 콘 · 눌림 · 맞춤값)
            var jet = packGo.transform.Find("Jet");
            if (jet == null) { WritePreviewLog("Jet 이 안 섰다 — 카탈로그 inferno_jet 확인"); return; }
            var tune = jet.GetComponent<ProtoFlameJetTuning>();
            if (tune != null) tune.Collect();   // 에디터에선 Awake 가 안 돈다
            var all = jet.GetComponentsInChildren<ParticleSystem>(true);
            var rate = all.Select(p => p.emission.rateOverTime.constant).ToArray();
            ParticleSystem root = jet.GetComponent<ParticleSystem>(), smoke = null, frags = null, sparks = null, head = null;
            foreach (var p in all)
            {
                p.Clear(false);
                if (p.name == "Smoke") smoke = p; else if (p.name == "Fragments") frags = p; else if (p.name == "Sparks") sparks = p; else if (p.name == "Head") head = p;
            }
            bool Linger(ParticleSystem p) => p == smoke || p == frags || p == sparks || p == head || p.name == "Embers" || ProtoFlameJetTuning.IsCharge(p.name) || ProtoFlameJetTuning.IsGround(p.name);
            float s = jet.lossyScale.x;
            log.AppendLine($"루트 배율 {s:0.000} (사거리 {st.range}m, 판정 {st.coneDegrees}°) · 맞춤값 visible {pack.visibleFraction} cone {pack.coneFraction} flatten {pack.flatten} tipAge {pack.tipAge}");

            var shots = new List<Shot>
            {
                new Shot { label = "1 Charge", t = 0.05f, side = true, w = 600, h = 260 },
                new Shot { label = "2 Start", t = 0.2f, side = true, w = 600, h = 260 },
                new Shot { label = "3 Sustain", t = OnFor, side = true, w = 600, h = 260 },
                new Shot { label = "4 FadeOut", t = OnFor + 0.22f, side = true, w = 600, h = 260 },
                new Shot { label = "Sustain big", t = OnFor, side = true, w = 1200, h = 520 },
                new Shot { label = "Game view", t = OnFor, side = false, w = 1200, h = 640 },
                new Shot { label = "Game fade", t = OnFor + 0.22f, side = false, w = 1200, h = 640 },
                new Shot { label = "Game after", t = OnFor + 0.8f, side = false, w = 1200, h = 640 },
            };
            var buf = new ParticleSystem.Particle[2048];
            float end = shots.Max(x => x.t) + 0.001f;
            bool wasOn = false;
            int step = 0;
            for (float t = 0f; t <= end; t += Dt, step++)
            {
                bool on = t < OnFor;
                if (on != wasOn)
                {
                    foreach (var p in all) { if (p == head) continue; var em = p.emission; em.enabled = on; }
                    if (head != null) { var em = head.emission; em.enabled = true; }
                    if (!on) StopDebris(pack, root, smoke, frags, sparks, buf);
                }
                if (on)
                {
                    float f = pack.rampIn > 0f ? Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(t / pack.rampIn)) : 1f;
                    for (int i = 0; i < all.Length; i++) if (!Linger(all[i])) { var em = all[i].emission; em.rateOverTime = rate[i] * f; }
                    TipSmoke(pack, root, smoke, buf);
                }
                else
                {
                    float since = t - OnFor;
                    if (head != null)
                    {
                        var em = head.emission;
                        float k = 1f - Mathf.Clamp01(since / Mathf.Max(0.01f, pack.muzzleLinger));
                        em.rateOverTime = rate[Array.IndexOf(all, head)] * k * k;
                        if (k <= 0f) em.enabled = false;
                    }
                    Fade(pack, all, Linger, since, buf);
                }
                wasOn = on;
                if (tune != null) tune.Tick(t);
                foreach (var p in all) p.Simulate(Dt, false, false, false);

                for (int i = 0; i < shots.Count; i++)
                {
                    var sh = shots[i];
                    if (sh.tex != null || t + Dt * 0.5f < sh.t) continue;
                    sh.tex = Capture(pru, sh.side, sh.w, sh.h);
                    shots[i] = sh;
                    if (sh.label == "3 Sustain" || sh.label == "4 FadeOut")
                    {
                        log.AppendLine($"{sh.label} t={t:0.00}: " + string.Join(", ", all.Select(p => $"{p.name} {p.particleCount}")));
                        if (sh.label == "3 Sustain") log.AppendLine(Extents(all, muzzle.position, owner.transform.forward, buf));
                    }
                }
            }

            // 모아 붙이기: 큰 지속(옆) · 4단계(2×2) · 게임 시점 2장
            int W = 1200;
            int H = 520 + 520 + 640 + 640 + 640;
            var sheet = new Texture2D(W, H, TextureFormat.RGBA32, false);
            int y = H;
            void Blit(Texture2D src, int x, int top)
            {
                if (src == null) return;
                sheet.SetPixels(x, top - src.height, src.width, src.height, src.GetPixels());
            }
            Blit(shots[4].tex, 0, y); y -= 520;
            Blit(shots[0].tex, 0, y); Blit(shots[1].tex, 600, y); y -= 260;
            Blit(shots[2].tex, 0, y); Blit(shots[3].tex, 600, y); y -= 260;
            Blit(shots[5].tex, 0, y); y -= 640;
            Blit(shots[6].tex, 0, y); y -= 640;
            Blit(shots[7].tex, 0, y);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(PreviewDir, "inferno_preview.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            foreach (var sh in shots) if (sh.tex != null) Object.DestroyImmediate(sh.tex);
            log.AppendLine("순서(위→아래): 지속 옆모습 크게 / 1 점화 · 2 시작 / 3 지속 · 4 사그라듦 / 게임 시점(58°) 지속 / 사그라듦(멈추고 0.22초) / 멈추고 0.8초(바닥 불 · 연기)");
            WritePreviewLog(log.ToString());
            Debug.Log("[인페르노] 미리보기 → " + Path.Combine(PreviewDir, "inferno_preview.png") + "\n" + log);
        }
        finally
        {
            if (world != null) Object.DestroyImmediate(world);
            foreach (var o in _temp) if (o != null) Object.DestroyImmediate(o);
            _temp.Clear();
            pru.Cleanup();
        }
    }

    static readonly List<Object> _temp = new List<Object>();

    /// 진단 — 시스템마다 총구에서 불줄기 방향으로 가장 멀리 간 입자 중심(m)과 그 입자의 지름, 옆(수평) · 위로 벗어난 최대 거리. 길이 · 폭을 숫자로 맞출 때.
    static string Extents(ParticleSystem[] all, Vector3 origin, Vector3 fwd, ParticleSystem.Particle[] buf)
    {
        var sb = new StringBuilder("도달(중심 m / 지름 m / 옆 / 위): ");
        Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
        foreach (var ps in all)
        {
            int n = ps.GetParticles(buf);
            if (n == 0) continue;
            bool world = ps.main.simulationSpace == ParticleSystemSimulationSpace.World;
            float far = -99f, farSize = 0f, wide = 0f, high = -99f;
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = world ? buf[i].position : ps.transform.TransformPoint(buf[i].position);
                Vector3 d = pos - origin;
                float along = Vector3.Dot(d, fwd);
                float sz = buf[i].GetCurrentSize(ps) * ps.transform.lossyScale.x;
                if (along > far) { far = along; farSize = sz; }
                wide = Mathf.Max(wide, Mathf.Abs(Vector3.Dot(d, side)) + sz * 0.5f);
                high = Mathf.Max(high, d.y + sz * 0.5f);
            }
            sb.Append($"{ps.name} {far:0.0}/{farSize:0.0}/{wide:0.0}/{high:0.0}  ");
        }
        return sb.ToString();
    }

    static void WritePreviewLog(string text)
    {
        try { Directory.CreateDirectory(PreviewDir); File.WriteAllText(Path.Combine(PreviewDir, "log.txt"), $"{DateTime.Now:s} v{Version}\n{text}"); } catch (Exception) { }
    }

    static void Prop(GameObject parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color, Shader unlit)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        var m = new Material(unlit != null ? unlit : Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        _temp.Add(m);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); else m.color = color;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Texture2D Capture(PreviewRenderUtility pru, bool side, int w, int h)
    {
        var cam = pru.camera;
        if (side)
        {
            cam.fieldOfView = 24f;
            var target = new Vector3(3.8f, 1.3f, 0f);
            cam.transform.SetPositionAndRotation(target + new Vector3(0f, 0.6f, -15f), Quaternion.LookRotation(new Vector3(0f, -0.6f, 15f)));
        }
        else
        {
            cam.fieldOfView = 30f;   // 게임 카메라 각도(58°) — 거리는 가깝게
            var target = new Vector3(3.4f, 0.6f, 0f);
            var rot = Quaternion.Euler(58f, 0f, 0f);
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * 13.5f, rot);
        }
        pru.BeginStaticPreview(new Rect(0, 0, w, h));
        pru.Render(true);
        var tex = pru.EndStaticPreview();
        if (tex.width == w && tex.height == h) return tex;
        // 고해상도 화면(배율)이면 크기가 다르다 — 맞춘다
        var rt = RenderTexture.GetTemporary(w, h, 0);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        outTex.ReadPixels(new Rect(0, 0, w, h), 0, 0); outTex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(tex);
        return outTex;
    }

    // ProtoFlamePack.TickTipSmoke 흉내 — 이번 틀에 수명 tipAge 를 넘은 불 입자 자리에서 연기
    static void TipSmoke(ProtoFlamePack pack, ParticleSystem root, ParticleSystem smoke, ParticleSystem.Particle[] buf)
    {
        if (root == null || smoke == null || pack.tipSmokeChance <= 0f) return;
        int n = root.GetParticles(buf);
        float unit = root.transform.lossyScale.x;
        for (int i = 0; i < n; i++)
        {
            var p = buf[i];
            float life = p.startLifetime; if (life <= 0f) continue;
            float ageNow = 1f - p.remainingLifetime / life, agePrev = 1f - (p.remainingLifetime + Dt) / life;
            if (agePrev >= pack.tipAge || ageNow < pack.tipAge) continue;
            if (UnityEngine.Random.value >= pack.tipSmokeChance) continue;
            EmitSmoke(smoke, p.position, p.velocity * 0.6f + Vector3.up * (1.2f * unit), p.GetCurrentSize(root) * 0.9f, UnityEngine.Random.Range(0.8f, 1.2f));
        }
    }

    static void EmitSmoke(ParticleSystem smoke, Vector3 pos, Vector3 vel, float size, float life)
    {
        smoke.Emit(new ParticleSystem.EmitParams { applyShapeToPosition = false, position = pos, velocity = vel, startSize = size, startLifetime = life }, 1);
    }

    // ProtoFlamePack.SpawnStopDebris 흉내
    static void StopDebris(ProtoFlamePack pack, ParticleSystem root, ParticleSystem smoke, ParticleSystem frags, ParticleSystem sparks, ParticleSystem.Particle[] buf)
    {
        int n = root.GetParticles(buf);
        var body = new List<ParticleSystem.Particle>();
        for (int i = 0; i < n; i++) if (buf[i].startLifetime > 0f && 1f - buf[i].remainingLifetime / buf[i].startLifetime >= 0.3f) body.Add(buf[i]);
        if (body.Count == 0) return;
        float unit = root.transform.lossyScale.x;
        ParticleSystem.Particle Pick(int j, int count) => body[Mathf.Clamp(Mathf.FloorToInt((j + UnityEngine.Random.value) * body.Count / count), 0, body.Count - 1)];
        int sN = smoke != null ? Mathf.Min(pack.stopSmoke, body.Count) : 0;
        for (int j = 0; j < sN; j++) { var p = Pick(j, sN); EmitSmoke(smoke, p.position, p.velocity * 0.6f + Vector3.up * unit, p.GetCurrentSize(root), UnityEngine.Random.Range(0.8f, 1.2f)); }
        int fN = frags != null ? Mathf.Min(pack.stopFragments, body.Count * 2) : 0;
        for (int j = 0; j < fN; j++)
        {
            var p = Pick(j, fN);
            frags.Emit(new ParticleSystem.EmitParams { applyShapeToPosition = false, position = p.position + UnityEngine.Random.insideUnitSphere * unit,
                velocity = p.velocity * 0.45f + UnityEngine.Random.onUnitSphere * (pack.fragmentSpeed * unit * UnityEngine.Random.Range(0.6f, 1.2f)) + Vector3.up * (2f * unit) }, 1);
        }
        int kN = sparks != null ? Mathf.Min(pack.stopSparks, body.Count * 3) : 0;
        for (int j = 0; j < kN; j++)
        {
            var p = Pick(j, kN);
            sparks.Emit(new ParticleSystem.EmitParams { applyShapeToPosition = false, position = p.position + UnityEngine.Random.insideUnitSphere * unit,
                velocity = p.velocity * 0.3f + UnityEngine.Random.onUnitSphere * (pack.sparkSpeed * unit * UnityEngine.Random.Range(0.5f, 1.3f)) + Vector3.up * (3f * unit) }, 1);
        }
    }

    // ProtoFlamePack.TickFade 흉내 — 몸통은 fadeOut 안에 꺼지고 총구 쪽 어린 입자는 snapOff 안에 작아진다
    static void Fade(ProtoFlamePack pack, ParticleSystem[] all, Func<ParticleSystem, bool> linger, float elapsed, ParticleSystem.Particle[] buf)
    {
        float left = pack.fadeOut - elapsed;
        if (left <= 0f) { foreach (var p in all) if (!linger(p)) p.Clear(false); return; }
        float k = Mathf.Clamp01(Dt / left);
        float leftNear = pack.snapOff - elapsed;
        float kNear = leftNear > 0f ? Mathf.Clamp01(Dt / leftNear) : 1f;
        float drag = Mathf.Exp(-pack.fadeDrag * Dt);
        foreach (var ps in all)
        {
            if (linger(ps)) continue;
            int n = ps.GetParticles(buf);
            float unit = ps.transform.lossyScale.x;
            var rise = new Vector3(0f, pack.fadeRise * unit * Dt, 0f);
            for (int i = 0; i < n; i++)
            {
                var p = buf[i];
                p.velocity = p.velocity * drag + rise;
                if (p.startLifetime > 0f && 1f - p.remainingLifetime / p.startLifetime < 0.3f)
                {
                    if (kNear >= 1f) p.remainingLifetime = 0f; else p.startSize *= 1f - kNear;
                }
                else p.remainingLifetime = Mathf.Max(0f, p.remainingLifetime - p.remainingLifetime * k);
                buf[i] = p;
            }
            ps.SetParticles(buf, n);
        }
    }
}
