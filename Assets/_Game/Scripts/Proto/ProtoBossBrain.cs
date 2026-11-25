using UnityEngine;

namespace Proto
{
    /// 최종 보스(boss_02, 망치) 행동. 프로토 테스트용 (사용자 요청 2026-10-13).
    ///
    /// ProtoZombie 가 보스 몸(체력·피격·사망·밀어내기)을 그대로 맡고, 이 컴포넌트가 붙어 있으면
    /// **이동과 공격만** 여기로 넘긴다(ProtoZombie.Update → Tick). 잡좀비·중간 보스 경로는 그대로다.
    ///
    /// 패턴 두 개, 둘 다 기획서 §08 규칙대로 **예고 → 실행**(바닥 예고 원 0.8초 이상):
    ///  · 내려찍기 — 가까우면. 이 클립은 왼쪽 앞 대각선을 친다(콘티 2컷 "대각선으로 내려침").
    ///  · 도약 내려찍기 — 거리가 벌어지면. 플레이어 자리에 예고 원을 깔고 달려와 뛰어올라 찍는다.
    /// 연출은 콘티 4컷(준비 · 휘두르기 · 충돌 · 여운)을 따른다 — ProtoBossFx 참고.
    ///
    /// 시각·위치는 전부 클립을 샘플링해 잰 값이다(2026-10-13, 망치 머리·골반 궤적, 배율 1 기준).
    /// 클립을 바꾸면 다시 재야 한다 — 재는 법은 메모리 tangtang-boss-arena 참고.
    ///
    /// **연출 시각은 액션 FX 시트가 우선한다(2026-10-29).** Resources/Data/ActionFxSheet 에 "Boss_Final/Slam" ·
    /// "Boss_Final/JumpSlam" 이 있으면 콘티 4컷(먼지 · 망치 불 · 궤적 · 충돌)은 시트의 이벤트가 그리고, 피해 판정 · 종료
    /// 시각도 시트의 이름표(impact · end)에서 읽는다 — 아트가 에디터(Tools/좀비 오버런/FX 툴)에서 옮기면 판정이 따라온다.
    /// 시트가 없으면 아래 인스펙터 상수와 Phases() 로 예전처럼 돈다.
    public class ProtoBossBrain : MonoBehaviour
    {
        public Transform hammerHead;

        [Header("내려찍기 (Standing Melee Attack Downward, 2.27s)")]
        public float slamRange = 4.2f;   // 이 거리 안이면 내려찍는다
        [Tooltip("충돌 지점의 몸 기준 위치(x 오른쪽, z 앞). 이 클립은 왼쪽 앞 대각선을 친다")]
        public Vector2 slamOffset = new Vector2(-2.7f, 1.95f);
        public float slamRadius   = 2.6f;
        public float slamDamage   = 40f;   // 28 → 40 (2026-10-13 강화)
        public float slamCooldown = 1.0f;   // 2026-10-13 잠깐 0.5(공격 속도 2배) → 사용자 요청으로 원래대로
        [Tooltip("망치를 치켜들기 시작(초) — 불이 붙는다")]
        public float slamWindupAt = 0.30f;
        [Tooltip("내려치기 시작(초) — 궤적이 켜진다")]
        public float slamSwingAt  = 0.80f;
        [Tooltip("망치가 땅을 치는 시각(초)")]
        public float slamImpactAt = 0.95f;
        public float slamEndAt    = 1.85f;   // 여기서 추격으로 돌아간다

        [Header("도약 내려찍기 (Standing Melee Run Jump Attack, 3.67s)")]
        public float jumpMinDist = 4.5f;
        public float jumpMaxDist = 12f;
        [Tooltip("충돌 순간 망치 머리의 몸 기준 위치 — 몸은 이만큼 덜 날아간다")]
        public Vector2 jumpHeadOffset = new Vector2(-1.1f, 3.4f);
        public float jumpRadius   = 3.6f;
        public float jumpDamage   = 60f;   // 42 → 60 (2026-10-13 강화)
        public float jumpCooldown = 6f;     // 2026-10-13 잠깐 3(공격 속도 2배) → 사용자 요청으로 원래대로
        public float jumpWindupAt = 0.85f;   // 도약(골반이 뜨기 시작)
        public float jumpSwingAt  = 1.25f;   // 정점 직후 내려치기
        public float jumpImpactAt = 1.78f;
        public float jumpEndAt    = 3.2f;

