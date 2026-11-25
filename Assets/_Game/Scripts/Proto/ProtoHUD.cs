using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// 프로토타입 HUD — <b>uGUI</b>(2026-10-06, IMGUI 에서 옮김).
    ///
    /// <para><b>캔버스를 코드로 짓는다.</b> 씬·프리팹 배선 없이 Awake 에서 한 번 만들고, 매 프레임 값만 갱신한다.
    /// 그래서 Proto_Chapter01 · Proto_City_Test · Proto_Stream_Test 어디서나 같은 HUD 가 뜬다. 정식 HUD(§14)를 프리팹으로
    /// 만들 때는 이 파일의 배치 숫자가 출발점이다.</para>
    ///
    /// <para>왜 옮겼나: ① IMGUI 는 프레임마다 여러 번 돌며 GC 를 만든다 ② 픽셀 좌표라 노치·해상도 대응이 안 됐다
    /// (지금은 CanvasScaler 1080×1920 + Safe Area) ③ 버튼 터치가 이동 조이스틱 드래그로도 읽혔다
    /// (지금은 EventSystem 이 UI 위 터치를 걸러 ProtoPlayer 가 무시한다) ④ 카메라 캡처에 안 찍혔다.</para>
    ///
    /// <para>글자는 TMP 가 아니라 <c>UnityEngine.UI.Text</c> + 내장 폰트다 — 프로젝트에 한글 TMP 폰트 에셋이 없고,
    /// 내장 동적 폰트는 IMGUI 때처럼 OS 한글 글꼴로 대체돼 그려진다. 정식 폰트가 정해지면 TMP 로 바꾼다.</para>
    ///
    /// <para>디버그용 IMGUI 두 개(캐릭터 교체 패널, 카메라 각도 표시)는 그대로 IMGUI 다 — 게임 화면이 아니라 도구라서.</para>
    /// </summary>
    public class ProtoHUD : MonoBehaviour
    {
        public ProtoRun run;
        public ProtoPlayer player;
        public ProtoSpawnDirector director;

        // ── 색 ─────────────────────────────────────────────────────────
        static readonly Color BarBg     = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color XpCol     = new Color(0.95f, 0.45f, 0.20f);
        static readonly Color HpCol     = new Color(0.35f, 0.80f, 0.45f);
        static readonly Color HpLowCol  = new Color(0.90f, 0.30f, 0.25f);
        static readonly Color BossCol   = new Color(0.82f, 0.18f, 0.26f);
        static readonly Color Orange    = new Color(1f, 0.55f, 0.12f);
        static readonly Color PanelCol  = new Color(0.08f, 0.09f, 0.12f, 0.96f);
        static readonly Color BtnCol    = new Color(0.22f, 0.24f, 0.30f, 1f);
        static readonly Color CardCol   = new Color(0.16f, 0.18f, 0.24f, 0.98f);
        static readonly Color EliteCol  = new Color(0.85f, 0.62f, 0.15f);   // 엘리트 체력 바(진화 상자와 같은 금색 계열)

        Font _font;
        Sprite _disc, _ring, _rounded;
        float _fps;
        bool _optionsOpen;
        float _resultSince = -1f;   // 결과 화면이 뜬 실시간 (클리어 애니 시작점)
        float _nextDebug;
        RunState _lastState = (RunState)(-1);

        // ── 구성 요소 ──────────────────────────────────────────────────
        RectTransform _canvasRect, _safe;
        Rect _lastSafe;
        GameObject _hud, _levelUp, _result, _resultDead, _resultClear, _options, _skip;
        RectTransform _xpFill, _hpFill, _fuelFill, _bossFill;
        GameObject _fuelBar, _bossBar, _ultGo, _optBtn, _tankExitGo;
        Image _fuelImg, _ultDisc, _ultRing;
        Text _lv, _timer, _hp, _fuelText, _bossText, _debug, _ultText;
        Text _warnText;

        /// UI 캡처 중(F10) — 개발용 IMGUI 창(캐릭터 교체 · 카메라 각도 · 맵 스트리밍)이 이 값을 보고 숨는다.
        public static bool CaptureMode;

        // ── 화면 칸 나누기(캔버스 단위, 1080×1920 반반 — 탕탕 HUD · ProtoTankIndicator · 캐릭터 교체 창이 같이 쓴다) ──
        // 11-18 사용자 "HUD 랑 안 겹치게 가독성 있게 · 표적 사살은 다시 오른쪽 · 하차는 안 겹치게":
        //   위 띠 = HUD(경험치 · 일시정지 줄 · 체력 · 시간 · 처치 · 보스 막대) / 오른쪽 아래 = 필사기(가운데 -154,154 · 220 원) + 그 위 하차(-154,350 · 220×92)
        //   왼쪽 아래 = 개발 정보 상자(계측 수치 + 조작 키) / 오른쪽 가운데 = 캐릭터 교체 창(IMGUI, 임시)
        public const float LayoutPad = 24f;
        public const float HudTopBand = 260f;                            // 위 HUD 띠 높이
        public const float RightStackTop = 396f;                         // 오른쪽 아래 묶음 윗변(하차 버튼 위 = 350 + 46)
        public static readonly Vector2 DevPanelSize = new Vector2(560f, 280f);   // 왼쪽 아래 개발 정보 상자
        GameObject _hintGo;
        bool _capturing;
        Text _evoText;              // 무기 진화 진행도 (ProtoEvolution.HudLine)
        Image _bossFillImg;
        string _shownEvo;
        float _nextEvo;

        // ── 경고 문구 (몬스터 대량 습격 예고) ─────────────────────────────
        static readonly Color WarnCol = new Color(1f, 0.82f, 0.2f, 1f);
        static string _warnMsg;
        static float  _warnUntil;   // unscaledTime 기준 — 레벨업 정지 중에도 사라지지 않게 시간은 실제 시간으로

        /// 화면 위쪽에 경고 문구를 seconds 초 동안 깜빡이며 띄운다. 씬 배선 없이 어디서나 부른다.
        /// redEdge = 화면 가장자리 빨간 테두리도 켤지(11-17 사용자 "엘리트랑 중간보스, 마지막 보스일때만" — 그 출현 경고만 true).
        public static void ShowWarning(string msg, float seconds, bool redEdge = false)
        {
            _warnMsg = msg;
            _warnUntil = Time.unscaledTime + seconds;
            _warnEdge = redEdge;
        }
        static bool _warnEdge;
        int _shownLv = -1, _shownSec = -1, _shownHp = -1, _shownMaxHp = -1;

        // ── 버프 아이템 (ProtoPickups) — 먹은 순간 문구 + 지속 중인 버프 남은 시간 ──────
        Text _pickText, _buffText;
        static string _pickMsg;
        static Color  _pickCol = Color.white;
        static float  _pickAt = -99f;
        const float PickShowTime = 1.4f;
        string _shownBuff;

        /// 아이템을 먹은 순간 화면 위쪽 가운데에 잠깐 떠올랐다 사라지는 문구. 씬 배선 없이 어디서나 부른다.
        public static void ShowPickup(string msg, Color col)
        {
            _pickMsg = msg; _pickCol = col;
            _pickAt = Time.unscaledTime;
        }

        // 레벨업 — 원작 팝업(ProtoSkillSelectPopup, Resources/Data/SkillSelectSkin)이 있으면 그것, 없으면 아래 코드 카드
        IProtoSkillPopup _popup;   // Survivor Ace 판(ProtoSkillSelectPopup) 또는 탕탕 판(ProtoTtSkillSelectPopup) — 스킨 style
        ProtoSkillSelectSkin _popupSkin;
        string _popupSig;
        int _popupLevel = -1;
        readonly System.Collections.Generic.List<ProtoSkillSelectPopup.CardData> _popupCards = new System.Collections.Generic.List<ProtoSkillSelectPopup.CardData>();
        readonly System.Collections.Generic.List<ProtoSkillSelectPopup.OwnedData> _popupLeft = new System.Collections.Generic.List<ProtoSkillSelectPopup.OwnedData>();
        readonly System.Collections.Generic.List<ProtoSkillSelectPopup.OwnedData> _popupRight = new System.Collections.Generic.List<ProtoSkillSelectPopup.OwnedData>();
        Text _luTitle;
        readonly Button[] _cards = new Button[4];
        readonly Text[] _cardTitle = new Text[4], _cardDesc = new Text[4], _cardLv = new Text[4];
        Text _rerollText;
        RectTransform _cardRow, _luBox, _optPanel;

        // 전투 화면 UI — 원작 전투 UI 사본(ProtoSaBattleUi, Resources/Data/SaBattleUiSkin)이 있으면
        // 위 줄(경험치 · 시간 · 처치) · 체력 · 연료 · 보스/엘리트 바 · 경고 · 아이템 문구 · 일시정지 · 결과를 그것으로, 없으면 아래 코드 화면
        ProtoSaBattleUi _sa;
        ProtoSaBattleUiSkin _saSkin;
        bool _optFromPause;          // 옵션 창을 일시정지 화면의 설정 버튼으로 열었다 — 닫으면 일시정지로 돌아간다
        float _saWarnSeen = -1f, _saPickSeen = -99f;
        bool _saResultShown;
        readonly System.Collections.Generic.List<ProtoSaBattleUi.SkillItem> _saWeapons = new System.Collections.Generic.List<ProtoSaBattleUi.SkillItem>();
        readonly System.Collections.Generic.List<ProtoSaBattleUi.SkillItem> _saStats = new System.Collections.Generic.List<ProtoSaBattleUi.SkillItem>();

        // 결과
        Text _deadBody, _clearBody;
        RawImage _clearAnim;
        RectTransform _clearPopup;
        CanvasGroup _clearPopupGroup;
        GameObject _clearBtn;

        // 옵션
        Text _optFog, _optBurn, _optRain, _optFlame, _optDmg, _optSfxPct;
        Slider _sfx;

        ProtoShooter _shooter;
        ProtoShooter Shooter
        {
            get
            {
                if (_shooter == null && player != null) _shooter = player.GetComponent<ProtoShooter>();
                return _shooter;
            }
        }

        // ════════════════════════════════════════════════════════════════
        void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _disc = MakeCircleSprite(false);
            _ring = MakeCircleSprite(true);
            _rounded = MakeRoundedSprite();
            EnsureEventSystem();
            Build();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fps = Mathf.Lerp(_fps <= 0f ? 1f / dt : _fps, 1f / dt, 0.1f);

            if (run != null && run.state == RunState.Result && Input.GetKeyDown(KeyCode.R))
                run.Restart();

            if (run != null)
            {
                if (run.state == RunState.Result) { if (_resultSince < 0f) _resultSince = Time.unscaledTime; }
                else _resultSince = -1f;
                if (Input.GetKeyDown(KeyCode.U)) run.TryUltimate();     // 필사기 (§06 v0.7 — 캐릭터 고유)
                if (Input.GetKeyDown(KeyCode.T)) run.CycleUltTree();    // 프로토 임시: 필사기 트리 돌리기(로비 대신)
                if (Input.GetKeyDown(KeyCode.F7)) run.DebugFillUlt();   // 디버그: 필사기 게이지 가득
                if (Input.GetKeyDown(KeyCode.F8) && run.IsRunning)      // 디버그: 즉시 레벨업(레벨업 팝업 확인용)
                    run.AddXP(Mathf.Max(0.01f, run.xpNeeded - run.xp));
                if (Input.GetKeyDown(KeyCode.F3)) run.DebugAirstrike(); // 디버그: 융단 폭격
                if (Input.GetKeyDown(KeyCode.F2)) run.DebugClear();     // 디버그: 즉시 클리어
                if (Input.GetKeyDown(KeyCode.F4) && run.IsRunning && run.player != null)
                    ProtoHeliCrash.Play(run.player.transform.position); // 디버그: 헬기 추락
                if (Input.GetKeyDown(KeyCode.F5) && run.IsRunning && run.player != null)
                    ProtoEvolution.Evolve(run.player);                  // 디버그: 지금 무기 바로 진화(조건 무시)
                if (Input.GetKeyDown(KeyCode.F6)) run.DebugHeliStrike();  // 디버그: 헬리콥터 공습(기총 소사) 지금 바로
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (Input.GetKeyDown(KeyCode.F10) && !_capturing && run.IsRunning && _sa != null)
                    StartCoroutine(CaptureUiScreens());                  // 디버그: UI 화면들을 차례로 띄워 실제 게임 화면 캡처
#endif
                if (Input.GetKeyDown(KeyCode.E)) ProtoTank.RequestExit(); // 탱크 하차 (프로토 한정)
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.O))
            {
                if (_sa != null) TogglePause();
                else ToggleOptions();
            }
        }

        void LateUpdate()
        {
            if (run == null || player == null || _canvasRect == null) return;
            ApplySafeArea();

            if (_sa != null) RouteSaMessages();
            else if (_warnText != null)
            {
                bool on = Time.unscaledTime < _warnUntil && run.state != RunState.Result;
                if (_warnText.gameObject.activeSelf != on) _warnText.gameObject.SetActive(on);
                if (on)
                {
                    if (_warnText.text != _warnMsg) _warnText.text = _warnMsg;
                    // 깜빡임 — 4Hz 로 밝기만 흔든다(완전히 꺼지면 글을 다 읽기 전에 사라진다)
                    float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f));
                    _warnText.color = new Color(WarnCol.r, WarnCol.g, WarnCol.b, a);
                }
            }

            if (_pickText != null && _sa == null)
            {
                float k = (Time.unscaledTime - _pickAt) / PickShowTime;
                bool on = k >= 0f && k < 1f && run.state != RunState.Result;
                if (_pickText.gameObject.activeSelf != on) _pickText.gameObject.SetActive(on);
                if (on)
                {
                    if (_pickText.text != _pickMsg) _pickText.text = _pickMsg;
                    _pickText.color = new Color(_pickCol.r, _pickCol.g, _pickCol.b, 1f - k * k);
                    _pickText.rectTransform.anchoredPosition = new Vector2(0f, -24f - 190f + 40f * k);   // 살짝 떠오른다
                }
            }
            if (_buffText != null)
            {
                // 보이는 숫자(초 · 0.1초)가 바뀔 때만 문자열을 새로 만든다 — 예전엔 버프가 있는 동안 매 프레임(2026-11-20)
                int ka = player.IsAdrenaline ? Mathf.CeilToInt(player.AdrenalineLeft) : -1;
                int ks = player.IsShielded ? Mathf.CeilToInt(player.ShieldLeft) : -1;
                int kl = player.IsSlowed ? Mathf.FloorToInt(player.SlowLeft * 10f + 0.5f) : -1;
                if (_buffStr == null || ka != _buffKa || ks != _buffKs || kl != _buffKl)
                {
                    _buffKa = ka; _buffKs = ks; _buffKl = kl;
                    string s = "";
                    if (player.IsAdrenaline) s += $"아드레날린 {Mathf.CeilToInt(player.AdrenalineLeft)}";
                    if (player.IsShielded)   s += (s.Length > 0 ? "   " : "") + $"보호막 {Mathf.CeilToInt(player.ShieldLeft)}";
                    // 개에게 물린 감속 — 표시가 없어서 "이유 없이 느려진다"로 보였다(2026-10-21 사용자)
                    if (player.IsSlowed)     s += (s.Length > 0 ? "   " : "") + $"<color=#FF8A6A>물림 감속 {player.SlowLeft:0.0}</color>";
                    _buffStr = s;
                }
                string b = _buffStr;
                bool on = b.Length > 0 && run.state == RunState.Running;
                if (_sa != null && _sa.HasBuff) _sa.SetBuff(b, on);   // 원작 글꼴 판(같은 자리)
                else
                {
                    if (b != _shownBuff) { _shownBuff = b; _buffText.text = b; }
                    if (_buffText.gameObject.activeSelf != on) _buffText.gameObject.SetActive(on);
                }
            }

            var st = run.state;
            if (st != _lastState)
            {
                _lastState = st;
                _hud.SetActive(st != RunState.Intro);
                _skip.SetActive(st == RunState.Intro);
                _levelUp.SetActive(st == RunState.LevelUp && _popup == null);
                if (_popup != null && st != RunState.LevelUp) { _popup.Hide(); _popupSig = null; _popupLevel = -1; }
                _result.SetActive(st == RunState.Result && _sa == null);
                if (st != RunState.Running && _optionsOpen) { _optionsOpen = false; _optFromPause = false; _options.SetActive(false); }
                if (_sa != null)
                {
                    if (st != RunState.Running && _sa.PauseOpen) _sa.HidePause();
                    if (st != RunState.Running && _sa.SettingsOpen) _sa.HideSettings();
                    // 클리어면 결과 화면 전에 HUD 위에서 승리 띠가 먼저 돈다(TickSaResult) — 그동안 HUD 를 켜 둔다
                    bool victory = st == RunState.Result && run.survived && _sa.HasVictory;
                    _sa.SetHudVisible(st != RunState.Intro && (st != RunState.Result || victory));
                    if (st != RunState.Result) { _sa.HideResult(); _sa.HideVictory(); _saResultShown = false; _saVictoryShown = false; }
                }
            }

            if (st != RunState.Intro) TickHud();
            if (st == RunState.LevelUp) TickLevelUp();
            if (st == RunState.Result) { if (_sa != null) TickSaResult(); else TickResult(); }
        }

        // ════════════════════════════════════════════════════════════════
        // 매 프레임 갱신
        // ════════════════════════════════════════════════════════════════
        void TickHud()
        {
            if (_sa != null) TickSa();
            else TickOldBars();

            // 무기 진화 진행도 — 초당 5회만 다시 만든다(문자열 할당)
            if (Time.unscaledTime >= _nextEvo)
            {
                _nextEvo = Time.unscaledTime + 0.2f;
                string evo = ProtoEvolution.HudLine(run);
                if (_sa != null && _sa.HasEvo) _sa.SetEvo(evo);   // 원작 그림체 판(같은 자리, 반투명 둥근 칸)
                else
                {
                    if (evo != _shownEvo) { _shownEvo = evo; _evoText.text = evo ?? ""; }
                    bool evoOn = evo != null;
                    if (_evoText.gameObject.activeSelf != evoOn) _evoText.gameObject.SetActive(evoOn);
                }
            }

            // 디버그 계측 — 초당 4회만 다시 쓴다(문자열 할당)
            if (Time.unscaledTime >= _nextDebug)
            {
                _nextDebug = Time.unscaledTime + 0.25f;
                // 개발 정보 상자 위쪽 4줄(11-18 6줄 → 4줄, 상자 높이를 줄이려고 짧은 항목을 한 줄에)
                _debug.text = $"KILL {run.kills}     적 {(director != null ? director.aliveCount : 0)} / 예산 {(director != null ? director.budget : 0)}     FPS {_fps:F0}\n" +
                              $"압력 {(director != null ? director.pressure : 0f):F2}     분리쌍 {(director != null ? director.separationPairChecks : 0)}\n" +
                              $"공격력 ×{player.damageMult:F2}     공속 ×{player.atkSpeedMult:F2}\n" +
                              $"이동 {player.MoveSpeed:F2} m/s     픽업 {player.PickupRange:F1}m     크리 {player.critChance * 100f:F0}%";
            }

            TickButtons();
        }

        /// 예전 코드 HUD 의 위 줄 · 체력 · 연료 · 보스 바(원작 UI 사본이 없을 때만).
        void TickOldBars()
        {
            SetFill(_xpFill, run.xpNeeded > 0 ? run.xp / run.xpNeeded : 0f);
            if (_shownLv != run.level) { _shownLv = run.level; _lv.text = $"Lv {run.level}"; }

            // 카운트업(§14) — 시간이 다 돼도 끝나지 않으므로(최종 보스 처치 = 클리어) 남은 시간을 보여주면 거짓말이 된다
            int rem = Mathf.FloorToInt(run.Elapsed);
            if (_shownSec != rem) { _shownSec = rem; _timer.text = $"{rem / 60:00}:{rem % 60:00}"; }

            float hpT = player.maxHP > 0 ? player.hp / player.maxHP : 0f;
            SetFill(_hpFill, hpT);
            _hpFill.GetComponent<Image>().color = hpT > 0.3f ? HpCol : HpLowCol;
            int hp = Mathf.CeilToInt(player.hp), mhp = Mathf.CeilToInt(player.maxHP);
            if (hp != _shownHp || mhp != _shownMaxHp) { _shownHp = hp; _shownMaxHp = mhp; _hp.text = $"HP {hp} / {mhp}"; }

            // 화염 연료 — 완전 자동 사격이라 언제 물러나야 하는지를 이 게이지 하나로 읽는다
            var sh = Shooter;
            bool fuel = sh != null && sh.UsesFuel;
            if (_fuelBar.activeSelf != fuel) _fuelBar.SetActive(fuel);
            if (fuel)
            {
                bool refuel = sh.IsRefueling;
                float ft = sh.FuelNormalized;
                SetFill(_fuelFill, ft);
                _fuelImg.color = refuel ? new Color(0.45f, 0.55f, 0.85f)
                               : (ft > 0.3f ? new Color(1.00f, 0.62f, 0.16f) : new Color(0.95f, 0.30f, 0.20f));
                _fuelText.text = refuel ? $"재충전 {sh.RefuelRemaining:F1}s" : "연료";
            }

            // 보스 체력 (등장 중에만). 보스가 없으면 엘리트를 같은 바에 금색으로
            var boss = director != null ? director.Boss : null;
            bool isElite = false;
            if ((boss == null || !boss.Alive) && director != null && director.Elite != null && director.Elite.Alive)
            { boss = director.Elite; isElite = true; }
            bool showBoss = boss != null && boss.Alive;
            if (_bossBar.activeSelf != showBoss) _bossBar.SetActive(showBoss);
            if (showBoss)
            {
                SetFill(_bossFill, boss.maxHP > 0f ? boss.hp / boss.maxHP : 0f);
                _bossFillImg.color = isElite ? EliteCol : BossCol;
                float eta = isElite ? director.FinalBossEta : -1f;   // 최종 보스가 나오면 엘리트는 상자 없이 도망간다
                float fleeShowAt = EnemyTable.I != null ? EnemyTable.I.eliteFleeShowAt : 60f;
                string label = isElite ? (eta >= 0f && eta <= fleeShowAt
                                            ? $"엘리트 · 진화 상자 · {Mathf.CeilToInt(eta)}초 뒤 도망" : "엘리트 · 진화 상자")
                             : director.FinalBossActive ? "최종 보스" : "중간 보스";
                _bossText.text = $"{label}   {Mathf.CeilToInt(boss.hp)} / {Mathf.CeilToInt(boss.maxHP)}";
            }
        }

        void TickButtons()
        {
            // 필사기 버튼 — 쿨타임 링(§14 'H · 액티브 버튼')
            bool running = run.state == RunState.Running;
            bool saUlt = _sa != null && _sa.HasUltButton;   // 원작 그림 필사기 버튼(UI_ZombieHUD 2판)이 있으면 예전 코드 버튼은 끈다
            bool oldUltOn = running && !saUlt;
            if (_ultGo.activeSelf != oldUltOn) _ultGo.SetActive(oldUltOn);
            if (saUlt) TickSaUlt(running);
            bool optOn = running && _sa == null;   // 원작 UI 사본이 있으면 왼쪽 위 일시정지 버튼이 대신한다
            if (_optBtn.activeSelf != optOn) _optBtn.SetActive(optOn);
            if (_sa != null) _sa.SetPauseButtonVisible(running);
            bool riding = running && ProtoTank.PlayerRiding;
            bool saTank = _sa != null && _sa.HasTankExit;   // 원작 노란 버튼 판(같은 자리)
            if (_tankExitGo.activeSelf != (riding && !saTank)) _tankExitGo.SetActive(riding && !saTank);
            if (saTank) _sa.SetTankExit(riding);
            if (running && !saUlt)
            {
                // 링 = 처치 게이지(§06 v0.7). 다 차면 버튼이 빛난다. 재사용 하한 중에는 남은 초를 보여 준다.
                var kind = run.UltKind;
                bool ready = run.UltReady;
                bool none = kind == ProtoUltimate.Kind.None;
                float pulse = ready ? 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
                _ultDisc.color = ready ? new Color(1f, 0.62f * pulse, 0.18f, 0.95f) : new Color(0.25f, 0.26f, 0.30f, none ? 0.5f : 0.85f);
                _ultRing.fillAmount = none ? 0f : run.ultCharge;
                _ultRing.color = ready ? new Color(1f, 0.92f, 0.6f) : Orange;
                string name = ProtoUltimate.NameOf(run.player);
                _ultText.text = none ? name
                              : ProtoUltimate.Active ? $"{name}\n{Mathf.CeilToInt(ProtoUltimate.Remaining)}s"
                              : ready ? name
                              : run.UltLocked ? $"{name}\n{Mathf.CeilToInt(run.UltLockRemaining)}s"
                              : $"{name}\n{Mathf.FloorToInt(run.ultCharge * 100f)}%";
            }
        }

        float _ultActiveTotal;   // 필사기를 쓴 순간의 남은 시간 — 쓰는 중 링을 남은 비율로 줄이는 데 쓴다

        /// 원작 그림 필사기 버튼 — 예전 코드 버튼과 같은 규칙(링 = 처치 게이지, 다 차면 빛남, 재사용 하한 중 남은 초).
        /// 쓰는 중에는 링이 남은 지속 시간만큼 하늘색으로 줄어든다.
        void TickSaUlt(bool running)
        {
            if (!running) { _sa.SetUlt(false, ProtoSaBattleUi.UltState.None, 0f, null, null); return; }
            var kind = run.UltKind;
            bool none = kind == ProtoUltimate.Kind.None;
            string name = ProtoUltimate.NameOf(run.player);
            bool active = ProtoUltimate.Active;
            if (active) { if (_ultActiveTotal <= 0f) _ultActiveTotal = Mathf.Max(0.01f, ProtoUltimate.Remaining); }
            else _ultActiveTotal = 0f;

            if (none) _sa.SetUlt(true, ProtoSaBattleUi.UltState.None, 0f, name, null);
            else if (active) _sa.SetUlt(true, ProtoSaBattleUi.UltState.Active, ProtoUltimate.Remaining / _ultActiveTotal, name, UltSub(1, Mathf.CeilToInt(ProtoUltimate.Remaining), "s"));
            else if (run.UltReady) _sa.SetUlt(true, ProtoSaBattleUi.UltState.Ready, 1f, name, "준비");
            else if (run.UltLocked) _sa.SetUlt(true, ProtoSaBattleUi.UltState.Locked, run.ultCharge, name, UltSub(2, Mathf.CeilToInt(run.UltLockRemaining), "s"));
            else _sa.SetUlt(true, ProtoSaBattleUi.UltState.Charging, run.ultCharge, name, UltSub(3, Mathf.FloorToInt(run.ultCharge * 100f), "%"));
        }

        // 필사기 버튼 아래 숫자 — 숫자가 바뀔 때만 새 문자열(2026-11-20: 예전엔 판 내내 매 프레임 만들었다)
        int _ultSubKind = -1, _ultSubVal = int.MinValue;
        string _ultSubStr;
        string UltSub(int kind, int v, string suffix)
        {
            if (kind != _ultSubKind || v != _ultSubVal || _ultSubStr == null) { _ultSubKind = kind; _ultSubVal = v; _ultSubStr = v + suffix; }
            return _ultSubStr;
        }

        int _buffKa = -2, _buffKs = -2, _buffKl = -2;
        string _buffStr;

        // ── 레벨업 팝업(원작 UI_DankeSkillSelect 사본) ─────────────────────
        void BuildPopup()
        {
            _popupSkin = ProtoSkillSelectSkin.Load();
            if (_popupSkin == null || !_popupSkin.useThisPopup || _popupSkin.popupPrefab == null) return;
            // HUD 캔버스(10) · 탱크 표시(5) 위. 결과 화면이 뜨면 상태가 바뀌어 닫힌다
            if (_popupSkin.style == ProtoSkillSelectSkin.Style.TangTang) _popup = ProtoTtSkillSelectPopup.Create(_popupSkin.popupPrefab, 20);
            else _popup = ProtoSkillSelectPopup.Create(_popupSkin.popupPrefab, 20, _popupSkin.bluePanel, _popupSkin.yellowPanel);
            // 아이콘을 켜 두면 원작처럼 카드 등장 때 아이콘 슬롯이 돈다 — 돌릴 그림 = 스킨의 아이콘들
            if (_popup != null && _popupSkin.showIcons)
            {
                var pool = new System.Collections.Generic.List<Sprite>();
                foreach (var e in _popupSkin.entries) if (e != null && e.icon != null) pool.Add(e.icon);
                _popup.SetReelIcons(pool);
            }
        }

        /// 강화 항목 id → 스킨 항목 id. 보조 장비 카드는 에셋 이름이 붙은 id(gear_&lt;이름&gt;)라 슬롯 종류로 바꾼다.
        string PopupKey(string id)
        {
            if (id == null || !id.StartsWith("gear_")) return id;
            string n = id.Substring(5);
            if (run.gearOrbitBlade != null && n == run.gearOrbitBlade.name) return "gear_orbit";
            if (run.gearSupportDrone != null && n == run.gearSupportDrone.name) return "gear_drone";
            if (run.gearGrenade != null && n == run.gearGrenade.name) return "gear_grenade";
            if (run.gearHeli != null && n == run.gearHeli.name) return "gear_heli";
            return id;
        }

        void TickPopup()
        {
            // 카드가 바뀌었을 때만 다시 채운다 — 새로고침, 또는 레벨이 밀려 바로 다음 레벨업(상태는 LevelUp 그대로)
            var sb = new System.Text.StringBuilder();
            sb.Append(run.level).Append('|').Append(run.rerollsLeft);
            foreach (var u in run.currentCards) sb.Append('|').Append(u.id);
            string sig = sb.ToString();
            if (sig == _popupSig) return;
            _popupSig = sig;
            bool animate = run.level != _popupLevel;
            _popupLevel = run.level;

            _popupCards.Clear();
            foreach (var u in run.currentCards)
            {
                var e = _popupSkin.Find(PopupKey(u.id));
                int lv = run.upgradeLevels.TryGetValue(u.id, out var l) ? l : 0;
                _popupCards.Add(new ProtoSkillSelectPopup.CardData
                {
                    title = u.title, desc = u.desc, hint = ProtoEvolution.HintFor(run, u.id),
                    level = lv, maxLevel = u.maxLevel,
                    weapon = e != null ? e.weapon : u.gear != null,
                    icon = e != null && _popupSkin.showIcons ? e.icon : null,   // 스킨에서 아이콘을 끄면 카드 안 그림 없음
                    color = e != null ? e.color : default,                     // 카드 색(탕탕 판) — 스킨 항목마다
                });
            }
            // 보유 줄: 무기(주무기 강화 · 보조 장비) 왼쪽, 능력치 오른쪽. 응급 처치(즉시 회복)는 보유가 아니라 뺀다
            _popupLeft.Clear(); _popupRight.Clear();
            foreach (var kv in run.upgradeLevels)
            {
                if (kv.Value <= 0 || kv.Key == "heal") continue;
                var e = _popupSkin.Find(PopupKey(kv.Key));
                if (e == null || e.icon == null || !_popupSkin.showIcons) continue;
                var o = new ProtoSkillSelectPopup.OwnedData { icon = e.icon, level = kv.Value };
                (e.weapon ? _popupLeft : _popupRight).Add(o);
            }
            // 원작(Survivor Ace) 팝업의 "선택 기회가 n회 남았습니다!"(바로 이어질 레벨업 수)는 우리 기획에 없어 뺐다(11-17 사용자 "문구 빼기").
            // 팝업은 remain 이 비면 그 글자를 숨긴다. 레벨업이 이어지는 동작 자체는 그대로(카드를 고르면 다음 레벨업 카드가 바로 뜬다).
            string remain = null;

            // 제목 띠엔 레벨만(11-13 사용자 요청) — 11-17 "레벨 7레벨로 적어주고" · "그냥 레벨 표시만" → "7레벨"
            _popup.Show($"{run.level}레벨", remain, _popupCards, _popupLeft, _popupRight, run.rerollsLeft, 1, animate,
                        i => { if (run != null && run.state == RunState.LevelUp) run.PickCard(i); },
                        () => { if (run != null && run.state == RunState.LevelUp && run.rerollsLeft > 0) run.Reroll(); },
                        () => { if (run != null && run.state == RunState.LevelUp) run.SkipCards(); });
        }

        void TickLevelUp()
        {
            if (_popup != null) { TickPopup(); return; }
            _luTitle.text = $"LEVEL {run.level}";
            int n = Mathf.Min(run.currentCards.Count, _cards.Length);
            for (int i = 0; i < _cards.Length; i++)
            {
                bool on = i < n;
                if (_cards[i].gameObject.activeSelf != on) _cards[i].gameObject.SetActive(on);
                if (!on) continue;
                var u = run.currentCards[i];
                int lv = run.upgradeLevels.TryGetValue(u.id, out var l) ? l : 0;
                _cardTitle[i].text = u.title;
                // 주무기 강화 카드에는 진화 안내(Lv5 + 보스 상자 → 무기)를 붙여 무엇을 모으는지 보이게 한다(진화 기획)
                string hint = ProtoEvolution.HintFor(run, u.id);
                _cardDesc[i].text = hint != null ? $"{u.desc}\n<color=#FFC94A>{hint}</color>" : u.desc;
                _cardLv[i].text = u.maxLevel >= 99 ? "" : $"Lv {lv} → {lv + 1}\n(최대 {u.maxLevel})";
                // 진화 재료 카드(주무기 강화)는 테두리만 금색 — 무엇을 모으는지 한눈에
                var ol = _cards[i].GetComponent<Outline>();
                if (ol != null) ol.effectColor = hint != null ? ProtoEvolution.Gold : Orange;
            }
            FitBox(_luBox);
            // 카드 폭: 상자(1040) 안에 한 줄로
            float w = _luBox.rect.width;
            float cw = Mathf.Min(300f, (w - 60f) / Mathf.Max(1, n) - 20f);
            for (int i = 0; i < n; i++)
            {
                var rt = (RectTransform)_cards[i].transform;
                rt.sizeDelta = new Vector2(cw, 460f);
                rt.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * (cw + 20f), 0f);
            }
            _rerollText.text = run.rerollsLeft > 0 ? $"다시 뽑기 ({run.rerollsLeft})" : "다시 뽑기 (소진)";
        }

        void TickResult()
        {
            int el = Mathf.FloorToInt(run.elapsed);
            string body = $"생존 시간   {el / 60:00}:{el % 60:00}\n처치        {run.kills}\n레벨        {run.level}";
            bool clear = run.survived;
            if (_resultClear.activeSelf != clear) _resultClear.SetActive(clear);
            if (_resultDead.activeSelf == clear) _resultDead.SetActive(!clear);

            if (!clear) { _deadBody.text = body; return; }

            if (_resultSince < 0f) _resultSince = Time.unscaledTime;
            _clearBody.text = body;

            // 캐릭터 클리어 애니 — 화면 높이 꽉, 발밑 기준으로 커진다
            float H = _canvasRect.rect.height;
            if (ProtoSpritePopup.TryGetClear(_resultSince, out int frame, out float pop, out float alpha))
            {
                var s = ProtoSpritePopup.Clear;
                _clearAnim.enabled = true;
                _clearAnim.texture = s.tex;
                _clearAnim.uvRect = s.UV(frame);
                float ph = H * pop;
                ((RectTransform)_clearAnim.transform).sizeDelta = new Vector2(ph * s.Aspect, ph);
                _clearAnim.color = new Color(1f, 1f, 1f, alpha);
            }
            else _clearAnim.enabled = false;

            // 팝업은 캐릭터가 한 번 튄 뒤(0.35s) 아래에서 올라온다 — 얼굴을 가리지 않게 화면 아래쪽
            float pt = Mathf.Clamp01((Time.unscaledTime - _resultSince - 0.35f) / 0.25f);
            float ease = 1f - Mathf.Pow(1f - pt, 3f);
            _clearPopup.anchoredPosition = new Vector2(0f, H * 0.04f - (1f - ease) * 60f);
            _clearPopupGroup.alpha = ease;
            if (_clearBtn.activeSelf != (pt >= 1f)) _clearBtn.SetActive(pt >= 1f);
        }

        // ════════════════════════════════════════════════════════════════
        // 전투 화면 UI(원작 전투 UI 사본 — ProtoSaBattleUi)
        // ════════════════════════════════════════════════════════════════
        void BuildSa()
        {
            _saSkin = ProtoSaBattleUiSkin.Load();
            if (_saSkin == null || !_saSkin.useThisUi || _saSkin.hudPrefab == null) return;
            _sa = ProtoSaBattleUi.Create(_saSkin.hudPrefab, _saSkin.pausePrefab, _saSkin.resultPrefab, TogglePause,
                                         () => { if (run != null) run.TryUltimate(); }, _saSkin.settingsPrefab);
            if (_sa == null) return;
            _sa.SetTankExitAction(ProtoTank.RequestExit);
            _sa.FeetOffset = _saSkin.feetBarOffset;   // 발밑 체력 바(탕탕 판 HUD 에만 있다)
            if (_sa.HasEvo) _evoText.gameObject.SetActive(false);
            if (_sa.HasBuff) _buffText.gameObject.SetActive(false);
            // 설정 창(원작 틀) — 예전 옵션 창과 같은 여섯 줄 · 같은 저장 값
            if (_sa.HasSettings)
            {
                _sa.BindToggle("fog", () => ProtoSettings.FogEnabled, () => ProtoSettings.FogEnabled = !ProtoSettings.FogEnabled);
                _sa.BindSlider("sfx", () => ProtoSettings.SfxVolume, v => ProtoSettings.SfxVolume = v, () => ProtoAudio.Play2D("ui_click"));
                _sa.BindToggle("burn", () => ProtoSettings.BurningBodies, () => ProtoSettings.BurningBodies = !ProtoSettings.BurningBodies);
                _sa.BindCycle("rain", () => ProtoSettings.RainLevelName, () => ProtoSettings.RainLevel = (ProtoSettings.RainLevel + 1) % 3);
                _sa.BindCycle("flame", () => ProtoSettings.FlameStyleName, () => ProtoSettings.FlameStyle = 1 - ProtoSettings.FlameStyle);
                _sa.BindCycle("dmg", () => ProtoSettings.DamageNumbersName, () => { ProtoSettings.CycleDamageNumbers(); ProtoDamageText.Clear(); });
            }
            // 같은 정보를 두 번 그리지 않게 예전 코드 HUD 의 해당 부품은 끈다(필사기 · 진화 · 버프 · 디버그 · 안내 글은 그대로)
            _xpFill.parent.gameObject.SetActive(false);
            _hpFill.parent.gameObject.SetActive(false);
            _lv.gameObject.SetActive(false);
            _timer.gameObject.SetActive(false);
            _fuelBar.SetActive(false); _bossBar.SetActive(false);
            _warnText.gameObject.SetActive(false); _pickText.gameObject.SetActive(false);
            _optBtn.SetActive(false);
        }

        void TickSa()
        {
            float dt = Time.unscaledDeltaTime;
            _sa.SetTop(run.level, run.xpNeeded > 0 ? run.xp / run.xpNeeded : 0f, Mathf.FloorToInt(run.Elapsed), run.kills);

            var cam = Camera.main;
            float hpT = player.maxHP > 0 ? player.hp / player.maxHP : 0f;
            var sh = Shooter;
            bool fuel = sh != null && sh.UsesFuel;
            Color fuelCol = Orange;
            float ft = 0f;
            string fuelLabel = null;
            if (fuel)
            {
                bool refuel = sh.IsRefueling;
                ft = sh.FuelNormalized;
                fuelCol = refuel ? new Color(0.45f, 0.55f, 0.85f) : (ft > 0.3f ? new Color(1.00f, 0.62f, 0.16f) : new Color(0.95f, 0.30f, 0.20f));
                fuelLabel = refuel ? $"재충전 {sh.RefuelRemaining:F1}s" : "연료";   // 예전 연료 바 글자 그대로
            }
            _sa.SetHero(cam, player.transform.position, _saSkin.heroBarOffset, hpT, Mathf.CeilToInt(player.hp), Mathf.CeilToInt(player.maxHP), fuel, ft, fuelCol, fuelLabel, dt);

            // 낮은 체력 — 화면 가장자리가 붉게 맥박친다
            float low = _saSkin.lowHpRatio;
            float red = low > 0f && hpT < low && hpT > 0f && run.state == RunState.Running
                      ? (0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f)) * (1f - hpT / low * 0.5f) : 0f;
            _sa.SetLowHp(red);

            // 보스 · 엘리트 — 예전 HUD 처럼 화면 위 한 칸을 같이 쓴다(보스 우선, 엘리트는 금색).
            // 프리팹에 엘리트 칸이 따로 있으면(원작 배치 1판) 엘리트는 머리 위
            var boss = director != null ? director.Boss : null;
            bool bossOn = boss != null && boss.Alive;
            string bossLabel = !bossOn ? null : (director.FinalBossActive ? "최종 보스" : "중간 보스") + $"   {Mathf.CeilToInt(boss.hp)} / {Mathf.CeilToInt(boss.maxHP)}";

            var elite = director != null ? director.Elite : null;
            bool eliteOn = elite != null && elite.Alive;
            string eliteLabel = null;
            if (eliteOn)
            {
                float eta = director.FinalBossEta;
                float fleeShowAt = EnemyTable.I != null ? EnemyTable.I.eliteFleeShowAt : 60f;
                eliteLabel = eta >= 0f && eta <= fleeShowAt ? $"진화 상자 · {Mathf.CeilToInt(eta)}초 뒤 도망" : "엘리트 · 진화 상자";
            }
            float eliteT = eliteOn && elite.maxHP > 0f ? elite.hp / elite.maxHP : 0f;
            float bossT = bossOn && boss.maxHP > 0f ? boss.hp / boss.maxHP : 0f;
            if (_sa.HasHeadBars)
            {
                // 머리 위 체력 막대가 있는 판(탕탕) — 11-18 사용자 "엘리트랑 중간 보스 나올때는 좀비 머리위에 hp 바 띄워주고
                // 화면 상단에는 최종 보스일때만": 화면 위 = 최종 보스만, 중간 보스 · 엘리트 = 머리 위(발밑 빨간 원은 ProtoBossMarker)
                bool finalOn = bossOn && director.FinalBossActive;
                bool midOn = bossOn && !finalOn;
                _sa.SetBoss(finalOn, finalOn ? bossT : 0f, finalOn ? bossLabel : null, false, dt);
                _sa.SetBossHead(midOn, cam, midOn ? ProtoBossMarker.HeadOf(boss.transform, boss.bodyRadius) : Vector3.zero, bossT, dt, midOn ? "중간 보스" : null);
                _sa.SetEliteHead(eliteOn, cam, eliteOn ? ProtoBossMarker.HeadOf(elite.transform, elite.bodyRadius) : Vector3.zero, eliteT, dt, eliteLabel);
            }
            else if (_sa.HasEliteBar)
            {
                _sa.SetBoss(bossOn, bossT, bossLabel, false, dt);
                _sa.SetElite(eliteOn, cam, eliteOn ? elite.transform.position + Vector3.up * _saSkin.eliteBarHeight : Vector3.zero, eliteT, eliteLabel, dt);
            }
            else if (bossOn) _sa.SetBoss(true, bossT, bossLabel, false, dt);
            else _sa.SetBoss(eliteOn, eliteT, eliteOn ? $"{eliteLabel}   {Mathf.CeilToInt(elite.hp)} / {Mathf.CeilToInt(elite.maxHP)}" : null, true, dt);
        }

        /// ShowWarning · ShowPickup 으로 들어온 문구를 원작 경고 띠 · 가운데 글로 한 번씩 넘긴다.
        void RouteSaMessages()
        {
            float now = Time.unscaledTime;
            if (_warnUntil != _saWarnSeen && now < _warnUntil && run.state != RunState.Result)
            {
                _saWarnSeen = _warnUntil;
                string m = _warnMsg ?? "";
                bool bossIcon = m.Contains("보스") || m.Contains("BOSS") || m.Contains("Elite") || m.Contains("엘리트");
                _sa.PlayWarning(m, _warnUntil - now, bossIcon, _warnEdge);
            }
            if (_pickAt != _saPickSeen && now - _pickAt < PickShowTime && run.state != RunState.Result)
            {
                _saPickSeen = _pickAt;
                _sa.PlayPickup(_pickMsg, _pickCol);
            }
        }

        /// 일시정지 버튼 · Esc — 일시정지 화면을 열고 닫는다(옵션 창이 떠 있으면 그것부터 닫는다).
        void TogglePause()
        {
            if (run == null || _sa == null) return;
            if (_optionsOpen) { ToggleOptions(); return; }
            if (_sa.SettingsOpen) { CloseSaSettings(); return; }
            if (_sa.PauseOpen) { ClosePause(); ProtoAudio.Play2D("ui_click"); return; }
            if (run.state != RunState.Running) return;   // 레벨업 · 결과 화면이 이미 timeScale 을 쥐고 있다
            Time.timeScale = 0f;
            OpenPause();
            ProtoAudio.Play2D("ui_click");
        }

        void OpenPause()
        {
            _saWeapons.Clear(); _saStats.Clear();
            bool icons = _popupSkin != null && _popupSkin.showIcons;
            // 11-18 보유한 것만 칸으로(사용자 "해당되는 칸만 보여주고 중앙 정렬") — 빈 칸 수(ShowPause 의 slots)는 넘기지 않는다
            foreach (var u in run.AllUpgrades)
            {
                if (u.id == "heal") continue;   // 응급 처치(즉시 회복)는 보유가 아니다
                int lv = run.upgradeLevels.TryGetValue(u.id, out var l) ? l : 0;
                if (lv <= 0) continue;
                var e = _popupSkin != null ? _popupSkin.Find(PopupKey(u.id)) : null;
                bool weapon = e != null ? e.weapon : u.gear != null;
                var item = new ProtoSaBattleUi.SkillItem { title = u.title, icon = icons && e != null ? e.icon : null, level = lv, maxLevel = u.maxLevel };
                (weapon ? _saWeapons : _saStats).Add(item);
            }
            _sa.SetHudVisible(false);   // 원작 세로판 제목이 위 HUD 와 겹친다
            _sa.ShowPause(_saWeapons, _saStats,
                          () => { ClosePause(); },
                          () => { _sa.HidePause(); run.Restart(); _lastState = (RunState)(-1); },   // 다음 LateUpdate 에서 화면 표시를 상태에 맞게 다시
                          OpenSettingsFromPause);
        }

        /// 일시정지의 설정(톱니) — 원작 틀 설정 창이 있으면 그것, 없으면 예전 옵션 창. 닫으면 일시정지로 돌아온다(정지는 그대로).
        void OpenSettingsFromPause()
        {
            _sa.HidePause();
            if (_sa.HasSettings) { _sa.ShowSettings(CloseSaSettings); return; }
            _optFromPause = true; _optionsOpen = true; _options.SetActive(true); RefreshOptions(); FitBox(_optPanel);
        }

        void CloseSaSettings()
        {
            _sa.HideSettings();
            if (run != null && run.state == RunState.Running) OpenPause();
        }

        void ClosePause()
        {
            _sa.HidePause();
            _sa.SetHudVisible(run.state != RunState.Intro && run.state != RunState.Result);
            if (run.state == RunState.Running) Time.timeScale = 1f;
        }

        /// 이번 판에 모은 강화를 한 줄에 셋씩.
        string SaUpgradeText()
        {
            var sb = new System.Text.StringBuilder();
            int n = 0;
            foreach (var u in run.AllUpgrades)
            {
                int lv = run.upgradeLevels.TryGetValue(u.id, out var l) ? l : 0;
                if (lv <= 0 || u.id == "heal") continue;
                if (n > 0) sb.Append(n % 3 == 0 ? "\n" : " · ");
                sb.Append(u.title).Append(" Lv.").Append(lv);
                n++;
            }
            return sb.ToString();
        }

        bool _saVictoryShown;
        const float VictoryTime = 1.3f;   // 원작 승리 띠 클립 1.2초 + 여유

        void TickSaResult()
        {
            bool clear = run.survived;
            float since = _resultSince >= 0f ? Time.unscaledTime - _resultSince : 0f;
            // 클리어: 결과 화면 전에 HUD 위에서 원작 승리 띠("생존 성공")를 한 번
            float delay = clear && _sa.HasVictory ? VictoryTime : 0f;
            if (delay > 0f && since < delay)
            {
                if (!_saVictoryShown) { _saVictoryShown = true; _sa.PlayVictory(); }
                return;
            }
            if (!_saResultShown)
            {
                _saResultShown = true;
                _sa.HideVictory();
                _sa.SetHudVisible(false);
                _sa.ShowResult(clear, Mathf.FloorToInt(run.elapsed), run.kills, run.level, SaUpgradeText(), () => { if (run != null) run.Restart(); });
            }
            // 클리어면 원작 비행기 자리에 우리 캐릭터 클리어 애니(승리 띠가 끝난 뒤부터 센다)
            if (clear && ProtoSpritePopup.TryGetClear(_resultSince + delay, out int frame, out float pop, out float alpha))
            {
                var s = ProtoSpritePopup.Clear;
                _sa.SetResultCharacter(true, s.tex, s.UV(frame), s.Aspect, alpha);
            }
            else _sa.SetResultCharacter(clear, null, default, 1f, 0f);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ════════════════════════════════════════════════════════════════
        // UI 화면 캡처(F10) — AI 컨셉 아트 재료용 "실제 게임 화면"
        // 플레이 중 F10 한 번이면 판을 먼저 채우고(강화 4개 · 좀비가 모일 때까지 15초) 아래 순서로 화면을 띄우며 찍는다(약 40초).
        // 01 전투 HUD · 02 같은 순간 UI 없이 · 03 레벨업 · 04 일시정지 · 05 설정 · 06 보스 경고 띠 · 07 보스전(보스가 다가온 뒤)
        // · 08 승리 띠 · 09 결과 성공 · (다시 시작 → 인트로 건너뜀, 같은 기록으로) 10 결과 실패.
        // 저장: <프로젝트>/ArtSource/UI_ConceptKit/01_화면_실제게임/ (에디터) · persistentDataPath (개발 빌드)
        // 게임 화면 해상도 그대로 찍고, 가로 2000px 보다 작으면 2배로 찍는다. 개발용 글자 · 창은 그동안 숨기고, 찍는 동안엔 죽지 않게 체력을 받친다.
        // 판이 끝나므로 하던 판은 날아간다(마지막은 실패 결과 화면에서 멈춤 — 다시 시작을 누르면 새 판).
        // ════════════════════════════════════════════════════════════════
        System.Collections.IEnumerator CaptureUiScreens()
        {
            _capturing = true;
            CaptureMode = true;
            bool debugOn = _debug.gameObject.activeSelf, hintOn = _hintGo != null && _hintGo.activeSelf;
            _debug.gameObject.SetActive(false);
            if (_hintGo != null) _hintGo.SetActive(false);
#if UNITY_EDITOR
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "ArtSource", "UI_ConceptKit", "01_화면_실제게임"));
#else
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "UI_ConceptKit");
#endif
            System.IO.Directory.CreateDirectory(dir);
            foreach (var old in System.IO.Directory.GetFiles(dir, "*.png")) System.IO.File.Delete(old);
            int count = 0;

            System.Collections.IEnumerator Shot(string name)
            {
                yield return new WaitForEndOfFrame();
                int super = Screen.width < 2000 ? 2 : 1;
                var tex = ScreenCapture.CaptureScreenshotAsTexture(super);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Destroy(tex);
                count++;
            }
            // 찍는 순서가 중간에 끊기지 않게 — 체력이 반 아래로 내려가면 채운다
            void KeepAlive()
            {
                var p = run.player;
                if (p != null && run.IsRunning && p.hp < p.maxHP * 0.5f) p.hp = p.maxHP * 0.85f;
            }
            System.Collections.IEnumerator Wait(float s)
            {
                float end = Time.unscaledTime + s;
                while (Time.unscaledTime < end) { KeepAlive(); yield return null; }
            }
            void LevelUpNow() => run.AddXP(Mathf.Max(0.01f, run.xpNeeded - run.xp));
            // 보유 목록이 비지 않게 — 즉시 회복은 빼고, 보조 무기를 2개까지 먼저 고른다
            int BestCard()
            {
                int gear = 0;
                foreach (var u in run.AllUpgrades)
                    if (u.gear != null && run.upgradeLevels.TryGetValue(u.id, out int l) && l > 0) gear++;
                int pick = -1;
                for (int i = 0; i < run.currentCards.Count; i++)
                {
                    var c = run.currentCards[i];
                    if (c.id == "heal") continue;
                    if (gear < 2 && c.gear != null) return i;
                    if (pick < 0) pick = i;
                }
                return pick < 0 ? 0 : pick;
            }

            // 0) 판 채우기 — 강화 4개를 먼저 얻고 좀비가 모일 때까지(게임 시간 15초, 최대 20초) 기다린다
            for (int i = 0; i < 4 && (run.IsRunning || run.state == RunState.LevelUp); i++)
            {
                if (run.state != RunState.LevelUp) LevelUpNow();
                yield return Wait(0.3f);
                if (run.state == RunState.LevelUp) run.PickCard(BestCard());
                yield return Wait(0.3f);
            }
            float warmUntil = Time.unscaledTime + 20f;
            while (run.IsRunning && run.Elapsed < 15f && Time.unscaledTime < warmUntil) { KeepAlive(); yield return null; }
            if (run.IsRunning)
            {
                float want = run.xpNeeded * 0.6f - run.xp;   // 경험치 칸 반쯤
                if (want > 0f) run.AddXP(want);
                if (run.player != null) run.player.hp = run.player.maxHP * 0.8f;   // 체력 조금 깎인 모습
            }
            yield return Wait(0.3f);

            // 1) 평소 HUD · 2) 같은 순간 UI 없이 — 둘이 짝이 되게 시간을 멈추고 찍는다
            if (run.IsRunning)
            {
                Time.timeScale = 0f;
                yield return Shot("01_전투HUD");
                var hidden = new System.Collections.Generic.List<Canvas>();
                foreach (var c in FindObjectsOfType<Canvas>())
                    if (c.enabled && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace) { c.enabled = false; hidden.Add(c); }
                yield return null;
                yield return Shot("02_UI없는_게임화면");
                foreach (var c in hidden) if (c != null) c.enabled = true;
                if (run.state == RunState.Running) Time.timeScale = 1f;
            }

            // 3) 레벨업 3택
            if (run.IsRunning)
            {
                LevelUpNow();
                yield return Wait(1.8f);
                yield return Shot("03_레벨업3택");
                if (run.state == RunState.LevelUp) run.PickCard(BestCard());
                yield return Wait(0.8f);
            }

            // 4) 일시정지 · 5) 설정
            if (run.IsRunning)
            {
                TogglePause();
                yield return Wait(0.8f);
                yield return Shot("04_일시정지");
                OpenSettingsFromPause();
                yield return Wait(0.8f);
                yield return Shot("05_설정");
                CloseSaSettings();
                yield return Wait(0.2f);
                ClosePause();
                yield return Wait(0.5f);
            }

            // 6) 보스 출현 경고 띠 · 7) 보스전 HUD — 띠가 걷히고 보스가 캐릭터 가까이 올 때까지(최대 6초)
            if (run.IsRunning && director != null)
            {
                if (director.Boss == null || !director.Boss.Alive) director.SpawnBoss(run.Elapsed, false);
                yield return Wait(1.0f);
                yield return Shot("06_보스출현_경고띠");
                yield return Wait(3.2f);
                float near = Time.unscaledTime + 6f;
                while (run.IsRunning && director.Boss != null && director.Boss.Alive && run.player != null && Time.unscaledTime < near &&
                       (director.Boss.transform.position - run.player.transform.position).sqrMagnitude > 7f * 7f)
                { KeepAlive(); yield return null; }
                yield return Shot("07_보스전_HUD");
            }

            // 8) 승리 띠 · 9) 결과 · 생존 성공 — 보스 막대가 띠 아래 남지 않게 보스를 먼저 쓰러뜨린다
            if (run.IsRunning)
            {
                if (director != null && director.Boss != null && director.Boss.Alive) director.Boss.TakeDamage(director.Boss.hp + 1f);   // 남은 체력만큼(큰 피해 숫자가 화면에 안 뜨게)
                yield return Wait(1.0f);
            }
            float recElapsed = run.elapsed; int recKills = run.kills, recLevel = run.level;
            var recLevels = new System.Collections.Generic.Dictionary<string, int>(run.upgradeLevels);
            if (run.IsRunning)
            {
                run.DebugClear();
                yield return Wait(0.55f);
                yield return Shot("08_승리띠");
                yield return Wait(2.2f);
                yield return Shot("09_결과_성공");
            }

            // 10) 결과 · 전투 실패 — 다시 시작하고 인트로를 건너뛴 뒤, 결과 칸이 비지 않게 앞 판 기록을 다시 넣고 쓰러진다
            run.Restart();
            _lastState = (RunState)(-1);
            yield return Wait(0.35f);
            float until = Time.unscaledTime + 8f;
            while (!run.IsRunning && Time.unscaledTime < until) { ProtoIntro.Skip(); yield return null; }
            if (run.IsRunning)
            {
                yield return Wait(0.5f);
                run.elapsed = recElapsed; run.kills = recKills; run.level = recLevel;
                run.xpNeeded = ProtoConfig.XPNeeded(recLevel);
                foreach (var kv in recLevels) run.upgradeLevels[kv.Key] = kv.Value;   // 결과 화면 표시용(다시 시작하면 지워진다)
                run.OnPlayerDied();
                yield return Wait(2.6f);
                yield return Shot("10_결과_실패");
            }

            _debug.gameObject.SetActive(debugOn);
            if (_hintGo != null) _hintGo.SetActive(hintOn);
            CaptureMode = false;
            _capturing = false;
            Debug.Log($"[UI 캡처] {count}장 → {dir}");
        }
