using UnityEngine;

namespace Proto
{
    // 새 종류는 맨 뒤에 붙인다 — 앞에 끼우면 씬·프리팹에 저장된 enum 값이 밀린다.
    public enum ZombieKind { Walker, Runner, Boss, Ranged, Dog }

    /// 좀비 1마리. 스폰 디렉터가 풀에서 꺼내 Spawn()으로 초기화한다.
    /// 이동은 코드가 담당하고(애니메이션 루트모션 사용 안 함) Animator에는 Speed/IsDead만 넘긴다.
    public class ProtoZombie : MonoBehaviour
    {
        /// 이 개체의 수치 줄(적 테이블). Spawn 이 넣는다. 움직임(kind)은 def.behaviour 를 따라간다.
        public EnemyDef def;
        public ZombieKind kind;
        public float hp, maxHP, moveSpeed, contactDamage;
        public int   xpValue;
        public float bodyRadius = ProtoConfig.BaseBodyRadius;
        /// 밀어내기에서 이 개체가 밀릴 비중. 0 이면 잡좀비가 밀어도 꿈쩍하지 않는다(보스).
        public float separationWeight = 1f;
        public bool  IsBoss => kind == ZombieKind.Boss;
        public bool  IsRanged => kind == ZombieKind.Ranged;
        public bool  IsDog    => kind == ZombieKind.Dog;
        /// 엘리트 — 워커로 Spawn 한 뒤 스폰 디렉터가 <see cref="MakeElite"/> 로 올린다. 죽으면 진화 상자.
        /// 종류(enum)를 새로 만들지 않은 이유: 모델·애니·풀이 워커와 같다. 다른 건 수치·덩치·빛뿐이다.
        public bool  IsElite { get; private set; }

        /// 공격 거리에 더하는 값. 탱크 탑승 중(프로토 한정)엔 차체 반경만큼 늘려 가장자리에서 멈춰 때리게 한다.
        public static float TargetPad;
        public bool  Alive { get; private set; }

        /// <summary>
        /// 바깥의 점을 몸 표면 안으로 당긴 점 — 착탄 연출 자리. 투사체는 판정 반경이 넉넉해 <b>몸에 닿기 전에</b> 맞으므로
        /// 총알 위치를 그대로 쓰면 불똥이 몸 앞 허공에 뜬다(2026-11-04 — 팩 착탄 효과처럼 큰 모양에서 눈에 띄었다).
        /// 가슴 높이(0.9 × 덩치) 중심, 반경 = bodyRadius. 이미 안이면 그대로.
        /// </summary>
        public Vector3 SurfacePoint(Vector3 from)
        {
            float body = Mathf.Max(1f, bodyRadius / ProtoConfig.BaseBodyRadius);
            Vector3 center = transform.position + Vector3.up * (0.9f * body);
            Vector3 d = from - center;
            float m = d.magnitude;
            return m > bodyRadius && m > 0.0001f ? center + d * (bodyRadius / m) : from;
        }

        Animator _anim;
        Transform _target;
        float _nextContactTime;
        float _despawnAt;
        bool  _dying;

        // ── 피격 피드백 ──────────────────────────────────────────────
        // 흰 플래시는 _EmissionColor 를 MaterialPropertyBlock 으로 흔들어 낸다.
        // 머티리얼을 통째로 갈아끼우면 같은 머티리얼이 그리는 발밑 블롭 그림자까지
        // 사라지므로 그 방법은 못 쓴다. _BaseColor 곱셈도 흰색으로 수렴하지 않는다
        // (주황이 밝은 주황이 될 뿐) — 가산되는 이미션이라야 흰 플래시가 된다.
        static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");
        // 화상은 알베도를 곱해 어둡게 한다. 이미션은 더하기만 하므로 어둡게 만들 수 없고,
        // 무엇보다 **프로퍼티가 달라야** 피격 플래시와 서로를 지우지 않는다
        // (셰이더 1208행 `albedo.rgb *= _BaseColor.rgb`).
        static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        Vector3 _lastHitDir;
        bool _bodyHidden;   // 조각나서 몸을 숨겼다 — 풀에서 다시 나올 때 되살린다
        /// 조각난 뒤 몸(숨긴 채)이 풀로 돌아가기까지. 쓰러지는 애니는 안 보이므로 deathLinger 까지 기다릴 이유가 없다
        const float GibLinger = 0.4f;
        float _flashUntil;
        bool  _flashOn;

        // ── 화상 (GDD §05 화염방사기) ────────────────────────────────
        // 0.25초마다 준다. 매 프레임이면 프레임레이트가 피해량이 되고,
        // 1초면 2초 화상이 2번만 들어와 죽는 순간 손실이 커진다.
        const float BurnTickInterval = 0.25f;
        float _burnUntil, _burnNextTick, _burnDps;
        public bool IsBurning => Alive && Time.time < _burnUntil;

        // 화상으로 죽으면 재가 된다. **완전한 검정은 사망 연출에서만** 쓴다 —
        // 살아 있는 동안 검게 만들면 저채도 야간 배경(§15)에 묻혀 "타고 있다"가 안 읽히고,
        // 파이로의 패시브(화상 적을 처치하면 전파)가 눈에 보이지 않게 된다.
        const float CharTime = 0.55f;
        float _charUntil;
        bool  _charring;

        // 화상 사망 — 타들어가며 사라진다(2026-11-10, ZombieBurnOut). 머티리얼을 타는 것으로 갈아끼우고 MPB 로 진행도를 민다.
        // 풀로 돌아갔다 다시 나올 때 반드시 되돌린다(ResetVisual → EndBurnOut).
        static readonly int DissolveID = Shader.PropertyToID("_Dissolve");
        static readonly int NoiseOffsetID = Shader.PropertyToID("_NoiseOffset");
        static readonly int NoiseScaleID = Shader.PropertyToID("_NoiseScale");
        static readonly int BurnAnchorID = Shader.PropertyToID("_BurnAnchor");
        static readonly int EdgeWidthID = Shader.PropertyToID("_EdgeWidth");
        static readonly int HeightBiasID = Shader.PropertyToID("_HeightBias");
        /// 다 타고 난 뒤 풀로 돌아가기까지의 여유(초) — 마지막 프레임이 잘리지 않게
        const float BurnOutTail = 0.05f;
        bool _burnOut;
        float _burnOutStart, _burnNoiseScale;
        Vector4 _burnAnchor, _burnSeed;      // anchor = 죽은 자리(xyz) + 몸 높이(w)
        Material[][] _burnSaved;             // 렌더러별 원래 머티리얼 묶음(안 갈아끼운 렌더러는 null)
        /// 불타 죽어 타들어가는 중인가 — <see cref="ProtoBurnFx"/> 가 불씨·사그라드는 불꽃을 낸다.
        public bool BurningOut => _burnOut && _dying;
        /// 타들어간 정도 0~1.
        public float BurnOut01 => _burnOut ? Mathf.Clamp01((Time.time - _burnOutStart) / Mathf.Max(0.05f, ZombieBurnOut.Duration)) : 0f;
        /// 타는 몸의 높이(m) — 죽는 순간 렌더러 경계에서 잰 값.
        public float BurnHeight => _burnAnchor.w;