        [Header("공통")]
        public float roarTime = 2.6f;   // 등장 포효(클립 2.83s) 동안 움직이지 않는다
        [Tooltip("공격 동작 재생 배속. 위의 시각(초)은 모두 1배속 클립 기준이고, 실제로는 이 배수만큼 빨리 지나간다. " +
                 "2026-10-13 사용자: 공격 속도 2배. 주의 — 내려찍기 예고가 0.95s → 0.48s 로 짧아져 기획서 §08 '예고 0.8s 이상' 보다 짧다")]
        public float attackSpeed = 1f;   // 2026-10-13 잠깐 2 → 사용자 요청으로 1(원래대로)

        // 도약 클립의 골반 전진량(정규화). 이 곡선대로 몸을 옮겨야 달리기·도약·착지 동작과 발이 맞는다.
        // 원래 전진 거리 5.93m(배율 1). 모양만 쓰고 거리는 착지점에 맞춰 늘이거나 줄인다.
        static readonly AnimationCurve JumpTravel = new AnimationCurve(
            new Keyframe(0.0f, 0f), new Keyframe(0.2f, 0.061f), new Keyframe(0.4f, 0.231f), new Keyframe(0.6f, 0.391f),
            new Keyframe(0.8f, 0.523f), new Keyframe(0.9f, 0.563f), new Keyframe(1.0f, 0.610f), new Keyframe(1.2f, 0.690f),
            new Keyframe(1.4f, 0.826f), new Keyframe(1.6f, 0.971f), new Keyframe(1.7f, 0.990f), new Keyframe(1.8f, 1f));

        enum State { Roar, Chase, Slam, Jump }
        State _state;
        float _t;                     // 현재 상태 경과
        float _nextSlam, _nextJump;   // Time.time 기준
        bool  _fxWindup, _fxSwing, _hit;
        Vector3 _from, _land, _impactPos;
        ProtoZombie _body;
        Animator _anim;
        Transform _target;
        ProtoBossFx.Telegraph _tele;

        // ── 액션 FX 시트 (ActionFxPlayer) ──
        public const string SlamKey = "Boss_Final/Slam";
        public const string JumpKey = "Boss_Final/JumpSlam";
        ActionFxPlayer _fx;
        bool  _viaSheet;             // 지금 공격의 연출을 시트가 맡고 있나
        float _impactAt, _endAt;     // 지금 공격의 충돌·종료 시각 — 시트 이름표가 있으면 그것, 없으면 인스펙터 값

        float Scale => transform.lossyScale.x;   // 반경·거리는 덩치에 비례

        public void OnSpawn(ProtoZombie body, Transform target)
        {
            _body = body; _target = target;
            _anim = GetComponentInChildren<Animator>();
            if (_anim != null) _anim.speed = 1f;   // 재사용 인스턴스 — 지난 판 공격 배속이 남지 않게
            // 액션 FX 시트 재생기 — 프리팹 배선 없이 붙인다. 망치 머리는 코드가 아는 자리라 소켓으로 등록
            _fx = GetComponent<ActionFxPlayer>();
            if (_fx == null) _fx = gameObject.AddComponent<ActionFxPlayer>();
            _fx.SetSocket("HammerHead", hammerHead);
            _state = State.Roar; _t = 0f;
            _nextSlam = Time.time + roarTime;
            _nextJump = Time.time + roarTime + 2f;
            ResetFx();
        }

        public void OnDie() { ResetFx(); if (_anim != null) _anim.speed = 1f; }   // 사망 클립은 원래 속도로

        void ResetFx()
        {
            _fxWindup = _fxSwing = _hit = false;
            _viaSheet = false;
            if (_fx != null) _fx.End();
            if (_tele != null) { _tele.Cancel(); _tele = null; }
            ProtoBossFx.HammerTrail(hammerHead, false);
        }