#endif

        // ════════════════════════════════════════════════════════════════
        // 옵션
        // ════════════════════════════════════════════════════════════════
        /// 옵션 창은 게임을 멈춘다 (레벨업 카드와 같은 방식).
        void ToggleOptions()
        {
            if (run == null) return;
            // 일시정지 화면의 설정 버튼으로 열었으면 닫을 때 일시정지 화면으로 돌아간다(정지는 그대로)
            if (_optionsOpen && _optFromPause)
            {
                _optionsOpen = false; _optFromPause = false;
                _options.SetActive(false);
                ProtoAudio.Play2D("ui_click");
                OpenPause();
                return;
            }
            // 레벨업·결과 화면 위에는 띄우지 않는다 — 그 화면들이 이미 timeScale 을 쥐고 있어서,
            // 옵션이 닫히며 1 로 되돌리면 정지가 풀려버린다.
            if (!_optionsOpen && run.state != RunState.Running) return;

            _optionsOpen = !_optionsOpen;
            _options.SetActive(_optionsOpen);
            Time.timeScale = _optionsOpen ? 0f : 1f;
            if (_optionsOpen) { RefreshOptions(); FitBox(_optPanel); }
            // timeScale 0 에서도 들려야 한다. ProtoAudio 의 쿨다운이 unscaledTime 기준인 이유다.
            ProtoAudio.Play2D("ui_click");
        }

        void RefreshOptions()
        {
            _optFog.text = ProtoSettings.FogEnabled ? "켜짐" : "꺼짐";
            _optBurn.text = ProtoSettings.BurningBodies ? "켜짐" : "꺼짐";
            _optRain.text = ProtoSettings.RainLevelName;
            _optFlame.text = ProtoSettings.FlameStyleName;
            _optDmg.text = ProtoSettings.DamageNumbersName;
            _sfx.SetValueWithoutNotify(ProtoSettings.SfxVolume);
            _optSfxPct.text = $"{ProtoSettings.SfxVolume * 100f:F0}%";
        }

        // ════════════════════════════════════════════════════════════════
        // 짓기
        // ════════════════════════════════════════════════════════════════
        void Build()
        {
            var cgo = new GameObject("_HUDCanvas", typeof(RectTransform));
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);   // 세로 기준(§ Orientation 9:19.5). 가로 에디터 화면에서도 0.5 매치로 비슷한 크기
            scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            _canvasRect = (RectTransform)cgo.transform;

            // 형제 순서 = 그리는 순서: HUD < 레벨업 < 결과 < 건너뛰기 < 옵션
            _safe = Rect(_canvasRect, "Safe", Vector2.zero, Vector2.one);
            _hud = Rect(_safe, "HUD", Vector2.zero, Vector2.one).gameObject;
            BuildHud((RectTransform)_hud.transform);
            _levelUp = BuildLevelUp();
            BuildPopup();
            _result = BuildResult();
            BuildSa();
            _skip = Label(_safe, "Skip", "TAP TO SKIP", 34, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 0f),
                          new Vector2(0f, 120f), new Vector2(0f, 60f)).gameObject;
            if (_sa != null && _sa.BodyFont != null)
            {
                // 인트로 건너뛰기 글자도 원작 글꼴 · 테두리로(원작 결과 화면 "빈 곳을 탭해 닫기"와 같은 말투)
                var st = _skip.GetComponent<Text>();
                st.font = _sa.BodyFont; st.fontStyle = FontStyle.Normal; st.text = "화면을 탭해 건너뛰기";
                var ol = _skip.AddComponent<Outline>(); ol.effectColor = new Color(0f, 0f, 0f, 0.85f); ol.effectDistance = new Vector2(3f, -3f);
            }
            _options = BuildOptions();

            _levelUp.SetActive(false); _result.SetActive(false);
            _skip.SetActive(false); _options.SetActive(false);
        }

        void BuildHud(RectTransform root)
        {
            const float pad = 24f;
            // 상단 경험치 바 — 풀폭
            var xp = Bar(root, "XP", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -pad - 11f), new Vector2(-pad * 2f, 22f), XpCol, out _xpFill, out _);
            _lv = Label(root, "Lv", "Lv 1", 38, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(pad + 150f, -pad - 56f), new Vector2(300f, 50f));
            _timer = Label(root, "Timer", "00:00", 50, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(1f, 1f),
                           new Vector2(-pad - 150f, -pad - 60f), new Vector2(300f, 60f));

            // 체력
            Bar(root, "HP", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad + 230f, -pad - 120f), new Vector2(460f, 40f), HpCol, out _hpFill, out _);
            _hp = Label(_hpFill.parent, "HPText", "HP", 28, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));

            // 연료(화염방사기일 때만)
            _fuelBar = Bar(root, "Fuel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad + 230f, -pad - 164f), new Vector2(460f, 30f), Orange, out _fuelFill, out _fuelImg).gameObject;
            _fuelText = Label(_fuelBar.transform, "FuelText", "연료", 22, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));

            // 보스 체력
            _bossBar = Bar(root, "Boss", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -pad - 230f), new Vector2(900f, 46f), BossCol, out _bossFill, out _bossFillImg).gameObject;
            _bossText = Label(_bossBar.transform, "BossText", "BOSS", 28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _fuelBar.SetActive(false); _bossBar.SetActive(false);

            // 경고 문구 — 몬스터 대량 습격(ProtoSpawnDirector 의 돌격·포위) 예고. 보스 체력 바 아래, 화면 위쪽 가운데
            _warnText = Label(root, "Warning", "", 58, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -pad - 330f), new Vector2(1200f, 90f));
            _warnText.color = WarnCol;
            _warnText.gameObject.SetActive(false);

            // 버프 아이템 — 먹은 순간 문구(위로 떠오름)와 지속 버프 남은 시간. 화면 위쪽 가운데(Lv·타이머 사이)
            _buffText = Label(root, "Buffs", "", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -pad - 80f), new Vector2(700f, 50f));
            _buffText.color = new Color(1f, 0.85f, 0.4f, 1f);
            _buffText.gameObject.SetActive(false);
            _pickText = Label(root, "Pickup", "", 52, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -pad - 190f), new Vector2(900f, 80f));
            _pickText.gameObject.SetActive(false);

            // 옵션 버튼 — 좌상단 HP 바(·연료 바 자리) 아래, 왼쪽 끝을 HP 바와 맞춘다.
            // 예전 자리(타이머 아래)는 우상단 캐릭터 교체 임시 패널(ProtoCharacterSwitcher)과 겹쳤다.
            _optBtn = Button(root, "OptionsBtn", "옵션", 30, new Vector2(0f, 1f), new Vector2(pad + 110f, -pad - 227f), new Vector2(220f, 72f), BtnCol, ToggleOptions).gameObject;

            // 무기 진화 진행도 — 옵션 버튼 아래, 왼쪽 끝을 HP 바와 맞춘다. "진화 ▶ 무기 / 주무기 Lv 3/5 · 진화권"
            _evoText = Label(root, "Evolution", "", 28, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                             new Vector2(pad + 300f, -pad - 320f), new Vector2(600f, 90f));
            _evoText.gameObject.SetActive(false);

            // 왼쪽 아래 개발 정보 상자(프로토 한정) — 계측 수치 4줄 + 조작 키 3열.
            // 11-18: 예전엔 수치(왼쪽 아래 글자)가 필사기 버튼 위에, 조작 안내(맨 아래 두 줄)가 화면 밖 화살표 위에 겹쳤다
            // → 어두운 반투명 상자 하나로 묶고, 필사기는 오른쪽 아래로 옮겼다. 크기 = DevPanelSize(화살표가 이 칸을 피한다)
            BuildDevPanel(root, pad);

            // 우하단 필사기 버튼 — 원 + 쿨타임 링
            var ub = Rect(root, "Ultimate", new Vector2(1f, 0f), new Vector2(1f, 0f));
            ub.sizeDelta = new Vector2(220f, 220f);
            ub.anchoredPosition = new Vector2(-pad - 130f, pad + 200f);
            _ultGo = ub.gameObject;
            _ultDisc = ub.gameObject.AddComponent<Image>();
            _ultDisc.sprite = _disc;
            var btn = ub.gameObject.AddComponent<Button>();
            btn.targetGraphic = _ultDisc;
            btn.onClick.AddListener(() => { if (run != null) run.TryUltimate(); });
            var ring = Rect(ub, "Ring", Vector2.zero, Vector2.one);
            ring.sizeDelta = new Vector2(24f, 24f);
            _ultRing = ring.gameObject.AddComponent<Image>();
            _ultRing.sprite = _ring;
            _ultRing.type = Image.Type.Filled;
            _ultRing.fillMethod = Image.FillMethod.Radial360;
            _ultRing.fillOrigin = (int)Image.Origin360.Top;
            _ultRing.fillClockwise = true;
            _ultRing.raycastTarget = false;
            _ultText = Label(ub, "Label", "ULTIMATE", 34, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 탱크 하차 버튼(프로토 한정) — 필사기 버튼 위, 탑승 중에만
            _tankExitGo = Button(root, "TankExit", "하차 (E)", 36, new Vector2(1f, 0f), new Vector2(-pad - 130f, pad + 370f),
                                 new Vector2(220f, 90f), BtnCol, ProtoTank.RequestExit).gameObject;
            _tankExitGo.SetActive(false);
        }

        // 개발 정보 상자의 조작 키 — 3열 × 5줄(열 먼저 채움)
        static readonly (string key, string label)[] DevKeys =
        {
            ("WASD", "이동"), ("U", "필사기"), ("T", "필사기 트리"), ("F7", "게이지 가득"), ("Esc", "일시정지"),
            ("B", "보스"), ("K", "엘리트"), ("F5", "즉시 진화"), ("F8", "즉시 레벨업"), ("N", "원거리 좀비"),
            ("F2", "즉시 클리어"), ("F3", "융단 폭격"), ("F6", "헬기 공습"), ("E", "탱크 하차"), ("C", "캐릭터 창"),
        };

        void BuildDevPanel(RectTransform root, float pad)
        {
            const float In = 16f, StatLine = 27f, KeyRow = 25f, BadgeH = 21f;
            var dev = Rect(root, "DevPanel", Vector2.zero, Vector2.zero);
            dev.pivot = Vector2.zero;
            dev.anchoredPosition = new Vector2(pad, pad);
            dev.sizeDelta = DevPanelSize;
            var bg = Img(dev.gameObject, new Color(0.03f, 0.04f, 0.07f, 0.66f), false);
            bg.sprite = _rounded; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = RoundedBorder / 14f;   // 모서리 반경 14
            _hintGo = dev.gameObject;   // 캡처(F10) 때 상자째 숨긴다

            float innerW = DevPanelSize.x - In * 2f;
            _debug = Label(dev, "Debug", "", 22, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                           new Vector2(In + innerW * 0.5f, -In - StatLine * 2f), new Vector2(innerW, StatLine * 4f));
            _debug.color = new Color(1f, 1f, 1f, 0.95f);

            float sepY = -In - StatLine * 4f - 6f;
            var sep = Rect(dev, "Sep", new Vector2(0f, 1f), new Vector2(1f, 1f));
            sep.offsetMin = new Vector2(In, sepY - 2f); sep.offsetMax = new Vector2(-In, sepY);
            Img(sep.gameObject, new Color(1f, 1f, 1f, 0.24f), false);

            float colW = innerW / 3f, keyTop = sepY - 12f;
            for (int i = 0; i < DevKeys.Length; i++)
            {
                var (key, label) = DevKeys[i];
                float x = In + (i / 5) * colW, y = keyTop - (i % 5) * KeyRow;
                float bw = Mathf.Max(30f, key.Length * 11f + 12f);
                var badge = Rect(dev, "Key_" + key, new Vector2(0f, 1f), new Vector2(0f, 1f));
                badge.pivot = new Vector2(0f, 1f);
                badge.anchoredPosition = new Vector2(x, y);
                badge.sizeDelta = new Vector2(bw, BadgeH);
                var bi = Img(badge.gameObject, new Color(1f, 0.77f, 0.25f, 0.92f), false);
                bi.sprite = _rounded; bi.type = Image.Type.Sliced; bi.pixelsPerUnitMultiplier = RoundedBorder / 5f;
                var kt = Label(badge, "Text", key, 17, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                kt.color = new Color(0.11f, 0.07f, 0f, 1f);
                Destroy(kt.GetComponent<Shadow>());   // 밝은 배지 위 어두운 글자 — 그림자는 번진다
                var lt = Label(dev, "Label_" + key, label, 19, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                               new Vector2(x + bw + 7f + 80f, y - BadgeH * 0.5f), new Vector2(160f, BadgeH + 4f));
                lt.fontStyle = FontStyle.Normal;
                lt.color = new Color(0.92f, 0.92f, 0.94f, 0.95f);
            }
        }

        GameObject BuildLevelUp()
        {
            var root = Rect(_canvasRect, "LevelUp", Vector2.zero, Vector2.one);
            Img(root.gameObject, new Color(0f, 0f, 0f, 0.72f), true);
            // 내용은 1040×1200 고정 상자 — 가로 에디터 화면(높이 1080 단위)에서는 통째로 줄인다(FitBox)
            var safe = Rect(root, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            safe.sizeDelta = new Vector2(1040f, 1200f);
            _luBox = safe;
            _luTitle = Label(safe, "Title", "LEVEL", 80, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), new Vector2(0f, 110f));
            Label(safe, "Sub", "하나를 고르세요", 34, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -140f), new Vector2(0f, 50f));

            _cardRow = Rect(safe, "Cards", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _cardRow.anchoredPosition = new Vector2(0f, 20f);
            for (int i = 0; i < _cards.Length; i++)
            {
                int idx = i;
                var b = Button(_cardRow, "Card" + i, "", 1, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 460f), CardCol,
                               () => { if (run != null && run.state == RunState.LevelUp) run.PickCard(idx); });
                Destroy(b.GetComponentInChildren<Text>().gameObject);
                var outline = b.gameObject.AddComponent<Outline>();
                outline.effectColor = Orange; outline.effectDistance = new Vector2(3f, -3f);
                var rt = (RectTransform)b.transform;
                _cardTitle[i] = Label(rt, "Title", "", 38, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(-24f, 80f));
                _cardTitle[i].color = new Color(1f, 0.85f, 0.45f);
                _cardDesc[i] = Label(rt, "Desc", "", 28, TextAnchor.MiddleCenter, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 10f), new Vector2(-30f, 200f));
                _cardDesc[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                _cardLv[i] = Label(rt, "Lv", "", 24, TextAnchor.LowerCenter, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 60f), new Vector2(-20f, 80f));
                _cardLv[i].color = new Color(1f, 1f, 1f, 0.7f);
                _cards[i] = b;
            }

            var reroll = Button(safe, "Reroll", "다시 뽑기", 32, new Vector2(0.5f, 0f), new Vector2(-190f, 70f), new Vector2(340f, 90f), BtnCol,
                                () => { if (run != null && run.rerollsLeft > 0) run.Reroll(); });
            _rerollText = reroll.GetComponentInChildren<Text>();
            Button(safe, "Skip", "건너뛰기", 32, new Vector2(0.5f, 0f), new Vector2(190f, 70f), new Vector2(340f, 90f), BtnCol,
                   () => { if (run != null) run.SkipCards(); });
            return root.gameObject;
        }

        GameObject BuildResult()
        {
            var root = Rect(_canvasRect, "Result", Vector2.zero, Vector2.one);
            Img(root.gameObject, new Color(0f, 0f, 0f, 0.82f), true);

            // 사망
            var dead = Rect(root, "Dead", Vector2.zero, Vector2.one);
            _resultDead = dead.gameObject;
            Label(dead, "Title", "사망", 90, TextAnchor.MiddleCenter, new Vector2(0f, 0.68f), new Vector2(1f, 0.68f), Vector2.zero, new Vector2(0f, 120f));
            _deadBody = Label(dead, "Body", "", 38, TextAnchor.MiddleCenter, new Vector2(0f, 0.55f), new Vector2(1f, 0.55f), Vector2.zero, new Vector2(0f, 200f));
            Button(dead, "Restart", "다시 시작 (R)", 36, new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(400f, 100f), BtnCol, () => { if (run != null) run.Restart(); });

            // 생존 성공 — 클리어 애니 + 아래쪽 팝업
            var clear = Rect(root, "Clear", Vector2.zero, Vector2.one);
            _resultClear = clear.gameObject;
            var anim = Rect(clear, "Anim", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            anim.pivot = new Vector2(0.5f, 0f);   // 발밑 기준으로 커진다
            _clearAnim = anim.gameObject.AddComponent<RawImage>();
            _clearAnim.raycastTarget = false;

            _clearPopup = Rect(clear, "Popup", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            _clearPopup.pivot = new Vector2(0.5f, 0f);
            _clearPopup.sizeDelta = new Vector2(860f, 480f);
            _clearPopupGroup = _clearPopup.gameObject.AddComponent<CanvasGroup>();
            var border = Img(_clearPopup.gameObject, new Color(1f, 0.55f, 0.12f, 0.95f), false);
            var inner = Rect(_clearPopup, "Inner", Vector2.zero, Vector2.one);
            inner.sizeDelta = new Vector2(-12f, -12f);
            Img(inner.gameObject, new Color(0.05f, 0.05f, 0.08f, 1f), false);   // 반투명이면 아래 주황 테두리 판이 비쳐 갈색이 됐다
            Label(inner, "Title", "생존 성공", 72, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(0f, 100f));
            _clearBody = Label(inner, "Body", "", 36, TextAnchor.MiddleCenter, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 10f), new Vector2(0f, 170f));
            _clearBtn = Button(inner, "Restart", "다시 시작 (R)", 36, new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(400f, 96f), BtnCol, () => { if (run != null) run.Restart(); }).gameObject;
            return root.gameObject;
        }

        GameObject BuildOptions()
        {
            var root = Rect(_canvasRect, "Options", Vector2.zero, Vector2.one);
            Img(root.gameObject, new Color(0f, 0f, 0f, 0.78f), true);
            var panel = Rect(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.sizeDelta = new Vector2(900f, 1380f);
            _optPanel = panel;
            Img(panel.gameObject, PanelCol, true);
            Label(panel, "Title", "옵션", 64, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(0f, 90f));

            float y = -170f;
            _optFog = OptionRow(panel, ref y, "안개", "거리 안개와 바닥 안개가 같이 켜지고 꺼진다. 설정은 저장된다.",
                                () => ProtoSettings.FogEnabled = !ProtoSettings.FogEnabled);

            // 효과음 — 슬라이더. 끄는 동안은 소리를 내지 않고 손을 뗐을 때 한 번만 들려준다
            Label(panel, "SfxLabel", "효과음", 38, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f + 150f, y), new Vector2(300f, 70f));
            _optSfxPct = Label(panel, "SfxPct", "80%", 34, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-450f, y), new Vector2(120f, 70f));
            _sfx = Slider(panel, new Vector2(1f, 1f), new Vector2(-200f, y), new Vector2(320f, 40f));
            _sfx.onValueChanged.AddListener(v => { ProtoSettings.SfxVolume = v; _optSfxPct.text = $"{v * 100f:F0}%"; });
            var trig = _sfx.gameObject.AddComponent<EventTrigger>();
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => ProtoAudio.Play2D("ui_click"));   // 바뀐 음량으로 한 번 들려준다
            trig.triggers.Add(up);
            Desc(panel, y, "총성·탄착·발소리의 전체 크기. 소리별 상대 음량은 파형에 구워져 있다.");
            y -= 180f;

            _optBurn = OptionRow(panel, ref y, "좀비 몸 불", "화염에 타는 좀비 몸에 불 입자를 붙인다. 꺼도 그을린 색은 그대로 남는다.",
                                 () => ProtoSettings.BurningBodies = !ProtoSettings.BurningBodies);
            _optRain = OptionRow(panel, ref y, "비", "약함은 가랑비, 강함은 폭우. 세기를 바꿔도 비용은 거의 같다.",
                                 () => ProtoSettings.RainLevel = (ProtoSettings.RainLevel + 1) % 3);
            _optFlame = OptionRow(panel, ref y, "화염 연출", "같은 불을 파티클과 스프라이트 두 방식으로 그린다. 판정은 둘 다 같다.",
                                  () => ProtoSettings.FlameStyle = 1 - ProtoSettings.FlameStyle);
            // 한 줄이 세 가지를 돈다(YAZS 식 → 기존 → 꺼짐). 모양이 바뀌면 떠 있던 숫자는 지운다.
            _optDmg = OptionRow(panel, ref y, "데미지 숫자", "YAZS 식: 얇은 숫자 · 치명타(노랑)는 굵고 2배. 기존: 굵은 숫자 · 같은 적 피해 합침.",
                                () => { ProtoSettings.CycleDamageNumbers(); ProtoDamageText.Clear(); });

            Button(panel, "Close", "닫기", 38, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(360f, 100f), BtnCol, ToggleOptions);
            return root.gameObject;
        }

        /// 옵션 한 줄: 이름 · 오른쪽 버튼(현재 값) · 아래 설명. 버튼의 글자 Text 를 돌려준다.
        Text OptionRow(RectTransform panel, ref float y, string name, string desc, System.Action toggle)
        {
            Label(panel, name + "Label", name, 38, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f + 200f, y), new Vector2(400f, 70f));
            var b = Button(panel, name + "Btn", "", 32, new Vector2(1f, 1f), new Vector2(-60f - 130f, y), new Vector2(260f, 76f), BtnCol, () =>
            {
                toggle();
                RefreshOptions();
                ProtoAudio.Play2D("ui_click");
            });
            Desc(panel, y, desc);
            y -= 180f;
            return b.GetComponentInChildren<Text>();
        }

        void Desc(RectTransform panel, float y, string text)
        {
            var d = Label(panel, "Desc", text, 26, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, y - 80f), new Vector2(-120f, 80f));
            d.color = new Color(1f, 1f, 1f, 0.65f);
            d.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // ════════════════════════════════════════════════════════════════
        // 작은 도우미
        // ════════════════════════════════════════════════════════════════
        static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Image Img(GameObject go, Color c, bool raycast)
        {
            var i = go.AddComponent<Image>();
            i.color = c;
            i.raycastTarget = raycast;   // 전면 배경만 true — 뒤의 게임 화면으로 터치가 새지 않게
            return i;
        }

        Text Label(Transform parent, string name, string text, int size, TextAnchor align, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sizeDelta)
        {
            var rt = Rect(parent, name, aMin, aMax);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>();   // 밝은 바닥 위에서도 읽히게
            sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
            sh.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        RectTransform Bar(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color fill, out RectTransform fillRt, out Image fillImg)
        {
            var bg = Rect(parent, name, aMin, aMax);
            bg.anchoredPosition = pos;
            bg.sizeDelta = size;
            Img(bg.gameObject, BarBg, false);
            fillRt = Rect(bg, "Fill", Vector2.zero, new Vector2(0f, 1f));
            fillImg = Img(fillRt.gameObject, fill, false);
            return bg;
        }

        static void SetFill(RectTransform fill, float t) => fill.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);

        Button Button(Transform parent, string name, string label, int size, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, Color col, UnityEngine.Events.UnityAction onClick)
        {
            var rt = Rect(parent, name, anchor, anchor);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var img = Img(rt.gameObject, col, true);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(onClick);
            Label(rt, "Text", label, size, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return b;
        }

        Slider Slider(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(parent, "Slider", anchor, anchor);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var bg = Rect(rt, "Bg", new Vector2(0f, 0.3f), new Vector2(1f, 0.7f));
            Img(bg.gameObject, new Color(1f, 1f, 1f, 0.2f), false);
            var area = Rect(rt, "FillArea", new Vector2(0f, 0.3f), new Vector2(1f, 0.7f));
            var fill = Rect(area, "Fill", Vector2.zero, Vector2.one);
            Img(fill.gameObject, Orange, false);
            var harea = Rect(rt, "HandleArea", Vector2.zero, Vector2.one);
            var handle = Rect(harea, "Handle", Vector2.zero, new Vector2(0f, 1f));
            handle.sizeDelta = new Vector2(44f, 20f);
            var himg = Img(handle.gameObject, Color.white, true);
            himg.sprite = _disc;
            var s = rt.gameObject.AddComponent<Slider>();
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = himg;
            s.minValue = 0f; s.maxValue = 1f;
            return s;
        }

        /// 고정 크기 상자를 화면에 맞게 줄인다(키우지는 않는다). 세로 폰은 1, 가로 에디터 화면은 0.8 안팎.
        void FitBox(RectTransform box)
        {
            var r = _canvasRect.rect;
            float k = Mathf.Min(1f, (r.height - 40f) / box.sizeDelta.y, (r.width - 40f) / box.sizeDelta.x);
            box.localScale = new Vector3(k, k, 1f);
        }

        /// 노치·펀치홀을 피한다. HUD(전투 중 정보)만 안전 영역 안에 두고, 전면 배경(레벨업·결과·옵션)은 화면 끝까지 덮는다.
        void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            if (sa == _lastSafe || Screen.width <= 0 || Screen.height <= 0) return;
            _lastSafe = sa;
            _safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            _safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        }

        /// uGUI 는 EventSystem 이 있어야 클릭이 먹는다. 씬에 없으면 만든다(옛 입력 시스템 — activeInputHandler 0).
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        /// 원(버튼 바탕)·고리(쿨타임 링) 스프라이트를 코드로 만든다. 가장자리 1.5px 안티에일리어싱.
        // 둥근 사각형 판(9-slice) — 원 그림의 가장자리를 테두리로 쓴다. 모서리 반경 = RoundedBorder / pixelsPerUnitMultiplier
        const float RoundedBorder = 31f;
        static Sprite MakeRoundedSprite()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            float r = N * 0.5f - 1f;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N * 0.5f, N * 0.5f));
                px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((r - d) / 1.2f) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                 new Vector4(RoundedBorder, RoundedBorder, RoundedBorder, RoundedBorder));
        }

        static Sprite MakeCircleSprite(bool ring)
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            float r = N * 0.5f - 1f, inner = r - N * 0.09f;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N * 0.5f, N * 0.5f));
                float a = Mathf.Clamp01((r - d) / 1.5f);
                if (ring) a *= Mathf.Clamp01((d - inner) / 1.5f);
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
