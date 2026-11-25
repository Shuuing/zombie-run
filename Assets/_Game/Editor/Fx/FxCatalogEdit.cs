using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Proto;

/// <summary>
/// 프리팹을 끌어다 놓으면 카탈로그 항목을 찾거나 만들어 ID 를 돌려주는 편집기 공용 도우미.
///
/// <para>런타임은 여전히 <b>ID 로만</b> 효과를 부른다 — 풀 · 동시 상한 · 쇼케이스 목록이 전부 ID 단위라서. 이 도우미는 사람이 ID 를
/// 손으로 적지 않게 할 뿐이다: 시트 이벤트의 프리팹 칸, 무기 연출 슬롯의 프리팹 칸, 카탈로그 탭의 "프리팹 → 항목" 칸이 전부
/// <see cref="EnsureEntryForPrefab"/> 로 온다. 같은 프리팹은 늘 같은 항목(ID)으로 간다.</para>
///
/// <para>새 항목의 ID 는 프리팹 이름(겹치면 _2, _3 …), 수명은 파티클 시스템에서 잰다(<see cref="EstimateLifetime"/>).
/// 자리 규약(+Z 방향 · 크기 1 기준)과 층은 사람이 note 에 적어 둔다.</para>
/// </summary>
public static class FxCatalogEdit
{
    public static FxCatalog Catalog => AssetDatabase.LoadAssetAtPath<FxCatalog>(FxAssetsSetup.CatalogPath);

    /// 이 ID 의 카탈로그 항목. 없으면 null.
    public static FxCatalog.Entry Find(string id)
    {
        var cat = Catalog;
        return cat != null && !string.IsNullOrEmpty(id) ? cat.Find(id) : null;
    }

    /// 이 ID 에 물린 프리팹. 카탈로그에 없거나 코드 그림이면 null.
    public static GameObject PrefabOf(string id)
    {
        var e = Find(id);
        return e != null ? e.prefab : null;
    }

    /// 이 프리팹을 쓰는 항목. 없으면 null.
    public static FxCatalog.Entry FindByPrefab(GameObject prefab)
    {
        var cat = Catalog;
        if (cat == null || prefab == null) return null;
        return cat.entries.FirstOrDefault(e => e != null && e.prefab == prefab);
    }

    /// 프리팹을 쓰는 항목의 ID. 없으면 프리팹 이름으로 새 항목을 만들어 그 ID 를 돌려준다. 카탈로그가 없으면 먼저 만든다.
    /// <paramref name="onTop"/>: 새로 만드는 항목의 '항상 위에'(기본 끔). 이미 있는 항목은 안 바꾼다. 11-13 무기 총구 · 피격 슬롯이 켜서 만들게 했다가 사용자 요청으로 되돌림 — 지금은 부르는 곳이 모두 끈 채.
    public static string EnsureEntryForPrefab(GameObject prefab, string undoName = "카탈로그 항목 (프리팹)", bool onTop = false)
    {
        if (prefab == null) return null;
        var cat = Catalog;
        if (cat == null) { FxAssetsSetup.CreateOrUpdate(); cat = Catalog; }
        if (cat == null) return null;

        var found = cat.entries.FirstOrDefault(e => e != null && e.prefab == prefab);
        if (found != null) return found.id;

        Undo.RecordObject(cat, undoName);
        var entry = new FxCatalog.Entry
        {
            id = UniqueId(cat, prefab.name),
            prefab = prefab,
            lifetime = EstimateLifetime(prefab),
            onTop = onTop,
            note = "프리팹을 끌어다 놓아 생긴 항목. 어느 층(정보 / 타격감 / 세리머니 / 분위기)이고 자리 규약이 무엇인지 적어 둘 것.",
        };
        cat.entries.Add(entry);
        EditorUtility.SetDirty(cat);
        FxCatalog.Reload();
        Debug.Log($"[FX 카탈로그] '{entry.id}' 항목을 만들었다 — 프리팹 {prefab.name}, 수명 {(entry.lifetime > 0f ? entry.lifetime.ToString("0.0") + "s" : "스팬(루프)")}{(onTop ? ", 항상 위에" : "")}");
        return entry.id;
    }

