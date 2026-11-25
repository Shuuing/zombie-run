using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Proto;
using Object = UnityEngine.Object;

/// <summary>
/// 외부 이펙트 팩(MasterStylizedFX)의 프리팹을 <b>우리 폴더로 복사해</b> 무기 세트에 입힌다.
/// 원본은 건드리지 않는다 — 프리팹 · 머티리얼 · 텍스처 · 메시를 전부 복사하고 이름을 바꾸고 참조를 복사본으로 돌린다
/// (팩을 지우거나 갱신해도 우리 효과는 남고, 복사본을 고쳐도 팩은 그대로). 팩 셰이더를 쓰는 머티리얼은 셰이더 파일도 복사하고 안의 이름을
/// "ZombieOverrun/MSFX/원래 이름" 으로 바꾼다(이름이 같은 셰이더가 둘이면 Shader.Find 가 헷갈린다). v3 까지는 셰이더를 복사하지 않았다(빌트인 판이라 어차피 URP Particles/Unlit 로 바꿨다).
///
/// <para><b>복사하면서 고치는 것.</b>
/// ① 중첩 프리팹(Common/Smoke 등)을 완전히 풀어 팩 프리팹과의 연결을 끊는다.
/// ② 빠진 스크립트 · 팩 데모 스크립트(Example) · 점광원을 뗀다 — 점광원은 착탄마다 켜면 모바일 예산을 넘긴다.
/// ③ 머티리얼이 없는 파티클(팩의 OneShotParticle 자리)은 끈다 — 팩에서도 안 그려지는 자리인데, 켜 두면 유니티 판에 따라 기본 머티리얼로 큰 덩어리가 나올 수 있다.
/// ④ URP 셰이더가 아닌 머티리얼은 URP Particles/Unlit 로 바꾼다 — URP 에선 분홍으로 나온다. 블렌드는 원본의 _Mode(표준 파티클) 또는 셰이더 이름(레거시)에서 옮긴다.
///    사용자 결정(2026-11-04): 팩 폴더는 그대로 두고 <b>복사본만 변환</b>한다. → 2026-11-05 바뀜: 팩 프리팹이 전부 분홍이라(빌트인 판) <b>MSFURP 패키지를 가져오고 복사본도 다시</b>
///    만들기로. v4 부터 에디터가 뜰 때 팩이 빌트인 판이면 먼저 <see cref="UrpPackage"/> 를 가져오고(팩 폴더가 URP 판으로 바뀐다), 끝나면 복사본을 URP 판 머티리얼로 다시 만든다.
///    URP 판 머티리얼은 셰이더가 이미 URP 라 ④ 의 변환은 건너뛰고(마스크 같은 기능이 그대로 남는다) 팩 셰이더만 복사한다.
/// ⑤ 파티클을 Hierarchy 스케일로 — 팩은 단위가 커서(섬광 3.5 유닛) 루트를 줄여야 하는데, 팩 기본값(Local)은 루트를 줄여도 입자가 안 줄어든다.
/// ⑥ 원샷 길이를 재서 카탈로그 수명으로 넣는다(팩의 duration 5초는 의미 없는 값 — 버스트 시각 + 입자 수명으로 잰다).</para>
///
/// <para>적용: 카탈로그 <c>rifle_muzzle</c> · <c>rifle_hit</c> 의 프리팹을 복사본으로(ID · 무기 세트는 그대로), 탄은 <c>WS_Rifle_TangTang.projectileModel</c> 로.
/// 되돌리기: 카탈로그 항목의 프리팹을 FX_Rifle_* 템플릿으로, projectileModel 을 비우면 된다.</para>
///
/// <para><b>자동 적용과 판(Version).</b> 복사본 폴더도 Library 표식도 없으면 에디터가 뜰 때 한 번 스스로 입힌다(메뉴를 누를 필요 없이).
/// 복사 규칙이 바뀌면 <see cref="Version"/> 을 올린다 — 표식의 판이 낮으면 복사본을 지우고 다시 만든다. 단, 복사본을 손으로 고친 흔적(표식보다 나중에 저장된 파일)이 있으면
/// 자동으로는 안 하고 메뉴 "복사본 지우고 다시 입히기"에 맡긴다. 표식을 Library 에 두는 이유: 되돌리려고 복사본 폴더를 지운 뒤 다시 자동으로 입히면 안 된다.</para>
/// </summary>
public static class FxPackImport
{
    const string Pack = "Assets/Soo_VFX/MasterStylizedFX";   // 11-12: 사용자가 외부 팩을 Soo_VFX 아래로 모았다 — 팩을 또 옮기면 여기와 FxTemplatePrefabs.PackBloodSheet 를 같이 고칠 것
    const string PackPrefabDir = Pack + "/Prefabs/YellowMachineGun";
    const string DstDir = "Assets/_Game/Art/Sujin/S_VFX/Weapon/Rifle/MSFX";   // 11-23 옮김(옛 Art/VFX/Weapons/Rifle/MSFX — 복사본 머티리얼 · 텍스처, 손보기 전 프리팹 셋은 S_VFX/_Unused/Rifle_MSFX_Original)
    /// 머티리얼 · 텍스처 · 메시 · 셰이더 복사본이 가는 폴더 — 세트마다 다르다(소총 DstDir, 화염방사기 FlameDstDir). CopyEffect 가 맞춘다.
    static string CurDst = DstDir;
    /// 소총 복사본 텍스처(T_MSFX_*) 자리 — 11-25 부터 Sujin 텍스처는 S_Textures 한 곳. 다른 세트는 그 세트 폴더(CurDst).
    const string RifleTexDir = "Assets/_Game/Art/Sujin/S_Textures/VFX";
    static string CurTexDst => CurDst == DstDir ? RifleTexDir : CurDst;
    const string RifleStatsPath = "Assets/_Game/Data/Weapons/Stats/WS_Rifle_TangTang.asset";
    static readonly string AutoMarker = Path.Combine("Library", "FxPackImport_YellowMachineGun.applied");

    /// 복사 규칙의 판. v1(11-04 19:11): 루트 0.22/0.3, 표준 파티클 머티리얼에 ×2 가 잘못 들어감. v2: 루트 0.14/0.18, 블렌드를 _Mode 에서, ×2 는 레거시만, 머티리얼 없는 파티클 끔.
    /// v3: 착탄 0.18 → 0.36 (사용자: "피격 효과가 너무 안 보인다"). v4(11-05): MSFURP 패키지를 가져온 뒤 URP 판 머티리얼로 다시 복사, 팩 셰이더도 복사.
    const int Version = 4;

    // 팩은 기본이 빌트인 파이프라인이다(ReadMe). 셰이더가 전부 빌트인 서피스 셰이더(#pragma surface)라 URP 에선 분홍으로 나온다.
    // 가져왔는지는 대표 셰이더 파일에 #pragma surface 가 남아 있는지로 안다.
    const string UrpPackage = Pack + "/MSFURP.unitypackage";
    const string ProbeShader = Pack + "/Shaders/CommonParticleOneShot.shader";
    /// 패키지 가져오기를 시도했다는 표식(판 번호) — 가져왔는데도 빌트인으로 보이면 같은 판에서 다시 시도하지 않는다(무한 반복 방지).
    static readonly string UrpAttemptMarker = Path.Combine("Library", "FxPackImport_MSFURP.attempted");

    // 팩 단위계 → 우리 단위계. 팩 총구 섬광 중심이 3.5 유닛 — 소총(길이 0.45m)엔 0.5m 쯤이 맞아 0.14 배.
    // 착탄은 0.36 배(중심 섬광 0.72m, 반짝임 0.41m) — 0.18 은 사용자가 "너무 안 보인다"고 봤다. 카메라 17.8m 에서 몸 폭보다 조금 커야 한 발 한 발이 읽힌다.
    // 탄은 1 — 발사체 모델은 ProtoProjectile 이 projectileSize(0.22)로 스케일한다.
    const float MuzzleScale = 0.14f, HitScale = 0.36f, BulletScale = 1f;

    static bool PackIsUrp()
    {
        if (!File.Exists(ProbeShader)) return true;   // 없으면 판단 못 한다 — 막지 않는다
        return !File.ReadAllText(ProbeShader).Contains("#pragma surface");
    }

