using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// 레벨업 스킬 3택 팝업 — Survivor Ace 원작 <c>UI_DankeSkillSelect</c> 프리팹(참고용 사본,
    /// <c>Assets/_Reference/SurvivorAce/UI/SkillSelect — GitHub 에 없음</c>)을 띄우고 카드 · 보유 줄 · 새로고침을 채운다.
    ///
    /// <para>게임 코드(ProtoRun 등)를 모른다 — 넣어 줄 값과 콜백만 받는다. 연결은 <see cref="ProtoHUD"/> 가 한다.
    /// 프리팹 안 오브젝트는 이름으로 찾는다(원작 이름 그대로: text_SkillName · img_SkillIcon · SkillStarCtrl …).</para>
    ///
    /// <para>원작사 그림 · 글꼴을 쓰는 임시 화면이다. 출시 전에 우리 그림으로 바꾼다.</para>
    /// </summary>
    public class ProtoSkillSelectPopup : MonoBehaviour, IProtoSkillPopup
    {
        public struct CardData
        {
            public string title, desc, hint;   // hint = 금색 덧줄(진화 안내 등), 없으면 null
            public int level, maxLevel;        // 지금 레벨(고르면 +1). maxLevel 99 이상이면 별을 숨긴다
            public bool weapon;                // 노랑(무기) / 파랑(능력치) 카드
            public Sprite icon;
            public Color color;                // 카드 색(탕탕 판 카드 바탕에 곱한다, 11-17). 알파 0 이면 무기 · 능력치 기본 색
        }

        public struct OwnedData { public Sprite icon; public int level; }

        class CardView
        {
            public GameObject go;
            public Image panel, icon;
            public Text title, desc;
            public GameObject newTag, stars;
            public RectTransform gray, light;
            public float spacing;
            public Button button;
            public Animator press;     // fg 의 Anim_skillSelectItemTween — 누름(Pressed → PressEnd) · 나머지 카드 내려감(Down)
            public Animator reveal;    // 카드 루트의 Anim_skillSelectItem — 등장(start → reel → reelEnd → afterEnd → end)
            public GameObject reel;    // 슬롯처럼 도는 아이콘 줄(Reel/Skill_1~7/Icon)
            public Image[] reelIcons;
            // 원작 클립이 켜고 끄는 것(m_IsActive) — 같은 시각에 코드도 맞춰 둔다(애니메이터가 못 바꾸는 경우에도 같게 보이게)
            public GameObject body, iconRoot, iconBg;   // fg/GameObject(카드 몸체) · fg/Root(아이콘 묶음) · fg/Root/img_SkillIcon_bg(파란 상자)
        }

        // 등장 연출 진행(카드마다). 레벨업 중엔 timeScale 0 이라 실시간 시계(_clock)로 돈다
        class RevealState { public int phase = 99; public float start, phaseAt; public bool full; }
        const int Done = 99;
        // 원작 루트 Normal 클립에서 카드 1~3 이 튀어나오는 시각(알파 0→1 + 0.8→1.1→1)
        static readonly float[] Stagger = { 0.23f, 0.33f, 0.42f };
        const float IconPopAt = 0.45f;        // start 클립에서 아이콘 상자가 튀어나오는 시각
        const float ReelSpeed = 1.5f;         // 슬롯 한 바퀴 0.9초 ÷ 1.5 (원작은 등급별 속도 배열)
        const float PressTime = 0.3f, PressEndTime = 0.4f;   // 누름 → 누름 끝 → 닫힘
        const float RevealTimeout = 4f;       // 무슨 일이 있어도 이 안에 카드를 다 보이게
        // 카드 슬롯 간격(화면 폭에 맞춤). 원작 1080 폭 배치 = 양옆 여백 30 · 카드 그림 사이 17.4(줄 간격 −47.6)
        const float SlotMargin = 30f;         // 화면 끝 ~ 바깥 카드 그림
        const float SlotGapMin = 17.4f;       // 카드 그림 사이 최소(원작 값) — 이보다 좁아야 하면 줄 전체를 줄인다
        const float SlotGapMax = 110f;        // 넓은 화면에서 이 이상은 벌리지 않는다(카드가 흩어져 보이지 않게)
        // 가로 화면(우리 게임은 가로) — 원작은 세로 1080×1920 화면용이라 Expand 로 넣으면 높이 1920 에 맞춰 작게 가운데만 차지한다.
        // 가로일 땐 팝업 판(Root/Panel) 전체를 화면 높이에 맞게 키우고, 내용(제목 띠 ~ 카드 아래)을 세로 가운데로 옮긴다.
        const float WideMarginY = 70f;        // 위아래 여백(캔버스 단위 — 가로 화면 높이는 늘 1920)
        const float WideScaleMax = 1.6f;      // 판 배율 상한
        const float SlotGapMaxWide = 170f;    // 가로 화면 카드 그림 사이 최대(판 배율 적용 전 단위)
        const float WideTitleGap = 90f;       // 가로 화면: 제목 띠 아래 ~ 카드 그림 위 간격(판 단위, 11-13 "슬롯 창을 좀 더 올려줘")
        const float WideRemainGap = 30f;      // 가로 화면: 카드 그림 아래 ~ 남은 횟수 글자 위 간격(판 단위)

        class SlotView { public GameObject go, iconGo, levelGo, maxGo; public Image icon; public Text level; }

        CardView[] _cards;
        readonly List<SlotView> _left = new List<SlotView>(), _right = new List<SlotView>();
        Sprite _bluePanel, _yellowPanel;
        Text _title, _remain, _rerollCount;
        Button _reroll, _skip;
        CanvasGroup _rerollGroup;
        Animator _rootAnim;
        Animator[] _anims;
        bool _useNewTag;   // 프리팹에서 "신규" 딱지를 다 꺼 두었으면 쓰지 않는다(보유 줄 · 새로고침도 프리팹에서 끄면 그대로 꺼진 채)
        Action<int> _onPick;
        Action _onReroll, _onSkip;
        bool _locked;
        float _clock;
        RevealState[] _rev;
        readonly List<Sprite> _reelPool = new List<Sprite>();
        int _picked = -1;
        float _pickAt;
        bool _pickEndSent;
        RectTransform _canvasRect, _select;
        HorizontalLayoutGroup _selectLayout;
        float _slotW = 393f, _cardVisW = 328f;   // 카드 칸 폭 · 그 안 실제 그림 폭(Init 에서 프리팹 값으로)
        float _fitW = -1f, _fitH = -1f;
        int _shownCards;
        // 가로 화면 맞춤: 판 · 판 안 내용의 세로 범위(판 기준, Init 때 프리팹 기본 상태로 잰다 — 연출 중엔 크기 · 위치가 흔들려서)
        class BoundItem { public GameObject go; public float yMin, yMax; }
        readonly List<BoundItem> _bounds = new List<BoundItem>();
        BoundItem _bTitle, _bRemain, _bCard;
        GameObject _haveSkill;
        RectTransform _panel;
        Vector2 _panelPos, _remainPos;
        int _padTop0;   // 카드 줄 레이아웃 원래 위 여백(원작 −77)
        float _remainLift;   // 남은 횟수 글자를 옮긴 거리(판 단위)
        Vector3 _panelScale = Vector3.one;

        public bool IsOpen => gameObject.activeSelf;

        /// 프리팹을 하나 만들어 숨겨 둔다. sortingOrder 는 HUD(10)보다 위로.
        public static ProtoSkillSelectPopup Create(GameObject prefab, int sortingOrder, Sprite bluePanel, Sprite yellowPanel)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab);
            go.name = "_SkillSelectPopup";
            var p = go.AddComponent<ProtoSkillSelectPopup>();
            p._bluePanel = bluePanel; p._yellowPanel = yellowPanel;
            if (!p.Init(sortingOrder)) { Destroy(go); return null; }
            go.SetActive(false);
            return p;
        }

        // ── 찾기 ──────────────────────────────────────────────────────────
        static Transform Find(Transform root, string name)
        {
            var q = new Queue<Transform>(); q.Enqueue(root);
            while (q.Count > 0)
            {
                var t = q.Dequeue();
                if (t != root && t.name == name) return t;
                for (int i = 0; i < t.childCount; i++) q.Enqueue(t.GetChild(i));
            }
            return null;
        }
        static T Get<T>(Transform root, string name) where T : Component
        {
            var t = Find(root, name);
            return t != null ? t.GetComponent<T>() : null;
        }
        static void Off(Transform root, string name)
        {
            var t = Find(root, name);
            if (t != null) t.gameObject.SetActive(false);
        }

        bool Init(int sortingOrder)
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) { Debug.LogWarning("[SkillSelectPopup] 프리팹 루트에 Canvas 가 없다"); return false; }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            // 원작 화면(1080×1920)이 어떤 비율에서도 통째로 들어오게 — 세로폰 1080×2340 이면 배율 1, 가로 에디터면 줄어든다
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            // 레벨업 중엔 Time.timeScale = 0 — 연출은 실시간으로
            _anims = GetComponentsInChildren<Animator>(true);
            foreach (var a in _anims)
            {
                a.updateMode = AnimatorUpdateMode.UnscaledTime;
                // 원작 클립의 애니메이션 이벤트(Listen)를 받을 곳 — 없으면 열 때마다 오류 로그
                if (a.GetComponent<ProtoAnimEventSink>() == null) a.gameObject.AddComponent<ProtoAnimEventSink>();
            }
            _rootAnim = GetComponent<Animator>();
            // 원작은 일부 CanvasGroup 을 코드가 켜고 껐다 — 팝업 전체를 누를 수 있게
            foreach (var g in GetComponentsInChildren<CanvasGroup>(true)) { g.interactable = true; g.blocksRaycasts = true; }

            var t = transform;
            _title = Get<Text>(t, "Text_Title");
            if (_title != null)
            {
                // 제목 띠(BG, 띠 가운데 0) 한가운데로 — 원작 글자는 3.3px 오른쪽에 놓여 있었다
                _title.alignment = TextAnchor.MiddleCenter;
                var trt = _title.rectTransform;
                trt.anchoredPosition = new Vector2(0f, trt.anchoredPosition.y);
            }
            _remain = Get<Text>(t, "text_RemainSkillCount");
            // 우리 게임에 없는 것: 스테이지 몬스터 패널 · 튜토리얼 · 보유 칸 틀 · 넷째 카드 · 안내 문구
            Off(t, "trans_monsterInfo"); Off(t, "GuideRoot"); Off(t, "HaveSkillItemTemp");
            Off(t, "DankeSelectSkillItem_4"); Off(t, "trans_chooseSkillText");
            // Panel/Mask = 화면 전체를 덮는 투명 클릭 막이(원작 NonDrawingGraphic, 원작 코드 obj_mask 가 켜고 껐다).
            // 켜 둔 채면 카드 위에서 모든 클릭을 먹는다 — 등장 중 잠금은 _locked 가 하므로 끈다(11-13 "카드 클릭이 안돼")
            var blocker = t.Find("Root/Panel/Mask");
            if (blocker != null)
            {
                foreach (var g in blocker.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
                blocker.gameObject.SetActive(false);
            }

            _cards = new CardView[3];
            for (int i = 0; i < 3; i++)
            {
                var card = Find(t, "DankeSelectSkillItem_" + (i + 1));
                if (card == null) { Debug.LogWarning("[SkillSelectPopup] 카드 " + (i + 1) + " 없음"); return false; }
                var v = new CardView { go = card.gameObject };
                v.panel = Get<Image>(card, "img_SkillTypeBg");
                v.title = Get<Text>(card, "text_SkillName");
                var nt = Find(card, "obj_new"); v.newTag = nt != null ? nt.gameObject : null;
                if (v.newTag != null && v.newTag.activeSelf) _useNewTag = true;
                v.icon = Get<Image>(card, "img_SkillIcon");
                var st = Find(card, "SkillStarCtrl");
                // 프리팹에서 별 줄을 꺼 두면(11-13 사용자 손편집) 코드도 켜지 않는다 — 신규 딱지와 같은 규칙
                if (st != null && st.gameObject.activeSelf)
                {
                    v.stars = st.gameObject;
                    v.gray = st.Find("Gray") as RectTransform;
                    v.light = st.Find("Light") as RectTransform;
                    var lg = v.gray != null ? v.gray.GetComponent<HorizontalLayoutGroup>() : null;
                    v.spacing = lg != null ? lg.spacing : -10f;
                }
                var item = Find(card, "tran_DescItem");
                if (item != null)
                {
                    var dot = item.Find("Image"); if (dot != null) dot.gameObject.SetActive(false);   // ◆ 없이
                    v.desc = Get<Text>(item, "Text");
                }
                // 원작에서 코드가 등급 · 조합에 따라 켜던 장식 — 일반 카드엔 없다
                foreach (var n in new[] { "img_SkillQuality", "img_skills_square_r", "img_skills_square_y", "img_skills_square_c",
                                          "img_mask_r", "img_mask_y", "img_mask_c", "obj_breakthroughCombine", "obj_combine",
                                          "text_breakThrough", "img_skillElementBg", "obj_Max", "obj_levelUpParent" })
                    Off(card, n);
                var fg = Find(card, "fg");
                if (fg != null) { v.button = fg.GetComponent<Button>(); v.press = fg.GetComponent<Animator>(); }
                if (v.button == null) { Debug.LogWarning("[SkillSelectPopup] 카드 버튼 없음"); return false; }
                var b = card.Find("fg/GameObject"); if (b != null) v.body = b.gameObject;
                var ir = card.Find("fg/Root"); if (ir != null) v.iconRoot = ir.gameObject;
                var ib = card.Find("fg/Root/img_SkillIcon_bg"); if (ib != null) v.iconBg = ib.gameObject;
                v.reveal = card.GetComponent<Animator>();   // 프리팹에선 꺼져 있다(원작 코드가 켜서 돌렸다)
                if (v.reveal != null) { v.reveal.enabled = false; if (v.reveal.runtimeAnimatorController == null) v.reveal = null; }
                var reel = Find(card, "Reel");
                if (reel != null)
                {
                    v.reel = reel.gameObject;
                    var icons = new List<Image>();
                    for (int k = 0; k < reel.childCount; k++) { var ic = Get<Image>(reel.GetChild(k), "Icon"); if (ic != null) icons.Add(ic); }
                    v.reelIcons = icons.ToArray();
                    v.reel.SetActive(false);
                }
                int idx = i;
                v.button.onClick.AddListener(() => Click(idx));
                _cards[i] = v;
            }
            _rev = new RevealState[_cards.Length];
            for (int i = 0; i < _rev.Length; i++) _rev[i] = new RevealState();

            // 카드 줄(HorizontalLayoutGroup) — 화면 폭에 맞춰 간격 · 크기를 FitSlots 가 정한다
            _canvasRect = (RectTransform)transform;
            _select = Find(t, "Select") as RectTransform;
            _selectLayout = _select != null ? _select.GetComponent<HorizontalLayoutGroup>() : null;
            if (_selectLayout != null) _padTop0 = _selectLayout.padding.top;
            var c0 = (RectTransform)_cards[0].go.transform;
            if (c0.sizeDelta.x > 1f) _slotW = c0.sizeDelta.x;
            if (_cards[0].panel != null && _cards[0].panel.rectTransform.sizeDelta.x > 1f) _cardVisW = _cards[0].panel.rectTransform.sizeDelta.x;

            // 새로고침 버튼 + 남은 횟수
            var btn = Find(t, "btn_refresh");
            if (btn != null)
            {
                _reroll = btn.GetComponent<Button>();
                _rerollGroup = btn.GetComponent<CanvasGroup>();
                if (_rerollGroup == null) _rerollGroup = btn.gameObject.AddComponent<CanvasGroup>();
                _rerollCount = Get<Text>(btn, "text_refreshCountShow");
                if (_reroll != null) _reroll.onClick.AddListener(() => { if (!_locked) _onReroll?.Invoke(); });
                // 건너뛰기(우리 GDD §07 — 원작엔 없다): 남은 횟수 글자를 복제해 버튼 아래에
                if (_rerollCount != null)
                {
                    var sk = Instantiate(_rerollCount.gameObject, btn.parent, false);
                    sk.name = "text_skip";
                    var rt = (RectTransform)sk.transform;
                    var brt = (RectTransform)btn;
                    rt.anchorMin = rt.anchorMax = brt.anchorMin;
                    rt.anchoredPosition = brt.anchoredPosition + new Vector2(0f, -brt.rect.height * 0.5f - 60f);
                    var tx = sk.GetComponent<Text>();
                    tx.text = "건너뛰기"; tx.raycastTarget = true;
                    _skip = sk.AddComponent<Button>();
                    _skip.onClick.AddListener(() => { if (!_locked) _onSkip?.Invoke(); });
                }
            }

            // 보유 줄 — 왼쪽(무기) · 오른쪽(능력치). 프리팹에 이미 복제된 칸(HaveSkillItemTemp 이름)을 쓴다
            CollectSlots(Find(t, "img_choose_Left"), _left);
            CollectSlots(Find(t, "img_choose_Right"), _right);

            MeasureContent(t);
            return true;
        }

        /// 판(Root/Panel) 안 내용의 세로 범위를 잰다 — 켜져 있는 것만 FitSlots 에서 합친다.
        void MeasureContent(Transform t)
        {
            _panel = t.Find("Root/Panel") as RectTransform;
            if (_panel == null) return;
            _panelPos = _panel.anchoredPosition;
            _panelScale = _panel.localScale;
            // 카드 칸 위치는 레이아웃이 정한다 — 재기 전에 한 번 돌린다
            if (_select != null) LayoutRebuilder.ForceRebuildLayoutImmediate(_select);
            var tb = Find(t, "TitleBG");
            _bTitle = AddBound(tb != null ? tb.gameObject : null, tb != null ? (tb.Find("BG") ?? tb) : null);
            foreach (var v in _cards) if (v.panel != null) { var b = AddBound(v.go, v.panel.transform); if (_bCard == null) _bCard = b; }
            if (_remain != null) { _remainPos = _remain.rectTransform.anchoredPosition; _bRemain = AddBound(_remain.gameObject, _remain.transform); }
            var rf = Find(t, "trans_refreshSkill");
            if (rf != null)
            {
                if (_reroll != null) AddBound(rf.gameObject, _reroll.transform);
                if (_skip != null) AddBound(rf.gameObject, _skip.transform);
            }
            var hs = Find(t, "HaveSkill");
            if (hs != null)
            {
                _haveSkill = hs.gameObject;
                AddBound(hs.gameObject, Find(hs, "img_choose_Left"));
                AddBound(hs.gameObject, Find(hs, "img_choose_Right"));
            }
        }

        BoundItem AddBound(GameObject owner, Transform x)
        {
            var r = x as RectTransform;
            if (owner == null || r == null) return null;
            var cs = new Vector3[4];
            r.GetWorldCorners(cs);
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var c in cs) { float y = _panel.InverseTransformPoint(c).y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
            var b = new BoundItem { go = owner, yMin = lo, yMax = hi };
            _bounds.Add(b);
            return b;
        }

        /// 가로 화면에서 카드 줄을 제목 띠 바로 아래(WideTitleGap)로 올린다 — 사이 빈자리는 원작 보유 줄 자리라
        /// 보유 줄을 켜 두면 원작 자리 그대로. 카드 줄(Select) 위치는 원작 루트 애니메이터(기본값 쓰기 켜짐)가 덮어쓰므로
        /// 레이아웃 위 여백(가운데 정렬이라 2배)으로 올린다. "선택 기회가 n회 남았습니다!" 도 카드 아래 원작 간격 그대로 같이 올린다.
        /// 돌려주는 값 = 올린 거리(판 단위).
        float LiftCards(bool wide)
        {
            float dy = 0f;
            if (wide && _bTitle != null && _bCard != null && (_haveSkill == null || !_haveSkill.activeInHierarchy))
                dy = Mathf.Max(0f, (_bTitle.yMin - WideTitleGap) - _bCard.yMax);
            if (_selectLayout != null)
            {
                int top = _padTop0 - Mathf.RoundToInt(2f * dy);
                if (_selectLayout.padding.top != top) { _selectLayout.padding.top = top; LayoutRebuilder.MarkLayoutForRebuild(_select); }
                dy = (_padTop0 - top) * 0.5f;
            }
            else dy = 0f;
            // 남은 횟수 글자: 올렸으면 카드 그림 바로 아래(WideRemainGap)로 — 원작 간격(160)이면 판이 덜 커진다
            if (_remain != null && _bRemain != null)
            {
                float ry = 0f;
                if (dy > 0f) ry = (_bCard.yMin + dy - WideRemainGap) - _bRemain.yMax;
                _remain.rectTransform.anchoredPosition = _remainPos + new Vector2(0f, ry);
                _remainLift = ry;
            }
            return dy;
        }

        /// 지금 켜진 내용의 세로 범위(판 기준, 카드를 올리기 전 자리). 없으면 false. skipRemain = 남은 횟수 글자는 따로 셈.
        bool ContentRange(out float lo, out float hi, bool skipRemain)
        {
            lo = float.MaxValue; hi = float.MinValue;
            foreach (var b in _bounds)
            {
                if (b.go == null || !b.go.activeInHierarchy || (skipRemain && b == _bRemain)) continue;
                lo = Mathf.Min(lo, b.yMin); hi = Mathf.Max(hi, b.yMax);
            }
            return hi > lo;
        }

        static void CollectSlots(Transform side, List<SlotView> into)
        {
            var parent = side != null ? side.Find("HaveSkillParent") : null;
            if (parent == null) return;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (!c.name.StartsWith("HaveSkillItemTemp")) continue;
                var un = c.Find("unlock"); if (un == null) continue;
                var lk = c.Find("lock"); if (lk != null) lk.gameObject.SetActive(false);
                var s = new SlotView { go = c.gameObject };
                var ic = un.Find("img_skillIcon"); if (ic != null) { s.iconGo = ic.gameObject; s.icon = ic.GetComponent<Image>(); }
                var lv = un.Find("obj_level");
                if (lv != null)
                {
                    s.levelGo = lv.gameObject;
                    s.level = Get<Text>(lv, "text_skillLevel");
                    var mx = lv.Find("obj_max"); if (mx != null) s.maxGo = mx.gameObject;
                }
                into.Add(s);
            }
        }

        // ── 보이기 ────────────────────────────────────────────────────────
        /// animate = 새로 열릴 때(레벨이 바뀜) 원작 등장 연출을 다시 튼다. 새로고침이면 false.
        public void Show(string title, string remain, IList<CardData> cards, IList<OwnedData> weapons, IList<OwnedData> passives,
                         int rerollsLeft, int rerollsMax, bool animate, Action<int> onPick, Action onReroll, Action onSkip)
        {
            _onPick = onPick; _onReroll = onReroll; _onSkip = onSkip;
            _locked = true;          // 카드가 다 펼쳐질 때까지 누르지 못한다(원작도 등장 중엔 선택을 잠갔다)
            _picked = -1;
            bool wasOpen = gameObject.activeSelf;
            gameObject.SetActive(true);

            if (_title != null) _title.text = title;
            if (_remain != null)
            {
                bool on = !string.IsNullOrEmpty(remain);
                _remain.gameObject.SetActive(on);
                if (on) _remain.text = remain;
            }

            _shownCards = 0;
            for (int i = 0; i < _cards.Length; i++)
            {
                var v = _cards[i];
                bool on = cards != null && i < cards.Count;
                v.go.SetActive(on);
                if (on) { FillCard(v, cards[i]); _shownCards++; }
            }
            _fitW = -1f;
            FitSlots();
            FillSlots(_left, weapons, false);
            FillSlots(_right, passives, true);   // 원작처럼 오른쪽 줄은 파란 칸(끝) 쪽부터 채운다

            if (_rerollCount != null) _rerollCount.text = $"남은 갱신 횟수 {rerollsLeft}/{rerollsMax}";
            if (_reroll != null) _reroll.interactable = rerollsLeft > 0;
            if (_rerollGroup != null) _rerollGroup.alpha = rerollsLeft > 0 ? 1f : 0.45f;

            bool open = animate || !wasOpen;
            if (open)
            {
                foreach (var a in _anims)
                    if (a != null && a.isActiveAndEnabled && a.runtimeAnimatorController != null) { a.Rebind(); a.Update(0f); }
                // 원작 등장: 제목 띠 0.3→1.1→1, 카드 1~3 이 0.23 · 0.33 · 0.42초에 차례로 0.8→1.1→1 로 튀어나온다(0.7초)
                if (_rootAnim != null && _rootAnim.runtimeAnimatorController != null) _rootAnim.SetTrigger("Normal");
            }
            else
            {
                // 새로고침: 눌림 상태 · 내려간 카드를 원래대로
                foreach (var v in _cards)
                    if (v.press != null && v.press.isActiveAndEnabled && v.press.runtimeAnimatorController != null) { v.press.Rebind(); v.press.Update(0f); }
            }
            for (int i = 0; i < _cards.Length; i++)
            {
                bool on = cards != null && i < cards.Count;
                if (on) StartReveal(i, open ? Stagger[Mathf.Min(i, Stagger.Length - 1)] : 0.1f * i, cards[i].icon != null);
                else _rev[i].phase = Done;
            }
        }

        /// 원작 아이콘 슬롯(Reel)에 돌릴 그림들. 비우면 그 카드 아이콘만 돈다.
        public void SetReelIcons(IList<Sprite> icons)
        {
            _reelPool.Clear();
            if (icons != null) foreach (var s in icons) if (s != null) _reelPool.Add(s);
        }

        // ── 등장 연출(카드마다) ────────────────────────────────────────────
        // 아이콘이 있으면 원작 그대로: 아이콘 상자가 튀어나와(start) 슬롯처럼 돌다(reel) 멈추고, 아이콘이 떨어져 자리 잡은 뒤(afterEnd)
        // 카드 몸체가 찌그러졌다 펴지며 펼쳐진다(end). 아이콘이 없으면(스킨 showIcons 끔) 슬롯을 건너뛰고 차례가 되면 바로 몸체가 펼쳐진다.
        void StartReveal(int i, float delay, bool hasIcon)
        {
            var v = _cards[i]; var r = _rev[i];
            if (v.reveal == null) { r.phase = Done; return; }
            v.reveal.enabled = true;
            v.reveal.Rebind();
            v.reveal.Play("Anim_skillSelectItem_start", 0, 0f);
            v.reveal.Update(0f);            // 첫 프레임: 몸체 · 아이콘 상자 숨김
            v.reveal.speed = 0f;            // 차례가 올 때까지 멈춰 둔다
            if (v.reel != null) v.reel.SetActive(false);
            r.full = hasIcon && v.reel != null;
            // 누름 애니메이터(기본값 쓰기 켜짐)가 매 프레임 몸체를 켜 버리므로 등장이 끝날 때까지 멈춘다 — 끝나면 RevealDone 에서 다시 켬
            if (v.press != null) v.press.enabled = false;
            // start 클립 첫 프레임: 몸체 · 아이콘 묶음 꺼짐, 파란 아이콘 상자 켜짐
            Act(v.body, false); Act(v.iconRoot, false); Act(v.iconBg, r.full);
            r.start = _clock + delay;
            r.phase = 0;
        }

        static void Act(GameObject g, bool on) { if (g != null && g.activeSelf != on) g.SetActive(on); }

        static void RevealDone(CardView v, RevealState r)
        {
            r.phase = Done;
            if (v.press != null && v.press.runtimeAnimatorController != null && !v.press.enabled)
            {
                v.press.enabled = true;
                v.press.Rebind();          // 지금(펼쳐진) 모습을 기본값으로 다시 잡고 idle 로
                v.press.Update(0f);
            }
        }

        void EnterEnd(CardView v, RevealState r) { r.phase = 6; r.phaseAt = _clock; }

        void Update() { Tick(Time.unscaledDeltaTime); }

        /// 팝업을 화면 규격에 맞춘다. 캔버스는 CanvasScaler(1080×1920 Expand)를 거친 단위라
        /// 세로폰(9:16 보다 길쭉)은 폭 1080 — 원작 배치 그대로, 세로 태블릿은 카드 간격만 벌린다(최대 SlotGapMax).
        /// 가로 화면(우리 게임)은 높이가 늘 1920 이라 원작 그대로면 작게 가운데만 차지한다 — 판 전체를 높이에 맞게 키우고
        /// (최대 WideScaleMax) 내용을 세로 가운데로, 카드 간격은 SlotGapMaxWide 까지. 카드 줄은 제목 띠 바로 아래로 올린다(LiftCards).
        /// 그래도 모자라면 카드 줄을 줄인다. 크기가 바뀌면(창 크기 · 회전) Tick 이 다시 맞춘다.
        void FitSlots()
        {
            if (_canvasRect == null) return;
            float w = _canvasRect.rect.width, h = _canvasRect.rect.height;
            if (w <= 1f || h <= 1f || (Mathf.Abs(w - _fitW) < 0.5f && Mathf.Abs(h - _fitH) < 0.5f)) return;
            _fitW = w; _fitH = h;

            // 가로 화면: 판 전체를 키워 화면 높이를 채우고 내용을 세로 가운데로. 세로 화면은 원작 배치 그대로
            bool wide = w > h;
            float ps = 1f;
            if (_panel != null)
            {
                float lift = LiftCards(wide);
                // 판 크기 · 자리는 카드를 올리기 전 범위(제목 띠 ~ 카드 아래)로 — 카드만 올라가고 제목 띠는 그대로
                if (wide && ContentRange(out float lo, out float hi, lift > 0f))
                {
                    // 올린 카드 아래로 같이 올라간 남은 횟수 글자 자리는 늘 잡아 둔다(글자가 뜰 때마다 판 크기가 바뀌지 않게)
                    if (lift > 0f && _bRemain != null) lo = Mathf.Min(lo, _bRemain.yMin + _remainLift);
                    ps = Mathf.Clamp((h - 2f * WideMarginY) / (hi - lo), 1f, WideScaleMax);
                    _panel.localScale = new Vector3(ps, ps, 1f);
                    _panel.anchoredPosition = _panelPos + new Vector2(0f, -(lo + hi) * 0.5f * ps);
                }
                else { _panel.localScale = _panelScale; _panel.anchoredPosition = _panelPos; }
            }

            if (_select == null || _selectLayout == null) return;
            int n = Mathf.Max(1, _shownCards);
            float avail = w / ps - 2f * SlotMargin;   // 판이 커진 만큼 판 안 단위로는 화면이 좁다
            float gap = n > 1 ? (avail - n * _cardVisW) / (n - 1) : 0f;
            gap = Mathf.Clamp(gap, SlotGapMin, wide ? SlotGapMaxWide : SlotGapMax);
            float total = n * _cardVisW + (n - 1) * gap;
            float scale = total > avail ? avail / total : 1f;
            // 줄 간격은 칸(393) 기준이라 칸과 그림 폭 차이만큼 빼 준다
            _selectLayout.spacing = gap - (_slotW - _cardVisW);
            _select.localScale = new Vector3(scale, scale, 1f);
        }

        /// 등장 · 선택 연출 진행. 플레이 중엔 Update 가 부른다(테스트는 직접).
        public void Tick(float dt)
        {
            _clock += dt;
            FitSlots();
            bool allDone = true;
            for (int i = 0; i < _rev.Length; i++)
            {
                TickReveal(i);
                if (_rev[i].phase != Done) allDone = false;
            }
            if (allDone && _picked < 0) _locked = false;
            TickPick();
        }

        void TickReveal(int i)
        {
            var r = _rev[i];
            if (r.phase == Done) return;
            var v = _cards[i]; var a = v.reveal;
            if (a == null || !v.go.activeInHierarchy) { RevealDone(v, r); return; }
            float t = _clock - r.start;
            if (t < 0f) return;
            var st = a.GetCurrentAnimatorStateInfo(0);
            if (t > RevealTimeout) { Finish(v); RevealDone(v, r); return; }   // 안전장치: 어떤 경우에도 카드는 보이게
            switch (r.phase)
            {
                case 0:
                    a.speed = 1f;
                    if (!r.full) { a.Play("Anim_skillSelectItem_end", 0, 0f); EnterEnd(v, r); }
                    else r.phase = 1;
                    break;
                case 1:   // 아이콘 상자가 튀어나오는 순간부터 슬롯이 돈다(start 0.45초에 fg/Root 켜짐)
                    if (t >= IconPopAt) { Act(v.iconRoot, true); SetReel(v, true); r.phase = 2; }
                    break;
                case 2:   // start → reel 로 넘어가면 바로 '멈춰'(end) — 슬롯 한 바퀴 끝에서 reelEnd 로
                    if (st.IsName("Anim_skillSelectItem_reel")) { a.SetFloat("speedGreenMult", ReelSpeed); a.SetTrigger("end"); r.phase = 3; }
                    break;
                case 3:   // 슬롯 멈춤 → 진짜 아이콘으로 바꾸고 떨어뜨린다(afterEnd)
                    if (st.IsName("Anim_skillSelectItem_reelEnd")) { SetReel(v, false); a.SetTrigger("finish"); r.phase = 4; r.phaseAt = _clock; }
                    break;
                case 4:   // 아이콘이 자리 잡으면 카드 몸체 펼침(end)
                    if (st.IsName("Anim_skillSelectItem_afterEnd") && st.normalizedTime >= 1f) { a.SetTrigger("AllFinish"); EnterEnd(v, r); }
                    else if (_clock - r.phaseAt > 1f) { a.Play("Anim_skillSelectItem_end", 0, 0f); EnterEnd(v, r); }
                    break;
                case 6:   // end 클립: 0.10초 몸체 켜짐(찌그러졌다 펴짐 · 알파 0.5→1), 0.18초 아이콘 묶음 켜짐 · 파란 상자 꺼짐
                {
                    float e = _clock - r.phaseAt;
                    if (e >= 0.10f) Act(v.body, true);
                    if (e >= 0.18f) { Act(v.iconRoot, true); Act(v.iconBg, false); }
                    if (e >= 0.18f && st.IsName("Anim_skillSelectItem_end") && st.normalizedTime >= 1f) RevealDone(v, r);
                    break;
                }
            }
        }

        void SetReel(CardView v, bool spin)
        {
            if (v.reel == null) return;
            if (spin && v.reelIcons != null)
                for (int k = 0; k < v.reelIcons.Length; k++)
                {
                    var s = _reelPool.Count > 0 ? _reelPool[UnityEngine.Random.Range(0, _reelPool.Count)] : v.icon != null ? v.icon.sprite : null;
                    v.reelIcons[k].sprite = s;
                    v.reelIcons[k].enabled = s != null;
                }
            v.reel.SetActive(spin);
            if (v.icon != null) v.icon.gameObject.SetActive(!spin);
        }

        static void Finish(CardView v)
        {
            if (v.reveal != null) { v.reveal.speed = 1f; v.reveal.Play("Anim_skillSelectItem_end", 0, 1f); v.reveal.Update(0f); }
            if (v.reel != null) v.reel.SetActive(false);
            if (v.icon != null) v.icon.gameObject.SetActive(true);
            Act(v.body, true); Act(v.iconRoot, true); Act(v.iconBg, false);
        }

        // ── 선택 연출 ────────────────────────────────────────────────────
        // 원작: 누른 카드 Pressed(0.93→1.1, 파란 테두리 빛) → PressEnd(0.4초, 빛이 꺼짐) / 나머지 카드 Down(0.27초, 아래로 304 내려가며 사라짐) → 닫힘
        void TickPick()
        {
            if (_picked < 0) return;
            float t = _clock - _pickAt;
            var v = _cards[_picked];
            if (!_pickEndSent && t >= PressTime)
            {
                _pickEndSent = true;
                if (Ready(v.press)) v.press.SetTrigger("PressEnd");
            }
            if (t >= PressTime + PressEndTime)
            {
                int i = _picked;
                _picked = -1;
                _onPick?.Invoke(i);
            }
        }

        static bool Ready(Animator a) => a != null && a.isActiveAndEnabled && a.runtimeAnimatorController != null;

        void FillCard(CardView v, CardData d)
        {
            if (v.panel != null)
            {
                var sp = d.weapon ? _yellowPanel : _bluePanel;
                if (sp != null) v.panel.sprite = sp;
            }
            if (v.title != null) v.title.text = d.title;
            if (v.newTag != null) v.newTag.SetActive(_useNewTag && d.level <= 0);
            if (v.icon != null)
            {
                v.icon.sprite = d.icon;
                v.icon.enabled = d.icon != null;
            }
            if (v.desc != null)
                v.desc.text = string.IsNullOrEmpty(d.hint) ? d.desc : $"{d.desc}\n<color=#FFC94A>{d.hint}</color>";

            // 별: 회색 칸 = 최대 레벨(9칸까지), 노란 별 = 고른 뒤 레벨. 두 줄 왼쪽 끝을 맞춰 가운데 정렬
            bool stars = v.stars != null && v.gray != null && v.light != null && d.maxLevel > 0 && d.maxLevel < 99;
            if (v.stars != null) v.stars.SetActive(stars);
            if (!stars) return;
            int slots = Mathf.Clamp(d.maxLevel, 1, v.gray.childCount);
            int lit = Mathf.Clamp(d.level + 1, 1, slots);
            float w = 0f;
            for (int k = 0; k < v.gray.childCount; k++)
            {
                var c = (RectTransform)v.gray.GetChild(k);
                bool on = k < slots;
                c.gameObject.SetActive(on);
                if (on) w += c.sizeDelta.x;
            }
            w += v.spacing * (slots - 1);
            for (int k = 0; k < v.light.childCount; k++) v.light.GetChild(k).gameObject.SetActive(k < lit);
            float x = -w * 0.5f;
            v.gray.anchoredPosition = new Vector2(x, v.gray.anchoredPosition.y);
            v.light.anchoredPosition = new Vector2(x, v.light.anchoredPosition.y);
        }

        static void FillSlots(List<SlotView> slots, IList<OwnedData> owned, bool fromEnd)
        {
            int n = owned != null ? Mathf.Min(owned.Count, slots.Count) : 0;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                int k = fromEnd ? i - (slots.Count - n) : i;   // 이 칸에 들어갈 보유 항목 번호
                bool has = k >= 0 && k < n && owned[k].icon != null;
                if (s.iconGo != null) s.iconGo.SetActive(has);
                if (s.levelGo != null) s.levelGo.SetActive(has);
                if (s.maxGo != null) s.maxGo.SetActive(false);
                if (!has) continue;
                if (s.icon != null) s.icon.sprite = owned[k].icon;
                if (s.level != null) s.level.text = owned[k].level.ToString();
            }
        }

        public void Hide()
        {
            _locked = false;
            _picked = -1;
            if (_rev != null) foreach (var r in _rev) r.phase = Done;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void Click(int i)
        {
            if (_locked || _picked >= 0) return;
            _locked = true;
            _picked = i; _pickAt = _clock; _pickEndSent = false;
            for (int k = 0; k < _cards.Length; k++)
            {
                var v = _cards[k];
                if (!v.go.activeInHierarchy || !Ready(v.press)) continue;
                v.press.SetTrigger(k == i ? "Pressed" : "Down");
            }
        }
    }
}
