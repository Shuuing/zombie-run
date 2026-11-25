using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 예산 기반 스폰 디렉터 (GDD §09).
    /// 웨이브 테이블이 아니라 "동시 존재 예산"을 채우는 방식이라 빌드가 강해져도 화면이 비지 않는다.
    /// 러버밴딩은 물량에만 적용한다 — 적 HP에 곱하면 강해진 감각이 사라진다.
    public class ProtoSpawnDirector : MonoBehaviour
    {
        public static ProtoSpawnDirector I { get; private set; }

        [Header("참조")]
        public GameObject zombiePrefab;

        // 임시(테스트) — 빠른 좀비를 겉모습으로 구분하려고 스킨만 갈아끼운다. 프리팹을 따로
        // 만들지 않는 이유: 애니 컨트롤러·플래시·그림자 배선이 전부 같아서 프리팹을 복제하면
        // 그 배선이 두 벌이 되고, 한쪽을 고칠 때 다른 쪽이 반드시 빠진다.
        [Tooltip("빠른 좀비(Runner) 전용 스킨. 비우면 기본 좀비와 같은 모습으로 나온다.")]
        public Material zombie2Material;
        public GameObject bossPrefab;
        [Tooltip("최종 보스 프리팹. 비우면 bossPrefab 을 1.3배로 키워 쓴다(전용 모델이 붙기 전까지).")]
        public GameObject finalBossPrefab;
        [Tooltip("원거리 좀비 프리팹. 비우면 Resources/Enemies/Zombie_Ranged 를 쓴다(씬마다 채우지 않아도 되게).")]
        public GameObject rangedPrefab;
        [Tooltip("좀비 개 프리팹. 비우면 Resources/Enemies/Zombie_Dog 를 쓴다.")]
        public GameObject dogPrefab;
        public Transform  player;
        public Camera     viewCamera;

        [Header("보스")]
        [Tooltip("끄면 보스가 등장하지 않는다")]
        public bool enableBoss = true;
        [Tooltip("디버그: B 키로 다음 차례 보스(중간 → 최종)를 즉시 소환")]
        public bool debugBossHotkey = true;

        /// 살아 있는 보스. 없으면 null. HUD가 체력 바를 그릴 때 쓴다.
        public ProtoZombie Boss { get; private set; }
        /// 살아 있는 엘리트(마지막으로 나온 것). 보스가 없을 때 HUD가 같은 체력 바로 보여준다.
        public ProtoZombie Elite { get; private set; }

        [Header("상태 (읽기 전용)")]
        public int aliveCount;
        public int budget;
        public float pressure = 1f;
        public int separationPairChecks;

        [Header("좀비 상호 밀어내기")]
        [Tooltip("끄면 좀비가 한 점에 뭉친다")]
        public bool separateZombies = true;

        ProtoGrid _grid;

        readonly List<ProtoZombie> _active = new List<ProtoZombie>();
        readonly Queue<ProtoZombie> _pool  = new Queue<ProtoZombie>();
        // 원거리 좀비는 프리팹(모델·컨트롤러)이 달라 잡좀비 풀과 섞으면 안 된다 — 보스와 같은 이유.
        readonly Queue<ProtoZombie> _rangedPool = new Queue<ProtoZombie>();
        int _rangedAlive;
        readonly Queue<ProtoZombie> _dogPool = new Queue<ProtoZombie>();
        int _dogAlive;
        Transform _poolRoot;

        float _nextSpawn;
        readonly Queue<float> _recentKills = new Queue<float>();

        // 보스는 프리팹이 달라 잡좀비 풀에 섞으면 안 된다. 인스턴스를 따로 들고 재사용한다.
        // 중간·최종은 덩치(스케일)가 달라 인스턴스를 나눈다 — 하나를 돌려 쓰면 스케일이 샌다.
        ProtoZombie _midInstance, _finalInstance;
        bool _midSpawned, _finalSpawned;
        float _midDeadAt;

        /// 지금 살아 있는 보스가 최종 보스인가. HUD 표기·스폰 중지·클리어 판정에 쓴다.
        public bool FinalBossActive => Boss != null && Boss == _finalInstance && Boss.Alive;
        public bool IsFinalBoss(ProtoZombie z) => z != null && z == _finalInstance;

        /// 최종 보스가 나올 때까지 남은 초. 중간 보스가 아직 안 나왔거나 살아 있으면 정해지지 않아 -1.
        /// 엘리트 체력 바의 "N초 뒤 도망" 표시용 — Update 의 최종 보스 소환 조건과 같은 식이다.
        public float FinalBossEta
        {
            get
            {
                if (_finalSpawned || !enableBoss) return -1f;
                if (!_midSpawned || Boss != null) return -1f;
                var t = EnemyTable.I;
                if (t == null) return -1f;
                float at = Mathf.Max(t.finalBossAt, _midDeadAt + t.finalBossAfterMid);
                return Mathf.Max(0f, at - (ProtoRun.I != null ? ProtoRun.I.Elapsed : 0f));
            }
        }

        public IReadOnlyList<ProtoZombie> Active => _active;

        void Awake()
        {
            I = this;
            if (rangedPrefab == null) rangedPrefab = Resources.Load<GameObject>("Enemies/Zombie_Ranged");
            // 좀비 개 — Tools/좀비 오버런/좀비 개 셋업 이 만든다
            if (dogPrefab == null) dogPrefab = Resources.Load<GameObject>("Enemies/Zombie_Dog");
            // 최종 보스(boss_02 망치) — Tools/좀비 오버런/최종 보스(boss_02) 셋업 이 만든다. 없으면 bossPrefab 을 키워 쓴다.
            if (finalBossPrefab == null) finalBossPrefab = Resources.Load<GameObject>("Enemies/Boss_Final");
            _poolRoot = new GameObject("_ZombiePool").transform;
            _poolRoot.SetParent(transform, false);
            _grid = new ProtoGrid(ProtoConfig.GridWorldHalf, ProtoConfig.GridCellSize);
        }

        // 이동(Update)이 모두 끝난 뒤 겹침을 푼다. Update 중에 풀면 같은 프레임에 다시 겹친다.
        void LateUpdate()
        {
            if (!separateZombies || _grid == null) return;
            if (ProtoRun.I == null || !ProtoRun.I.IsRunning) return;

            _grid.Separate(_active, ProtoConfig.SeparationDist,
                           ProtoConfig.SeparationStrength, ProtoConfig.Delta,
                           player != null ? player.position : Vector3.zero);   // 격자를 플레이어 둘레로(2026-11-20)
            separationPairChecks = _grid.LastPairChecks;
        }

        void OnDestroy() { if (I == this) I = null; }

        void Update()
        {
            // 발밑 빨간 원(11-17) — 엘리트 · 중간 보스만(사용자 "마지막 보스에는 바닥 표시 빼주고"). 멈춤 · 결과 화면 중에도 살아 있으면 계속
            var markBoss = Boss != null && Boss.Alive && !FinalBossActive ? Boss : null;
            ProtoBossMarker.Track(ProtoBossMarker.Boss, markBoss != null ? markBoss.transform : null, markBoss != null ? markBoss.bodyRadius : 0f);
            var markElite = Elite != null && Elite.Alive ? Elite : null;
            ProtoBossMarker.Track(ProtoBossMarker.Elite, markElite != null ? markElite.transform : null, markElite != null ? markElite.bodyRadius : 0f);
            if (Boss != null) ProtoTankIndicator.Ensure();   // 화면 밖 보스 방향 빨간 화살표(11-17) — 탱크 화살표와 같은 관리자
            if (ProtoRun.I == null || !ProtoRun.I.IsRunning) return;

            float elapsed = ProtoRun.I.Elapsed;
            UpdatePressure();

            // 적 종류·일정은 전부 적 테이블(Resources/Data/EnemyTable)에서 읽는다. 없으면 아무것도 내보내지 않는다(에러는 테이블이 찍는다).
            var table = EnemyTable.I;
            if (table == null) return;

            budget = Mathf.Clamp(
                Mathf.RoundToInt(ProtoConfig.BudgetBase + ProtoConfig.BudgetPerMin * (elapsed / 60f)),
                ProtoConfig.BudgetBase, ProtoConfig.BudgetCap);

            // ── 보스 2회: 중간 보스 → 최종 보스 ─────────────────────────
            if (Boss != null && !Boss.Alive) Boss = null;   // 죽으면 바 숨김 (사체는 아직 남아 있다)
            // 임시(테스트) — 시작하자마자 최종 보스. HP 는 정식 등장 시각 기준(ProtoConfig.DebugFinalBossFirst 주석)
            if (enableBoss && ProtoConfig.DebugFinalBossFirst && !_finalSpawned && elapsed >= ProtoConfig.DebugFinalBossAt)
            {
                _midSpawned = true;
                SpawnBoss(table.finalBossAt, true);
            }
            if (enableBoss)
            {
                if (!_midSpawned && elapsed >= table.midBossAt) SpawnBoss(elapsed, false);
                else if (_midSpawned && !_finalSpawned && Boss == null
                         && elapsed >= table.finalBossAt
                         && elapsed >= _midDeadAt + table.finalBossAfterMid)
                    TrySpawnFinal(elapsed);
            }
            // 디버그 B: 다음 차례 보스를 즉시 (중간 → 최종)
            if (debugBossHotkey && Input.GetKeyDown(KeyCode.B) && Boss == null)
            {
                if (_midSpawned) TrySpawnFinal(elapsed); else SpawnBoss(elapsed, false);
            }

            // 최종 보스가 나온 뒤로는 울타리 안 1:1 — 잡좀비를 새로 내보내지 않는다(잡으면 곧 클리어)
            if (_finalSpawned) return;

            // ── 몬스터 대량 습격 (돌격 1:30 · 포위 3:15) ─────────────────
            TickHorde(elapsed);
            // ── 엘리트 (1:05 · 3:25) — 잡으면 진화 상자 ─────────────────
            TickElite(elapsed);
            if (debugBossHotkey && Input.GetKeyDown(KeyCode.K)) SpawnElite(elapsed);   // 디버그: 엘리트 즉시
            if (debugBossHotkey && Input.GetKeyDown(KeyCode.H)) HordeRush(elapsed);   // 디버그: 돌격 즉시
            if (debugBossHotkey && Input.GetKeyDown(KeyCode.J)) HordeRing(elapsed);   // 디버그: 포위 즉시
            // 디버그: N 키로 원거리 좀비 1마리 (40초를 기다리지 않고 확인용)
            if (debugBossHotkey && Input.GetKeyDown(KeyCode.N)) SpawnOne(elapsed, ZombieKind.Ranged);
            // 디버그: M 키로 좀비 개 3마리
            if (debugBossHotkey && Input.GetKeyDown(KeyCode.M)) for (int i = 0; i < 3; i++) SpawnOne(elapsed, ZombieKind.Dog);

            if (Time.time < _nextSpawn) return;
            _nextSpawn = Time.time + ProtoConfig.SpawnInterval / pressure;

            // 예산의 85~100%를 유지
            int want = Mathf.RoundToInt(budget * 0.95f) - aliveCount;
            int perTick = Mathf.Clamp(want, 0, 6);
            for (int i = 0; i < perTick; i++) SpawnOne(elapsed);
        }

        /// 최근 10초 처치율로 스폰 속도만 보정 (GDD §09)
        void UpdatePressure()
        {
            float now = Time.time;
            while (_recentKills.Count > 0 && now - _recentKills.Peek() > 10f) _recentKills.Dequeue();
            float killRate = _recentKills.Count / 10f;
            float expected = Mathf.Max(0.01f, budget * 0.22f);
            pressure = Mathf.Clamp(killRate / expected, 0.75f, 1.35f);
        }

        void SpawnOne(float elapsed) => SpawnOne(elapsed, null);

        /// 종류별 스폰 조건(테이블 줄의 unlockAt · chance · maxAlive). 칸이 비어 있으면 안 나온다.
        static bool Rolls(EnemyDef d, float elapsed, int alive)
        {
            if (d == null) return false;
            if (elapsed < d.unlockAt) return false;
            if (d.maxAlive > 0 && alive >= d.maxAlive) return false;
            return Random.value < d.chance;
        }

        /// 테이블 줄의 스킨. 비어 있으면 러너만 예전 방식(디렉터 인스펙터의 zombie2Material)으로 — 씬 배선이 아직 그쪽에 있다.
        Material SkinFor(EnemyDef d) => d.skin != null ? d.skin : (d.behaviour == ZombieKind.Runner ? zombie2Material : null);

        public void SpawnOne(float elapsed, ZombieKind? force)
        {
            if (player == null || zombiePrefab == null) return;
            var t = EnemyTable.I;
            if (t == null) return;

            EnemyDef d = Rolls(t.runner, elapsed, 0) ? t.runner : t.walker;
            if (rangedPrefab != null && Rolls(t.ranged, elapsed, _rangedAlive)) d = t.ranged;
            else if (dogPrefab != null && Rolls(t.dog, elapsed, _dogAlive)) d = t.dog;
            if (force.HasValue) d = t.Of(force.Value);
            if (d == null) d = t.walker;
            if (d == null) return;
            if (d.behaviour == ZombieKind.Ranged && rangedPrefab == null) d = t.walker;
            if (d.behaviour == ZombieKind.Dog && dogPrefab == null) d = t.walker;
            if (d == null) return;
            var kind = d.behaviour;

            Vector3 pos = PickSpawnPoint();
            float hpScale = ProtoConfig.EnemyHPScale(elapsed);

            ProtoZombie z = kind == ZombieKind.Ranged ? (_rangedPool.Count > 0 ? _rangedPool.Dequeue() : CreateNew(rangedPrefab))
                          : kind == ZombieKind.Dog    ? (_dogPool.Count > 0 ? _dogPool.Dequeue() : CreateNew(dogPrefab))
                          : (_pool.Count > 0 ? _pool.Dequeue() : CreateNew(zombiePrefab));
            if (z == null) return;
            z.transform.SetParent(null, true);
            z.Spawn(pos, d, hpScale, player);
            // 스킨은 Spawn 뒤에 입힌다 — Spawn 이 ResetVisual 로 MPB 를 초기화하므로
            // 먼저 입히면 그 초기화에 덮인다.
            z.SetSkin(SkinFor(d));
            _active.Add(z);
            if (kind == ZombieKind.Ranged) _rangedAlive++;
            if (kind == ZombieKind.Dog) _dogAlive++;
            aliveCount = _active.Count;
        }

        ProtoZombie CreateNew(GameObject prefab)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab);
            var z = go.GetComponent<ProtoZombie>();
            if (z == null) z = go.AddComponent<ProtoZombie>();
            return z;
        }

        /// 최종 보스 직전 — 바닥의 젬을 FinalGatherTime 초 동안 빠르게 모아 한 번에 먹고(ProtoGemPool.StartGather),
        /// 레벨업 카드를 다 고른 뒤에 소환한다. 매 프레임 같은 조건으로 다시 들어오므로 단계는 저절로 이어진다:
        /// 모으는 중 → 기다림 / 경험치가 들어가 카드가 뜸 → Update 가 멈춤 / 젬 없음 + Running → 소환.
        /// (보스전 도중에 카드가 끼어들면 집중이 깨진다 — 2026-10-21 사용자)
        void TrySpawnFinal(float elapsed)
        {
            var gp = ProtoGemPool.I;
            if (gp != null)
            {
                if (gp.Gathering) return;
                // 모으기는 한 번만 — 그사이에도 잡좀비를 쏴서 젬이 계속 떨어지므로, 남을 때마다 다시 모으면 보스가 영영 안 나온다.
                // 그 뒤에 떨어진 몇 개는 SpawnBoss 의 MagnetAll 이 끌어온다.
                if (!_finalGathered && gp.LiveGems > 0) { _finalGathered = true; gp.StartGather(EnemyTable.I != null ? EnemyTable.I.finalGatherTime : 1f); return; }
            }
            SpawnBoss(elapsed, true);
        }
        bool _finalGathered;

        /// 보스 소환. final=false 면 중간 보스, true 면 최종 보스(울타리 + 잡좀비 정리).
        /// 이미 보스가 살아 있으면 아무것도 하지 않는다.
        public void SpawnBoss(float elapsed, bool final)
        {
            if (player == null) return;
            var table = EnemyTable.I;
            var bossDef = table != null ? (final ? table.finalBoss : table.midBoss) : null;
            if (bossDef == null)
            {
                Debug.LogWarning("[Spawn] 적 테이블의 " + (final ? "finalBoss" : "midBoss") + " 칸이 비어 있다 — 보스를 소환할 수 없다");
                _midSpawned = _finalSpawned = true;
                return;
            }
            var prefab = final && finalBossPrefab != null ? finalBossPrefab : bossPrefab;
            if (prefab == null)
            {
                Debug.LogWarning("[Spawn] bossPrefab 이 비어 있다 — 보스를 소환할 수 없다");
                _midSpawned = _finalSpawned = true;   // 매 프레임 경고가 쏟아지지 않게
                return;
            }
            if (Boss != null && Boss.Alive) return;

            ref ProtoZombie inst = ref (final ? ref _finalInstance : ref _midInstance);
            if (inst == null)
            {
                var go = Instantiate(prefab);
                go.name = final ? "_FinalBoss" : "_MidBoss";
                inst = go.GetComponent<ProtoZombie>();
                if (inst == null) inst = go.AddComponent<ProtoZombie>();
                // 덩치(최종 보스 1.3배)는 테이블 줄의 scale 로 Spawn 이 입힌다
            }
            var z = inst;
            if (_active.Contains(z)) return;   // 사체가 아직 정리되지 않은 상태

            Vector3 pos;
            if (final)
            {
                // 탕탕특공대식 보스전: 잡좀비·구슬을 치우고, 떨어진 젬은 끌어오고, 울타리를 세운다.
                // 최종 보스전에는 탱크를 못 쓴다 — 울타리 크기(화면 기준)와 맞지 않는다. 내려서 싸우고,
                // 보스가 죽을 때까지 탑승도 잠긴다(ProtoTank.TickIdle). 중간 보스는 탈 수 있다(2026-10-21 사용자).
                ProtoTank.RequestExit();
                // 못 잡은 엘리트는 상자 없이 도망간다 — 왜 상자가 없는지 문구로 알린다
                if (Elite != null && Elite.Alive) { ProtoHUD.ShowWarning("엘리트가 도망쳤습니다!", 2.5f); Elite = null; }
                ClearMinions();
                ProtoEnemyBolt.ClearAll();
                ProtoPoisonGas.ClearAll();
                ProtoGemPool.I?.MagnetAll();
                ProtoBossArena.Begin(player.position);
                pos = ProtoBossArena.BossSpawnPoint();
            }
            else pos = PickSpawnPoint();

            z.transform.SetParent(null, true);
            z.Spawn(pos, bossDef, ProtoConfig.EnemyHPScale(elapsed), player);
            if (final) ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Heavy);   // 최종 보스 등장
            // 등장 표시(카탈로그 boss_spawn 프리팹이 있을 때만 나온다 — 코드 그림 없음). scale = 보스 덩치
            ProtoFx.Play(FxIds.BossSpawn, pos, Vector3.zero, Mathf.Max(1f, bossDef.scale));
            _active.Add(z);
            aliveCount = _active.Count;

            Boss = z;
            if (final) _finalSpawned = true; else _midSpawned = true;
            // 원작 경고 띠는 원래 "BOSS 출현"용 — 문구에 "보스"가 있으면 띠 위에 왕관 해골이 뜬다(ProtoHUD.RouteSaMessages)
            ProtoHUD.ShowWarning(final ? "최종 보스 출현!" : "중간 보스 출현!", 3f, redEdge: true);
            Debug.Log($"[Spawn] {(final ? "최종" : "중간")} 보스 등장 — HP {z.maxHP:F0} (경과 {elapsed:F0}s)");
        }

        // ── 몬스터 대량 습격 (탕탕특공대식, 2026-10-13) ───────────────────
        bool _rushWarned, _rushDone, _ringWarned, _ringDone;

        void TickHorde(float elapsed)
        {
            var t = EnemyTable.I;
            if (t == null) return;
            float lead = t.hordeWarnLead;
            if (!_rushWarned && elapsed >= t.rushAt - lead) { _rushWarned = true; ProtoHUD.ShowWarning("몬스터 떼가 몰려옵니다!", lead + 1.5f); }
            if (!_rushDone && elapsed >= t.rushAt) HordeRush(elapsed);
            if (!_ringWarned && elapsed >= t.ringAt - lead) { _ringWarned = true; ProtoHUD.ShowWarning("몬스터가 포위해 옵니다!", lead + 1.5f); }
            if (!_ringDone && elapsed >= t.ringAt) HordeRing(elapsed);
        }

        /// ① 한 방향 돌격: 화면 밖 한쪽에 가로 7줄 대열(HordeRushCount 만큼 뒤로 늘어남)을 세우고 빠르게 달려오게 한다.
        /// 방향은 플레이어가 향한 쪽 ±90° 안 — 등 뒤에서 오면 보이기도 전에 붙는다.
        public void HordeRush(float elapsed)
        {
            _rushDone = _rushWarned = true;
            if (player == null) return;
            var pp = player.GetComponent<ProtoPlayer>();
            Vector3 fwd = pp != null ? pp.FacingDir : Vector3.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            Vector3 from = Quaternion.Euler(0f, Random.Range(-90f, 90f), 0f) * fwd.normalized;   // 몰려오는 쪽
            Vector3 side = Vector3.Cross(Vector3.up, from);
            var t = EnemyTable.I;
            if (t == null || t.runner == null) return;
            Vector3 front = player.position + from * OffscreenDistance(from, ViewRadius() + ProtoConfig.SpawnRingPad);
            const int cols = 7; const float gap = 1.1f;
            for (int i = 0; i < t.rushCount; i++)
            {
                int c = i % cols, r = i / cols;
                Vector3 p = front + side * ((c - (cols - 1) * 0.5f) * gap) + from * (r * gap)
                          + new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));
                SpawnAt(p, t.runner, elapsed, t.rushSpeed);
            }
            Debug.Log($"[Spawn] 몬스터 떼 돌격 {t.rushCount}마리 (경과 {elapsed:F0}s)");
        }

        /// ② 포위: 플레이어 둘레 원 위에 고르게 세운다. 그냥 쫓아오므로 원이 조여 드는 모양이 된다.
        public void HordeRing(float elapsed)
        {
            _ringDone = _ringWarned = true;
            if (player == null) return;
            var t = EnemyTable.I;
            if (t == null || t.walker == null) return;
            int n = t.ringCount;
            float off = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                float a = (off + 360f * i / n) * Mathf.Deg2Rad;
                Vector3 p = player.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * t.ringRadius;
                SpawnAt(p, t.walker, elapsed, -1f);
            }
            ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Medium);   // 포위 등장
            Debug.Log($"[Spawn] 몬스터 포위 {n}마리 (경과 {elapsed:F0}s)");
        }

        // ── 엘리트 좀비 (2026-10-21 — 무기 진화 상자) ─────────────────────
        int _eliteNext;          // 다음에 나올 EliteSpawnTimes 칸
        bool _eliteWarned;

        void TickElite(float elapsed)
        {
            if (Elite != null && !Elite.Alive) Elite = null;
            var t = EnemyTable.I;
            if (t == null || t.eliteSpawnTimes == null) return;
            var times = t.eliteSpawnTimes;
            if (_eliteNext >= times.Length) return;
            float at = times[_eliteNext];
            if (!_eliteWarned && elapsed >= at - t.eliteWarnLead)
            {
                _eliteWarned = true;
                ProtoHUD.ShowWarning("엘리트 좀비 출현! 잡으면 진화 상자", t.eliteWarnLead + 1.5f, redEdge: true);
            }
            if (elapsed >= at)
            {
                _eliteNext++; _eliteWarned = false;
                SpawnElite(elapsed);
            }
        }

        /// 엘리트 한 마리 — 플레이어가 향한 쪽 화면 밖에서 걸어 들어온다(뒤에서 나오면 보이기 전에 붙는다).
        public void SpawnElite(float elapsed)
        {
            if (player == null || zombiePrefab == null) return;
            var pp = player.GetComponent<ProtoPlayer>();
            Vector3 fwd = pp != null ? pp.FacingDir : Vector3.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * fwd.normalized;
            var t = EnemyTable.I;
            if (t == null || t.walker == null || t.elite == null) return;
            var z = SpawnAt(player.position + dir * OffscreenDistance(dir, ViewRadius() + ProtoConfig.SpawnRingPad), t.walker, elapsed, -1f);
            if (z == null) return;
            z.MakeElite(t.elite, ProtoConfig.EnemyHPScale(elapsed));
            Elite = z;
            ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Medium);
            Debug.Log($"[Spawn] 엘리트 등장 — HP {z.maxHP:F0} (경과 {elapsed:F0}s)");
        }

        /// 지정한 자리에 잡좀비 하나(잡좀비 프리팹 풀 — 워커·러너·엘리트). speed &lt;= 0 이면 종류 기본 속도.
        ProtoZombie SpawnAt(Vector3 pos, EnemyDef d, float elapsed, float speed)
        {
            if (player == null || zombiePrefab == null || d == null) return null;
            float lim = ProtoConfig.ArenaHalf;
            pos.x = Mathf.Clamp(pos.x, -lim, lim); pos.z = Mathf.Clamp(pos.z, -lim, lim); pos.y = 0f;
            var z = _pool.Count > 0 ? _pool.Dequeue() : CreateNew(zombiePrefab);
            if (z == null) return null;
            z.transform.SetParent(null, true);
            z.Spawn(pos, d, ProtoConfig.EnemyHPScale(elapsed), player);
            z.SetSkin(SkinFor(d));
            if (speed > 0f) z.moveSpeed = speed;
            z.gasBomber = false;   // 떼 돌격은 그냥 때린다 — 수십 마리가 한꺼번에 자폭하면 피할 길이 없다
            _active.Add(z);
            aliveCount = _active.Count;
            return z;
        }

        /// 보스 말고 살아 있는 잡좀비를 모두 치운다(경험치 없음). 최종 보스전 시작용.
        void ClearMinions()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                if (!_active[i].IsBoss) Release(_active[i]);
        }

        /// 가시 영역 외곽 링. 이동 방향 앞쪽에 70%, 뒤쪽에 30% (GDD §09)
        Vector3 PickSpawnPoint()
        {
            float ringR = ViewRadius() + ProtoConfig.SpawnRingPad;
            // 스폰 링은 항상 기준 방향이 필요하므로 FacingDir(0 이 되지 않는 쪽)을 쓴다.
            var pp = player.GetComponent<ProtoPlayer>();
            Vector3 fwd = pp != null ? pp.FacingDir : Vector3.forward;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float baseAng = Mathf.Atan2(fwd.z, fwd.x);
                bool front = Random.value < 0.7f;
                float spread = front ? Random.Range(-1.2f, 1.2f) : Random.Range(1.9f, 4.4f);
                float ang = baseAng + spread;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 p = player.position + dir * OffscreenDistance(dir, ringR);
                p.y = 0f;
                float lim = ProtoConfig.ArenaHalf;
                if (Mathf.Abs(p.x) <= lim && Mathf.Abs(p.z) <= lim) return p;
            }
            // 아레나 안쪽으로 강제 클램프
            Vector3 fb = player.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * ringR;
            fb.x = Mathf.Clamp(fb.x, -ProtoConfig.ArenaHalf, ProtoConfig.ArenaHalf);
            fb.z = Mathf.Clamp(fb.z, -ProtoConfig.ArenaHalf, ProtoConfig.ArenaHalf);
            fb.y = 0f;
            return fb;
        }

        float ViewRadius()
        {
            if (viewCamera == null) return 12f;
            // 카메라가 보는 지면 범위의 대략적인 반경
            float d = Vector3.Distance(viewCamera.transform.position, player.position);
            float vExtent = 2f * d * Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float hExtent = vExtent * viewCamera.aspect;
            return Mathf.Max(vExtent, hExtent) * 0.5f + 1f;
        }

        /// 플레이어에서 <paramref name="dir"/> 쪽으로 <paramref name="r"/> 만큼 떨어진 자리가 아직 화면에 비치면,
        /// 화면 밖으로 나갈 때까지 2m 씩 더 민 거리를 돌려준다. 이미 화면 밖인 방향은 그대로 r 이다.
        ///
        /// <see cref="ViewRadius"/> 는 화면 가운데 깊이에서 잰 반폭이라 원 하나로 화면을 다 덮지 못한다 — 원근 카메라는
        /// 화면 위쪽일수록 바닥이 멀고 넓게 담긴다. 각도 1(58°·화각 42°)에서는 위쪽 모서리만 살짝 걸렸는데(원 16m · 모서리 19m),
        /// 2026-11-10 에 기본이 된 각도 3(Yet Another Zombie Survivors 식 — 55°·화각 60°)에서는 원 19m 에 모서리가 26m 라
        /// 화면 위쪽 좌우에서 좀비가 눈앞에 생겨난다. 실제 카메라로 화면 안인지 물어보고 밀어낸다.
        float OffscreenDistance(Vector3 dir, float r)
        {
            if (viewCamera == null || player == null) return r;
            const float Margin = 0.06f;      // 화면 끝에서 이만큼 더 나가야 몸이 다 가려진다 (뷰포트 비율)
            for (int i = 0; i < 14; i++)     // 최대 +28m — 각도 3 의 위쪽 모서리(55° 에서 26m, 50° 로 낮춰도 31m)를 넘긴다
            {
                Vector3 v = viewCamera.WorldToViewportPoint(player.position + dir * r + Vector3.up);
                if (v.z <= 0f || v.x < -Margin || v.x > 1f + Margin || v.y < -Margin || v.y > 1f + Margin) break;
                r += 2f;
            }
            return r;
        }

        public ProtoZombie FindNearest(Vector3 from, float maxRange)
        {
            ProtoZombie best = null;
            float bestSq = maxRange * maxRange;
            for (int i = 0; i < _active.Count; i++)
            {
                var z = _active[i];
                if (z == null || !z.Alive) continue;
                float sq = (z.transform.position - from).sqrMagnitude;
                if (sq <= bestSq) { bestSq = sq; best = z; }
            }
            return best;
        }

        public void OnZombieKilled(ProtoZombie z)
        {
            _recentKills.Enqueue(Time.time);
            ProtoRun.I?.OnKill(z);
            ProtoGemPool.I?.Drop(z.transform.position, z.xpValue);

            // 진화 상자 — 엘리트·중간 보스 (최종 보스는 판이 끝나므로 없음)
            if (z.IsElite) ProtoPickups.Drop(z.transform.position, ProtoPickups.Kind.Chest);

            if (z.IsBoss)
            {
                if (IsFinalBoss(z))
                {
                    ProtoBossArena.End();               // 벽이 가라앉는다
                    ProtoRun.I?.OnFinalBossKilled();    // 최종 보스 처치 = 클리어
                }
                else
                {
                    _midDeadAt = ProtoRun.I != null ? ProtoRun.I.Elapsed : 0f;
                    ProtoPickups.Drop(z.transform.position, ProtoPickups.Kind.Chest);
                }
            }
        }

        public void Release(ProtoZombie z)
        {
            _active.Remove(z);
            aliveCount = _active.Count;
            z.ForceDespawn();
            z.transform.SetParent(_poolRoot, true);

            // 보스는 프리팹이 다르다. 잡좀비 풀에 넣으면 다음 워커 스폰이 이 인스턴스를
            // 꺼내 보스 외형의 워커가 생긴다. 인스턴스만 남겨두고 큐에는 넣지 않는다.
            if (z.IsBoss)
            {
                if (Boss == z) Boss = null;
                return;
            }
            if (z.IsRanged) { _rangedAlive = Mathf.Max(0, _rangedAlive - 1); _rangedPool.Enqueue(z); return; }
            if (z.IsDog)    { _dogAlive = Mathf.Max(0, _dogAlive - 1); _dogPool.Enqueue(z); return; }
            _pool.Enqueue(z);
        }

        public void ClearAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--) Release(_active[i]);
            _recentKills.Clear();
            Boss = null;
            Elite = null;
            _eliteNext = 0; _eliteWarned = false;
            _finalGathered = false;
            _midSpawned = _finalSpawned = false;
            _rushWarned = _rushDone = _ringWarned = _ringDone = false;
            _midDeadAt = 0f;
            _rangedAlive = 0;
            _dogAlive = 0;
            ProtoBossArena.Clear();
            ProtoPoisonGas.ClearAll();
        }
    }
}