        static readonly Color BurnTint     = new Color(0.62f, 0.60f, 0.58f, 1f);  // 그을림
        static readonly Color BurnEmission = new Color(0.42f, 0.13f, 0.02f, 1f);  // 속은 벌겋게
        static readonly Color CharTint     = new Color(0.06f, 0.055f, 0.05f, 1f); // 재

        // ── 폭발 그을음 (로켓) ───────────────────────────────────────
        // 로켓 폭발 반경에 든 좀비는 죽을 때까지 검게 남는다. 화상과 달리 시간이 지나도
        // 안 풀린다 — "이 좀비는 폭발을 맞았다"가 계속 보이는 게 요청이었다.
        //
        // 위 CharTint(재, 0.06)보다 살짝 밝게 둔 이유: 화상 사망 연출의 완전한 검정과
        // 구분되어야 하고, 저채도 야간 배경(§15)에서 실루엣이 아예 묻히면 안 된다.
        // 그래도 어두운 편이라 배경에 묻히는지는 실기 화면에서 봐야 한다 — 여기 값만 고치면 된다.
        static readonly Color BlastTint = new Color(0.11f, 0.10f, 0.095f, 1f);
        bool _blasted;

        // 엘리트는 몸이 은은한 금빛 — 진화 상자(같은 금색)를 떨군다는 걸 색으로 묶는다
        static readonly Color EliteEmission = new Color(0.42f, 0.30f, 0.05f, 1f);

        // 마지막으로 렌더러에 밀어 넣은 값. 같으면 다시 쓰지 않는다 —
        // 300마리 × 렌더러 수만큼 매 프레임 SetPropertyBlock 을 부르면 그것만으로 예산을 먹는다.
        Color _pushedBase, _pushedEmis;
        bool  _pushedOnce;

        // 종류별 스킨 교체용. 풀에서 재사용되므로 프리팹 원본을 기억해 두고 되돌린다.
        Material[] _defaultSkins;

        Vector3 _knockDir;
        float _knockSpeed;      // 초기 속도. 선형 감쇠라 거리 = speed * time / 2
        float _knockUntil;
        float _knockScale = 1f;
        WaterContactFoam _foam;

        // ── 원거리 (ZombieKind.Ranged) ───────────────────────────────
        // 시전은 애니메이션 이벤트가 아니라 시간으로 잰다 — Animator 가 CullCompletely 라
        // 이벤트에 기대면 화면 가장자리에서 구슬이 안 나가는 경우가 생긴다.
        bool  _holding;            // 사거리 안에서 멈춰 선 상태 (Stop/Chase 히스테리시스)
        bool  _casting, _released;
        float _castStart, _nextCast;
        Transform _handL, _handR;
        Transform _charge;         // 시전 중 손에 모이는 구슬 (예고)

        // ── 독가스 자폭 (빨간 좀비 = Runner) ─────────────────────────
        /// 스폰 디렉터가 Spawn 뒤에 끌 수 있다(몬스터 떼 돌격은 그냥 때린다).
        public bool gasBomber;
        bool  _fusing;
        float _fuseEnd;
        Vector3 _baseScale = Vector3.one;
        bool  _baseScaleKnown;
        float _scaleMul = 1f;   // 덩치 배율(EnemyDef.scale). 프리팹 스케일(_baseScale)에 곱한다
        static readonly Color FuseEmission = new Color(0.35f, 0.9f, 0.1f, 1f);

        // ── 좀비 개 (ZombieKind.Dog) ─────────────────────────────────
        // 피해는 무는 순간(DogBiteDelay 뒤)에 들어간다. 트리거와 동시에 주면 입을 벌리기도 전에 아프다.
        float _biteAt = -1f;

        // 최종 보스(망치) 전용 행동. 붙어 있으면 이동·공격을 여기로 넘긴다(ProtoBossBrain 주석).
        ProtoBossBrain _brain;
        static readonly int AnimSpeed = Animator.StringToHash("Speed"), AnimAttack = Animator.StringToHash("Attack"),
                            AnimIsDead = Animator.StringToHash("IsDead"), AnimScream = Animator.StringToHash("Scream");   // 문자열 해시를 매 프레임 안 하게
        bool _animLooked, _brainLooked;   // 없는 컴포넌트를 매 프레임 다시 찾지 않게(보스만 brain 이 있다 — 2026-11-20 최적화)

        void Awake() { EnsureCache(); }

        /// Awake 는 에디터(플레이 모드 밖)에서 호출되지 않는다. 에디터 도구나 테스트가
        /// TakeDamage 를 직접 부를 수 있으므로 캐시를 지연 생성한다.
        void EnsureCache()
        {
            if (_anim == null && !_animLooked) { _anim = GetComponentInChildren<Animator>(); _animLooked = true; }
            if (_brain == null && !_brainLooked) { _brain = GetComponent<ProtoBossBrain>(); _brainLooked = true; }
            if (_foam == null)
            {
                // 물에 들어갔을 때 수면에 남는 파문. 프리팹에 안 붙어 있어도 여기서 붙인다 —
                // 좀비는 풀에서 Instantiate 되므로 프리팹을 손대면 판이 시작된 뒤 반영이 안 된다.
                _foam = GetComponent<WaterContactFoam>();
                if (_foam == null) _foam = gameObject.AddComponent<WaterContactFoam>();
            }
            if (_renderers == null || _renderers.Length == 0) _renderers = GetComponentsInChildren<Renderer>(true);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (!_baseScaleKnown) { _baseScale = transform.localScale; _baseScaleKnown = true; }
            // 프리팹 원본 스킨을 기억한다 — 풀에서 재사용될 때 되돌릴 기준이 필요하다.
            if (_defaultSkins == null && _renderers != null)
            {
                _defaultSkins = new Material[_renderers.Length];
                for (int i = 0; i < _renderers.Length; i++)
                    _defaultSkins[i] = _renderers[i] != null ? _renderers[i].sharedMaterial : null;
            }
        }

        /// 종류별 파문 크기. **좀비는 접촉 띠를 쓰지 않는다** — 물량전에서 흰 띠가 여러 개 겹치면
        /// 수면이 통째로 하얘지고, 그러면 플레이어 위치를 읽기 어려워진다(GDD 15: 대비는 캐릭터에).
        /// 파문은 자리 경쟁(WaterContactFoam.MaxEmitters)에서 플레이어에게 밀리므로 물량전에서도
        /// 화면에 뜨는 개수가 고정이다.
        void ConfigureFoam(ZombieKind k)
        {
            if (_foam == null) return;
            _foam.mode = WaterContactFoam.Mode.Ripples;

            bool boss = k == ZombieKind.Boss;
            _foam.stepDistance  = boss ? 1.0f  : 0.7f;
            _foam.startRadius   = boss ? 0.22f : 0.10f;
            _foam.endRadius     = boss ? 1.00f : 0.50f;
            _foam.life          = boss ? 1.0f  : 0.7f;
            _foam.ringThickness = boss ? 0.10f : 0.06f;
            _foam.sideOffset    = boss ? 0.18f : 0.09f;
            _foam.strength      = boss ? 0.85f : 0.55f;   // 플레이어(1.0)보다 옅게 — 시선을 안 끌게
            _foam.entrySplash   = boss ? 1.6f  : 1.2f;
            _foam.priority      = boss ? 5    : 0;        // 플레이어는 10 이라 항상 이긴다
        }

