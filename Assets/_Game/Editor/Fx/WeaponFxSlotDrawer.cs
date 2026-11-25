using UnityEditor;
using UnityEngine;
using Proto;

/// <summary>
/// 무기 연출 슬롯(<see cref="WeaponFxSet.Slot"/>)의 인스펙터 — 1줄 <b>프리팹 칸</b>, 2줄 ID · ▾ · <b>항상 위에</b>, 3줄 <b>크기</b> 슬라이더.
/// 프리팹을 끌어다 놓으면 그 프리팹의 카탈로그 항목(없으면 새로)의 ID 가 들어간다(<see cref="FxCatalogEdit.EnsureEntryForPrefab"/>).
/// 코드 그림을 쓰는 슬롯은 프리팹 칸이 비어 있고 ID 만 있다 — ▾ 로 고른다. 프리팹 칸을 비우면 슬롯이 꺼진다(ID 도 비운다).
///
/// <para>11-13 사용자 "총구 이펙트는 총보다, hit 는 좀비보다 위로 · FX 툴에 사이즈 수정 기능":
/// 같은 날 사용자가 "그냥 되돌려줘" — 총구 · 피격 칸에 놓을 때 '항상 위에'를 자동으로 켜던 기본값을 뺐다(새 항목은 늘 끈 채). 켜고 끄는 체크만 남김.
/// '항상 위에'는 카탈로그 항목의 값이라 이 효과를 쓰는 모든 무기에 같이 먹는다. 크기는 이 무기의 이 자리에만(ctx.scale) — 효과 자체를 키우려면 카탈로그 탭의 '크기(프리팹 루트)'.</para>
/// </summary>
[CustomPropertyDrawer(typeof(WeaponFxSet.Slot))]
public class WeaponFxSlotDrawer : PropertyDrawer
{
    const float Line = 18f, Gap = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => Line * 3f + Gap * 2f;

    public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
    {
        var idProp = property.FindPropertyRelative("fxId");
        var scaleProp = property.FindPropertyRelative("scale");
        if (idProp == null || scaleProp == null) { EditorGUI.PropertyField(rect, property, label, true); return; }

        EditorGUI.BeginProperty(rect, label, property);
        var r1 = new Rect(rect.x, rect.y, rect.width, Line);
        var r2 = new Rect(rect.x, rect.y + Line + Gap, rect.width, Line);
        var r3 = new Rect(rect.x, rect.y + (Line + Gap) * 2f, rect.width, Line);
        float indent = EditorGUIUtility.labelWidth;

        // 1줄: 이름 | 프리팹 — ID 에 물린 프리팹을 보여 주고, 놓으면 ID 를 바꾼다
        var cur = FxCatalogEdit.PrefabOf(idProp.stringValue);
        EditorGUI.BeginChangeCheck();
        var np = (GameObject)EditorGUI.ObjectField(r1, label, cur, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck() && np != cur)
            idProp.stringValue = np == null ? "" : FxCatalogEdit.EnsureEntryForPrefab(np, "무기 연출 슬롯 (프리팹)");

        // 2줄: (들여쓰기) ID [▾]  [✓ 항상 위에]
        const float BtnW = 22f, TopW = 78f;
        var rId = new Rect(r2.x + indent, r2.y, Mathf.Max(40f, r2.width - indent - BtnW - TopW - 8f), Line);
        var rBtn = new Rect(rId.xMax + 2f, r2.y, BtnW, Line);
        var rTop = new Rect(rBtn.xMax + 6f, r2.y, TopW, Line);

        string status = Status(idProp.stringValue);
        EditorGUI.PropertyField(rId, idProp, GUIContent.none);
        if (string.IsNullOrEmpty(idProp.stringValue))
            EditorGUI.LabelField(rId, new GUIContent("  (비어 있음 — 이 자리는 안 낸다)"), EditorStyles.centeredGreyMiniLabel);
        if (EditorGUI.DropdownButton(rBtn, new GUIContent("▾", status), FocusType.Passive))
        {
            var so = property.serializedObject;
            string path = idProp.propertyPath;
            FxCatalogEdit.ShowIdMenu(idProp.stringValue, id =>
            {
                so.Update();
                var p = so.FindProperty(path);
                if (p != null) { p.stringValue = id; so.ApplyModifiedProperties(); }
            });
        }

        var entry = FxCatalogEdit.Find(idProp.stringValue);
        using (new EditorGUI.DisabledScope(entry == null || entry.prefab == null))
        {
            bool on = entry != null && entry.onTop;
            bool now = EditorGUI.ToggleLeft(rTop, new GUIContent("항상 위에",
                "카탈로그 항목의 값 — 총 · 적 몸에 가려지지 않게 오버레이 카메라(FxOverlay)가 따로 그린다. 바닥 효과 · 연기 · 피는 끈다. " +
                "이 효과를 쓰는 모든 무기에 같이 먹는다. 프리팹 효과만, 플레이 중에만"), on);
            if (entry != null && now != on) FxCatalogEdit.SetOnTop(entry, now);
        }

        // 3줄: 크기 슬라이더 — 이 무기의 이 자리 배율(ctx.scale)
        string real = entry != null && entry.prefab != null
            ? $" — 실제 = 프리팹 루트 {entry.prefab.transform.localScale.x:0.###} × {scaleProp.floatValue:0.##} = {entry.prefab.transform.localScale.x * scaleProp.floatValue:0.###}"
            : "";
        var sizeLabel = new GUIContent("    크기", "이 무기의 이 자리만 키우고 줄인다(1 = 기본, 소총 · 잡좀비 기준). 덩치 · 치명타는 코드가 따로 곱한다. " +
                                                  "효과 자체(모든 무기)를 바꾸려면 카탈로그 탭의 '크기(프리팹 루트)'" + real);
        EditorGUI.BeginChangeCheck();
        float s = EditorGUI.Slider(r3, sizeLabel, scaleProp.floatValue, 0.05f, 4f);
        if (EditorGUI.EndChangeCheck()) scaleProp.floatValue = Mathf.Max(0.01f, s);
        EditorGUI.EndProperty();
    }

    static string Status(string id)
    {
        if (string.IsNullOrEmpty(id)) return "ID 가 비어 있다 — 이 자리는 아무것도 안 낸다";
        var e = FxCatalogEdit.Find(id);
        if (e == null) return ProtoFx.HasBuiltin(id) ? "카탈로그에 없음 — 코드 그림 이름으로 바로 쓴다" : "카탈로그에도 코드 그림에도 없는 ID";
        if (e.prefab != null) return "프리팹 " + e.prefab.name + (e.onTop ? " · 항상 위에" : "");
        return !string.IsNullOrEmpty(e.builtin) ? "코드 그림 " + e.builtin : "항목에 프리팹도 코드 그림도 없다";
    }
}