    // ── 자동 적용 ────────────────────────────────────────────────────
    [InitializeOnLoadMethod]
    static void AutoApplyOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(PackPrefabDir)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { AutoApplyOnce(); return; }   // 다음 틀에 다시
            int ver = MarkerVersion();
            bool has = AssetDatabase.IsValidFolder(DstDir);
            if (ver >= Version) return;
            if (ver == 0 && has) { WriteMarker(); return; }   // 표식 없이 복사본만 있다(다른 PC · 버전 관리) — 건드리지 않는다
            try
            {
                // v4: 팩이 아직 빌트인 판이면 먼저 MSFURP 패키지를 가져온다(사용자 결정 11-05). 끝나면 OnUrpPackageImported 가 이어서 복사본을 만든다.
                // 가져오는 중 팩 스크립트가 바뀌어 도메인이 다시 뜨면 콜백은 사라지지만 이 메서드가 다시 돌고, 그때는 팩이 URP 판이라 아래 복사 단계로 간다.
                if (!PackIsUrp() && File.Exists(UrpPackage))
                {
                    if (UrpImportAttempted())
                    {
                        Debug.LogError("[FX 팩] MSFURP.unitypackage 를 가져왔는데도 팩 셰이더가 빌트인으로 보인다 — 패키지 내용을 확인할 것. 자동 적용은 여기서 멈춘다(메뉴 '소총에 MasterStylizedFX 입히기' 로 다시).");
                        WriteMarker();
                        return;
                    }
                    StartUrpImport();
                    return;   // 표식은 복사본까지 끝난 뒤에 쓴다
                }
                if (ver == 0)
                {
                    Debug.Log("[FX 팩] YellowMachineGun 을 소총에 자동으로 입힌다");
                    Apply();
                }
                else
                {
                    if (CopiesEditedSinceMarker())
                    {
                        Debug.LogWarning($"[FX 팩] 복사 규칙이 v{ver} → v{Version} 로 바뀌었지만 복사본을 손으로 고친 흔적이 있어 자동으로 다시 만들지 않는다 — " +
                                         "메뉴 '복사본 지우고 다시 입히기' 로. (안 고쳤으면 표식 시각이 잘못된 것 — 같은 메뉴를 누르면 된다)");
                        WriteMarker();
                        return;
                    }
                    Debug.Log($"[FX 팩] 복사 규칙 v{ver} → v{Version}: 복사본을 지우고 다시 만든다 (v4: 팩 URP 판 머티리얼 · 셰이더 복사)");
                    Reapply();
                }
                WriteMarker();
            }
            catch (Exception e) { Debug.LogError("[FX 팩] 자동 적용 실패 — 메뉴 '소총에 MasterStylizedFX 입히기' 로 다시: " + e); WriteMarker(); }
        };
    }

    static int MarkerVersion()
    {
        if (!File.Exists(AutoMarker)) return 0;
        var m = Regex.Match(File.ReadAllText(AutoMarker), @"v(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 1;   // 판이 안 적힌 옛 표식 = v1
    }

    static void WriteMarker()
    {
        try { File.WriteAllText(AutoMarker, $"v{Version} {DateTime.Now:s}"); } catch (Exception) { }
    }

    static bool UrpImportAttempted()
    {
        if (!File.Exists(UrpAttemptMarker)) return false;
        var m = Regex.Match(File.ReadAllText(UrpAttemptMarker), @"v(\d+)");
        return m.Success && int.Parse(m.Groups[1].Value) >= Version;
    }

    /// 팩의 URP 패키지를 조용히(대화상자 없이) 가져온다. 팩 폴더 안 535개(머티리얼 240 · 프리팹 149 · 셰이더 14 · 모델 36 · 스크립트 7 · 데모 씬)가 URP 판으로 덮인다.
    /// 끝나면 <see cref="OnUrpPackageImported"/> 가 복사본을 다시 만든다(메뉴 · 자동 둘 다 같은 길).
    static void StartUrpImport()
    {
        try { File.WriteAllText(UrpAttemptMarker, $"v{Version} {DateTime.Now:s}"); } catch (Exception) { }
        AssetDatabase.importPackageCompleted -= OnUrpPackageImported;  AssetDatabase.importPackageCompleted += OnUrpPackageImported;
        AssetDatabase.importPackageFailed    -= OnUrpPackageFailed;    AssetDatabase.importPackageFailed    += OnUrpPackageFailed;
        AssetDatabase.importPackageCancelled -= OnUrpPackageCancelled; AssetDatabase.importPackageCancelled += OnUrpPackageCancelled;
        Debug.Log($"[FX 팩] 팩이 빌트인 판이라 분홍으로 나온다 — {UrpPackage} 를 가져온다(팩 폴더가 URP 판으로 바뀐다). 끝나면 소총 복사본을 다시 만든다.");
        AssetDatabase.ImportPackage(UrpPackage, false);
    }

    /// 복사본 중에 표식보다 나중에 저장된 파일이 있나 — 사용자가 손댄 흔적.
    static bool CopiesEditedSinceMarker()
    {
        if (!File.Exists(AutoMarker) || !Directory.Exists(DstDir)) return false;
        var limit = File.GetLastWriteTime(AutoMarker).AddSeconds(30);
        return Directory.GetFiles(DstDir, "*.*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")).Any(f => File.GetLastWriteTime(f) > limit);
    }

    // ── 메뉴 ─────────────────────────────────────────────────────────
    [MenuItem("Tools/좀비 오버런/FX 툴/소총에 MasterStylizedFX (YellowMachineGun) 입히기")]
    public static void ApplyYellowMachineGunToRifle()
    {
        if (!AssetDatabase.IsValidFolder(PackPrefabDir)) { Debug.LogError($"[FX 팩] 팩 폴더가 없다: {PackPrefabDir}"); return; }

        // 빌트인 버전이면 두 길 — 복사본만 변환(팩 그대로, 모양 단순해짐) 또는 팩의 URP 패키지를 가져오기(모양 그대로, 팩 폴더가 바뀐다)
        if (!PackIsUrp() && File.Exists(UrpPackage))
        {
            int pick = EditorUtility.DisplayDialogComplex("MasterStylizedFX 는 빌트인 버전",
                "팩 셰이더가 빌트인 서피스 셰이더라 URP 에선 분홍으로 나온다.\n\n" +
                "· 복사본만 변환: 팩 폴더는 그대로, 복사본 머티리얼을 URP Particles/Unlit 로 바꾼다(마스크 텍스처 같은 기능은 잃는다).\n" +
                "· URP 패키지 가져오기: 팩에 든 MSFURP.unitypackage 를 가져와 팩 셰이더·머티리얼을 URP 판으로 바꾸고(팩 폴더가 바뀐다) 이어서 복사본을 다시 만든다.",
                "복사본만 변환", "취소", "URP 패키지 가져오기");
            if (pick == 1) return;
            if (pick == 2) { StartUrpImport(); return; }
        }

        bool has = AssetDatabase.IsValidFolder(DstDir);
        if (!EditorUtility.DisplayDialog("MasterStylizedFX 입히기",
                (has ? $"복사본 폴더 {DstDir} 가 이미 있다 — 지우고 새로 만든다(손으로 고친 게 있으면 사라진다).\n\n" : "") +
                $"YellowMachineGun 총구 · 착탄 · 탄 프리팹을 {DstDir} 로 복사하고(머티리얼 · 텍스처 · 메시까지, 이름 바꿔)\n" +
                "카탈로그 rifle_muzzle · rifle_hit 와 WS_Rifle_TangTang 의 발사체 모델이 복사본을 가리키게 한다.\n원본은 건드리지 않는다. 계속할까?",
                has ? "지우고 다시 입힌다" : "입힌다", "취소")) return;
        if (has) Reapply(); else Apply();
        WriteMarker();
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/소총 MasterStylizedFX 복사본 지우고 다시 입히기")]
    public static void ReapplyMenu() => ApplyYellowMachineGunToRifle();

    static void UnhookUrpImport()
    {
        AssetDatabase.importPackageCompleted -= OnUrpPackageImported;
        AssetDatabase.importPackageFailed    -= OnUrpPackageFailed;
        AssetDatabase.importPackageCancelled -= OnUrpPackageCancelled;
    }

    static void OnUrpPackageImported(string packageName)
    {
        UnhookUrpImport();
        if (!PackIsUrp())
        {
            Debug.LogError($"[FX 팩] {packageName} 을 가져왔는데 대표 셰이더({ProbeShader})가 아직 빌트인(#pragma surface)으로 보인다 — 패키지 내용을 확인할 것. 복사본은 그대로 둔다.");
            WriteMarker();
            return;
        }
        try { File.Delete(UrpAttemptMarker); } catch (Exception) { }
        // 임포트 직후 한 틀 뒤에 — 에셋 DB 가 새 셰이더 · 머티리얼을 다 알게 된 뒤 복사한다
        EditorApplication.delayCall += () =>
        {
            try
            {
                if (CopiesEditedSinceMarker())
                {
                    Debug.LogWarning($"[FX 팩] {packageName} 가져옴. 복사본을 손으로 고친 흔적이 있어 자동으로 다시 만들지 않는다 — 메뉴 '복사본 지우고 다시 입히기' 로.");
                    WriteMarker();
                    return;
                }
                Debug.Log($"[FX 팩] {packageName} 가져옴 — 소총 복사본을 URP 판 머티리얼로 다시 만든다");
                if (AssetDatabase.IsValidFolder(DstDir)) Reapply(); else Apply();
                WriteMarker();
            }
            catch (Exception e) { Debug.LogError("[FX 팩] 패키지는 가져왔지만 복사본 다시 만들기 실패 — 메뉴 '복사본 지우고 다시 입히기' 로: " + e); WriteMarker(); }
        };
    }

    static void OnUrpPackageFailed(string packageName, string error)
    {
        UnhookUrpImport();
        Debug.LogError($"[FX 팩] {packageName} 가져오기 실패: {error} — 프로젝트 창에서 {UrpPackage} 를 더블클릭해 직접 가져온 뒤 메뉴 '소총에 MasterStylizedFX 입히기'");
        WriteMarker();
    }

    static void OnUrpPackageCancelled(string packageName)
    {
        UnhookUrpImport();
        Debug.LogWarning($"[FX 팩] {packageName} 가져오기가 취소됐다 — 메뉴 '소총에 MasterStylizedFX 입히기' 에서 다시 고를 수 있다");
        WriteMarker();
    }

    // ── 본체 ─────────────────────────────────────────────────────────
    /// 복사본 폴더를 지우고 처음부터. 카탈로그 · 수치 에셋의 참조는 Apply 가 새 복사본으로 다시 건다.
    static void Reapply()
    {
        ProtoFx.Clear();
        if (AssetDatabase.IsValidFolder(DstDir)) AssetDatabase.DeleteAsset(DstDir);
        // 텍스처는 S_Textures 에 따로 있다 — 소총 복사본(T_MSFX_*)도 같이 지워야 이름 뒤에 " 1" 이 붙은 사본이 안 생긴다
        if (AssetDatabase.IsValidFolder(RifleTexDir))
            foreach (var g in AssetDatabase.FindAssets("T_MSFX_ t:Texture", new[] { RifleTexDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileName(p).StartsWith("T_MSFX_")) AssetDatabase.DeleteAsset(p);
            }
        Apply();
    }

    static void Apply()
    {
        EnsureFolder(DstDir);
        var copies = new Dictionary<string, Object>();   // 원본 경로 → 복사본. 머티리얼 · 텍스처 · 메시를 두 번 복사하지 않게
        var muzzle = CopyEffect(PackPrefabDir + "/YellowMachineGunMuzzle.prefab", "FX_Rifle_Muzzle_MSFX", MuzzleScale, copies, out float muzzleLen);
        var hit    = CopyEffect(PackPrefabDir + "/YellowMachineGunHit.prefab",    "FX_Rifle_Hit_MSFX",    HitScale,    copies, out float hitLen);
        var bullet = CopyEffect(PackPrefabDir + "/YellowMachineGunBullet.prefab", "FX_Rifle_Bullet_MSFX", BulletScale, copies, out _);

        var cat = FxCatalogEdit.Catalog;
        if (cat != null)
        {
            Assign(cat, FxIds.RifleMuzzle, muzzle, muzzleLen);
            Assign(cat, FxIds.RifleHit, hit, hitLen);
            EditorUtility.SetDirty(cat);
        }
        else Debug.LogWarning("[FX 팩] 카탈로그가 없다 — [카탈로그·시트 만들기] 뒤에 다시");

        var st = AssetDatabase.LoadAssetAtPath<WeaponStats>(RifleStatsPath);
        if (st != null && bullet != null)
        {
            st.projectileModel = bullet;
            st.projectileFacesTravel = true;   // 팩 탄은 +Z 로 날아가게 만들어졌다(BulletShooter 가 transform.forward 로 민다)
            EditorUtility.SetDirty(st);
        }
        else if (st == null) Debug.LogWarning($"[FX 팩] 소총 수치 에셋이 없다: {RifleStatsPath}");

        AssetDatabase.SaveAssets();
        FxCatalog.Reload();
        ProtoFx.Clear();   // 풀에 남은 옛 프리팹 인스턴스
        if (FxShowcase.Instance != null) FxShowcase.Instance.Reload();
        Debug.Log($"[FX 팩] YellowMachineGun 입힘(v{Version}) — 총구 {Name(muzzle)}(루트 ×{MuzzleScale}, 수명 {muzzleLen:0.00}s) · 착탄 {Name(hit)}(루트 ×{HitScale}, 수명 {hitLen:0.00}s) · 탄 {Name(bullet)} → {DstDir}. " +
                  $"복사한 에셋 {copies.Count}개. 크기는 [무기] 탭의 슬롯 배율로, 자리는 세트의 muzzleOffset 으로 더 맞춘다. " +
                  "되돌리려면 카탈로그 rifle_muzzle · rifle_hit 의 프리팹을 FX_Rifle_* 로, WS_Rifle_TangTang.projectileModel 을 비운다.");
        if (muzzle != null) EditorGUIUtility.PingObject(muzzle);
    }

    static string Name(Object o) => o != null ? o.name : "(실패)";

    // ── 화염방사기 — RedFlameThrower (2026-11-05, 사용자 "RedFlameThrower 프리팹 이펙트를 복사해서 W_Flamethrower 무기 이펙트로") ─────────
    // 소총과 같은 복사 규칙(중첩 풀기 · 스크립트 · 점광원 제거 · 머티리얼 · 텍스처 · 셰이더 복사, Hierarchy 스케일). 루트 스케일은 1 — 런타임 ProtoFlamePack 이
    // 프리팹 루트 제트의 "속도 × 수명"(28 × 1.5 = 42 팩 단위)을 사거리로 나눠 맞춘다(사거리가 바뀌어도 그림이 따라간다).
    // 적용: 카탈로그 flamethrower_jet 의 프리팹 = 복사본(스팬, 수명 0), WFX_Flamethrower.jet = flamethrower_jet. 되돌리기: jet 슬롯의 ID 를 비우면 ProtoFlameCone(FX_FlameJet) 로.
    const string FlameSrc = Pack + "/Prefabs/FireThrower/RedFlameThrower.prefab";
    const string FlameDstDir = "Assets/_Game/Art/VFX/Weapons/Flamethrower/MSFX";
    const string FlameFxSetPath = "Assets/_Game/Data/Weapons/Fx/WFX_Flamethrower.asset";
    static readonly string FlameMarker = Path.Combine("Library", "FxPackImport_RedFlameThrower.applied");
    /// 손편집 표식(2026-11-06) — 복사본을 사람이 고친 흔적을 한 번이라도 봤으면 남긴다. 있는 한 판이 올라도 **자동으로는 절대 다시 만들지 않는다**(메뉴 · 배치 진입점만 지운다).
    /// 예전엔 건너뛰며 표식(판)만 새로 써서 다음 판에서 "손댄 흔적 없음"으로 보고 지워 버렸다 — 사용자가 에디터에서 고친 프리팹이 v15 · v17 재생성에 두 번 덮였다.
    static readonly string FlameHandEdited = Path.Combine("Library", "FxPackImport_RedFlameThrower.handedited");
    /// 화염방사기 복사 규칙의 판. v1(11-05): 팩 그대로 복사. v2: 참고 영상(사용자 sssss.mp4 — 굵고 밝은 노란 심이 주황 불기둥으로 퍼지며 끝에서 부풀어 위로 말리는 2D 화염방사기)
    /// 에 맞춰 <see cref="StyleFlameJet"/> 로 손질 — 복사본 머티리얼 · 텍스처도 이 폴더로(v1 은 소총 폴더에 떨어졌다). 판이 낮으면 복사본을 지우고 다시 만든다(손으로 고친 흔적이 있으면 메뉴로).
    /// v3: 사용자 "전혀 다르다 — 총구에서 퍼져 나가는 부채꼴이어야 하는데 총구 자리도 안 맞는다": 팩의 Head(불머리)가 루트 뒤 1.9 유닛에서 뒤로 흐르고 Smoke 도 뒤에 있어
    /// 불이 총 몸통에서 나오는 것처럼 보였다 → 셋 다 원점(총구)으로, Head 는 앞으로 조금. 부채꼴은 ProtoFlamePack.coneFraction 1(판정 부채꼴 그대로) + 속도 폭을 좁혀 가장자리가 깨끗하게.
    /// v4: 사용자 "총구에서 안 나간다" — 로그에 "Particle Velocity curves must all be in the same mode" 가 방출을 켤 때마다 찍혔다: 속도-수명 모듈의 x · z 를 상수, y 를 곡선으로 넣어
    /// 모드가 섞였고 유니티가 모듈을 거부했다(불이 안 나오거나 엉뚱하게). 셋 다 곡선(상수 0 곡선)으로.
    /// v5: 사용자 스크린샷 — 불이 총구가 아니라 캐릭터 몸 · 손에서 시작했다(v3 화면). 늘림 빌보드는 입자 자리를 가운데로 두고 앞뒤로 늘어나는데, 태어날 때 크기(0.35 × 0.6~1m)에 길이 1.4 와
    /// 속도 늘림(5.5m/s × 0.1)을 더해 약 1m 짜리 조각이 총구 뒤 0.5m 까지 걸쳤다. → 태어날 때 작게(0.12, 영상처럼 총구에서 가늘게 시작해 부채꼴로), 늘림 1.0 · 속도 0.04, 발사 원점 0.4 앞,
    /// 노이즈는 수명 따라 커지게(갓 태어난 입자가 뒤로 안 흔들리게), 불머리는 작고 조금 앞에.
    /// v6: 사용자 "너무 노란 불꽃 — 붉은 불꽃으로", "사거리가 적 앞쪽까지만 — 끝까지": 색을 주황-빨강 계열로(심은 밝은 주황, 끝은 검붉음), 알파를 수명 0.65 까지 유지(끝이 일찍 꺼져 짧아 보였다).
    /// 길이 자체는 ProtoFlamePack.visibleFraction 0.8 → 0.62, 시작점은 muzzleForward 0.2m.
    /// v7: 사용자 "방출될 때 주변으로 불꽃 파편도 자연스럽게 튀게" — 잔불(Embers) 시스템을 더한다: 불줄기 전체(콘 부피)에서 바깥으로 튀는 작은 가산 점, 중력에 떨어지며 식는다.
    /// v8: 사용자 "파편이 너무 멀리 나간다 · 옆으로도 튀게" — 잔불 속도 8~26 → 4~12, 수명 0.5~1.1 → 0.35~0.8, 끌림 1.5 → 2.5, 콘 18° → 35° · 무작위 방향 0.35 → 0.7 · 구면 0.15 → 0.3.
    /// v9: 사용자 "불꽃보다 파편이 더 길다" — 잔불이 태어나는 콘 길이가 18(불줄기 길이)이고 거기서 또 4 유닛을 날아 끝이 22.8 팩 단위, 눈에 보이는 불끝(23 × visibleFraction 0.62 = 14.3)의 1.6배였다.
    /// → 콘 길이 8 · 속도 3~7 · 수명 0.25~0.55 · 끌림 3 으로 끝 ≈ 10.5(보이는 불꽃의 73%) — 불꽃 안쪽에서 튀고 꺼진다.
    /// v10: 사용자 참고 영상(gemini_generated_video — 실사풍 화염방사기: 총구에 짧은 청백색 심 → 크림 · 주황 불덩이가 부풀며 위로 말림 → 그 위로 검은 연기가 피어오름 →
    /// 작은 불똥이 위로 떠오름 → 멈추면 불줄기가 총구에서 떨어져 나가 떠오르며 연기로 바뀐다) "검은 연기도 섞이고 불꽃 파편도 튀고 자연스럽게 생기고 사라지게".
    /// Head = 청백색 총구 심(짧게), Smoke = 검은 연기 기둥(월드 공간 · 불줄기 중간~끝에서 태어나 떠오르며 부풂 · 알파 서서히), Embers = 위로 떠오르는 불똥(중력 −),
    /// 불줄기 끝을 조금 더 부풀게. 생기고 사라지는 흐름은 ProtoFlamePack(방출 램프 · 떨어져 떠오르는 페이드 · 멈출 때 연기 한 번).
    /// v11: 사용자 참고 영상(effect_01.mp4 — 2D 카툰 화염방사기: 총구 심이 끊기면 불줄기 몸통이 떠가며 검붉게 식어 연기가 되고, 그 사이로 작은 불꽃 조각과 불똥이 흩어진다)
    /// "사라질 때 불꽃이 사그라들고 연기가 나오고 불꽃 파편이 날리게 · 움직일 때 덩어리가 아니라 계속 나오는 불처럼".
    /// ① 불줄기 · 총구 심을 <b>월드 공간</b>으로(로컬이면 총구를 돌릴 때 줄기 전체가 통째로 따라 돌아 덩어리 하나로 보였다) + 이동 속도 일부 물려받기 + 걸은 거리당 방출.
    /// ② Head(총구 심)를 켠다 — 팩 원본이 꺼 둔 오브젝트라 v10 의 심 손질이 화면에 안 나왔다. 색은 영상처럼 노란-흰색 → 주황. 멈추면 ProtoFlamePack 이 잠깐 가늘어지며 끈다.
    /// ③ 새 시스템 둘 — Fragments(불꽃 파편: 불 머티리얼 그대로의 작은 조각, 분사 중엔 줄기 끝에서 조금씩, 멈출 땐 몸통 자리에서 한 번에) · Sparks(멈출 때 흩어지는 불똥, 떨어진다).
    ///    멈출 때 연기 · 파편 · 불똥을 "남은 불 입자 자리"에서 내는 건 ProtoFlamePack.SpawnStopDebris.
    /// v12: 사용자 "검은 연기가 연기 텍스처가 아닌 것 같다 · 너무 오래 살아 있다" — v10 연기는 불 머티리얼(불 시트 + 녹는 셰이더)을 어둡게 칠한 것이라 검은 불덩이로 보였고,
    /// 팩 원본의 시뮬레이션 속도 0.5 가 남아 수명 1~1.7초가 실제론 2~3.4초였다. → 팩의 연기 머티리얼(<see cref="PackSmokeMat"/>: URP Particles/Unlit 알파 블렌드 + 6×6 연기 플립북)을
    /// 복사해 쓰고(M_MSFX_Smoke · T_MSFX_Smoke3), 플립북을 수명 동안 한 바퀴(뭉게 → 흩어짐), 속도 1 · 수명 0.7~1.1초, 멈출 때 연기도 0.8~1.2초(ProtoFlamePack).
    /// v13: 사용자 "불 이펙트 끝에 검은 연기가 되는 느낌으로" — 연기를 모양(콘 부피)에서 따로 내지 않는다(방출 0). 대신 ProtoFlamePack.TickTipSmoke 가 수명 72% 를 넘는 불 입자
    ///      자리 · 속도 · 크기에서 연기를 낸다(불줄기 끝에서 불덩이가 그대로 검은 연기로). 연기 색은 더 검게(희미한 온기만), 불줄기 끝 색도 더 어둡게 식는다.
    /// v14: 사용자 스크린샷(불줄기 끝 — 붉게 녹아 사라지는 구간에 파란 동그라미) "이 부분이 검은 연기로 변해서 날아가면서 사라지게" — 불 입자 자체가 수명 0.6 → 0.8 → 1 에서
    ///      빨강 → 검붉음 → 그을음(거의 검정)으로 식고 알파를 0.85 까지 0.7 로 더 붙잡아 끝 덩어리가 검게 변하는 게 보이게. 연기는 위로 덜 띄우고(y 5 → 2.5) 끌림을 줄여(1.8 → 1.2)
    ///      불줄기 방향으로 날아가며 흩어진다(ProtoFlamePack 도 불 속도의 75% 를 물려준다).
    /// v15: 사용자 "검은 연기가 불꽃이 사라질 때 나오는 게 아니라 끝부분에서 연기가 나오게" — v12 에서 연기 머티리얼을 프로젝트 연기 규칙(큐 2850, 밤 어둠 막 앞)으로 두었는데
    ///      불은 큐 3000 이라 유니티가 큐 순서대로 연기를 **항상 불보다 먼저** 그렸다 → 분사 중 끝에서 난 연기는 불덩이에 덮여 안 보이고, 불이 꺼질 때만 드러났다(정렬 fudge 는 같은 큐 안에서만 듣는다).
    ///      연기 큐를 불과 같은 3000 으로(fudge −1 로 불 앞에). 연기는 더 일찍(수명 62%, 불덩이가 아직 꽉 찬 끝부분) 위 · 앞으로 피어오르고(ProtoFlamePack.tipAge), 불 끝 색은 v14 의 그을음 대신 검붉게만.
    /// v16: 사용자 "불꽃 텍스처를 수정 — 무빙할 때 불 이펙트가 불꽃 모양 느낌이 안 난다" — 불 입자의 바탕(_Base)이 팩 원본의 뭉게구름 시트(SmokeSheet 4×4)라 월드 공간에서 걸으며 쏘면
    ///      둥근 덩어리 꼬리로 보였다. 팩의 불꽃 혀 시트(<see cref="PackFireTex"/>, 3×3: 혀 3종 → 갈라짐 → 조각)로 바꾸고 수명 동안 한 바퀴(앞 55% 는 혀 3종을 천천히 오가며 일렁임, 뒤는 갈라져 조각).
    ///      혀 끝은 텍스처에서 왼쪽(−U)인데 늘림 빌보드는 텍스처 U 축을 속도 방향에 놓는다(_StretchUvProbe 로 잼) → <see cref="TongueFlipX"/> 로 끝이 진행 방향(사용자 시트처럼)을 보게.
    ///      이동 속도 물려받기 0.35 → 0.5(걸을 때 줄기가 덜 뒤처져 불꽃 줄기로 읽힌다), 크기 2.6~3.8 → 3.2~4.4(혀는 칸의 가운데 띠만 차지한다). Head · Fragments 도 같은 머티리얼이라 3×3 에 맞춘다.
    /// v17: 늘림 빌보드의 텍스처 방향을 BakeMesh 로 쟀다(임시 _StretchUvProbe, 지움) — 입자 자리가 **앞끝**이고 꼬리가 뒤로 늘어나며, 텍스처 U=0(왼쪽)이 진행 방향 · U=1 이 꼬리.
    ///      팩 혀 시트는 끝이 왼쪽이라 뒤집지 않으면(flip 0) 끝이 진행 방향을 본다 — 사용자 시트(노즐에서 뻗는 줄기, 혀 끝이 앞)와 같은 방향.
    const int FlameVersion = 17;
    /// 불꽃 혀 가로 뒤집기 확률(렌더러 flip.x). 0 = 혀 끝이 늘 진행 방향(측정값 기준), 1 = 늘 꼬리 쪽, 0.5 = 팩의 Fires 처럼 반반(일렁이지만 방향 없음).
    const float TongueFlipX = 0f;
    /// 팩의 연기 머티리얼 — Common/Smoke 프리팹이 쓰는 것. URP Particles/Unlit, 알파 블렌드, Textures/Bullet/Smoke3.png(6×6 플립북, 회색 음영 연기).
    const string PackSmokeMat = Pack + "/Materials/Bullet/Smoke.mat";
    /// 팩의 불꽃 혀 시트 — FireProjectile · FireSwordBeam 의 Fires 가 쓰는 것. 768², 3×3(첫 줄 혀 3종, 둘째 줄 갈라진 혀, 셋째 줄 조각), 알파가 모양.
    const string PackFireTex = Pack + "/Textures/Common/Fire.png";
    /// 연기 큐 — 불(CommonFire, Transparent 3000)과 같은 큐여야 렌더러의 sortingFudge 가 앞뒤를 정한다(큐가 다르면 큐 순서가 먼저라 fudge 가 안 듣는다 — v12~14 의 2850 은 늘 불 뒤였다).
    /// FxTemplatePrefabs.SmokeQueue(2850, 밤 어둠 막 앞)는 안 쓴다 — 이 연기는 불 위에 보여야 하고 거의 검정이라 밤에 떠 보이지 않는다.
    const int SmokeQueue = 3000;

    // 2026-11-23: 화염방사기 MSFX 복사본(Weapons/Flamethrower/MSFX)을 지웠다 — 화염방사기 · 인페르노 모두 인페르노 불줄기(inferno_jet)를 쓴다.
    // 예전엔 카탈로그 flamethrower_jet 이 비어 있으면 에디터가 켜질 때 팩을 다시 복사했다 — 이제 자동으로는 안 한다(메뉴로만, 백업 _Backup/20261123_SujinFolder).
    static readonly bool FlameAutoApply = false;

    [InitializeOnLoadMethod]
    static void AutoApplyFlameOnce()
    {
        if (!FlameAutoApply) return;
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FlameSrc) == null) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { AutoApplyFlameOnce(); return; }
            if (!PackIsUrp()) return;   // 소총 쪽 자동 흐름이 URP 패키지를 먼저 가져온다 — 그 뒤 로드에서 온다
            int ver = MarkerVersion(FlameMarker);
            bool first = ver == 0;
            // 판이 올랐으면(손질 규칙이 바뀜) 복사본을 지우고 다시 — 손으로 고친 흔적이 있으면 자동으로는 안 한다
            if (!first && ver < FlameVersion)
            {
                if (File.Exists(FlameHandEdited) || EditedSince(FlameMarker, FlameDstDir))
                {
                    // 손편집 표식을 남기고 판만 맞춘다(경고가 판마다 한 번) — 표식이 있는 한 다음 판에서도 자동 재생성은 없다. 사람이 고친 프리팹이 우선이다
                    try { File.WriteAllText(FlameHandEdited, $"v{ver}→v{FlameVersion} {DateTime.Now:s}"); } catch (Exception) { }
                    Debug.LogWarning($"[FX 팩] 화염방사기 복사 규칙이 v{ver} → v{FlameVersion} 로 바뀌었지만 복사본을 손으로 고친 흔적이 있어 자동으로 다시 만들지 않는다(앞으로도 안 한다) — 규칙을 다시 입히려면 메뉴 '화염방사기에 MasterStylizedFX 입히기'(손편집이 사라진다)");
                    WriteFlameMarker();
                    return;
                }
                Debug.Log($"[FX 팩] 화염방사기 복사 규칙 v{ver} → v{FlameVersion}: 복사본을 지우고 다시 만든다(참고 영상 느낌으로 손질)");
                ProtoFx.Clear();
                if (AssetDatabase.IsValidFolder(FlameDstDir)) AssetDatabase.DeleteAsset(FlameDstDir);
                CleanFlameV1Leftovers();
                try { if (ApplyFlamethrower()) WriteFlameMarker(); }
                catch (Exception e) { Debug.LogError("[FX 팩] 화염방사기 다시 만들기 실패 — 메뉴로 다시: " + e); WriteFlameMarker(); }
                return;
            }
            // 표식이 있어도 카탈로그 연결이 비어 있으면 다시 잇는다(복사본은 재사용) — 11-05 처음 돌 때 카탈로그 항목보다 먼저 돌아 프리팹이 안 걸린 채 표식만 남았다(사용자 "이펙트 연결이 안 되어 있다").
            // 세트(WFX_Flamethrower.jet)는 보지 않는다 — 사용자가 되돌리려고 비운 것일 수 있다.
            if (!first && !FlameLinkBroken()) return;
            // 복사본이 이미 있으면(다른 PC) 다시 복사하지 않고 카탈로그 · 세트만 잇는다. 카탈로그 · 세트가 아직 없으면(처음 뜰 때 순서) 표식을 안 써 다음 로드에 다시 온다
            try { Debug.Log(first ? "[FX 팩] RedFlameThrower 를 화염방사기에 자동으로 입힌다" : "[FX 팩] 카탈로그 flamethrower_jet 에 프리팹이 안 걸려 있다 — 복사본을 다시 잇는다"); if (ApplyFlamethrower()) WriteFlameMarker(); }
            catch (Exception e) { Debug.LogError("[FX 팩] 화염방사기 자동 적용 실패 — 메뉴 '화염방사기에 MasterStylizedFX 입히기' 로 다시: " + e); WriteFlameMarker(); }
        };
    }

    /// <summary>
    /// 복사본을 참고 영상 느낌으로 손질한다(v2, 사용자 "이런 느낌으로 수정" — sssss.mp4: 2D 화염방사기, 총구에서 굵고 밝은 노란-흰 심이 곧게 뻗다 주황 불기둥으로 넓어지고
    /// 끝에서 느려지며 부풀어 위로 말려 올라간다. 연기는 거의 없다). 팩 원본은 가는 붉은 제트(색 1, 0.24, 0 · 알파 0.37 · 방출 20/s · 콘 5.8°).
    /// 값은 팩 단위(루트 스케일은 런타임 ProtoFlamePack 이 사거리로 맞춘다 — 속도 · 크기 · 끌림(drag)은 Hierarchy 스케일을 그대로 따라간다).
    /// </summary>
    static void StyleFlameJet(string prefabPath)
    {
        // v12: 연기 머티리얼은 팩에서 따로 복사한다(불 머티리얼과 다른 셰이더). 복사본은 화염 폴더로
        CurDst = FlameDstDir;
        var copies = new Dictionary<string, Object>();
        var packSmoke = AssetDatabase.LoadAssetAtPath<Material>(PackSmokeMat);
        var smokeMat = packSmoke != null ? CopyMaterial(packSmoke, copies) : null;
        if (smokeMat != null)
        {
            smokeMat.renderQueue = SmokeQueue;
            if (smokeMat.HasProperty("_BaseColor")) smokeMat.SetColor("_BaseColor", Color.white);   // 색은 입자가 정한다(startColor × 수명 색)
            EditorUtility.SetDirty(smokeMat);
        }
        else Debug.LogWarning($"[FX 팩] 팩 연기 머티리얼이 없다: {PackSmokeMat} — 연기는 불 머티리얼 그대로(검은 불덩이처럼 보인다)");
        // v16: 불꽃 혀 시트 — 불 머티리얼(M_MSFX_CommonFire, 루트 · Head · Fragments 공용)의 _Base 를 바꾼다
        var packFire = AssetDatabase.LoadAssetAtPath<Texture>(PackFireTex);
        var fireTex = packFire != null ? CopyTexture(packFire, copies) : null;
        if (fireTex == null) Debug.LogWarning($"[FX 팩] 팩 불꽃 혀 시트가 없다: {PackFireTex} — 불은 뭉게구름 시트 그대로");

        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var em = ps.emission;
                var col = ps.colorOverLifetime;
                var sz = ps.sizeOverLifetime;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                bool isRoot = ps.gameObject == root;
                if (isRoot)
                {
                    // 불기둥 — 밝은 노란-흰 심(알파 0.37 → 0.75 로 또렷하게), 수명 따라 흰 → 노랑 → 주황 → 검붉게 식고 끝에서 꺼진다
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.28f, 0.75f));   // v6: 노랑(1, 0.92, 0.6) → 주황-빨강
                    col.enabled = true;
                    col.color = new ParticleSystem.MinMaxGradient(Grad(
                        // v6: 밝은 주황 심 → 주황-빨강 → 빨강 → 검붉음(노랑 단계 없음). 알파는 0.65 까지 1 — 끝까지 닿아 보이게
                        // v13: 끝 색 (0.5, 0.07, 0.02) → (0.3, 0.05, 0.02). v14: 0.8 에 검붉음, 끝은 그을음(0.08, 0.06, 0.05) — 끝 덩어리가 검은 연기로 변하는 게 보이게(사용자 스크린샷의 파란 구간).
                        // 셰이더 발광(커스텀 y: 3 → 0)이 끝에서 0 이라 정점색이 그대로 — 검게 나온다. 알파는 0.85 까지 0.7 로 붙잡아 검어진 뒤에도 잠깐 보인다
                        // v15: 그을음(0.08, 0.06, 0.05)은 "불이 검게 사라진다"로 읽혀 뺀다 — 끝은 검붉게(0.15, 0.05, 0.03), 검은 연기는 그 위에 따로 핀다(ProtoFlamePack.TickTipSmoke)
                        new[] { (new Color(1f, 0.82f, 0.5f), 0f), (new Color(1f, 0.5f, 0.14f), 0.25f), (new Color(0.95f, 0.24f, 0.06f), 0.6f), (new Color(0.5f, 0.1f, 0.04f), 0.8f), (new Color(0.15f, 0.05f, 0.03f), 1f) },
                        new[] { (1f, 0f), (1f, 0.65f), (0.7f, 0.85f), (0f, 1f) }));
                    main.startSize = new ParticleSystem.MinMaxCurve(3.2f, 4.4f);           // v11: 4.2 → 3.8 — 크기 폭이 크면 큰 덩어리 몇 개가 눈에 띈다. v16: 3.2~4.4 — 혀 시트는 칸의 가운데 띠만 차지해 같은 굵기엔 더 커야 한다
                    main.startSpeed = new ParticleSystem.MinMaxCurve(22f, 28f);           // v3: 16~28 → 22~28 — 속도 폭이 넓으면 부채꼴 가장자리가 너덜거린다
                    // v11: 월드 공간 — 총구를 돌리거나 걸으면 이미 나간 불은 제자리에 남고 새 불만 새 방향으로 나간다(호스처럼 휘며 "계속 나오는" 불).
                    // 로컬이면 줄기 전체가 총구를 따라 통째로 돌아 덩어리 하나로 보였다(사용자 11-05 "움직일 때 덩어리감"). 이동 속도의 35% 를 물려받아 걷는 동안 줄기가 너무 뒤처지지 않게,
                    // 걸은 거리 1 유닛마다 6 개를 더 내어 빨리 걸어도 줄기가 안 끊기게. 방출원 속도는 Transform 으로(강체가 없다 — 팩 기본 Rigidbody 모드면 0 이라 둘 다 안 먹는다).
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
                    var inh = ps.inheritVelocity;
                    inh.enabled = true; inh.mode = ParticleSystemInheritVelocityMode.Initial;
                    inh.curve = new ParticleSystem.MinMaxCurve(0.5f);                       // v16: 0.35 → 0.5 — 걸으며 쏠 때 줄기가 덜 뒤처져 "불꽃 줄기"로 읽힌다
                    em.rateOverDistance = 6f;
                    // v16: 불꽃 혀 시트 3×3 을 수명 동안 한 바퀴 — 앞 55% 는 첫 줄(혀 3종)을 천천히 오가며 일렁이고, 그 뒤 갈라져(둘째 줄) 조각(셋째 줄)이 되며 녹는다(커스텀 데이터 디졸브와 같이)
                    if (fireTex != null)
                    {
                        if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Base"))
                        {
                            r.sharedMaterial.SetTexture("_Base", fireTex);
                            EditorUtility.SetDirty(r.sharedMaterial);
                        }
                        SetTongueSheet(ps, Curve((0f, 0f), (0.55f, 0.32f), (0.8f, 0.64f), (1f, 0.98f)), 0f, 0f);
                        if (r != null) r.flip = new Vector3(TongueFlipX, 0f, 0f);
                    }
                    var shp = ps.shape;
                    shp.radius = 0.25f;                                                    // v3: 총구 한 점에서 퍼진다(팩 0.1 그대로여도 되지만 심이 너무 가늘지 않게)
                    shp.randomDirectionAmount = 0f;
                    shp.position = new Vector3(0f, 0f, 0.4f);                              // v5: 발사 원점을 총구 조금 앞(팩 단위 — 루트 ×0.23 이면 9cm)에 — 첫 조각의 뒤쪽 절반이 총구 뒤로 안 걸치게
                    ps.transform.localPosition = Vector3.zero;
                    // 끝에서 부푼다 — 작게 시작해 중간에 제 크기, 끝에서 1.35배
                    sz.enabled = true;
                    sz.size = new ParticleSystem.MinMaxCurve(1f, Curve((0f, 0.12f), (0.4f, 0.85f), (1f, 1.5f)));    // v5: 총구에선 가늘게(0.12), 끝에서 부풂. v10: 끝 1.35 → 1.5(불덩이처럼)
                    em.rateOverTime = 55f;                                  // 20 → 40: 굵고 끊김 없는 줄기(입자가 서로 겹쳐야 "불 하나"). v11: 55 — 조준을 돌릴 때 입자 사이가 비지 않게
                    // 끌림 — 끝으로 갈수록 느려져 한곳에 모이며 부푼다(영상의 불기둥 끝). 거리 = v/d·(1−e^(−d·t)) 라 1.5초에 원래의 약 0.5배 — ProtoFlamePack.Travel 이 같은 식으로 잰다
                    var lim = ps.limitVelocityOverLifetime;
                    lim.enabled = true; lim.separateAxes = false;
                    lim.limit = new ParticleSystem.MinMaxCurve(1000f);
                    lim.dampen = 0f;
                    lim.drag = new ParticleSystem.MinMaxCurve(0.9f);
                    lim.multiplyDragByParticleSize = false; lim.multiplyDragByParticleVelocity = false;
                    // 끝에서 위로 말린다 — 수명 절반까지는 곧게, 그 뒤 위로(로컬, 팩 단위 — 루트 스케일을 따라간다)
                    // x · y · z 는 같은 모드여야 한다(아니면 "Particle Velocity curves must all be in the same mode" — v3 에서 불이 안 나오던 원인) — 셋 다 곡선
                    var vel = ps.velocityOverLifetime;
                    vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;   // v11: 월드(위는 늘 위 — 시뮬레이션도 월드라 같이 맞춘다)
                    vel.x = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 0f));
                    vel.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 0f));
                    vel.y = new ParticleSystem.MinMaxCurve(6f, Curve((0f, 0f), (0.45f, 0f), (1f, 1f)));
                    // 일렁임 — 약한 노이즈로 가장자리가 핥듯 흔들린다
                    var nz = ps.noise;
                    nz.enabled = true; nz.frequency = 0.35f; nz.scrollSpeed = new ParticleSystem.MinMaxCurve(0.6f);
                    nz.strength = new ParticleSystem.MinMaxCurve(1.3f, Curve((0f, 0f), (0.4f, 0.6f), (1f, 1f)));   // v5: 갓 태어난 입자는 안 흔들리고(총구에서 곧게) 끝으로 갈수록 일렁인다. v10: 1.0 → 1.3
                    nz.octaveCount = 2; nz.damping = true; nz.quality = ParticleSystemNoiseQuality.Medium;
                    if (r != null) { r.lengthScale = 1.0f; r.velocityScale = 0.04f; }   // v5: 1.4/0.1 은 조각이 1m 로 늘어나 총구 뒤까지 걸쳤다
                }
                else if (ps.name == "Head")
                {
                    // 총구 심 — v10: 참고 영상처럼 총구 바로 앞의 짧은 심이 곧 주황으로 바뀐다(불이 "여기서 생긴다"는 자리). v3: 팩은 루트 뒤 1.91 유닛에서 뒤로 흘렀다 → 총구 앞으로
                    // v11: 팩 원본은 이 오브젝트를 꺼 둔다(m_IsActive 0) — 복사본도 꺼진 채라 v10 손질이 화면에 안 나왔다 → 켠다. 색은 영상(effect_01)의 노란-흰 심 → 주황.
                    // 로컬 공간 그대로(총구에 붙어 있는 짧은 불) — 멈추면 ProtoFlamePack 이 muzzleLinger 동안 방출을 줄이며 끈다(영상: 줄기가 떨어져 나간 뒤 총구에 작은 불이 잠깐 남는다).
                    ps.gameObject.SetActive(true);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.93f, 0.7f, 0.9f));
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);          // v5: 1.6~2.6 은 총구 뒤까지 덮었다
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);     // 짧게 — 심은 총구 앞 한 뼘
                    col.enabled = true;
                    col.color = new ParticleSystem.MinMaxGradient(Grad(
                        new[] { (new Color(1f, 0.97f, 0.85f), 0f), (new Color(1f, 0.8f, 0.45f), 0.5f), (new Color(1f, 0.5f, 0.18f), 1f) },
                        new[] { (1f, 0f), (0.8f, 0.6f), (0f, 1f) }));
                    em.rateOverTime = 24f;
                    ps.transform.localPosition = new Vector3(0f, 0f, 0.5f);
                    if (fireTex != null)
                    {
                        SetTongueSheet(ps, Curve((0f, 0f), (1f, 0.32f)), 0f, 0f);   // v16: 심은 첫 줄(혀 3종)만 오간다
                        if (r != null) r.flip = new Vector3(TongueFlipX, 0f, 0f);
                    }
                }
                else if (ps.name == "Smoke")
                {
                    // 검은 연기 — v10: 불줄기 중간~끝에서 태어나 떠오르며 부풀고 천천히 흩어진다(참고 영상의 불 위로 피어오르는 검은 연기).
                    // 월드 공간 — 총을 돌리거나 내려도 연기는 그 자리에 남아 떠오른다. 셰이더(PowDissolve)는 정점색 × 연기 시트라 어둡게 칠하면 검은 연기, 커스텀 데이터가 수명 끝에서 녹인다.
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.simulationSpeed = 1f;                                             // v12: 팩은 0.5 — 수명 1~1.7 이 실제론 2~3.4초였다(사용자 "너무 오래 살아 있다")
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.06f, 0.05f, 0.045f, 1f), new Color(0.12f, 0.1f, 0.09f, 1f));   // v11: 검붉은 연기 → v13: 검은 연기(사용자 "검은 연기") — 온기는 수명 색 첫 키의 붉은기만
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);     // v12: 1~1.7 → 0.7~1.1(속도 1 기준 — 영상의 연기는 1초 안에 흩어진다). v14: 0.8~1.2 — 날아가는 거리만큼
                    main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
                    main.startSize = new ParticleSystem.MinMaxCurve(3f, 5f);
                    main.startRotation = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);   // v12: 플립북에 위아래(음영)가 있다 — 살짝만 기울인다
                    col.enabled = true;
                    col.color = new ParticleSystem.MinMaxGradient(Grad(   // 태어날 땐 불빛을 받아 살짝 붉고, 곧 검정 — 알파는 서서히 들어와(툭 생기지 않게) 끝에서 빠진다(흩어지는 건 플립북이 한다)
                        new[] { (new Color(1f, 0.7f, 0.55f), 0f), (new Color(0.85f, 0.82f, 0.8f), 0.3f), (new Color(0.75f, 0.75f, 0.75f), 1f) },
                        new[] { (0f, 0f), (0.85f, 0.12f), (0.7f, 0.5f), (0f, 1f) }));
                    sz.enabled = true;
                    sz.size = new ParticleSystem.MinMaxCurve(1f, Curve((0f, 0.6f), (0.5f, 1f), (1f, 1.4f)));   // v12: 1.6 → 1.3 — 플립북이 흩어지며 넓어지므로 덜 키운다. v14: 1.4
                    // v12: 연기 플립북 6×6 을 수명 동안 한 바퀴(뭉게 → 흩어짐). 불 시트용 커스텀 데이터는 끈다(연기 셰이더는 안 읽는다)
                    var uvS = ps.textureSheetAnimation;
                    uvS.enabled = true; uvS.mode = ParticleSystemAnimationMode.Grid; uvS.numTilesX = 6; uvS.numTilesY = 6;
                    uvS.animation = ParticleSystemAnimationType.WholeSheet; uvS.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
                    uvS.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                    uvS.startFrame = new ParticleSystem.MinMaxCurve(0f); uvS.cycleCount = 1;
                    var cdS = ps.customData; cdS.enabled = false;
                    var shs = ps.shape;
                    shs.enabled = true; shs.shapeType = ParticleSystemShapeType.ConeVolume;
                    shs.angle = 22f; shs.radius = 0.5f; shs.length = 9f;
                    shs.position = new Vector3(0f, 0f, 2.5f);                              // 총구 바로 앞은 비우고 불줄기 중간부터
                    shs.randomDirectionAmount = 0.2f;
                    var limS = ps.limitVelocityOverLifetime;                               // 앞으로 밀리다 멈춰 불 위에 모인다
                    limS.enabled = true; limS.separateAxes = false; limS.limit = new ParticleSystem.MinMaxCurve(1000f); limS.dampen = 0f;
                    limS.drag = new ParticleSystem.MinMaxCurve(1.2f); limS.multiplyDragByParticleSize = false; limS.multiplyDragByParticleVelocity = false;   // v14: 1.8 → 1.2 — 불줄기 방향으로 더 날아간다
                    var velS = ps.velocityOverLifetime;                                    // 떠오름 — x · y · z 같은 모드(곡선)
                    velS.enabled = true; velS.space = ParticleSystemSimulationSpace.World;
                    velS.x = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 0f));
                    velS.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 0f));
                    velS.y = new ParticleSystem.MinMaxCurve(2.5f, Curve((0f, 0.2f), (0.4f, 0.8f), (1f, 1f)));   // v14: 5 → 2.5 — 위로 솟기보다 날아가며 흩어진다(사용자 "날아가면서 사라지는")
                    var nzS = ps.noise;
                    nzS.enabled = true; nzS.frequency = 0.3f; nzS.scrollSpeed = new ParticleSystem.MinMaxCurve(0.4f);
                    nzS.strength = new ParticleSystem.MinMaxCurve(1.2f, Curve((0f, 0.2f), (1f, 1f)));   // v14: 1.5 → 1.2
                    nzS.octaveCount = 1; nzS.damping = true; nzS.quality = ParticleSystemNoiseQuality.Low;
                    em.rateOverTime = 0f;                                                  // v13: 0 — 연기는 모양에서 안 낸다. 분사 중엔 ProtoFlamePack.TickTipSmoke(불줄기 끝), 멈출 땐 SpawnStopDebris(몸통)가 Emit 으로
                    ps.transform.localPosition = Vector3.zero;
                    if (r != null)
                    {
                        r.renderMode = ParticleSystemRenderMode.Billboard;                 // 팩은 늘림 — 연기는 둥글게
                        r.sortingFudge = -1f;                                              // 불줄기(0) 조금 위 — 불 위로 검은 연기가 섞여 보이게(잔불 −5 보다는 아래)
                        if (smokeMat != null)
                        {
                            // v12: 연기 전용 머티리얼(팩 Bullet/Smoke 복사본 — URP Particles/Unlit 알파 블렌드 + 연기 플립북). 정점 스트림은 기본으로(불 시트용 커스텀 스트림은 안 쓴다)
                            r.sharedMaterial = smokeMat;
                            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                                { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
                        }
                    }
                }
            }
            AddEmbers(root);
            AddFragments(root);
            AddSparks(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[FX 팩] {Path.GetFileName(prefabPath)} 손질(v{FlameVersion}) — 노란-흰 총구 심 · 주황-빨강 불줄기(월드 공간 · 끝에서 부풀어 위로) · 검은 연기 플립북({(smokeMat != null ? smokeMat.name : "불 머티리얼 그대로")}, 불줄기 끝에서 ProtoFlamePack 이 냄) · 떠오르는 불똥 · 불꽃 파편(Fragments) · 멈출 때 불똥(Sparks). 부채꼴 · 생기고 사라지는 흐름은 ProtoFlamePack");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// 불꽃 파편(v11, 사용자 참고 영상 effect_01 — 멈출 때 식어 가는 몸통에서 작은 불꽃 조각이 떨어져 나와 날린다; 분사 중에도 줄기 끝에서 조금씩 떨어져 나간다).
    /// 루트 제트의 직렬화 값을 통째로 베껴(머티리얼 · 정점 스트림 · 커스텀 데이터 — PowDissolve 셰이더가 그 스트림을 요구한다) 작고 짧고 떨어지는 조각으로 바꾼다.
    /// 분사 중엔 줄기 앞쪽 절반에서 6/s(ProtoFlamePack 이 부채꼴에 맞춰 늘린다), 멈출 땐 ProtoFlamePack.SpawnStopDebris 가 남은 불 입자 자리에서 Emit 으로 한 번에.
    /// 값은 팩 단위(루트 스케일을 따라간다). 월드 공간 — 떨어져 나간 조각은 총구를 돌려도 제자리에.
    static void AddFragments(GameObject root)
    {
        if (root.transform.Find("Fragments") != null) return;
        var src = root.GetComponent<ParticleSystem>();
        var go = new GameObject("Fragments");
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();
        if (src != null)
        {
            CopyComponentValues(src, ps);
            var sr = src.GetComponent<ParticleSystemRenderer>();
            if (sr != null) CopyComponentValues(sr, r);
        }
        var main = ps.main;
        main.duration = 5f; main.loop = true; main.playOnAwake = true; main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.stopAction = ParticleSystemStopAction.None;
        main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);                      // 불줄기 입자(2.6~3.8)의 1/4 — 조각
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.72f, 0.4f, 0.9f));
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.25f);                      // 포물선으로 떨어진다
        var em = ps.emission;
        em.enabled = true; em.rateOverTime = 6f; em.rateOverDistance = 0f;
        em.SetBursts(new ParticleSystem.Burst[0]);
        var sh = ps.shape;
        sh.enabled = true; sh.shapeType = ParticleSystemShapeType.ConeVolume;
        sh.angle = 30f; sh.radius = 0.3f; sh.length = 10f;                                 // 줄기 중간~끝(4~14)에서 태어난다 — 총구 쪽은 아직 가는 심
        sh.position = new Vector3(0f, 0f, 4f); sh.rotation = Vector3.zero; sh.scale = Vector3.one;
        sh.randomDirectionAmount = 0.6f; sh.sphericalDirectionAmount = 0.3f;
        var col = ps.colorOverLifetime; col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(1f, 0.95f, 0.7f), 0f), (new Color(1f, 0.6f, 0.2f), 0.35f), (new Color(0.9f, 0.25f, 0.06f), 0.7f), (new Color(0.35f, 0.05f, 0.02f), 1f) },
            new[] { (1f, 0f), (1f, 0.55f), (0f, 1f) }));
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, Curve((0f, 1f), (0.5f, 0.85f), (1f, 0.15f)));   // 타들어 작아진다
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true; lim.separateAxes = false; lim.limit = new ParticleSystem.MinMaxCurve(1000f); lim.dampen = 0f;
        lim.drag = new ParticleSystem.MinMaxCurve(1.5f); lim.multiplyDragByParticleSize = false; lim.multiplyDragByParticleVelocity = false;
        var vel = ps.velocityOverLifetime; vel.enabled = false;                             // 루트의 '끝에서 위로 말림'은 조각엔 안 쓴다
        var inh = ps.inheritVelocity; inh.enabled = false;
        var nz = ps.noise;
        nz.enabled = true; nz.frequency = 0.8f; nz.scrollSpeed = new ParticleSystem.MinMaxCurve(0.8f);
        nz.strength = new ParticleSystem.MinMaxCurve(0.9f, Curve((0f, 0.3f), (1f, 1f)));
        nz.octaveCount = 1; nz.damping = true; nz.quality = ParticleSystemNoiseQuality.Low;
        var uv = ps.textureSheetAnimation;
        if (uv.enabled) uv.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);          // 시트 칸을 무작위로 — 같은 모양 조각이 줄지어 보이지 않게
        // v16: 불꽃 혀 시트(3×3)면 둘째 줄(갈라진 혀, 칸 3~5)에서 시작해 셋째 줄(조각, 6~8)로 — 조각은 처음부터 갈라져 있다
        if (uv.enabled && uv.numTilesX == 3 && uv.numTilesY == 3) SetTongueSheet(ps, Curve((0f, 0f), (1f, 0.33f)), 0.34f, 0.6f);
        if (r != null)
        {
            r.renderMode = ParticleSystemRenderMode.Billboard;                             // 늘리지 않는다 — 불꽃 조각
            r.sortingFudge = -2f;                                                          // 불줄기(0) · 연기(−1) 앞, 불똥(−5) 뒤
        }
    }

    /// 불꽃 혀 시트(3×3, v16) 재생 — 전체 시트를 수명 동안 frameOverTime 곡선대로(0~1 이 칸 0~8), 시작 칸은 startMin~startMax(0~1, 칸 수로 나눈 값) 사이 무작위.
    static void SetTongueSheet(ParticleSystem ps, AnimationCurve frameOverTime, float startMin, float startMax)
    {
        var uv = ps.textureSheetAnimation;
        uv.enabled = true; uv.mode = ParticleSystemAnimationMode.Grid; uv.numTilesX = 3; uv.numTilesY = 3;
        uv.animation = ParticleSystemAnimationType.WholeSheet; uv.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
        uv.frameOverTime = new ParticleSystem.MinMaxCurve(1f, frameOverTime);
        uv.startFrame = startMax > startMin ? new ParticleSystem.MinMaxCurve(startMin, startMax) : new ParticleSystem.MinMaxCurve(startMin);
        uv.cycleCount = 1;
    }

    /// 멈출 때 흩어지는 불똥(v11, 영상 — 몸통이 연기로 바뀌는 동안 작은 밝은 점들이 사방으로 튀어 천천히 떨어진다). 분사 중 떠오르는 잔불(Embers)과 달리 떨어지고 오래 산다.
    /// 스스로는 안 낸다(방출 0 · 모양 없음) — ProtoFlamePack.SpawnStopDebris 가 남은 불 입자 자리 · 속도로 Emit 한다. 가산 점(M_Fx_Glow), 월드 공간, 팩 단위.
    static void AddSparks(GameObject root)
    {
        if (root.transform.Find("Sparks") != null) return;
        var go = new GameObject("Sparks");
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 5f; main.loop = true; main.playOnAwake = true; main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.stopAction = ParticleSystemStopAction.None;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(2.2f, 1.3f, 0.45f, 1f));   // HDR 주황-노랑(가산 — 블룸을 탄다), 잔불과 같은 색
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.35f);                       // 튀어 올랐다 천천히 떨어진다
        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f; em.rateOverDistance = 0f;
        var sh = ps.shape; sh.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(1f, 0.95f, 0.7f), 0f), (new Color(1f, 0.5f, 0.12f), 0.5f), (new Color(0.6f, 0.1f, 0.02f), 1f) },
            new[] { (1f, 0f), (1f, 0.5f), (0f, 1f) }));
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, Curve((0f, 1f), (1f, 0.3f)));
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true; lim.separateAxes = false; lim.limit = new ParticleSystem.MinMaxCurve(1000f); lim.dampen = 0f;
        lim.drag = new ParticleSystem.MinMaxCurve(2f); lim.multiplyDragByParticleSize = false; lim.multiplyDragByParticleVelocity = false;
        var nz = ps.noise;
        nz.enabled = true; nz.frequency = 0.7f; nz.scrollSpeed = new ParticleSystem.MinMaxCurve(0.6f);
        nz.strength = new ParticleSystem.MinMaxCurve(0.6f);
        nz.octaveCount = 1; nz.damping = true; nz.quality = ParticleSystemNoiseQuality.Low;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = FxTemplatePrefabs.GlowDot();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 1.5f; r.velocityScale = 0.04f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.sortingFudge = -5f;   // 불줄기 · 연기 · 파편보다 앞에
    }

    /// 같은 종류 컴포넌트의 직렬화 값을 통째로 옮긴다(인스펙터의 'Paste Component Values' 와 같다). 소속 오브젝트 · 프리팹 연결 같은 신원 필드는 건너뛴다 —
    /// EditorUtility.CopySerialized 는 그것까지 옮길 수 있어 안 쓴다. 파티클 모듈은 한 덩어리(구조체)라 모듈 단위로 복사된다.
    static void CopyComponentValues(Component src, Component dst)
    {
        var from = new SerializedObject(src);
        var to = new SerializedObject(dst);
        var it = from.GetIterator();
        bool enter = true;
        while (it.Next(enter))
        {
            enter = false;
            switch (it.name)
            {
                case "m_ObjectHideFlags": case "m_CorrespondingSourceObject": case "m_PrefabInstance": case "m_PrefabAsset": case "m_GameObject": case "m_Enabled":
                    continue;
            }
            to.CopyFromSerializedProperty(it);
        }
        to.ApplyModifiedPropertiesWithoutUndo();
    }

    /// 대화창 없이 복사본을 지우고 현재 판(<see cref="FlameVersion"/>)으로 다시 만든다 — 배치 모드
    /// (<c>Unity -batchmode -executeMethod FxPackImport.RebuildFlamethrower</c>)나 다른 코드에서. 메뉴 '화염방사기에 MasterStylizedFX 입히기' 와 같은 일.
    public static void RebuildFlamethrower()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FlameSrc) == null) { Debug.LogError($"[FX 팩] 원본이 없다: {FlameSrc}"); return; }
        ProtoFx.Clear();
        if (AssetDatabase.IsValidFolder(FlameDstDir)) AssetDatabase.DeleteAsset(FlameDstDir);
        CleanFlameV1Leftovers();
        ClearFlameHandEdited();   // 명시적으로 부른 것 — 손편집을 버리고 규칙대로
        if (ApplyFlamethrower()) WriteFlameMarker();
        else Debug.LogWarning("[FX 팩] 화염방사기 다시 만들기 — 카탈로그나 세트가 아직 없어 표식을 안 썼다(다음 로드에 다시)");
    }

    /// 잔불(v7) — 불줄기 전체에서 주변으로 튀는 불꽃 파편. 가산 점(M_Fx_Glow), 콘 부피(길이 18 팩 단위 ≈ 사거리)에서 바깥 + 조금 위로, 중력에 떨어지며 작아지고 검붉게 식는다.
    /// 값은 팩 단위(루트 스케일을 따라간다). ProtoFlamePack 이 방출 · 사그라들기를 다른 시스템과 같이 다룬다(자식이라 _ps 에 든다).
    static void AddEmbers(GameObject root)
    {
        if (root.transform.Find("Embers") != null) return;
        var go = new GameObject("Embers");
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 5f; main.loop = true; main.playOnAwake = true; main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);     // v9: 0.35~0.8 → 0.25~0.55. v10: 0.3~0.8 — 떠오르며 조금 더 머문다(끝은 여전히 보이는 불끝 안 ≈ 10.7)
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);            // v9: 4~12 → 3~7
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.38f);      // v10: 0.22~0.5 → 작은 불똥
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(2.2f, 1.3f, 0.45f, 1f));   // HDR 주황-노랑(가산 — 블룸을 탄다)
        main.gravityModifier = -0.15f;                                      // v10: 0.5(떨어짐) → −0.15(참고 영상처럼 위로 떠오른다)
        main.stopAction = ParticleSystemStopAction.None;
        var em = ps.emission; em.enabled = true; em.rateOverTime = 30f;
        var sh = ps.shape;
        sh.enabled = true; sh.shapeType = ParticleSystemShapeType.ConeVolume;
        sh.angle = 35f; sh.radius = 0.25f; sh.length = 8f;                  // 꼭짓점 총구. v8: 18° → 35°(옆으로). v9: 길이 18 → 8 — 불줄기 앞쪽 절반에서만 태어나 불꽃 안에서 꺼진다
        sh.randomDirectionAmount = 0.7f; sh.sphericalDirectionAmount = 0.3f; // v8: 0.35/0.15 → 0.7/0.3 — 앞으로만 말고 사방으로
        sh.position = new Vector3(0f, 0f, 0.6f);
        var col = ps.colorOverLifetime; col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(Grad(
            new[] { (new Color(1f, 0.9f, 0.6f), 0f), (new Color(1f, 0.45f, 0.1f), 0.55f), (new Color(0.5f, 0.08f, 0.02f), 1f) },
            new[] { (1f, 0f), (1f, 0.6f), (0f, 1f) }));
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, Curve((0f, 1f), (1f, 0.2f)));
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true; lim.separateAxes = false; lim.limit = new ParticleSystem.MinMaxCurve(1000f); lim.dampen = 0f;
        lim.drag = new ParticleSystem.MinMaxCurve(3f); lim.multiplyDragByParticleSize = false; lim.multiplyDragByParticleVelocity = false;   // v8: 1.5 → 2.5, v9: 3
        var nz = ps.noise;
        nz.enabled = true; nz.frequency = 0.6f; nz.scrollSpeed = new ParticleSystem.MinMaxCurve(0.8f);
        nz.strength = new ParticleSystem.MinMaxCurve(0.8f, Curve((0f, 0f), (0.3f, 0.7f), (1f, 1f)));
        nz.octaveCount = 1; nz.damping = true; nz.quality = ParticleSystemNoiseQuality.Low;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = FxTemplatePrefabs.GlowDot();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 1.2f; r.velocityScale = 0.03f;   // v10: 점에 가깝게
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.sortingFudge = -5f;   // 불줄기보다 앞에
    }

    static Gradient Grad((Color c, float t)[] colors, (float a, float t)[] alphas)
    {
        var g = new Gradient();
        g.SetKeys(colors.Select(k => new GradientColorKey(k.c, k.t)).ToArray(), alphas.Select(k => new GradientAlphaKey(k.a, k.t)).ToArray());
        return g;
    }

    static AnimationCurve Curve(params (float t, float v)[] keys)
    {
        var c = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    /// 카탈로그 flamethrower_jet 항목이 없거나 프리팹이 비어 있나(카탈로그 자체가 없으면 false — 아직 만들어지기 전).
    static bool FlameLinkBroken()
    {
        var cat = FxCatalogEdit.Catalog;
        if (cat == null) return false;
        var e = cat.Find(FxIds.FlamethrowerJet);
        return e == null || e.prefab == null;
    }

    /// v1 은 복사 헬퍼가 소총 폴더(DstDir)에 머티리얼 · 셰이더를 떨어뜨렸다 — 소총이 안 쓰는 것만 지운다.
    static void CleanFlameV1Leftovers()
    {
        string mat = DstDir + "/M_MSFX_CommonFire.mat", sh = DstDir + "/SH_MSFX_PowDissolve.shader";
        if (AssetDatabase.LoadAssetAtPath<Material>(mat) != null) { AssetDatabase.DeleteAsset(mat); Debug.Log($"[FX 팩] v1 잔재 삭제: {mat}"); }
        if (AssetDatabase.LoadAssetAtPath<Shader>(sh) != null)
        {
            bool used = AssetDatabase.FindAssets("t:Material", new[] { DstDir }).Select(AssetDatabase.GUIDToAssetPath)
                                     .Any(mp => AssetDatabase.GetDependencies(mp, false).Contains(sh));
            if (!used) { AssetDatabase.DeleteAsset(sh); Debug.Log($"[FX 팩] v1 잔재 삭제: {sh}"); }
        }
    }

    static void WriteFlameMarker()
    {
        try { File.WriteAllText(FlameMarker, $"v{FlameVersion} {DateTime.Now:s}"); } catch (Exception) { }
    }

    static int MarkerVersion(string marker)
    {
        if (!File.Exists(marker)) return 0;
        var m = Regex.Match(File.ReadAllText(marker), @"v(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 1;
    }

    /// 복사본 폴더에 표식보다 나중에 저장된 파일이 있나 — 사용자가 손댄 흔적.
    static bool EditedSince(string marker, string dir)
    {
        if (!File.Exists(marker) || !Directory.Exists(dir)) return false;
        var limit = File.GetLastWriteTime(marker).AddSeconds(30);
        return Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")).Any(f => File.GetLastWriteTime(f) > limit);
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/화염방사기에 MasterStylizedFX (RedFlameThrower) 입히기")]
    public static void ApplyFlamethrowerMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FlameSrc) == null) { Debug.LogError($"[FX 팩] 원본이 없다: {FlameSrc}"); return; }
        bool has = AssetDatabase.IsValidFolder(FlameDstDir);
        if (!EditorUtility.DisplayDialog("MasterStylizedFX 화염방사기 입히기",
                (has ? $"복사본 폴더 {FlameDstDir} 가 이미 있다 — 지우고 새로 만든다(손으로 고친 게 있으면 사라진다).\n\n" : "") +
                $"RedFlameThrower 프리팹을 {FlameDstDir} 로 복사하고(머티리얼 · 텍스처 · 셰이더까지, 이름 바꿔)\n" +
                "카탈로그 flamethrower_jet 과 WFX_Flamethrower 의 화염 줄기(jet) 슬롯이 복사본을 가리키게 한다. 원본은 건드리지 않는다. 계속할까?",
                has ? "지우고 다시 입힌다" : "입힌다", "취소")) return;
        if (has) { ProtoFx.Clear(); AssetDatabase.DeleteAsset(FlameDstDir); }
        ClearFlameHandEdited();   // 사람이 직접 눌렀다 — 손편집을 버리고 규칙대로
        ApplyFlamethrower();
        WriteFlameMarker();
    }

    static void ClearFlameHandEdited()
    {
        try { if (File.Exists(FlameHandEdited)) File.Delete(FlameHandEdited); } catch (Exception) { }
    }

    /// 복사(이미 있으면 그 복사본) → 카탈로그 flamethrower_jet(없으면 항목도 만든다) → WFX_Flamethrower.jet. 카탈로그나 세트가 아직 없으면 false(다음 로드에 다시).
    static bool ApplyFlamethrower()
    {
        EnsureFolder(FlameDstDir);
        var copies = new Dictionary<string, Object>();
        string jetPath = $"{FlameDstDir}/FX_Flamethrower_Jet_MSFX.prefab";
        var jet = AssetDatabase.LoadAssetAtPath<GameObject>(jetPath);
        if (jet == null)
        {
            jet = CopyEffect(FlameSrc, "FX_Flamethrower_Jet_MSFX", 1f, copies, out _, FlameDstDir);
            if (jet != null) StyleFlameJet(AssetDatabase.GetAssetPath(jet));
        }
        if (jet == null) return false;

        bool ok = true;
        var cat = FxCatalogEdit.Catalog;
        if (cat != null)
        {
            var e = cat.Find(FxIds.FlamethrowerJet);
            if (e == null)
            {
                e = new FxCatalog.Entry { id = FxIds.FlamethrowerJet, lifetime = 0f, poolSize = 1, maxAlive = 0, previewOffset = new Vector3(0f, 1.1f, 0.45f),
                                          note = "무기·화염방사기 — 화염 줄기(스팬, ProtoFlamePack 이 세트 jet 슬롯으로 세운다 — ProtoFx.Play 안 씀). 크기는 사거리에 자동, 세트 jet.scale 로 배율" };
                cat.entries.Add(e);
            }
            e.prefab = jet;
            e.lifetime = 0f;   // 스팬 — ProtoFlamePack 이 방출을 여닫는다
            const string tag = "MSFX RedFlameThrower 복사본";
            if (string.IsNullOrEmpty(e.note) || !e.note.Contains(tag)) e.note = (e.note ?? "") + $" | 모양 = {tag}({jet.name}, 원본 {FlameSrc})";
            EditorUtility.SetDirty(cat);
        }
        else { Debug.LogWarning("[FX 팩] 카탈로그가 아직 없다 — 다음 로드에 다시 잇는다([카탈로그·시트 만들기] 가 만든다)"); ok = false; }

        var set = AssetDatabase.LoadAssetAtPath<WeaponFxSet>(FlameFxSetPath);
        if (set != null)
        {
            if (set.jet == null) set.jet = new WeaponFxSet.Slot();
            if (string.IsNullOrEmpty(set.jet.fxId)) set.jet.fxId = FxIds.FlamethrowerJet;   // 비어 있을 때만 — 시트(flamethrower_sheet) 로 바꿔 둔 것을 되돌리지 않는다(11-05)
            if (set.jet.scale <= 0f) set.jet.scale = 1f;
            EditorUtility.SetDirty(set);
        }
        else { Debug.LogWarning($"[FX 팩] 화염방사기 연출 세트가 아직 없다: {FlameFxSetPath} — 다음 로드에 다시 잇는다"); ok = false; }

        AssetDatabase.SaveAssets();
        FxCatalog.Reload();
        ProtoFx.Clear();
        if (FxShowcase.Instance != null) FxShowcase.Instance.Reload();
        Debug.Log($"[FX 팩] RedFlameThrower 입힘 — 화염 줄기 {Name(jet)} → {FlameDstDir}, 복사한 에셋 {copies.Count}개. 카탈로그 {FxIds.FlamethrowerJet} · WFX_Flamethrower.jet. " +
                  "크기는 사거리에 자동(ProtoFlamePack), 배율은 세트 jet.scale, 넓이 · 눌림은 _FlamePack 의 coneFraction · flatten. 되돌리려면 jet 슬롯의 ID 를 비운다.");
        EditorGUIUtility.PingObject(jet);
        return ok;
    }

    /// 카탈로그 항목의 프리팹을 복사본으로. ID · 폴백 · 풀 설정은 그대로, 수명만 잰 값으로.
    static void Assign(FxCatalog cat, string id, GameObject prefab, float length)
    {
        if (prefab == null) return;
        var e = cat.Find(id);
        if (e == null) { Debug.LogWarning($"[FX 팩] 카탈로그에 {id} 가 없다 — [카탈로그·시트 만들기] 뒤에 다시"); return; }
        e.prefab = prefab;
        e.lifetime = length > 0f ? length + 0.1f : 0f;
        const string tag = "MSFX YellowMachineGun 복사본";
        if (string.IsNullOrEmpty(e.note) || !e.note.Contains(tag)) e.note = (e.note ?? "") + $" | 모양 = {tag}({prefab.name}, 원본 {PackPrefabDir})";
    }

    // ── 프리팹 복사 ──────────────────────────────────────────────────
    static GameObject CopyEffect(string srcPath, string newName, float rootScale, Dictionary<string, Object> copies, out float length, string dstDir = null)
    {
        length = 0f;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(srcPath) == null) { Debug.LogError($"[FX 팩] 원본이 없다: {srcPath}"); return null; }
        CurDst = dstDir ?? DstDir;
        string dstPath = AssetDatabase.GenerateUniqueAssetPath($"{CurDst}/{newName}.prefab");

        var root = PrefabUtility.LoadPrefabContents(srcPath);
        try
        {
            root.name = Path.GetFileNameWithoutExtension(dstPath);
            // 팩 데모 씬 자리(RedFlameThrower 는 x -10 · y 90°)가 루트에 남아 있다 — 복사본 루트는 원점 · 무회전(쓰는 쪽이 자리를 준다)
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            // ① 중첩 프리팹을 완전히 푼다 — 안 풀면 복사본이 팩의 Common 프리팹을 계속 가리킨다
            for (bool again = true; again;)
            {
                again = false;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.gameObject == root || !PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                    PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    again = true;
                    break;
                }
            }

            // ② 빠진 스크립트 · 팩 데모 스크립트 · 점광원 제거
            int missing = 0, lights = 0, demos = 0, noMat = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) missing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var script = MonoScript.FromMonoBehaviour(mb);
                string sp = script != null ? AssetDatabase.GetAssetPath(script) : "";
                if (sp.StartsWith(Pack) && mb.GetType().Name.Contains("Example")) { Object.DestroyImmediate(mb); demos++; }
                else if (sp.StartsWith(Pack)) Debug.Log($"[FX 팩] {newName}: 팩 스크립트 {mb.GetType().Name} 을 남겨 둔다(팩 Scripts 폴더에 의존)");
            }
            foreach (var l in root.GetComponentsInChildren<Light>(true)) { Object.DestroyImmediate(l); lights++; }

            // ③ 머티리얼 없는 파티클은 끈다 ④ 머티리얼 · 텍스처 · 메시 → 복사본
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                if (r is ParticleSystemRenderer psr0 && (mats.Length == 0 || mats.All(x => x == null)))
                {
                    psr0.enabled = false;
                    var ps0 = psr0.GetComponent<ParticleSystem>();
                    if (ps0 != null) { var em = ps0.emission; em.enabled = false; }
                    noMat++;
                    continue;
                }
                for (int i = 0; i < mats.Length; i++) mats[i] = CopyMaterial(mats[i], copies);
                r.sharedMaterials = mats;
                if (r is ParticleSystemRenderer psr)
                {
                    if (psr.trailMaterial != null) psr.trailMaterial = CopyMaterial(psr.trailMaterial, copies);
                    if (psr.renderMode == ParticleSystemRenderMode.Mesh && psr.mesh != null) psr.mesh = CopyMesh(psr.mesh, copies);
                }
            }

            // ⑤ 파티클 — Hierarchy 스케일 · 풀링에 맞게 · ⑥ 원샷 길이
            float len = 0f;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.stopAction = ParticleSystemStopAction.None;
                main.playOnAwake = true;
                if (ps.emission.enabled) len = Mathf.Max(len, OneShotLength(ps));
            }
            length = len;
            root.transform.localScale = Vector3.one * rootScale;

            var saved = PrefabUtility.SaveAsPrefabAsset(root, dstPath);
            Debug.Log($"[FX 팩] {srcPath} → {dstPath} (루트 ×{rootScale}, 원샷 {len:0.00}s, 빠진 스크립트 {missing} · 데모 스크립트 {demos} · 점광원 {lights} 제거 · 머티리얼 없는 파티클 {noMat} 끔)");
            return saved;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// 원샷이 끝나는 시각 — 마지막 버스트(또는 연속 방출이면 duration) + 입자 수명 + 시작 지연. 무한 수명(탄)은 0 으로 친다.
    static float OneShotLength(ParticleSystem ps)
    {
        var main = ps.main;
        var em = ps.emission;
        float life = Max(main.startLifetime);
        if (float.IsInfinity(life) || life > 60f) life = 0f;
        float last = 0f;
        if (main.loop || Max(em.rateOverTime) > 0f || Max(em.rateOverDistance) > 0f) last = main.duration;
        for (int i = 0; i < em.burstCount; i++) last = Mathf.Max(last, em.GetBurst(i).time);
        return Max(main.startDelay) + last + life;
    }

    static float Max(ParticleSystem.MinMaxCurve c)
    {
        switch (c.mode)
        {
            case ParticleSystemCurveMode.Constant:     return c.constant;
            case ParticleSystemCurveMode.TwoConstants: return c.constantMax;
            default:                                   return c.curveMultiplier;   // 곡선은 보통 0~1 — 배율이 곧 최댓값
        }
    }

    // ── 에셋 복사 ────────────────────────────────────────────────────
    static Material CopyMaterial(Material m, Dictionary<string, Object> copies)
    {
        if (m == null) return null;
        string src = AssetDatabase.GetAssetPath(m);
        if (string.IsNullOrEmpty(src) || !src.StartsWith(Pack)) return m;   // 팩 밖(유니티 기본 등)은 그대로
        if (copies.TryGetValue(src, out var done)) return (Material)done;

        string dst = AssetDatabase.GenerateUniqueAssetPath($"{CurDst}/M_MSFX_{m.name}.mat");
        if (!AssetDatabase.CopyAsset(src, dst)) { Debug.LogWarning($"[FX 팩] 머티리얼 복사 실패: {src}"); return m; }
        var c = AssetDatabase.LoadAssetAtPath<Material>(dst);
        copies[src] = c;
        foreach (var prop in c.GetTexturePropertyNames())
        {
            var tex = c.GetTexture(prop);
            if (tex == null) continue;
            var ct = CopyTexture(tex, copies);
            if (ct != tex) c.SetTexture(prop, ct);
        }
        ConvertToUrp(c);
        if (c.shader != null)
        {
            string sp = AssetDatabase.GetAssetPath(c.shader);
            if (!string.IsNullOrEmpty(sp) && sp.StartsWith(Pack)) c.shader = CopyShader(c.shader, copies);   // 속성값은 이름이 같으면 그대로 남는다
        }
        EditorUtility.SetDirty(c);
        return c;
    }

    static Texture CopyTexture(Texture tex, Dictionary<string, Object> copies)
    {
        string src = AssetDatabase.GetAssetPath(tex);
        if (string.IsNullOrEmpty(src) || !src.StartsWith(Pack)) return tex;
        if (copies.TryGetValue(src, out var done)) return (Texture)done;
        EnsureFolder(CurTexDst);
        string dst = AssetDatabase.GenerateUniqueAssetPath($"{CurTexDst}/T_MSFX_{Path.GetFileNameWithoutExtension(src)}{Path.GetExtension(src)}");
        if (!AssetDatabase.CopyAsset(src, dst)) { Debug.LogWarning($"[FX 팩] 텍스처 복사 실패: {src}"); return tex; }
        var c = AssetDatabase.LoadAssetAtPath<Texture>(dst);   // 임포트 설정(.meta)도 같이 복사된다
        copies[src] = c;
        return c;
    }

    /// 메시는 FBX 의 하위 에셋 — FBX 를 통째로 복사하고 같은 이름의 메시를 찾는다.
    static Mesh CopyMesh(Mesh mesh, Dictionary<string, Object> copies)
    {
        string src = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(src) || !src.StartsWith(Pack)) return mesh;
        string key = src + "::" + mesh.name;
        if (copies.TryGetValue(key, out var done)) return (Mesh)done;
        if (!copies.TryGetValue(src, out var model))
        {
            string dst = AssetDatabase.GenerateUniqueAssetPath($"{CurDst}/MSFX_{Path.GetFileNameWithoutExtension(src)}{Path.GetExtension(src)}");
            if (!AssetDatabase.CopyAsset(src, dst)) { Debug.LogWarning($"[FX 팩] 모델 복사 실패: {src}"); return mesh; }
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            model = AssetDatabase.LoadMainAssetAtPath(dst);
            copies[src] = model;
        }
        string modelPath = AssetDatabase.GetAssetPath(model);
        var found = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().FirstOrDefault(x => x.name == mesh.name)
                 ?? AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().FirstOrDefault();
        if (found == null) { Debug.LogWarning($"[FX 팩] 복사한 모델에서 메시 {mesh.name} 을 못 찾았다 — 원본 메시를 그대로 쓴다"); return mesh; }
        copies[key] = found;
        return found;
    }

    /// <summary>
    /// 팩 셰이더를 복사본 폴더로 — 파일은 SH_MSFX_*, 안의 이름은 "ZombieOverrun/MSFX/원래 이름"(같은 이름의 셰이더가 둘이면 Shader.Find 가 헷갈린다).
    /// 복사본이 팩 폴더에 기대지 않게 하려는 것. 팩 안 다른 파일을 상대 경로로 #include 하거나 복사본이 컴파일에 실패하면 팩 셰이더를 그대로 쓰고 알린다.
    /// </summary>
    static Shader CopyShader(Shader sh, Dictionary<string, Object> copies)
    {
        string src = AssetDatabase.GetAssetPath(sh);
        if (string.IsNullOrEmpty(src) || !src.StartsWith(Pack)) return sh;
        if (copies.TryGetValue(src, out var done)) return (Shader)done;

        string text = File.ReadAllText(src);
        const string head = "Shader \"";
        int q = text.IndexOf(head, StringComparison.Ordinal);
        int e = q >= 0 ? text.IndexOf('"', q + head.Length) : -1;
        if (q < 0 || e < 0) { Debug.LogWarning($"[FX 팩] 셰이더 이름을 못 찾았다 — 팩 것을 그대로 쓴다: {src}"); return sh; }
        string srcDir = Path.GetDirectoryName(src).Replace('\\', '/');
        for (int i = text.IndexOf("#include \"", StringComparison.Ordinal); i >= 0; i = text.IndexOf("#include \"", i + 1, StringComparison.Ordinal))
        {
            int a = i + 10, b = text.IndexOf('"', a);
            if (b < 0) break;
            string inc = text.Substring(a, b - a);
            if (!inc.StartsWith("Packages/") && !inc.StartsWith("Assets/") && File.Exists(Path.Combine(srcDir, inc)))
            { Debug.LogWarning($"[FX 팩] {Path.GetFileName(src)} 가 팩 안 {inc} 를 #include 한다 — 복사하지 않고 팩 셰이더를 그대로 쓴다"); return sh; }
        }

        string name = text.Substring(q + head.Length, e - (q + head.Length));
        string dst = AssetDatabase.GenerateUniqueAssetPath($"{CurDst}/SH_MSFX_{Path.GetFileNameWithoutExtension(src)}.shader");
        File.WriteAllText(dst, text.Substring(0, q) + head + "ZombieOverrun/MSFX/" + name + text.Substring(e));
        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
        var c = AssetDatabase.LoadAssetAtPath<Shader>(dst);
        if (c == null || ShaderUtil.ShaderHasError(c))
        {
            Debug.LogWarning($"[FX 팩] 복사한 셰이더가 안 돈다({dst}) — 지우고 팩 셰이더를 그대로 쓴다");
            AssetDatabase.DeleteAsset(dst);
            return sh;
        }
        copies[src] = c;
        Debug.Log($"[FX 팩] 셰이더 복사 {name} → ZombieOverrun/MSFX/{name} ({dst})");
        return c;
    }

    /// <summary>
    /// URP 셰이더가 아닌 머티리얼은 URP 에서 안 그려진다 — URP Particles/Unlit 로 바꾸고 블렌드 · 컬 · 색 모드 · 텍스처 · 색을 옮긴다.
    /// <list type="bullet">
    /// <item>빌트인 표준 파티클(Particles/Standard Unlit): 블렌드는 _Mode(0 불투명 · 1 컷아웃 · 2 페이드 · 3 투명 · 4 가산 · 5 감산 · 6 변조)에서. 불투명·컷아웃은 알파로 —
    ///       연기처럼 알파 텍스처를 쓰는 입자를 불투명으로 두는 건 팩의 실수라 본다. 색은 _Color 그대로(×2 없음).</item>
    /// <item>레거시 파티클(Legacy Shaders/Particles · Mobile/Particles): 이름으로 블렌드, tex × tint × 2 라 색에 2배.</item>
    /// <item>팩 서피스 셰이더(alpha:fade): 알파 블렌드, 첫 텍스처(_Main)만 — 마스크 같은 기능은 잃는다. 제대로 보려면 MSFURP 패키지를 가져온다.</item>
    /// </list>
    /// </summary>
    static void ConvertToUrp(Material c)
    {
        var sh = c.shader;
        if (sh == null) return;
        string n = sh.name;
        if (n.StartsWith("Universal Render Pipeline/") || n.StartsWith("Shader Graphs/") || n.StartsWith("ZombieOverrun/")) return;   // 이미 URP · 우리 셰이더
        bool legacy2x = n.StartsWith("Legacy Shaders/Particles") || n.StartsWith("Mobile/Particles");
        bool standard = n.StartsWith("Particles/Standard");
        string shaderPath = AssetDatabase.GetAssetPath(sh);
        bool packSurface = !string.IsNullOrEmpty(shaderPath) && shaderPath.StartsWith(Pack) && File.Exists(shaderPath) && File.ReadAllText(shaderPath).Contains("#pragma surface");
        if (!legacy2x && !standard && !packSurface && n != "Hidden/InternalErrorShader") return;   // 모르는 셰이더는 건드리지 않는다
        var urp = Shader.Find(FxShaderUnify.ShaderName);   // 2026-11-20 — 게임 파티클 셰이더 하나(URP Particles/Unlit 과 속성 이름 같음)
        if (urp == null) { Debug.LogWarning($"[FX 팩] 파티클 셰이더를 못 찾았다 — {c.name} 은 {n} 그대로"); return; }

        // 원래 값은 셰이더를 바꾸기 전에 읽는다
        Texture tex = c.HasProperty("_MainTex") ? c.GetTexture("_MainTex") : null;
        if (tex == null) foreach (var p in c.GetTexturePropertyNames()) { tex = c.GetTexture(p); if (tex != null) break; }   // 첫 텍스처를 바탕으로
        Color tint = c.HasProperty("_TintColor") ? c.GetColor("_TintColor") : c.HasProperty("_Color") ? c.GetColor("_Color") : Color.white;
        float mode = standard && c.HasProperty("_Mode") ? c.GetFloat("_Mode") : -1f;
        float cull = c.HasProperty("_Cull") ? c.GetFloat("_Cull") : 0f;
        float colorMode = c.HasProperty("_ColorMode") ? c.GetFloat("_ColorMode") : 0f;   // 0 곱 · 1 가산 · 2 감산 · 3 오버레이 · 4 색 · 5 차 — URP 와 같은 순서
        int queue = c.renderQueue;

        string blend;
        if (legacy2x) blend = n.Contains("Additive") ? "add" : n.Contains("Multiply") ? "mul" : "alpha";
        else if (standard) blend = mode == 4f || mode == 5f ? "add" : mode == 6f ? "mul" : mode == 3f ? "premul" : "alpha";
        else blend = "alpha";
        float gain = legacy2x ? 2f : 1f;

        c.shader = urp;
        if (tex != null) { c.SetTexture("_BaseMap", tex); c.mainTexture = tex; }
        c.SetColor("_BaseColor", new Color(tint.r * gain, tint.g * gain, tint.b * gain, Mathf.Min(1f, tint.a * gain)));
        c.SetFloat("_Surface", 1f); c.SetFloat("_ZWrite", 0f); c.SetFloat("_Cull", cull);
        c.SetFloat("_ColorMode", colorMode);
        c.SetFloat("_FlipbookBlending", 0f); c.SetFloat("_SoftParticlesEnabled", 0f); c.SetFloat("_CameraFadingEnabled", 0f);
        c.SetFloat("_EmissionEnabled", 0f); c.SetColor("_EmissionColor", Color.black);
        c.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        switch (blend)
        {
            case "add":    c.SetFloat("_Blend", 2f); c.SetFloat("_SrcBlend", 5f); c.SetFloat("_DstBlend", 1f);  c.SetFloat("_SrcBlendAlpha", 1f); c.SetFloat("_DstBlendAlpha", 1f);  break;
            case "mul":    c.SetFloat("_Blend", 3f); c.SetFloat("_SrcBlend", 2f); c.SetFloat("_DstBlend", 0f);  c.SetFloat("_SrcBlendAlpha", 0f); c.SetFloat("_DstBlendAlpha", 1f);  break;
            case "premul": c.SetFloat("_Blend", 1f); c.SetFloat("_SrcBlend", 1f); c.SetFloat("_DstBlend", 10f); c.SetFloat("_SrcBlendAlpha", 1f); c.SetFloat("_DstBlendAlpha", 10f); break;
            default:       c.SetFloat("_Blend", 0f); c.SetFloat("_SrcBlend", 5f); c.SetFloat("_DstBlend", 10f); c.SetFloat("_SrcBlendAlpha", 1f); c.SetFloat("_DstBlendAlpha", 10f); break;
        }
        c.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        foreach (var k in new[] { "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON", "_EMISSION", "_FLIPBOOKBLENDING_ON", "_SOFTPARTICLES_ON", "_FADING_ON",
                                  "_COLORCOLOR_ON", "_COLOROVERLAY_ON", "_COLORADDSUBDIFF_ON" }) c.DisableKeyword(k);
        if (blend == "premul") c.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        if (blend == "mul") c.EnableKeyword("_ALPHAMODULATE_ON");
        if (colorMode == 1f || colorMode == 2f || colorMode == 5f) c.EnableKeyword("_COLORADDSUBDIFF_ON");
        else if (colorMode == 3f) c.EnableKeyword("_COLOROVERLAY_ON");
        else if (colorMode == 4f) c.EnableKeyword("_COLORCOLOR_ON");
        c.renderQueue = queue > 0 ? queue : 3000;
        Debug.Log($"[FX 팩] {c.name}: {n}{(mode >= 0f ? $" (_Mode {mode:0})" : "")} → {FxShaderUnify.ShaderName} ({blend}, 컬 {cull:0}, 색 모드 {colorMode:0}, ×{gain:0.#})");
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