        /// 수치 줄. Spawn 을 거치지 않은 개체(툴의 더미 등)는 기본값(워커 숫자)으로 움직인다.
        EnemyDef D => def != null ? def : Fallback;
        static EnemyDef _fallback;
        static EnemyDef Fallback
        {
            get
            {
                if (_fallback == null) { _fallback = ScriptableObject.CreateInstance<EnemyDef>(); _fallback.name = "(기본 적 수치)"; }
                return _fallback;
            }
        }

        /// 테이블 줄의 숫자를 이 몸에 옮긴다. Spawn 과 MakeElite 가 같이 쓴다 — 두 곳이 따로 베끼면 칸 하나가 반드시 빠진다.
        void ApplyDef(EnemyDef d, float hpScale)
        {
            def = d;
            maxHP = d.hp * hpScale;
            moveSpeed = d.moveSpeed;
            contactDamage = d.contactDamage;
            xpValue = d.xp;
            bodyRadius = d.BodyRadius;
            separationWeight = d.separationWeight;
            _knockScale = d.knockback;
            _scaleMul = Mathf.Max(0.01f, d.scale);
        }

        public void Spawn(Vector3 pos, EnemyDef d, float hpScale, Transform target)
        {
            if (d == null) { Debug.LogError("[ProtoZombie] Spawn 에 적 수치(EnemyDef)가 없다 — 적 테이블의 칸을 확인할 것."); d = Fallback; }
            ZombieKind k = d.behaviour;
            kind = k; _target = target;
            EnsureCache();
            transform.position = pos;
            transform.rotation = Quaternion.identity;

            // 풀에서 재사용된 개체에 이전 종류의 값이 남지 않게 테이블 줄로 전부 덮는다
            IsElite = false;
            // 풀에서 꺼낸 개체에 이전 판의 플래시/화상/넉백이 남아 있으면 그 상태로 등장한다
            ResetVisual();
            _knockUntil = 0f;
            ApplyDef(d, hpScale);
            ConfigureFoam(k);

            hp = maxHP;
            Alive = true; _dying = false;
            _nextContactTime = 0f;
            _holding = false; _casting = false; _released = false;
            _biteAt = -1f;
            gasBomber = k == ZombieKind.Runner && d.gas.enabled;
            _fusing = false;
            transform.localScale = _baseScale * _scaleMul;   // 예고 중 부푼 채로 풀에 들어갔을 수 있다. 덩치(엘리트·최종 보스)도 여기서
            // 첫 시전은 멈춰 선 뒤 조금 있다가 — 등장하자마자 쏘면 화면에 들어온 걸 볼 틈이 없다
            _nextCast = Time.time + Random.Range(0.8f, 1.6f);
            SetCharge(false, 0f);

            gameObject.SetActive(true);
            if (_anim != null)
            {
                _anim.Rebind();
                _anim.SetBool(AnimIsDead, false);
                _anim.SetFloat(AnimSpeed, moveSpeed);
                // 보스 등장 포효. AC_Boss 에만 있는 트리거라 좀비에게 쏘면 무시된다.
                if (k == ZombieKind.Boss) _anim.SetTrigger(AnimScream);
            }
            // 포효는 화면 전체에 속하는 소리다(거리 감쇠 없음) — 보스는 시야 밖 스폰 링에서
            // 등장하므로 거리로 깎으면 정작 등장을 알려야 할 때 들리지 않는다.
            if (k == ZombieKind.Boss) ProtoAudio.Play2D("boss_roar");
            if (_brain != null) _brain.OnSpawn(this, target);
        }

        /// 방금 워커로 Spawn 한 개체를 엘리트로 올린다(적 테이블의 elite 줄). 풀로 돌아갔다 다시 나오면 Spawn 이 되돌린다.
        public void MakeElite(EnemyDef eliteDef, float hpScale)
        {
            if (eliteDef == null) { Debug.LogError("[ProtoZombie] 엘리트 수치(EnemyDef)가 없다 — 적 테이블의 elite 칸을 확인할 것."); return; }
            IsElite = true;
            ApplyDef(eliteDef, hpScale);
            hp = maxHP;
            gasBomber = false;
            transform.localScale = _baseScale * _scaleMul;
            if (_anim != null) _anim.SetFloat(AnimSpeed, moveSpeed);
            _pushedOnce = false;
            PushVisual();
        }

        void Update()
        {
            float dt = ProtoConfig.Delta;

            // 플래시 만료는 죽는 중에도 처리해야 한다 — 마지막 한 발이 치명타면
            // 그대로 하얀 채로 사망 애니메이션을 재생하게 된다.
            if (_flashOn && Time.time >= _flashUntil) _flashOn = false;
            TickBurn();
            PushVisual();   // 값이 그대로면 안에서 그냥 돌아온다

            if (_dying)
            {
                if (Time.time >= _despawnAt) ProtoSpawnDirector.I?.Release(this);
                return;
            }
            if (!Alive || _target == null) return;

            Vector3 to = _target.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            // 넉백은 추격 중이든 접촉 중이든 똑같이 먹어야 한다.
            // 접촉 분기에서 빼면 붙어 있는 좀비는 아무리 쏴도 꿈쩍하지 않는다.
            Vector3 disp = KnockbackStep(dt);

            if (_brain != null && _brain.enabled) disp += _brain.Tick(to, dist, dt);
            else if (kind == ZombieKind.Ranged) disp += TickRanged(to, dist, dt);
            else if (kind == ZombieKind.Dog) disp += TickDog(to, dist, dt);
            else if (gasBomber) { disp += TickGasBomber(to, dist, dt); if (!Alive) return; }
            else if (dist > D.contactRange + TargetPad + (IsElite ? bodyRadius - ProtoConfig.BaseBodyRadius : 0f))   // 엘리트는 덩치만큼 앞에서 멈춘다
            {
                Vector3 dir = to / Mathf.Max(dist, 0.0001f);
                disp += dir * moveSpeed * dt;
                transform.rotation = Quaternion.LookRotation(dir);
                if (_anim != null) _anim.SetFloat(AnimSpeed, moveSpeed);
            }
            else
            {
                if (_anim != null) _anim.SetFloat(AnimSpeed, 0f);
                if (Time.time >= _nextContactTime)
                {
                    _nextContactTime = Time.time + D.contactCooldown;
                    if (_anim != null) _anim.SetTrigger(AnimAttack);
                    // 휘두르는 소리는 피해보다 **먼저** 나야 한다. 플레이어가 화면을 보기 전에
                    // 귀로 "붙었다"를 알아채는 유일한 단서다.
                    ProtoAudio.Play("zattack", transform.position, IsBoss ? 1.2f : 1f);
                    var p = _target.GetComponent<ProtoPlayer>();
                    if (p != null) p.TakeDamage(contactDamage, ProtoPlayer.DamageKind.Claw);   // 평타 = 할퀴기(빨간 번쩍 + 할퀴기 이펙트)
                }
            }

            if (disp.sqrMagnitude > 0f)
            {
                Vector3 next = transform.position + disp;
                // 프롭 통과 방지. 밀어내기가 침투 얕은 축으로 일어나므로 벽을 따라 미끄러진다.
                var obs = ProtoObstacles.I;
                if (obs != null && obs.blockEnemies) next = obs.ResolveMove(next, bodyRadius);
                if (IsBoss) next = ProtoBossArena.Clamp(next, bodyRadius);   // 최종 보스도 울타리 안에서만
                transform.position = next;
            }
        }

