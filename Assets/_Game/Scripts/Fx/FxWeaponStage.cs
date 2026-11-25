using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 무기 무대 — 쇼케이스 씬에서 <b>캐릭터가 무기를 들고 더미 좀비를 향해 쏘는 장면</b>. FX 툴 창의 [무기] 탭이 조종한다.
    ///
    /// <para>연출 경로는 게임(<see cref="ProtoShooter"/> · <see cref="ProtoProjectile"/> · <see cref="ProtoZombie"/>)과 같다 —
    /// 무기 연출 세트(<see cref="WeaponFxSet"/>)에서 총구·탄피·불똥·처치·폭발 ID 를 읽어 <see cref="ProtoFx"/> 로 내고,
    /// 예광탄은 같은 투사체(<see cref="ProtoProjectile"/>), 탄피는 <see cref="ProtoGunFx"/>, 화염은 <see cref="ProtoFlameCone"/>,
    /// 광선은 <see cref="ProtoBeamFx"/>, 폭격은 <see cref="ProtoAirstrike"/>. 그래서 여기서 보이는 그대로 게임에 나온다.
    /// 총구 자리·사격 자세(총열을 조준 방향으로 트는 것)도 슈터와 같은 규칙이다.</para>
    ///
    /// <para>게임 로직은 없다 — 타게팅 대신 늘 더미를 겨누고, 피해 대신 더미의 <b>가짜 체력</b>을 깎아 처치 리듬(바닥나면 처치 효과 + 잠깐 사라짐)만 흉내 낸다.
    /// 좀비 AI · 넉백 · 화상 · 데미지 숫자는 하지 않는다. 더미 프리팹(Zombie_Walker)에는 스크립트가 없어 흰 플래시는 여기서 직접 민다.</para>
    ///
    /// <para>쏘지 못하는 무기: 손에 들지 않는 보조 장비(Orbit · Companion)와 헬기 공습(HeliCall — 좀비 목록이 필요하다).
    /// 그런 무기도 [슬롯 재생](<see cref="PlaySlot"/>)으로 세트의 효과 하나하나는 볼 수 있다.</para>
    /// </summary>
    public class FxWeaponStage : MonoBehaviour
    {
        /// 게임 씬의 플레이어 실측 높이(ProtoSceneBuilder.PlayerHeight). 프리팹 그대로는 키가 달라 무기·효과 크기가 어긋난다.
        public const float CharacterHeight = 1.70f;

        [Header("자리")]
        [Tooltip("무대(더미) 기준 캐릭터 자리. 카메라는 -Z 에 있다 — 왼쪽 앞에 세워 총구와 더미가 같이 보이게")]
        public Vector3 shooterOffset = new Vector3(-2.4f, 0f, -1.6f);

        [Header("쏘기")]
        [Tooltip("더미를 향해 계속 쏜다. F 키")]
        public bool autoFire = true;
        [Tooltip("공속 배율. 1 = 수치 에셋의 간격 그대로")]
        [Range(0.25f, 4f)] public float fireRateMult = 1f;
        [Tooltip("치명타 확률 — 불똥 ×1.5 · 치명타 소리 · 약한 흔들림")]
        [Range(0f, 1f)] public float critChance = 0.1f;
        [Tooltip("발사음 · 탄착음 · 사망음")]
        public bool sound = true;
        [Tooltip("융단 폭격 로켓의 주기 폭격(airstrikeEvery)도 부른다")]
        public bool airstrike = true;

        [Header("더미")]
        [Tooltip("가짜 체력 — 이만큼 피해를 주면 처치 효과가 나고 잠깐 사라진다. 소총 12 × 5발 = 60")]
        public float dummyHp = 60f;
        [Tooltip("맞을 때 흰 플래시(ProtoZombie 와 같은 세기·시간)")]
        public bool dummyFlash = true;
        [Tooltip("처치 뒤 다시 서기까지(초)")]
        public float respawnDelay = 0.6f;

        public enum SlotKind { Muzzle, Hit, Kill, Explode }

        // ── 창이 읽는 상태 ──────────────────────────────────────────────
        public GameObject Character { get; private set; }
        public GameObject CharacterPrefab { get; private set; }
        public WeaponModelDef Weapon { get; private set; }
        public WeaponStats Stats => Weapon != null ? Weapon.stats : null;
        public WeaponFxSet FxSet => WeaponFxSet.Of(Stats);
        public int Shots { get; private set; }
        public int Hits { get; private set; }
        public int Kills { get; private set; }
        /// 더미가 서 있는데 맞히지 못하고 수명을 다한 투사체 수 — 총구 자리·조준이 어긋났다는 신호
        public int Misses { get; private set; }
        public float DummyHpNow => _hp;
        public string Status { get; private set; }
        public bool Active => Character != null;
        /// 지금 무기로 자동 사격이 되는가. 안 되는 무기(궤도 · 소환물 · 헬기 공습)는 슬롯 재생만.
        public bool CanFire => Weapon != null && Weapon.mount == WeaponMount.Hand && Stats != null && FiresInStage(Stats);
        public Transform Muzzle => _muzzle;

        Transform _stage, _dummy, _root, _visual;
        Animator _anim;
        WeaponSocket _socket;

        // 총구 — ProtoShooter.TickMuzzle 과 같은 규칙(에셋의 muzzlePoint, 없으면 가장 긴 축의 손에서 먼 끝)
        Transform _muzzle, _muzzleWeapon;
        Vector3 _muzzleLocal;
        Quaternion _muzzleRot = Quaternion.identity;
        bool _muzzleFound, _muzzleParented;

        float _nextFire, _lastFire = -999f, _aimTwist, _nextStrike;
        bool _lastCrit;
        const float AimHoldTime = 0.45f, AimTwistSpeed = 600f;   // ProtoShooter 와 같다

        // 줄 선 발사 — ProtoShooter 와 같은 규칙(ProtoFireSync): 트리거는 바로, 총구 화염 · 탄피 · 총알 · 소리는 사격 자세에 들어온 뒤
        enum ShotKind { Single, Shotgun, Beam }
        bool _pending;
        ShotKind _pendingKind;
        Vector3 _pendingAim;
        float _pendingAt;
        bool _pendingSawTransition;
        bool _aimSettled = true;
        /// 줄 선 발사가 실제로 나가기까지 걸린 시간(초) — 창이 보여 준다
        public float LastShotDelay { get; private set; }
        /// 지난 발이 나간 순간 사격 클립의 어디였나(ProtoFireSync.Describe) — 창 [무기] 탭 "지난 발 애니".
        public string LastShotAnim { get; private set; } = "";

        // ── 한 발 타임라인(스크럽) — 녹화한 한 발을 슬라이더로 본다. 창의 [● 한 발 녹화] → 녹화가 끝나면 무대가 멈추고 슬라이더가 열린다 ──
        // 포즈는 Animator 녹화/재생(StartRecording · playbackTime), 몸 틀기는 프레임마다 기록, 프리팹 효과는 기록한 시각 · 자리로 되감아 Simulate,
        // 총알은 기록한 출발점 · 방향 · 속도로 계산. 코드 그림(탄피 · 광선 · 기본 총구 화염)은 되감을 수 없어 안 보인다(그 수를 센다).
        [Tooltip("한 발 녹화 길이(초, 게임 시간). 트리거 → 발사(최대 0.3) → 비행 → 착탄 · 처치 효과까지")]
        public float recordSeconds = 0.8f;
        public bool Recording { get; private set; }
        public bool Scrubbing { get; private set; }
        public float ScrubTime { get; private set; }
        public float ScrubLength => _rec != null ? _rec.length : 0f;
        /// 기록된 시각(초, 트리거 = 0). 없으면 -1
        public float RecFireAt => _rec != null ? _rec.fireAt : -1f;
        public float RecHitAt => _rec != null ? _rec.hitAt : -1f;
        public float RecKillAt => _rec != null ? _rec.killAt : -1f;
        /// 되감지 못한 코드 그림 효과 수
        public int RecCodeOnly => _rec != null ? _rec.codeOnly : 0;
        /// 녹화한 발이 나간 순간의 클립 위치(타임라인 안내 줄).
        public string RecFireAnim => _rec != null ? _rec.fireAnim : "";

        class FxEvent { public float t; public string id; public GameObject prefab; public Vector3 pos; public Quaternion rot; public float scale; public GameObject inst; }
        class BulletEvent { public float t, end; public Vector3 origin, dir; public ProjectileSpec spec; public ProtoProjectile p; public GameObject inst; public bool model; }
        class ShotRecord
        {
            public float start, length, fireAt = -1f, hitAt = -1f, killAt = -1f, animStart;
            public int codeOnly;
            public string fireAnim = "";
            public readonly List<FxEvent> fx = new List<FxEvent>();
            public readonly List<BulletEvent> bullets = new List<BulletEvent>();
            public readonly List<Vector2> twist = new List<Vector2>();   // (시각, 몸 틀기 각)
        }
        ShotRecord _rec;
        Transform _scrubRoot;
        AnimatorUpdateMode _animModeBefore = AnimatorUpdateMode.Normal;

        // 더미
        float _hp, _deadUntil = -1f;
        FxHandle _goreHandle; float _goreCheckAt = -1f;   // 처치 0.6초 뒤 바닥 피 입자 수를 세어 Status 에 적는다(사용자 "바닥 피가 안 보인다" 진단)
        Renderer[] _dummyRenderers = new Renderer[0];
        MaterialPropertyBlock _mpb;
        float _flashUntil;
        bool _flashOn;
        static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

        // 투사체 — 슈터와 같은 풀 방식. 피해·치명타·방향은 여기서 들고 있다(투사체는 더미를 모른다)
        struct Shot { public float dmg; public bool crit; public Vector3 dir; public ProjectileSpec spec; }
        readonly Queue<ProtoProjectile> _pool = new Queue<ProtoProjectile>();
        readonly List<ProtoProjectile> _live = new List<ProtoProjectile>();
        readonly Dictionary<ProtoProjectile, Shot> _shots = new Dictionary<ProtoProjectile, Shot>();
        readonly HashSet<ProtoProjectile> _hitDone = new HashSet<ProtoProjectile>();
        Transform _poolRoot;

        // ── 플레이 중 스크립트 리로드 대비 ──
        // 툴을 고치는 동안 무대가 플레이 중인 일이 잦다. 리로드는 직렬화되는 필드(오브젝트 참조 · 숫자)는 살리고
        // Queue · Dictionary · MaterialPropertyBlock 은 비운다 — 그러면 날아가던 총알은 아무도 안 굴리고, 피격 사전이 없어 맞아도 모른다.
        // 그래서 만든 투사체 전부를 직렬화되는 목록에 두고, 리로드 뒤 첫 틀에서 잃어버린 것을 회수한다.
        [SerializeField] List<ProtoProjectile> _all = new List<ProtoProjectile>();
        bool _reconciled;   // 직렬화 안 됨 → 리로드 뒤 false 로 돌아온다

        // 화염 · 수류탄. 화염은 세트 jet 슬롯이 있으면 ProtoFlamePack(팩 프리팹 그대로), 아니면 ProtoFlameCone — ProtoShooter 와 같은 선택
        IProtoFlameFx _flame;
        GameObject _flameGo;
        bool _flamePack;
        GameObject _nade;
        Vector3 _nadeFrom, _nadeTo;
        float _nadeStart, _nadeAt = -1f;
        const float NadeFlight = 0.7f, NadeArc = 1.6f;

        // ── 세우기 · 치우기 ───────────────────────────────────────────────
        /// 캐릭터 프리팹(Char_01_Rookie …)을 무대 옆에 세우고 무기를 쥐어 준다. weapon 이 null 이면 프리팹의 소켓이 든 기본 무기.
        public void Begin(GameObject charPrefab, WeaponModelDef weapon, Transform stage, Transform dummy)
        {
            End();
            _stage = stage; _dummy = dummy;
            Status = null; Shots = Hits = Kills = Misses = 0;
            CharacterPrefab = charPrefab;
            if (charPrefab == null) { Status = "캐릭터 프리팹이 없다"; return; }

            // 루트(조준 방향) 아래 Visual(캐릭터) — 게임의 Player/Visual 구조와 같다. 사격 자세로 Visual 만 돌리므로 루트가 따로 있어야 한다
            _root = new GameObject("무기 무대 — " + charPrefab.name).transform;
            _root.SetParent(stage, false);
            _root.localPosition = shooterOffset;
            Character = Instantiate(charPrefab, _root);
            Character.name = "Visual";
            Character.transform.localPosition = Vector3.zero;
            Character.transform.localRotation = Quaternion.identity;
            _visual = Character.transform;

            // 게임플레이 스크립트는 끈다 — 무기 소켓만 남긴다(입 셀 같은 프로토 컴포넌트가 플레이어를 찾으면 안 된다)
            foreach (var mb in Character.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(mb is WeaponSocket)) mb.enabled = false;
            _anim = Character.GetComponentInChildren<Animator>(true);
            if (_anim != null)
            {
                _anim.enabled = true;
                _anim.applyRootMotion = false;
                _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            FitHeight();

            // 소켓은 Awake(Instantiate 순간)에서 프리팹의 기본 무기를 이미 물렸다. 다른 무기를 골랐으면 갈아 끼운다(진화 무기 · 다른 캐릭터 무기)
            _socket = Character.GetComponentInChildren<WeaponSocket>(true);
            Weapon = weapon != null ? weapon : (_socket != null ? _socket.weapon : null);
            if (_socket != null && weapon != null && weapon != _socket.weapon) _socket.Equip(weapon);
            if (_socket != null) _socket.Show(true);

            if (_socket == null) Status = "캐릭터에 WeaponSocket 이 없다 — 무기를 들 수 없다";
            else if (Weapon == null) Status = "무기가 없다 — 캐릭터 소켓도 비어 있다";
            else if (Weapon.stats == null) Status = $"{Weapon.name}: 수치 에셋(stats)이 비어 있다 — 쏠 수 없다. [슬롯 재생] 은 기본 세트로 된다";
            else if (Weapon.mount != WeaponMount.Hand) Status = $"{Label(Weapon)}: 손에 들지 않는 장비 — 무대에서 쏘지 않는다. [슬롯 재생] 으로 효과만 본다";
            else if (!FiresInStage(Weapon.stats)) Status = $"{Label(Weapon)}: 이 발사 방식({Weapon.stats.mode})은 좀비 목록이 필요해 무대에서 쏘지 않는다 — 프로토 씬에서. [슬롯 재생] 은 된다";

            _hp = dummyHp; _deadUntil = -1f;
            _nextFire = Time.time + 0.4f;      // 세워지고 잠깐 뒤 첫 발
            _nextStrike = Time.time + 1f;      // 슈터와 같다 — 첫 폭격은 1초 뒤
            _nadeAt = -1f;
            _aimTwist = 0f;
            CacheDummy();
            Aim();
            TickMuzzle();
        }

        public void End()
        {
            if (Scrubbing) EndScrub();
            if (Recording && _anim != null) _anim.StopRecording();
            Recording = false; _rec = null;
            if (_flame != null) { ProtoAudio.SetLoop(null, false); Destroy(_flameGo); _flame = null; _flameGo = null; }
            for (int i = 0; i < _live.Count; i++)
                if (_live[i] != null) { _live[i].gameObject.SetActive(false); _pool.Enqueue(_live[i]); }
            _live.Clear(); _shots.Clear(); _hitDone.Clear();
            if (_nade != null) _nade.SetActive(false);
            _nadeAt = -1f;
            if (_dummyRenderers != null && _dummyRenderers.Length > 0) SetFlash(false);
            if (_dummy != null) _dummy.gameObject.SetActive(true);
            if (_root != null) Destroy(_root.gameObject);
            _root = null; Character = null; _visual = null; _anim = null; _socket = null;
            Weapon = null; CharacterPrefab = null;
            _pending = false;
            _muzzle = null; _muzzleWeapon = null; _muzzleFound = false; _muzzleParented = false;
            _deadUntil = -1f;
        }

        void OnDestroy()
        {
            End();
            for (int i = 0; i < _all.Count; i++) if (_all[i] != null) Destroy(_all[i].gameObject);
            _all.Clear();
            if (_poolRoot != null) Destroy(_poolRoot.gameObject);
            if (_nade != null) Destroy(_nade);
        }

        /// 리로드 뒤 첫 틀 — 잃어버린 총알(목록에 있는데 살아 있지도 풀에 있지도 않은 것)을 회수한다. 평소엔 한 번 돌고 끝.
        void Reconcile()
        {
            _reconciled = true;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            int stray = 0;
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var p = _all[i];
                if (p == null) { _all.RemoveAt(i); continue; }
                if (_live.Contains(p) || _pool.Contains(p)) continue;
                p.gameObject.SetActive(false);
                if (_poolRoot != null) p.transform.SetParent(_poolRoot, true);
                _pool.Enqueue(p);
                stray++;
            }
            if (stray > 0) Debug.Log($"[무기 무대] 스크립트 리로드 뒤 총알 {stray}발 회수");
        }

        static string Label(WeaponModelDef w) => w == null ? "무기 없음" : string.IsNullOrEmpty(w.displayName) ? w.name : w.displayName;

        /// 무대에서 자동 사격이 되는 발사 방식. 궤도 · 소환물은 보조 장비 런타임(ProtoSupportGear)이, 헬기 공습은 좀비 목록이 필요하다.
        static bool FiresInStage(WeaponStats st) => st.mode != FireMode.Orbit && st.mode != FireMode.Companion && st.mode != FireMode.HeliCall;

        /// 바인드 포즈의 렌더러 바운즈로 키를 재서 1.70m 로 맞춘다 — ProtoSceneBuilder 가 플레이어를 세울 때와 같은 규칙. 무기는 뺀다.
        void FitHeight()
        {
            bool any = false; var b = new Bounds();
            foreach (var r in Character.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<WeaponSocket>() != null) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any || b.size.y < 0.05f) return;
            Character.transform.localScale *= CharacterHeight / b.size.y;
        }

        // ── 틱 ─────────────────────────────────────────────────────────
        void Update()
        {
            if (Character == null) return;
            if (!_reconciled) Reconcile();
            if (Scrubbing) return;   // 되감기 중엔 살아 있는 것은 멈춰 있다(timeScale 0) — 슬라이더가 그린다
            float dt = Time.deltaTime;
            TickMuzzle();
            TickProjectiles(dt);
            TickNade();
            TickDummy();

            var st = Stats;
            if (st == null || !CanFire) return;
            Aim();
            bool firing = autoFire && _deadUntil < 0f;

            // 화염방사기는 발사 간격이 아니라 '쏘는 중인가' 로 지속음을 켜고 끈다(ProtoShooter 와 같다)
            if (st.mode == FireMode.Cone) ProtoAudio.SetLoop(st.SfxKey, sound && firing);

            // 주기 폭격(융단 폭격 로켓) — 주무기 발사와 따로 돈다
            if (airstrike && st.airstrikeEvery > 0f && firing && Time.time >= _nextStrike)
            {
                _nextStrike = Time.time + st.airstrikeEvery;
                ProtoAirstrike.Strike(_root.position, st.airstrikeDamage, WeaponFxSet.Of(st));
            }

            if (!firing || Time.time < _nextFire) return;
            Fire(st);
        }

        void LateUpdate()
        {
            if (Character == null) return;
            // 애니 상태(WeaponVisibility)가 무기를 숨겨도 무대에선 늘 보이게 — 무기를 보는 자리다
            if (_socket != null && !_socket.IsShown) _socket.Show(true);
            if (Scrubbing) return;
            TickMuzzle();      // 애니메이터가 본을 움직인 뒤 총구를 다시 잡는다(무기 자식으로 붙었으면 아무 일도 안 한다)
            TickAimTwist();
            TickPendingShot();
            TickRecord();
        }

        // ── 한 발 녹화 · 되감기 ───────────────────────────────────────────
        /// 한 발을 녹화한다 — 연사를 끄고 더미를 세운 뒤 한 발 쏘며 recordSeconds 동안 기록. 끝나면 되감기(스크럽)로 들어간다.
        public bool BeginRecord()
        {
            if (Character == null || !CanFire || Recording) return false;
            if (Scrubbing) EndScrub();
            autoFire = false;
            if (_deadUntil >= 0f) Respawn();
            _rec = new ShotRecord { start = Time.time, length = Mathf.Max(0.2f, recordSeconds) };
            Recording = true;
            if (_anim != null) _anim.StartRecording(0);   // 0 = StopRecording 까지
            FireOnce();
            return true;
        }

        void TickRecord()
        {
            if (!Recording || _rec == null) return;
            float t = Time.time - _rec.start;
            _rec.twist.Add(new Vector2(t, _aimTwist));
            if (t < _rec.length) return;

            Recording = false;
            if (_anim != null)
            {
                _anim.StopRecording();
                _rec.animStart = _anim.recorderStartTime;
                _animModeBefore = _anim.updateMode;
                _anim.updateMode = AnimatorUpdateMode.UnscaledTime;   // 무대를 멈춰도(timeScale 0) playbackTime 이 그려지게
                _anim.StartPlayback();
            }
            Scrubbing = true;
            if (FxShowcase.Instance != null) FxShowcase.Instance.SetPaused(true);
            SetScrub(_rec.fireAt >= 0f ? _rec.fireAt : 0f);
        }

        /// 되감기 시각을 옮긴다(초, 트리거 = 0). 포즈 · 몸 틀기 · 프리팹 효과 · 총알 · 더미(플래시 · 처치)가 그 시각으로 간다.
        public void SetScrub(float t)
        {
            if (!Scrubbing || _rec == null) return;
            t = Mathf.Clamp(t, 0f, _rec.length);
            ScrubTime = t;
            if (_scrubRoot == null) { _scrubRoot = new GameObject("_WeaponScrub").transform; _scrubRoot.SetParent(transform, false); }

            if (_anim != null) _anim.playbackTime = Mathf.Clamp(_rec.animStart + t, _anim.recorderStartTime, _anim.recorderStopTime);
            if (_visual != null) _visual.localRotation = Quaternion.Euler(0f, TwistAt(t), 0f);

            foreach (var e in _rec.fx)
            {
                if (e.prefab == null) continue;   // 코드 그림 — 되감지 못한다
                if (t < e.t) { if (e.inst != null) e.inst.SetActive(false); continue; }
                if (e.inst == null) { e.inst = Instantiate(e.prefab, _scrubRoot); e.inst.name = "되감기 " + e.id; }
                e.inst.SetActive(true);
                e.inst.transform.SetPositionAndRotation(e.pos, e.rot);
                e.inst.transform.localScale = e.prefab.transform.localScale * e.scale;
                Sim(e.inst, t - e.t);
            }
            foreach (var b in _rec.bullets)
            {
                bool on = t >= b.t && t < b.end;
                if (!on) { if (b.inst != null) b.inst.SetActive(false); continue; }
                if (b.inst == null) b.inst = MakeScrubBullet(b);
                b.inst.SetActive(true);
                float age = t - b.t, traveled = b.spec.speed * age;
                b.inst.transform.SetPositionAndRotation(b.origin + b.dir * traveled, Quaternion.LookRotation(b.dir));
                if (b.model) Sim(b.inst, age);
                else
                {
                    // ProtoProjectile.StretchTracer 와 같다 — 꼬리는 날아간 거리만큼만
                    float baseS = b.spec.size, len = b.spec.tracerLength > 0f ? b.spec.tracerLength : 1f;
                    float full = baseS * len * 1.25f / 0.22f;
                    float k = Mathf.Clamp(traveled / Mathf.Max(0.01f, full), 0.08f, 1f);
                    float wid = baseS * (b.spec.tracerWidth > 0f ? b.spec.tracerWidth : 1f);
                    b.inst.transform.localScale = new Vector3(wid, wid, baseS * len * k);
                }
            }
            if (_dummy != null) _dummy.gameObject.SetActive(_rec.killAt < 0f || t < _rec.killAt);
            SetFlash(_rec.hitAt >= 0f && t >= _rec.hitAt && t < _rec.hitAt + ProtoConfig.HitFlashTime);
        }

        /// 되감기를 끝내고 살아 있는 무대로 돌아간다(연사는 꺼진 채 — 창에서 다시 켠다).
        public void EndScrub()
        {
            if (!Scrubbing) return;
            Scrubbing = false;
            if (_scrubRoot != null) { Destroy(_scrubRoot.gameObject); _scrubRoot = null; }
            if (_anim != null) { _anim.StopPlayback(); _anim.updateMode = _animModeBefore; }
            SetFlash(false);
            if (_dummy != null) _dummy.gameObject.SetActive(true);
            _deadUntil = -1f; _hp = dummyHp;
            if (FxShowcase.Instance != null) FxShowcase.Instance.SetPaused(false);
            _rec = null;
        }

        float TwistAt(float t)
        {
            var tw = _rec.twist;
            if (tw.Count == 0) return _aimTwist;
            for (int i = 1; i < tw.Count; i++)
                if (tw[i].x >= t)
                {
                    float span = tw[i].x - tw[i - 1].x;
                    float k = span > 0.0001f ? (t - tw[i - 1].x) / span : 1f;
                    return Mathf.LerpAngle(tw[i - 1].y, tw[i].y, k);
                }
            return tw[tw.Count - 1].y;
        }

        static void Sim(GameObject go, float age)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Simulate(Mathf.Max(0f, age), false, true);
        }

        GameObject MakeScrubBullet(BulletEvent b)
        {
            GameObject go;
            if (b.spec.model != null)
            {
                go = new GameObject("되감기 탄");
                go.transform.SetParent(_scrubRoot, false);
                var m = Instantiate(b.spec.model, go.transform);
                m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * b.spec.size;   // ProtoProjectile 과 같다(치명타 ×1.45 는 생략)
                b.model = true;
            }
            else
            {
                go = new GameObject("되감기 예광탄");
                go.transform.SetParent(_scrubRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = ProtoGunFx.TracerMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = ProtoGunFx.TracerMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                b.model = false;
            }
            return go;
        }

        /// 녹화 중이면 프리팹 효과를 기록한다(ProtoFx.Play 와 같은 자리 · 회전 · 배율). 코드 그림이면 수만 센다.
        void LogFx(string id, Vector3 pos, Quaternion rot, float scale)
        {
            if (!Recording || _rec == null || string.IsNullOrEmpty(id)) return;
            var cat = FxCatalog.Load();
            var e = cat != null ? cat.Find(id) : null;
            var prefab = e != null ? e.prefab : null;
            if (prefab == null) _rec.codeOnly++;
            _rec.fx.Add(new FxEvent { t = Time.time - _rec.start, id = id, prefab = prefab, pos = pos, rot = rot, scale = scale });
        }

        void QueueShot(Vector3 aim, ShotKind kind)
        {
            _pending = true; _pendingAim = aim; _pendingKind = kind; _pendingAt = Time.time;
            _pendingSawTransition = false;   // LateUpdate 에서 전환을 보고, 끝난 뒤에 낸다(ProtoShooter 와 같다)
        }

        void TickPendingShot()
        {
            if (!_pending) return;
            var st = Stats;
            if (st == null) { _pending = false; return; }
            if (!ProtoFireSync.ShouldRelease(_anim, _aimSettled, _pendingAt, ref _pendingSawTransition)) return;
            _pending = false;
            LastShotDelay = Time.time - _pendingAt;
            LastShotAnim = ProtoFireSync.Describe(_anim, LastShotDelay);
            if (Recording && _rec != null && _rec.fireAt < 0f) { _rec.fireAt = Time.time - _rec.start; _rec.fireAnim = LastShotAnim; }
            Vector3 aim = _pendingAim;
            Sfx(st);
            GunFx(st, aim);
            switch (_pendingKind)
            {
                case ShotKind.Shotgun: FireShotgun(st, aim); break;
                case ShotKind.Beam:    FireBeam(st, Jitter(st, aim)); break;
                default:               FireOne(st, Jitter(st, aim), Origin(), RollDamage(st), _lastCrit); break;
            }
        }

        /// 한 발(Space · 창의 [한 발]). 자동 사격과 무관하게 지금 바로.
        public void FireOnce()
        {
            var st = Stats;
            if (Character == null || st == null || !CanFire) return;
            if (_deadUntil >= 0f) Respawn();   // 더미가 사라진 동안 눌렀으면 바로 세운다
            Aim();
            Fire(st);
        }

        // ── 발사 — ProtoShooter.Update 의 switch 와 같은 분기 ────────────────
        void Fire(WeaponStats st)
        {
            _nextFire = Time.time + Mathf.Max(0.03f, st.interval / Mathf.Max(0.05f, fireRateMult));
            _lastFire = Time.time;
            Vector3 aim = AimDir();
            Shots++;
            switch (st.mode)
            {
                // 총구 화염 · 탄피 · 총알 · 소리는 사격 자세에 들어온 뒤(ProtoFireSync) — 트리거만 지금
                case FireMode.Shotgun:   FireAnim(); QueueShot(aim, ShotKind.Shotgun); break;
                case FireMode.Cone:      FireAnim(); FireCone(st); break;                                   // 몸 정면 부채꼴 — 그림과 판정이 같이 돈다
                case FireMode.Airstrike: ProtoAirstrike.Strike(_root.position, st.damage, WeaponFxSet.Of(st)); break;
                case FireMode.Grenade:   FireAnim(); ThrowNade(st); break;
                default:                 FireAnim(); QueueShot(aim, st.beam ? ShotKind.Beam : ShotKind.Single); break;   // 슬러그 레일 — 즉발 광선
            }
        }

        void FireAnim() { if (_anim != null) _anim.SetTrigger("Fire"); }
        void Sfx(WeaponStats st) { if (sound) ProtoAudio.Play(st.SfxKey, Origin()); }

        /// 총구 화염 + 탄피 — ProtoShooter.GunFx 와 같다. 어느 효과를 얼마나 크게는 무기 연출 세트가 정한다.
        void GunFx(WeaponStats st, Vector3 aim)
        {
            var fx = WeaponFxSet.Of(st);
            Vector3 o = Origin();
            if (fx.muzzle != null && fx.muzzle.On)
            {
                ProtoFx.Play(fx.muzzle.fxId, fx.MuzzlePos(o, aim), aim, fx.muzzle.scale, false);
                LogFx(fx.muzzle.fxId, fx.MuzzlePos(o, aim), Quaternion.LookRotation(aim), fx.muzzle.scale);
            }
            if (fx.casingScale > 0f)
            {
                Vector3 right = Vector3.Cross(Vector3.up, aim);
                Vector3 port = o - aim * fx.casingPortBack + Vector3.up * 0.03f;
                ProtoGunFx.EjectCasing(port, right, aim, fx.casingScale);
            }
        }

        Vector3 Jitter(WeaponStats st, Vector3 dir)
        {
            if (st.spreadDegrees <= 0.01f) return dir;
            return Quaternion.Euler(0f, Random.Range(-st.spreadDegrees, st.spreadDegrees) * 0.5f, 0f) * dir;
        }

        float RollDamage(WeaponStats st)
        {
            _lastCrit = Random.value < critChance;
            return _lastCrit ? st.damage * ProtoConfig.CritMultiplier : st.damage;
        }

        ProjectileSpec SpecOf(WeaponStats st) => new ProjectileSpec
        {
            speed = st.projectileSpeed, radius = st.projectileRadius, life = st.projectileLife, size = st.projectileSize,
            aoeRadius = st.aoeRadius, knockbackMult = st.knockbackMult, pierce = st.pierce, model = st.projectileModel,
            facesTravel = st.projectileFacesTravel, noShake = st.noShake,
            tracerColor = st.tracerColor, tracerLength = st.tracerLength, tracerWidth = st.tracerWidth,
            fx = WeaponFxSet.Of(st),
        };

        void FireOne(WeaponStats st, Vector3 dir, Vector3 origin, float damage, bool crit)
        {
            var p = _pool.Count > 0 ? _pool.Dequeue() : CreateBullet();
            var spec = SpecOf(st);
            p.Launch(origin, dir, damage, crit, spec);
            _live.Add(p);
            _hitDone.Remove(p);
            _shots[p] = new Shot { dmg = damage, crit = crit, dir = dir, spec = spec };
            if (Recording && _rec != null)
            {
                float t = Time.time - _rec.start;
                _rec.bullets.Add(new BulletEvent { t = t, end = t + spec.life, origin = origin, dir = dir, spec = spec, p = p });
            }
        }

        void FireShotgun(WeaponStats st, Vector3 dir)
        {
            int n = Mathf.Max(1, st.pellets);
            var origin = Origin();
            float step = n > 1 ? st.spreadDegrees / (n - 1) : 0f;
            float start = -st.spreadDegrees * 0.5f;
            float dmg = RollDamage(st);
            for (int i = 0; i < n; i++)
                FireOne(st, Quaternion.Euler(0f, start + step * i, 0f) * dir, origin, dmg, _lastCrit);
        }

        /// 즉발 광선 — 슈터와 같은 판정(선에서 projectileRadius 안)으로 더미를 맞히고, 그림은 사거리 끝까지.
        void FireBeam(WeaponStats st, Vector3 dir)
        {
            Vector3 origin = Origin();
            Vector3 start = new Vector3(origin.x, 0f, origin.z);
            float len = st.range;
            float dmg = RollDamage(st);
            bool crit = _lastCrit;
            Vector3 v = DummyPos - start; v.y = 0f;
            float t = Vector3.Dot(v, dir);
            if (_deadUntil < 0f && t >= 0f && t <= len && (v - dir * t).sqrMagnitude <= st.projectileRadius * st.projectileRadius)
            {
                Hits++;
                Vector3 at = start + dir * t + Vector3.up * origin.y;   // 광선 위에서 더미와 가장 가까운 점 = 착탄점
                DummyHit(dmg, dir * st.knockbackMult, crit, false, HitFx.From(WeaponFxSet.Of(st), SurfacePoint(at)));
            }
            ProtoBeamFx.Play(origin, origin + dir * len, st.tracerColor, crit);
        }

        /// 화염 — 게임과 같은 화염 연출(세트 jet 슬롯이 있으면 ProtoFlamePack, 아니면 ProtoFlameCone)을 총구에 붙이고 틱마다 Pulse. 판정 꼭짓점은 캐릭터, 몸 정면 부채꼴(ProtoShooter.FireCone 과 같다).
        void FireCone(WeaponStats st)
        {
            var set = WeaponFxSet.Of(st);
            bool pack = set != null && set.jet != null && set.jet.On;
            if (_flame != null && _flamePack != pack) { Destroy(_flameGo); _flame = null; _flameGo = null; }   // 창에서 슬롯을 바꾸면 갈아 끼운다
            if (_flame == null)
            {
                _flamePack = pack;
                _flameGo = new GameObject(pack ? "_FlamePack" : "_FlameCone");
                _flameGo.transform.SetParent(_root, false);
                _flame = pack ? (IProtoFlameFx)_flameGo.AddComponent<ProtoFlamePack>() : _flameGo.AddComponent<ProtoFlameCone>();
            }
            _flame.Configure(st, _muzzle);
            _flame.Pulse();

            Vector3 dir = _root.forward;
            Vector3 to = DummyPos - _root.position; to.y = 0f;
            if (_deadUntil >= 0f || to.sqrMagnitude > st.range * st.range) return;
            float cosHalf = Mathf.Cos(st.coneDegrees * 0.5f * Mathf.Deg2Rad);
            if (to.sqrMagnitude > 0.0001f && Vector3.Dot(to.normalized, dir) < cosHalf) return;
            Hits++;
            // 연출은 화염 세트대로(기본은 불똥 없음 — 틱마다 튀면 소음), 자리는 몸 가운데
            DummyHit(RollDamage(st), to.sqrMagnitude > 0.0001f ? to.normalized * st.knockbackMult : Vector3.zero, false, false,
                     HitFx.From(WeaponFxSet.Of(st), DummyPos + Vector3.up * 0.9f));
        }

        // ── 수류탄 — ProtoSupportGear 의 투척을 흉내 낸다(손에서 더미 발밑으로 포물선, 떨어지면 explode 슬롯 + 처치) ──
        void ThrowNade(WeaponStats st)
        {
            if (_nade == null)
            {
                _nade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _nade.name = "_StageNade";
                Destroy(_nade.GetComponent<Collider>());
                _nade.transform.localScale = Vector3.one * 0.18f;
                var mr = _nade.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                var olive = new Color(0.3f, 0.36f, 0.22f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", olive); else m.color = olive;
                mr.sharedMaterial = m;
            }
            _nadeFrom = Origin();
            _nadeTo = DummyPos;
            _nadeStart = Time.time;
            _nadeAt = Time.time + NadeFlight;
            _nade.transform.position = _nadeFrom;
            _nade.SetActive(true);
        }

        void TickNade()
        {
            if (_nadeAt < 0f || _nade == null) return;
            float k = Mathf.Clamp01((Time.time - _nadeStart) / NadeFlight);
            _nade.transform.position = Vector3.Lerp(_nadeFrom, _nadeTo, k) + Vector3.up * (4f * NadeArc * k * (1f - k));
            if (Time.time < _nadeAt) return;
            _nadeAt = -1f;
            _nade.SetActive(false);
            var st = Stats;
            if (st == null) return;
            var set = WeaponFxSet.Of(st);
            float radius = st.aoeRadius * st.RadiusMultAtLevel(1);
            if (set.explode != null && set.explode.On)
            {
                var c = FxContext.At(_nadeTo);
                c.radius = radius; c.scale = set.explode.scale; c.big = true;
                ProtoFx.Play(set.explode.fxId, c);
            }
            Vector3 d = DummyPos - _nadeTo; d.y = 0f;
            if (_deadUntil < 0f && d.sqrMagnitude <= radius * radius)
            {
                Hits++;
                DummyHit(st.damage, (d.sqrMagnitude > 0.0001f ? d.normalized : _root.forward) * st.knockbackMult, false, true, HitFx.Blast(set));
            }
        }

        // ── 투사체 ───────────────────────────────────────────────────────
        ProtoProjectile CreateBullet()
        {
            if (_poolRoot == null) { _poolRoot = new GameObject("_StageBulletPool").transform; }
            var go = new GameObject("Bullet");
            go.transform.localScale = Vector3.one * 0.22f;
            go.AddComponent<MeshFilter>().sharedMesh = ProtoGunFx.TracerMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ProtoGunFx.TracerMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var p = go.AddComponent<ProtoProjectile>();
            _all.Add(p);
            return p;
        }

        void TickProjectiles(float dt)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                if (p == null) { _live.RemoveAt(i); continue; }
                bool alive = p.Tick(dt, null);   // 좀비 목록 없이 날기만 — 더미 판정은 아래서
                bool had = _shots.TryGetValue(p, out var s);
                if (alive && had && !_hitDone.Contains(p)) alive = !TryHitDummy(p, s, dt);
                if (!alive)
                {
                    // 더미가 서 있는데 한 번도 못 맞히고 수명을 다했다 — 조준·총구 자리가 어긋난 신호. 3발 넘게 쌓이면 상태 줄에 알린다
                    if (had && !_hitDone.Contains(p) && _deadUntil < 0f)
                    {
                        Misses++;
                        if (Hits == 0 && Misses >= 3) Status = $"총알 {Misses}발이 더미를 지나쳤다 — 총구 높이·자리(shooterOffset)와 투사체 반경을 볼 것";
                    }
                    _shots.Remove(p);
                    _live.RemoveAt(i);
                    p.gameObject.SetActive(false);
                    if (_poolRoot != null) p.transform.SetParent(_poolRoot, true);
                    _pool.Enqueue(p);
                }
            }
        }

        /// ProtoProjectile.Tick 의 명중 판정과 같다(가슴 높이 0.9, 반경 + 한 틱 이동의 반). 맞았으면 true — 관통이면 계속 난다.
        bool TryHitDummy(ProtoProjectile p, Shot s, float dt)
        {
            if (_deadUntil >= 0f) return false;   // 더미가 사라진 동안은 지나간다
            float step = s.spec.speed * dt;
            float r = s.spec.radius + step * 0.5f;
            Vector3 d = DummyChest - p.transform.position;
            if (d.sqrMagnitude > r * r) return false;
            Hits++;
            if (Recording && _rec != null)
            {
                float now = Time.time - _rec.start;
                if (_rec.hitAt < 0f) _rec.hitAt = now;
                var b = _rec.bullets.Find(x => x.p == p);
                if (b != null && (s.spec.aoeRadius > 0.01f || s.spec.pierce <= 0)) b.end = now;
            }
            if (s.spec.aoeRadius > 0.01f) { Explode(s, p.transform.position, p.Traveled); return true; }
            DummyHit(s.dmg, s.dir * s.spec.knockbackMult, s.crit, false, HitFx.From(s.spec.fx, SurfacePoint(p.transform.position)));   // 몸 표면으로 당긴다(ProtoZombie.SurfacePoint 와 같다)
            _hitDone.Add(p);
            return s.spec.pierce <= 0;
        }

        /// 착탄 폭발 — ProtoProjectile.Explode 와 같다(explode 슬롯, radius = 판정 반경, big, 탱크 포는 quiet).
        void Explode(Shot s, Vector3 center, float traveled)
        {
            var set = s.spec.fx != null ? s.spec.fx : WeaponFxSet.Default;
            if (set.explode != null && set.explode.On)
            {
                var c = FxContext.At(center);
                float scale = set.explode.scale * set.ExplodeDistanceScale(traveled);   // 가까이서 터지면 작게(세트 explodeNearScale)
                c.radius = s.spec.aoeRadius; c.scale = scale; c.big = true; c.quiet = s.spec.noShake;
                ProtoFx.Play(set.explode.fxId, c);
                LogFx(set.explode.fxId, center, Quaternion.identity, scale);
            }
            Vector3 d = DummyPos - center; d.y = 0f;
            if (d.sqrMagnitude > s.spec.aoeRadius * s.spec.aoeRadius) return;
            Vector3 push = d.sqrMagnitude > 0.0001f ? d.normalized : s.dir;
            DummyHit(s.dmg, push * s.spec.knockbackMult, s.crit, true, HitFx.Blast(set));   // 큰 한 방 — 자리 불똥 없음
        }

        // ── 더미 — ProtoZombie.DealDamage / Die 의 연출 부분만 ──────────────
        Vector3 DummyPos => _dummy != null ? _dummy.position : (_stage != null ? _stage.position : Vector3.zero);
        Vector3 DummyChest => DummyPos + Vector3.up * 0.9f;

        /// 바깥의 점을 더미 몸 표면(가슴 중심, 반경 = 잡좀비 몸 반경) 안으로 당긴다 — ProtoZombie.SurfacePoint 와 같은 규칙.
        Vector3 SurfacePoint(Vector3 from)
        {
            Vector3 d = from - DummyChest;
            float m = d.magnitude, r = ProtoConfig.BaseBodyRadius;
            return m > r && m > 0.0001f ? DummyChest + d * (r / m) : from;
        }

        void DummyHit(float amount, Vector3 hitDir, bool crit, bool bigHit, HitFx fx)
        {
            if (_deadUntil >= 0f) return;
            if (dummyFlash) { _flashUntil = Time.time + ProtoConfig.HitFlashTime; SetFlash(true); }

            // 피격 불똥 — 세트의 ID 와 정확한 착탄점, 안 넘어왔으면 공통 hit_spark 를 가슴 높이 맞은 쪽에. 큰 한 방은 공통 불똥 없음. 방향 = 날아온 쪽
            string sparkId = fx.specified ? fx.spark : (bigHit ? null : FxIds.HitSpark);
            if (!string.IsNullOrEmpty(sparkId))
            {
                Vector3 back = hitDir.sqrMagnitude > 0.0001f ? -hitDir : Random.insideUnitSphere;
                back.y = 0f;
                back = back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.back;
                Vector3 at = fx.specified && fx.hasPoint ? fx.point : DummyPos + Vector3.up * 0.95f + back * (ProtoConfig.BaseBodyRadius * 0.6f);
                float sc = (fx.specified ? fx.sparkScale : 1f) * (crit ? 1.5f : 1f);
                ProtoFx.Play(sparkId, at, back, sc, crit);
                LogFx(sparkId, at, Quaternion.LookRotation(back), sc);
            }
            if (Recording && _rec != null && _rec.hitAt < 0f) _rec.hitAt = Time.time - _rec.start;   // 광선 · 화염처럼 총알 없이 맞는 경로
            if (sound) ProtoAudio.Play(crit ? "crit" : "hit", DummyPos);
            if (crit) ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Light);

            _hp -= amount;
            if (_hp > 0f) return;
            Kill(fx, hitDir);
        }

        void Kill(HitFx fx, Vector3 hitDir = default)
        {
            Kills++;
            // 처치 공통 폭발 — 게임(ProtoZombie.Die)과 같은 함수: 카탈로그 fx_zombie_death 에 프리팹이 있으면 몸 가운데에 몸 크기대로(2026-11-13).
            // 몸 조각보다 먼저 — 몸이 보일 때 키를 잰다. 아래 세트 처치 효과 · 몸 조각 · 피도 같이 난다
            if (_dummy != null && _dummy.gameObject.activeInHierarchy &&
                ProtoZombie.PlayDeathFx(_dummy, _dummyRenderers, 1f, false, out var deathAt, out var deathSize))
                LogFx(FxIds.ZombieDeath, deathAt, Quaternion.identity, deathSize);
            // 몸 조각 + 피 터짐 — 게임(ProtoZombie.Die)과 같은 길. 조각은 코드 그림이라 타임라인 되감기엔 안 보인다(수만 센다), 피는 프리팹이라 보인다
            if (_dummy != null && _dummy.gameObject.activeInHierarchy &&
                ZombieGibs.Burst(_dummy.gameObject, DummyPos + Vector3.up * 0.8f, DummyPos.y, hitDir, 1f))
            {
                _goreHandle = ProtoFx.Play(FxIds.ZombieGore, DummyPos + Vector3.up * 0.8f, hitDir, 1f, false);
                _goreCheckAt = Time.time + 0.6f;
                LogFx(FxIds.ZombieGore, DummyPos + Vector3.up * 0.8f, hitDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(hitDir) : Quaternion.identity, 1f);
                if (Recording && _rec != null) _rec.codeOnly++;
            }
            // 처치 효과 — 세트의 ID, 몸 가운데, 덩치 1(잡좀비). 쓰러지는 애니 대신 잠깐 사라졌다 돌아온다
            string killId = fx.specified ? fx.kill : FxIds.KillBurst;
            if (!string.IsNullOrEmpty(killId))
            {
                float sc = fx.specified ? fx.killScale : 1f;
                ProtoFx.Play(killId, DummyPos + Vector3.up * 0.8f, Vector3.zero, sc, false);
                LogFx(killId, DummyPos + Vector3.up * 0.8f, Quaternion.identity, sc);
            }
            if (Recording && _rec != null && _rec.killAt < 0f) _rec.killAt = Time.time - _rec.start;
            if (sound) ProtoAudio.Play("zdie", DummyPos);
            SetFlash(false);
            if (_dummy != null) _dummy.gameObject.SetActive(false);
            _deadUntil = Time.time + respawnDelay;
        }

        void TickDummy()
        {
            if (_flashOn && Time.time >= _flashUntil) SetFlash(false);
            if (_deadUntil >= 0f && Time.time >= _deadUntil) Respawn();
            if (_goreCheckAt > 0f && Time.time >= _goreCheckAt) { _goreCheckAt = -1f; ReportGore(); }
        }

        /// 처치 0.6초 뒤 — 피 인스턴스가 살아 있고 바닥 입자가 몇 개인지. 창의 상태 줄과 콘솔에 적는다.
        void ReportGore()
        {
            var go = _goreHandle != null ? _goreHandle.Instance : null;
            if (go == null || !_goreHandle.Alive) { Status = "바닥 피 진단: zombie_gore 인스턴스가 0.6초 안에 사라졌다 — 카탈로그 수명(2.6초여야)을 볼 것"; Debug.LogWarning("[피] " + Status); return; }
            var g = go.transform.Find("GroundSplats");
            if (g == null) { Status = $"바닥 피 진단: 프리팹 {go.name} 에 GroundSplats 자식이 없다 — 옛 프리팹(S_VFX/Hit/FX_Hit_Gore)"; Debug.LogWarning("[피] " + Status); return; }
            int Count(Transform t) { var ps = t != null ? t.GetComponent<ParticleSystem>() : null; return ps != null ? ps.particleCount : -1; }
            Status = $"바닥 피 진단: 핏자국(시트) {Count(g)}개 · 단색 {Count(g.Find("GroundStains"))}개 · 웅덩이 {Count(g.Find("Pool"))}개 · 바닥 y {g.position.y:0.00} (더미 발 y {DummyPos.y:0.00}) · 인스턴스 {go.name} {(go.activeInHierarchy ? "활성" : "비활성")} · 루트 크기 {go.transform.localScale.x:0.00}";
            Debug.Log("[피] " + Status);
        }

        void Respawn()
        {
            _deadUntil = -1f;
            _hp = dummyHp;
            if (_dummy != null) _dummy.gameObject.SetActive(true);
        }

        void CacheDummy()
        {
            _dummyRenderers = _dummy != null ? _dummy.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            SetFlash(false);
        }

        /// 흰 플래시 — ProtoZombie.PushVisual 과 같은 방법(_EmissionColor 를 MPB 로). 끌 때는 블록을 비워 머티리얼 원래대로.
        void SetFlash(bool on)
        {
            _flashOn = on;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();   // 리로드 뒤엔 비어 있다
            if (_dummyRenderers == null) _dummyRenderers = new Renderer[0];
            if (on)
            {
                float v = ProtoConfig.HitFlashStrength;
                _mpb.SetColor(BaseColorID, Color.white);
                _mpb.SetColor(EmissionID, new Color(v, v, v, 1f));
            }
            for (int i = 0; i < _dummyRenderers.Length; i++)
                if (_dummyRenderers[i] != null) _dummyRenderers[i].SetPropertyBlock(on ? _mpb : null);
        }

        // ── 슬롯 재생 — 세트의 자리 하나만(창의 [총구 ▶] 등). 쏘지 못하는 무기도 이걸로 본다 ───────
        public void PlaySlot(SlotKind kind, float radius, bool big)
        {
            if (Character == null) return;
            var set = FxSet;
            var st = Stats;
            switch (kind)
            {
                case SlotKind.Muzzle:
                {
                    if (set.muzzle == null || !set.muzzle.On) { Status = "총구 슬롯이 비어 있다"; return; }
                    Vector3 o = Origin(), dir = MuzzleDir();
                    ProtoFx.Play(set.muzzle.fxId, set.MuzzlePos(o, dir), dir, set.muzzle.scale, big);
                    if (set.casingScale > 0f)
                        ProtoGunFx.EjectCasing(o - dir * set.casingPortBack + Vector3.up * 0.03f, Vector3.Cross(Vector3.up, dir), dir, set.casingScale);
                    break;
                }
                case SlotKind.Hit:
                {
                    if (set.hit == null || !set.hit.On) { Status = "피격 슬롯이 비어 있다(화염처럼 틱이 잦은 무기는 비운다)"; return; }
                    Vector3 back = -AimDir();
                    Vector3 at = DummyPos + Vector3.up * 0.95f + back * (ProtoConfig.BaseBodyRadius * 0.6f);
                    ProtoFx.Play(set.hit.fxId, at, back, set.hit.scale, big);
                    if (dummyFlash) { _flashUntil = Time.time + ProtoConfig.HitFlashTime; SetFlash(true); }
                    break;
                }
                case SlotKind.Kill:
                    if (set.kill == null || !set.kill.On) { Status = "처치 슬롯이 비어 있다"; return; }
                    ProtoFx.Play(set.kill.fxId, DummyPos + Vector3.up * 0.8f, Vector3.zero, set.kill.scale, big);
                    break;
                case SlotKind.Explode:
                {
                    if (set.explode == null || !set.explode.On) { Status = "폭발 슬롯이 비어 있다(aoe 무기만 쓴다)"; return; }
                    var c = FxContext.At(DummyPos);
                    c.radius = st != null && st.aoeRadius > 0.01f ? st.aoeRadius : radius;
                    c.scale = set.explode.scale; c.big = true; c.quiet = st != null && st.noShake;
                    ProtoFx.Play(set.explode.fxId, c);
                    break;
                }
            }
            Status = null;
        }

        // ── 조준 · 총구 — ProtoShooter 와 같은 규칙 ─────────────────────────
        void Aim()
        {
            Vector3 to = DummyPos - _root.position; to.y = 0f;
            if (to.sqrMagnitude > 0.0001f) _root.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        Vector3 AimDir()
        {
            Vector3 aim = DummyPos - _root.position; aim.y = 0f;
            return aim.sqrMagnitude > 0.0001f ? aim.normalized : _root.forward;
        }

        Vector3 Origin() => _muzzle != null ? _muzzle.position : _root.position + Vector3.up;

        Vector3 MuzzleDir()
        {
            if (_muzzle == null || !_muzzleFound) return _root.forward;
            Vector3 d = _muzzle.forward; d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : _root.forward;
        }

        /// 쏠 때 Visual 을 Y 로 돌려 총열이 조준 방향을 보게 — ProtoShooter.TickAimTwist 와 같다(소총 자세는 총열이 57° 왼쪽을 본다).
        void TickAimTwist()
        {
            if (_visual == null) return;
            bool firing = Time.time - _lastFire < AimHoldTime;
            float target = 0f;
            if (firing && _muzzleFound && _muzzle != null)
            {
                Vector3 barrel = _muzzle.forward; barrel.y = 0f;
                if (barrel.sqrMagnitude > 0.0001f)
                    target = Mathf.Clamp(_aimTwist - Vector3.SignedAngle(_root.forward, barrel, Vector3.up), -90f, 90f);
            }
            _aimTwist = Mathf.MoveTowardsAngle(_aimTwist, target, AimTwistSpeed * Time.deltaTime);
            _visual.localRotation = Quaternion.Euler(0f, _aimTwist, 0f);
            _aimSettled = Mathf.Abs(Mathf.DeltaAngle(_aimTwist, target)) <= ProtoFireSync.AimSettleDeg;
        }

        /// 총구 자리 — 무기 에셋의 muzzlePoint, 없으면 메시 바운드에서 추정. 찾으면 무기 자식으로 붙여 계층이 따라가게(ProtoShooter.TickMuzzle).
        void TickMuzzle()
        {
            if (_root == null) return;
            if (_muzzle == null)   // 무기를 갈아 끼우면 자식이던 총구도 같이 사라진다 — 매번 확인
            {
                _muzzle = new GameObject("MuzzleAuto").transform;
                _muzzle.SetParent(_root, false);
                _muzzleWeapon = null; _muzzleParented = false; _muzzleFound = false;
            }
            Transform w = _socket != null && _socket.transform.childCount > 0 ? _socket.transform.GetChild(0) : null;
            if (w == _muzzleWeapon) { if (!_muzzleParented) PlaceFallbackMuzzle(); return; }

            _muzzleWeapon = w;
            var def = _socket != null ? _socket.weapon : null;
            if (w != null && def != null && def.hasMuzzle) { _muzzleLocal = def.muzzlePoint; _muzzleFound = true; }
            else
            {
                Vector3 grip = w != null && w.parent != null ? w.InverseTransformPoint(w.parent.position) : Vector3.zero;
                _muzzleFound = w != null && MuzzleFinder.TryFind(w, grip, false, out _muzzleLocal);
            }
            _muzzleParented = false;
            if (_muzzleFound)
            {
                _muzzle.SetParent(w, false);
                _muzzleParented = _muzzle.parent == w;
                _muzzleRot = Quaternion.LookRotation(MuzzleFinder.BarrelAxis(w, _muzzleLocal), Vector3.up);
                if (_muzzleParented) { _muzzle.localPosition = _muzzleLocal; _muzzle.localRotation = _muzzleRot; }
            }
            else
            {
                _muzzle.SetParent(_root, false);
                _muzzle.localRotation = Quaternion.identity;
            }
            PlaceFallbackMuzzle();
            if (_flame != null && Stats != null) _flame.Configure(Stats, _muzzle);
        }

        void PlaceFallbackMuzzle()
        {
            if (_muzzle == null || _muzzleParented) return;
            if (_muzzleFound && _muzzleWeapon != null)
            {
                _muzzle.position = _muzzleWeapon.TransformPoint(_muzzleLocal);
                _muzzle.rotation = _muzzleWeapon.rotation * _muzzleRot;
            }
            else _muzzle.localPosition = new Vector3(0f, 1.0f, 0f);
        }
    }
}
