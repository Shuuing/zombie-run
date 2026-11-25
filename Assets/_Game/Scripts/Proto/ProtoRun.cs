using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    public enum RunState { Running, LevelUp, Result, Intro }   // Intro = 시작 연출(ProtoIntro) 중 — 스폰·사격·타이머·이동 정지

    /// 런 진행 허브: 타이머, 경험치/레벨, 레벨업 카드, 결과.
    public class ProtoRun : MonoBehaviour
    {
        public static ProtoRun I { get; private set; }

        [Header("참조")]
        public ProtoPlayer player;
        public ProtoSpawnDirector director;
        public ProtoGemPool gems;

        [Header("보조 장비 카드 (§07 — 자동 공격 장치, 슬롯 상한 2)")]
        // 카드가 무엇을 주는지는 코드가 아니라 에셋이 정한다. 씬 빌더가 물려준다.
        public WeaponModelDef gearOrbitBlade;
        public WeaponModelDef gearSupportDrone;
        public WeaponModelDef gearGrenade;
        public WeaponModelDef gearHeli;

        [Header("상태")]
        public RunState state = RunState.Running;
        public float elapsed;
        public int level = 1;
        public float xp;
        public float xpNeeded;
        public int kills;
        public bool survived;

        public bool  IsRunning => state == RunState.Running;
        public float Elapsed   => elapsed;
        public float Remaining => Mathf.Max(0f, ProtoConfig.RunDuration - elapsed);

        // ── 업그레이드 정의 (GDD §06 패시브) ────────────────────────────
        public class Upgrade
        {
            public string id, title, desc;
            public int maxLevel = 5;
            public System.Action<ProtoPlayer> apply;

            /// 보조 장비 카드면 그 장비. null 이면 일반 버프다.
            /// 슬롯이 꽉 찼을 때 "먹을 수 없는 카드"를 거르려면 후보 단계에서 알아야 한다(§07 원칙).
            public WeaponModelDef gear;
        }

        public readonly Dictionary<string, int> upgradeLevels = new Dictionary<string, int>();
        List<Upgrade> _all;
        /// 강화 항목 전체(일시정지 · 결과 화면의 보유 목록용 — 이름 · 최대 레벨을 읽는다).
        public IReadOnlyList<Upgrade> AllUpgrades => _all;
        public List<Upgrade> currentCards = new List<Upgrade>();
        public int rerollsLeft = 1;
        readonly Queue<string> _recent = new Queue<string>();

        void Awake()
        {
            I = this;
            Time.timeScale = 1f;   // 이전 세션이 정지 상태로 끝났을 수 있다
            ProtoSettings.ApplyAll();   // 저장된 옵션(안개 등)을 씬에 입힌다
            ProtoBurnFx.Ensure();       // 불타는 좀비용 공유 파티클 (씬 배선 없음)
            ProtoAirstrike.Preload();   // 헬리콥터 공습 카드·데몰리셔 진화 폭격의 그림자·미사일·헬기 소리 미리 준비
            ProtoRainFx.Ensure();       // 비 (옵션이 꺼져 있으면 방출하지 않는다)
            ProtoDestructibles.Ensure(); // 쏴서 터뜨리는 승합차(프로토 한정) — _Props/Van_Abandoned 를 훑는다
            ProtoHeliCrash.Ensure();     // 좀비가 매달린 헬기 추락(프로토 한정) — 한 판에 1~2번
            BuildUpgrades();
            xpNeeded = ProtoConfig.XPNeeded(level);
        }

        void Start()
        {
            ProtoTank.SpawnAbandoned(player);   // 프로토 한정 테스트 — 필드 무작위 자리에 탱크 1대 (파괴되면 잠시 뒤 다른 자리에)
            ProtoIntro.Play(this);              // 비행기 → 낙하산 → 착지. 끝나면 Running
        }

        void OnDestroy() { if (I == this) I = null; }

        void BuildUpgrades()
        {
            _all = new List<Upgrade>
            {
                new Upgrade { id="dmg",   title="화력 증폭",   desc="공격력 +8%",
                              apply = p => p.damageMult   += 0.08f },
                new Upgrade { id="rate",  title="탄창 확장",   desc="공격 속도 +7%",
                              apply = p => p.atkSpeedMult += 0.07f },
                new Upgrade { id="move",  title="경량 프레임", desc="이동 속도 +6%",
                              apply = p => p.moveMult     += 0.06f },
                new Upgrade { id="hp",    title="방탄 장비",   desc="최대 체력 +12%",
                              apply = p => p.RaiseMaxHP(0.12f) },
                new Upgrade { id="magnet",title="자기장 코일", desc="픽업 반경 +25%, 경험치 +6%",
                              apply = p => { p.pickupMult += 0.25f; p.xpMult += 0.06f; } },
                new Upgrade { id="crit",  title="정밀 조준",   desc="치명타 확률 +4%",
                              apply = p => p.critChance   += 0.04f },
                // 무기 진화 재료(ProtoEvolution.WeaponCardId) — 예전 "소총 강화". 캐릭터마다 주무기가 달라 이름을 바꿨다(id 는 그대로)
                new Upgrade { id="rifle", title="주무기 강화", desc="주무기 피해 +18%",
                              apply = p => p.damageMult   += 0.18f },
                new Upgrade { id="heal",  title="응급 처치",   desc="즉시 체력 35 회복",  maxLevel = 99,
                              apply = p => p.Heal(35f) },
            };

            // ── 보조 장비 (§07 신설 계열) ────────────────────────────────
            // 주무기가 아니라 카드로 얻는 자동 공격 장치. 슬롯 상한 2 는 ProtoSupportGear 가 지킨다.
            AddGearCard(gearOrbitBlade,  "회전 블레이드", "몸 주위를 도는 톱날. 레벨마다 개수·피해 증가");
            AddGearCard(gearSupportDrone, "지원 드론",    "따라다니며 스스로 조준해 쏜다. 레벨마다 기수·피해 증가");
            AddGearCard(gearGrenade,      "수류탄",       "좀비가 가장 많이 모인 곳에 자동으로 던진다. 레벨마다 개수·반경·피해 증가");
            AddGearCard(gearHeli,         "헬리콥터 공습", "무전으로 공격 헬기를 불러 기총 소사로 좀비를 쓸어 버린다. 레벨마다 출격 횟수·피해 증가");

            foreach (var u in _all) upgradeLevels[u.id] = 0;
        }

        void AddGearCard(WeaponModelDef def, string title, string desc)
        {
            if (def == null || def.stats == null) return;   // 배선 안 됐으면 카드가 아예 안 뜬다
            _all.Add(new Upgrade
            {
                id = "gear_" + def.name,
                title = title,
                desc = desc,
                maxLevel = 5,
                gear = def,
                apply = p =>
                {
                    var g = p.GetComponent<ProtoSupportGear>();
                    if (g == null) g = p.gameObject.AddComponent<ProtoSupportGear>();
                    g.Acquire(def);
                },
            });
        }

        void Update()
        {
            if (state != RunState.Running) return;

            elapsed += Time.deltaTime;
            TickKillRate();
            // 클리어는 시간이 아니라 최종 보스 처치다(2026-10-13). 사망 애니를 보여준 뒤 결과 화면.
            if (_clearAt > 0f && elapsed >= _clearAt)
            {
                _clearAt = 0f;
                survived = true;
                EndRun();
            }
        }

        float _clearAt;

        /// 최종 보스 처치 — ClearDelay 뒤 클리어. 그 사이에도 게임은 돈다(젬 줍기·벽 가라앉기).
        public void OnFinalBossKilled()
        {
            if (state == RunState.Result || _clearAt > 0f) return;
            _clearAt = elapsed + ProtoConfig.ClearDelay;
        }

        public void AddXP(float amount)
        {
            if (state != RunState.Running) return;
            xp += amount;
            if (xp >= xpNeeded) BeginLevelUp();
        }

        public void OnKill() { kills++; }

        /// 좀비 처치 — 킬 수 + 필사기 게이지. 엘리트는 +20%(§06).
        public void OnKill(ProtoZombie z)
        {
            OnKill();
            _killsThisFrame++;
            ChargeUlt(z != null && z.IsElite ? 1.2f : 1f);
        }

        // ── 필사기 (§06 v0.7) ───────────────────────────────────────────
        // 캐릭터별 고유 필사기(ProtoUltimate). 쿨타임이 아니라 처치로 차는 게이지다.
        // 2026-10-29 결정: 파트너 12초 합류(ProtoAlly)는 삭제, 먼저 만든 캐릭터는 루키·파이로(+브레이커 10-29), 갈래 변경은 무료.
        // 컷인(10-29)·헬기 융단 폭격(10-28)도 예전에 필사기에서 뺐다 — 헬기는 레벨업 카드 "헬리콥터 공습"이다.
        //
        // 주기: 발동 후 UltLockout(30초) 동안은 차지 않고, 평균 처치 속도면 발동 후 UltCycle(약 50초)에 다시 찬다.
        // 처치 1마리당 충전량 = 1 / (최근 처치 속도 × 충전 구간). 처치 속도는 긴 이동 평균이라 물량이 늘어도 주기가 짧아지지 않고,
        // 평소보다 많이 잡는 순간에만 빨리 찬다. 보스에게 준 피해 1%당 +2%.
        public const float UltCycle = 50f;
        public const float UltLockout = 30f;
        const float KillRateWindow = 40f;   // 처치 속도 이동 평균 시간(초)
        const float MinKillRate = 0.3f;

        [Header("필사기 스킬 트리 (프로토 임시 — 로비 대신 인스펙터 · T키)")]
        public UltTree rookieTree = new UltTree();
        public UltTree pyroTree = new UltTree();
        public UltTree breakerTree = new UltTree();

        [Header("필사기 게이지")]
        [Range(0f, 1f)] public float ultCharge;
        float _ultLockUntil = UltLockout;   // 런 시작도 막 쓴 것처럼 — 첫 충전은 약 50초
        float _killRate = 1f;               // 초당 처치(이동 평균)
        int _killsThisFrame;

        public bool  UltLocked => elapsed < _ultLockUntil;
        public float UltLockRemaining => Mathf.Max(0f, _ultLockUntil - elapsed);
        public ProtoUltimate.Kind UltKind => ProtoUltimate.KindOf(player);
        public bool  UltReady => state == RunState.Running && ultCharge >= 1f && UltKind != ProtoUltimate.Kind.None && !ProtoUltimate.Active;

        public UltTree TreeOf(ProtoUltimate.Kind k) => k == ProtoUltimate.Kind.Rookie ? rookieTree : k == ProtoUltimate.Kind.Pyro ? pyroTree : k == ProtoUltimate.Kind.Breaker ? breakerTree : null;

        void ChargeUlt(float units)
        {
            if (state != RunState.Running || UltLocked || ProtoUltimate.Active) return;
            float span = Mathf.Max(1f, UltCycle - UltLockout);
            ultCharge = Mathf.Min(1f, ultCharge + units / (Mathf.Max(MinKillRate, _killRate) * span));
        }

        /// 보스에게 준 피해(최대 체력 대비 비율). 1% 당 게이지 2%.
        public void OnBossDamaged(float fraction)
        {
            if (state != RunState.Running || UltLocked || ProtoUltimate.Active || fraction <= 0f) return;
            ultCharge = Mathf.Min(1f, ultCharge + fraction * 2f);
        }

        void TickKillRate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _killRate += (_killsThisFrame / KillRateWindow) - _killRate * (dt / KillRateWindow);
            _killsThisFrame = 0;
        }

        public bool TryUltimate()
        {
            if (!UltReady || player == null) return false;
            if (!ProtoUltimate.Play(player, TreeOf(UltKind))) return false;   // 표적이 없으면 게이지를 쓰지 않는다
            ultCharge = 0f;
            _ultLockUntil = elapsed + UltLockout;
            return true;
        }

        /// T키 — 지금 캐릭터의 필사기 트리를 한 단계씩 돌린다(기본 → I → I·II → A → B).
        public void CycleUltTree()
        {
            var t = TreeOf(UltKind);
            if (t == null) return;
            t.Cycle();
            ProtoHUD.ShowPickup($"{ProtoUltimate.NameOf(player)} 트리: {t.Short()}", new Color(1f, 0.79f, 0.29f));
        }

        /// 디버그: 게이지 가득(F7) — 재사용 하한도 푼다.
        public void DebugFillUlt()
        {
            if (state != RunState.Running) return;
            _ultLockUntil = elapsed;
            ultCharge = 1f;
        }

        /// 디버그: 융단 폭격만 따로(F3).
        public void DebugAirstrike()
        {
            if (state == RunState.Running && player != null) ProtoAirstrike.Play(player.transform.position);
        }

        /// 디버그: 헬리콥터 공습(기총 소사) 지금 바로(F6) — 카드·쿨타임·근처 좀비 조건 없이.
        /// 카드를 가졌으면 그 레벨 수치, 없으면 Lv1 수치로 부른다.
        public void DebugHeliStrike()
        {
            if (state != RunState.Running || player == null || gearHeli == null || gearHeli.stats == null) return;
            var g = player.GetComponent<ProtoSupportGear>();
            int lv = Mathf.Max(1, g != null ? g.LevelOf(gearHeli) : 0);
            var st = gearHeli.stats;
            ProtoHeliGunship.Call(player.transform, st.damage * st.DamageMultAtLevel(lv), st.CountAtLevel(lv), WeaponFxSet.Of(st));
        }

        /// 디버그: 즉시 생존 성공(클리어 연출 확인용)
        public void DebugClear()
        {
            if (state == RunState.Result) return;
            if (state == RunState.LevelUp) currentCards.Clear();
            survived = true;
            EndRun();
        }

        void BeginLevelUp()
        {
            xp -= xpNeeded;
            level++;
            xpNeeded = ProtoConfig.XPNeeded(level);
            rerollsLeft = 1;
            DrawCards();
            state = RunState.LevelUp;
            Time.timeScale = 0f;   // GDD §07: 레벨업 시 완전 정지
        }

        /// 3장 제시. 최대 레벨에 도달한 항목은 후보에서 제외 —
        /// "먹을 수 없는 카드"를 절대 보여주지 않는다 (GDD §07).
        public void DrawCards()
        {
            var pool = new List<Upgrade>();
            var weights = new List<float>();

            var gear = player != null ? player.GetComponent<ProtoSupportGear>() : null;

            foreach (var u in _all)
            {
                if (upgradeLevels[u.id] >= u.maxLevel) continue;
                // 슬롯이 꽉 찬 상태에서 "새" 보조 장비는 먹을 수 없다 — 보여주지 않는다(§07 원칙).
                if (u.gear != null && upgradeLevels[u.id] == 0 && gear != null && gear.IsFull) continue;
                float w = upgradeLevels[u.id] > 0 ? 2f : 1f;   // 보유 항목 강화에 가중
                if (_recent.Contains(u.id)) w *= 0.35f;        // 최근 이력 쿨다운
                pool.Add(u); weights.Add(w);
            }

            currentCards.Clear();
            for (int pick = 0; pick < 3 && pool.Count > 0; pick++)
            {
                float total = 0f;
                for (int i = 0; i < weights.Count; i++) total += weights[i];
                float r = Random.value * total;
                int chosen = weights.Count - 1;
                for (int i = 0; i < weights.Count; i++)
                {
                    r -= weights[i];
                    if (r <= 0f) { chosen = i; break; }
                }
                currentCards.Add(pool[chosen]);
                pool.RemoveAt(chosen); weights.RemoveAt(chosen);
            }
        }

        public void Reroll()
        {
            if (rerollsLeft <= 0) return;
            rerollsLeft--;
            ProtoAudio.Play2D("ui_click");
            DrawCards();
        }

        public void PickCard(int index)
        {
            if (index < 0 || index >= currentCards.Count) return;
            var u = currentCards[index];
            u.apply(player);
            upgradeLevels[u.id]++;

            // 적용됐다는 연출 — 캐릭터에서 하늘로 빛기둥. 씬 배선이 필요 없는 정적 호출이다.
            if (player != null) ProtoFx.Play(FxIds.LevelUpBeam, FxContext.At(player.transform.position));
            // 빛기둥이 0.13초에 솟구치므로 아르페지오 첫 음을 같은 순간에 붙인다.
            ProtoAudio.Play2D("levelup");

            _recent.Enqueue(u.id);
            while (_recent.Count > 2) _recent.Dequeue();

            // 주무기 Lv5 를 채운 순간 — 진화권(보스 상자)을 들고 있으면 바로 진화
            if (u.id == ProtoEvolution.WeaponCardId) ProtoEvolution.TryEvolve(this);

            ResumeFromLevelUp();
        }

        public void SkipCards()
        {
            ProtoAudio.Play2D("ui_click");
            ResumeFromLevelUp();   // GDD §07: 스킵 허용 (프로토는 골드 보상 생략)
        }

        void ResumeFromLevelUp()
        {
            currentCards.Clear();
            state = RunState.Running;
            Time.timeScale = 1f;
            // 한 번에 여러 레벨이 밀려 있으면 이어서 처리
            if (xp >= xpNeeded) BeginLevelUp();
        }

        public void OnPlayerDied()
        {
            survived = false;
            EndRun();
        }

        void EndRun()
        {
            state = RunState.Result;
            Time.timeScale = 0f;
            // 울리고 있던 총성·신음을 먼저 끊는다. 결과 화면의 팡파르가 물량전의 잔향
            // 위에 얹히면 무엇이 끝났는지가 들리지 않는다. 화염 루프도 여기서 같이 죽는다.
            ProtoAudio.StopAll();
            ProtoSpritePopup.Stop();   // 전용 소리 소스를 끊는다 — 결과 팡파르 위에 이전 소리가 남지 않게
            ProtoAirstrike.Stop();
            ProtoHeliGunship.Stop();
            ProtoHeliCrash.Stop();
            ProtoUltimate.Stop();
            ProtoIntro.Stop();
            ProtoAudio.Play2D(survived ? "victory" : "gameover");
            if (survived) ProtoSpritePopup.PlayClear();   // 클리어 애니 + 영상 소리(첫 바퀴 싱크)
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            ProtoAudio.StopAll();   // 결과 화면의 팡파르가 다음 판으로 넘어오지 않게
            ProtoTank.ResetAll();   // 타고 있었으면 먼저 내린다 — 캐릭터 모습·카메라 복구
            director?.ClearAll();
            ProtoEnemyBolt.ClearAll();   // 원거리 좀비가 던져 둔 구슬
            ProtoDestructibles.ResetAll(); // 터진 승합차 원래대로
            ProtoPickups.ClearAll();       // 승합차가 떨군 버프 아이템
            ProtoPoisonGas.ClearAll();
            ProtoHeliCrash.ResetAll();     // 떠 있던 헬기·잔해 치우고 일정 다시
            gems?.ClearAll();

            elapsed = 0f; level = 1; xp = 0f; kills = 0;
            _clearAt = 0f;
            ultCharge = 0f;
            _ultLockUntil = UltLockout;
            _killRate = 1f;
            _killsThisFrame = 0;
            ProtoSpritePopup.Stop();
            ProtoAirstrike.Stop();
            ProtoHeliGunship.Stop();
            ProtoUltimate.Stop();
            ProtoDamageText.Clear();
            xpNeeded = ProtoConfig.XPNeeded(level);
            survived = false;
            rerollsLeft = 1;
            currentCards.Clear();
            _recent.Clear();

            foreach (var u in _all) upgradeLevels[u.id] = 0;
            ProtoEvolution.ResetRun(player);   // 진화 무기를 들고 있으면 기본 무기로

            if (player != null)
            {
                // 보조 장비도 런 성장이므로 재시작 때 사라져야 한다. 안 지우면
                // 다시 시작한 판에 지난 판의 블레이드가 그대로 돌고 있다.
                var gear = player.GetComponent<ProtoSupportGear>();
                if (gear != null) gear.ClearAll();

                player.transform.position = Vector3.zero;
                player.maxHP        = ProtoConfig.PlayerMaxHP;
                player.hp           = player.maxHP;
                player.moveMult     = 1f;
                player.damageMult   = 1f;
                player.atkSpeedMult = 1f;
                player.pickupMult   = 1f;
                player.xpMult       = 1f;
                player.critChance   = ProtoConfig.CritChance;
                player.ResetBuffs();
            }
            state = RunState.Running;
            ProtoTank.SpawnAbandoned(player);
            ProtoIntro.Play(this);   // 다시 시작해도 낙하산으로 내려온다
        }
    }
}