        /// <summary>
        /// 원거리 좀비 한 프레임. 돌려주는 값은 이동 변위.
        /// 쫓아간다 → 사거리(Stop) 안 + 화면 안이면 멈춘다 → 쿨이 차면 시전(1.2초 예고 후 발사) → 반복.
        /// 플레이어가 Chase 거리 밖으로 벗어나면 다시 쫓는다. <b>화면 밖에서는 쏘지 않는다</b> —
        /// 보이지 않는 곳에서 날아오는 구슬은 피할 방법이 없다.
        /// </summary>
        Vector3 TickRanged(Vector3 to, float dist, float dt)
        {
            Vector3 dir = to / Mathf.Max(dist, 0.0001f);
            if (dist > 0.01f) transform.rotation = Quaternion.LookRotation(dir);

            var rd = D.ranged;
            if (_casting)
            {
                float t = Time.time - _castStart;
                // 손에 구슬이 모인다 — 0.45초부터 발사 순간까지 커진다
                if (!_released)
                {
                    SetCharge(t > 0.45f, Mathf.Clamp01((t - 0.45f) / (rd.release - 0.45f)));
                    if (t >= rd.release)
                    {
                        _released = true;
                        SetCharge(false, 0f);
                        Vector3 from = HandPoint();
                        Vector3 aim = _target.position - from; aim.y = 0f;
                        ProtoEnemyBolt.Fire(new Vector3(from.x, Mathf.Clamp(from.y, 0.7f, 1.2f), from.z), aim, rd.boltSpeed, rd.boltDamage);
                        ProtoAudio.Play("zattack", transform.position, 0.9f);
                    }
                }
                if (t >= rd.castTime) _casting = false;
                if (_anim != null) _anim.SetFloat(AnimSpeed, 0f);
                return Vector3.zero;
            }

            float pad = TargetPad;   // 탱크 탑승 중엔 차체 가장자리 기준
            if (_holding && dist > rd.chaseRange + pad) _holding = false;
            else if (!_holding && dist <= rd.stopRange + pad && OnScreen()) _holding = true;

            if (!_holding)
            {
                if (_anim != null) _anim.SetFloat(AnimSpeed, moveSpeed);
                return dir * moveSpeed * dt;
            }

            if (_anim != null) _anim.SetFloat(AnimSpeed, 0f);
            if (Time.time >= _nextCast && OnScreen())
            {
                _casting = true; _released = false;
                _castStart = Time.time;
                _nextCast = Time.time + rd.cooldown + Random.Range(-0.3f, 0.3f);   // 여럿이 박자 맞춰 쏘지 않게
                if (_anim != null) _anim.SetTrigger(AnimAttack);
            }
            return Vector3.zero;
        }

