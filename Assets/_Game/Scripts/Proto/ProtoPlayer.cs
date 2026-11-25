using UnityEngine;

namespace Proto
{
    /// 플레이어: 이동, 스탯, 피격. 사격은 ProtoShooter가 담당한다.
    /// 입력은 WASD/방향키 + 화면 드래그(마우스·터치) 둘 다 받는다.
    public class ProtoPlayer : MonoBehaviour, IProtoAimer
    {
        static readonly int AnimSpeed = Animator.StringToHash("Speed");   // 2026-11-20: 문자열 해시를 매 프레임 안 하게
        [Header("런타임 스탯 (업그레이드로 증가)")]
        public float maxHP        = ProtoConfig.PlayerMaxHP;
        public float hp;
        public float moveMult     = 1f;
        public float damageMult   = 1f;
        public float atkSpeedMult = 1f;
        public float pickupMult   = 1f;
        public float xpMult       = 1f;
        public float critChance   = ProtoConfig.CritChance;

        [Header("충돌")]
        [Tooltip("프롭 충돌 판정에 쓰는 몸통 반지름")]
        public float bodyRadius = 0.32f;

        // 아드레날린 배율은 상한 **뒤에** 곱한다 — 8초짜리 버프라 상한(스폰이 못 따라오는 문제)과 무관하고,
        // 앞에 곱하면 이동속도를 다 올린 빌드에선 아이템이 아무 일도 안 한다.
        public float MoveSpeed  => Mathf.Min(ProtoConfig.MoveSpeed * moveMult, ProtoConfig.MoveSpeedCap)
                                   * (IsAdrenaline ? ProtoConfig.AdrenalineMoveMult : 1f) * (IsSlowed ? _slowMult : 1f);
        public bool  IsSlowed    => Time.time < _slowUntil;
        public float SlowLeft    => Mathf.Max(0f, _slowUntil - Time.time);
        /// 실제 공격속도 배율 = 업그레이드 × 아드레날린. 발사 간격을 계산하는 곳은 전부 이 값을 쓴다.
        public float AtkSpeed    => atkSpeedMult * (IsAdrenaline ? ProtoConfig.AdrenalineAtkMult : 1f);

        // ── 버프 아이템 (승합차 드롭, ProtoPickups) ──────────────────────
        float _adrenalineUntil, _shieldUntil;
        public bool  IsAdrenaline => Time.time < _adrenalineUntil;
        public bool  IsShielded   => Time.time < _shieldUntil;
        public float AdrenalineLeft => Mathf.Max(0f, _adrenalineUntil - Time.time);
        public float ShieldLeft     => Mathf.Max(0f, _shieldUntil - Time.time);

        /// 아드레날린: 공속·이속 증가 + 걸린 감속을 풀고, 지속 중엔 감속이 안 걸린다.
        public void GrantAdrenaline(float seconds)
        {
            _adrenalineUntil = Mathf.Max(_adrenalineUntil, Time.time + seconds);
            _slowUntil = 0f;
        }

        /// 보호막: 그 시간 동안 무적. 무적 자체는 피격 무적과 같은 값(_invulnUntil)을 쓴다.
        public void GrantShield(float seconds)
        {
            _shieldUntil = Mathf.Max(_shieldUntil, Time.time + seconds);
            GrantInvuln(seconds);
        }

        /// 판을 다시 시작할 때 — 버프·감속이 다음 판으로 넘어가지 않게.
        public void ResetBuffs() { _adrenalineUntil = _shieldUntil = _slowUntil = 0f; _invulnUntil = 0f; }
        public float PickupRange => ProtoConfig.PickupRadius * pickupMult;
        public bool  IsAlive     => hp > 0f;
        public bool  IsInvulnerable => Time.time < _invulnUntil;

        // ── 발소리 ───────────────────────────────────────────────────────
        // **애니메이션 이벤트를 쓰지 않는다.** 이동은 코드가 하고(루트모션 미사용) Animator 에는
        // Speed 만 넘기므로, 클립에 이벤트를 심으면 캐릭터 6종(§04)의 이동 클립마다 같은
        // 작업을 반복해야 하고 클립을 다시 받을 때마다 사라진다. 이동 거리로 재면 캐릭터를
        // 갈아끼워도, 이동 속도 업그레이드(경량 프레임)가 붙어도 걸음 수가 저절로 맞는다.
        //
        // 1.25m: 기본 속도 4.2m/s 에서 초당 3.4걸음이 된다. 이보다 짧게 잡으면
        // 상한 속도(7.5m/s)에서 초당 6걸음이 되어 발소리가 드르륵거린다.
        const float StepDistance = 1.25f;
        float _stepAccum;