    /// 항목의 '항상 위에'를 바꾼다(되돌리기 가능). 이 효과를 쓰는 모든 무기 · 이벤트에 같이 먹는다.
    public static void SetOnTop(FxCatalog.Entry entry, bool on)
    {
        var cat = Catalog;
        if (cat == null || entry == null || entry.onTop == on) return;
        Undo.RecordObject(cat, "항상 위에");
        entry.onTop = on;
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssetIfDirty(cat);
        FxCatalog.Reload();
    }

    /// 프리팹 루트 배율을 바꿔 저장한다(Ctrl+Z 로 되돌리면 다시 저장할 것). 이 효과를 쓰는 모든 곳에 먹는다 — ProtoFx 는 낼 때마다 루트 배율을 읽는다.
    /// 그 프리팹이 프리팹 모드로 열려 있으면 그쪽 저장이 덮어쓸 수 있다.
    public static void SetPrefabRootScale(GameObject prefab, Vector3 scale)
    {
        if (prefab == null || scale.x <= 0f || scale.y <= 0f || scale.z <= 0f) return;
        var so = new SerializedObject(prefab.transform);
        so.FindProperty("m_LocalScale").vector3Value = scale;
        so.ApplyModifiedProperties();
        PrefabUtility.SavePrefabAsset(prefab);
        Debug.Log($"[FX 카탈로그] {prefab.name} 루트 배율 → {scale.x:0.###}");
    }

    /// 빈 항목을 만든다(코드 그림 이름을 적거나 프리팹을 나중에 넣을 때).
    public static FxCatalog.Entry AddEmpty(string baseId = "new_fx")
    {
        var cat = Catalog;
        if (cat == null) return null;
        Undo.RecordObject(cat, "카탈로그 항목 추가");
        var entry = new FxCatalog.Entry { id = UniqueId(cat, baseId), lifetime = 2f };
        cat.entries.Add(entry);
        EditorUtility.SetDirty(cat);
        FxCatalog.Reload();
        return entry;
    }

    public static bool Remove(FxCatalog.Entry entry)
    {
        var cat = Catalog;
        if (cat == null || entry == null || !cat.entries.Contains(entry)) return false;
        Undo.RecordObject(cat, "카탈로그 항목 삭제");
        cat.entries.Remove(entry);
        EditorUtility.SetDirty(cat);
        FxCatalog.Reload();
        return true;
    }

    /// ID 를 바꾼다. 같은 ID 가 있으면 false. 이 ID 를 쓰는 시트 이벤트·무기 슬롯·코드는 따라오지 않는다 — 부르는 쪽도 고칠 것.
    public static bool Rename(FxCatalog.Entry entry, string newId)
    {
        var cat = Catalog;
        if (cat == null || entry == null) return false;
        newId = string.IsNullOrEmpty(newId) ? "" : newId.Trim();
        if (newId == entry.id) return true;
        if (string.IsNullOrEmpty(newId) || cat.Find(newId) != null) return false;
        Undo.RecordObject(cat, "카탈로그 ID");
        entry.id = newId;
        cat.InvalidateLookup();   // Find 의 ID 사전을 다시 만들게(2026-11-20)
        EditorUtility.SetDirty(cat);
        FxCatalog.Reload();
        return true;
    }

    static string UniqueId(FxCatalog cat, string baseId)
    {
        string id = string.IsNullOrEmpty(baseId) ? "fx" : baseId.Trim();
        if (cat.Find(id) == null) return id;
        for (int i = 2; i < 1000; i++)
        {
            string c = id + "_" + i;
            if (cat.Find(c) == null) return c;
        }
        return id + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
    }