        /// 시트에 이 동작이 있으면 연출을 시트에 맡기고 충돌·종료 시각을 이름표에서 읽는다. 없으면 인스펙터 값.
        void BeginSheet(string key, float radius, bool big, float impactAt, float endAt)
        {
            _viaSheet = _fx != null && _fx.Begin(key, _impactPos, radius, big, Scale);
            _impactAt = _viaSheet ? _fx.MarkerTime("impact", impactAt) : impactAt;
            _endAt    = _viaSheet ? _fx.MarkerTime("end", endAt) : endAt;
        }

        /// 한 프레임. 돌려주는 값은 이동 변위(ProtoZombie 가 장애물·울타리 처리 후 적용).
        public Vector3 Tick(Vector3 to, float dist, float dt)
        {
            // 공격 중에는 클립을 attackSpeed 배로 돌리므로 시각도 그만큼 빨리 흐른다(시각 값은 1배속 클립 기준)
            _t += (_state == State.Slam || _state == State.Jump) ? dt * attackSpeed : dt;
            Vector3 dir = dist > 0.001f ? to / dist : transform.forward;

            switch (_state)
            {
                case State.Roar:
                    SetSpeed(0f);
                    if (_t >= roarTime) Enter(State.Chase);
                    return Vector3.zero;

                case State.Chase:
                    Face(dir, dt, 360f);
                    if (dist <= slamRange * Scale && Time.time >= _nextSlam) { StartSlam(dir); return Vector3.zero; }
                    if (dist >= jumpMinDist * Scale && dist <= jumpMaxDist * Scale && Time.time >= _nextJump) { StartJump(); return Vector3.zero; }
                    if (dist <= 1.6f * Scale) { SetSpeed(0f); return Vector3.zero; }   // 붙어서 쿨 기다리는 중
                    SetSpeed(_body.moveSpeed);
                    return dir * _body.moveSpeed * dt;

                case State.Slam: return TickSlam();
                case State.Jump: return TickJump();
            }
            return Vector3.zero;
        }

        // ── 내려찍기 ───────────────────────────────────────────────
        void StartSlam(Vector3 dir)
        {
            Enter(State.Slam);
            // 클립이 왼쪽 앞 대각선을 치므로, 충돌 지점이 플레이어 쪽에 오도록 몸을 그만큼 오른쪽으로 비튼다
            float off = Mathf.Atan2(slamOffset.x, slamOffset.y) * Mathf.Rad2Deg;   // 약 -54°
            transform.rotation = Quaternion.LookRotation(Quaternion.Euler(0f, -off, 0f) * dir);
            _impactPos = Ground(transform.position + transform.rotation * new Vector3(slamOffset.x, 0f, slamOffset.y) * Scale);
            BeginSheet(SlamKey, slamRadius * Scale, false, slamImpactAt, slamEndAt);
            if (_anim != null) { _anim.SetTrigger(ProtoAnimIds.Attack); _anim.speed = attackSpeed; }
            SetSpeed(0f);
            _tele = ProtoBossFx.ShowTelegraph(_impactPos, slamRadius * Scale, _impactAt / attackSpeed);
        }

        Vector3 TickSlam()
        {
            if (_viaSheet) _fx.Advance(_t); else Phases(slamWindupAt, slamSwingAt, slamImpactAt);
            if (!_hit && _t >= _impactAt) { _hit = true; Impact(_impactPos, slamRadius * Scale, slamDamage, false); }
            if (_t >= _endAt) { _nextSlam = Time.time + slamCooldown; Enter(State.Chase); }
            return Vector3.zero;
        }

