using System;
using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 레벨업 팝업(<see cref="ProtoSkillSelectPopup"/>)에 쓸 프리팹 · 카드 바탕 · 강화 항목별 아이콘.
    /// 에셋: <c>Resources/Data/SkillSelectSkin.asset</c>(파일 자리 <c>Assets/_Reference/SurvivorAce/UI/Resources/Data</c> — GitHub 에 없음). 없거나 <see cref="useThisPopup"/> 가 꺼져 있으면 ProtoHUD 의 예전 카드 화면이 뜬다.
    ///
    /// <para>지금 값은 Survivor Ace 원작 사본(<c>Assets/_Reference/SurvivorAce/UI/SkillSelect — GitHub 에 없음</c>)을 가리킨다 — 출시 전에 우리 그림으로 바꾼다.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Proto/Skill Select Skin", fileName = "SkillSelectSkin")]
    public class ProtoSkillSelectSkin : ScriptableObject
    {
        public const string ResourcePath = "Data/SkillSelectSkin";
        public const string TangTangPath = "Data/TtSkillSelectSkin";

        [Tooltip("끄면 예전 코드 카드 화면(ProtoHUD.BuildLevelUp)으로 돌아간다")]
        public bool useThisPopup = true;

        public enum Style { SurvivorAce, TangTang }
        [Tooltip("팝업 프리팹 종류 — SurvivorAce = UI_DankeSkillSelect 사본(ProtoSkillSelectPopup), TangTang = UI_TTSkillSelect(ProtoTtSkillSelectPopup)")]
        public Style style = Style.SurvivorAce;

        [Tooltip("레벨업 3택 팝업 프리팹 — 루트에 Canvas, 원작 이름의 자식들(DankeSelectSkillItem_1~3 등)")]
        public GameObject popupPrefab;

        [Tooltip("끄면 카드 안 아이콘 그림(과 보유 줄 아이콘)을 그리지 않는다 — 아래 entries 의 icon 은 그대로 둔다")]
        public bool showIcons = true;

        [Tooltip("능력치 카드 바탕(파랑)")] public Sprite bluePanel;
        [Tooltip("무기 카드 바탕(노랑)")] public Sprite yellowPanel;

        [Serializable]
        public class Entry
        {
            [Tooltip("강화 항목 id(ProtoRun.Upgrade.id). 보조 장비는 gear_orbit · gear_drone · gear_grenade · gear_heli")]
            public string id;
            public Sprite icon;
            [Tooltip("켜면 무기(노랑 카드 · 보유 줄 왼쪽), 끄면 능력치(파랑 · 오른쪽)")]
            public bool weapon;
            [Tooltip("카드 색 — 탕탕 판 레벨업 카드의 머리 띠 · 몸통 · 아이콘 뒤 원에 곱한다(11-17, 카드 12종 모두 다른 색). 알파 0 이면 무기 주황 · 능력치 초록 기본")]
            public Color color;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].id == id) return entries[i];
            return null;
        }

        static ProtoSkillSelectSkin _loaded;
        static bool _tried;

        public static ProtoSkillSelectSkin Load()
        {
            if (_loaded == null && !_tried)
            {
                _tried = true;
                // 그림체(ProtoUiTheme)가 탕탕이면 탕탕 스킨, 없으면 Survivor Ace 스킨
                if (ProtoUiTheme.Current == ProtoUiTheme.Theme.TangTang) _loaded = Resources.Load<ProtoSkillSelectSkin>(TangTangPath);
                if (_loaded == null) _loaded = Resources.Load<ProtoSkillSelectSkin>(ResourcePath);
            }
            return _loaded;
        }
    }
}
