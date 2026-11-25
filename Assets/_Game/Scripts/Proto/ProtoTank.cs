using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 버려진 탱크 — <b>프로토 한정 테스트 기능</b> (기획서에 없음, 사용자 요청 2026-10-08).
    ///
    /// <para>필드에 한 대 놓인다. 플레이어가 <see cref="BoardRange"/> 안에 들어오면 자동 탑승하고,
    /// 하차 버튼(또는 E)으로 내린다. 내린 탱크는 그때의 HP 를 그대로 들고 남는다.</para>
    ///
    /// <para><b>플레이어 오브젝트는 그대로 둔다.</b> 좀비·젬·카메라·보조 장비가 전부 Player 트랜스폼을
    /// 보고 있으므로, 탑승 중에는 Player 를 탱크 위치에 붙여 두고 모델만 숨긴다. 대신 세 곳에 갈림길이 있다:
    /// <see cref="ProtoPlayer"/>(이동 → <see cref="Drive"/>, 피해 → <see cref="TakeDamage"/>),
    /// <see cref="ProtoShooter"/>(주무기 사격 중지), <see cref="ProtoZombie.TargetPad"/>(차체 가장자리에서 멈춤).</para>
    ///
    /// 정한 규칙 (사용자 확인):
    /// <list type="bullet">
    /// <item>전용 무기 — 포탑이 스스로 돌아 가장 가까운 적에게 포탄(착탄 범위 폭발). 공격력·치명타 버프는 받는다.</item>
    /// <item>조작 — 차체는 입력 방향으로 천천히 회전하고, 정면과 어긋난 만큼 느려진다(제자리 회전 가능).</item>
    /// <item>몸통 박치기 — 움직이는 중 닿은 일반 좀비는 즉사, 한 마리마다 탱크 HP 감소. 보스는 못 밀고 막힌다.</item>
    /// <item>파괴 — 주변 폭발(피해+넉백) 후 캐릭터가 튕겨 나오고 잠깐 무적. 부품이 흩어지고, 잔해는 잠시 뒤 가라앉아 사라진다.</item>
    /// <item>등장 — 필드에 항상 한 대만. 파괴되면 RespawnDelay 뒤 플레이어 주변 무작위 자리에 새로 나온다.</item>
    /// <item>빈 탱크 — 좀비는 공격하지 않는다. 내린 직후엔 범위를 한 번 벗어나야 다시 탄다.</item>
    /// </list>
    /// </summary>
    public class ProtoTank : MonoBehaviour
    {
        // ── 수치 (프로토 튜닝용) ─────────────────────────────────────────
        public const float MaxHP        = 300f;
        const float BoardRange          = 2.4f;    // 차체 중심에서. 바닥 원으로 보여 준다
        const float ReboardCooldown     = 1.5f;
        const float Speed               = 4.6f;    // 캐릭터 기본 4.2 보다 조금 빠르게 — 대신 회전이 느리다
        const float HullTurn            = 110f;    // 도/초
        const float TurretTurn          = 220f;
        const float Radius              = 1.55f;   // 차체(2.4×3.0m)를 원으로 근사
        const float HitIFrame           = 0.25f;   // 좀비 여럿이 붙었을 때 한 프레임에 녹지 않게
        const float RamCost             = 1.5f;    // 박치기 1마리당 탱크 HP
        const float RamMinSpeed         = 1.2f;    // 이보다 느리면 박치기가 아니라 '붙어 있는 것'
        const float ExplodeRadius       = 3.6f;
        // 파괴 폭발 프리팹(FX_Car_Explosion) 배율 — 승합차 2 기준. 예전 코드 그림 폭발 크기 비(탱크 3.6 : 승합차 5×0.85)를 따랐다.
        // 11-13 ×0.85 는 좀비 처치 폭발만 해서 "안 보인다" → ×2 배로(불덩이가 판정 반경의 절반쯤)
        const float BlastFxScale        = 1.7f;
        const float ExplodeDamage       = 80f;
        const float EjectInvuln         = 2.0f;    // 파괴로 튕겨 나올 때
        const float ExitInvuln          = 0.6f;    // 직접 내릴 때
        const float CamZoom             = 1.18f;   // 탑승 중 카메라를 이만큼 뺀다

        // 등장 · 재등장 — 필드에는 항상 한 대만. 파괴되면 잔해가 가라앉아 사라지고, 일정 시간 뒤 다른 자리에 새로 나온다
        const float RespawnDelay        = 20f;     // 파괴 후 (런 시간 기준 — 레벨업 정지 중엔 안 흐른다)
        const float WreckLinger         = 4f;      // 잔해가 남아 있는 시간
        const float WreckSink           = 1.5f;    // 땅속으로 가라앉는 시간
        const float SpawnMin = 8f, SpawnMax = 16f; // 플레이어로부터 거리. 화면 밖일 수 있다 — 찾아가는 맛
        const float SpawnPop            = 0.35f;   // 나타날 때 커지는 시간

        // 전용 포
        const float CannonDamage = 55f, CannonInterval = 1.1f, CannonRange = 12f, CannonAoe = 2.6f;
        // 착탄 폭발 그림 = 카탈로그 tank_cannon_hit(2026-11-17, 프리팹 Survivor Ace FX_Death_XiaoGuai_002). 이 배율은 프리팹 루트 위에 곱한다 —
        // 크기를 바꾸려면 이 값이나 FX 툴 카탈로그 '크기(프리팹 루트)'. 프리팹 칸을 비우면 예전 공용 착탄 폭발(링 = 판정 반경)
        const float CannonHitFxScale = 1f;
        // 가까이서 터지면 작게(2026-11-17 "거리에 따라 이펙트 사이즈를"): 포구(탱크 중심에서 2m 앞)에서 거의 안 날아가고 터지면 ×CannonHitNearScale,
        // CannonHitFullTravel m 이상 날아가 터지면 ×1, 그 사이는 부드럽게. 차체에 붙은 좀비를 쏠 때 폭발이 탱크 · 캐릭터를 통째로 덮지 않게 —
        // 판정 반경(CannonAoe)은 그대로다(그림만 줄인다)
        const float CannonHitNearScale = 0.5f, CannonHitFullTravel = 5f;
        const float AimTolerance = 8f;             // 포탑이 이 각도 안으로 돌아와야 쏜다

        public static ProtoTank Ridden { get; private set; }
        public static bool PlayerRiding => Ridden != null;
        /// 적 투사체(ProtoEnemyBolt)가 탑승 중 맞힘 판정에 쓰는 차체 반경.
        public static float HitRadius => Radius;
        /// 지금 필드에 있는 탱크(잔해 포함, 없으면 null). 화면 가장자리 화살표(ProtoTankIndicator)가 본다.
        public static ProtoTank Current => _field;
        static ProtoTank _field;          // 지금 필드의 탱크(잔해 포함). 이게 null 이어야 새로 나온다
        static ProtoPlayer _owner;
        static float _respawnAt = -1f;    // ProtoRun.elapsed 기준. -1 = 예약 없음
        static bool _hardReset;           // 재시작 중 — 바퀴 자국도 즉시 지운다

        public float hp = MaxHP;
        public bool  Wrecked { get; private set; }

        Transform _turret, _muzzle;
        ProtoPlayer _player;
        ProtoShooter _shooter;
        Transform _visual;
        Vector3 _visualScale;
        GameObject _playerRing;
        WeaponStats _cannon;
        WeaponFxSet _cannonFx;   // 탱크 포 연출 세트 — 착탄 폭발만 전용(tank_cannon_hit), 나머지 자리는 기본 세트와 같다

        float _nextFire, _iframeUntil, _reboardAt;
        bool  _armed = true;          // 내린 뒤 범위를 한 번 벗어나야 true
        float _speedNow;
        float _popT;

        LineRenderer _boardRing;
        ProtoTankTracks _tracks;   // 바퀴 자국 (탱크가 아니라 월드에 남는다 — 별도 오브젝트)
        Transform _bar, _barFill;
        Material _barFillMat;
        Material _ringMat, _barBgMat;   // 탱크마다 만든 것 — OnDestroy 에서 같이 지운다(2026-11-20: 예전엔 남았다)

        // ── 생성 · 정리 ─────────────────────────────────────────────────

        /// 런 시작 때 한 대를 무작위 자리에 놓는다. 이후 파괴될 때마다 <see cref="RespawnDelay"/> 뒤 다시 나온다.
        public static void SpawnAbandoned(ProtoPlayer player)
        {
            ResetAll();
            _owner = player;
            if (player == null) return;
            if (Object.FindObjectOfType<ProtoTankSpawner>() == null)
                new GameObject("_TankSpawner").AddComponent<ProtoTankSpawner>();
            ProtoTankIndicator.Ensure();
            SpawnAt(RandomSpot(player.transform.position));
        }

        static void SpawnAt(Vector3 p)
        {
            if (_owner == null || _field != null) return;   // 한 대만
            var prefab = Resources.Load<GameObject>("Vehicles/Tank01");
            if (prefab == null) { Debug.LogWarning("[탱크] Resources/Vehicles/Tank01 없음 — 탱크 없이 진행"); return; }
            var go = Instantiate(prefab, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go.name = "_Tank";
            _field = go.AddComponent<ProtoTank>();
            _field.Init(_owner);
        }

        /// 플레이어에서 SpawnMin~SpawnMax 떨어진 빈 자리. 건물·프롭 안(밀어내기가 크게 일어나는 곳)은 건너뛴다.
        static Vector3 RandomSpot(Vector3 around)
        {
            around.y = 0f;
            float lim = ProtoConfig.ArenaHalf - Radius;
            var obs = ProtoObstacles.I;
            Vector3 fallback = around + Vector3.forward * SpawnMin;
            for (int i = 0; i < 32; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Vector3 p = around + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(SpawnMin, SpawnMax);
                p.x = Mathf.Clamp(p.x, -lim, lim);
                p.z = Mathf.Clamp(p.z, -lim, lim);
                Vector3 r = obs != null ? obs.Resolve(p, Radius, 6) : p;
                if ((r - p).sqrMagnitude > 0.25f) { fallback = r; continue; }   // 프롭에 걸침
                if (Flat(r - around).magnitude < SpawnMin * 0.7f) continue;      // 경계에 눌려 너무 가까워짐
                return r;
            }
            return fallback;
        }

        /// ProtoTankSpawner 가 매 프레임 부른다 — 파괴 후 예약된 재등장.
        public static void TickSpawner()
        {
            var run = ProtoRun.I;
            if (run == null || !run.IsRunning || _owner == null) return;
            if (_field != null || _respawnAt < 0f || run.elapsed < _respawnAt) return;
            _respawnAt = -1f;
            SpawnAt(RandomSpot(_owner.transform.position));
        }

        /// 재시작용. 타고 있었으면 먼저 내려서 캐릭터 모습·카메라를 되돌린다.
        public static void ResetAll()
        {
            if (Ridden != null) Ridden.Exit(false);
            Ridden = null;
            ProtoZombie.TargetPad = 0f;
            _respawnAt = -1f;
            if (ProtoCameraFollow.I != null) ProtoCameraFollow.I.vehicleZoom = 1f;
            if (_field != null) { _hardReset = true; DestroyImmediate(_field.gameObject); _hardReset = false; }
            _field = null;
        }

        /// 하차 버튼 · E 키.
        public static void RequestExit()
        {
            if (Ridden != null && ProtoRun.I != null && ProtoRun.I.IsRunning) Ridden.Exit(false);
        }

        void Init(ProtoPlayer player)
        {
            _player  = player;
            _shooter = player.GetComponent<ProtoShooter>();
            _turret  = transform.Find("Turret");
            _muzzle  = _turret != null ? _turret.Find("Muzzle") : null;

            _cannon = ScriptableObject.CreateInstance<WeaponStats>();
            _cannon.name = "(탱크 포)";
            _cannon.mode = FireMode.Rocket;
            _cannon.damage = CannonDamage;
            _cannon.interval = CannonInterval;
            _cannon.range = CannonRange;
            _cannon.aoeRadius = CannonAoe;
            _cannon.noShake = true;   // 탱크 공격은 화면을 흔들지 않는다 (2026-10-13 사용자 요청)
            _cannon.knockbackMult = 1.8f;
            _cannon.projectileSpeed = 24f;
            _cannon.projectileRadius = 0.55f;
            _cannon.projectileLife = 1.2f;
            _cannon.projectileSize = 0.42f;
            // 착탄 폭발을 공용 explosion 대신 탱크 전용 카탈로그 항목으로(2026-11-17 "FX_Death_XiaoGuai_002 를 대포 착탄에").
            // 세트 기본값(처치 kill_burst 등)은 그대로 — 바뀌는 건 explode 슬롯 하나뿐이다.
            _cannonFx = ScriptableObject.CreateInstance<WeaponFxSet>();
            _cannonFx.name = "(탱크 포 연출)";
            _cannonFx.explode = new WeaponFxSet.Slot(FxIds.TankCannonHit, CannonHitFxScale);
            _cannonFx.explodeNearScale = CannonHitNearScale;
            _cannonFx.explodeFullDistance = CannonHitFullTravel;
            _cannon.fx = _cannonFx;

            // 밤 씬 어둠 막(큐 2900)에 묻히지 않게 캐릭터와 같은 큐로 — 탑승하면 이게 '캐릭터'다.
            var ring = player.GetComponentInChildren<ProtoGroundRing>(true);
            if (ring != null) ring.RaiseQueueOf(gameObject);

            BuildBoardRing();
            BuildBar();
            _tracks = ProtoTankTracks.Create();

            transform.localScale = Vector3.zero;   // 톡 튀어나오듯 등장
            _popT = 0f;
        }

        void OnDestroy()
        {
            if (Ridden == this) Ridden = null;
            if (_field == this) _field = null;
            if (_cannon != null) Destroy(_cannon);
            if (_cannonFx != null) Destroy(_cannonFx);
            if (_barFillMat != null) Destroy(_barFillMat);
            if (_barBgMat != null) Destroy(_barBgMat);
            if (_ringMat != null) Destroy(_ringMat);
            // 바퀴 자국은 탱크가 사라져도 제 수명(9초)만큼 남았다가 지워진다. 재시작이면 바로.
            if (_tracks != null) Destroy(_tracks.gameObject, _hardReset ? 0f : 9f);
        }

        // ── 매 프레임 ───────────────────────────────────────────────────

        void Update()
        {
            TickCamera();
            if (_popT < SpawnPop)
            {
                _popT += Time.deltaTime;
                float k = Mathf.Clamp01(_popT / SpawnPop);
                float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);   // BackOut — 살짝 넘쳤다 돌아온다
                transform.localScale = Vector3.one * back;
            }
            var run = ProtoRun.I;
            if (_tracks != null) _tracks.Tick(transform, Ridden == this && !Wrecked && run != null && run.IsRunning);
            if (Wrecked || run == null || !run.IsRunning || _player == null) { TickBar(); return; }

            if (Ridden == this) TickRide();
            else TickIdle();
            TickBar();
        }

        float _bossMsgAt;   // "보스전엔 못 탄다" 문구를 너무 자주 띄우지 않게

        void TickIdle()
        {
            float d = Flat(_player.transform.position - transform.position).magnitude;
            if (!_armed && d > BoardRange + 0.6f) _armed = true;

            // 최종 보스전 중에는 못 탄다 — 보스가 나올 때 내리고(ProtoSpawnDirector.SpawnBoss), 죽을 때까지 잠근다.
            // 중간 보스는 탈 수 있다(2026-10-21 사용자).
            var sd = ProtoSpawnDirector.I;
            bool bossFight = sd != null && sd.FinalBossActive;
            if (bossFight && d <= BoardRange && Time.unscaledTime >= _bossMsgAt)
            {
                _bossMsgAt = Time.unscaledTime + 3f;
                ProtoHUD.ShowPickup("최종 보스전에는 탱크를 탈 수 없습니다", new Color(1f, 0.55f, 0.4f));
            }
            bool canBoard = _armed && !bossFight && Time.time >= _reboardAt && _player.IsAlive && !ProtoIntro.Playing;
            if (_boardRing != null)
            {
                _boardRing.enabled = canBoard;
                float a = 0.45f + 0.25f * Mathf.Sin(Time.time * 5f);
                var c = new Color(1f, 0.85f, 0.3f, a);
                _boardRing.startColor = c; _boardRing.endColor = c;
            }
            if (canBoard && d <= BoardRange) Board();
        }

        void TickRide()
        {
            if (_boardRing != null) _boardRing.enabled = false;
            Ram();
            PushOutZombies();
            TickTurret();
        }

        /// 플레이어 입력으로 차체를 굴린다. ProtoPlayer.Update 가 부른다(입력은 거기서 읽는다).
        public void Drive(Vector3 dir)
        {
            float dt = ProtoConfig.Delta;
            Vector3 pos = transform.position;
            Vector3 next = pos;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Vector3 n = dir.normalized;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(n), HullTurn * dt);
                // 정면과 어긋날수록 느리게 — 뒤를 보고 있으면 제자리에서 돈다
                float align = Vector3.Dot(transform.forward, n);
                float sp = Speed * Mathf.Min(1f, dir.magnitude) * Mathf.Clamp01((align - 0.2f) / 0.6f);
                next = pos + transform.forward * sp * dt;
            }

            float lim = ProtoConfig.ArenaHalf;
            next.x = Mathf.Clamp(next.x, -lim, lim);
            next.z = Mathf.Clamp(next.z, -lim, lim);
            next.y = 0f;
            if (ProtoObstacles.I != null) next = ProtoObstacles.I.ResolveMove(next, Radius);
            next = ProtoBossArena.Clamp(next, Radius);   // 하차가 막혀 있던 경우의 안전장치

            // 보스는 못 밀어낸다 — 차체가 막힌다
            var boss = ProtoSpawnDirector.I != null ? ProtoSpawnDirector.I.Boss : null;
            if (boss != null && boss.Alive)
            {
                Vector3 to = Flat(next - boss.transform.position);
                float min = Radius + boss.bodyRadius;
                if (to.sqrMagnitude < min * min)
                    next = boss.transform.position + (to.sqrMagnitude > 0.0001f ? to.normalized : -transform.forward) * min;
                next.y = 0f;
            }

            _speedNow = dt > 0f ? Flat(next - pos).magnitude / dt : 0f;
            transform.position = next;
        }

        // ── 박치기 · 밀어내기 ───────────────────────────────────────────

        void Ram()
        {
            if (_speedNow < RamMinSpeed) return;
            var sd = ProtoSpawnDirector.I;
            if (sd == null) return;
            var list = sd.Active;
            int killed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var z = list[i];
                if (z == null || !z.Alive || z.IsBoss) continue;
                Vector3 to = Flat(z.transform.position - transform.position);
                float r = Radius + z.bodyRadius * 0.5f;
                if (to.sqrMagnitude > r * r) continue;
                Vector3 push = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
                z.TakeDamage(z.hp + 1f, push * 2.2f, false, false, HitFx.Blast(null));   // 깔림 — 자리 불똥 없이 처치 효과만
                killed++;
            }
            if (killed == 0) return;
            hp -= RamCost * killed;
            ProtoCameraFollow.Shake(0.05f, 0.12f);
            if (hp <= 0f) Destroyed();
        }

        /// 서 있을 때 차체 안으로 겹쳐 들어온 좀비를 가장자리로 밀어낸다(분리 계산이 밀어 넣는다).
        void PushOutZombies()
        {
            var sd = ProtoSpawnDirector.I;
            if (sd == null) return;
            var list = sd.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                Vector3 to = Flat(z.transform.position - transform.position);
                float r = Radius + z.bodyRadius * 0.5f;
                if (to.sqrMagnitude >= r * r) continue;
                Vector3 n = to.sqrMagnitude > 0.0001f ? to.normalized : -transform.forward;
                z.transform.position = transform.position + n * r;
            }
        }

        // ── 포탑 · 사격 ─────────────────────────────────────────────────

        void TickTurret()
        {
            if (_turret == null) return;
            float dt = ProtoConfig.Delta;
            var sd = ProtoSpawnDirector.I;
            var target = sd != null ? sd.FindNearest(transform.position, CannonRange) : null;

            Vector3 want = target != null ? Flat(target.transform.position - _turret.position) : transform.forward;
            if (want.sqrMagnitude < 0.0001f) want = transform.forward;
            _turret.rotation = Quaternion.RotateTowards(_turret.rotation, Quaternion.LookRotation(want.normalized), TurretTurn * dt);

            if (target == null || Time.time < _nextFire || _shooter == null) return;
            if (Vector3.Angle(Flat(_turret.forward), want) > AimTolerance) return;

            _nextFire = Time.time + CannonInterval / Mathf.Max(0.05f, _player.AtkSpeed);
            Vector3 origin = _muzzle != null ? _muzzle.position : _turret.position + Vector3.up * 1.6f;
            // 몸통(0.9m) 을 겨눠 살짝 내리꽂는다 — 포구가 1.6m 라 수평으로 쏘면 머리 위로 지나간다
            Vector3 aim = target.transform.position + Vector3.up * 0.9f - origin;
            bool crit = Random.value < _player.critChance;
            float dmg = CannonDamage * _player.damageMult * (crit ? ProtoConfig.CritMultiplier : 1f);
            _shooter.FireExternal(_cannon, aim.normalized, origin, dmg, crit);
            ProtoAudio.Play("rocket_fire", origin);
        }

        // ── 탑승 · 하차 · 파괴 ──────────────────────────────────────────

        void Board()
        {
            Ridden = this;
            _visual = _player.transform.Find("Visual");
            if (_visual != null) { _visualScale = _visual.localScale; _visual.localScale = Vector3.zero; }
            var ring = _player.GetComponentInChildren<ProtoGroundRing>(true);
            _playerRing = ring != null ? ring.gameObject : null;
            if (_playerRing != null) _playerRing.SetActive(false);

            _player.transform.position = transform.position;
            ProtoZombie.TargetPad = Radius - 0.3f;   // 좀비가 차체 가장자리에서 멈춰 공격한다
            _nextFire = Time.time + 0.4f;
            ProtoAudio.Play2D("ui_click");
        }

        void Exit(bool destroyed)
        {
            if (Ridden != this) return;
            Ridden = null;
            ProtoZombie.TargetPad = 0f;
            if (_visual != null) _visual.localScale = _visualScale;
            if (_playerRing != null) _playerRing.SetActive(true);

            // 옆(오른쪽 → 왼쪽 → 뒤)으로 내린다. 프롭에 막히면 밀려난 자리를 쓴다.
            Vector3[] dirs = { transform.right, -transform.right, -transform.forward };
            Vector3 best = transform.position + dirs[0] * (Radius + 0.7f);
            foreach (var d in dirs)
            {
                Vector3 p = transform.position + d * (Radius + 0.7f);
                p.y = 0f;
                Vector3 r = ProtoObstacles.I != null ? ProtoObstacles.I.Resolve(p, _player.bodyRadius, 4) : p;
                if ((r - p).sqrMagnitude < 0.01f) { best = r; break; }
                best = r;
            }
            float lim = ProtoConfig.ArenaHalf;
            best.x = Mathf.Clamp(best.x, -lim, lim);
            best.z = Mathf.Clamp(best.z, -lim, lim);
            _player.transform.position = best;
            _player.GrantInvuln(destroyed ? EjectInvuln : ExitInvuln);

            _armed = false;
            _reboardAt = Time.time + ReboardCooldown;
            if (!destroyed) ProtoAudio.Play2D("ui_click");
        }

        /// 좀비 접촉 피해. ProtoPlayer.TakeDamage 가 탑승 중이면 여기로 돌린다.
        public void TakeDamage(float amount)
        {
            if (Wrecked || Time.time < _iframeUntil) return;
            _iframeUntil = Time.time + HitIFrame;
            hp -= amount;
            ProtoAudio.Play("hit", transform.position, 0.8f);
            if (hp <= 0f) Destroyed();
        }

        void Destroyed()
        {
            if (Wrecked) return;
            hp = 0f;
            Wrecked = true;
            Exit(true);

            Vector3 c = transform.position;
            // 카탈로그 tank_blast — 프리팹 FX_Car_Explosion(2026-11-13, 바닥에 BlastFxScale 배 + 풀 · 폭발음), 비우면 코드 그림 폭발
            VehicleBlastFx.Play(FxIds.TankBlast, c + Vector3.up * 0.3f, ExplodeRadius, BlastFxScale, () => ProtoExplosion.Aftermath(c, ExplodeRadius));
            ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Heavy);   // 탱크 파괴
            var sd = ProtoSpawnDirector.I;
            if (sd != null)
            {
                var list = sd.Active;
                float rSq = ExplodeRadius * ExplodeRadius;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var z = list[i];
                    if (z == null || !z.Alive) continue;
                    Vector3 d = Flat(z.transform.position - c);
                    if (d.sqrMagnitude > rSq) continue;
                    z.ApplyBlastSoot();
                    z.TakeDamage(ExplodeDamage * (_player != null ? _player.damageMult : 1f),
                                 (d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward) * 2.5f, false, true, HitFx.Blast(null));
                }
            }

            if (_boardRing != null) _boardRing.enabled = false;
            if (_bar != null) _bar.gameObject.SetActive(false);

            // 잔해는 검게 남긴다(다시 탈 수 없다) — 부품을 떼어 흩뿌린 뒤 그 자리에 그대로 둔다
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(ProtoShaderIds._BaseColor, new Color(0.13f, 0.12f, 0.11f, 1f));
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r != null && !(r is LineRenderer) && (_bar == null || !r.transform.IsChildOf(_bar))) r.SetPropertyBlock(mpb);
            ProtoTankDebris.Shatter(transform, _bar, c);
            StartCoroutine(AfterBlasts(c));
            StartCoroutine(RemoveWreck());
            if (ProtoRun.I != null) _respawnAt = ProtoRun.I.elapsed + RespawnDelay;
        }

        /// 잔해를 잠깐 보여 준 뒤 땅속으로 가라앉혀 지운다. 지워져야(_field == null) 다음 탱크가 나온다.
        System.Collections.IEnumerator RemoveWreck()
        {
            float t = 0f;
            while (t < WreckLinger) { t += ProtoConfig.Delta; yield return null; }
            var deb = GetComponent<ProtoTankDebris>();
            if (deb != null) deb.enabled = false;   // 가라앉는 동안 바닥 판정이 다시 밀어 올리지 않게
            Vector3 from = transform.position;
            t = 0f;
            while (t < WreckSink)
            {
                t += ProtoConfig.Delta;
                float k = Mathf.Clamp01(t / WreckSink);
                transform.position = from + Vector3.down * (2.6f * k * k);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// 큰 폭발 뒤 작은 2차 폭발 두 번 — 탄약·연료가 이어서 터지는 느낌. 판정 없음(연출만).
        System.Collections.IEnumerator AfterBlasts(Vector3 c)
        {
            float[] delays = { 0.28f, 0.55f };
            foreach (var d in delays)
            {
                float until = Time.time + d;
                while (Time.time < until) yield return null;
                Vector2 o = Random.insideUnitCircle * 1.2f;
                ProtoFx.PlayBlast(FxIds.TankDebrisPop, c + new Vector3(o.x, 0.4f, o.y), 1.4f);
                ProtoCameraFollow.Shake(0.1f, 0.15f);
            }
        }

        // ── 카메라 ──────────────────────────────────────────────────────

        void TickCamera()
        {
            var cam = ProtoCameraFollow.I;
            if (cam == null) return;
            float want = Ridden == this ? CamZoom : 1f;
            cam.vehicleZoom = Mathf.MoveTowards(cam.vehicleZoom, want, 0.6f * Time.deltaTime);
        }

        // ── 바닥 원 (탑승 범위) ─────────────────────────────────────────

        void BuildBoardRing()
        {
            var go = new GameObject("BoardRing");
            go.transform.SetParent(transform, false);
            _boardRing = go.AddComponent<LineRenderer>();
            _boardRing.useWorldSpace = false;
            _boardRing.loop = true;
            _boardRing.widthMultiplier = 0.09f;
            _boardRing.alignment = LineAlignment.TransformZ;
            _boardRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _boardRing.receiveShadows = false;
            _boardRing.sharedMaterial = _ringMat = ProtoFxMat.New("TankBoardRing");   // (ProtoFxMat — 게임 이펙트 셰이더 하나, 2026-11-23)
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // XY 원 → 바닥(XZ)
            go.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            const int N = 64;
            _boardRing.positionCount = N;
            for (int i = 0; i < N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                _boardRing.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * BoardRange);
            }
        }

        // ── 머리 위 HP 바 ───────────────────────────────────────────────

        const float BarW = 2.2f, BarH = 0.22f, BarY = 3.0f;

        void BuildBar()
        {
            var sh = Shader.Find("Sprites/Default");
            _bar = new GameObject("HPBar").transform;
            _bar.SetParent(transform, false);

            var bgMat = _barBgMat = new Material(sh) { color = new Color(0f, 0f, 0f, 0.65f) };
            var bg = Quad("Bg", _bar, bgMat);
            bg.localScale = new Vector3(BarW + 0.08f, BarH + 0.08f, 1f);

            _barFillMat = new Material(sh) { color = new Color(0.35f, 0.9f, 0.35f) };
            _barFill = Quad("Fill", _bar, _barFillMat);
            _barFill.localPosition = new Vector3(0f, 0f, -0.01f);
            _barFill.GetComponent<Renderer>().sortingOrder = 1;
        }

        static Transform Quad(string name, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        void TickBar()
        {
            if (_bar == null) return;
            bool show = !Wrecked && (Ridden == this || hp < MaxHP);
            if (_bar.gameObject.activeSelf != show) _bar.gameObject.SetActive(show);
            if (!show) return;

            var cam = Camera.main;
            _bar.position = transform.position + Vector3.up * BarY;
            if (cam != null) _bar.rotation = cam.transform.rotation;

            float t = Mathf.Clamp01(hp / MaxHP);
            _barFill.localScale = new Vector3(BarW * t, BarH, 1f);
            _barFill.localPosition = new Vector3(-BarW * (1f - t) * 0.5f, 0f, -0.01f);
            _barFillMat.color = t > 0.5f ? new Color(0.35f, 0.9f, 0.35f)
                              : t > 0.25f ? new Color(1f, 0.75f, 0.2f) : new Color(0.95f, 0.25f, 0.2f);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
