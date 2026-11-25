using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 슈터가 조준 방향을 넘기는 대상. 몸을 돌리는 것은 이동을 소유한 쪽이다(ProtoPlayer 주석).
    public interface IProtoAimer
    {
        void SetAimDir(Vector3 dir);
    }

    /// <summary>
    /// 주무기 런타임 (GDD §03: 조작은 이동뿐, 사격은 완전 자동 / §05: 주무기는 캐릭터에 고정).
    ///
    /// <para>수치는 <b>장착된 무기의 <see cref="WeaponStats"/></b> 에서 읽는다. 예전에는 전부
    /// <see cref="ProtoConfig"/> 전역 상수였고, 그래서 캐릭터를 바꿔도 소총처럼 쐈다.
    /// 무기가 없거나 수치 에셋이 안 물려 있으면 그 전역값으로 떨어진다(런이 멈추지 않게).</para>
    ///
    /// <para>회전 블레이드·지원 드론은 <b>여기 없다</b> — 주무기가 아니라 카드로 얻는 보조 장비이므로
    /// <see cref="ProtoSupportGear"/> 가 굴린다. 대신 투사체 풀은 여기 하나뿐이라
    /// 소환물도 <see cref="FireExternal"/> 로 이 풀을 빌려 쓴다.</para>
    /// </summary>
    public class ProtoShooter : MonoBehaviour
    {
        static readonly int AnimFire = Animator.StringToHash("Fire");   // 2026-11-20
        public Transform muzzle;

        ProtoPlayer _player;
        IProtoAimer _aimer;     // 몸을 돌리는 쪽(ProtoPlayer)
        Animator _anim;
        WeaponSocket _socket;
        float _nextFire;
        float _nextStrike;   // 주기 폭격(WeaponStats.airstrikeEvery) 다음 회차

        readonly Queue<ProtoProjectile> _pool = new Queue<ProtoProjectile>();
        readonly List<ProtoProjectile> _live  = new List<ProtoProjectile>();
        Transform _poolRoot;

        WeaponStats _fallback;      // 수치 에셋이 없을 때 쓰는 ProtoConfig 사본
        WeaponStats _applied;       // 마지막으로 셋업한 무기 (진화로 바뀌면 타이머를 리셋한다)

        // 화염 연출은 구현이 둘이다(파티클/스프라이트). 옵션으로 갈아 끼우므로
        // 인터페이스로 들고, 지울 때 쓸 GameObject 를 따로 기억한다.
        IProtoFlameFx _flame;
        GameObject _flameGo;
        int _flameStyle = -1;

        // ── 총구 자리 ────────────────────────────────────────────────────
        // 인스펙터의 muzzle 은 Player 에 손으로 박아 둔 고정점이라 **무기와 무관하다**.
        // 실제로 재 보니 y=1.050 으로 Head 본(y=1.040)과 같은 높이였다 — 총알은 작고 빨라서
        // 아무도 눈치 못 챘지만, 화염처럼 굵고 이어지는 것이 나가면 입에서 뿜는 것처럼 보인다.
        // 그래서 **장착된 무기 모델에서 총열 끝을 직접 찾는다.**
        Transform _autoMuzzle;      // 그 자리를 들고 있는 빈 오브젝트. 연출은 이걸 따라간다
        Transform _muzzleWeapon;    // 마지막으로 총구를 계산한 무기 오브젝트
        Vector3 _muzzleLocal;       // 그 무기 로컬 공간에서의 총열 끝
        Quaternion _muzzleRot = Quaternion.identity;   // 그 무기 로컬 공간에서 총열 방향(+Z)
        bool _muzzleFound;
        bool _muzzleParented;   // 무기 자식으로 붙었나 (에디터에선 프리팹 인스턴스라 막힌다)

        // ── 화염 연료 (§05 파이로) ───────────────────────────────────────
        // 상태를 무기 에셋이 아니라 여기 두는 이유: 에셋은 **여러 캐릭터가 공유하는
        // 수치**다. 남은 연료를 거기 적으면 파이로가 쓰던 잔량이 다음 판까지 따라온다.
        float _fuel;        // 남은 분사 가능 시간(초)
        float _lockUntil;   // 이 시각까지 강제 대기. 0 이면 잠기지 않은 상태

        /// <summary>지금 장착한 무기가 연료를 쓰는가. HUD 가 표시 여부를 이걸로 정한다.</summary>
        public bool UsesFuel => Stats.HasFuel;

        /// <summary>남은 연료 0~1. 강제 대기 중에는 대기가 얼마나 찼는지를 돌려준다
        /// — 게이지가 비어 있기만 하면 "언제 다시 쏘는지" 를 알 수 없다.</summary>
        public float FuelNormalized
        {
            get
            {
                var st = Stats;
                if (!st.HasFuel) return 1f;
                if (IsRefueling)
                {
                    float total = Mathf.Max(0.1f, st.refuelSeconds);
                    return 1f - Mathf.Clamp01((_lockUntil - Time.time) / total);
                }
                return Mathf.Clamp01(_fuel / st.fuelSeconds);
            }
        }

        /// <summary>연료가 바닥나 강제로 쉬는 중인가.</summary>
        public bool IsRefueling => Time.time < _lockUntil;

        /// <summary>대기가 끝나기까지 남은 초.</summary>
        public float RefuelRemaining => Mathf.Max(0f, _lockUntil - Time.time);

        /// 현재 장착 무기의 수치. 화면 표시용으로도 쓰인다.
        public WeaponStats Stats
        {
            get
            {
                if (_socket != null && _socket.weapon != null && _socket.weapon.stats != null)
                    return _socket.weapon.stats;
                return Fallback;
            }
        }

        WeaponStats Fallback
        {
            get
            {
                if (_fallback != null) return _fallback;
                _fallback = ScriptableObject.CreateInstance<WeaponStats>();
                _fallback.name = "(ProtoConfig 전역값)";
                _fallback.mode     = FireMode.Single;
                _fallback.damage   = ProtoConfig.WeaponDamage;
                _fallback.interval = ProtoConfig.WeaponInterval;
                _fallback.range    = ProtoConfig.WeaponRange;
                return _fallback;
            }
        }

        void Awake()
        {
            _player = GetComponent<ProtoPlayer>();
            _aimer  = GetComponent<IProtoAimer>();
            RebindVisual();

            _poolRoot = new GameObject("_BulletPool").transform;
            // (예전 _bulletMat 은 만들기만 하고 아무도 안 썼다 — 탄은 ProtoGunFx.TracerMaterial. 2026-11-20 뺌)
        }

        /// Visual 자식 교체 후 캐시를 다시 잡는다 (ProtoPlayer.RebindVisual 과 같은 이유).
        public void RebindVisual()
        {
            _anim   = GetComponentInChildren<Animator>();
            _aimTwist = 0f;     // 새 Visual 은 돌려 놓은 적이 없다
            _socket = GetComponentInChildren<WeaponSocket>(true);
            _applied = Stats;
            _nextFire = 0f;
            ApplyWeapon(_applied);
        }

        /// 무기가 바뀌었을 때 이펙트를 맞춘다. 화염 콘은 Cone 무기일 때만 존재한다.
        void ApplyWeapon(WeaponStats st)
        {
            // 무기를 바꾸면 연료는 가득 찬 상태로 시작한다. 이월시키면 방금 집어든 무기가
            // 이유 없이 잠겨 있는 일이 생긴다.
            _fuel = st.HasFuel ? st.fuelSeconds : 0f;
            _lockUntil = 0f;
            _pending = false;   // 무기가 바뀌면 옛 무기로 줄 선 발사는 버린다

            if (st.mode != FireMode.Cone)
            {
                DropFlame();
                StopFireLoop();   // 화염에서 다른 무기로 갈아탔으면 지속음이 남아 있다
                return;
            }
            // 화염은 이제 파티클(Resources/VFX/FX_FlameJet)만 쓴다. 머티리얼 배선이 필요 없어서
            // 예전의 flameMaterial 게이트도 없앴다 — 프리팹이 없으면 ProtoFlameCone 이
            // 경고 한 줄만 남기고 판정만 돌린다.
            // 옵션을 바꿨으면 통째로 갈아 끼운다. 둘을 동시에 띄우면 불이 겹쳐 보인다.
            int style = DesiredFlameStyle(st);
            if (_flame != null && _flameStyle != style) DropFlame();

            if (_flame == null)
            {
                _flameStyle = style;
                _flameGo = new GameObject(style == 2 ? "_FlamePack" : style == 1 ? "_FlameSprite" : "_FlameCone");
                _flameGo.transform.SetParent(transform, false);
                _flame = style == 2 ? (IProtoFlameFx)_flameGo.AddComponent<ProtoFlamePack>()
                       : style == 1 ? (IProtoFlameFx)_flameGo.AddComponent<ProtoFlameSprite>()
                       : _flameGo.AddComponent<ProtoFlameCone>();
            }
            _flame.Configure(st, AutoMuzzle);
        }

        /// 이 무기가 쓸 화염 연출. 무기 연출 세트가 화염 줄기(jet, 외부 팩 프리팹)를 가리키면 옵션보다 먼저 —
        /// 그 모양 그대로 쓰는 ProtoFlamePack(2, 2026-11-05). 아니면 옵션(0 콘 · 1 스프라이트).
        static int DesiredFlameStyle(WeaponStats st)
        {
            var fxSet = WeaponFxSet.Of(st);
            return fxSet != null && fxSet.jet != null && fxSet.jet.On ? 2 : ProtoSettings.FlameStyle;
        }

        /// 붙어 있는 화염 연출을 떼어낸다. 남은 불은 오브젝트와 같이 사라진다.
        void DropFlame()
        {
            if (_flameGo != null) Destroy(_flameGo);
            _flameGo = null;
            _flame = null;
            _flameStyle = -1;
        }

        // 애니메이터가 본을 움직인 뒤에 총구를 다시 잡는다. 무기 자식으로 붙은 경우에는
        // 계층이 알아서 하므로 이 호출이 아무 일도 안 한다.
        void LateUpdate()
        {
            TickMuzzle();
            TickAimTwist();
            TickPendingShot();   // 애니메이터가 본을 움직이고 몸을 튼 뒤 — 총구가 제자리에 온 다음 낸다
        }

        // ── 줄 선 발사 (ProtoFireSync) ───────────────────────────────────
        // 트리거를 건 순간이 아니라 사격 자세 · 사격 상태에 들어온 뒤에 총구 화염 · 탄피 · 총알 · 소리를 낸다.
        enum ShotKind { Single, Shotgun, Beam }
        bool _pending;
        ShotKind _pendingKind;
        WeaponStats _pendingStats;
        Vector3 _pendingAim;
        float _pendingAt;
        bool _pendingSawTransition;
        bool _aimSettled = true;

        void QueueShot(WeaponStats st, Vector3 aim, ShotKind kind)
        {
            _pending = true; _pendingStats = st; _pendingAim = aim; _pendingKind = kind; _pendingAt = Time.time;
            _pendingSawTransition = false;   // LateUpdate 에서 애니메이터가 전환을 시작한 것을 보고, 끝난 뒤에 낸다
        }

        void TickPendingShot()
        {
            if (!_pending) return;
            if (!ProtoFireSync.ShouldRelease(_anim, _aimSettled, _pendingAt, ref _pendingSawTransition)) return;
            _pending = false;
            var st = _pendingStats;
            if (st == null) return;
            Vector3 aim = _pendingAim;
            FireSfx(st);
            GunFx(st, aim);
            switch (_pendingKind)
            {
                case ShotKind.Shotgun: FireShotgun(st, aim); break;
                case ShotKind.Beam:    FireBeam(st, Jitter(st, aim)); break;
                default:               FireOne(st, Jitter(st, aim), Origin(), RollDamage(st), _lastCrit); break;
            }
        }

        // ── 쏘는 동안 총열을 조준 방향에 맞추기 ───────────────────────────
        /// <summary>
        /// 쏠 때 캐릭터 모델(Visual)을 Y 축으로 돌려 <b>총열이 조준 방향(Player 정면)을 보게</b> 한다.
        ///
        /// <para>왜: Mixamo 소총 자세는 몸을 비스듬히 돌려 선다. 하체 Rifle Idle + 상체 사격 클립을 합치면
        /// 총열이 Player 정면에서 <b>57° 왼쪽</b>을 본다(2026-10-01 실측). 총알·화염은 조준 방향으로 나가니
        /// '총 옆에서 불이 나가는' 것처럼 보였다. 판정은 Player 정면 그대로이고 그림만 돌린다.</para>
        ///
        /// <para>재는 값이 이미 지금의 보정을 포함하므로(보정 후 총열 각도) 목표 = 지금 보정 − 남은 어긋남 으로
        /// 몇 프레임에 수렴한다. 쏘기를 멈추면 0 으로 돌아온다.</para>
        /// </summary>
        void TickAimTwist()
        {
            if (_anim == null) return;
            var visual = _anim.transform;
            bool firing = Time.time - _lastFireTime < AimHoldTime;
            float target = 0f;
            if (firing && _muzzleFound && _autoMuzzle != null)
            {
                Vector3 barrel = _autoMuzzle.forward; barrel.y = 0f;
                if (barrel.sqrMagnitude > 0.0001f)
                    target = Mathf.Clamp(_aimTwist - Vector3.SignedAngle(transform.forward, barrel, Vector3.up), -90f, 90f);
            }
            _aimTwist = Mathf.MoveTowardsAngle(_aimTwist, target, AimTwistSpeed * Time.deltaTime);
            visual.localRotation = Quaternion.Euler(0f, _aimTwist, 0f);
            _aimSettled = Mathf.Abs(Mathf.DeltaAngle(_aimTwist, target)) <= ProtoFireSync.AimSettleDeg;   // 줄 선 발사가 본다
        }

        /// 마지막 발사 뒤 이 시간 동안은 조준 자세(몸 돌림)를 유지한다. 연사 간격보다 길어야 사이에 몸이 안 풀린다.
        const float AimHoldTime = 0.45f;
        /// 몸을 돌리는 속도(도/초). 57° 를 0.1초 안에 — 화염은 총열이 정면 35° 안에 들어와야 켜진다.
        const float AimTwistSpeed = 600f;
        float _aimTwist;
        float _lastFireTime = -999f;

        void Update()
        {
            // 런이 멈추거나 죽었으면 화염 지속음도 같이 끊어야 한다. 여기서 그냥 return 하면
            // 판정은 멈췄는데 소리만 계속 지진다 (결과 화면에서 불 뿜는 소리가 남았다).
            if (ProtoRun.I == null || !ProtoRun.I.IsRunning || _player == null || !_player.IsAlive)
            {
                StopFireLoop();
                _pending = false;   // 죽거나 멈춘 뒤에 줄 선 발사가 나가면 안 된다
                return;
            }

            TickMuzzle();
            TickProjectiles();

            // 탱크 탑승 중(프로토 한정)에는 본체 주무기를 쉰다 — 탱크 포가 이 풀을 빌려 쏜다(FireExternal).
            if (ProtoTank.PlayerRiding) { StopFireLoop(); return; }
            if (Time.time < holdFireUntil) { StopFireLoop(); return; }   // 필사기 난사 중

            var st = Stats;
            // 첫 폭격은 무기를 받고 1초 뒤 — 진화하자마자 폭격이 보여야 "진화했다"로 읽힌다
            if (!ReferenceEquals(st, _applied)) { _applied = st; _nextFire = 0f; _nextStrike = Time.time + 1f; ApplyWeapon(st); }
            // 옵션에서 화염 연출을 바꿨으면 무기가 그대로여도 다시 붙여야 한다.
            // 2026-11-20 고침: 예전엔 옵션 값(0~1)과만 비교해, 화염 줄기(jet, 스타일 2) 무기는 매 프레임 다시 장착됐다 —
            // ApplyWeapon 이 연료를 가득 채우므로 화염방사기 · 인페르노 연료가 줄지 않았다(3초 · 6초 분사 / 2초 재충전이 동작 안 함).
            else if (st.mode == FireMode.Cone && _flameStyle != DesiredFlameStyle(st)) ApplyWeapon(st);

            // 보조 장비 모드(궤도·소환물)가 주무기 자리에 들어오면 아무것도 쏘지 않는다.
            // 캐릭터 에셋이 잘못 배선된 경우인데, 조용히 소총처럼 쏘는 것보다 안 쏘는 편이 눈에 띈다.
            if (st.IsSupportGear) return;

            // 조준 방향은 **매 프레임** 갱신한다. 예전에는 발사 순간에만 rotation 을 직접
            // 대입했고, 그래서 회전이 발사 간격만큼 끊겼다(로켓은 1.7초에 한 번 툭).
            // 실제로 몸을 돌리는 것은 ProtoPlayer 다 — 이동을 소유한 쪽이 회전도 소유한다.
            var target = FindTarget(transform.position, st.range);
            // 좀비가 모여 있는 승합차가 사거리 안이면 그 차를 먼저 노린다(ProtoDestructibles — 쏴서 터뜨리는 차)
            bool hasTarget = target != null;
            Vector3 targetPos = hasTarget ? target.transform.position : Vector3.zero;
            if (ProtoDestructibles.PreferredTarget(transform.position, st.range, out var vanAim))
            { hasTarget = true; targetPos = vanAim; }
            if (hasTarget && st.AimsAtTarget && _aimer != null)
                _aimer.SetAimDir(targetPos - transform.position);

            // 연료를 쓰는 무기면 먼저 연료를 굴린다. 잠겨 있으면 판정도 이펙트도 소리도 없다.
            bool fueled = !st.HasFuel || TickFuel(st, hasTarget);

            // 화염방사기는 **발사 간격이 아니라 '조준 중인가'** 로 소리를 켜고 끈다.
            // 0.2초마다 한 발씩 재생하면 지지는 소리가 잘게 끊긴다 (ProtoAudio.SetLoop 주석).
            if (st.mode == FireMode.Cone) ProtoAudio.SetLoop(st.SfxKey, hasTarget && fueled);

            // 주기 폭격 — 주무기 발사와 따로 돈다. 적이 없을 때는 미뤄 두었다가 적이 보이면 바로 부른다
            if (st.airstrikeEvery > 0f && hasTarget && Time.time >= _nextStrike)
            {
                _nextStrike = Time.time + st.airstrikeEvery;
                ProtoAirstrike.Strike(transform.position, st.airstrikeDamage, WeaponFxSet.Of(st));
            }

            if (!fueled || Time.time < _nextFire || !hasTarget) return;

            _nextFire = Time.time + Mathf.Max(0.03f, st.interval / Mathf.Max(0.05f, _player.AtkSpeed));

            Vector3 aim = targetPos - transform.position;
            aim.y = 0f;
            aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : transform.forward;

            switch (st.mode)
            {
                // 발사음은 **1회 발사당 한 번**이다. 산탄총을 펠릿마다 울리면 한 번 쏠 때
                // 5발이 겹쳐 총성이 찌그러지고, 보이스 상한까지 산탄총이 다 먹는다.
                // 총구 화염 · 탄피 · 총알 · 발사음은 사격 자세에 들어온 뒤(ProtoFireSync) — 트리거만 지금 건다
                case FireMode.Shotgun: FireAnim(); QueueShot(st, aim, ShotKind.Shotgun); break;
                // 화염 콘만 **몸이 향한 방향**으로 판정한다. 콘 메시는 루트의 자식이라 회전이
                // 따라오는 동안 조준 원본 방향으로 판정하면 그림과 판정이 어긋난다 —
                // 부채꼴은 그 어긋남이 눈에 바로 보인다("맞았는데 안 맞은 것처럼 보인다").
                // 투사체는 총구에서 떠나면 스스로 날아가므로 조준 방향을 그대로 쓴다.
                // 사격 자세도 튼다 — 안 틀면 대기 자세(총이 옆을 향함) 그대로 총구에서 불이 정면으로 나가 어긋나 보였다.
                // 0.2초마다 다시 걸려 사격 클립 앞부분(반동 전, 정면 조준)에 머문다.
                case FireMode.Cone:    FireAnim(); FireCone(st, transform.forward);   break;
                // 융단 폭격 무전기(로켓 발사기 진화) — 직접 쏘지 않고 헬기 한 회차를 부른다. 지점은 플레이어 둘레 좀비 쪽으로 붙는다
                case FireMode.Airstrike: ProtoAirstrike.Strike(transform.position, st.damage, WeaponFxSet.Of(st)); break;
                default:
                    FireAnim();
                    QueueShot(st, aim, st.beam ? ShotKind.Beam : ShotKind.Single);   // 슬러그 레일 — 즉발 광선
                    break;
            }
        }

        /// <summary>
        /// 연료를 한 프레임 굴린다. 반환값은 <b>지금 분사할 수 있는가</b>.
        ///
        /// <para>분사 중이면 줄고, 아니면 회복한다. 0 이 되면 <see cref="WeaponStats.refuelSeconds"/>
        /// 동안 잠기고 그 뒤 가득 찬다. <b>부분 회복으로는 잠금이 풀리지 않는다</b> —
        /// 바닥나자마자 조금 회복해서 찔끔 쏘고 다시 잠기면 화염이 딸꾹질하는 것처럼 된다.</para>
        ///
        /// <para>회복을 <see cref="ProtoConfig.Delta"/> 로 하는 이유는 나머지 전투 로직과
        /// 같은 시간을 써야 하기 때문이다. 프레임이 튀는 순간에만 연료가 더 빨리 닳으면
        /// 성능이 곧 밸런스가 된다.</para>
        /// </summary>
        bool TickFuel(WeaponStats st, bool wantFire)
        {
            if (Time.time < _lockUntil) return false;

            // 잠금이 방금 끝났다 — 가득 채우고 다시 쏠 수 있게 한다.
            if (_lockUntil > 0f)
            {
                _lockUntil = 0f;
                _fuel = st.fuelSeconds;
            }

            float dt = ProtoConfig.Delta;

            if (!wantFire)
            {
                _fuel = Mathf.Min(st.fuelSeconds, _fuel + Mathf.Max(0f, st.fuelRegenPerSecond) * dt);
                return true;
            }

            _fuel -= dt;
            if (_fuel > 0f) return true;

            _fuel = 0f;
            _lockUntil = Time.time + Mathf.Max(0.1f, st.refuelSeconds);
            return false;
        }

        /// <summary>
        /// 무엇이든 총에서 나가는 것의 출발점 — 총알·발사음·화염이 모두 이걸 쓴다.
        /// <see cref="AutoMuzzle"/> 가 무기 모델에서 찾은 총열 끝이고, 못 찾으면 인스펙터의
        /// <see cref="muzzle"/>, 그것도 없으면 발밑 1m 다.
        ///
        /// <para>예전에는 <see cref="muzzle"/> 을 바로 썼는데, 그 점이 <b>Head 본과 같은 높이</b>
        /// 였다(y=1.050 대 1.040). 총알은 작고 빨라서 아무도 눈치 못 챘지만 머리에서 나가고
        /// 있었다 — 화염을 붙이고 나서야 드러났다.</para>
        /// </summary>
        Vector3 Origin()
        {
            if (_autoMuzzle != null) return _autoMuzzle.position;
            if (muzzle != null) return muzzle.position;
            return transform.position + Vector3.up * 1.0f;
        }

        /// <summary>
        /// 총구 자리를 들고 있는 빈 오브젝트. 연출은 이걸 따라간다.
        /// </summary>
        Transform AutoMuzzle
        {
            get { EnsureAutoMuzzle(); return _autoMuzzle; }
        }

        /// 총구 자리를 들고 있는 빈 오브젝트를 보장한다. 무기를 갈아끼우면 예전 것이 무기와
        /// 함께 사라지므로 <b>매번 확인해야 한다</b>.
        void EnsureAutoMuzzle()
        {
            if (_autoMuzzle != null) return;
            var go = new GameObject("MuzzleAuto");
            go.transform.SetParent(transform, false);
            _autoMuzzle = go.transform;
            _muzzleWeapon = null;      // 새로 만들었으니 무기에 다시 붙여야 한다
            _muzzleParented = false;
            PlaceFallbackMuzzle();     // 무기를 찾기 전까지는 대비 자리에 둔다
        }

        /// <summary>
        /// 총구 자리를 갱신한다. 무기가 바뀐 프레임에만 일한다.
        ///
        /// <para><b>찾았으면 무기의 자식으로 붙인다.</b> 매 프레임 월드 좌표를 다시 잡는 방법도
        /// 되지만, 그러면 <c>Update</c> 에서 계산한 값을 애니메이터가 그 뒤에 덮어써서 총구가
        /// 한 프레임 늦는다. 자식으로 붙여 두면 트랜스폼 계층이 알아서 해 주므로 스크립트
        /// 실행 순서를 신경 쓸 필요가 없다.</para>
        ///
        /// <para>무기를 못 찾으면 인스펙터의 <see cref="muzzle"/>, 그것도 없으면 발밑 1m 로
        /// 떨어진다. 무기는 <see cref="WeaponSocket"/> 이 <b>런타임에</b> 붙이므로 처음 몇
        /// 프레임은 대비책으로 도는 게 정상이다.</para>
        /// </summary>
        void TickMuzzle()
        {
            EnsureAutoMuzzle();

            Transform w = _socket != null && _socket.transform.childCount > 0
                        ? _socket.transform.GetChild(0) : null;
            if (w == _muzzleWeapon)
            {
                // 무기 자식으로 붙었으면 계층이 알아서 한다. 아니면 매 프레임 따라가야 한다.
                if (!_muzzleParented) PlaceFallbackMuzzle();
                return;
            }

            _muzzleWeapon = w;
            // 무기 에셋에 저장된 총구가 있으면 그걸 쓴다(그립 툴의 빨간 마커로 맞춘 값). 없을 때만 추정한다.
            var def = _socket != null ? _socket.weapon : null;
            if (w != null && def != null && def.hasMuzzle) { _muzzleLocal = def.muzzlePoint; _muzzleFound = true; }
            else _muzzleFound = w != null && TryFindBarrelTip(w, out _muzzleLocal);
            _muzzleParented = false;

            if (_muzzleFound)
            {
                // 붙는 데 성공했는지 확인한다. 에디터에서는 무기가 프리팹 인스턴스 안이라
                // 재부모화가 조용히 거부된다 — 그걸 모르고 localPosition 을 넣으면
                // 총구가 엉뚱한 월드 좌표로 날아간다.
                _autoMuzzle.SetParent(w, false);
                _muzzleParented = _autoMuzzle.parent == w;
                // 총구의 정면(+Z)을 총열 방향으로 — 화염이 이 방향으로 나간다(몸 정면으로 내면 총이 옆을 볼 때 옆에서 뿜는다).
                _muzzleRot = Quaternion.LookRotation(MuzzleFinder.BarrelAxis(w, _muzzleLocal), Vector3.up);
                if (_muzzleParented)
                {
                    _autoMuzzle.localPosition = _muzzleLocal;
                    _autoMuzzle.localRotation = _muzzleRot;
                }
            }
            else
            {
                _autoMuzzle.SetParent(transform, false);
                _autoMuzzle.localRotation = Quaternion.identity;   // 총열을 모르면 몸 정면
            }
            PlaceFallbackMuzzle();

            // 총구가 옮겨 갔으니 연출에 다시 건네준다. 무기를 갈아끼우면 예전 MuzzleAuto 가
            // 무기와 함께 사라지므로, 이걸 빼먹으면 불이 발밑으로 떨어진다.
            if (_flame != null && _applied != null) _flame.Configure(_applied, _autoMuzzle);
        }

        /// <summary>
        /// 무기 자식으로 못 붙었을 때 총구 자리를 직접 잡는다. 총열은 찾았지만 붙이기만
        /// 실패했을 수도 있으므로, 그 경우에는 무기 좌표계로 환산해 따라간다.
        /// </summary>
        void PlaceFallbackMuzzle()
        {
            if (_autoMuzzle == null || _muzzleParented) return;
            if (_muzzleFound && _muzzleWeapon != null)
            {
                _autoMuzzle.position = _muzzleWeapon.TransformPoint(_muzzleLocal);
                _autoMuzzle.rotation = _muzzleWeapon.rotation * _muzzleRot;
            }
            else if (muzzle != null) _autoMuzzle.position = muzzle.position;
            else _autoMuzzle.localPosition = new Vector3(0f, 1.0f, 0f);
        }

        /// <summary>
        /// 무기 모델에서 총열 끝을 찾는다. <b>가장 긴 축이 총열</b>이라고 본다 — 총은 길쭉하다.
        ///
        /// <para>어느 쪽 끝이 총구인가는 <b>손에서 먼 쪽</b>으로 고른다. 처음에는 "캐릭터 정면으로
        /// 더 나간 쪽"으로 골랐는데 그러면 <b>자세에 따라 앞뒤가 뒤집힌다</b> — 바인드 포즈에서는
        /// 팔이 옆으로 뻗어 총열이 월드 X 를 향하고 있어서 정면 기준이 무의미했다. 손은 손잡이를
        /// 쥐고 있으므로 총구는 언제나 손에서 먼 쪽이고, 이 기준은 자세와 무관하다.</para>
        ///
        /// <para>정점을 다 훑지 않고 <b>메시 바운드의 모서리 8개</b>만 본다. 무기가 바뀔 때만
        /// 도는 코드지만 굳이 수만 정점을 읽을 이유가 없다.</para>
        /// </summary>
        bool TryFindBarrelTip(Transform w, out Vector3 local)
        {
            // 손이 쥔 지점을 무기 로컬 좌표로 환산한다. 소켓 원점이 곧 손이고,
            // WeaponModelDef.gripPoint 가 0 이 아니어도 이렇게 구하면 알아서 맞는다.
            // 정점은 안 읽는다 — 빌드에서 Read/Write 꺼진 메시면 실패한다. 정확한 값은 에셋의 muzzlePoint 몫이다.
            Vector3 grip = w.parent != null ? w.InverseTransformPoint(w.parent.position) : Vector3.zero;
            return MuzzleFinder.TryFind(w, grip, false, out local);
        }

        /// 총구 화염 + 탄피. <b>어느 효과를 얼마나 크게</b>는 무기 연출 세트(<see cref="WeaponFxSet"/>)가 정한다 —
        /// 예전의 "캐논이면 1.35, 샷건이면 1.25, 로켓은 없음" 코드 분기는 에셋 값으로 옮겼다.
        /// mult 는 필사기 난사처럼 순간적으로 키울 때(1 보다 크면 big 으로 넘긴다).
        void GunFx(WeaponStats st, Vector3 aim, float mult = 1f)
        {
            var fx = WeaponFxSet.Of(st);
            Vector3 o = Origin();
            if (fx.muzzle != null && fx.muzzle.On)
                ProtoFx.Play(fx.muzzle.fxId, fx.MuzzlePos(o, aim), aim, fx.muzzle.scale * mult, mult > 1f);   // 세트의 muzzleOffset 으로 자리 보정
            if (fx.casingScale > 0f)
            {
                // 배출구는 총구에서 총열을 따라 뒤로(casingPortBack) — 총 길이는 세트가 안다
                Vector3 right = Vector3.Cross(Vector3.up, aim);
                Vector3 port = o - aim * fx.casingPortBack + Vector3.up * 0.03f;
                ProtoGunFx.EjectCasing(port, right, aim, fx.casingScale);
            }
        }

        void FireAnim()
        {
            _lastFireTime = Time.time;
            if (_anim != null) _anim.SetTrigger(AnimFire);
        }

        /// 발사음. 소리 키는 무기 에셋이 정한다(<see cref="WeaponStats.SfxKey"/>) —
        /// 탕탕 소총과 탕탕 캐논은 둘 다 FireMode.Single 이라 모드만으로는 갈라지지 않는다.
        void FireSfx(WeaponStats st) => ProtoAudio.Play(st.SfxKey, Origin());

        /// 화염 지속음을 끊는다. 무기를 바꿀 때·죽을 때·컴포넌트가 꺼질 때 모두 필요하다.
        /// 키를 null 로 넘기는 것은 "무엇이 울고 있든 끈다" 는 뜻이다 — 무기가 이미 바뀐 뒤에
        /// 부르는 경로가 있어서, 끄려는 키를 맞춰 넘기려 하면 그 경로에서 소리가 남는다.
        void StopFireLoop() => ProtoAudio.SetLoop(null, false);

        void OnDisable() => StopFireLoop();

        // ── 단발 · 산탄 ─────────────────────────────────────────────────
        ProjectileSpec SpecOf(WeaponStats st) => new ProjectileSpec
        {
            speed = st.projectileSpeed,
            radius = st.projectileRadius,
            life = st.projectileLife,
            size = st.projectileSize,
            aoeRadius = st.aoeRadius,
            knockbackMult = st.knockbackMult,
            pierce = st.pierce,
            model = st.projectileModel,
            facesTravel = st.projectileFacesTravel,
            noShake = st.noShake,
            tracerColor = st.tracerColor,
            tracerLength = st.tracerLength,
            tracerWidth = st.tracerWidth,
            fx = WeaponFxSet.Of(st),
        };

        // 크리 여부를 밖으로 내보내는 이유는 총알 크기로만 표시하기 때문이다(§15: 데미지 숫자 없음).
        bool _lastCrit;
        float RollDamage(WeaponStats st)
        {
            _lastCrit = Random.value < _player.critChance;
            float dmg = st.damage * _player.damageMult;
            if (_lastCrit) dmg *= ProtoConfig.CritMultiplier;
            return dmg;
        }

        /// 단발 무기의 탄착군. 산탄과 달리 균등 배치가 아니라 무작위다 — 연사 무기라
        /// 매 발이 같은 자리로 가면 흩어지는 느낌이 안 난다(탕탕 캐논 스프레드 8°).
        Vector3 Jitter(WeaponStats st, Vector3 dir)
        {
            if (st.spreadDegrees <= 0.01f) return dir;
            float a = Random.Range(-st.spreadDegrees, st.spreadDegrees) * 0.5f;
            return Quaternion.Euler(0f, a, 0f) * dir;
        }

        void FireOne(WeaponStats st, Vector3 dir, Vector3 origin, float damage, bool crit = false)
        {
            var p = _pool.Count > 0 ? _pool.Dequeue() : CreateBullet();
            p.Launch(origin, dir, damage, crit, SpecOf(st));
            _live.Add(p);
        }

        /// <summary>
        /// 보조 장비(소환물)가 이 풀을 빌려 쏜다. 투사체 풀을 컴포넌트마다 따로 두면
        /// 같은 총알이 두 벌 생겨 상한 관리가 무의미해진다.
        /// </summary>
        public void FireExternal(WeaponStats st, Vector3 dir, Vector3 origin, float damage, bool crit = false)
            => FireOne(st, dir, origin, damage, crit);

        // ── 필사기 난사(브레이커 — ProtoUltimate) ───────────────────────
        /// 이 시각까지 평소 자동 사격을 쉰다. 필사기가 조준을 쥐고 있는 동안 평소 조준이 몸을 다른 쪽으로 돌리면 안 된다.
        [System.NonSerialized] public float holdFireUntil;

        /// 필사기 한 회차 — 몸을 dir 로 돌리고, 총구 화염·탄피·발사음과 함께 탄 count 발을 부채 spread° 안에 무작위로 흩뿌린다.
        public void UltVolley(WeaponStats st, Vector3 dir, int count, float spread, float damage, float flashScale)
        {
            if (_aimer != null) _aimer.SetAimDir(dir);
            FireAnim();
            ProtoAudio.Play(st.SfxKey, Origin());
            GunFx(st, dir, flashScale);   // 난사 회차는 세트의 총구 화염을 flashScale 배로
            var o = Origin();
            for (int i = 0; i < count; i++)
            {
                var d = Quaternion.Euler(0f, Random.Range(-0.5f, 0.5f) * spread, 0f) * dir;
                bool crit = Random.value < _player.critChance;
                FireOne(st, d, o, crit ? damage * ProtoConfig.CritMultiplier : damage, crit);
            }
        }

        // ── 즉발 광선 (슬러그 레일) ──────────────────────────────────────
        readonly List<ProtoZombie> _beamHits = new List<ProtoZombie>(16);
        Vector3 _beamStart, _beamDir;
        System.Comparison<ProtoZombie> _beamCmp;
        int BeamOrder(ProtoZombie a, ProtoZombie b)
            => Vector3.Dot(a.transform.position - _beamStart, _beamDir).CompareTo(Vector3.Dot(b.transform.position - _beamStart, _beamDir));

        /// <summary>
        /// 쏘는 순간 사거리 끝까지 직선 판정. 선에서 projectileRadius(+ 덩치 초과분) 안의 적을 가까운 순으로
        /// pierce+1 마리까지 같은 피해로 맞춘다. 승합차에 걸리면 광선이 거기서 끝난다(탄이 차에서 멈추는 것과 같다).
        /// 그림(<see cref="ProtoBeamFx"/>)은 판정과 같은 길이로 그린다.
        /// </summary>
        void FireBeam(WeaponStats st, Vector3 dir)
        {
            Vector3 origin = Origin();
            Vector3 start = new Vector3(origin.x, 0f, origin.z);
            float len = st.range;

            // 승합차 — 0.5m 간격으로 짚어 가다 처음 맞은 자리에서 끊는다
            float dmg = RollDamage(st);
            bool crit = _lastCrit;
            for (float s = 0.5f; s <= len; s += 0.5f)
                if (ProtoDestructibles.ProjectileHit(start + dir * s + Vector3.up * origin.y, st.projectileRadius, dmg)) { len = s; break; }

            _beamHits.Clear();
            var sd = ProtoSpawnDirector.I;
            if (sd != null)
            {
                var list = sd.Active;
                for (int i = 0; i < list.Count; i++)
                {
                    var z = list[i];
                    if (z == null || !z.Alive) continue;
                    Vector3 v = z.transform.position - start; v.y = 0f;
                    float t = Vector3.Dot(v, dir);
                    if (t < 0f || t > len) continue;
                    float r = st.projectileRadius + Mathf.Max(0f, z.bodyRadius - ProtoConfig.BaseBodyRadius);
                    if ((v - dir * t).sqrMagnitude > r * r) continue;
                    _beamHits.Add(z);
                }
                _beamStart = start; _beamDir = dir;
                if (_beamCmp == null) _beamCmp = BeamOrder;   // 정렬 비교를 한 번만 만든다 — 예전엔 쏠 때마다 람다 · 클로저(2026-11-20)
                _beamHits.Sort(_beamCmp);
                int max = Mathf.Min(_beamHits.Count, st.pierce + 1);
                var fxSet = WeaponFxSet.Of(st);
                for (int i = 0; i < max; i++)
                {
                    var z = _beamHits[i];
                    Vector3 v = z.transform.position - start; v.y = 0f;
                    Vector3 at = start + dir * Vector3.Dot(v, dir) + Vector3.up * origin.y;   // 광선 위에서 적과 가장 가까운 점 = 착탄점
                    z.TakeDamage(dmg, dir * st.knockbackMult, crit, false, HitFx.From(fxSet, z.SurfacePoint(at)));   // 몸 표면으로 당긴다
                }
            }

            ProtoBeamFx.Play(origin, origin + dir * len, st.tracerColor, crit);
        }

        void FireShotgun(WeaponStats st, Vector3 dir)
        {
            int n = Mathf.Max(1, st.pellets);
            var origin = Origin();
            // 펠릿을 부채꼴에 균등 배치한다. 난수로 뿌리면 발마다 명중 수가 흔들려
            // 산탄총이 "운에 따라 세지는 무기"가 된다 — DPS 기준선을 못 잡는다.
            float step = n > 1 ? st.spreadDegrees / (n - 1) : 0f;
            float start = -st.spreadDegrees * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var d = Quaternion.Euler(0f, start + step * i, 0f) * dir;
                FireOne(st, d, origin, RollDamage(st), _lastCrit);
            }
        }

        // ── 부채꼴 지속 (화염방사기) ────────────────────────────────────
        void FireCone(WeaponStats st, Vector3 dir)
        {
            if (_flame != null) _flame.Pulse();

            var sd = ProtoSpawnDirector.I;
            if (sd == null) return;

            float rSq = st.range * st.range;
            float cosHalf = Mathf.Cos(st.coneDegrees * 0.5f * Mathf.Deg2Rad);

            // 부채꼴의 꼭짓점은 **플레이어**다. 총열 끝으로 옮겨 보았지만 되돌렸다 —
            // 총은 몸 중심에서 0.46m 옆으로 치우쳐 쥐고 있어서, 거기를 꼭짓점으로 삼으면
            // **코앞의 좀비가 판정에서 빠진다**(1m 앞은 각도로 25°인데 반각이 15°다).
            // 그림과 판정의 어긋남은 대신 불줄기 길이를 줄여서 맞춘다
            // (<see cref="ProtoFlameCone"/> 의 reach).
            var list = sd.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                Vector3 to = z.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > rSq) continue;
                if (to.sqrMagnitude > 0.0001f && Vector3.Dot(to.normalized, dir) < cosHalf) continue;

                // 연출은 화염 무기의 세트대로(기본 세트는 불똥 없음 — 틱마다 튀면 소음). 자리는 몸 가운데
                z.TakeDamage(RollDamage(st),
                             to.sqrMagnitude > 0.0001f ? to.normalized * st.knockbackMult : Vector3.zero,
                             false, false, HitFx.From(WeaponFxSet.Of(st), z.transform.position + Vector3.up * 0.9f));
                // 화상은 적의 상태다 — 슈터가 목록으로 들고 있으면 무기를 바꾸는 순간 사라지고,
                // 파이로의 '불씨'(화상 적 처치 시 전파, §06)가 붙을 데도 없어진다.
                z.ApplyBurn(st.burnDuration, st.burnDps * _player.damageMult);
            }
            ProtoDestructibles.DamageInCone(transform.position, dir, st.range, cosHalf, RollDamage(st));
        }

        // ── 타게팅 ──────────────────────────────────────────────────────
        /// 사거리 내 최근접 적. 동거리면 체력이 낮은 쪽을 먼저 친다.
        ProtoZombie FindTarget(Vector3 origin, float range)
        {
            var dir = ProtoSpawnDirector.I;
            if (dir == null) return null;

            ProtoZombie best = null;
            float bestSq = range * range;
            float bestHP = float.MaxValue;
            var list = dir.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                float sq = (z.transform.position - origin).sqrMagnitude;
                if (sq > bestSq + 0.01f) continue;
                // 거의 같은 거리면 체력 낮은 쪽 우선
                if (sq < bestSq - 0.25f || (Mathf.Abs(sq - bestSq) <= 0.25f && z.hp < bestHP))
                {
                    bestSq = sq; bestHP = z.hp; best = z;
                }
            }
            return best;
        }

        // ── 투사체 풀 ───────────────────────────────────────────────────
        ProtoProjectile CreateBullet()
        {
            // 예전엔 노란 구였다. 방향이 없어 "떠다닌다"로 읽혀서 예광탄으로 바꿨다(ProtoGunFx, 2026-10-13).
            var go = new GameObject("Bullet");
            go.transform.localScale = Vector3.one * 0.22f;
            go.AddComponent<MeshFilter>().sharedMesh = ProtoGunFx.TracerMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ProtoGunFx.TracerMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.AddComponent<ProtoProjectile>();
        }

        void TickProjectiles()
        {
            var dir = ProtoSpawnDirector.I;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                if (p == null) { _live.RemoveAt(i); continue; }
                if (!p.Tick(ProtoConfig.Delta, dir))
                {
                    _live.RemoveAt(i);
                    p.gameObject.SetActive(false);
                    p.transform.SetParent(_poolRoot, true);
                    _pool.Enqueue(p);
                }
            }
        }
    }
}