        /// <summary>
        /// 좀비 개 한 프레임. 돌려주는 값은 이동 변위.
        /// 빠르게 쫓아가 붙으면 문다 → DogBiteDelay 뒤 피해 + 감속. 무는 동안에도 계속 따라붙는다 —
        /// 멈춰서 물면 플레이어가 한 걸음만 떼도 빠져나가 "물렸다"가 거의 안 일어난다.
        /// </summary>
        Vector3 TickDog(Vector3 to, float dist, float dt)
        {
            Vector3 dir = to / Mathf.Max(dist, 0.0001f);
            if (dist > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
            float reach = D.contactRange + TargetPad;

            // 예약된 물기. 그사이 멀어졌으면(조금은 봐준다) 허공을 문다.
            if (_biteAt > 0f && Time.time >= _biteAt)
            {
                _biteAt = -1f;
                if (dist <= reach + 0.4f)
                {
                    var p = _target.GetComponent<ProtoPlayer>();
                    if (p != null)
                    {
                        // 피해가 막히는 물림(보호막·피격 직후 무적)은 감속도 안 건다 — 피해를 주기 **전에** 본다
                        // (피해가 무적을 새로 걸기 때문). 예전엔 HP 는 그대로인데 느려져 "이유 없이 느려진다"로 보였다(2026-10-21).
                        bool blocked = p.IsInvulnerable || p.IsShielded;
                        p.TakeDamage(contactDamage);
                        if (!blocked) p.ApplySlow(D.bite.slowMult, D.bite.slowTime);
                    }
                }
            }

            if (dist <= reach && Time.time >= _nextContactTime)
            {
                _nextContactTime = Time.time + D.contactCooldown;
                _biteAt = Time.time + D.bite.delay;
                if (_anim != null) _anim.SetTrigger(AnimAttack);
                ProtoAudio.Play("zattack", transform.position, 0.8f);
            }

            if (_anim != null) _anim.SetFloat(AnimSpeed, dist > reach ? moveSpeed : 0f);
            // 붙어 있으면 그 자리 — 플레이어 몸을 파고들지 않게
            return dist > reach ? dir * moveSpeed * dt : Vector3.zero;
        }

        /// <summary>
        /// 자폭 러너 한 프레임. 쫓아가 GasTriggerRange 안이면 멈춰서 예고(부풀며 초록 깜빡임) →
        /// GasFuseTime 뒤 터진다: 독가스를 남기고 경험치 없이 사라진다.
        /// 예고가 시작되면 플레이어가 멀어져도 취소하지 않는다 — 빠져나가는 게 대응이다.
        /// </summary>
        Vector3 TickGasBomber(Vector3 to, float dist, float dt)
        {
            Vector3 dir = to / Mathf.Max(dist, 0.0001f);
            var gd = D.gas;
            if (_fusing)
            {
                float k = 1f - Mathf.Clamp01((_fuseEnd - Time.time) / gd.fuseTime);   // 0 → 1
                transform.localScale = _baseScale * _scaleMul * (1f + 0.3f * k * k + 0.04f * Mathf.Sin(Time.time * 45f));
                if (_anim != null) _anim.SetFloat(AnimSpeed, 0f);
                PushVisual();
                if (Time.time >= _fuseEnd) GasBurst();
                return Vector3.zero;
            }
            if (dist <= gd.triggerRange + TargetPad)
            {
                _fusing = true;
                _fuseEnd = Time.time + gd.fuseTime;
                ProtoAudio.Play("zattack", transform.position, 0.9f);   // 귀로 먼저 "붙었다"
                return Vector3.zero;
            }
            if (dist > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
            if (_anim != null) _anim.SetFloat(AnimSpeed, moveSpeed);
            return dir * moveSpeed * dt;
        }

        void GasBurst()
        {
            _fusing = false;
            Alive = false; _dying = false;
            _burnUntil = 0f;
            ProtoPoisonGas.Burst(transform.position, D.gas);
            transform.localScale = _baseScale * _scaleMul;
            // 처치가 아니다 — 젬·처치 수 없이 바로 풀로. 몸은 섬광·구름 뒤에서 사라진다.
            ProtoSpawnDirector.I?.Release(this);
        }

        bool OnScreen()
        {
            var cam = Camera.main;
            if (cam == null) return true;
            Vector3 v = cam.WorldToViewportPoint(transform.position + Vector3.up);
            // 가장자리 10% 는 화면 밖으로 친다 — 몸이 반쯤 잘린 채 쏘면 어디서 날아왔는지 안 읽힌다
            return v.z > 0f && v.x > 0.1f && v.x < 0.9f && v.y > 0.08f && v.y < 0.92f;
        }

        Vector3 HandPoint()
        {
            if (_handL == null && _anim != null && _anim.isHuman)
            {
                _handL = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
                _handR = _anim.GetBoneTransform(HumanBodyBones.RightHand);
            }
            if (_handL != null && _handR != null) return (_handL.position + _handR.position) * 0.5f;
            return transform.position + Vector3.up * 1.1f + transform.forward * 0.6f;
        }

        /// 손에 모이는 구슬. 처음 쓸 때 만든다(원거리 좀비만).
        void SetCharge(bool on, float k)
        {
            if (_charge == null)
            {
                if (!on) return;
                _charge = ProtoEnemyBolt.CreateChargeOrb(transform);
            }
            if (_charge.gameObject.activeSelf != on) _charge.gameObject.SetActive(on);
            if (!on) return;
            _charge.position = HandPoint();
            var cam = Camera.main;
            if (cam != null) _charge.rotation = cam.transform.rotation;
            _charge.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.5f, k) * (1f + 0.15f * Mathf.Sin(Time.time * 30f));
        }

        /// 이번 프레임의 넉백 변위. 선형으로 감쇠한다.
        Vector3 KnockbackStep(float dt)
        {
            if (Time.time >= _knockUntil) return Vector3.zero;
            float k = (_knockUntil - Time.time) / ProtoConfig.KnockbackTime;   // 1 -> 0
            return _knockDir * (_knockSpeed * k * dt);
        }

        /// 피해 적용. 실제로 죽였으면 true.
        public bool TakeDamage(float amount) { return TakeDamage(amount, Vector3.zero); }

        /// <summary>
        /// hitDir 은 투사체 진행 방향(수평). zero 면 넉백 없이 플래시만.
        ///
        /// <para>crit 을 인자로 받는 이유: 치명타 여부는 <see cref="ProtoShooter"/> 가 굴려
        /// 투사체가 들고 오는 정보라 여기서는 알 수 없다. 데미지 숫자를 쓰지 않는
        /// 게임(§15)이어서 크리는 총알 크기로만 보였는데, 소리로도 갈라주면 훨씬 빨리 읽힌다.
        /// 기본값 false 라 화염·블레이드·접촉처럼 크리 개념이 없는 경로는 손댈 필요가 없다.</para>
        /// </summary>
        public bool TakeDamage(float amount, Vector3 hitDir, bool crit = false)
        { return DealDamage(amount, true, hitDir, crit); }

        /// <summary>
        /// bigHit = 큰 한 방(로켓 폭발·융단 폭격·교체 피해·필사기). 데미지 숫자는 <b>치명타 · 보스 · 큰 한 방</b>에만 뜬다
        /// (ProtoDamageText, 사용자 결정 A안) — 평범한 탄까지 띄우면 물량전에서 화면이 숫자로 덮인다.
        /// </summary>
        public bool TakeDamage(float amount, Vector3 hitDir, bool crit, bool bigHit)
        { return DealDamage(amount, true, hitDir, crit, bigHit); }

        /// <summary>
        /// 무기 연출 세트가 있는 경로(투사체 · 광선 · 블레이드) — 정확한 착탄점과 <b>그 무기의</b> 불똥·처치 효과를 넘긴다(<see cref="HitFx"/>).
        /// 안 넘기는 경로는 공통 효과를 쓴다.
        /// </summary>
        public bool TakeDamage(float amount, Vector3 hitDir, bool crit, bool bigHit, HitFx fx)
        { return DealDamage(amount, true, hitDir, crit, bigHit, fx); }

        /// flash=false 는 화상처럼 "맞은 느낌"을 내면 안 되는 지속 피해용이다.
        bool DealDamage(float amount, bool flash, Vector3 hitDir, bool crit = false, bool bigHit = false, HitFx fx = default)
        {
            if (!Alive) return false;
            if (hitDir.sqrMagnitude > 0.0001f) _lastHitDir = hitDir;   // 죽을 때 조각이 튀는 방향

            // 숫자는 머리 위(보스는 몸 반경 비율로 더 높이). 치명타·보스·큰 한 방은 항상, 평범한 탄은 화면이 한산할 때만.
            // 기존 식은 같은 대상의 0.5초 안 피해를 하나로 합치고(GetInstanceID 가 그 열쇠), YAZS 식(기본)은 맞을 때마다 한 개씩 띄운다 — ProtoDamageText.Style.
            if (flash)
                ProtoDamageText.Show(transform.position + Vector3.up * (1.9f * Mathf.Max(1f, bodyRadius / ProtoConfig.BaseBodyRadius)),
                                     amount, crit, GetInstanceID(), minor: !(crit || bigHit || IsBoss || IsElite));

            if (flash)
            {
                Flash();
                // 피격 불똥(모양은 카탈로그 프리팹). 무기가 연출 세트(HitFx)를 넘기면 그 ID 와 정확한 착탄점, 아니면 공통 hit_spark 를
                // 가슴 높이의 맞은 쪽에. 큰 한 방(폭발류)은 공통 불똥을 내지 않는다 — 폭발이 이미 보여 준다. 방향 = 날아온 쪽으로 튄다.
                // 치명타는 한 단계 크게. 동시 개수 상한은 카탈로그 maxAlive 가 지킨다(물량전에서 가장 오래된 것부터 회수).
                string sparkId = fx.specified ? fx.spark : (bigHit ? null : FxIds.HitSpark);
                // 피 튀김(hit_blood)은 무기 세트와 상관없이 불똥과 같은 자리에 — 카탈로그에 프리팹이 있을 때만 나온다(2026-11-12).
                // 큰 한 방은 불똥처럼 뺀다. 동시 개수 상한은 카탈로그 maxAlive.
                bool blood = !bigHit;
                if (!string.IsNullOrEmpty(sparkId) || blood)
                {
                    float body = Mathf.Max(1f, bodyRadius / ProtoConfig.BaseBodyRadius);
                    Vector3 back = hitDir.sqrMagnitude > 0.0001f ? -hitDir : Random.insideUnitSphere;
                    back.y = 0f;
                    back = back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.back;
                    Vector3 at = fx.specified && fx.hasPoint ? fx.point
                               : transform.position + Vector3.up * (0.95f * body) + back * (bodyRadius * 0.6f);
                    if (!string.IsNullOrEmpty(sparkId))
                        ProtoFx.Play(sparkId, at, back, body * (fx.specified ? fx.sparkScale : 1f) * (crit ? 1.5f : 1f), crit);
                    if (blood)
                        ProtoFx.Play(FxIds.HitBlood, at, back, body * (crit ? 1.5f : 1f), crit);
                }
                // 탄착음. 화상(flash=false)은 소리를 내지 않는다 — 0.25초마다 울리면
                // 불타는 좀비 한 마리가 보이스 상한을 혼자 다 먹는다.
                // crit.wav 는 탄착 + 금속 핑이 **한 파일에 같이** 들어 있으므로 둘을
                // 겹쳐 재생하지 않는다 (겹치면 충격음이 두 겹이 되어 찌그러진다).
                ProtoAudio.Play(crit ? "crit" : "hit", transform.position);
                // 치명타는 손끝에 '탁' — 약한 흔들림(2026-10-13 사용자). 더 센 흔들림이 진행 중이면 그쪽이 이긴다(AddShake)
                if (crit) ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Light);
                // 발성은 절반만. 매번 울리면 물량전에서 신음이 벽처럼 쌓이고,
                // 탄착음이 담당하는 "맞았다"가 오히려 안 들린다.
                if (Random.value < 0.45f) ProtoAudio.Play("zhurt", transform.position, 0.85f);
            }
            if (hitDir.sqrMagnitude > 0.0001f && _knockScale > 0f)
            {
                hitDir.y = 0f;
                _knockDir   = hitDir.normalized;
                // 선형 감쇠라 이동 거리 = speed * time / 2 → speed = 2 * dist / time
                _knockSpeed = 2f * ProtoConfig.KnockbackDist * _knockScale / ProtoConfig.KnockbackTime;
                _knockUntil = Time.time + ProtoConfig.KnockbackTime;
            }

            // 보스에게 준 피해는 필사기 게이지를 채운다(§06 — 1% 당 2%). 남은 체력을 넘는 몫은 세지 않는다.
            if (IsBoss && maxHP > 0f && ProtoRun.I != null) ProtoRun.I.OnBossDamaged(Mathf.Min(amount, hp) / maxHP);

            hp -= amount;
            if (hp > 0f) return false;
            Die(fx);
            return true;
        }

