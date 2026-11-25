using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 데이터 테이블(적 · 캐릭터 · 필사기) 검증과 선택 메뉴.
///
/// <para><b>왜 검증이 필요한가.</b> 테이블은 ID(참조)로 서로를 가리킨다. 칸 하나가 비면 코드는 조용히
/// 그 종류를 건너뛴다(스폰 디렉터는 빈 칸을 스폰하지 않고, 교체 패널은 프리팹 없는 줄을 누를 수 없다).
/// "왜 원거리 좀비가 안 나오지"를 10분 뒤에 찾는 대신, 플레이 들어갈 때 한 번 훑어 경고로 띄운다.</para>
/// </summary>
public static class DataTableSetup
{
    const string EnemyTablePath = "Assets/_Game/Resources/" + EnemyTable.ResourcePath + ".asset";
    const string CharTablePath  = "Assets/_Game/Resources/" + CharacterTable.ResourcePath + ".asset";

    [MenuItem("Tools/좀비 오버런/데이터 테이블/검증")]
    public static void ValidateMenu()
    {
        var problems = Validate();
        if (problems.Count == 0) Debug.Log("[데이터 테이블] 이상 없음 — 적 7칸, 캐릭터 명단, 필사기 참조가 모두 차 있다.");
        else foreach (var p in problems) Debug.LogWarning("[데이터 테이블] " + p);
    }

    [MenuItem("Tools/좀비 오버런/데이터 테이블/적 테이블 선택")]
    public static void SelectEnemyTable() => Select(EnemyTablePath);

    [MenuItem("Tools/좀비 오버런/데이터 테이블/캐릭터 테이블 선택")]
    public static void SelectCharacterTable() => Select(CharTablePath);

    static void Select(string path)
    {
        var o = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (o == null) { Debug.LogError("[데이터 테이블] 없음: " + path); return; }
        Selection.activeObject = o;
        EditorGUIUtility.PingObject(o);
    }

    /// 문제 목록. 비어 있으면 정상.
    public static List<string> Validate()
    {
        var list = new List<string>();

        var et = AssetDatabase.LoadAssetAtPath<EnemyTable>(EnemyTablePath);
        if (et == null) list.Add("적 테이블이 없다: " + EnemyTablePath + " — 적이 하나도 나오지 않는다.");
        else
        {
            CheckEnemy(list, "walker", et.walker, Proto.ZombieKind.Walker);
            CheckEnemy(list, "runner", et.runner, Proto.ZombieKind.Runner);
            CheckEnemy(list, "ranged", et.ranged, Proto.ZombieKind.Ranged);
            CheckEnemy(list, "dog", et.dog, Proto.ZombieKind.Dog);
            CheckEnemy(list, "elite", et.elite, Proto.ZombieKind.Walker);
            CheckEnemy(list, "midBoss", et.midBoss, Proto.ZombieKind.Boss);
            CheckEnemy(list, "finalBoss", et.finalBoss, Proto.ZombieKind.Boss);
            if (et.midBossAt >= et.finalBossAt) list.Add($"적 테이블: 중간 보스({et.midBossAt}s)가 최종 보스({et.finalBossAt}s)보다 늦다.");
            if (et.eliteSpawnTimes != null)
                foreach (var t in et.eliteSpawnTimes)
                    if (t >= et.finalBossAt) list.Add($"적 테이블: 엘리트 {t}s 는 최종 보스({et.finalBossAt}s) 뒤라 나오지 않는다.");
        }

        var ct = AssetDatabase.LoadAssetAtPath<CharacterTable>(CharTablePath);
        if (ct == null) list.Add("캐릭터 테이블이 없다: " + CharTablePath + " — 교체 패널이 비고 필사기가 잠긴다.");
        else if (ct.roster == null || ct.roster.Length == 0) list.Add("캐릭터 테이블 명단이 비어 있다.");
        else
        {
            for (int i = 0; i < ct.roster.Length; i++)
            {
                var c = ct.roster[i];
                string who = $"캐릭터 {i + 1}번";
                if (c == null) { list.Add(who + " 칸이 비어 있다."); continue; }
                who += $"({c.Label})";
                if (c.prefab == null) list.Add(who + ": prefab 이 비어 있다 — 'Tools/좀비 오버런/데이터 테이블/캐릭터 프리팹 만들기·갱신' 으로 채울 것.");
                if (c.bodyMaterial == null) list.Add(who + ": bodyMaterial 이 비어 있다(베이스 몸 색 그대로).");
                if (c.weapon != null && c.weapon.stats == null) list.Add(who + ": 무기 " + c.weapon.name + " 에 수치(stats)가 없다 — ProtoConfig 폴백으로 쏜다.");
                if (c.ultimate != null && string.IsNullOrEmpty(c.ultimate.displayName)) list.Add(who + ": 필사기 " + c.ultimate.name + " 의 displayName 이 비어 있다.");
            }
        }
        return list;
    }

    static void CheckEnemy(List<string> list, string slot, EnemyDef d, Proto.ZombieKind expect)
    {
        if (d == null) { list.Add($"적 테이블: {slot} 칸이 비어 있다 — 이 종류는 나오지 않는다."); return; }
        if (d.behaviour != expect) list.Add($"적 테이블: {slot} 칸의 {d.name} 은 움직임이 {d.behaviour} 다 (보통 {expect}). 의도한 것이면 무시.");
        if (d.hp <= 0f) list.Add($"적 {d.name}: hp 가 0 이하다.");
        if (d.moveSpeed < 0f) list.Add($"적 {d.name}: moveSpeed 가 음수다.");
        if ((slot == "ranged" || slot == "dog") && d.chance <= 0f) list.Add($"적 {d.name}: chance 가 0 이라 일반 스폰에 섞이지 않는다(디버그 키로만 나온다).");
    }