        // ── 도약 내려찍기 ─────────────────────────────────────────
        void StartJump()
        {
            Enter(State.Jump);
            _from = transform.position;
            // 착지점 = 지금 플레이어 자리. 예고 원이 이 자리에 깔리므로 플레이어는 비키면 된다.
            _impactPos = Ground(ProtoBossArena.Clamp(_target.position, 0.6f));
            Vector3 to = Flat(_impactPos - _from);
            Vector3 face = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
            var rot = Quaternion.LookRotation(face);
            Vector3 headOff = rot * new Vector3(jumpHeadOffset.x, 0f, jumpHeadOffset.y) * Scale;
            // 망치 머리는 몸보다 앞·왼쪽에 떨어진다 — 몸의 도착점은 그만큼 뺀 자리
            _land = _impactPos - headOff;
            // 너무 가까우면 뒤로 물러나 뛰게 되므로 그 자리에서 뛴다(망치만 앞에 떨어진다)
            if (Vector3.Dot(_land - _from, face) < 0f) { _land = _from; _impactPos = Ground(_from + headOff); }
            transform.rotation = rot;
            BeginSheet(JumpKey, jumpRadius * Scale, true, jumpImpactAt, jumpEndAt);
            if (_anim != null) { _anim.SetTrigger(ProtoAnimIds.JumpAttack); _anim.speed = attackSpeed; }
            SetSpeed(0f);
            _tele = ProtoBossFx.ShowTelegraph(_impactPos, jumpRadius * Scale, _impactAt / attackSpeed);
            ProtoAudio.Play2D("boss_roar", 0.6f);
        }

        Vector3 TickJump()
        {
            if (_viaSheet) _fx.Advance(_t); else Phases(jumpWindupAt, jumpSwingAt, jumpImpactAt);
            // 클립의 골반 전진 곡선대로 착지점까지 옮긴다(루트 모션은 버리고 있으므로 코드가 대신 한다)
            Vector3 want = Vector3.Lerp(_from, _land, JumpTravel.Evaluate(_t));
            Vector3 disp = Flat(want - transform.position);
            if (!_hit && _t >= _impactAt) { _hit = true; Impact(_impactPos, jumpRadius * Scale, jumpDamage, true); }
            if (_t >= _endAt) { _nextJump = Time.time + jumpCooldown; _nextSlam = Time.time + 0.6f; Enter(State.Chase); }
            return disp;
        }

        // ── 공통 ───────────────────────────────────────────────────
        /// 콘티 1·2컷: 치켜드는 동안 망치에 불이 붙고, 내려칠 때 궤적이 그려진다.
        void Phases(float windupAt, float swingAt, float impactAt)
        {
            if (!_fxWindup && _t >= windupAt) { _fxWindup = true; ProtoBossFx.WindupDust(transform.position, Scale); }
            if (_fxWindup && _t < impactAt) ProtoBossFx.HammerFlames(hammerHead, ProtoConfig.Delta);
            if (!_fxSwing && _t >= swingAt) { _fxSwing = true; ProtoBossFx.HammerTrail(hammerHead, true); }
            if (_fxSwing && _t >= impactAt + 0.12f) ProtoBossFx.HammerTrail(hammerHead, false);
        }

        /// 콘티 3·4컷: 충돌 — 피해 판정 + 폭발·충격파·파편·균열·연기·카메라 흔들림.
        void Impact(Vector3 pos, float radius, float damage, bool big)
        {
            if (_tele != null) { _tele.Cancel(); _tele = null; }
            if (!_viaSheet) ProtoBossFx.Impact(pos, radius, big);   // 시트 모드에선 "boss_impact" 이벤트가 그린다
            if (_target == null) return;
            var p = _target.GetComponent<ProtoPlayer>();
            if (p != null && Flat(_target.position - pos).magnitude <= radius + p.bodyRadius) p.TakeDamage(damage);
        }

        void Enter(State s)
        {
            _state = s; _t = 0f;
            // 추격·포효로 돌아오면 걷기·달리기는 원래 속도로 (공격 상태는 Start* 에서 다시 올린다)
            if (_anim != null && s != State.Slam && s != State.Jump) _anim.speed = 1f;
            _fxWindup = _fxSwing = _hit = false;
            _viaSheet = false;
            if (_fx != null) _fx.End();   // 켜져 있던 스팬(망치 불·궤적) 정리
            ProtoBossFx.HammerTrail(hammerHead, false);
        }

        void Face(Vector3 dir, float dt, float degPerSec)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), degPerSec * dt);
        }

        void SetSpeed(float s) { if (_anim != null) _anim.SetFloat(ProtoAnimIds.Speed, s); }
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        static Vector3 Ground(Vector3 v) { v.y = 0f; return v; }
    }
}