        void Flash()
        {
            _flashUntil = Time.time + ProtoConfig.HitFlashTime;
            _flashOn = true;
            PushVisual();
        }

        /// <summary>
        /// 화상을 붙인다. <b>겹쳐 쌓지 않고 지속시간만 갱신</b>한다 — 쌓이게 두면 분사 한 번에
        /// 화상이 수십 겹 붙어 화염방사기 DPS 가 기획 수치와 무관해진다.
        /// dps 는 플레이어 공격력 배율이 이미 곱해진 값이다.
        /// </summary>
        public void ApplyBurn(float duration, float dps)
        {
            if (!Alive || duration <= 0f || dps <= 0f) return;
            bool fresh = Time.time >= _burnUntil;
            _burnUntil = Mathf.Max(_burnUntil, Time.time + duration);
            _burnDps = dps;
            if (fresh) _burnNextTick = Time.time + BurnTickInterval;
            PushVisual();
        }

        /// <summary>
        /// 로켓 폭발 반경에 들었다는 표시. <b>한 번 붙으면 죽을 때까지 안 풀린다</b> —
        /// 화상(<see cref="ApplyBurn"/>)처럼 지속시간을 갖지 않는다.
        /// 피격 흰 플래시는 이미션이라 검은 몸에서도 그대로 읽힌다.
        /// </summary>
        public void ApplyBlastSoot()
        {
            if (!Alive || _blasted) return;
            _blasted = true;
            PushVisual();
        }

        void TickBurn()
        {
            if (!Alive || Time.time >= _burnUntil) return;
            if (Time.time < _burnNextTick) return;
            _burnNextTick = Time.time + BurnTickInterval;
            // 화상 피해는 플래시도 넉백도 일으키지 않는다 — 0.25초마다 하얘지면 "타고 있다"가
            // 안 읽히고, 0.25초마다 밀면 좀비가 계속 떨린다.
            DealDamage(_burnDps * BurnTickInterval, false, Vector3.zero);
        }