    // ── 플레이 들어갈 때 자동 검증 ────────────────────────────────────
    // 테이블 참조가 빠진 채로 플레이하면 증상이 "그 적이 안 나온다"로만 보여 원인을 찾기 어렵다.
    // 경고만 띄우고 막지는 않는다 — 일부러 칸을 비워 테스트할 때도 있다.
    [InitializeOnLoadMethod]
    static void HookPlayMode()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        // 도메인 리로드마다 보고서를 한 번 쓴다 — 에디터 밖(세션·CI)에서 참조가 제대로 풀렸는지 읽을 수 있게.
        EditorApplication.delayCall += () => { try { WriteReport(false); } catch (System.Exception e) { Debug.LogWarning("[데이터 테이블] 보고서 실패: " + e.Message); } };
    }

    const string ReportPath = "Library/DataTableReport.txt";

    [MenuItem("Tools/좀비 오버런/데이터 테이블/보고서 쓰기 (Library/DataTableReport.txt)")]
    public static void WriteReportMenu() => WriteReport(true);

    /// 테이블이 실제로 어떻게 풀렸는지(참조가 가리키는 에셋 이름·핵심 값)를 텍스트로 남긴다.
    /// 인스펙터의 "Missing" 은 코드에서 null 로 보이므로, 이름이 "(없음)" 이면 참조가 깨진 것이다.
    static void WriteReport(bool log)
    {
        var sb = new StringBuilder();
        sb.AppendLine("데이터 테이블 보고서  " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        var problems = Validate();
        sb.AppendLine("문제 " + problems.Count + "건");
        foreach (var p in problems) sb.AppendLine("  - " + p);

        var et = AssetDatabase.LoadAssetAtPath<EnemyTable>(EnemyTablePath);
        sb.AppendLine();
        sb.AppendLine("[적 테이블] " + (et != null ? EnemyTablePath : "(없음)"));
        if (et != null)
        {
            Enemy(sb, "walker", et.walker); Enemy(sb, "runner", et.runner); Enemy(sb, "ranged", et.ranged); Enemy(sb, "dog", et.dog);
            Enemy(sb, "elite", et.elite); Enemy(sb, "midBoss", et.midBoss); Enemy(sb, "finalBoss", et.finalBoss);
            sb.AppendLine($"  일정: 중간 보스 {et.midBossAt}s · 최종 {et.finalBossAt}s(+{et.finalBossAfterMid}) · 돌격 {et.rushAt}s×{et.rushCount} · 포위 {et.ringAt}s×{et.ringCount} · 엘리트 {string.Join("/", et.eliteSpawnTimes ?? new float[0])}s");
        }

        var ct = AssetDatabase.LoadAssetAtPath<CharacterTable>(CharTablePath);
        sb.AppendLine();
        sb.AppendLine("[캐릭터 테이블] " + (ct != null ? CharTablePath : "(없음)"));
        if (ct != null && ct.roster != null)
            for (int i = 0; i < ct.roster.Length; i++)
            {
                var c = ct.roster[i];
                if (c == null) { sb.AppendLine($"  {i + 1}. (빈 칸)"); continue; }
                string ult = c.ultimate != null ? $"{c.ultimate.name}:{c.ultimate.GetType().Name} '{c.ultimate.displayName}'" : "(없음)";
                string wp = c.weapon != null ? c.weapon.name + (c.weapon.stats != null ? "/" + c.weapon.stats.name : "/수치 없음") : "(없음)";
                sb.AppendLine($"  {i + 1}. {c.name} '{c.Label}'  prefab={Name(c.prefab)}  body={Name(c.bodyMaterial)}  weapon={wp}  ult={ult}");
            }

        foreach (var guid in AssetDatabase.FindAssets("t:UltimateDef"))
        {
            var u = AssetDatabase.LoadAssetAtPath<UltimateDef>(AssetDatabase.GUIDToAssetPath(guid));
            if (u == null) continue;
            string extra = u is UltBarrageDef b ? $"spread {b.spread}/{b.spreadA}/{b.spreadB} tracer {b.tracerColor}"
                         : u is UltFlameRingDef f ? $"radius {f.radius}/{f.radiusA} dur {f.duration}/{f.durationII}"
                         : u is UltTargetKillDef t ? $"targets {t.targets}/{t.targetsA}/{t.targetsB} mul {t.damageMul}/{t.damageMulI}" : "";
            sb.AppendLine($"[필사기] {u.name} '{u.displayName}' color {u.color}  {extra}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
        if (log) Debug.Log("[데이터 테이블] 보고서 → " + ReportPath + "\n" + sb);
    }

    static string Name(Object o) => o != null ? o.name : "(없음)";

    static void Enemy(StringBuilder sb, string slot, EnemyDef d)
    {
        if (d == null) { sb.AppendLine($"  {slot}: (없음)"); return; }
        sb.AppendLine($"  {slot}: {d.name} '{d.displayName}' {d.behaviour} hp {d.hp} spd {d.moveSpeed} contact {d.contactDamage}@{d.contactRange} xp {d.xp} scale {d.scale} body {d.BodyRadius:0.###} knock {d.knockback} skin={Name(d.skin)} unlock {d.unlockAt} chance {d.chance} max {d.maxAlive} gas={d.gas.enabled}");
    }

    static void OnPlayModeChanged(PlayModeStateChange s)
    {
        if (s != PlayModeStateChange.ExitingEditMode) return;
        foreach (var p in Validate()) Debug.LogWarning("[데이터 테이블] " + p);
    }
}