        Animator _anim;
        float _invulnUntil;
        Vector3 _dragOrigin;
        bool _dragging;
        Vector3 _facingDir = Vector3.forward;
        Vector3 _moveDir;

        /// 이번 프레임의 실제 이동 방향. 정지 중이면 zero.
        /// 카메라 리드 오프셋이 이 값을 쓴다 — 정지 중에도 방향이 있으면 시작하자마자
        /// 카메라가 리드 거리만큼(1.4m) 튄다.
        public Vector3 MoveDir => _moveDir;

        /// 마지막으로 움직인 방향(0이 되지 않음). 스폰 링의 앞/뒤 배분처럼
        /// 항상 기준 방향이 필요한 곳에서 쓴다.
        public Vector3 FacingDir => _facingDir;

        // ── 몸 회전 ─────────────────────────────────────────────────────
        // **회전의 주인은 여기다.** 예전에는 ProtoShooter 가 발사할 때마다 rotation 을 직접
        // 대입했고, 그래서 발사 간격만큼 뚝뚝 끊겼다(로켓은 1.7초에 한 번). 조준 방향은
        // 슈터가 매 프레임 알려주고, 실제로 몸을 돌리는 것은 이동을 소유한 이 클래스가 한다.
        //
        // 조준 방향에 유효 시간을 두는 이유: Update 실행 순서가 정해져 있지 않아서
        // 슈터가 이 프레임에 먼저 돌든 나중에 돌든 결과가 같아야 한다.
        const float AimHoldTime = 0.2f;
        Vector3 _aimDir;
        float _aimUntil;

        /// <summary>슈터가 매 프레임 조준 방향을 넘긴다. 사거리에 적이 없으면 부르지 않는다.</summary>
        public void SetAimDir(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            _aimDir = dir.normalized;
            _aimUntil = Time.time + AimHoldTime;
        }

        /// <summary>조준 중이면 그 방향, 아니면 이동 방향. 둘 다 없으면 현재 방향을 유지한다.</summary>
        void TickRotation(Vector3 moveDir)
        {
            Vector3 want = Time.time < _aimUntil ? _aimDir
                         : (moveDir.sqrMagnitude > 0.0001f ? moveDir : Vector3.zero);
            if (want.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(want),
                ProtoConfig.TurnSpeed * ProtoConfig.Delta);
        }

        void Awake()
        {
            hp = maxHP;
            RebindVisual();
        }

        /// Visual 자식이 통째로 교체됐을 때 캐시를 다시 잡는다.
        /// Awake 에서 한 번만 잡으면 캐릭터를 갈아끼운 순간 Animator 가 죽은 참조가 되어
        /// 다리가 멈춘다(에러는 안 난다 — 그래서 더 찾기 어렵다). 캐릭터 교체 테스트 툴과
        /// 나중의 스쿼드 순차 교체(§04)가 이 함수를 부른다.
        public void RebindVisual() => _anim = GetComponentInChildren<Animator>();

        void Start()
        {
            // 시작 지점이 프롭 안이면 첫 프레임에 밀려나면서 튕기는 것처럼 보인다.
            // 이동이 시작되기 전에 한 번 정리해 둔다.
            var obs = ProtoObstacles.I;
            if (obs == null) return;
            Vector3 p = transform.position; p.y = 0f;
            Vector3 fixedPos = obs.ResolveMove(p, bodyRadius, 4);
            if ((fixedPos - p).sqrMagnitude > 0.0001f)
            {
                Debug.Log($"[Player] 시작 지점이 프롭과 겹쳐 {Vector3.Distance(p, fixedPos):F2}m 이동: {p:F2} → {fixedPos:F2}");
                transform.position = fixedPos;
            }
            else transform.position = p;
        }