        /// <summary>피격 플래시 · 화상 · 재를 하나의 MPB 로 합쳐 쓴다.</summary>
        void PushVisual()
        {
            EnsureCache();

            Color baseCol = Color.white;
            Color emis = Color.black;

            if (_burnOut)
            {
                // 타들어가는 중 — 살아서 타던 색(그을림 + 벌건 속)에서 시작해 남은 몸이 재 색으로 식는다.
                // 불선 바로 앞의 새까만 그을음과 밝은 선은 셰이더(BurnDissolve · BurnEdge)가 그린다.
                float k = BurnOut01;
                baseCol = Color.Lerp(BurnTint, CharTint, Mathf.Clamp01(k / 0.7f));
                emis    = Color.Lerp(BurnEmission, Color.black, Mathf.Clamp01(k / 0.8f));
            }
            else if (_charring)
            {
                // 화상 사망 연출이 최우선 — 재가 되며 꺼진다(보스, 또는 타는 머티리얼이 없을 때의 옛 연출).
                float t = Mathf.Clamp01((Time.time - (_charUntil - CharTime)) / CharTime);
                baseCol = Color.Lerp(BurnTint, CharTint, t);
                emis    = Color.Lerp(BurnEmission, Color.black, Mathf.Clamp01(t * 1.4f));
            }
            else
            {
                if (_blasted) baseCol = BlastTint;
                if (IsBurning)
                {
                    // 그을린 좀비는 화상이 붙어도 **더 밝게 만들지 않는다.** BurnTint(0.62)를
                    // 그대로 쓰면 폭발 흔적이 화염에 지워져 검정이 풀린 것처럼 보인다.
                    if (!_blasted) baseCol = BurnTint;
                    emis = BurnEmission;
                }
                else if (IsElite) emis = EliteEmission;
            }

            // 자폭 예고 — 점점 빨라지는 초록 깜빡임
            if (_fusing && Alive)
            {
                float k = 1f - Mathf.Clamp01((_fuseEnd - Time.time) / D.gas.fuseTime);
                float blink = Mathf.Sin(Time.time * Mathf.Lerp(18f, 50f, k)) > 0f ? 1f : 0.25f;
                emis = FuseEmission * (blink * (0.6f + 0.8f * k));
            }

            // 피격 플래시가 화상 이미션을 이긴다. 프로퍼티가 달라 알베도(그을림)는 그대로 남는다.
            if (_flashOn)
            {
                float v = ProtoConfig.HitFlashStrength;
                emis = new Color(v, v, v, 1f);
            }

            // 타들어가는 중엔 진행도가 매 프레임 바뀌므로 색이 그대로여도 다시 쓴다.
            if (!_burnOut && _pushedOnce && baseCol == _pushedBase && emis == _pushedEmis) return;
            _pushedBase = baseCol; _pushedEmis = emis; _pushedOnce = true;

            // 한 블록에 두 값을 **항상 같이** 쓴다. SetPropertyBlock 은 블록을 통째로 갈아끼우므로
            // 한쪽만 쓰는 경로를 만들면 다른 쪽이 조용히 지워진다.
            _mpb.SetColor(BaseColorID, baseCol);
            _mpb.SetColor(EmissionID, emis);
            if (_burnOut)
            {
                // 본체와 불선(같은 렌더러의 두 머티리얼)이 이 값들을 같이 읽는다 — 그래야 선이 구멍 가장자리에 정확히 얹힌다.
                _mpb.SetFloat(DissolveID, BurnOut01);
                _mpb.SetVector(BurnAnchorID, _burnAnchor);
                _mpb.SetVector(NoiseOffsetID, _burnSeed);
                _mpb.SetFloat(NoiseScaleID, _burnNoiseScale);
                _mpb.SetFloat(EdgeWidthID, ZombieBurnOut.EdgeWidth);
                _mpb.SetFloat(HeightBiasID, ZombieBurnOut.HeightBias);
            }
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// 종류별 스킨을 갈아끼운다. null 이면 프리팹 원본으로 되돌린다(풀 재사용 때문에 필수).
        ///
        /// <para><b>함정</b>: 갈아끼울 머티리얼은 원본과 <b>키워드·렌더큐까지</b> 같아야 한다.
        /// `_EMISSION` 이 빠지면 피격 흰 플래시가 아무 에러 없이 죽고(키워드라 MPB 로 못 켠다),
        /// 큐가 2450 이 아니거나 `BCM_PLANAR_SHADOW` 가 빠지면 발밑 그림자가 사라진다.
        /// `tilemap_zombie2` 가 실제로 그 상태였다 — zombie1 을 복제해 텍스처만 바꿔서 맞췄다.</para>
        /// </summary>
        public void SetSkin(Material skin)
        {
            EnsureCache();
            if (_renderers == null || _defaultSkins == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                var want = skin != null ? skin : _defaultSkins[i];
                if (want != null && _renderers[i].sharedMaterial != want)
                    _renderers[i].sharedMaterial = want;
            }
            // 스킨을 바꾸면 MPB 로 밀어둔 색을 다시 써야 한다 — 머티리얼이 바뀌면서
            // 이전 프레임의 그을림·플래시가 그대로 남거나 사라진 것처럼 보인다.
            _pushedOnce = false;
            PushVisual();
        }

        /// 풀에서 꺼낸 개체에 이전 판의 플래시·화상·재가 남아 있으면 그 상태로 등장한다.
        void ResetVisual()
        {
            if (_bodyHidden) ShowBody();   // 조각나서 숨긴 채 풀에 들어갔다
            EndBurnOut();                  // 타들어간 채 풀에 들어갔다 — 머티리얼 · 애니 속도를 되돌린다
            _flashOn = false; _flashUntil = 0f;
            _burnUntil = 0f; _burnNextTick = 0f; _burnDps = 0f;
            _charring = false; _charUntil = 0f;
            _blasted = false;
            _pushedOnce = false;
            PushVisual();
        }

        /// fx = 죽인 무기의 연출 지시(HitFx). 기본값이면 공통 처치 효과.
        void Die(HitFx fx = default)
        {
            // 타는 중에 죽었으면 재가 되며 꺼진다. Alive 를 내리기 전에 봐야 한다.
            if (IsBurning) { _charring = true; _charUntil = Time.time + CharTime; }
            _burnUntil = 0f;

            Alive = false; _dying = true;
            if (_brain != null) _brain.OnDie();
            _casting = false; SetCharge(false, 0f); _biteAt = -1f;
            if (_fusing) { _fusing = false; transform.localScale = _baseScale * _scaleMul; }   // 예고 중에 죽이면 가스 없이 쓰러진다   // 시전 중에 죽으면 손의 구슬도 꺼진다
            // 사망 발성. 보스는 조금 크게 — 유일하게 한 마리만 죽는 적이라 상한에 걸리지 않는다.
            ProtoAudio.Play("zdie", transform.position, IsBoss ? 1.3f : 1f);
            // 처치 공통 폭발(카탈로그 fx_zombie_death) — 무기와 상관없이 모든 적, 몸 가운데에서 몸 크기대로(PlayDeathFx, 2026-11-13).
            // 몸 조각 · 피를 내기 전에 잰다 — 조각을 내면 몸을 숨긴다. 아래 무기 처치 효과 · 몸 조각 · 피 터짐도 그대로 같이 난다(사용자 "전 이펙트도 살려줘").
            EnsureCache();
            PlayDeathFx(transform, _renderers, Mathf.Max(1f, bodyRadius / ProtoConfig.BaseBodyRadius), IsBoss || IsElite, out _, out _);
            // 처치 터짐(모양은 카탈로그 프리팹). 죽인 무기의 세트가 있으면 그 ID, 아니면 공통 kill_burst.
            // 자리 = 몸 가운데, 크기 = 덩치, big = 보스·엘리트. 사망 애니메이션과 쓰러진 몸(DeathLinger)은 그대로 — 그 위에 얹는 "잡았다" 신호다.
            string killId = fx.specified ? fx.kill : FxIds.KillBurst;
            if (!string.IsNullOrEmpty(killId))
            {
                float body = Mathf.Max(1f, bodyRadius / ProtoConfig.BaseBodyRadius);
                ProtoFx.Play(killId, transform.position + Vector3.up * (0.8f * body), Vector3.zero,
                             body * (fx.specified ? fx.killScale : 1f), IsBoss || IsElite);
            }
            // 몸 조각 + 피 터짐(2026-11-05 사용자: "적이 죽으면 피 터지며 파편이 터지는 이펙트"). 조각은 코드 그림(ZombieGibs, 본 단위),
            // 피·바닥 피는 카탈로그 zombie_gore. 보스(자기 연출)와 불타 죽은 것(재가 된다)은 제외. 조각을 냈으면 몸을 숨기고 일찍 거둔다.
            bool gibbed = false;
            if (ZombieGibs.Enabled && !IsBoss && !_charring)
            {
                float body = Mathf.Max(1f, bodyRadius / ProtoConfig.BaseBodyRadius);
                Vector3 center = transform.position + Vector3.up * (0.8f * body);
                gibbed = ZombieGibs.Burst(gameObject, center, transform.position.y, _lastHitDir, body);
                if (gibbed)
                {
                    HideBody();
                    ProtoFx.Play(FxIds.ZombieGore, center, _lastHitDir, body, IsBoss || IsElite);
                }
            }
            // 불타 죽었으면 새까매져 누워 있는 대신 타들어가며 사라진다(2026-11-10 사용자: "불타 죽는 좀비는 디졸브로 타들어가는 느낌").
            // 보스는 자기 사망 연출이 길어 제외 — 옛 그을림 그대로. 타는 머티리얼이 없어도 옛 연출로 떨어진다.
            bool burnOut = _charring && !IsBoss && BeginBurnOut();
            _despawnAt = Time.time + (gibbed ? GibLinger : burnOut ? ZombieBurnOut.Duration + BurnOutTail : D.deathLinger);
            if (_anim != null)
            {
                _anim.SetFloat(AnimSpeed, 0f); _anim.SetBool(AnimIsDead, true);
                if (burnOut) _anim.speed = ZombieBurnOut.AnimSpeed;   // 천천히 무너지며 탄다 — 무늬가 월드 좌표라 빨리 움직이면 몸 위를 흐른다
            }
            ProtoSpawnDirector.I?.OnZombieKilled(this);
        }

        /// 처치 공통 폭발의 기준 키 — 워커 좀비(Zombie_Walker, 배율 1)를 렌더러 바운즈로 잰 높이(m). 이 키일 때 scale 1 = 프리팹 크기 그대로.
        public const float DeathFxRefHeight = 1.72f;

        /// <summary>
        /// 좀비 처치 공통 폭발(카탈로그 <c>fx_zombie_death</c>)을 몸 가운데에, 몸 크기대로 낸다. 프리팹이 비어 있으면 아무것도 안 하고 false —
        /// 부르는 쪽이 예전 처치 효과(무기 세트 kill 슬롯)를 낸다.
        /// <para><b>크기 = 몸 높이 / <see cref="DeathFxRefHeight"/>.</b> 몸 높이는 렌더러(스킨 · 메시) 바운즈 윗면 − 발 높이로 잰다 —
        /// 적마다 모델 자체 크기가 달라서(개 · 보스) bodyRadius 로는 비율이 안 맞는다. 렌더러가 없거나 다 꺼져 있으면 fallbackBody(덩치 배율)로.</para>
        /// 몸 조각을 내기 전(몸을 숨기기 전)에 불러야 잰다. FX 툴 무기 무대의 더미도 같은 함수를 쓴다.
        /// </summary>
        public static bool PlayDeathFx(Transform body, Renderer[] renderers, float fallbackBody, bool big, out Vector3 center, out float size)
        {
            Vector3 feet = body != null ? body.position : Vector3.zero;
            center = feet; size = 0f;
            if (body == null || !ProtoFx.HasPrefab(FxIds.ZombieDeath)) return false;

            float top = float.NegativeInfinity;
            if (renderers != null)
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (!(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;   // 손의 구슬 · 파티클은 빼고 몸만
                    top = Mathf.Max(top, r.bounds.max.y);
                }
            float h = top - feet.y;
            if (h > 0.2f && h < 20f) { size = h / DeathFxRefHeight; center = feet + Vector3.up * (h * 0.5f); }
            else { size = Mathf.Max(0.1f, fallbackBody); center = feet + Vector3.up * (0.8f * size); }
            ProtoFx.Play(FxIds.ZombieDeath, center, Vector3.zero, size, big);
            return true;
        }

        /// <summary>
        /// 몸의 머티리얼을 타는 것(본체 디졸브 + 가산 불선)으로 갈아끼우고 타들어가기 시작한다. 갈아끼운 렌더러가 하나도 없으면 false.
        /// 진행도는 <see cref="PushVisual"/> 이 매 프레임 MPB 로 민다. 되돌리기는 <see cref="EndBurnOut"/>(풀에서 다시 나올 때).
        /// </summary>
        bool BeginBurnOut()
        {
            if (!ZombieBurnOut.Enabled || !ZombieBurnOut.Ready) return false;
            EnsureCache();
            if (_renderers == null || _renderers.Length == 0) return false;
            if (_burnSaved == null || _burnSaved.Length != _renderers.Length) _burnSaved = new Material[_renderers.Length][];

            int swapped = 0;
            float top = transform.position.y;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                _burnSaved[i] = null;
                if (r == null || !r.enabled || !(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;
                var src = r.sharedMaterials;
                var burn = ZombieBurnOut.Materials(src);   // 좀비 몸(툰)이 아니면 null — 손의 구슬 · 물 파문은 그대로 둔다
                if (burn == null) continue;
                _burnSaved[i] = src;
                r.sharedMaterials = burn;
                top = Mathf.Max(top, r.bounds.max.y);
                swapped++;
            }
            if (swapped == 0) return false;

            float body = Mathf.Max(1f, bodyRadius / ProtoConfig.BaseBodyRadius);
            Vector3 p = transform.position;
            _burnAnchor = new Vector4(p.x, p.y, p.z, Mathf.Clamp(top - p.y, 0.5f, 6f));
            _burnSeed = new Vector4(Random.value * 100f, Random.value * 100f, Random.value * 100f, 0f);   // 마리마다 다른 무늬
            _burnNoiseScale = ZombieBurnOut.NoiseScale / body;                                               // 덩치가 크면 무늬도 같이 커진다
            _burnOut = true; _burnOutStart = Time.time;
            _pushedOnce = false;
            PushVisual();
            ProtoBurnFx.EmberBurst(this);
            return true;
        }

        /// 타는 머티리얼을 원래대로 되돌린다. 타들어가지 않았으면 아무 일도 안 한다.
        void EndBurnOut()
        {
            if (!_burnOut) return;
            _burnOut = false;
            if (_burnSaved != null && _renderers != null)
                for (int i = 0; i < _renderers.Length && i < _burnSaved.Length; i++)
                {
                    if (_burnSaved[i] == null) continue;
                    if (_renderers[i] != null) _renderers[i].sharedMaterials = _burnSaved[i];
                    _burnSaved[i] = null;
                }
            if (_anim != null) _anim.speed = 1f;
            _mpb?.Clear();   // 디졸브 값들을 지운다 — 색 둘은 바로 뒤 PushVisual 이 다시 쓴다
        }

        /// 조각난 몸을 숨긴다(렌더러만 — 죽음 처리·풀 반환은 그대로).
        void HideBody()
        {
            EnsureCache();
            _bodyHidden = true;
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].enabled = false;
        }

        void ShowBody()
        {
            _bodyHidden = false;
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].enabled = true;
        }

        public void ForceDespawn()
        {
            Alive = false; _dying = false;
            gameObject.SetActive(false);
        }
    }
}
