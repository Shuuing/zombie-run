using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// 레벨업 3택 팝업이 갖춰야 할 것 — Survivor Ace 판(<see cref="ProtoSkillSelectPopup"/>)과 탕탕 판(<see cref="ProtoTtSkillSelectPopup"/>)이 같이 쓴다.
    public interface IProtoSkillPopup
    {
        bool IsOpen { get; }
        void Show(string title, string remain, IList<ProtoSkillSelectPopup.CardData> cards, IList<ProtoSkillSelectPopup.OwnedData> weapons,
                  IList<ProtoSkillSelectPopup.OwnedData> passives, int rerollsLeft, int rerollsMax, bool animate,
                  Action<int> onPick, Action onReroll, Action onSkip);
        void SetReelIcons(IList<Sprite> icons);
        void Hide();
    }

    /// <summary>
    /// 탕탕특공대 원작 레벨업 화면(SkillLearnUIPanel) 사본 <c>UI_TTSkillSelect</c> 을 띄우는 팝업 — <b>참고용 원작 그림, 출시 전 교체</b>.
    /// 게임 코드를 모른다(카드 값 · 콜백만 받는다). 연결은 <see cref="ProtoHUD"/>, 프리팹은 스킨 <c>Resources/Data/TtSkillSelectSkin</c>.
    ///
    /// <para>프리팹 이름: Root/CommonTitle/Text_Title · Root/LearnedGroup/OwnedWeapons|OwnedPassives/LayoutGroup/Slot0~5/Icon ·
    /// Root/Cards/Card_1~3(ButtonRoot · Image_BG_Weapon/Supply · Image_Icon · Text_Name · Text_Desc · HorizontalStar/Star0~4 · NewParent · EnhancedParent/Text_EnhDesc · Image_NotSelect) ·
    /// Text_Remain · ButtonRefresh(+RefreshCountText) · ButtonSkip.</para>
    /// 원작은 카드 연출을 애니메이터로 했지만 사본엔 코드가 없어 등장 · 선택 · 사라짐 · 새 별 깜빡임을 코드로 한다(시간 정지 중에도 돈다).
    ///
    /// <para>11-18 연출 2판(사용자 "등장이 어색 · 카드 오버스럽게 · 선택을 확실하게 보여주고 사라지게"):
    /// 등장 = 배경막 서서히 → 제목 띠 통통 튀며 → 카드가 아래 가운데에서 부채꼴로 날아와 뒤집히듯 펴지고 넘쳤다 자리 잡음 + 착지 섬광 · 빛 고리, 그 뒤 살짝 둥실.
    /// 선택 = 고른 카드 펑 커지며 가운데로 · 흰 섬광 · 금빛 테두리 · 뒤에서 도는 빛살, 나머지는 어두워지며 아래로 떨어져 사라짐 →
    /// 잠깐 보여 준 뒤 고른 카드가 커지며 흩어지고 제목 · 배경막이 걷힌다. 고른 결과(onPick)는 팝업이 다 사라진 뒤에 보낸다(<see cref="PickDone"/>).
    /// 예전 판 = <c>_Backup/20261118_SkillPopupAnim</c>.</para>
    /// </summary>
    public class ProtoTtSkillSelectPopup : MonoBehaviour, IProtoSkillPopup
    {
        class CardView
        {
            public GameObject go;
            public RectTransform rt;
            public Button button;
            public GameObject bgWeapon, bgSupply, newTag, hintRoot, stars, dim;
            public Image icon;
            public Text title, desc, hint;
            public GameObject[] starBg = new GameObject[5], starOn = new GameObject[5];
            public Image blink;   // 이번에 생길 별(깜빡임)
            // 설명 글 칸 기본 자리 · 크기와 진화 안내 줄 높이 — 안내가 없으면 설명 글이 그 자리까지 내려와 가운데에 선다(11-17 카드 다시 짜기)
            public Vector2 descPos, descSize;
            public float hintH;
            // 카드 색(11-17 "카드 12종 모두 다르게") — 바탕 그림은 회색조라 색을 곱해 머리 띠 · 몸통이 그 색이 된다. 기본 색은 프리팹 값
            public Image bgW, bgS, glow, descBg;
            public Color bgWCol, bgSCol, glowCol, descBgCol;
            // 11-18 연출 — 카드 통째 투명도, 원래 자리 · 그리는 순서, 코드가 붙인 섬광(카드 모양 흰 막) · 테두리 빛 · 빛살
            public CanvasGroup group;
            public Vector2 basePos;
            public int sibling;
            public Image fxFlash, fxGlow, fxRays;
            public Color ringCol;
            // 11-18 "사방으로 퍼지는 빛 더 화려하게" — 둘째 빛살(반대로 돎) · 별빛 폭발 · 반짝이(카드 둘레)
            public Image fxRays2, fxBurst;
            public Image[] fxSparks;
        }

        /// 고른 카드 뒤 빛 모양(11-18 비교용) — 0 = 1판(코드 빛살 한 겹),
        /// 1 = 방사광 + 별빛(Survivor Ace FangSheGuang 두 겹 · Ray_016_Hard), 2 = 회전 빛 + 방사광(탕탕 UIFX_RotateLight_White + SA FangSheGuang · Ray_016_Hard),
        /// 3 = 우리 그림 2판(코드 굵은 빛살 + 가는 빛살 + 별빛). 1 · 2 의 원작 그림은 <c>Resources/PickFx</c> 에서 읽고, 없으면 코드 그림으로 대신한다.
        /// 1~3 은 반짝이 8개가 카드 둘레에서 깜빡인다.
        /// <para>11-18 사용자 비교 뒤 "A로 해줘" → 기본 1. 그림 = <c>Sujin/S_UI/Resources/PickFx</c>(FangSheGuang · Ray_016_Hard, 원작 그림 — 출시 전에 3(우리 그림)으로 돌리거나 그림 교체).
        /// 비교 그림 <c>_Reference/TangTang/UI/Source~/preview/pick_fx_compare*</c>, 바꾸기 전 판 <c>_Backup/20261118_PickFxA</c>.</para>
        public static int PickFxStyle = 1;
        static readonly Vector2[] SparkPos =
        {
            new Vector2(-205f, 250f), new Vector2(195f, 290f), new Vector2(232f, 40f), new Vector2(-238f, -50f),
            new Vector2(-190f, -285f), new Vector2(208f, -250f), new Vector2(-20f, 335f), new Vector2(40f, -330f),
        };
        static readonly float[] SparkSize = { 180f, 140f, 200f, 160f, 190f, 150f, 120f, 170f };
        float _raySpin = 40f, _ray2Spin, _ray2Alpha;
        bool _burst, _sparks;

        CardView[] _cards;
        Image _backdrop; Color _backdropCol;
        RectTransform _titleRt; CanvasGroup _titleGroup; Vector2 _titleBase;
        readonly List<Image> _ownW = new List<Image>(), _ownP = new List<Image>();
        Text _title, _remain, _rerollCount;
        Button _reroll, _skip;
        CanvasGroup _rerollGroup;
        RectTransform _root, _canvasRect;
        Action<int> _onPick;
        Action _onReroll, _onSkip;
        bool _locked;
        int _picked = -1, _shown;
        float _clock, _openAt, _pickAt;
        bool _pickSent;

        // ── 연출 시간(초, 시간 정지 무시) ──
        // 등장: 배경막 BgIn 동안 → 제목 TitleAt 부터 TitleDur → 카드 i 는 CardAt + CardGap·i 부터 CardDur
        // 11-18 "조금 더 빠르게" — 전부 약 0.75배(등장 끝 0.94 → 0.68초, 고른 뒤 복귀 0.82 → 0.6초)
        const float BgIn = 0.15f, TitleAt = 0.02f, TitleDur = 0.45f, CardAt = 0.1f, CardGap = 0.08f, CardDur = 0.42f;
        const float DropFrom = 820f;            // 카드가 날아오기 시작하는 깊이(아래로, 판 단위)
        // 선택: 펑(PunchT) → 가운데로(MoveT) → 잠깐 보여 줌 → ExitAt 부터 ExitDur 동안 사라짐 → onPick
        const float PunchT = 0.08f, MoveT = 0.22f, ExitAt = 0.42f, ExitDur = 0.18f;
        const float PickSettle = 0.16f, PickFlash = 0.22f, OthersOut = 0.2f;   // 펑 뒤 1.12 로 돌아오는 시간 · 흰 섬광 · 나머지 카드 사라지는 시간
        const float PickDone = ExitAt + ExitDur;
        const float GlowPad = 48f;               // 테두리 빛이 카드 밖으로 번지는 폭
        static readonly Color PickGlow = new Color(1f, 0.86f, 0.32f, 1f), PickRays = new Color(1f, 0.93f, 0.62f, 1f);
        static readonly Color PickRays2 = new Color(1f, 0.97f, 0.82f, 1f), PickBurst = new Color(1f, 0.95f, 0.78f, 1f), PickSpark = new Color(1f, 0.97f, 0.8f, 1f);

        public bool IsOpen => gameObject.activeSelf;

        public static ProtoTtSkillSelectPopup Create(GameObject prefab, int sortingOrder)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab);
            go.name = "_SkillSelectPopupTT";
            var p = go.AddComponent<ProtoTtSkillSelectPopup>();
            if (!p.Init(sortingOrder)) { Destroy(go); return null; }
            go.SetActive(false);
            return p;
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (name.Contains("/")) return root.Find(name);
            var q = new Queue<Transform>(); q.Enqueue(root);
            while (q.Count > 0)
            {
                var t = q.Dequeue();
                if (t != root && t.name == name) return t;
                for (int i = 0; i < t.childCount; i++) q.Enqueue(t.GetChild(i));
            }
            return null;
        }
        static T Get<T>(Transform root, string name) where T : Component { var t = Find(root, name); return t != null ? t.GetComponent<T>() : null; }
        static GameObject Go(Transform root, string name) { var t = Find(root, name); return t != null ? t.gameObject : null; }

        bool Init(int order)
        {
            var c = GetComponent<Canvas>();
            if (c == null) return false;
            c.sortingOrder = order;
            _canvasRect = (RectTransform)transform;
            var t = transform;
            _root = t.Find("Root") as RectTransform;
            _backdrop = Get<Image>(t, "ModalBackground");
            if (_backdrop != null) _backdropCol = _backdrop.color;
            _titleRt = Find(t, "CommonTitle") as RectTransform;
            if (_titleRt != null)
            {
                _titleBase = _titleRt.anchoredPosition;
                _titleGroup = _titleRt.GetComponent<CanvasGroup>();
                if (_titleGroup == null) _titleGroup = _titleRt.gameObject.AddComponent<CanvasGroup>();
            }
            _title = Get<Text>(t, "Text_Title");
            _remain = Get<Text>(t, "Text_Remain");
            _rerollCount = Get<Text>(t, "RefreshCountText");
            _reroll = Get<Button>(t, "ButtonRefresh");
            _skip = Get<Button>(t, "ButtonSkip");
            if (_reroll != null)
            {
                _reroll.onClick.AddListener(() => { if (_locked) return; ProtoAudio.Play2D("ui_click"); _onReroll?.Invoke(); });
                _rerollGroup = _reroll.transform.parent.GetComponent<CanvasGroup>();
                if (_rerollGroup == null) _rerollGroup = _reroll.transform.parent.gameObject.AddComponent<CanvasGroup>();
            }
            if (_skip != null) _skip.onClick.AddListener(() => { if (_locked) return; ProtoAudio.Play2D("ui_click"); _onSkip?.Invoke(); });

            foreach (var (side, list) in new[] { ("OwnedWeapons", _ownW), ("OwnedPassives", _ownP) })
            {
                var lay = Find(Find(t, side), "LayoutGroup");
                if (lay == null) continue;
                for (int i = 0; i < lay.childCount; i++) list.Add(Get<Image>(lay.GetChild(i), "Icon"));
            }

            var cards = new List<CardView>();
            for (int i = 1; i <= 3; i++)
            {
                var card = Find(t, "Card_" + i);
                if (card == null) break;
                var v = new CardView { go = card.gameObject, rt = (RectTransform)card };
                v.button = Get<Button>(card, "ButtonRoot");
                v.bgWeapon = Go(card, "Image_BG_Weapon"); v.bgSupply = Go(card, "Image_BG_Supply");
                v.icon = Get<Image>(card, "Image_Icon");
                v.title = Get<Text>(card, "Text_Name");
                v.desc = Get<Text>(card, "Text_Desc");
                v.newTag = Go(card, "NewParent");
                v.hintRoot = Go(card, "EnhancedParent");
                v.hint = Get<Text>(card, "Text_EnhDesc");
                if (v.desc != null) { v.descPos = v.desc.rectTransform.anchoredPosition; v.descSize = v.desc.rectTransform.sizeDelta; }
                if (v.hintRoot != null) v.hintH = ((RectTransform)v.hintRoot.transform).sizeDelta.y;
                v.bgW = v.bgWeapon != null ? v.bgWeapon.GetComponent<Image>() : null;
                v.bgS = v.bgSupply != null ? v.bgSupply.GetComponent<Image>() : null;
                v.glow = Get<Image>(card, "IconGlow");
                if (v.bgW != null) v.bgWCol = v.bgW.color;
                if (v.bgS != null) v.bgSCol = v.bgS.color;
                if (v.glow != null) v.glowCol = v.glow.color;
                v.descBg = Get<Image>(card, "Image_DescBG");   // 설명 상자(회색조 CardDescBox 판이면 카드 색을 곱한다)
                if (v.descBg != null) v.descBgCol = v.descBg.color;
                v.stars = Go(card, "HorizontalStar");
                v.dim = Go(card, "Image_NotSelect");
                for (int s = 0; s < 5; s++)
                {
                    var st = Find(card, "Star" + s);
                    if (st == null) continue;
                    var bg = st.Find("ImageBG"); var on = st.Find("ImageIcon");
                    v.starBg[s] = bg != null ? bg.gameObject : null;
                    v.starOn[s] = on != null ? on.gameObject : null;
                }
                v.basePos = v.rt.anchoredPosition;
                v.sibling = card.GetSiblingIndex();
                v.group = card.GetComponent<CanvasGroup>();
                if (v.group == null) v.group = card.gameObject.AddComponent<CanvasGroup>();
                BuildFx(v, card);
                // 카드 그림이 다 터치를 받는다 — 눌림은 ButtonRoot 하나가 받는다(자식 그림 터치는 위로 올라간다)
                int k = i - 1;
                if (v.button != null) v.button.onClick.AddListener(() => Click(k));
                cards.Add(v);
            }
            _cards = cards.ToArray();
            return _cards.Length > 0;
        }

        /// 카드 내용 뿌리("root")에 연출용 그림 셋을 붙인다 — 뒤: 빛살 · 테두리 빛, 맨 앞: 섬광(카드 바탕과 같은 모양). 터치는 안 받는다.
        void BuildFx(CardView v, Transform card)
        {
            var host = Find(card, "root") as RectTransform;
            if (host == null) host = (RectTransform)card;
            var bg = v.bgW != null ? v.bgW : v.bgS;
            Vector2 size = bg != null ? bg.rectTransform.sizeDelta : new Vector2(350f, 540f);
            // 뒤에서부터: 둘째 빛살 · 빛살 · 별빛 폭발 · 테두리 빛 · (카드 바탕 · 내용) · 섬광 · 반짝이
            v.fxRays2 = FxImage(host, "FxRays2", RaySprite(), Image.Type.Simple, new Vector2(1250f, 1250f), 0);
            v.fxRays = FxImage(host, "FxRays", RaySprite(), Image.Type.Simple, new Vector2(1000f, 1000f), 1);
            v.fxBurst = FxImage(host, "FxBurst", StarSprite(), Image.Type.Simple, new Vector2(1350f, 1350f), 2);
            v.fxGlow = FxImage(host, "FxGlow", GlowSprite(), Image.Type.Sliced, size + Vector2.one * (GlowPad * 2f), 3);
            v.fxFlash = FxImage(host, "FxFlash", bg != null ? bg.sprite : null, bg != null ? bg.type : Image.Type.Simple, size, -1);
            if (bg != null) v.fxFlash.pixelsPerUnitMultiplier = bg.pixelsPerUnitMultiplier;
            v.fxSparks = new Image[SparkPos.Length];
            for (int s = 0; s < SparkPos.Length; s++)
            {
                v.fxSparks[s] = FxImage(host, "FxSpark" + s, SparkleSprite(), Image.Type.Simple, Vector2.one * SparkSize[s], -1);
                v.fxSparks[s].rectTransform.anchoredPosition = SparkPos[s];
            }
        }

        /// <see cref="PickFxStyle"/> 에 맞춰 빛살 그림 · 크기 · 도는 속도를 고른다(Show 때마다 — 비교 렌더가 판을 바꿀 수 있게).
        void ApplyPickStyle()
        {
            Sprite r1, r2 = null; float s1, s2 = 1250f;
            switch (PickFxStyle)
            {
                case 1: r1 = LoadFx("FangSheGuang", RaySprite); r2 = r1; s1 = 1150f; s2 = 820f; _raySpin = 25f; _ray2Spin = -40f; _ray2Alpha = 0.5f; break;
                case 2: r1 = LoadFx("UIFX_RotateLight_White", RaySpriteWide); r2 = LoadFx("FangSheGuang", RaySpriteThin); s1 = 1000f; s2 = 1300f; _raySpin = 30f; _ray2Spin = -18f; _ray2Alpha = 0.6f; break;
                case 3: r1 = RaySpriteWide(); r2 = RaySpriteThin(); s1 = 1050f; s2 = 1250f; _raySpin = 28f; _ray2Spin = -16f; _ray2Alpha = 0.65f; break;
                default: r1 = RaySprite(); s1 = 1000f; _raySpin = 40f; _ray2Spin = 0f; _ray2Alpha = 0f; break;
            }
            _burst = _sparks = PickFxStyle != 0;
            var burst = PickFxStyle == 3 ? StarSprite() : LoadFx("Ray_016_Hard", StarSprite);
            foreach (var v in _cards)
            {
                if (v.fxRays != null) { v.fxRays.sprite = r1; v.fxRays.rectTransform.sizeDelta = Vector2.one * s1; }
                if (v.fxRays2 != null && r2 != null) { v.fxRays2.sprite = r2; v.fxRays2.rectTransform.sizeDelta = Vector2.one * s2; }
                if (v.fxBurst != null) v.fxBurst.sprite = burst;
            }
        }

        static readonly Dictionary<string, Sprite> _fxCache = new Dictionary<string, Sprite>();
        /// 원작 빛 그림(Resources/PickFx/이름) — 스프라이트로 안 들어왔으면 텍스처로 만들고, 아예 없으면 코드 그림.
        static Sprite LoadFx(string name, Func<Sprite> fallback)
        {
            if (_fxCache.TryGetValue(name, out var s) && s != null) return s;
            s = Resources.Load<Sprite>("PickFx/" + name);
            if (s == null)
            {
                var tex = Resources.Load<Texture2D>("PickFx/" + name);
                if (tex != null) s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            if (s == null) return fallback();
            _fxCache[name] = s;
            return s;
        }

        static Image FxImage(RectTransform host, string name, Sprite spr, Image.Type type, Vector2 size, int sibling)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(host, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            if (sibling >= 0) rt.SetSiblingIndex(sibling); else rt.SetAsLastSibling();
            var im = go.GetComponent<Image>();
            im.sprite = spr; im.type = type; im.raycastTarget = false;
            im.color = new Color(1f, 1f, 1f, 0f);
            go.SetActive(false);
            return im;
        }

        // ── 코드로 그린 연출 그림(한 번 만들어 같이 쓴다) ─────────────────────
        static Sprite _glowSpr, _raySpr;

        /// 둥근 사각 테두리 빛(9-slice, 테두리 120) — 빛줄 = 가장자리에서 44px 안쪽 둥근 사각(모서리 반경 28),
        /// 밖으로 44px · 안으로 30px 동안 부드럽게 사라지고 가운데는 비었다(가운데 칸은 늘어나도 투명).
        /// 카드보다 GlowPad 만큼 크게 깔면 빛줄이 카드 가장자리 바로 밖에 오고, 키우면 속 빈 빛 고리로 퍼진다.
        /// 11-18 1판(128px · 반경 12 · 안쪽 12px)은 모서리가 각지고 고리 안이 검은 네모처럼 보여 바꿨다(GIF 확인).
        static Sprite GlowSprite()
        {
            if (_glowSpr != null) return _glowSpr;
            const int N = 256; const float B = 120f, Edge = 44f, R = 28f, Out = 44f, In = 30f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "PopupGlow" };
            var px = new Color32[N * N];
            float half = N * 0.5f, inner = half - Edge - R;   // 둥근 사각의 곧은 변 반폭(모서리 반경 뺀)
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - inner, qy = Mathf.Abs(y + 0.5f - half) - inner;
                    float sd = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - R;   // 밖 +, 안 -
                    float a = sd >= 0f ? Mathf.Pow(Mathf.Clamp01(1f - sd / Out), 2f) : Mathf.Pow(Mathf.Clamp01(1f + sd / In), 1.5f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _glowSpr = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(B, B, B, B));
            _glowSpr.name = "PopupGlow";
            return _glowSpr;
        }

        /// 빛살(14갈래) + 가운데 은은한 빛 — 고른 카드 뒤에서 돈다.
        static Sprite RaySprite()
        {
            if (_raySpr != null) return _raySpr;
            const int N = 256; const float Rays = 14f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "PopupRays" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ray = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(Mathf.Atan2(dy, dx) * Rays), 3f);
                    float fall = Mathf.Pow(Mathf.Clamp01(1f - r), 1.4f);
                    float glow = Mathf.Pow(Mathf.Clamp01(1f - r * 1.8f), 2f);
                    float a = Mathf.Clamp01(ray * fall + glow * 0.6f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _raySpr = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            _raySpr.name = "PopupRays";
            return _raySpr;
        }

        static Sprite _rayWide, _rayThin, _star, _sparkle;

        /// 코드 그림 공통 — 256 정사각, f(가로 -1~1, 세로 -1~1) = 알파.
        static Sprite MakeFx(string name, int n, Func<float, float, float> f)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float a = Mathf.Clamp01(f((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            s.name = name;
            return s;
        }

        /// 굵은 빛살 10갈래(끝이 넓은 부채꼴) + 가운데 빛 — 3안 앞 겹.
        static Sprite RaySpriteWide()
        {
            if (_rayWide != null) return _rayWide;
            return _rayWide = MakeFx("PopupRaysWide", 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                float ray = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(th * 10f), 1.6f);
                float wob = 0.75f + 0.25f * Mathf.Cos(th * 10f * 0.5f + 1.3f);   // 갈래마다 길이 다르게(긴 것 · 짧은 것 번갈아)
                float fall = Mathf.Pow(Mathf.Clamp01(1f - r / wob), 1.2f);
                return ray * fall + Mathf.Pow(Mathf.Clamp01(1f - r * 2f), 2f) * 0.7f;
            });
        }

        /// 가는 빛살 32갈래(바늘처럼) — 3안 뒷 겹, 반대로 돈다.
        static Sprite RaySpriteThin()
        {
            if (_rayThin != null) return _rayThin;
            return _rayThin = MakeFx("PopupRaysThin", 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                float ray = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(th * 32f), 14f);
                float len = 0.6f + 0.4f * (0.5f + 0.5f * Mathf.Sin(th * 7f + 0.5f)) ;   // 길이 들쭉날쭉
                float fall = Mathf.Pow(Mathf.Clamp01(1f - r / len), 0.9f) * Mathf.Clamp01(r * 6f);   // 가운데는 비움(앞 겹 빛이 채움)
                return ray * fall;
            });
        }

        /// 별빛 폭발 8갈래(길쭉한 4 + 짧은 4) + 밝은 심 — 고르는 순간 펑.
        static Sprite StarSprite()
        {
            if (_star != null) return _star;
            return _star = MakeFx("PopupStar", 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                float big = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(th * 4f), 40f) * Mathf.Pow(Mathf.Clamp01(1f - r), 1.3f);
                float small = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(th * 4f + Mathf.PI), 60f) * Mathf.Pow(Mathf.Clamp01(1f - r * 1.6f), 1.5f) * 0.8f;
                float core = Mathf.Exp(-r * r * 40f) + Mathf.Exp(-r * r * 6f) * 0.35f;
                return big + small + core;
            });
        }

        /// 반짝이(십자 별) — 카드 둘레에서 깜빡인다.
        static Sprite SparkleSprite()
        {
            if (_sparkle != null) return _sparkle;
            return _sparkle = MakeFx("PopupSparkle", 128, (u, v) =>
            {
                float au = Mathf.Abs(u), av = Mathf.Abs(v);
                float cross = Mathf.Pow(Mathf.Clamp01(1f - av), 2.2f) * Mathf.Exp(-au * 20f) + Mathf.Pow(Mathf.Clamp01(1f - au), 2.2f) * Mathf.Exp(-av * 20f);
                float diag = (Mathf.Exp(-Mathf.Abs(u - v) * 26f) + Mathf.Exp(-Mathf.Abs(u + v) * 26f)) * Mathf.Pow(Mathf.Clamp01(1f - (au + av)), 3f) * 0.45f;   // 짧은 대각 팔
                return cross + diag + Mathf.Exp(-(u * u + v * v) * 30f) * 0.9f;
            });
        }

        // ── 보이기 ────────────────────────────────────────────────────────
        public void Show(string title, string remain, IList<ProtoSkillSelectPopup.CardData> cards, IList<ProtoSkillSelectPopup.OwnedData> weapons,
                         IList<ProtoSkillSelectPopup.OwnedData> passives, int rerollsLeft, int rerollsMax, bool animate,
                         Action<int> onPick, Action onReroll, Action onSkip)
        {
            _onPick = onPick; _onReroll = onReroll; _onSkip = onSkip;
            bool wasOpen = gameObject.activeSelf;
            gameObject.SetActive(true);
            _picked = -1; _pickSent = false;
            if (_title != null) _title.text = string.IsNullOrEmpty(title) ? "스킬 선택" : title;   // 11-17 사용자 "그냥 레벨 표시만" — "7레벨"
            if (_remain != null) { bool on = !string.IsNullOrEmpty(remain); _remain.gameObject.SetActive(on); if (on) _remain.text = remain; }

            _shown = 0;
            for (int i = 0; i < _cards.Length; i++)
            {
                bool on = cards != null && i < cards.Count;
                _cards[i].go.SetActive(on);
                if (on) { Fill(_cards[i], cards[i]); _shown++; }
                _cards[i].go.transform.SetSiblingIndex(_cards[i].sibling);   // 지난번 고른 카드를 맨 앞으로 올렸던 것을 되돌린다
            }
            FillOwned(_ownW, weapons);
            FillOwned(_ownP, passives);
            if (_rerollCount != null) _rerollCount.text = $"남은 갱신 {rerollsLeft}/{rerollsMax}";
            if (_reroll != null) _reroll.interactable = rerollsLeft > 0;
            if (_rerollGroup != null) _rerollGroup.alpha = rerollsLeft > 0 ? 1f : 0.45f;

            Fit();
            ApplyPickStyle();
            if (animate || !wasOpen) { _openAt = _clock; _locked = true; }   // 카드가 다 내려앉을 때까지 잠금
            else { _openAt = _clock - 10f; _locked = false; }
            Pose();
        }

        void Fill(CardView v, ProtoSkillSelectPopup.CardData c)
        {
            if (v.title != null) v.title.text = c.maxLevel > 5 && c.maxLevel < 99 ? $"{c.title} Lv{c.level + 1}" : c.title;
            if (v.desc != null) v.desc.text = c.desc;
            if (v.bgWeapon != null) v.bgWeapon.SetActive(c.weapon);
            if (v.bgSupply != null) v.bgSupply.SetActive(!c.weapon);
            bool tint = c.color.a > 0.01f;
            var col = new Color(c.color.r, c.color.g, c.color.b, 1f);
            if (v.bgW != null) v.bgW.color = tint ? col : v.bgWCol;
            if (v.bgS != null) v.bgS.color = tint ? col : v.bgSCol;
            if (v.glow != null) v.glow.color = tint ? new Color(col.r, col.g, col.b, 0.32f) : v.glowCol;
            if (v.descBg != null && v.descBg.sprite != null && v.descBg.sprite.name == "CardDescBox") v.descBg.color = tint ? col : v.descBgCol;
            v.ringCol = Color.Lerp(tint ? col : (c.weapon ? v.bgWCol : v.bgSCol), Color.white, 0.45f);   // 착지 빛 고리 = 카드 색을 밝게
            if (v.icon != null) { v.icon.sprite = c.icon; v.icon.enabled = c.icon != null; }
            if (v.newTag != null) v.newTag.SetActive(c.level <= 0);
            bool hasHint = !string.IsNullOrEmpty(c.hint);
            if (v.hintRoot != null) v.hintRoot.SetActive(hasHint);
            if (hasHint && v.hint != null) v.hint.text = c.hint;
            if (v.desc != null && v.hintH > 0f && v.hintH < 60f)   // 안내 줄이 설명 상자 안에 있는 판(11-17 2판)만
            {
                float k = hasHint ? 0f : (v.hintH + 4f) * 0.5f;
                var dr = v.desc.rectTransform;
                dr.anchoredPosition = v.descPos - new Vector2(0f, k);
                dr.sizeDelta = v.descSize + new Vector2(0f, k * 2f);
            }
            // 별: 원작처럼 5칸(최대 레벨 5 이하만). 지금 레벨까지 노란 별, 고르면 생길 별은 깜빡인다
            bool stars = c.maxLevel > 0 && c.maxLevel <= 5;
            if (v.stars != null) v.stars.SetActive(stars);
            v.blink = null;
            for (int s = 0; s < 5; s++)
            {
                bool slot = stars && s < c.maxLevel;
                if (v.starBg[s] != null) v.starBg[s].SetActive(slot);
                if (v.starOn[s] == null) continue;
                bool lit = slot && s <= c.level;
                v.starOn[s].SetActive(lit);
                var img = v.starOn[s].GetComponent<Image>();
                if (img != null) img.color = Color.white;
                if (lit && s == c.level) v.blink = img;
            }
            if (v.dim != null) v.dim.SetActive(false);
            v.rt.localScale = Vector3.one;
        }

        static void FillOwned(List<Image> slots, IList<ProtoSkillSelectPopup.OwnedData> owned)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var im = slots[i];
                if (im == null) continue;
                bool has = owned != null && i < owned.Count && owned[i].icon != null;
                im.enabled = has;
                if (has) im.sprite = owned[i].icon;
            }
        }

        /// 가로 화면: 세로판(1080×1920)이 Expand 로 높이에 맞춰 작게 서므로 판을 조금 키운다(일시정지와 같은 규칙).
        void Fit()
        {
            if (_root == null) return;
            ProtoSaBattleUi.FitPortraitPanel(transform, _root);   // 캔버스 배율로 1.2배(글자를 제 크기로 — 11-17 "텍스트가 깨지는것같아")
        }

        public void SetReelIcons(IList<Sprite> icons) { }   // 탕탕 판엔 아이콘 슬롯 연출이 없다

        public void Hide()
        {
            _locked = false; _picked = -1;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void Click(int i)
        {
            if (_locked || _picked >= 0) return;
            ProtoAudio.Play2D("ui_click");
            _picked = i; _pickAt = _clock; _pickSent = false; _locked = true;
            for (int k = 0; k < _cards.Length; k++) if (k != i && _cards[k].dim != null) _cards[k].dim.SetActive(true);
            _cards[i].go.transform.SetAsLastSibling();   // 고른 카드를 맨 앞에 — 뒤 빛살이 옆 카드 위로 보이게
        }

        // ── 연출(시간 정지 중에도) ──────────────────────────────────────────
        void Update() { Tick(Time.unscaledDeltaTime); }

        /// 한 프레임 진행(Update 가 부른다 — 편집 모드 렌더 검사도 직접 부를 수 있게 공개).
        public void Tick(float dt)
        {
            _clock += dt;
            Pose();
            // 마지막 카드가 70% 내려앉으면 누를 수 있다
            if (_locked && _picked < 0 && _clock - _openAt >= CardAt + CardGap * Mathf.Max(0, _shown - 1) + CardDur * 0.7f) _locked = false;
            // 고른 결과는 팝업이 다 사라진 뒤에 보낸다 — 그동안 게임은 멈춰 있다(이어지는 레벨업이면 바로 새 카드가 다시 날아온다)
            if (_picked >= 0 && !_pickSent && _clock - _pickAt >= PickDone)
            {
                _pickSent = true;
                int p = _picked;
                _onPick?.Invoke(p);
            }
        }

        void Pose()
        {
            float t = _clock - _openAt;                              // 등장 뒤 흐른 시간
            float pt = _picked >= 0 ? _clock - _pickAt : -1f;        // 고른 뒤 흐른 시간
            float exit = pt < 0f ? 0f : Mathf.Clamp01((pt - ExitAt) / ExitDur);

            // 배경막: 서서히 어두워지고, 사라질 때 걷힌다
            if (_backdrop != null)
            {
                var c = _backdropCol;
                c.a *= OutQuad(Mathf.Clamp01(t / BgIn)) * (1f - OutQuad(exit));
                _backdrop.color = c;
            }
            // 제목 띠: 작게 시작해 통통 튀며(탄성) 자리 잡고, 사라질 때 위로 빠진다
            if (_titleRt != null)
            {
                float p = Mathf.Clamp01((t - TitleAt) / TitleDur);
                float s = Mathf.LerpUnclamped(0.3f, 1f, OutElastic(p));
                _titleRt.localScale = new Vector3(s, s, 1f);
                _titleRt.anchoredPosition = _titleBase + new Vector2(0f, 110f * OutCubic(exit));
                _titleGroup.alpha = Mathf.Clamp01((t - TitleAt) / 0.1f) * (1f - exit);
            }

            float mid = (_shown - 1) * 0.5f;
            for (int i = 0; i < _cards.Length; i++)
            {
                var v = _cards[i];
                if (!v.go.activeSelf) continue;
                float start = CardAt + CardGap * i;
                float p = Mathf.Clamp01((t - start) / CardDur);
                Vector2 off = Vector2.zero;
                float rz = 0f, ry = 0f, s = 1f, alpha = 1f;
                if (p < 1f)
                {
                    // 등장: 화면 아래 가운데에서 부채꼴로 날아와 자리를 넘쳤다 돌아오고, 옆으로 누운 카드가 뒤집히듯 펴진다
                    float mv = OutBack(p, 1.5f);
                    off = new Vector2(-v.basePos.x * (1f - mv), -DropFrom * (1f - mv));
                    rz = (mid - i) * 30f * (1f - OutBack(p, 2.2f));
                    ry = 85f * (1f - OutCubic(Mathf.Clamp01(p / 0.55f)));
                    s = Mathf.LerpUnclamped(0.45f, 1f, OutBack(p, 2.6f));
                    alpha = Mathf.Clamp01(p / 0.16f);
                }
                // 착지: 흰 섬광 + 카드 색 빛 고리가 퍼진다
                float flash = 0.75f * (p < 0.36f ? 0f : p < 0.46f ? (p - 0.36f) / 0.1f : Mathf.Clamp01(1f - (p - 0.46f) / 0.4f));
                float rp = Mathf.Clamp01((p - 0.42f) / 0.38f);
                float glowA = p > 0.42f ? 0.85f * (1f - rp) : 0f, glowS = 1f + 0.12f * OutCubic(rp);
                Color glowC = v.ringCol;
                float raysA = 0f, raysS = 1f, rays2A = 0f, burstA = 0f, burstS = 1f, sparkA = 0f, sparkIn = 0f;
                // 다 내려앉은 뒤엔 살짝 둥실(카드마다 박자 다르게)
                float idle = Mathf.Clamp01((t - start - CardDur) / 0.5f);
                off.y += idle * 5f * Mathf.Sin(_clock * 2.4f + i * 1.9f);

                if (_picked >= 0)
                {
                    if (i == _picked)
                    {
                        // 고른 카드: 펑(1.24) → 1.12, 가운데로 옮겨 가며 기울기를 거두고, 흰 섬광 · 금빛 테두리 · 뒤 빛살
                        float ps = pt < PunchT ? Mathf.Lerp(1f, 1.24f, OutQuad(pt / PunchT))
                                               : Mathf.Lerp(1.24f, 1.12f, OutCubic(Mathf.Clamp01((pt - PunchT) / PickSettle)));
                        float mvp = OutBack(Mathf.Clamp01(pt / MoveT), 1.3f);
                        off = Vector2.LerpUnclamped(off, new Vector2(-v.basePos.x, -10f), mvp);   // 1.12배여도 제목 띠와 안 닿게 살짝 아래
                        float k = 1f - Mathf.Clamp01(mvp);
                        rz *= k; ry *= k;
                        // 사라짐: 더 커지며 흩어진다
                        s = Mathf.Max(s, 1f) * ps * Mathf.Lerp(1f, 1.32f, OutCubic(exit));
                        flash = Mathf.Max(flash, 0.9f * (1f - Mathf.Clamp01(pt / PickFlash)));
                        glowA = Mathf.Clamp01(pt / 0.06f) * (0.8f + 0.2f * Mathf.Sin(_clock * 11f));
                        glowS = 1f + 0.03f * Mathf.Sin(_clock * 11f);
                        glowC = PickGlow;
                        raysA = 0.8f * Mathf.Clamp01(pt / 0.11f);
                        raysS = Mathf.LerpUnclamped(0.4f, 1f, OutBack(Mathf.Clamp01(pt / 0.24f), 1.6f)) * Mathf.Lerp(1f, 1.6f, OutCubic(exit));
                        rays2A = _ray2Alpha * Mathf.Clamp01((pt - 0.04f) / 0.14f);
                        if (_burst)
                        {
                            // 별빛 폭발: 펑 하고 1.15배까지 튀어나왔다가 은은하게(0.45) 남아 반짝인다
                            burstA = Mathf.Clamp01(pt / 0.03f) * Mathf.Lerp(1f, 0.45f, OutQuad(Mathf.Clamp01((pt - 0.06f) / 0.25f))) * (0.85f + 0.15f * Mathf.Sin(_clock * 17f));
                            burstS = Mathf.LerpUnclamped(0.25f, 1.15f, OutBack(Mathf.Clamp01(pt / 0.18f), 2f)) * Mathf.Lerp(1f, 1.8f, OutCubic(exit));
                        }
                        if (_sparks) { sparkA = Mathf.Clamp01((pt - 0.05f) / 0.1f); sparkIn = Mathf.Clamp01((pt - 0.05f) / 0.2f); }
                        alpha *= 1f - exit * exit;
                    }
                    else
                    {
                        // 나머지: 어두워지며 작아지고 아래로 떨어져 사라진다
                        float q = Mathf.Clamp01(pt / OthersOut);
                        s *= Mathf.Lerp(1f, 0.78f, OutQuad(q));
                        off.y -= 170f * q * q;
                        rz += (i < _picked ? 12f : -12f) * OutQuad(q);
                        alpha *= 1f - OutQuad(q);
                        glowA *= 1f - q;
                    }
                }
                v.rt.anchoredPosition = v.basePos + off;
                v.rt.localRotation = Quaternion.Euler(0f, ry, rz);
                v.rt.localScale = new Vector3(s, s, 1f);
                v.group.alpha = alpha;
                SetFx(v.fxFlash, Color.white, flash, 1f);
                SetFx(v.fxGlow, glowC, glowA, glowS);
                SetFx(v.fxRays, PickRays, raysA, raysS);
                if (raysA > 0f && v.fxRays != null) v.fxRays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -_clock * _raySpin);
                SetFx(v.fxRays2, PickRays2, rays2A, raysS);
                if (rays2A > 0f && v.fxRays2 != null) v.fxRays2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -_clock * _ray2Spin + 7f);
                SetFx(v.fxBurst, PickBurst, burstA, burstS);
                if (burstA > 0f && v.fxBurst != null) v.fxBurst.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _clock * 8f);
                if (v.fxSparks != null)
                    for (int sp = 0; sp < v.fxSparks.Length; sp++)
                    {
                        // 반짝이: 제각각 박자로 깜빡이며 조금씩 밖으로 퍼진다
                        float tw = 0.3f + 0.7f * Mathf.Abs(Mathf.Sin(_clock * (5f + sp * 0.7f) + sp * 1.3f));
                        SetFx(v.fxSparks[sp], PickSpark, sparkA * tw, (0.45f + 0.75f * tw) * Mathf.LerpUnclamped(0.3f, 1f, OutBack(sparkIn, 2f)));
                        if (sparkA > 0f)
                        {
                            var srt = v.fxSparks[sp].rectTransform;
                            srt.anchoredPosition = SparkPos[sp] * (1f + 0.12f * sparkIn + 0.35f * exit);
                            srt.localRotation = Quaternion.Euler(0f, 0f, _clock * 70f + sp * 23f);
                        }
                    }
                if (v.blink != null) { var c = v.blink.color; c.a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(_clock * 7f)); v.blink.color = c; }
            }
        }

        static void SetFx(Image im, Color c, float a, float scale)
        {
            if (im == null) return;
            bool on = a > 0.003f;
            if (im.gameObject.activeSelf != on) im.gameObject.SetActive(on);
            if (!on) return;
            c.a = a;
            im.color = c;
            im.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        // ── 움직임 곡선 ──
        static float OutQuad(float x) => 1f - (1f - x) * (1f - x);
        static float OutCubic(float x) { x = 1f - x; return 1f - x * x * x; }
        /// 넘쳤다 돌아옴 — s 가 클수록 많이 넘친다(1.7 ≈ 10%)
        static float OutBack(float x, float s) { x -= 1f; return x * x * ((s + 1f) * x + s) + 1f; }
        /// 통통 튐 — 1.35 → 0.88 → 1.04 → 1
        static float OutElastic(float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * x) * Mathf.Sin((x * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
        }
    }
}