        void Update()
        {
            if (!IsAlive) return;
            // 시작 연출(낙하산) 중에는 움직이지 않는다 — 몸은 ProtoIntro 가 공중에서 굴린다
            if (ProtoRun.I != null && ProtoRun.I.state == RunState.Intro) { if (_anim != null) _anim.SetFloat(AnimSpeed, 0f); return; }

            Vector3 dir = ReadInput();

            // 탱크 탑승 중(프로토 한정) — 입력은 탱크가 쓰고, 몸은 탱크에 붙어 다닌다(모델은 숨김)
            var tank = ProtoTank.Ridden;
            if (tank != null)
            {
                _moveDir = dir;
                if (dir.sqrMagnitude > 0.0001f) _facingDir = dir;
                tank.Drive(dir);
                transform.SetPositionAndRotation(tank.transform.position, tank.transform.rotation);
                if (_anim != null) _anim.SetFloat(AnimSpeed, 0f);
                return;
            }
            float speed = MoveSpeed;
            _moveDir = dir;

            if (dir.sqrMagnitude > 0.0001f)
            {
                _facingDir = dir;
                Vector3 next = transform.position + dir * speed * ProtoConfig.Delta;
                // 아레나 밖으로 나가지 않게 클램프 (보이지 않는 벽 대체 — 물리 미사용)
                float lim = ProtoConfig.ArenaHalf;
                next.x = Mathf.Clamp(next.x, -lim, lim);
                next.z = Mathf.Clamp(next.z, -lim, lim);
                next.y = 0f;
                // 배경 프롭·블로커를 통과하지 못하게 밀어낸다 (GDD §16 — Unity Physics 미사용). 가린 것은 ProtoSeeThrough 가 비운다
                if (ProtoObstacles.I != null) next = ProtoObstacles.I.ResolveMove(next, bodyRadius);
                // 최종 보스 울타리 — 프롭 밀어내기 뒤에 해야 프롭이 울타리 밖으로 밀어내지 못한다
                next = ProtoBossArena.Clamp(next, bodyRadius);
                if (DiagMove) Diagnose(transform.position, next, dir, speed);
                // 발소리는 **실제로 움직인 거리**로 센다. 입력 방향 × 속도로 세면 프롭에
                // 막혀 제자리걸음일 때도 계속 걸음 소리가 난다.
                _stepAccum += (next - transform.position).magnitude;
                transform.position = next;
                if (_stepAccum >= StepDistance)
                {
                    _stepAccum -= StepDistance;
                    ProtoAudio.Play("step", transform.position);
                }
            }
            else
            {
                // 멈춰 있는 동안 조금 채워 둔다. 0 으로 비우면 다시 걷기 시작할 때
                // 1.25m 를 걷고 나서야 첫 걸음 소리가 나서 출발이 미끄러지는 것처럼 들린다.
                _stepAccum = Mathf.Max(_stepAccum, StepDistance * 0.75f);
            }

            // 조준 중이면 그 방향, 아니면 이동 방향으로 부드럽게 돌린다
            TickRotation(dir);

            if (_anim != null)
                _anim.SetFloat(AnimSpeed, dir.sqrMagnitude > 0.0001f ? speed : 0f);
        }

        Vector3 ReadInput()
        {
            // 키보드
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 dir = new Vector3(h, 0f, v);

            // 화면 드래그 (플로팅 조이스틱 — GDD §03)
            // 두 손가락이면 카메라 핀치(ProtoCameraFollow) — 이동 드래그는 멈춘다. 안 멈추면 핀치하는 동안 캐릭터가 걸어 나간다.
            if (Input.touchCount >= 2) { _dragging = false; return dir; }
            // UI(필사기·옵션 버튼 등) 위에서 시작한 터치는 이동이 아니다 — 예전 IMGUI HUD 는 이걸 못 걸러
            // 버튼을 누르면 캐릭터가 그쪽으로 걸어갔다.
            if (Input.GetMouseButtonDown(0) && !PointerOverUI()) { _dragOrigin = Input.mousePosition; _dragging = true; }
            if (Input.GetMouseButtonUp(0))   { _dragging = false; }
            if (_dragging)
            {
                Vector2 delta = (Vector2)(Input.mousePosition - _dragOrigin);
                const float deadzone = 12f, radius = 90f;
                if (delta.magnitude > deadzone)
                {
                    Vector2 n = delta / radius;
                    if (n.magnitude > 1f) n.Normalize();
                    dir = new Vector3(n.x, 0f, n.y);
                }
            }

            if (dir.sqrMagnitude > 1f) dir.Normalize();
            return dir;
        }

