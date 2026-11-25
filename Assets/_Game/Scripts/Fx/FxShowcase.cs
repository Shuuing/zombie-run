using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Proto
{
    /// <summary>
    /// FX 쇼케이스 — 카탈로그(FxCatalog)와 액션 시트(ActionFxSheet)의 항목을 키 하나로 되풀이 재생하며 보는 씬의 구동기.
    /// 게임을 돌리지 않아도 <b>게임과 같은 카메라 각도·같은 밤 어둠</b> 아래에서 효과 하나만 반복해 볼 수 있다.
    ///
    /// <list type="bullet">
    /// <item>시트 동작: 미리보기 프리팹을 무대에 세우고 클립을 Playable 로 돌리며 ActionFxPlayer 를 <b>같은 시계</b>로 전진시킨다 —
    ///       게임의 보스 브레인이 하는 것과 같은 경로라 여기서 보이는 그대로 게임에 나온다.</item>
    /// <item>카탈로그 항목: 턴테이블 위 더미 좀비를 기준으로 ProtoFx.Play. 수명 0 인 항목은 스팬으로 켰다가 spanHold 뒤에 끈다.</item>
    /// <item>슬로모션은 Time.timeScale 로 — 애니메이션·파티클·코드 그림이 전부 같이 느려진다.
    ///       히트스톱은 ProtoFxRunner.BaseTimeScale 로 돌아오므로 슬로모션이 풀리지 않는다.</item>
    /// </list>
    /// 씬은 Tools/좀비 오버런/FX 툴/쇼케이스 씬 만들기 가 굽는다(FxShowcaseSceneBuilder).
    /// 플레이 중에 FX 툴 창으로 시트를 고쳐도 같은 에셋 인스턴스라 <b>다음 반복부터 바로</b> 반영된다.
    ///
    /// <para><b>FX 툴 창이 조종한다.</b> 플레이 중이면 <see cref="Instance"/> 로 창이 이 씬을 찾아 항목 선택 · 다시 재생 · 배속 · 정지 ·
    /// 스크럽(<see cref="Seek"/>) · 반복 · 턴테이블 · 밤 · 반경을 창에서 건드린다. 키는 그대로 먹으므로 창 없이(빌드 · Game 뷰만)도 쓸 수 있다.</para>
    /// </summary>
    public class FxShowcase : MonoBehaviour
    {
        [Header("무대")]
        [Tooltip("효과가 놓이는 자리. 카메라·밤 어둠의 중심. 비우면 자기 자신")]
        public Transform stage;
        [Tooltip("더미가 올라탄 턴테이블(ProtoTurntable). 시트 동작의 모델도 이 아래에 세운다")]
        public ProtoTurntable turntable;
        [Tooltip("카탈로그 항목의 기준이 되는 더미 좀비. 시트 동작을 볼 땐 숨긴다")]
        public GameObject dummy;
        [Tooltip("카메라의 밤 어둠. 비우면 N 키가 아무것도 안 한다")]
        public ProtoNightOverlay night;

        [Header("재생")]
        [Tooltip("코드 그림이 링 크기로 쓰는 판정 반경(폭발·보스 충돌). +/- 키")]
        public float radius = 2.4f;
        [Tooltip("큰 한 방(도약 착지). B 키")]
        public bool big;
        [Tooltip("끝나면 다시 튼다. L 키")]
        public bool loop = true;
        [Tooltip("끝나고 다시 틀기까지 쉬는 시간(초, 실시간)")]
        public float loopGap = 0.7f;
        [Tooltip("스팬(수명 0) 항목을 켜 두는 시간(초, 게임 시간)")]
        public float spanHold = 1.6f;
        [Tooltip("원샷 항목의 최소 재생 시간(초, 게임 시간) — 수명이 더 길면 수명만큼")]
        public float oneShotHold = 1.2f;
        [Tooltip("시트 동작 모델이 바라보는 방향(Y 각). 180 이면 카메라 쪽")]
        public float actionFacing = 180f;
        [Tooltip("시작할 때 보여 줄 항목(시트 키 또는 카탈로그 ID). 없으면 첫 항목")]
        public string startItem = ProtoBossBrain.SlamKey;

        [Header("표시")]
        [Tooltip("화면의 목록·도움말. H 키. 녹화할 땐 끈다")]
        public bool showUi = true;

        /// 1~4 키 순서. FX 툴 창의 무대 줄도 이 순서로 보여 준다.
        public static readonly float[] Speeds = { 1f, 0.5f, 0.25f, 0.1f };

        public class Item
        {
            public string label, note;
            public ActionFxSheet.Entry action;   // 시트 동작
            public FxCatalog.Entry fx;           // 카탈로그 항목
            public bool builtinOnly;             // 카탈로그에 없는 코드 그림 — ID 가 곧 코드 그림 이름
            public bool IsAction => action != null;
            public bool IsSpan => fx != null && fx.lifetime <= 0f;
        }

        readonly List<Item> _items = new List<Item>();
        int _index = -1;
        int _speed;
        bool _paused;
        bool _playing;
        float _t;              // 시트 동작: 동작 시각(게임 시간). 카탈로그: 켠 뒤 지난 시간
        float _hold;           // 카탈로그 항목을 켜 두는 길이
        float _gapUntil = -1f; // 반복 대기: 이 실시간에 다시 튼다. <0 = 대기 아님
        string _status;

        // 시트 동작 재생
        GameObject _model;
        ActionFxPlayer _player;
        PlayableGraph _graph;
        AnimationClipPlayable _clip;
        float _length;
        // 카탈로그 항목 재생
        FxHandle _handle;

        GUIStyle _head, _line, _dim, _warn;

        // 무기 무대 — FX 툴 창 [무기] 탭이 세운다. 켜져 있는 동안 카탈로그·시트 항목 재생은 쉰다
        FxWeaponStage _weapon;

        Transform Stage => stage != null ? stage : transform;
        Transform Mount => turntable != null ? turntable.transform : Stage;

        // ── 바깥(FX 툴 창)에서 보는 상태 ──────────────────────────────────
        /// 플레이 중인 쇼케이스. 없으면 null — FX 툴 창은 이걸로 "무대가 연결됐는지" 안다.
        public static FxShowcase Instance { get; private set; }
        public IReadOnlyList<Item> Items => _items;
        public int Index => _index;
        public Item Current => _index >= 0 && _index < _items.Count ? _items[_index] : null;
        public int SpeedIndex => _speed;
        public bool Paused => _paused;
        public bool IsPlaying => _playing;
        /// 시트 동작: 동작 시각(게임 시간). 카탈로그 항목: 켠 뒤 지난 시간
        public float Elapsed => _t;
        /// 시트 동작: 동작 길이. 카탈로그 항목: 켜 두는 길이
        public float Length => Current != null && Current.IsAction ? _length : _hold;
        public string Status => _status;
        /// 지금 세워 둔 시트 동작 모델과 그 연출 플레이어. 카탈로그 항목을 보는 중이면 null
        public GameObject Model => _model;
        public ActionFxPlayer Player => _player;
        /// 시트 동작의 Target(충돌점) 월드 좌표. 창의 씬 핸들이 쓴다
        public Vector3 ActionTarget() => Current != null && Current.IsAction && _model != null ? TargetOf(Current.action) : Vector3.zero;
        /// 무기 무대(캐릭터가 무기를 들고 더미를 향해 쏜다). 처음 쓸 때 붙는다 — 씬을 다시 굽지 않아도 된다
        public FxWeaponStage WeaponStage
        {
            get
            {
                if (_weapon == null)
                {
                    _weapon = GetComponent<FxWeaponStage>();
                    if (_weapon == null) _weapon = gameObject.AddComponent<FxWeaponStage>();
                }
                return _weapon;
            }
        }
        /// 무기 무대가 켜져 있나 — 그동안 Current 항목은 쉰다
        public bool WeaponMode => _weapon != null && _weapon.Active;

        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; }

        void Start()
        {
            ProtoFxRunner.Ensure();
            BuildList();
            ApplySpeed();
            int start = 0;
            if (!string.IsNullOrEmpty(startItem))
            {
                int i = _items.FindIndex(x => x.label == startItem);
                if (i >= 0) start = i;
            }
            Select(start);
        }

        void OnDestroy()
        {
            if (_weapon != null) _weapon.End();
            StopCurrent();
            Time.timeScale = 1f;
            ProtoFxRunner.BaseTimeScale = 1f;
        }

        // ── 목록 ───────────────────────────────────────────────────────
        void BuildList()
        {
            _items.Clear();
            var sheet = ActionFxSheet.Load();
            if (sheet != null)
                foreach (var a in sheet.actions)
                    if (a != null && !string.IsNullOrEmpty(a.key))
                        _items.Add(new Item
                        {
                            label = a.key, action = a,
                            note = (a.clip != null ? "클립 " + a.clip.name : "클립 없음") + $" · 이벤트 {a.events.Count}개 · 길이 {a.Length:0.00}s",
                        });

            var cat = FxCatalog.Load();
            var seen = new HashSet<string>();
            if (cat != null)
                foreach (var e in cat.entries)
                    if (e != null && !string.IsNullOrEmpty(e.id) && seen.Add(e.id))
                        _items.Add(new Item
                        {
                            label = e.id, fx = e,
                            note = (e.prefab != null ? "프리팹 " + e.prefab.name : !string.IsNullOrEmpty(e.builtin) ? "코드 그림 " + e.builtin : "비어 있음")
                                   + (e.lifetime <= 0f ? " · 스팬" : $" · 수명 {e.lifetime:0.0}s") + (string.IsNullOrEmpty(e.note) ? "" : "\n" + e.note),
                        });

            // 카탈로그에 아직 없는 코드 그림 — 그대로 이름으로 부를 수 있으니 같이 보여 준다
            foreach (var n in ProtoFx.BuiltinNames)
                if (seen.Add(n))
                    _items.Add(new Item { label = n, builtinOnly = true, fx = new FxCatalog.Entry { id = n, builtin = n, lifetime = 1.5f },
                                          note = "코드 그림 (카탈로그에 없음 — 카탈로그·시트 만들기 로 채울 수 있다)" });
        }

        /// 번호로 고른다(감긴다). 같은 항목이면 다시 재생.
        public void Select(int i)
        {
            if (_items.Count == 0) { _index = -1; _status = "카탈로그·시트에 항목이 없다 — Tools/좀비 오버런/FX 툴/카탈로그·시트 만들기"; return; }
            int n = _items.Count;
            int ni = ((i % n) + n) % n;
            bool same = ni == _index;
            if (WeaponMode) { EndWeapon(); same = false; }   // 항목을 고르면 무기 무대는 내린다
            _index = ni;
            _status = null;
            if (same) Replay(); else Play();
        }

        /// 이름(시트 키 또는 카탈로그 ID)으로 고른다. 목록에 없으면 false — 새로 만든 항목이면 <see cref="Reload"/> 뒤에 다시.
        public bool Select(string label)
        {
            int i = _items.FindIndex(x => x.label == label);
            if (i < 0) return false;
            Select(i);
            return true;
        }

        /// 카탈로그·시트를 다시 읽고 보던 항목을 다시 튼다. F5.
        public void Reload()
        {
            if (WeaponMode)
            {
                // 무기 무대는 그대로 두고 목록만 — ProtoFx 는 어차피 Play 때마다 카탈로그를 읽는다
                FxCatalog.Reload(); ActionFxSheet.Reload();
                BuildList();
                _status = "카탈로그·시트를 다시 읽었다 (무기 무대는 그대로)";
                return;
            }
            FxCatalog.Reload(); ActionFxSheet.Reload();
            string keep = Current != null ? Current.label : null;
            StopCurrent();
            BuildList();
            int i = keep != null ? _items.FindIndex(x => x.label == keep) : 0;
            _index = -1;
            Select(Mathf.Max(0, i));
            _status = "카탈로그·시트를 다시 읽었다";
        }

        // ── 무기 무대 ───────────────────────────────────────────────────
        /// 캐릭터가 무기를 들고 더미를 향해 쏘는 무대를 세운다. weapon 이 null 이면 캐릭터의 기본 무기.
        /// 보던 항목은 내린다(Current 는 남아 있어 항목을 다시 고르면 거기로 돌아간다).
        public void BeginWeapon(GameObject charPrefab, WeaponModelDef weapon)
        {
            StopCurrent();
            ProtoFx.StopAll();
            _gapUntil = -1f;
            if (dummy != null) dummy.SetActive(true);
            _status = null;
            WeaponStage.Begin(charPrefab, weapon, Stage, dummy != null ? dummy.transform : null);
            if (!string.IsNullOrEmpty(WeaponStage.Status)) _status = WeaponStage.Status;
        }

        /// 무기 무대를 내린다. 항목 재생은 다시 고를 때까지 쉰다.
        public void EndWeapon()
        {
            if (_weapon != null) _weapon.End();
            ProtoFx.StopAll();
        }

        // ── 재생 ───────────────────────────────────────────────────────
        /// 항목을 처음부터(모델 새로 세움). 다른 항목으로 옮길 때.
        void Play()
        {
            StopCurrent();
            ProtoFx.StopAll();   // 이전 항목이 남긴 연기 등도 정리
            var it = Current;
            if (it == null) return;
            _gapUntil = -1f;
            _t = 0f;
            if (it.IsAction) PlayAction(it.action); else PlayFx(it);
        }

        /// 같은 항목을 다시. 시트 동작은 모델을 그대로 두고 시계만 되감는다 — 깜빡임 없이, 턴테이블 각도도 그대로.
        public void Replay()
        {
            if (WeaponMode) { _weapon.FireOnce(); return; }   // 무기 무대에선 Space = 한 발
            var it = Current;
            if (it == null) return;
            if (it.IsAction && _model != null && _player != null)
            {
                if (_handle != null) { ProtoFx.Stop(_handle); _handle = null; }
                _gapUntil = -1f;
                _t = 0f;
                BeginAction(it.action);
                _playing = true;
            }
            else Play();
        }

        void PlayAction(ActionFxSheet.Entry a)
        {
            if (dummy != null) dummy.SetActive(false);
            var sheet = ActionFxSheet.Load();
            var prefab = a.previewPrefab != null ? a.previewPrefab : (sheet != null ? sheet.defaultPreviewPrefab : null);
            if (prefab == null) { _status = $"{a.key}: 미리보기 프리팹이 없다 — 시트의 previewPrefab 을 지정할 것"; return; }

            _model = Instantiate(prefab, Mount);
            _model.name = "미리보기 " + a.key;
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.Euler(0f, actionFacing, 0f);
            _model.transform.localScale = prefab.transform.localScale * (a.previewScale <= 0f ? 1f : a.previewScale);

            // 게임 로직은 끈다 — 보스 브레인이 플레이어를 찾아 움직이면 안 된다. 연출 플레이어만 남긴다
            foreach (var mb in _model.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(mb is ActionFxPlayer)) mb.enabled = false;
            _player = _model.GetComponent<ActionFxPlayer>();
            if (_player == null) _player = _model.AddComponent<ActionFxPlayer>();
            _player.enabled = true;
            _player.sheet = sheet;

            var anim = _model.GetComponentInChildren<Animator>(true);
            if (anim != null && a.clip != null)
            {
                // 컨트롤러 대신 클립 하나를 Playable 로 — 상태 이름을 몰라도 되고, 휴머노이드도 그대로 돈다.
                // Manual 모드: 시계는 아래 TickAction 이 쥔다(ActionFxPlayer 와 같은 _t).
                anim.enabled = true;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _graph = PlayableGraph.Create("FxShowcase");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(_graph, "모델", anim);
                _clip = AnimationClipPlayable.Create(_graph, a.clip);
                _clip.SetApplyFootIK(false);
                output.SetSourcePlayable(_clip);
                _graph.Play();
            }
            else if (anim != null)
            {
                anim.enabled = false;   // 클립이 없으면 컨트롤러가 제멋대로 돌지 않게 — 포즈 고정
                _status = $"{a.key}: 클립이 없어 포즈 없이 효과만 보여 준다";
            }

            _length = Mathf.Max(a.Length, 0.1f);
            BeginAction(a);
            _playing = true;
        }

        void BeginAction(ActionFxSheet.Entry a)
        {
            float s = _model.transform.lossyScale.x;
            if (!_player.Begin(a.key, TargetOf(a), a.previewRadius * s, big, s))
                _status = $"{a.key}: 시트에서 못 찾았다";
            _length = Mathf.Max(a.Length, 0.1f);   // 반복 사이에 에디터로 이벤트를 옮겼을 수 있다
            if (_graph.IsValid()) { _clip.SetTime(0); _graph.Evaluate(); }
        }

        Vector3 TargetOf(ActionFxSheet.Entry a) => _model.transform.TransformPoint(a.previewTarget);

        /// 시트 동작의 시각을 옮긴다(스크럽). 지나간 이벤트는 발화한 것으로 치고 포즈만 맞춘다 — FX 툴 창의 타임라인이 부른다.
        /// 끝난 뒤(반복 대기 중)에 불러도 그 시각부터 이어서 돈다.
        public void Seek(float t)
        {
            var it = Current;
            if (it == null || !it.IsAction || _model == null || _player == null) return;
            if (!_player.Playing || _player.Current != it.action) BeginAction(it.action);
            _gapUntil = -1f;
            _t = Mathf.Clamp(t, 0f, _length);
            if (_graph.IsValid()) { _clip.SetTime(_t); _graph.Evaluate(); }
            _player.SetTarget(TargetOf(it.action));
            _player.Seek(_t);
            _playing = true;
        }

        public void SetPaused(bool v) { _paused = v; ApplySpeed(); }

        public void ResetTurntable() { if (turntable != null) turntable.transform.localRotation = Quaternion.identity; }

        void PlayFx(Item it)
        {
            if (dummy != null) dummy.SetActive(true);
            var anchor = dummy != null ? dummy.transform : Stage;
            var ctx = FxContext.On(anchor, true);
            ctx.offset = it.fx.previewOffset;   // 피격 불똥은 가슴, 바닥 효과는 발
            ctx.radius = radius;
            ctx.big = big;
            ctx.scale = 1f;
            _handle = ProtoFx.Play(it.fx.id, ctx, it.IsSpan);
            if (it.fx.prefab == null)
            {
                if (string.IsNullOrEmpty(it.fx.builtin)) _status = $"{it.fx.id}: 재생할 게 없다 — 프리팹도 코드 그림도 비어 있다";
                else if (!ProtoFx.HasBuiltin(it.fx.builtin)) _status = $"{it.fx.id}: 코드 그림 '{it.fx.builtin}' 이 등록돼 있지 않다 (ProtoFxBuiltins)";
            }
            _hold = it.IsSpan ? spanHold : Mathf.Max(oneShotHold, it.fx.lifetime);
            _playing = true;
        }

        void StopCurrent()
        {
            _playing = false;
            _gapUntil = -1f;
            if (_handle != null) { ProtoFx.Stop(_handle); _handle = null; }
            if (_player != null) { _player.End(); _player = null; }
            if (_graph.IsValid()) _graph.Destroy();
            if (_model != null) { Destroy(_model); _model = null; }
        }

        void Finish()
        {
            if (_handle != null) { ProtoFx.Stop(_handle); _handle = null; }
            if (_player != null) _player.End();   // 모델은 마지막 포즈로 둔다
            _playing = false;
            _gapUntil = loop ? Time.unscaledTime + loopGap : -1f;
        }

        // ── 틱 ─────────────────────────────────────────────────────────
        void Update()
        {
            HandleKeys();
            if (_paused) return;
            if (WeaponMode) return;   // 무기 무대는 자기 Update 로 돈다

            if (!_playing)
            {
                if (_gapUntil >= 0f && Time.unscaledTime >= _gapUntil) Replay();
                return;
            }
            var it = Current;
            if (it == null) return;
            if (it.IsAction) TickAction(it.action);
            else
            {
                _t += Time.deltaTime;
                if (_t >= _hold) Finish();
            }
        }

        void TickAction(ActionFxSheet.Entry a)
        {
            if (_player == null) { Finish(); return; }
            _t += Time.deltaTime;
            if (_graph.IsValid()) { _clip.SetTime(_t); _graph.Evaluate(); }
            _player.SetTarget(TargetOf(a));   // 턴테이블이 돌아도 충돌점이 모델 앞을 따라가게
            _player.Advance(_t);
            if (_t >= _length) Finish();
        }

        void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Replay();
            if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.DownArrow)) Select(_index + 1);
            if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.UpArrow)) Select(_index - 1);
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetSpeed(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetSpeed(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetSpeed(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetSpeed(3);
            if (Input.GetKeyDown(KeyCode.P)) { _paused = !_paused; ApplySpeed(); }
            if (Input.GetKeyDown(KeyCode.L)) loop = !loop;
            if (Input.GetKeyDown(KeyCode.T) && turntable != null) turntable.spinning = !turntable.spinning;
            if (Input.GetKeyDown(KeyCode.R)) ResetTurntable();
            if (Input.GetKeyDown(KeyCode.N) && night != null) night.enabled = !night.enabled;
            if (Input.GetKeyDown(KeyCode.B)) big = !big;
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) radius = Mathf.Min(8f, radius + 0.5f);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) radius = Mathf.Max(0.5f, radius - 0.5f);
            if (Input.GetKeyDown(KeyCode.H)) showUi = !showUi;
            if (Input.GetKeyDown(KeyCode.F5)) Reload();
            if (Input.GetKeyDown(KeyCode.F) && WeaponMode) _weapon.autoFire = !_weapon.autoFire;
        }

        /// 1~4 키 순서(<see cref="Speeds"/>). 정지도 푼다.
        public void SetSpeed(int i) { _speed = Mathf.Clamp(i, 0, Speeds.Length - 1); _paused = false; ApplySpeed(); }

        void ApplySpeed()
        {
            float s = Speeds[_speed];
            ProtoFxRunner.BaseTimeScale = s;
            Time.timeScale = _paused ? 0f : s;
        }

        // ── 화면 표시 ───────────────────────────────────────────────────
        void OnGUI()
        {
            if (!showUi) return;
            if (_head == null)
            {
                _head = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, richText = true };
                _line = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
                _dim  = new GUIStyle(_line);
                _dim.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
                _warn = new GUIStyle(_line);
                _warn.normal.textColor = new Color(1f, 0.85f, 0.3f);
            }

            float s = Screen.height / 1000f;   // 세로 1000 단위 가상 화면 — Game 뷰 크기가 달라도 같은 비율
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;
            const float Pad = 12f, Row = 19f;

            // 헤더
            string head = $"FX 쇼케이스   속도 ×{Speeds[_speed]:0.##}{(_paused ? "  <color=#ffd54f>정지</color>" : "")}" +
                          $"   반복 {OnOff(loop)}   턴테이블 {OnOff(turntable != null && turntable.spinning)}" +
                          $"   밤 {OnOff(night != null && night.enabled)}   반경 {radius:0.0}   큰 한 방 {OnOff(big)}";
            Panel(new Rect(Pad - 4f, 6f, w - Pad * 2f + 8f, 26f));
            GUI.Label(new Rect(Pad, 8f, w - Pad * 2f, 24f), head, _head);

            // 목록
            int n = _items.Count;
            int rows = Mathf.Min(n, 24);
            int first = Mathf.Clamp(_index - rows / 2, 0, Mathf.Max(0, n - rows));
            float listTop = 40f;
            Panel(new Rect(Pad - 4f, listTop - 4f, 300f, rows * Row + 8f));
            for (int r = 0; r < rows; r++)
            {
                var it = _items[first + r];
                bool cur = first + r == _index;
                string tag = it.IsAction ? "<color=#80d8ff>동작</color>" : it.builtinOnly ? "<color=#bbbbbb>코드</color>" : it.IsSpan ? "<color=#c5e1a5>스팬</color>" : "<color=#ffe082>원샷</color>";
                string text = (cur ? "▶ " : "   ") + tag + "  " + (cur ? "<b>" + it.label + "</b>" : it.label);
                GUI.Label(new Rect(Pad, listTop + r * Row, 292f, Row + 2f), text, cur ? _line : _dim);
            }
            if (n > rows)
                GUI.Label(new Rect(Pad, listTop + rows * Row + 6f, 292f, Row), $"{_index + 1} / {n}", _dim);

            // 현재 항목
            var c = Current;
            float dx = Pad + 310f, dw = w - dx - Pad;
            if (WeaponMode)
            {
                var ws = _weapon;
                var sb = new System.Text.StringBuilder();
                sb.Append("<b>무기 무대</b>  ").Append(ws.CharacterPrefab != null ? ws.CharacterPrefab.name : "?").Append(" · ")
                  .Append(ws.Weapon != null ? (string.IsNullOrEmpty(ws.Weapon.displayName) ? ws.Weapon.name : ws.Weapon.displayName) : "무기 없음").Append('\n');
                var st = ws.Stats;
                if (st != null) sb.Append($"{st.mode} · 간격 {st.interval:0.00}s × 공속 {ws.fireRateMult:0.##} · 연출 세트 {ws.FxSet.name}\n");
                sb.Append(ws.CanFire ? (ws.autoFire ? "연사 중" : "멈춤") : "쏘지 않음")
                  .Append($" · 발사 {ws.Shots} · 명중 {ws.Hits} · 빗나감 {ws.Misses} · 처치 {ws.Kills} · 더미 체력 {ws.DummyHpNow:0}/{ws.dummyHp:0}\n");
                if (!string.IsNullOrEmpty(ws.Status)) sb.Append("<color=#ffd54f>").Append(ws.Status).Append("</color>\n");
                sb.Append("Space 한 발 · F 연사 켜고 끄기 · [ ] 로 카탈로그·시트 항목으로 돌아간다");
                float dh = 22f * 6;
                Panel(new Rect(dx - 4f, listTop - 4f, dw + 8f, dh));
                GUI.Label(new Rect(dx, listTop, dw, dh), sb.ToString(), _line);
            }
            else if (c != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("<b>").Append(c.label).Append("</b>\n").Append(c.note).Append('\n');
                if (c.IsAction)
                {
                    sb.Append($"\n동작 시각 {_t:0.00} / {_length:0.00}s\n");
                    foreach (var e in c.action.events)
                    {
                        if (e == null) continue;
                        bool on = _t >= e.time;
                        string k = e.kind == ActionFxSheet.Kind.Span ? $"{e.time:0.00}~{e.endTime:0.00}" : $"{e.time:0.00}";
                        string line = $"  {k,-11} {e.name}" + (string.IsNullOrEmpty(e.fxId) ? "" : "  → " + e.fxId) +
                                      (string.IsNullOrEmpty(e.marker) ? "" : "  [" + e.marker + "]");
                        sb.Append(on ? line : "<color=#888888>" + line + "</color>").Append('\n');
                    }
                }
                else sb.Append(_playing ? $"\n켜짐 {_t:0.00} / {_hold:0.00}s" : loop ? "\n곧 다시" : "\n끝");
                float dh = 22f * (c.IsAction ? 5 + c.action.events.Count : 5);
                Panel(new Rect(dx - 4f, listTop - 4f, dw + 8f, dh));
                GUI.Label(new Rect(dx, listTop, dw, dh), sb.ToString(), _line);
            }

            // 상태·도움말
            float helpTop = 1000f - 70f;
            Panel(new Rect(Pad - 4f, helpTop - 4f, w - Pad * 2f + 8f, 62f));
            if (!string.IsNullOrEmpty(_status)) GUI.Label(new Rect(Pad, helpTop - 26f, w - Pad * 2f, 22f), _status, _warn);
            GUI.Label(new Rect(Pad, helpTop, w - Pad * 2f, 22f),
                "[ ] 또는 ↑↓ 항목 이동 · Space 다시 재생(무기 무대: 한 발) · F 연사 · L 반복 · 1~4 속도(1 · ½ · ¼ · 1/10) · P 정지 · F5 카탈로그·시트 다시 읽기", _dim);
            GUI.Label(new Rect(Pad, helpTop + 20f, w - Pad * 2f, 22f),
                "T 턴테이블 · R 방향 초기화 · N 밤 어둠 · B 큰 한 방 · +/- 반경 · H 이 표시 숨김 · 휠 카메라 각도 · FX 툴 창(Tools ▸ 좀비 오버런 ▸ FX 툴)이 이 씬을 조종한다", _dim);
            GUI.matrix = Matrix4x4.identity;
        }

        static string OnOff(bool v) => v ? "<color=#a5d6a7>켬</color>" : "<color=#9e9e9e>끔</color>";

        static void Panel(Rect r)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
