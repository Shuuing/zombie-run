using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// 전투 화면 UI — Survivor Ace 원작 전투 UI 사본(<c>Assets/_Reference/SurvivorAce/UI/Battle — GitHub 에 없음</c>)으로 만든
    /// <c>UI_ZombieHUD</c> · <c>UI_ZombiePause</c> · <c>UI_ZombieResult</c> 를 띄우고 값을 채운다.
    ///
    /// <para>게임 코드(ProtoRun 등)를 모른다 — 넣어 줄 값과 콜백만 받는다. 연결은 <see cref="ProtoHUD"/> 가 한다.
    /// 프리팹 안 오브젝트는 원작 이름으로 찾는다(ExpCtrl · TimeRoot · killCountCtrl · StateBarHero …).
    /// 에디터에서 프리팹 부품을 꺼 두면 코드도 켜지 않는다(위치 · 크기도 프리팹 값 그대로 쓴다).</para>
    ///
    /// <para>원작사 그림 · 글꼴을 쓰는 임시 화면이다. 출시 전에 우리 그림으로 바꾼다.</para>
    /// </summary>
    public class ProtoSaBattleUi : MonoBehaviour
    {
        public struct SkillItem { public string title; public Sprite icon; public int level, maxLevel; }

        // ── HUD ────────────────────────────────────────────────────────
        GameObject _hud;
        RectTransform _hudRect, _follow;
        Image _expFill;
        Text _lv, _min, _sec, _kill;
        Button _pauseBtn;
        GameObject _time, _killGo;
        Text _timeText;              // 시간 한 덩어리 글자(탕탕 판 TimeRoot/TimeText) — 원작 판은 MinTxt · SecTxt 둘
        // 캐릭터 발밑 체력(탕탕 판 Follow/FeetHpBar) — 위 체력 칸과 같이 갱신
        RectTransform _feet;
        Image _feetHp, _feetReduce;
        float _feetReduceT = 1f;
        /// 발밑 체력 바를 발에서 얼마나 내릴지(캔버스 단위). ProtoHUD 가 스킨 값(feetBarOffset)을 넣는다.
        public float FeetOffset { get; set; } = 34f;
        // 주인공 바
        RectTransform _hero;
        Image _heroHp, _heroReduce, _energyBar;
        Text _heroHpText, _fuelText;
        GameObject _energy;
        float _heroReduceT = 1f;
        // 보스 · 엘리트 바
        GameObject _boss, _elite;
        Image _bossHp, _bossTrail, _eliteHp, _eliteTrail;
        Text _bossText, _eliteText;
        RectTransform _eliteRect;
        float _bossTrailT = 1f, _eliteTrailT = 1f;
        // 보스 머리 위 체력(탕탕 판 4판, Follow/BossHeadBar)
        // 11-18 사용자 "엘리트랑 중간 보스 나올때는 머리위에 hp 바, 화면 상단에는 최종 보스일때만" — 머리 위 막대 두 개(Follow/BossHeadBar · Follow/EliteHeadBar)
        class HeadBar { public GameObject go; public RectTransform rt; public Image hp, trail; public Text label; public float trailT = 1f; public string shown = ""; }
        HeadBar _bossHeadBar, _eliteHeadBar;
        Color _bossCol = Color.red;
        CanvasGroup _bossGroup;
        bool _bossIsElite;
        static readonly Color EliteGold = new Color(0.85f, 0.62f, 0.15f);   // 예전 HUD 엘리트 바 색
        // 경고 띠 · 가운데 글 · 낮은 체력 붉은 막
        GameObject _warn;
        Image _warnEdge;
        Animator _warnAnim;
        Text _warnText;
        float _warnUntil;
        Text _show;
        RectTransform _showRect;
        Vector2 _showPos;
        float _showAt = -99f;
        Color _showCol = Color.white;
        const float ShowTime = 1.4f;
        Image _red;
        GameObject _redGo, _warnIcon;
        // 필사기 버튼
        GameObject _ult;
        Button _ultButton;
        Image _ultRing, _ultBack, _ultFace, _ultGlow, _ultRipple;
        Text _ultName, _ultSub;
        Color _ultGlowCol, _ultRippleCol, _ultFaceCol = Color.white;

        // ── 일시정지 ────────────────────────────────────────────────────
        GameObject _pause;
        Animator _pauseAnim;
        RectTransform _pauseRoot;
        Transform _activeParent, _passiveParent;
        GameObject _activeTmpl, _passiveTmpl, _activeRow, _passiveRow;
        readonly List<GameObject> _pauseItems = new List<GameObject>();
        Action _onContinue, _onRestart, _onSettings;

        // ── 결과 ────────────────────────────────────────────────────────
        GameObject _result;
        Animator _resultAnim;
        GameObject _successBg, _failBg;
        Text _info, _chapter, _wave, _waveDesc, _killCount, _upgradeList;
        RawImage _charWin, _charFail;
        Action _onResultRestart;

        public bool PauseOpen => _pause != null && _pause.activeSelf;
        public bool ResultOpen => _result != null && _result.activeSelf;
        public RectTransform HudRect => _hudRect;

        /// 프리팹들을 만들어 둔다(HUD 는 켜고, 일시정지 · 결과 · 설정은 숨김). 하나라도 없으면 그 화면만 빠진다.
        public static ProtoSaBattleUi Create(GameObject hudPrefab, GameObject pausePrefab, GameObject resultPrefab, Action onPauseButton,
                                             Action onUltButton = null, GameObject settingsPrefab = null)
        {
            if (hudPrefab == null) return null;
            var host = new GameObject("_SaBattleUi");
            var ui = host.AddComponent<ProtoSaBattleUi>();
            if (!ui.InitHud(hudPrefab, onPauseButton, onUltButton)) { Destroy(host); return null; }
            if (pausePrefab != null) ui.InitPause(pausePrefab);
            if (resultPrefab != null) ui.InitResult(resultPrefab);
            if (settingsPrefab != null) ui.InitSettings(settingsPrefab);
            return ui;
        }

        // ── 찾기 ──────────────────────────────────────────────────────────
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
        static T Get<T>(Transform root, string name) where T : Component
        {
            var t = Find(root, name);
            return t != null ? t.GetComponent<T>() : null;
        }
        static bool On(GameObject go) => go != null && go.activeSelf;

        /// 원작 애니메이션 이벤트(Listen · AnimationEnd)를 받아 버릴 수신기를 애니메이터마다 붙인다.
        static void SinkEvents(GameObject root)
        {
            foreach (var a in root.GetComponentsInChildren<Animator>(true))
            {
                if (a.GetComponent<ProtoAnimEventSink>() == null) a.gameObject.AddComponent<ProtoAnimEventSink>();
                a.updateMode = AnimatorUpdateMode.UnscaledTime;   // 일시정지 · 레벨업(timeScale 0) 중에도 돈다
            }
        }

        GameObject Spawn(GameObject prefab, string name)
        {
            var go = Instantiate(prefab, transform);
            go.name = name;
            SinkEvents(go);
            return go;
        }

        // ════════════════════════════════════════════════════════════════
        // HUD
        // ════════════════════════════════════════════════════════════════
        bool InitHud(GameObject prefab, Action onPause, Action onUlt)
        {
            _hud = Spawn(prefab, "UI_ZombieHUD");
            var r = _hud.transform;
            _hudRect = (RectTransform)r;
            if (_hud.GetComponent<Canvas>() == null) return false;

            // 경험치: 2판(예전 자리 · 화면 폭 전체) = XpBar/Track/Fill · LvText, 1판(원작 배치) = ExpCtrl 안 Fill · "Lv.99"
            var xpBar = Find(r, "XpBar");
            if (xpBar != null) { _expFill = Get<Image>(xpBar, "Fill"); _lv = Get<Text>(r, "LvText"); }
            else { var exp = Find(r, "ExpCtrl"); _expFill = Get<Image>(exp, "Fill"); _lv = FindLevelText(exp); }
            _time = Find(r, "TimeRoot")?.gameObject;
            _min = Get<Text>(Find(r, "TimeRoot"), "MinTxt");
            _sec = Get<Text>(Find(r, "TimeRoot"), "SecTxt");
            _timeText = Get<Text>(Find(r, "TimeRoot"), "TimeText");
            var kill = Find(r, "killCountCtrl");
            _killGo = kill != null ? kill.gameObject : null;
            _kill = kill != null ? (Get<Text>(kill, "animator/Text") ?? Get<Text>(kill, "Text")) : null;   // 원작 판 animator/Text, 탕탕 판 Text
            _pauseBtn = Get<Button>(r, "Button_Back");
            if (_pauseBtn != null && onPause != null) _pauseBtn.onClick.AddListener(() => onPause());
            // 원작 그림은 다 터치를 받게 돼 있다 — HUD 가 이동 드래그(ProtoPlayer 는 UI 위 터치를 무시)를 막지 않게
            // 일시정지 버튼만 남기고 끈다(경고 띠 · 낮은 체력 붉은 막은 화면을 크게 덮는다)
            foreach (var g in _hud.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            if (_pauseBtn != null)
            {
                var hit = _pauseBtn.GetComponent<Graphic>();
                if (hit != null) hit.raycastTarget = true;
                else if (_pauseBtn.targetGraphic != null) _pauseBtn.targetGraphic.raycastTarget = true;
            }

            // 체력 · 연료: 2판 = 왼쪽 위 HeroBar · FuelBar(예전 자리), 1판 = 발밑을 따라다니는 StateBarHero(Follow 아래)
            _follow = (RectTransform)Find(r, "Follow");
            var hero = Find(r, "HeroBar") ?? Find(r, "StateBarHero");
            if (hero != null)
            {
                _hero = (RectTransform)hero;
                _heroHp = Get<Image>(hero, "Image_Hp");
                _heroReduce = Get<Image>(hero, "Image_Reduce");
                _heroHpText = Get<Text>(hero, "Text_HP");
            }
            _feet = Find(r, "FeetHpBar") as RectTransform;
            if (_feet != null)
            {
                _feetHp = Get<Image>(_feet, "Image_Hp");
                _feetReduce = Get<Image>(_feet, "Image_Reduce");
                _feet.gameObject.SetActive(false);
            }
            var fuel = Find(r, "FuelBar") ?? Find(hero, "EnergyBar");
            if (fuel != null)
            {
                _energy = fuel.gameObject;
                _energyBar = Get<Image>(fuel, "bar");
                _fuelText = Get<Text>(fuel, "FuelText");
                _energy.SetActive(false);
            }
            _boss = Find(r, "StateBarBoss")?.gameObject;
            if (_boss != null)
            {
                _bossHp = Get<Image>(_boss.transform, "image_hp");
                _bossTrail = Get<Image>(_boss.transform, "Image_Forward");
                _bossText = Get<Text>(_boss.transform, "Text_HP");
                if (_bossHp != null) _bossCol = _bossHp.color;
                // 경고 띠가 뜨는 동안 잠깐 숨기려고(띠와 겹친다) — 알파만 내린다
                _bossGroup = _boss.GetComponent<CanvasGroup>();
                if (_bossGroup == null) _bossGroup = _boss.AddComponent<CanvasGroup>();
                _bossGroup.blocksRaycasts = false; _bossGroup.interactable = false;
                _boss.SetActive(false);
            }
            _elite = Find(r, "StateBarElite")?.gameObject;   // 1판만 — 2판은 엘리트도 보스 칸을 금색으로 쓴다
            if (_elite != null)
            {
                _eliteRect = (RectTransform)_elite.transform;
                _eliteHp = Get<Image>(_elite.transform, "Image_Hp");
                _eliteTrail = Get<Image>(_elite.transform, "Image_Reduce");
                _eliteText = Get<Text>(_elite.transform, "Text_HP");
                _elite.SetActive(false);
            }
            _bossHeadBar = FindHeadBar(r, "BossHeadBar");
            _eliteHeadBar = FindHeadBar(r, "EliteHeadBar");
            _warn = Find(r, "UIFX_Warning")?.gameObject;
            if (_warn != null)
            {
                _warnAnim = Get<Animator>(_warn.transform, "Root");
                _warnText = Get<Text>(_warn.transform, "stayTime");
                _warnIcon = Find(_warn.transform, "Image_1")?.gameObject;   // 원작 stg_fx_bossIcon
                _warnEdge = Get<Image>(_warn.transform, "Root/root/Image_BG");   // 탕탕 판 화면 가장자리 빨간 테두리(없으면 null)
                _warn.SetActive(false);
            }
            var show = Find(r, "ShowText");
            if (show != null)
            {
                _show = Get<Text>(show, "complete");
                if (_show != null) { _showRect = _show.rectTransform; _showPos = _showRect.anchoredPosition; _show.gameObject.SetActive(false); }
            }
            _redGo = Find(r, "RedScreen")?.gameObject;
            _red = _redGo != null ? _redGo.GetComponent<Image>() : null;
            if (_redGo != null) _redGo.SetActive(false);

            // 필사기 버튼(2판에만 — 원작 충전 게이지 그림). 없으면 ProtoHUD 가 예전 코드 버튼을 쓴다
            var ult = Find(r, "UltButton");
            if (ult != null)
            {
                _ult = ult.gameObject;
                _ultButton = ult.GetComponent<Button>();
                var hit = ult.GetComponent<Graphic>(); if (hit != null) hit.raycastTarget = true;   // 위에서 다 끈 터치를 이 버튼만 다시
                if (_ultButton != null && onUlt != null) _ultButton.onClick.AddListener(() => onUlt());
                _ultRing = Get<Image>(ult, "Ring");
                _ultBack = Get<Image>(ult, "RingBack");
                _ultFace = Get<Image>(ult, "Face");
                if (_ultFace != null) _ultFaceCol = _ultFace.color;   // 프리팹 색을 기준으로(탕탕 판은 남색 원)
                _ultGlow = Get<Image>(ult, "Glow");
                _ultRipple = Get<Image>(ult, "Ripple");
                _ultName = Get<Text>(ult, "Label");
                _ultSub = Get<Text>(ult, "Sub");
                if (_ultGlow != null) _ultGlowCol = _ultGlow.color;
                if (_ultRipple != null) _ultRippleCol = _ultRipple.color;
                if (_ultName != null) BodyFont = _ultName.font;
            }

            // 예전 코드 HUD 에만 있던 것(2판 프리팹에만): 진화 진행 · 버프 남은 시간 · 탱크 하차
            _evo = Find(r, "EvoPanel")?.gameObject;
            _evoText = _evo != null ? Get<Text>(_evo.transform, "Text") : null;
            if (_evo != null) _evo.SetActive(false);
            _buff = Get<Text>(r, "BuffText");
            if (_buff != null) _buff.gameObject.SetActive(false);
            _tank = Find(r, "TankExit")?.gameObject;
            if (_tank != null)
            {
                var g = _tank.GetComponent<Graphic>(); if (g != null) g.raycastTarget = true;
                var b = _tank.GetComponent<Button>();
                if (b != null) b.onClick.AddListener(() => { ProtoAudio.Play2D("ui_click"); _onTankExit?.Invoke(); });
                _tank.SetActive(false);
            }
            // 승리 띠(원작 "Victoria" → "생존 성공")
            _victory = Find(r, "UIFX_ZhanDouShengLi")?.gameObject;
            if (_victory != null)
            {
                _victoryAnim = _victory.GetComponent<Animator>();
                var m = Get<Image>(_victory.transform, "Image_Mask"); if (m != null) m.raycastTarget = false;
                _victory.SetActive(false);
            }
            if (BodyFont == null) { var anyText = r.GetComponentsInChildren<Text>(true); foreach (var t in anyText) if (t.font != null && t.font.name.StartsWith("Alibaba")) { BodyFont = t.font; break; } }
            return true;
        }

        public bool HasUltButton => _ult != null;

        // ── 예전 코드 HUD 에만 있던 글자 · 버튼 ───────────────────────────
        GameObject _evo, _tank, _victory;
        Text _evoText, _buff;
        Animator _victoryAnim;
        Action _onTankExit;
        string _shownEvo2, _shownBuff2;

        /// 원작 본문 글꼴(알리바바). 예전 코드 글자를 원작 그림체로 바꿀 때 쓴다.
        public Font BodyFont { get; private set; }
        public bool HasEvo => _evo != null;
        public bool HasBuff => _buff != null;
        public bool HasTankExit => _tank != null;
        public bool HasVictory => _victory != null;

        /// 진화 진행(ProtoEvolution.HudLine). null 이면 숨김.
        public void SetEvo(string line)
        {
            if (_evo == null) return;
            bool on = !string.IsNullOrEmpty(line);
            if (_evo.activeSelf != on) _evo.SetActive(on);
            if (on && line != _shownEvo2 && _evoText != null) { _shownEvo2 = line; _evoText.text = line; }
        }

        /// 버프 남은 시간 한 줄. 빈 글자면 숨김.
        public void SetBuff(string line, bool visible)
        {
            if (_buff == null) return;
            bool on = visible && !string.IsNullOrEmpty(line);
            if (_buff.gameObject.activeSelf != on) _buff.gameObject.SetActive(on);
            if (on && line != _shownBuff2) { _shownBuff2 = line; _buff.text = line; }
        }

        public void SetTankExit(bool on) { if (_tank != null && _tank.activeSelf != on) _tank.SetActive(on); }
        public void SetTankExitAction(Action a) => _onTankExit = a;

        /// 클리어 순간 원작 승리 띠(1.2초 클립)를 한 번 재생한다.
        public void PlayVictory()
        {
            if (_victory == null) return;
            _victory.SetActive(true);
            if (_victoryAnim != null && _victoryAnim.runtimeAnimatorController != null) _victoryAnim.Rebind();
        }
        public void HideVictory() { if (_victory != null) _victory.SetActive(false); }

        // 원작 경험치 칸의 "Lv.99" 글자 — ProgressTextBar 바로 아래(Slider 형제)에 켜져 있는 Text
        static Text FindLevelText(Transform exp)
        {
            var bar = Find(exp, "ProgressTextBar");
            if (bar == null) return null;
            for (int i = 0; i < bar.childCount; i++)
            {
                var t = bar.GetChild(i).GetComponent<Text>();
                if (t != null && t.gameObject.activeSelf) return t;
            }
            return null;
        }

        /// 채움 막대 비율. Filled 그림은 fillAmount, 나머지(9-slice 늘인 그림)는 폭(anchorMax.x)으로 줄인다.
        static void SetFill(Image img, float t)
        {
            if (img == null) return;
            t = Mathf.Clamp01(t);
            if (img.type == Image.Type.Filled) { img.fillAmount = t; return; }
            var rt = img.rectTransform;
            if (!Mathf.Approximately(rt.anchorMax.x, t)) rt.anchorMax = new Vector2(t, rt.anchorMax.y);
            bool on = t > 0.002f;
            if (img.enabled != on) img.enabled = on;
        }

        public void SetHudVisible(bool on)
        {
            if (_hud != null && _hud.activeSelf != on) _hud.SetActive(on);
        }

        int _shownLv = -1, _shownSec = -1, _shownKills = -1;

        /// 위 줄: 레벨 · 경험치(0~1) · 경과 초 · 처치 수.
        public void SetTop(int level, float xpT, int seconds, int kills)
        {
            SetFill(_expFill, xpT);
            if (_lv != null && level != _shownLv) { _shownLv = level; _lv.text = $"Lv.{level}"; }
            if (seconds != _shownSec)
            {
                _shownSec = seconds;
                if (_min != null) _min.text = (seconds / 60).ToString("00");
                if (_sec != null) _sec.text = (seconds % 60).ToString("00");
                if (_timeText != null) _timeText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }
            if (_kill != null && kills != _shownKills) { _shownKills = kills; _kill.text = kills.ToString(); }
        }

        public void SetPauseButtonVisible(bool on)
        {
            if (_pauseBtn != null && _pauseBtn.gameObject.activeSelf != on) _pauseBtn.gameObject.SetActive(on);
        }

        /// 월드 좌표 → HUD 캔버스(Follow) 안 좌표. 카메라 뒤면 false.
        Canvas _hudCanvas;
        GameObject _hudCanvasOf;

        bool ToHud(Camera cam, Vector3 world, out Vector2 local)
        {
            local = default;
            if (cam == null || _follow == null) return false;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return false;
            if (_hudCanvas == null || _hudCanvasOf != _hud) { _hudCanvas = _hud.GetComponent<Canvas>(); _hudCanvasOf = _hud; }   // 프레임마다 GetComponent 대신(2026-11-20)
            var c = _hudCanvas;
            var uiCam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_follow, sp, uiCam, out local);
        }

        static Vector2 BottomLeft(RectTransform r, Vector2 local) => local + r.rect.size * r.pivot;

        int _shownHp = -1, _shownMax = -1;
        string _shownFuel;

        /// 체력 바(+ 연료). 바가 Follow 아래에 있으면(1판) 발밑을 따라가고, 아니면(2판) 프리팹 자리 그대로.
        /// worldFeet = 발 위치, offsetY = 따라갈 때 캔버스 단위로 아래로 내릴 거리. fuelLabel = 연료 줄 글자(없으면 null).
        public void SetHero(Camera cam, Vector3 worldFeet, float offsetY, float hpT, int hp, int maxHp,
                            bool fuelOn, float fuelT, Color fuelCol, string fuelLabel, float dt)
        {
            // 발밑 체력(탕탕 판) — 위 체력 칸이 있든 없든 따로 발을 따라간다
            if (_feet != null && _follow != null && _feet.parent == _follow)
            {
                bool fv = ToHud(cam, worldFeet, out var fl);
                if (_feet.gameObject.activeSelf != fv) _feet.gameObject.SetActive(fv);
                if (fv)
                {
                    _feet.anchoredPosition = BottomLeft(_follow, fl) + new Vector2(0f, -FeetOffset);
                    float t = Mathf.Clamp01(hpT);
                    SetFill(_feetHp, t);
                    _feetReduceT = t >= _feetReduceT ? t : Mathf.MoveTowards(_feetReduceT, t, dt * 0.6f);
                    SetFill(_feetReduce, _feetReduceT);
                }
            }
            // 연료 — 위 체력 칸과 따로(탕탕 판은 위 체력 칸 없이 발밑 체력 밑에 연료 줄이 붙는다)
            if (_energy != null && _energy.activeSelf != fuelOn) _energy.SetActive(fuelOn);
            if (fuelOn)
            {
                SetFill(_energyBar, fuelT);
                if (_energyBar != null) _energyBar.color = fuelCol;
                if (_fuelText != null && fuelLabel != _shownFuel) { _shownFuel = fuelLabel; _fuelText.text = fuelLabel ?? ""; }
            }
            if (_hero == null) return;
            if (_follow != null && _hero.parent == _follow)
            {
                bool vis = ToHud(cam, worldFeet, out var local);
                if (_hero.gameObject.activeSelf != vis) _hero.gameObject.SetActive(vis);
                if (!vis) return;
                _hero.anchoredPosition = BottomLeft(_follow, local) + new Vector2(0f, -offsetY);
            }
            hpT = Mathf.Clamp01(hpT);
            SetFill(_heroHp, hpT);
            // 깎인 부분이 잠깐 남았다가 따라 내려온다(원작 Reduce 줄)
            _heroReduceT = hpT >= _heroReduceT ? hpT : Mathf.MoveTowards(_heroReduceT, hpT, dt * 0.6f);
            SetFill(_heroReduce, _heroReduceT);
            if (_heroHpText != null && (hp != _shownHp || maxHp != _shownMax))
            {
                _shownHp = hp; _shownMax = maxHp;
                _heroHpText.text = _follow != null && _hero.parent == _follow ? hp.ToString() : $"{hp} / {maxHp}";
            }
        }

        /// 화면 위 보스 체력 바. on=false 면 숨김. elite = 엘리트를 같은 칸에 금색으로(예전 HUD 와 같은 방식).
        public void SetBoss(bool on, float hpT, string label, bool elite, float dt)
        {
            if (_boss == null) return;
            if (_boss.activeSelf != on) { _boss.SetActive(on); if (on) _bossTrailT = 1f; }
            if (!on) return;
            // 경고 띠(보스 출현 등)가 떠 있는 동안은 숨기고, 띠가 걷히면 0.25초에 걸쳐 다시 나타난다
            if (_bossGroup != null)
            {
                bool warnOn = _warn != null && _warn.activeSelf;
                _bossGroup.alpha = warnOn ? 0f : Mathf.MoveTowards(_bossGroup.alpha, 1f, dt * 4f);
            }
            if (elite != _bossIsElite) { _bossIsElite = elite; _bossTrailT = 1f; if (_bossHp != null) _bossHp.color = elite ? EliteGold : _bossCol; }
            hpT = Mathf.Clamp01(hpT);
            SetFill(_bossHp, hpT);
            _bossTrailT = hpT >= _bossTrailT ? hpT : Mathf.MoveTowards(_bossTrailT, hpT, dt * 0.35f);
            SetFill(_bossTrail, _bossTrailT);
            if (_bossText != null && _bossText.text != label) _bossText.text = label;
        }

        /// 엘리트 머리 위 체력 바(1판 프리팹에만 있다 — 2판은 SetBoss(elite: true) 를 쓴다). worldHead = 머리 위치.
        public void SetElite(bool on, Camera cam, Vector3 worldHead, float hpT, string label, float dt)
        {
            if (_elite == null) return;
            bool vis = on && ToHud(cam, worldHead, out var local);
            if (_elite.activeSelf != vis) { _elite.SetActive(vis); if (vis) _eliteTrailT = 1f; }
            if (!vis) return;
            ToHud(cam, worldHead, out local);
            _eliteRect.anchoredPosition = BottomLeft(_follow, local) + new Vector2(0f, 30f);
            hpT = Mathf.Clamp01(hpT);
            SetFill(_eliteHp, hpT);
            _eliteTrailT = hpT >= _eliteTrailT ? hpT : Mathf.MoveTowards(_eliteTrailT, hpT, dt * 0.5f);
            SetFill(_eliteTrail, _eliteTrailT);
            if (_eliteText != null && _eliteText.text != label) _eliteText.text = label;
        }

        HeadBar FindHeadBar(Transform r, string name)
        {
            var t = Find(r, name);
            if (t == null || _follow == null || t.parent != _follow) return null;
            var h = new HeadBar { go = t.gameObject, rt = (RectTransform)t, hp = Get<Image>(t, "Image_Hp"), trail = Get<Image>(t, "Image_Reduce"), label = Get<Text>(t, "Label") };
            if (h.label != null) h.label.text = "";
            h.go.SetActive(false);
            return h;
        }

        /// 머리 위 체력 막대가 있는 판인가(탕탕 판). 있으면 ProtoHUD 가 중간 보스 · 엘리트는 머리 위, 화면 위 막대는 최종 보스만 쓴다.
        public bool HasHeadBars => _bossHeadBar != null;

        /// 중간 보스 머리 위 체력(Follow/BossHeadBar — 없으면 아무것도 안 함). worldHead = 머리 위 점(ProtoBossMarker.HeadOf), label = 막대 위 글(없으면 숨김).
        public void SetBossHead(bool on, Camera cam, Vector3 worldHead, float hpT, float dt, string label = null) => SetHead(_bossHeadBar, on, cam, worldHead, hpT, dt, label);
        /// 엘리트 머리 위 체력(Follow/EliteHeadBar, 금색) — label 에 "진화 상자 · n초 뒤 도망" 같은 글.
        public void SetEliteHead(bool on, Camera cam, Vector3 worldHead, float hpT, float dt, string label = null) => SetHead(_eliteHeadBar, on, cam, worldHead, hpT, dt, label);

        /// 경고 띠가 떠 있는 동안은 숨긴다(화면 위 보스 막대와 같은 규칙). 깎인 부분은 잔상으로 남았다가 따라 내려온다.
        void SetHead(HeadBar h, bool on, Camera cam, Vector3 worldHead, float hpT, float dt, string label)
        {
            if (h == null) return;
            Vector2 local = default;
            bool vis = on && !(_warn != null && _warn.activeSelf) && ToHud(cam, worldHead, out local);
            if (h.go.activeSelf != vis) { h.go.SetActive(vis); if (vis) h.trailT = Mathf.Clamp01(hpT); }
            if (!vis) return;
            h.rt.anchoredPosition = BottomLeft(_follow, local);
            hpT = Mathf.Clamp01(hpT);
            SetFill(h.hp, hpT);
            h.trailT = hpT >= h.trailT ? hpT : Mathf.MoveTowards(h.trailT, hpT, dt * 0.35f);
            SetFill(h.trail, h.trailT);
            string l = label ?? "";
            if (h.label != null && l != h.shown) { h.shown = l; h.label.text = l; h.label.gameObject.SetActive(l.Length > 0); }
        }

        /// 엘리트 칸이 따로 있는가(1판). 없으면 ProtoHUD 가 보스 칸을 금색으로 같이 쓴다.
        public bool HasEliteBar => _elite != null;

        /// 원작 경고 띠("BOSS 출현" 자리)를 msg 로 한 번 재생한다. 띠는 3초짜리 — seconds 가 더 길면 그동안 켜 둔다.
        /// bossIcon = 띠 위 왕관 해골 그림(보스 · 엘리트 경고에만).
        /// redEdge = 화면 가장자리 빨간 테두리(경고 Root/root/Image_BG). 11-17 엘리트 · 중간 · 최종 보스 출현 경고에만 켠다(ProtoHUD.ShowWarning).
        public void PlayWarning(string msg, float seconds, bool bossIcon, bool redEdge = true)
        {
            if (_warn == null) return;
            if (_warnText != null) _warnText.text = msg;
            if (_warnIcon != null) _warnIcon.SetActive(bossIcon);
            if (_warnEdge != null) _warnEdge.enabled = redEdge;
            _warn.SetActive(true);
            if (_warnAnim != null && _warnAnim.runtimeAnimatorController != null)
            {
                _warnAnim.Rebind();   // 원작 컨트롤러는 상태 하나(켜지면 3초 클립 한 번) — 처음으로 되감기만 하면 된다
            }
            _warnUntil = Time.unscaledTime + Mathf.Max(3f, seconds);
        }

        /// 아이템 먹은 문구 — 원작 가운데 글 자리에서 살짝 떠올랐다 사라진다.
        public void PlayPickup(string msg, Color col)
        {
            if (_show == null) return;
            _show.text = msg; _showCol = col; _showAt = Time.unscaledTime;
            _show.gameObject.SetActive(true);
        }

        /// 낮은 체력 붉은 막(원작 RedScreen). t = 0(안 보임) ~ 1.
        public void SetLowHp(float t)
        {
            if (_redGo == null) return;
            bool on = t > 0.001f;
            if (_redGo.activeSelf != on) _redGo.SetActive(on);
            if (on && _red != null) { var c = _red.color; c.a = Mathf.Clamp01(t); _red.color = c; }
        }

        // ── 필사기 버튼 ─────────────────────────────────────────────────
        public enum UltState { None, Charging, Ready, Active, Locked }
        static readonly Color UltCharging = new Color(1f, 0.62f, 0.18f, 1f);   // 예전 버튼 링과 같은 주황
        static readonly Color UltReadyCol = new Color(1f, 0.86f, 0.32f, 1f);   // 다 차면 금색
        static readonly Color UltActiveCol = new Color(0.45f, 0.92f, 1f, 1f);  // 쓰는 중 — 원작 경험치 하늘색
        static readonly Color UltDim = new Color(0.55f, 0.55f, 0.6f, 1f);
        UltState _ultState = UltState.None;
        float _ultReadyAt;
        string _ultShownName = "\u0001", _ultShownSub = "\u0001";   // 처음 한 번은 꼭 쓰게(프리팹 견본 글자를 지운다)
        Font _ultNumFont;   // 아래 숫자 글꼴(원작 제목 글꼴 — 한글이 없다)

        /// 필사기 버튼 상태. fill = 링(0~1), name = 위 글자(필사기 이름), sub = 아래 숫자(62% · 8s 등, 없으면 null).
        public void SetUlt(bool show, UltState state, float fill, string name, string sub)
        {
            if (_ult == null) return;
            if (_ult.activeSelf != show) _ult.SetActive(show);
            if (!show) return;
            if (state != _ultState) { if (state == UltState.Ready) _ultReadyAt = Time.unscaledTime; _ultState = state; }
            if (_ultRing != null)
            {
                _ultRing.fillAmount = Mathf.Clamp01(fill);
                _ultRing.color = state == UltState.Ready ? UltReadyCol : state == UltState.Active ? UltActiveCol
                               : state == UltState.Locked || state == UltState.None ? UltDim : UltCharging;
            }
            if (_ultFace != null) { var fc = _ultFaceCol; if (state == UltState.None) fc.a *= 0.55f; _ultFace.color = fc; }
            if (_ultName != null && name != _ultShownName) { _ultShownName = name; _ultName.text = name ?? ""; }
            if (_ultSub != null && sub != _ultShownSub)
            {
                _ultShownSub = sub;
                _ultSub.text = sub ?? "";
                // 원작 숫자 글꼴엔 한글이 없어 "준비" 같은 글자는 가는 대체 글꼴로 그려진다 — 한글이 있으면 이름 글꼴로
                bool hangul = false;
                if (sub != null) foreach (var ch in sub) if (ch >= 0xAC00 && ch <= 0xD7A3) { hangul = true; break; }
                if (_ultNumFont == null) _ultNumFont = _ultSub.font;
                var want = hangul && _ultName != null ? _ultName.font : _ultNumFont;
                if (_ultSub.font != want) _ultSub.font = want;
                _ultSub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
                // 아래 숫자가 없으면 이름을 가운데로
                if (_ultName != null) _ultName.rectTransform.anchoredPosition = new Vector2(0f, string.IsNullOrEmpty(sub) ? 0f : 16f);
            }
        }

        // 준비 완료: 빛 고리가 숨 쉬듯 밝아졌다 어두워지고(원작 Add), 물결 고리가 1.2초마다 밖으로 퍼진다(원작 Out)
        public void TickUlt(float now)   // Update 가 부른다(편집 모드 렌더 프로브도 직접 부를 수 있게 공개)
        {
            if (_ult == null || !_ult.activeInHierarchy) return;
            bool ready = _ultState == UltState.Ready;
            if (_ultGlow != null)
            {
                float a = ready ? 0.55f + 0.35f * Mathf.Sin((now - _ultReadyAt) * 5f) : 0f;
                var c = _ultGlowCol; c.a = a; _ultGlow.color = c;
            }
            if (_ultRipple != null)
            {
                float t = ready ? ((now - _ultReadyAt) % 1.2f) / 0.8f : 1f;
                bool on = t < 1f;
                var c = _ultRippleCol; c.a = on ? (1f - t) * 0.9f : 0f; _ultRipple.color = c;
                float s = on ? 1f + 0.45f * t : 1f;
                _ultRipple.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            // 준비되는 순간 버튼이 한 번 톡 튄다
            float pop = ready ? Mathf.Clamp01((now - _ultReadyAt) / 0.25f) : 1f;
            float k = 1f + 0.12f * Mathf.Sin(pop * Mathf.PI);
            _ult.transform.localScale = new Vector3(k, k, 1f);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            TickUlt(now);
            if (_warn != null && _warn.activeSelf && now >= _warnUntil) _warn.SetActive(false);
            if (_show != null && _show.gameObject.activeSelf)
            {
                float k = (now - _showAt) / ShowTime;
                if (k >= 1f || k < 0f) _show.gameObject.SetActive(false);
                else
                {
                    _show.color = new Color(_showCol.r, _showCol.g, _showCol.b, 1f - k * k);
                    _showRect.anchoredPosition = _showPos + new Vector2(0f, 40f * k);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        // 일시정지
        // ════════════════════════════════════════════════════════════════
        void InitPause(GameObject prefab)
        {
            _pause = Spawn(prefab, "UI_ZombiePause");
            var r = _pause.transform;
            _pauseAnim = _pause.GetComponent<Animator>();
            _pauseRoot = (RectTransform)r.Find("Root");
            var act = Find(r, "ActiveSkillRoot");
            var pas = Find(r, "PassiveSkillRoot");
            _activeRow = act != null ? act.gameObject : null;
            _passiveRow = pas != null ? pas.gameObject : null;
            _activeParent = Find(act, "skillParent");
            _passiveParent = Find(pas, "skillParent");
            _activeTmpl = Find(_activeParent, "UISkill")?.gameObject;
            _passiveTmpl = Find(_passiveParent, "UISkill")?.gameObject;
            if (_activeTmpl != null) _activeTmpl.SetActive(false);
            if (_passiveTmpl != null) _passiveTmpl.SetActive(false);
            Hook(r, "Continue", () => _onContinue?.Invoke());
            Hook(r, "Back", () => _onRestart?.Invoke());
            Hook(r, "Setting", () => _onSettings?.Invoke());
            _pause.SetActive(false);
        }

        static void Hook(Transform root, string holder, Action a)
        {
            var b = Get<Button>(Find(root, holder), "ButtonLanguage");
            if (b == null) return;
            b.onClick.AddListener(() => { ProtoAudio.Play2D("ui_click"); a(); });
        }

        /// 일시정지 화면을 연다. weapons = 무기 줄(원작 "무기"), stats = 능력치 줄(원작 "강화").
        /// weaponSlots · statSlots = 이번 판에 가질 수 있는 종류 수 — 칸 판(탕탕 판)은 남는 자리를 빈 칸으로 채운다(0 이면 빈 칸 없음).
        public void ShowPause(List<SkillItem> weapons, List<SkillItem> stats, Action onContinue, Action onRestart, Action onSettings, int weaponSlots = 0, int statSlots = 0)
        {
            if (_pause == null) return;
            _onContinue = onContinue; _onRestart = onRestart; _onSettings = onSettings;
            foreach (var g in _pauseItems) if (g != null) Destroy(g);
            _pauseItems.Clear();
            Fill(_activeParent, _activeTmpl, weapons, weaponSlots);
            Fill(_passiveParent, _passiveTmpl, stats, statSlots);
            _pause.SetActive(true);
            FitPause();
            if (_pauseAnim != null && _pauseAnim.runtimeAnimatorController != null) { _pauseAnim.Rebind(); _pauseAnim.Play("UI_Pause_Show", 0, 0f); }
        }

        public void HidePause() { if (_pause != null) _pause.SetActive(false); }

        void Fill(Transform parent, GameObject tmpl, List<SkillItem> items, int slots = 0)
        {
            if (parent == null || tmpl == null || items == null) return;
            // 한 줄 6칸(원작 줄 폭 1080 · 칸 194). 넘치면 줄이 좁아지게 칸을 줄인다
            int n = items.Count;
            // 11-18 보유가 없으면 판 가운데 안내 문구(탕탕 판 프리팹의 EmptyText — 칸 판 형제, 없으면 아무 일 없음)
            var emptyText = parent.parent != null ? parent.parent.Find("EmptyText") : null;
            if (emptyText != null) emptyText.gameObject.SetActive(Mathf.Max(n, slots) == 0);
            var grid = parent.GetComponent<GridLayoutGroup>();
            // 11-18 칸 판(탕탕 판 — 한 줄 6칸): 빈 칸까지 센 수가 한 줄을 넘으면 판 폭 안에 들도록 칸을 줄인다(판 높이가 한 줄 기준)
            int total = grid != null ? Mathf.Max(n, slots) : n;
            float f = grid != null ? FitGrid(grid, total) : 1f;
            var baseCell = grid != null ? _gridBase[grid.GetInstanceID()].cell : Vector2.zero;
            float k = n <= 5 || grid != null ? 1f : 5f / n;
            foreach (var it in items)
            {
                var go = Instantiate(tmpl, parent);
                go.name = "UISkill_" + it.title;
                go.SetActive(true);
                go.transform.localScale = new Vector3(k, k, 1f);
                if (grid != null) ShrinkItem(go.transform, baseCell, f);
                var t = go.transform;
                var icon = Get<Image>(t, "Icon");
                bool hasIcon = it.icon != null;
                if (icon != null) { icon.sprite = it.icon; icon.enabled = hasIcon; }
                var name = Get<Text>(t, "Name");
                if (name != null) { name.text = it.title; name.gameObject.SetActive(!hasIcon); }
                // 별: 회색 칸 = 최대 레벨(9칸까지), 노란 별 = 지금 레벨. 9 를 넘으면 별 대신 "Lv.n"
                var stars = Find(t, "SkillStarCtrl");
                var lvText = Get<Text>(t, "Level");
                bool useStars = it.maxLevel <= 9 && stars != null && stars.gameObject.activeSelf;
                if (stars != null)
                {
                    var gray = Find(stars, "Gray"); var light = Find(stars, "Light");
                    for (int i = 0; gray != null && i < gray.childCount; i++) gray.GetChild(i).gameObject.SetActive(useStars && i < it.maxLevel);
                    for (int i = 0; light != null && i < light.childCount; i++) light.GetChild(i).gameObject.SetActive(useStars && i < it.level);
                }
                if (lvText != null) { lvText.text = $"Lv.{it.level}"; lvText.gameObject.SetActive(!useStars); }
                _pauseItems.Add(go);
            }
            // 빈 칸 — 칸 바탕만 흐리게(아이콘 · 이름 · 별 · 레벨 끔), 누를 수 없음
            for (int i = n; i < total; i++)
            {
                var go = Instantiate(tmpl, parent);
                go.name = "UISkill_Empty";
                go.SetActive(true);
                ShrinkItem(go.transform, baseCell, f);
                var t = go.transform;
                var icon = Get<Image>(t, "Icon"); if (icon != null) icon.enabled = false;
                foreach (var off in new[] { "Name", "SkillStarCtrl", "Level" }) { var o = Find(t, off); if (o != null) o.gameObject.SetActive(false); }
                var cg = go.GetComponent<CanvasGroup>();
                if (cg == null) cg = go.AddComponent<CanvasGroup>();
                cg.alpha = EmptySlotAlpha; cg.interactable = false; cg.blocksRaycasts = false;
                _pauseItems.Add(go);
            }
        }

        const float EmptySlotAlpha = 0.4f;

        struct GridBase { public Vector2 cell, spacing; public int cols; }
        readonly Dictionary<int, GridBase> _gridBase = new Dictionary<int, GridBase>();

        /// 칸 판의 처음 값(프리팹)을 기억해 두고, total 칸이 한 줄(cols)을 넘으면 같은 폭에 total 칸이 들게 줄인다. 줄인 배율을 돌려준다.
        float FitGrid(GridLayoutGroup grid, int total)
        {
            int id = grid.GetInstanceID();
            if (!_gridBase.TryGetValue(id, out var b))
            {
                b = new GridBase { cell = grid.cellSize, spacing = grid.spacing, cols = grid.constraintCount };
                _gridBase[id] = b;
            }
            float f = 1f;
            int cols = b.cols;
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && total > b.cols)
            {
                float w = b.cols * b.cell.x + (b.cols - 1) * b.spacing.x;
                f = w / (total * b.cell.x + (total - 1) * b.spacing.x);
                cols = total;
            }
            grid.constraintCount = cols;
            grid.cellSize = b.cell * f;
            grid.spacing = b.spacing * f;
            return f;
        }

        /// 줄인 칸에 맞춰 칸 안(ItemRoot — 원작 크기 그대로 두고 배율로 줄인 판)을 같은 비율로 더 줄인다.
        static void ShrinkItem(Transform item, Vector2 baseCell, float f)
        {
            if (f >= 0.999f) return;
            var ir = Find(item, "ItemRoot") as RectTransform;
            if (ir == null) { item.localScale = new Vector3(f, f, 1f); return; }
            var inner = baseCell + ir.sizeDelta;                 // 칸 안 좌표 크기(견본 그대로)
            ir.localScale = new Vector3(ir.localScale.x * f, ir.localScale.y * f, 1f);
            ir.sizeDelta = inner - baseCell * f;
            var lv = Find(item, "Level");
            if (lv != null) lv.localScale = new Vector3(f, f, 1f);
        }

        /// 가로 화면: 원작 세로판(1080×1920)이 Expand 로 높이에 맞춰 작게 서므로 판을 조금 키운다(위아래 여백을 남기고).
        void FitPause()
        {
            if (_pauseRoot == null) return;
            FitPortraitPanel(_pause.transform, _pauseRoot);
        }

        static readonly Dictionary<int, Vector2> _baseRef = new Dictionary<int, Vector2>();

        /// 세로판(1080×1920, Expand) 창을 가로 화면에서 1.2배로 키운다 — 레벨업 · 일시정지가 같이 쓴다.
        /// 11-17 "텍스트가 깨지는것같아": 예전엔 판 Transform 을 1.2배 했는데, 글자는 원래 크기로 그려진 뒤 늘어나 흐려졌다.
        /// 이제 CanvasScaler 기준 해상도를 1.2 로 나눠 캔버스 배율로 키운다(글자를 제 크기로 그린다). 스케일러가 없으면 예전 방식.
        public static void FitPortraitPanel(Transform canvasRoot, RectTransform root)
        {
            var c = canvasRoot != null ? canvasRoot.GetComponent<Canvas>() : null;
            var px = c != null ? c.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
            float k = px.width > px.height ? 1.2f : 1f;
            var sc = canvasRoot != null ? canvasRoot.GetComponent<CanvasScaler>() : null;
            if (sc != null && sc.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                int id = sc.GetInstanceID();
                if (!_baseRef.TryGetValue(id, out var b)) { b = sc.referenceResolution; _baseRef[id] = b; }
                sc.referenceResolution = b / k;
                if (root != null) root.localScale = Vector3.one;
            }
            else if (root != null) root.localScale = new Vector3(k, k, 1f);
        }

        // ════════════════════════════════════════════════════════════════
        // 결과
        // ════════════════════════════════════════════════════════════════
        void InitResult(GameObject prefab)
        {
            _result = Spawn(prefab, "UI_ZombieResult");
            var r = _result.transform;
            _resultAnim = _result.GetComponent<Animator>();
            _successBg = Find(r, "SuccessBg")?.gameObject;
            _failBg = Find(r, "FailBg")?.gameObject;
            _info = Get<Text>(r, "Text_Info");
            _chapter = Get<Text>(r, "Text_Chapter");
            _wave = Get<Text>(r, "Text_Wave");
            _waveDesc = Get<Text>(r, "Text_Wave_Desc");
            _killCount = Get<Text>(r, "txt_kill_count");
            _upgradeList = Get<Text>(r, "UpgradeList");
            _charWin = _successBg != null ? Get<RawImage>(_successBg.transform, "CharAnim") : null;
            _charFail = _failBg != null ? Get<RawImage>(_failBg.transform, "CharAnim") : null;
            var close = Get<Button>(r, "CloseBtn");
            if (close != null) close.onClick.AddListener(() => { ProtoAudio.Play2D("ui_click"); _onResultRestart?.Invoke(); });
            _result.SetActive(false);
        }

        public void ShowResult(bool clear, int seconds, int kills, int level, string upgrades, Action onRestart)
        {
            if (_result == null) return;
            _onResultRestart = onRestart;
            if (_successBg != null) _successBg.SetActive(clear);
            if (_failBg != null) _failBg.SetActive(!clear);
            if (_info != null) _info.text = clear ? "생존 성공" : "전투 실패";
            if (_chapter != null) _chapter.text = "생존 시간";
            if (_wave != null) _wave.text = $"{seconds / 60:00}:{seconds % 60:00}";
            if (_waveDesc != null) _waveDesc.text = $"Lv.{level} 달성";
            if (_killCount != null) _killCount.text = kills.ToString();
            if (_upgradeList != null) _upgradeList.text = string.IsNullOrEmpty(upgrades) ? "-" : upgrades;
            if (_charWin != null) _charWin.enabled = false;
            if (_charFail != null) _charFail.enabled = false;
            _result.SetActive(true);
            if (_resultAnim != null && _resultAnim.runtimeAnimatorController != null)
            {
                _resultAnim.Rebind();
                _resultAnim.SetTrigger(clear ? "BattleSuccess" : "BattleFail");
            }
        }

        /// 클리어 때 원작 비행기 자리에 우리 캐릭터 클리어 애니 한 칸을 띄운다(tex = 시트, uv = 칸, aspect = 칸 가로/세로).
        public void SetResultCharacter(bool clear, Texture tex, Rect uv, float aspect, float alpha)
        {
            var ri = clear ? _charWin : _charFail;
            if (ri == null) return;
            bool on = tex != null && alpha > 0.001f;
            ri.enabled = on;
            if (!on) return;
            ri.texture = tex; ri.uvRect = uv;
            var rt = ri.rectTransform;
            float h = rt.sizeDelta.y;
            rt.sizeDelta = new Vector2(h * aspect, h);
            ri.color = new Color(1f, 1f, 1f, alpha);
        }

        public void HideResult() { if (_result != null) _result.SetActive(false); }

        // ════════════════════════════════════════════════════════════════
        // 설정 창(원작 UI_BattleSetting 틀) — 줄은 프리팹의 Row_<키>(Toggle · Cycle · Slider)
        // ════════════════════════════════════════════════════════════════
        class SettingRow
        {
            public Func<bool> getOn; public Func<string> getText; public Func<float> getValue;
            public GameObject on, off; public Text value; public Slider slider;
        }
        GameObject _settings;
        Animator _settingsAnim;
        readonly Dictionary<string, SettingRow> _rows = new Dictionary<string, SettingRow>();
        Action _onSettingsClose;

        public bool HasSettings => _settings != null;
        public bool SettingsOpen => _settings != null && _settings.activeSelf;

        void InitSettings(GameObject prefab)
        {
            _settings = Spawn(prefab, "UI_ZombieSettings");
            var r = _settings.transform;
            _settingsAnim = _settings.GetComponent<Animator>();
            var close = Get<Button>(r, "Button_Close");
            if (close != null) close.onClick.AddListener(() => { ProtoAudio.Play2D("ui_click"); _onSettingsClose?.Invoke(); });
            var mask = Get<Button>(r, "Image_Mask");   // 원작처럼 바깥을 눌러도 닫힌다
            if (mask != null) mask.onClick.AddListener(() => _onSettingsClose?.Invoke());
            _settings.SetActive(false);
        }

        SettingRow RowOf(string key)
        {
            if (_settings == null) return null;
            if (_rows.TryGetValue(key, out var row)) return row;
            var t = Find(_settings.transform, "Row_" + key);
            if (t == null) return null;
            row = new SettingRow
            {
                on = Find(t, "On")?.gameObject, off = Find(t, "Off")?.gameObject,
                value = Get<Text>(t, "Value"), slider = Get<Slider>(t, "Slider"),
            };
            _rows[key] = row;
            return row;
        }

        static Button ButtonIn(Transform row, string name) => row != null ? Get<Button>(row, name) : null;

        /// 켬/끔 줄(원작 스위치 btn_kai / btn_guan). click 은 값을 바꾸는 일만 — 그림은 Refresh 가 맞춘다.
        public void BindToggle(string key, Func<bool> get, Action click)
        {
            var row = RowOf(key); if (row == null) return;
            row.getOn = get;
            var b = ButtonIn(Find(_settings.transform, "Row_" + key), "Toggle");
            if (b != null) b.onClick.AddListener(() => { click(); ProtoAudio.Play2D("ui_click"); RefreshSettings(); });
        }

        /// 돌아가며 바뀌는 줄(노란 버튼 + 지금 값 글자).
        public void BindCycle(string key, Func<string> get, Action click)
        {
            var row = RowOf(key); if (row == null) return;
            row.getText = get;
            var b = ButtonIn(Find(_settings.transform, "Row_" + key), "Cycle");
            if (b != null) b.onClick.AddListener(() => { click(); ProtoAudio.Play2D("ui_click"); RefreshSettings(); });
        }

        /// 슬라이더 줄(원작 효과음 슬라이더). set 은 끄는 동안 계속, release 는 손을 뗄 때 한 번.
        public void BindSlider(string key, Func<float> get, Action<float> set, Action release)
        {
            var row = RowOf(key); if (row == null || row.slider == null) return;
            row.getValue = get;
            row.slider.onValueChanged.AddListener(v => { set(v); if (row.value != null) row.value.text = $"{v * 100f:F0}%"; });
            if (release != null)
            {
                var trig = row.slider.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var up = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
                up.callback.AddListener(_ => release());
                trig.triggers.Add(up);
            }
        }

        public void RefreshSettings()
        {
            foreach (var row in _rows.Values)
            {
                if (row.getOn != null)
                {
                    bool v = row.getOn();
                    if (row.on != null) row.on.SetActive(v);
                    if (row.off != null) row.off.SetActive(!v);
                }
                if (row.getText != null && row.value != null) row.value.text = row.getText();
                if (row.getValue != null && row.slider != null)
                {
                    float v = row.getValue();
                    row.slider.SetValueWithoutNotify(v);
                    if (row.value != null) row.value.text = $"{v * 100f:F0}%";
                }
            }
        }

        public void ShowSettings(Action onClose)
        {
            if (_settings == null) return;
            _onSettingsClose = onClose;
            _settings.SetActive(true);
            RefreshSettings();
            // 가로 화면: 원작 세로판이 Expand 로 높이에 맞춰 작게 서므로 판을 키운다(판 높이 약 1210 → 화면 높이의 80% 안쪽, 최대 1.3배)
            var root = (RectTransform)_settings.transform.Find("Root");
            if (root != null)
            {
                var cr = ((RectTransform)_settings.transform).rect;
                float k = cr.width > cr.height ? Mathf.Clamp(cr.height * 0.82f / 1210f, 1f, 1.3f) : 1f;
                root.localScale = new Vector3(k, k, 1f);
            }
            if (_settingsAnim != null && _settingsAnim.runtimeAnimatorController != null) _settingsAnim.Rebind();
        }

        public void HideSettings() { if (_settings != null) _settings.SetActive(false); }
    }
}