        static bool PointerOverUI()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.phase == TouchPhase.Began && es.IsPointerOverGameObject(t.fingerId)) return true;
                }
                return false;
            }
            return es.IsPointerOverGameObject();
        }

        // ── 피해 종류 · 피격 표시 (2026-11-12 사용자) ────────────────────────────
        // "초록 혼불탄에 맞으면 초록으로 한 번 번쩍 + 초록 마이너스 숫자 + 몸에서 초록 피격 이펙트, 기본 피해는 빨간 숫자"

        /// 피해 종류 — 숫자 색 · 번쩍임 · 몸 이펙트가 갈린다.
        /// Normal = 빨간 "-n" + 몸이 빨갛게 번쩍(보스 · 폭발 · 개 물기 등).
        /// Claw(평타 좀비의 접촉 공격) = Normal + 몸에 할퀴기 이펙트(zombie_claw_hit).
        /// Poison(원거리 좀비 혼불탄 · 러너 독가스) = 초록 "-n" + 몸이 초록으로 번쩍 + 몸에 초록 피격 이펙트(player_poison_hit).
        public enum DamageKind { Normal, Poison, Claw }

        /// 숫자를 띄울 높이(m) — 캐릭터 키 약 2.7m(Visual 1.6배) 머리 위
        const float DamageNumberHeight = 2.6f;
        /// 몸 피격 이펙트 자리(m, 플레이어 로컬 위) — 가슴께
        const float BodyFxHeight = 1.3f;

        public void TakeDamage(float amount) => TakeDamage(amount, DamageKind.Normal);

        public void TakeDamage(float amount, DamageKind kind)
        {
            if (!IsAlive) return;
            if (ProtoConfig.DebugInvincible) return;   // 임시 — 테스트용 무적 (ProtoConfig 참고)
            // 탱크 탑승 중에는 탱크가 맞는다 (탱크 HP 가 0 이 되면 파괴 → 하차)
            if (ProtoTank.Ridden != null) { ProtoTank.Ridden.TakeDamage(amount); return; }
            if (Time.time < _invulnUntil) return;
            hp = Mathf.Max(0f, hp - amount);
            _invulnUntil = Time.time + ProtoConfig.InvulnAfterHit;

            // **상태 전이를 소리보다 먼저 한다.** 연출 호출을 상태 전이 앞에 끼워 넣으면
            // 그 호출이 언젠가 예외를 던지는 날 런이 끝나지 않는다 — 죽었는데 계속 굴러가는
            // 상태가 되고, 원인이 오디오라는 것은 아무도 짐작하지 못한다.
            bool died = hp <= 0f;
            if (died) ProtoRun.I?.OnPlayerDied();
            // 숫자 · 번쩍임 · 몸 이펙트는 상태 전이 **뒤에** — 죽은 한 방에도 숫자는 뜬다(무적·탱크로 막힌 피해는 위에서 이미 걸러졌다)
            HitFeedback(amount, kind);
            if (died) return;
            // 크게 맞으면(보스 공격 등 20 이상) 화면도 흔들린다 — 잡좀비 접촉(8)은 너무 잦아 제외
            if (amount >= 20f) ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Medium);

            // 살아남았을 때만 피격음. 죽는 순간에는 결과 스팅어(gameover)가 그 자리를 맡는다 —
            // 둘을 같이 내면 EndRun 의 StopAll 과 겹쳐 피격음이 팡파르 위에 얹힌다.
            // 무적 판정을 통과한 뒤라는 것도 중요하다. 위 early return 앞에 두면 붙어 있는
            // 좀비가 계속 때리는 동안 피해가 없는데도 피격음이 난다.
            ProtoAudio.Play("player_hurt", transform.position);
        }

        void HitFeedback(float amount, DamageKind kind)
        {
            bool poison = kind == DamageKind.Poison;
            ProtoDamageText.ShowPlayer(transform.position + Vector3.up * DamageNumberHeight, amount,
                                       poison ? ProtoDamageText.ColPlayerPoison : ProtoDamageText.ColPlayerHurt);
            FlashBody(poison ? PoisonFlashTint : HurtFlashTint);   // 독 = 초록, 그 밖 = 빨강(2026-11-12 사용자)
            // 몸 피격 이펙트 — 카탈로그 프리팹이 있을 때만 나온다(코드 그림 없음). 가슴께에 붙어 따라다닌다
            string fx = poison ? FxIds.PlayerPoisonHit : kind == DamageKind.Claw ? FxIds.ZombieClawHit : null;
            if (fx == null) return;
            var c = FxContext.On(transform, follow: true);
            c.offset = Vector3.up * BodyFxHeight;
            ProtoFx.Play(fx, c);
        }

        // ── 몸 번쩍임 ─────────────────────────────────────────────────────
        // 캐릭터 머티리얼(BCM_Toony_shader_03)은 _EMISSION 키워드가 꺼져 있어 좀비처럼 이미션으로 번쩍일 수 없다(키워드를 켜면
        // 빌드에서 그 변형이 빠질 수 있다). 셰이더가 텍스처 색에 곱하는 _BaseColor 를 머티리얼마다 MaterialPropertyBlock 으로
        // 잠깐 크게 올린다 — 1 을 넘는 초록 배율이라 몸 전체가 밝은 초록으로 뜬다. 끝나면 블록을 비워 머티리얼 값으로 돌아간다.
        static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        /// 독 피격 번쩍임 — _BaseColor 에 곱하는 배율(텍스처 색 × 이 값)
        static readonly Color PoisonFlashTint = new Color(0.55f, 2.2f, 0.55f, 1f);
        /// 기본 피격 번쩍임(빨강) — 같은 방식
        static readonly Color HurtFlashTint = new Color(2.2f, 0.5f, 0.45f, 1f);
        const float FlashHold = 0.06f, FlashFade = 0.16f;   // 꽉 찬 채로 0.06초 → 0.16초에 걸쳐 원래대로
        System.Collections.Generic.List<SkinnedMeshRenderer> _flashR;
        readonly System.Collections.Generic.List<SkinnedMeshRenderer> _flashList = new System.Collections.Generic.List<SkinnedMeshRenderer>();
        readonly System.Collections.Generic.List<Material> _flashMats = new System.Collections.Generic.List<Material>();
        MaterialPropertyBlock _flashMpb;
        Color _flashTint;
        float _flashT = -1f;

        /// 몸을 tint 배율로 한 번 번쩍인다. 캐릭터가 교체됐을 수 있어 렌더러는 그때그때 다시 잡는다.
        public void FlashBody(Color tint)
        {
            if (_flashT >= 0f) ApplyFlash(0f);   // 앞 번쩍임이 남아 있으면 지우고 다시
            GetComponentsInChildren(_flashList);   // 목록을 재사용(2026-11-20: 피격마다 새 배열이었다)
            _flashR = _flashList;
            _flashTint = tint;
            _flashT = 0f;
            ApplyFlash(1f);
        }

        void LateUpdate()
        {
            if (_flashT < 0f) return;
            _flashT += Time.unscaledDeltaTime;   // 레벨업 정지 중에도 끝나게(초록인 채로 멈춰 있지 않게)
            float k = _flashT <= FlashHold ? 1f : 1f - (_flashT - FlashHold) / FlashFade;
            if (k <= 0f) { ApplyFlash(0f); _flashT = -1f; return; }
            ApplyFlash(k);
        }

        /// k = 1 이면 꽉 찬 번쩍임, 0 이면 블록을 비워 원래대로.
        void ApplyFlash(float k)
        {
            if (_flashR == null) return;
            if (_flashMpb == null) _flashMpb = new MaterialPropertyBlock();
            foreach (var r in _flashR)
            {
                if (r == null) continue;
                r.GetSharedMaterials(_flashMats);   // 매 프레임 머티리얼 배열을 새로 만들지 않게
                var mats = _flashMats;
                for (int i = 0; i < mats.Count; i++)
                {
                    _flashMpb.Clear();
                    if (k > 0f && mats[i] != null && mats[i].HasProperty(BaseColorID))
                    {
                        Color b = mats[i].GetColor(BaseColorID);
                        Color hit = new Color(b.r * _flashTint.r, b.g * _flashTint.g, b.b * _flashTint.b, b.a);
                        _flashMpb.SetColor(BaseColorID, Color.LerpUnclamped(b, hit, k));
                    }
                    r.SetPropertyBlock(_flashMpb, i);   // 빈 블록 = 머티리얼 값 그대로
                }
            }
        }

        // ── 이동 진단 (임시, 2026-10-21 사용자: "이유 없이 갑자기 느려지거나 막힌다") ─────────
        // 실제 이동이 기대(기본 속도 × 실제 프레임 시간)보다 20% 넘게 줄면 원인을 콘솔에 남긴다. 원인별 0.5초에 한 줄.
        // 2026-10-21 사용자 확인 후 꺼 둠 — 다시 의심되면 true.
        public static bool DiagMove = false;
        float _diagNextLog;

        void Diagnose(Vector3 from, Vector3 to, Vector3 dir, float speed)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float baseSpeed = Mathf.Min(ProtoConfig.MoveSpeed * moveMult, ProtoConfig.MoveSpeedCap) * (IsAdrenaline ? ProtoConfig.AdrenalineMoveMult : 1f);
            float wanted = baseSpeed * dt;
            float moved = (to - from).magnitude;
            if (moved >= wanted * 0.8f || Time.unscaledTime < _diagNextLog) return;
            _diagNextLog = Time.unscaledTime + 0.5f;

            string why;
            if (IsSlowed) why = $"개 물림 감속 ×{_slowMult:0.00} (남은 {SlowLeft:0.0}초)";
            else if (dt > ProtoConfig.MaxDeltaTime) why = $"프레임 끊김 dt={dt * 1000f:0}ms (이동은 {ProtoConfig.MaxDeltaTime * 1000f:0}ms 분까지만)";
            else
            {
                Vector3 free = from + dir * speed * ProtoConfig.Delta;
                float lim = ProtoConfig.ArenaHalf;
                if (Mathf.Abs(free.x) > lim || Mathf.Abs(free.z) > lim) why = "맵 경계";
                else if (ProtoBossArena.Clamp(free, bodyRadius) != free) why = "보스 울타리";
                else why = "충돌 볼륨(프롭·블로커)에 밀림";
            }
            Debug.Log($"[이동 진단] 기대 {wanted / dt:0.0}m/s → 실제 {moved / dt:0.0}m/s — {why} @ ({from.x:0.0}, {from.z:0.0}) 경과 {(ProtoRun.I != null ? ProtoRun.I.Elapsed : 0f):0.0}s");
        }

        // ── 감속 (좀비 개에게 물림) ─────────────────────────────────────
        // 겹쳐 쌓지 않는다 — 개 여러 마리가 물어도 가장 센 감속 하나만, 지속시간만 갱신.
        // 쌓이면 개 세 마리에 발이 묶여 아무것도 못 하고 죽는다.
        float _slowUntil, _slowMult = 1f;

        public void ApplySlow(float mult, float seconds)
        {
            if (!IsAlive) return;
            if (ProtoTank.Ridden != null) return;   // 탱크는 개에게 물려도 안 느려진다
            if (IsAdrenaline) return;               // 아드레날린 중엔 안 느려진다
            // 피해가 막힌 물림(보호막·피격 직후 무적)은 감속도 없다 — 부르는 쪽(ProtoZombie 개 물기)이 피해 **전에** 본다.
            // 여기서 보면 방금 그 물림의 피해가 건 무적에 걸려 감속이 영영 안 걸린다.
            _slowMult  = IsSlowed ? Mathf.Min(_slowMult, mult) : mult;
            _slowUntil = Mathf.Max(_slowUntil, Time.time + seconds);
        }

        public void Heal(float amount) => hp = Mathf.Min(maxHP, hp + amount);

        /// 잠깐 무적. 탱크에서 내리거나 파괴돼 튕겨 나올 때 — 좀비 한가운데 떨어져 바로 죽지 않게.
        public void GrantInvuln(float seconds) => _invulnUntil = Mathf.Max(_invulnUntil, Time.time + seconds);

        /// 최대 체력 증가 시 현재 체력도 같은 비율로 올려준다 (레벨업이 손해가 되지 않게)
        public void RaiseMaxHP(float addPercent)
        {
            float before = maxHP;
            maxHP *= 1f + addPercent;
            hp += maxHP - before;
        }
    }
}
