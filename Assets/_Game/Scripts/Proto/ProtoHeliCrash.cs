using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 헬기 추락 이벤트 — 프로토 한정. 한 판에 1~2번, 좀비 떼가 매달린 공격헬기가 화면에 들어와
    /// 균형을 잃고 불붙어 빙글빙글 돌며 플레이어 근처에 떨어진다. 떨어진 자리 주변에 큰 피해.
    ///
    /// <para><b>연출 순서</b>(2026-10-20 사용자 콘티):
    /// ① 불붙은 채 빙글빙글 돌며 화면 밖에서 진입(매달린 좀비들이 할퀸다) → ② "쿵" 엔진 2차 피격, 불·연기가 커진다 →
    /// ③ 충돌 1초 전 예고 원, 0.5초 만에 내리꽂힌다 → ④ 지면 충돌: 큰 폭발, 부품·좀비가 사방으로 튄다 → 잔해가 잠깐 탄다.</para>
    ///
    /// <para><b>판정:</b> 바닥 예고 원(<see cref="ProtoBossFx.ShowTelegraph"/>) 테두리 = 피해 반경, 채움이 가득 차는 순간이 충돌.
    /// 좀비·보스·승합차·플레이어 모두 맞는다(플레이어는 예고 원을 보고 피할 수 있다). 잔해 불은 좀비에게만 화상.</para>
    ///
    /// <para>게임 시간(scaled)으로 돈다 — 레벨업 카드로 멈추면 같이 멈춘다. 씬 배선 없음(<see cref="ProtoRun"/> 이 Ensure).
    /// 디버그: F4.</para>
    /// </summary>
    public class ProtoHeliCrash : MonoBehaviour
    {
        // ── 일정 ─────────────────────────────────────────────────────────
        const float FirstMin = 70f,  FirstMax = 120f;    // 첫 번째는 반드시
        const float SecondChance = 0.6f;                 // 두 번째는 이 확률로
        const float SecondMin = 170f, SecondMax = 230f;
        const float BusyRetry = 3f;                      // 융단 폭격 중이면 이만큼 미룬다(헬기 소리가 겹친다)

        // ── 피해 ─────────────────────────────────────────────────────────
        public const float Radius       = 5.5f;   // 충돌 피해 반경(예고 원 테두리)
        const float BlastFxScale        = 2.2f;   // 충돌 폭발 프리팹(FX_Car_Explosion) 배율 — 승합차 2 기준, 예전 코드 그림 크기 비(5.5 : 5). 11-13 1.1 → 2.2("안 보인다")
        public const float ZombieDamage = 450f;   // 잡좀비는 확실히, 보스는 일부 (공격력 배율을 곱한다)
        public const float PlayerDamage = 25f;    // 예고 원 안에 서 있으면
        public const float VanDamage    = 400f;   // 승합차 — 근처에 있으면 연쇄로 터진다
        const float BurnRadius = 3.2f;            // 잔해 불 — 좀비에게만 화상
        const float BurnTime   = 4f;
        const float BurnDps    = 60f;

        // ── 비행 ─────────────────────────────────────────────────────────
        // 2026-10-20 사용자 피드백: 마지막에 천천히 돌며 내려오니 피하기 너무 쉽다, 예고도 너무 이르다 →
        // 처음부터 불붙어 돌면서 들어오고, 마지막엔 짧게 내리꽂는다. 예고 원은 충돌 직전에만.
        const float ApproachTime = 2.6f;          // 진입(돌면서 비틀거림) → 추락 시작
        const float StaggerAt    = 1.1f;          // 엔진 2차 피격("쿵") — 더 심하게 흔들린다
        const float FallTime     = 0.5f;          // 내리꽂기
        const float SpinStart = 150f, SpinEnd = 330f;   // 진입 중 회전 속도(도/초) — 점점 빨라진다
        const float TelegraphLead = 1.0f;         // 충돌 몇 초 전에 예고 원을 띄우나
        const float AimReach  = 2.5f;             // 예고 순간 충돌 지점을 플레이어 쪽으로 이만큼까지 당긴다
        // 카메라가 ~15m 높이에서 내려다본다 — 높이 띄우면 카메라에 붙어 거대해지거나 화면 아래로 빠진다(융단 폭격 DropHeight 와 같은 이유)
        const float EntryDist = 20f, EntryHeight = 4.5f; // 화면 밖에서 들어온다
        const float LoseDist  = 6f,  LoseHeight  = 3.8f;
        const float CrashMin  = 4.5f, CrashMax   = 7f;   // 플레이어에서 충돌 지점까지 — 화면 안, 발밑은 아니게
        const float WreckLinger = BurnTime, WreckSink = 1.2f;
        const float Gravity = 20f;

        // ── 매달린 좀비 ───────────────────────────────────────────────────
        // 헬기 루트 로컬(5배 모델 기준, 기수 +Z). yaw 는 헬기를 바라보는 방향.
        static readonly Vector4[] ClingSlots =
        {
            new Vector4(-0.95f, 0.9f,  1.4f,  90f),   // 조종석 왼쪽
            new Vector4( 0.95f, 0.9f,  0.9f, -90f),   // 조종석 오른쪽
            new Vector4(-1.6f,  0.8f, -0.3f,  90f),   // 왼쪽 날개
            new Vector4( 1.6f,  0.8f, -0.4f, -90f),   // 오른쪽 날개
            new Vector4( 0.1f,  2.2f, -1.3f, 180f),   // 등 위
            new Vector4(-0.5f,  1.3f, -2.9f,  90f),   // 꼬리
            new Vector4( 0.4f, -1.3f,  0.6f,   0f),   // 바퀴에 매달림
            new Vector4(-0.4f, -1.2f, -0.7f, 180f),
        };
        const float ClingScale = 0.85f;

        // 잔해: 동체는 한 덩어리로 내려앉고 로터·꼬리 로터·꼬리 붐만 떨어져 날아간다
        static readonly HashSet<string> Hull = new HashSet<string>
            { "mesh_rep_0_ori_repair_quad", "Object002", "Object003", "Object004", "Object005", "Object006", "Object009" };
        const string MainRotor = "Object001", TailRotor = "Object007";
        static readonly Vector3 EngineLocal = new Vector3(0.75f, 1.8f, -0.5f);

        static ProtoHeliCrash _instance;

        class Clinger
        {
            public Transform tf;
            public Animator anim;
            public Vector3 home, vel, spin;
            public float nextClaw, seed, landedAt = -1f;
            public bool flying, falling;   // falling = 진입 중 먼저 떨어져 나간 한 마리(피해 없음)
        }

        readonly List<float> _schedule = new List<float>();
        readonly List<Clinger> _clingers = new List<Clinger>();
        Transform _heli, _rotor, _tail;
        Vector3 _start, _lose, _crash, _dirIn;
        float _t = -1f, _yaw, _rotorSpeed, _burnNext;
        int _phase;   // 0 진입, 1 추락, 2 잔해
        bool _staggered, _aimed;
        ProtoBossFx.Telegraph _tele;
        ParticleSystem _fire, _smoke;
        Material _smokeMat;
        AudioSource _voice;
        float _voiceFade = -1f;

        public static bool Active => _instance != null && _instance._t >= 0f;

        /// 런 시작 때(ProtoRun.Awake) — 일정을 짠다.
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new GameObject("_HeliCrash").AddComponent<ProtoHeliCrash>();
            _instance.Plan();
        }

        /// 재시작 — 떠 있던 헬기·잔해를 치우고 일정을 다시 짠다.
        public static void ResetAll()
        {
            if (_instance == null) return;
            _instance.Clear();
            _instance.Plan();
        }

        /// 결과 화면 — 헬기 소리만 끊는다(장면은 정지 상태 그대로).
        public static void Stop()
        {
            if (_instance != null && _instance._voice != null) { Destroy(_instance._voice.gameObject); _instance._voice = null; }
        }

        /// 디버그(F4): 지금 바로 한 대. 이미 떨어지는 중이면 무시.
        public static void Play(Vector3 center)
        {
            Ensure();
            if (!Active) _instance.Begin(center);
        }

        void OnDestroy() { if (_instance == this) _instance = null; if (_smokeMat != null) Destroy(_smokeMat); }

        void Plan()
        {
            _schedule.Clear();
            _schedule.Add(Random.Range(FirstMin, FirstMax));
            if (Random.value < SecondChance) _schedule.Add(Random.Range(SecondMin, SecondMax));
        }

        // ── 시작 ─────────────────────────────────────────────────────────
        void Begin(Vector3 center)
        {
            var prefab = Resources.Load<GameObject>("Vehicles/Heli01");
            if (prefab == null) { Debug.LogWarning("[헬기 추락] Resources/Vehicles/Heli01 없음 — 이벤트를 건너뛴다"); return; }
            Clear();

            center.y = 0f;
            // 화면은 세로보다 가로가 넓다 — 충돌 지점은 플레이어 좌우, 헬기는 반대편에서 플레이어 머리 위를
            // 가로질러 와서 떨어진다. 위·아래에서 오면 진입과 비틀거림이 화면 밖에서 다 끝나 버린다.
            var cam = Camera.main;
            Vector3 camFwd = cam != null ? Flat(cam.transform.forward).normalized : Vector3.forward;
            if (camFwd.sqrMagnitude < 0.01f) camFwd = Vector3.forward;
            Vector3 camRight = Vector3.Cross(Vector3.up, camFwd);
            float sideSign = Random.value < 0.5f ? 1f : -1f;
            _crash = PickCrashSpot(center, camRight * sideSign, camFwd);
            sideSign = Vector3.Dot(_crash - center, camRight) >= 0f ? 1f : -1f;   // 경계에 밀려 반대로 갔을 수 있다
            _dirIn = Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f) * (camRight * sideSign);
            _start = _crash - _dirIn * EntryDist + Vector3.up * EntryHeight;
            _lose  = _crash - _dirIn * LoseDist  + Vector3.up * LoseHeight;

            _yaw = Quaternion.LookRotation(_dirIn).eulerAngles.y;
            _heli = Instantiate(prefab, _start, Quaternion.Euler(0f, _yaw, 0f), transform).transform;
            _heli.name = "Heli";
            _rotor = MakePivot(MainRotor);
            _tail  = MakePivot(TailRotor);
            _rotorSpeed = 1100f;

            SpawnClingers();
            BuildFire();
            SetEmission(_fire, 40f);    // 처음부터 불붙은 채 들어온다 — 2차 피격 때 더 커진다
            SetEmission(_smoke, 16f);
            StartVoice();
            // 예고 원은 여기서 띄우지 않는다 — 충돌 TelegraphLead 초 전에 Aim() 이 띄운다

            _t = 0f;
            _phase = 0;
            _staggered = false;
            _aimed = false;
        }

        /// side = 화면 좌/우 중 고른 쪽, fwd = 화면 위쪽. 몇 번 실패하면 반대쪽도 본다.
        Vector3 PickCrashSpot(Vector3 around, Vector3 side, Vector3 fwd)
        {
            float lim = ProtoConfig.ArenaHalf - 2f;
            var obs = ProtoObstacles.I;
            Vector3 fallback = around + side * CrashMin;
            for (int i = 0; i < 24; i++)
            {
                Vector3 s = i < 12 ? side : -side;
                // 화면 아래쪽으로 당겨 둔다 — 공중의 헬기는 높이만큼(×~0.6) 화면 위로 떠 보여서, 안 당기면 진입이 화면 위에 잘린다
                Vector3 p = around + s * Random.Range(CrashMin, CrashMax) + fwd * Random.Range(-3.5f, -0.5f);
                p.x = Mathf.Clamp(p.x, -lim, lim);
                p.z = Mathf.Clamp(p.z, -lim, lim);
                Vector3 r = obs != null ? obs.Resolve(p, 2.5f, 6) : p;
                if ((r - p).sqrMagnitude > 0.25f) { fallback = r; continue; }   // 건물에 걸침
                return r;
            }
            return fallback;
        }

        /// 메시 피벗이 전부 모델 원점이라 그대로 돌리면 크게 원을 그린다 — 바운드 중심에 빈 축을 끼운다(ProtoTankDebris 와 같은 문제).
        Transform MakePivot(string mesh)
        {
            foreach (var r in _heli.GetComponentsInChildren<Renderer>())
            {
                if (r.name != mesh) continue;
                var pivot = new GameObject(mesh + "_Spin").transform;
                pivot.SetParent(r.transform.parent, false);
                pivot.position = r.bounds.center;
                pivot.rotation = _heli.rotation;
                r.transform.SetParent(pivot, true);
                return pivot;
            }
            return null;
        }

        void SpawnClingers()
        {
            var dir = ProtoSpawnDirector.I;
            var prefab = dir != null ? dir.zombiePrefab : null;
            if (prefab == null) return;
            for (int i = 0; i < ClingSlots.Length; i++)
            {
                var s = ClingSlots[i];
                var go = Instantiate(prefab, _heli);
                go.name = "Clinger";
                var c = new Clinger
                {
                    tf = go.transform,
                    anim = go.GetComponentInChildren<Animator>(),
                    home = new Vector3(s.x, s.y, s.z),
                    seed = Random.value * 10f,
                    nextClaw = Random.Range(0f, 0.8f),
                };
                c.tf.localPosition = c.home;
                c.tf.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), s.w + Random.Range(-20f, 20f), Random.Range(-8f, 8f));
                c.tf.localScale = Vector3.one * ClingScale;
                if (c.anim != null)
                {
                    c.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    c.anim.speed = Random.Range(0.9f, 1.25f);
                    c.anim.SetFloat(ProtoAnimIds.Speed, 0f);
                }
                _clingers.Add(c);
            }
        }

        void BuildFire()
        {
            var src = Resources.Load<GameObject>(ProtoBurnFx.FxPath);
            if (src == null) return;
            _fire = MakeEmitter(src, "Fire", 3f);
            _smoke = MakeEmitter(src, "Smoke", 3.2f);
            if (_smoke != null)
            {
                // 불 머티리얼은 가산 — 검은 연기가 안 보이므로 알파 블렌드로 복제한다
                var r = _smoke.GetComponent<ParticleSystemRenderer>();
                if (_smokeMat == null)
                {
                    _smokeMat = new Material(r.sharedMaterial) { name = "M_HeliSmoke" };
                    _smokeMat.SetFloat(ProtoShaderIds._Blend, 0f);
                    _smokeMat.SetFloat(ProtoShaderIds._SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _smokeMat.SetFloat(ProtoShaderIds._DstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }
                r.sharedMaterial = _smokeMat;
                var m = _smoke.main;
                m.startColor = new Color(0.08f, 0.08f, 0.09f, 0.75f);
                m.startLifetimeMultiplier = 2.2f;
                m.gravityModifierMultiplier = -0.25f;
            }
        }

        ParticleSystem MakeEmitter(GameObject src, string name, float size)
        {
            var go = Instantiate(src, _heli);
            go.name = name;
            go.transform.localPosition = EngineLocal;
            var ps = go.GetComponentInChildren<ParticleSystem>(true);
            if (ps == null) return null;
            var m = ps.main;
            m.simulationSpace = ParticleSystemSimulationSpace.World;   // 헬기가 지나간 자리에 꼬리를 남긴다
            m.startSizeMultiplier *= size;
            var em = ps.emission;
            em.rateOverTimeMultiplier = 0f;   // 엔진 피격 전에는 꺼 둔다
            ps.Play();
            return ps;
        }

        void SetEmission(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.rateOverTimeMultiplier = rate;
        }

        // ── 진행 ─────────────────────────────────────────────────────────
        void Update()
        {
            TickVoice();
            float dt = ProtoConfig.Delta;
            if (dt <= 0f) return;   // 레벨업 정지 중

            var run = ProtoRun.I;
            if (_t < 0f)
            {
                if (run == null || !run.IsRunning || run.player == null || _schedule.Count == 0) return;
                if (run.elapsed < _schedule[0]) return;
                // 필사기 폭격만 피한다 — 무전기 무기(진화)는 판 내내 폭격해서 Active 로 보면 헬기가 영영 안 나온다
                if (ProtoAirstrike.UltimateActive || ProtoHeliGunship.Active) { _schedule[0] = run.elapsed + BusyRetry; return; }   // 공습 헬기와 두 대가 겹치면 소리·화면이 엉킨다
                _schedule.RemoveAt(0);
                Begin(run.player.transform.position);
                return;
            }

            _t += dt;
            if (_phase == 0) TickApproach(dt);
            else if (_phase == 1) TickFall(dt);
            else TickWreck(dt);
            TickClingers(dt);
        }

        void TickApproach(float dt)
        {
            float k = Mathf.Clamp01(_t / ApproachTime);
            float e = 1f - (1f - k) * (1f - k);   // 감속 — 무게에 눌려 버벅인다
            Vector3 side = Vector3.Cross(Vector3.up, _dirIn);
            float shake = _staggered ? 1f : 0.35f;
            Vector3 wob = side * Mathf.Sin(_t * 2.3f) * 0.6f * shake + Vector3.up * Mathf.Sin(_t * 3.1f) * 0.35f * shake;
            _heli.position = Vector3.Lerp(_start, _lose, e) + wob;

            _yaw += Mathf.Lerp(SpinStart, SpinEnd, k) * dt;   // 처음부터 통제 불능 — 돌면서 들어온다
            float roll  = Mathf.Sin(_t * 3.3f) * (_staggered ? 18f : 10f) + (_staggered ? Mathf.Sin(_t * 23f) * 3f : 0f);
            float pitch = 10f + Mathf.Sin(_t * 2.1f) * 4f * shake;
            _heli.rotation = Quaternion.Euler(pitch, _yaw, roll);
            SpinRotors(dt);

            if (!_staggered && _t >= StaggerAt) Stagger();
            if (!_aimed && _t >= ApproachTime + FallTime - TelegraphLead) Aim();
            if (_t >= ApproachTime) { _phase = 1; _lose = _heli.position; }
        }

        /// 충돌 TelegraphLead 초 전 — 충돌 지점을 지금 플레이어 쪽으로 AimReach 까지 당기고 예고 원을 띄운다.
        /// 미리 정해 둔 자리에만 떨어지면 처음 위치만 보고 비켜서면 끝이라 긴장이 없다.
        void Aim()
        {
            _aimed = true;
            var run = ProtoRun.I;
            if (run != null && run.player != null)
            {
                Vector3 to = Flat(run.player.transform.position) - _crash;
                Vector3 p = _crash + Vector3.ClampMagnitude(to, AimReach);
                var obs = ProtoObstacles.I;
                Vector3 r = obs != null ? obs.Resolve(p, 2.5f, 6) : p;
                if ((r - p).sqrMagnitude <= 0.25f) _crash = r;   // 건물에 걸치면 원래 자리 그대로
            }
            _tele = ProtoBossFx.ShowTelegraph(_crash, Radius, ApproachTime + FallTime - _t);
        }

        /// 엔진 2차 피격 — "쿵!" 작은 폭발, 불·연기가 커지고, 한 마리가 떨어져 나간다.
        void Stagger()
        {
            _staggered = true;
            Vector3 eng = _heli.TransformPoint(EngineLocal);
            ProtoFx.PlayBlast(FxIds.HeliCrashPop, eng, 1.1f, quiet: true);   // 카탈로그 heli_crash_pop(기본 = 코드 그림 폭발, 흔들림 없음)
            ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Light);
            SetEmission(_fire, 70f);
            SetEmission(_smoke, 26f);
            _rotorSpeed = 760f;
            for (int i = 0; i < _clingers.Count; i++)
            {
                var c = _clingers[i];
                if (c.home.y > 0f || c.flying) continue;   // 바퀴에 매달린 녀석 하나
                Fling(c, (_heli.position - _crash).normalized * 1.5f + Vector3.up * 2f);
                c.falling = true;
                break;
            }
        }

        void TickFall(float dt)
        {
            float k = Mathf.Clamp01((_t - ApproachTime) / FallTime);
            Vector3 p = Vector3.Lerp(Flat(_lose), _crash, k);
            p.y = _lose.y * (1f - k * k);   // 가속 낙하 — 짧게 내리꽂는다
            _heli.position = p;

            _yaw += SpinEnd * (1f - k * 0.6f) * dt;   // 돌던 기세가 남은 채
            float pitch = Mathf.Lerp(12f, 50f, k);    // 기수를 처박는다
            float roll  = Mathf.Sin(_t * 5f) * 16f;
            _heli.rotation = Quaternion.Euler(pitch, _yaw, roll);
            _rotorSpeed = Mathf.Lerp(760f, 420f, k);
            SpinRotors(dt);

            if (k >= 1f) Impact();
        }

        void SpinRotors(float dt)
        {
            if (_rotor != null) _rotor.localRotation = Quaternion.AngleAxis(_rotorSpeed * dt, Vector3.up) * _rotor.localRotation;
            if (_tail != null)  _tail.localRotation  = Quaternion.AngleAxis(_rotorSpeed * 1.6f * dt, Vector3.right) * _tail.localRotation;
        }

        // ── 충돌 ─────────────────────────────────────────────────────────
        void Impact()
        {
            _phase = 2;
            _t = 0f;
            _burnNext = 0f;
            Vector3 c = _crash;
            if (_tele != null) { _tele.Cancel(); _tele = null; }

            // 카탈로그 heli_crash_impact — 프리팹 FX_Car_Explosion(2026-11-13, 바닥에 BlastFxScale 배) + 흔들림(Huge) · 폭발음 · 풀은 ImpactFeel.
            // 프리팹을 비우면 예전 코드 그림(보스 충돌: 섬광·화구·충격파·파편·균열·흔들림(Huge)·폭발음)
            VehicleBlastFx.Play(FxIds.HeliCrashImpact, c, Radius, BlastFxScale, () => ProtoBossFx.ImpactFeel(c, Radius, true));
            StartCoroutine(AfterBlasts(c));
            FadeVoice();

            var run = ProtoRun.I;
            float mult = run != null && run.player != null ? run.player.damageMult : 1f;
            var dir = ProtoSpawnDirector.I;
            if (dir != null)
            {
                float rSq = Radius * Radius;
                var list = dir.Active;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var z = list[i];
                    if (z == null || !z.Alive) continue;
                    Vector3 d = Flat(z.transform.position - c);
                    if (d.sqrMagnitude > rSq) continue;
                    z.ApplyBlastSoot();   // 피해보다 먼저 — 이 폭발로 죽어도 그을린 채 쓰러진다
                    z.TakeDamage(ZombieDamage * mult, (d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward) * 3f, false, true, HitFx.Blast(null));
                }
            }
            ProtoDestructibles.DamageInRadius(c, Radius, VanDamage);
            if (run != null && run.player != null && Flat(run.player.transform.position - c).magnitude <= Radius)
                run.player.TakeDamage(PlayerDamage);

            // 매달려 있던 좀비들이 사방으로 튄다
            for (int i = 0; i < _clingers.Count; i++)
            {
                var cl = _clingers[i];
                if (cl.flying) continue;
                Vector3 away = Flat(cl.tf.position - c);
                away = away.sqrMagnitude > 0.01f ? away.normalized : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                Fling(cl, away * Random.Range(4f, 8f) + Vector3.up * Random.Range(7f, 11f));
            }

            // 잔해: 검게 그을려 흩어진다. 불은 더 크게 남는다
            SetEmission(_fire, 70f);
            SetEmission(_smoke, 30f);
            if (_fire != null)  _fire.transform.SetParent(transform, true);
            if (_smoke != null) _smoke.transform.SetParent(transform, true);
            if (_fire != null)  _fire.transform.position  = c + Vector3.up * 0.6f;
            if (_smoke != null) _smoke.transform.position = c + Vector3.up * 1.2f;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(ProtoShaderIds._BaseColor, new Color(0.13f, 0.12f, 0.11f, 1f));
            foreach (var r in _heli.GetComponentsInChildren<Renderer>())
                if (!(r is ParticleSystemRenderer)) r.SetPropertyBlock(mpb);
            ProtoTankDebris.Shatter(_heli, null, c, Hull);
        }

        /// 큰 폭발 뒤 작은 2차 폭발 — 연료가 이어서 터지는 느낌. 판정 없음(연출만).
        System.Collections.IEnumerator AfterBlasts(Vector3 c)
        {
            float[] delays = { 0.25f, 0.5f, 0.8f };
            float t = 0f;
            foreach (var d in delays)
            {
                while (t < d) { t += ProtoConfig.Delta; yield return null; }
                Vector3 off = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(1f, 2.8f);
                ProtoFx.PlayBlast(FxIds.HeliCrashPop, c + off + Vector3.up * 0.3f, 1.6f, quiet: true);
            }
        }

        void TickWreck(float dt)
        {
            // 잔해 불 — 좀비에게 화상(0.25초마다 갱신. ApplyBurn 은 지속시간만 늘린다)
            if (_t < BurnTime && _t >= _burnNext)
            {
                _burnNext = _t + 0.25f;
                var run = ProtoRun.I;
                float mult = run != null && run.player != null ? run.player.damageMult : 1f;
                var dir = ProtoSpawnDirector.I;
                if (dir != null)
                {
                    float rSq = BurnRadius * BurnRadius;
                    var list = dir.Active;
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        var z = list[i];
                        if (z == null || !z.Alive) continue;
                        if (Flat(z.transform.position - _crash).sqrMagnitude > rSq) continue;
                        z.ApplyBurn(1.0f, BurnDps * mult);
                    }
                }
            }
            if (_t >= BurnTime - 1f) { SetEmission(_fire, 0f); SetEmission(_smoke, 0f); }

            // 잠깐 보여 준 뒤 가라앉혀 지운다
            if (_t >= WreckLinger && _heli != null)
            {
                var deb = _heli.GetComponent<ProtoTankDebris>();
                if (deb != null) deb.enabled = false;   // 가라앉는 동안 바닥 판정이 다시 밀어 올리지 않게
                _heli.position += Vector3.down * (2.6f / WreckSink) * dt;
            }
            if (_t >= WreckLinger + WreckSink && AllClingersGone()) Clear();
        }

        // ── 매달린 좀비 ───────────────────────────────────────────────────
        void Fling(Clinger c, Vector3 vel)
        {
            c.flying = true;
            c.tf.SetParent(transform, true);
            c.vel = vel;
            c.spin = Random.onUnitSphere * Random.Range(200f, 500f);
            if (c.anim != null) c.anim.SetBool(ProtoAnimIds.IsDead, true);
        }

        void TickClingers(float dt)
        {
            for (int i = _clingers.Count - 1; i >= 0; i--)
            {
                var c = _clingers[i];
                if (c.tf == null) { _clingers.RemoveAt(i); continue; }
                if (!c.flying)
                {
                    // 버둥거리며 할퀸다 — 붙은 자리에서 조금씩 흔들린다
                    float s = c.seed + _t;
                    c.tf.localPosition = c.home + new Vector3(Mathf.Sin(s * 7f), Mathf.Sin(s * 5.3f), Mathf.Sin(s * 6.1f)) * 0.06f;
                    c.nextClaw -= dt;
                    if (c.nextClaw <= 0f && c.anim != null)
                    {
                        c.anim.SetTrigger(Random.value < 0.6f ? "Attack" : "Bite");
                        c.nextClaw = Random.Range(0.7f, 1.6f);
                    }
                    continue;
                }
                if (c.landedAt < 0f)
                {
                    c.vel.y -= Gravity * dt;
                    c.tf.position += c.vel * dt;
                    c.tf.rotation = Quaternion.AngleAxis(c.spin.magnitude * dt, c.spin.normalized) * c.tf.rotation;
                    if (c.tf.position.y <= 0f && c.vel.y < 0f)
                    {
                        // 땅에 떨어지면 그 자리에 쓰러진다 — 사망 애니가 눕혀 준다. 경험치도 떨군다
                        Vector3 p = c.tf.position; p.y = 0f;
                        c.tf.position = p;
                        c.tf.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        c.landedAt = 0f;
                        var run = ProtoRun.I;
                        if (run != null)
                        {
                            run.OnKill();
                            if (run.gems != null) run.gems.Drop(p, EnemyTable.I != null && EnemyTable.I.walker != null ? EnemyTable.I.walker.xp : 1);
                        }
                        ProtoAudio.Play("zdie", p, 0.8f);
                    }
                    continue;
                }
                c.landedAt += dt;
                if (c.landedAt > 1.8f) c.tf.position += Vector3.down * 1.2f * dt;   // 가라앉아 사라진다
                if (c.landedAt > 3f) { Destroy(c.tf.gameObject); _clingers.RemoveAt(i); }
            }
        }

        bool AllClingersGone() => _clingers.Count == 0;

        // ── 소리: 융단 폭격처럼 도시 배경음의 헬기 통과음을 다시 쓴다 ───────
        // 클립 길이의 65% 지점이 머리 위 — 엔진 피격 순간에 맞춘다.
        void StartVoice()
        {
            var clip = Resources.Load<AudioClip>("Ambience/heli_" + Random.Range(1, 5));
            if (clip == null) clip = Resources.Load<AudioClip>("Ambience/heli_1");
            if (clip == null) return;
            var go = new GameObject("HeliVoice");
            go.transform.SetParent(transform, false);
            _voice = go.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0f;
            _voice.clip = clip;
            _voice.volume = ProtoSettings.SfxVolume * 0.9f;
            _voice.time = Mathf.Clamp(clip.length * 0.65f - StaggerAt, 0f, clip.length - 0.05f);
            _voice.Play();
            _voiceFade = -1f;
        }

        void FadeVoice() { if (_voice != null) _voiceFade = 0f; }

        void TickVoice()
        {
            if (_voice == null) return;
            bool paused = Time.timeScale <= 0.0001f;
            if (paused) { if (_voice.isPlaying) _voice.Pause(); return; }
            if (!_voice.isPlaying) _voice.UnPause();
            if (_voiceFade < 0f) return;
            _voiceFade += Time.deltaTime / 0.35f;   // 충돌 폭발음에 묻히며 끊긴다
            _voice.volume = ProtoSettings.SfxVolume * 0.9f * Mathf.Clamp01(1f - _voiceFade);
            if (_voiceFade >= 1f) { Destroy(_voice.gameObject); _voice = null; }
        }

        // ── 정리 ─────────────────────────────────────────────────────────
        void Clear()
        {
            StopAllCoroutines();
            _t = -1f;
            if (_tele != null) { _tele.Cancel(); _tele = null; }
            for (int i = 0; i < _clingers.Count; i++) if (_clingers[i].tf != null) Destroy(_clingers[i].tf.gameObject);
            _clingers.Clear();
            if (_heli != null) Destroy(_heli.gameObject);
            if (_fire != null) Destroy(_fire.gameObject);
            if (_smoke != null) Destroy(_smoke.gameObject);
            if (_voice != null) Destroy(_voice.gameObject);
            _heli = _rotor = _tail = null;
            _fire = _smoke = null;
            _voice = null;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