    /// 파티클 시스템들에서 한 번 재생 길이를 잰다 — 지연 + 마지막으로 낳는 때 + 입자 수명의 최대. 루프가 하나라도 있으면 0 = 스팬(코드가 켜고 끈다).
    /// 파티클이 하나도 없으면(메시·트레일만) 끝을 모르니 기본 2초.
    /// <para>11-13: 버스트만 쓰는 시스템은 duration 대신 마지막 버스트 시각으로(팩 프리팹은 duration 이 5초 같은 의미 없는 값이라 MSFX 총구가 6초로 잡혔다).
    /// 그리지 않는 빈 껍데기(렌더러 · 방출 꺼짐, 입자 상한 0, 꺼진 오브젝트 — 팩 프리팹 루트에 흔한 반복 시스템)는 세지 않는다(그 반복 때문에 스팬으로 잘못 잡혔다).</para>
    public static float EstimateLifetime(GameObject prefab)
    {
        if (prefab == null) return 2f;
        var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0) return 2f;
        float max = 0f;
        bool any = false;
        foreach (var ps in systems)
        {
            var m = ps.main;
            var em = ps.emission;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null || !r.enabled || !em.enabled || m.maxParticles <= 0 || !ActiveUnder(ps.transform, prefab.transform)) continue;
            any = true;
            if (m.loop) return 0f;
            bool continuous = MaxOf(em.rateOverTime) > 0f || MaxOf(em.rateOverDistance) > 0f;
            float last = continuous ? m.duration : 0f;
            for (int i = 0; i < em.burstCount; i++)
            {
                var b = em.GetBurst(i);
                last = Mathf.Max(last, b.time + Mathf.Max(0, b.cycleCount - 1) * b.repeatInterval);
            }
            float t = MaxOf(m.startDelay) + last + MaxOf(m.startLifetime);
            if (t > max) max = t;
        }
        if (!any) return 2f;
        return Mathf.Max(0.3f, Mathf.Ceil(max * 10f) / 10f);
    }

    /// 프리팹 안에서 이 오브젝트부터 루트까지 전부 켜져 있나(프리팹 에셋은 activeInHierarchy 를 믿을 수 없다).
    static bool ActiveUnder(Transform t, Transform root)
    {
        for (; t != null; t = t.parent)
        {
            if (!t.gameObject.activeSelf) return false;
            if (t == root) break;
        }
        return true;
    }

    static float MaxOf(ParticleSystem.MinMaxCurve c)
    {
        switch (c.mode)
        {
            case ParticleSystemCurveMode.Constant:     return c.constant;
            case ParticleSystemCurveMode.TwoConstants: return c.constantMax;
            default:                                   return c.curveMultiplier;   // 커브는 보통 0~1 — 배율이 상한
        }
    }

    /// 효과 ID 드롭다운 — 카탈로그 ID(프리팹/코드 그림 표시) + 카탈로그 밖 코드 그림 이름 + 비우기.
    public static void ShowIdMenu(string current, Action<string> set)
    {
        ProtoFxBuiltins.Register();
        var cat = Catalog;
        var menu = new GenericMenu();
        if (cat != null)
            foreach (var en in cat.entries.Where(x => x != null && !string.IsNullOrEmpty(x.id)).OrderBy(x => x.id))
            {
                string id = en.id;
                string tag = en.prefab != null ? " (프리팹 " + en.prefab.name + ")" : !string.IsNullOrEmpty(en.builtin) ? " (코드 그림)" : " (비어 있음)";
                menu.AddItem(new GUIContent("카탈로그/" + id + tag), id == current, () => set(id));
            }
        foreach (var b in ProtoFx.BuiltinNames.OrderBy(x => x))
        {
            if (cat != null && cat.Find(b) != null) continue;
            string id = b;
            menu.AddItem(new GUIContent("코드 그림 (카탈로그 밖)/" + id), id == current, () => set(id));
        }
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("비우기"), string.IsNullOrEmpty(current), () => set(""));
        menu.ShowAsContext();
    }
}
