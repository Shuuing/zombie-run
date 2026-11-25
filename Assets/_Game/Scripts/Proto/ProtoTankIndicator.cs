using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// 화면 밖 탱크 · 보스 방향 화살표 — <b>프로토 한정 · 임시 디자인</b> (사용자: "나중에 변경될 수 있다", 2026-10-08).
    ///
    /// <para>교체하기 쉽게 이 파일 하나로 끝난다: 자기 캔버스를 직접 만들고, 탱크는 <see cref="ProtoTank.Current"/>,
    /// 보스는 <see cref="ProtoSpawnDirector.Boss"/> 만 읽는다. 모양을 바꾸려면 <see cref="Build"/> 만, 규칙을 바꾸려면
    /// <see cref="LateUpdate"/> · <see cref="EdgePoint"/> 만 고치면 된다. 지우려면 이 파일과 <c>ProtoTankIndicator.Ensure()</c> 줄(ProtoTank · ProtoSpawnDirector).</para>
    ///
    /// 규칙: 탱크 — 있고 · 잔해가 아니고 · 타고 있지 않고 · 화면 밖일 때. 보스(중간 · 최종) — 살아 있고 화면 밖일 때(빨강).
    /// 화면 가장자리(위 · 옆 · 아래)에서 그쪽을 가리키고 거리(m)를 같이 띄운다.
    /// 11-17 사용자 "하차 버튼을 아래로 배치해주고 탱크나 보스 위치 화살표는 그 위쪽 사이드랑 아래로 표시해줘. 보스는 화살표 빨간색으로":
    ///  - 위는 HUD 띠(경험치 · 체력 · 시간 · 보스 막대) 밑까지만, 아래 두 구석(왼쪽 필사기 · 오른쪽 하차 버튼 자리)에 걸리면 그 버튼 위 옆 가장자리로 올린다.
    /// 11-18 사용자 "HUD 랑 안 겹치게 · 표적 사살은 다시 오른쪽": 피하는 칸 = 왼쪽 아래 개발 정보 상자 · 오른쪽 아래 필사기+하차 · 오른쪽 캐릭터 교체 창.
    /// </summary>
    public class ProtoTankIndicator : MonoBehaviour
    {
        // 캔버스 기준 단위(1080×1920, 가로세로 반반 — ProtoHUD · 탕탕 HUD 와 같은 기준)
        const float EdgeMargin = 90f;     // 옆 · 아래 가장자리에서 안쪽으로
        const float TopBand    = ProtoHUD.HudTopBand;   // 위는 HUD 띠 밑까지(경험치 · 일시정지 줄 · 체력 · 보스 막대)
        const float ArrowSize  = 84f;
        const float ZoneGap    = ArrowSize * 0.5f + 8f;   // 칸 가장자리에서 화살표 가운데까지
        // 11-18 칸 나누기(ProtoHUD 레이아웃 값): 왼쪽 아래 = 개발 정보 상자, 오른쪽 아래 = 필사기 + 그 위 하차, 오른쪽 가운데 = 캐릭터 교체 창
        static float CornerLW => ProtoHUD.LayoutPad + ProtoHUD.DevPanelSize.x + ZoneGap;
        static float CornerLH => ProtoHUD.LayoutPad + ProtoHUD.DevPanelSize.y + ZoneGap;
        const float CornerRW = 300f;                                  // 필사기 원(가운데 -154 · 빛 290) 왼쪽까지
        const float CornerRH = ProtoHUD.RightStackTop + ZoneGap;      // 하차 버튼 윗변 위까지
        const float LabelGap   = 82f;     // 화살표에서 화면 안쪽으로 라벨을 이만큼
        static readonly Color TankCol = new Color(1f, 0.78f, 0.22f, 0.95f);
        static readonly Color BossCol = new Color(1f, 0.25f, 0.25f, 0.98f);

        static ProtoTankIndicator _inst;

        class Marker
        {
            public RectTransform root, arrow, label;
            public Text text;
            public int shownDist = -1;
            public string name;
        }

        Canvas _canvas;
        Marker _tank, _boss;
        Texture2D _tex;
        Sprite _sprite;

        public static void Ensure()
        {
            if (_inst != null) return;
            _inst = new GameObject("_TankIndicator").AddComponent<ProtoTankIndicator>();
        }

        void Awake() => Build();

        void OnDestroy()
        {
            if (_inst == this) _inst = null;
            if (_tex != null) Destroy(_tex);
        }

        void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5;   // HUD(10) 아래 — 버튼·레벨업 카드를 가리지 않게
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);   // ProtoHUD 와 같은 기준
            scaler.matchWidthOrHeight = 0.5f;

            // 화살표 — 위를 가리키는 삼각형을 코드로 그린다(에셋 없음)
            _tex = BuildTriangle(64);
            _sprite = Sprite.Create(_tex, new Rect(0, 0, _tex.width, _tex.height), new Vector2(0.5f, 0.5f));
            _tank = MakeMarker("Tank", "TANK", TankCol);
            _boss = MakeMarker("Boss", "BOSS", BossCol);
        }

        Marker MakeMarker(string id, string name, Color col)
        {
            var m = new Marker { name = name };
            m.root = new GameObject("Indicator_" + id, typeof(RectTransform)).GetComponent<RectTransform>();
            m.root.SetParent(transform, false);

            m.arrow = new GameObject("Arrow", typeof(RectTransform)).GetComponent<RectTransform>();
            m.arrow.SetParent(m.root, false);
            m.arrow.sizeDelta = new Vector2(ArrowSize, ArrowSize);
            var img = m.arrow.gameObject.AddComponent<Image>();
            img.sprite = _sprite;
            img.color = col;
            img.raycastTarget = false;
            var sh = m.arrow.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
            sh.effectDistance = new Vector2(3f, -3f);

            m.label = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
            m.label.SetParent(m.root, false);
            m.label.sizeDelta = new Vector2(220f, 70f);
            m.text = m.label.gameObject.AddComponent<Text>();
            m.text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m.text.fontSize = 30;
            m.text.fontStyle = FontStyle.Bold;
            m.text.alignment = TextAnchor.MiddleCenter;
            m.text.color = col;
            m.text.raycastTarget = false;
            var ol = m.label.gameObject.AddComponent<ProtoTextOutline>();   // 8방향(기본 Outline 은 굵으면 계단이 진다)
            ol.effectColor = new Color(0f, 0f, 0f, 0.8f);
            ol.effectDistance = new Vector2(2f, -2f);

            m.root.gameObject.SetActive(false);
            return m;
        }

        static Texture2D BuildTriangle(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // 꼬리가 파인 화살촉(위를 가리킴). 정삼각형으로 그리면 120° 마다 모양이 같아서
                    // 방향이 안 읽힌다(실측: 239° 돌린 게 위를 가리키는 것처럼 보였다).
                    // 바깥 삼각형: 꼭짓점 (0.5, 0.95), 뒤 모서리 (0.18/0.82, 0.08)
                    // 파낸 삼각형: 꼭짓점 (0.5, 0.42), 같은 뒤 모서리 — 그 안은 비운다
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float du = Mathf.Abs(u - 0.5f);
                    float outer = (0.95f - v) / (0.95f - 0.08f) * 0.32f - du;            // > 0 이면 바깥 삼각형 안
                    float notch = du - (0.42f - v) / (0.42f - 0.08f) * 0.32f;            // > 0 이면 파낸 곳 밖
                    float a = Mathf.Clamp01(Mathf.Min(outer, notch, v - 0.08f) * n);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        void LateUpdate()
        {
            var run = ProtoRun.I;
            var cam = Camera.main;
            bool live = cam != null && run != null && run.IsRunning && run.player != null;

            var tank = ProtoTank.Current;
            bool tankOn = live && tank != null && !tank.Wrecked && !ProtoTank.PlayerRiding;
            Place(_tank, tankOn ? tank.transform : null, cam, run, 1f);

            var dir = ProtoSpawnDirector.I;
            var boss = dir != null ? dir.Boss : null;
            bool bossOn = live && boss != null && boss.Alive;
            Place(_boss, bossOn ? boss.transform : null, cam, run, 1.5f);
        }

        void Place(Marker mk, Transform target, Camera cam, ProtoRun run, float aimHeight)
        {
            bool want = target != null;
            Vector2 screen = Vector2.zero, center = Vector2.zero, dirv = Vector2.zero;
            Rect area = default;
            if (want)
            {
                Vector3 sp = cam.WorldToScreenPoint(target.position + Vector3.up * aimHeight);
                area = Screen.safeArea;
                center = area.center;
                screen = sp;
                if (sp.z < 0f) screen = center - (screen - center);   // 카메라 뒤 — 반대로 뒤집는다
                bool onScreen = sp.z > 0f && area.Contains(screen);
                want = !onScreen;
                dirv = screen - center;
                if (dirv.sqrMagnitude < 0.01f) want = false;
            }

            if (mk.root.gameObject.activeSelf != want) mk.root.gameObject.SetActive(want);
            if (!want) return;

            Vector2 n = dirv.normalized;
            Vector2 edge = EdgePoint(center, n, area, _canvas.scaleFactor);

            float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);
            mk.arrow.position = edge;
            mk.arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg - 90f);
            mk.arrow.localScale = Vector3.one * pulse;

            mk.label.position = edge - n * (LabelGap * _canvas.scaleFactor);
            float dist = Vector3.Distance(Flat(target.position), Flat(run.player.transform.position));
            int d = Mathf.RoundToInt(dist);
            if (d != mk.shownDist) { mk.shownDist = d; mk.text.text = $"{mk.name}\n{d}m"; }   // 바뀔 때만 문자열을 만든다
        }

        /// 화면 중심 → 대상 방향 반직선이 안쪽 사각형(위는 HUD 띠 밑, 옆 · 아래는 여백)과 만나는 점.
        /// 아래 두 구석(왼쪽 개발 정보 상자 · 오른쪽 필사기+하차)에 들어가면 그 칸 바로 옆(아래 가장자리) 또는 바로 위(옆 가장자리) 중
        /// 가까운 쪽으로, 오른쪽 캐릭터 교체 창에 걸리면 창 왼쪽 옆으로 비킨다.
        static Vector2 EdgePoint(Vector2 c, Vector2 n, Rect area, float s)
        {
            float left = area.xMin + EdgeMargin * s, right = area.xMax - EdgeMargin * s;
            float bottom = area.yMin + EdgeMargin * s, top = area.yMax - TopBand * s;
            if (c.y > top) c.y = top; if (c.y < bottom) c.y = bottom;   // (아주 납작한 화면 대비)
            float tx = n.x > 0.0001f ? (right - c.x) / n.x : n.x < -0.0001f ? (left - c.x) / n.x : float.MaxValue;
            float ty = n.y > 0.0001f ? (top - c.y) / n.y : n.y < -0.0001f ? (bottom - c.y) / n.y : float.MaxValue;
            Vector2 p = c + n * Mathf.Min(tx, ty);
            float lzR = area.xMin + CornerLW * s, lzT = area.yMin + CornerLH * s;
            float rzL = area.xMax - CornerRW * s, rzT = area.yMin + CornerRH * s;
            bool bottomFree = lzR < rzL;   // 아래 가장자리에 두 칸 사이 빈 곳이 있나(아주 좁은 화면이면 없다)
            if (p.x < lzR && p.y < lzT) p = Nearer(p, new Vector2(lzR, bottom), new Vector2(left, lzT), bottomFree);
            else if (p.x > rzL && p.y < rzT) p = Nearer(p, new Vector2(rzL, bottom), new Vector2(right, rzT), bottomFree);

            if (ProtoCharacterSwitcher.TryGetScreenRect(out var sw))
            {
                float m = ZoneGap * s;
                if (p.x > sw.xMin - m && p.x < sw.xMax + m && p.y > sw.yMin - m && p.y < sw.yMax + m)
                    p.x = sw.xMin - m;
            }
            return p;
        }

        static Vector2 Nearer(Vector2 p, Vector2 alongBottom, Vector2 alongSide, bool bottomOk)
            => bottomOk && (alongBottom - p).sqrMagnitude <= (alongSide - p).sqrMagnitude ? alongBottom : alongSide;

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
