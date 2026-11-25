// 좀비 오버런 — 프로토타입 튜닝 수치 (코드 상수로 남는 것만)
// 값의 출처는 기획서(GDD) 절 번호로 표기했다. 프로토에서 의도적으로 다르게 잡은 값은 이유를 적었다.
//
// **여기 없는 것** — 2026-11-05 에 데이터 에셋으로 옮겼다. 아래를 고치려면 코드가 아니라 에셋을 연다.
//   · 무기 수치            → Assets/_Game/Data/Weapons/Stats/WS_*.asset (WeaponStats)
//   · 적 7종 수치·스폰 조건 → Assets/_Game/Data/Enemies/E_*.asset (EnemyDef)
//   · 보스·습격·엘리트 일정 → Assets/_Game/Resources/Data/EnemyTable.asset (EnemyTable)
//   · 캐릭터 ↔ 무기 ↔ 필사기 → Assets/_Game/Data/Characters/C_*.asset + Resources/Data/CharacterTable.asset
//   · 필사기 수치           → Assets/_Game/Data/Ultimates/ULT_*.asset (UltimateDef 자식)
// 남은 것은 "게임 전체의 성질"(플레이어 공통, 피격 느낌, 스폰 예산, 밸런스 수식, 프레임 안전장치)과 디버그 스위치다.
using UnityEngine;

namespace Proto
{
    public static class ProtoConfig
    {
        // ── 플레이어 (GDD §04) ──────────────────────────────────────────
        public const float PlayerMaxHP     = 100f;
        public const float MoveSpeed       = 4.2f;
        public const float MoveSpeedCap    = 7.5f;   // 이 상한을 넘기면 스폰이 못 따라와 전투가 사라진다
        public const float PickupRadius    = 3.0f;   // 젬(물고기) 자석 반경. 1.8m 는 "너무 가까이 가야 먹힌다"(2026-11-19 사용자) → 3m

        // 몸이 목표 방향으로 돌아가는 속도. 예전에는 사격 순간에 즉시 대입해서
        // 로켓(간격 1.7s)은 1.7초에 한 번 툭 돌았다. 720°/s 면 180° 반전이 0.25초 —
        // 자동 사격 게임에서 조준이 늦다는 느낌 없이 회전이 이어져 보이는 하한이다.
        public const float TurnSpeed       = 720f;
        public const float CritChance      = 0.05f;
        public const float CritMultiplier  = 1.5f;
        public const float InvulnAfterHit  = 0.6f;

        // ── 임시(테스트 전용) — 무적 ──────────────────────────────────
        // 사용자 요청(2026-10-13): 보스 테스트 동안 캐릭터가 죽지 않게. 모든 피해(탱크 탑승 중 포함)를 무시한다.
        // **테스트가 끝나면 false 로 되돌릴 것.**
        public static bool DebugInvincible = false;   // 2026-10-13 테스트 끝 — 꺼 둠

        // ── 주무기 폴백 ─────────────────────────────────────────────────
        // 여기를 고쳐도 인게임은 안 바뀐다 — 무기 수치는 `WeaponStats` 에셋(Assets/_Game/Data/Weapons/Stats)에서 읽는다.
        // 아래는 무기 에셋이 안 물려 있을 때의 폴백일 뿐이다. 소총을 튜닝하려면 `WS_Rifle_TangTang` 을 고칠 것.
        public const float WeaponDamage    = 12f;
        public const float WeaponInterval  = 0.35f;
        public const float WeaponRange     = 9f;
        public const float ProjectileSpeed = 24f;
        public const float ProjectileRadius= 0.45f;
        public const float ProjectileLife  = 1.2f;

        // ── 버프 아이템 (승합차가 터지면 떨군다, 프로토 — 2026-10-14 사용자 요청) ─────
        // 4종: HP · 자석 · 아드레날린 · 보호막 (폭탄은 사용자가 뺐다 — 승합차 폭발과 역할이 겹친다).
        // 승합차 재생성 시간·대수는 사용자가 나중에 조절한다(ProtoDestructibles.RespawnAfter) —
        // 그에 따라 아이템이 너무 흔해지면 PickupDropChance 로 줄인다.
        public const float PickupDropChance  = 1.0f;   // 승합차 1대당 떨굴 확률
        public const float PickupWeightHP     = 40f, PickupWeightMagnet = 25f, PickupWeightAdrenaline = 20f, PickupWeightShield = 15f;
        public const float PickupLowHP        = 0.3f;  // HP 가 이 비율 이하면 HP 가중치 ×PickupLowHPBoost
        public const float PickupLowHPBoost   = 3f;
        public const float PickupLife         = 15f;   // 바닥에 남는 시간. 마지막 4초는 깜빡인다
        public const float ItemPickupRadius   = 1.0f;  // 이 거리 안에 들어가면 먹는다(젬 자석 범위와 별개 — 직접 가서 먹어야 한다)
        public const float PickupHealPct      = 0.2f;  // 최대 HP 의 20%
        public const float AdrenalineTime     = 8f;
        public const float AdrenalineAtkMult  = 1.5f;
        public const float AdrenalineMoveMult = 1.2f;
        public const float ShieldTime         = 4f;
        public const float MagnetItemRadius   = 6f;    // 자석 아이템 흡수 반경 — 화면 전체(MagnetAll)는 너무 강했다(2026-10-14 사용자). 화면 가로폭 ~10m

        // ── 피격 피드백 (GDD §15: 데미지 숫자를 최소화하라 → 숫자 대신 플래시+넉백) ──
        // 플래시는 머티리얼의 _EmissionColor 를 MaterialPropertyBlock 으로 흔든다.
        // 그래서 좀비/보스 머티리얼에 _EMISSION 키워드가 켜져 있어야 한다 (평상시 색은 검정).
        public const float HitFlashTime     = 0.09f;  // 더 길면 물량전에서 화면이 하얗게 뒤덮인다
        public const float HitFlashStrength = 1.5f;   // 1.5 면 거의 순백. 낮추면 원래 색이 비친다
        public const float KnockbackDist    = 0.16f;  // 총알 한 발에 밀리는 거리. 적마다의 배율은 EnemyDef.knockback
        public const float KnockbackTime    = 0.11f;  // 밀림이 사그라드는 시간

        // ── 임시(테스트 전용) — 바로 최종 보스 ─────────────────────────
        // 사용자 요청(2026-10-13): 보스 테스트용. 켜면 중간 보스를 건너뛰고 런 시작 DebugFinalBossAt 초에 최종 보스가 나온다.
        // HP 는 정식 등장 시각(EnemyTable.finalBossAt) 기준으로 계산해 실제와 같은 세기로 싸운다.
        // **테스트가 끝나면 false 로 되돌릴 것.**
        public static bool  DebugFinalBossFirst = false;   // 2026-10-13 테스트 끝 — 꺼 둠
        public const  float DebugFinalBossAt    = 3f;
        public const float ClearDelay = 2.5f;   // 최종 보스 사망 애니를 보여준 뒤 클리어 화면

        // 잡좀비의 기준 몸 반경. 투사체 판정은 이 값을 기준으로 잡혀 있으므로,
        // 덩치가 다른 적은 "초과분"만 판정 반경에 더한다 (기존 좀비 감각을 건드리지 않기 위해).
        // 적마다의 몸 반경은 EnemyDef.BodyRadius (비워 두면 이 값 × 덩치).
        public const float BaseBodyRadius = 0.28f;

        // 좀비끼리 밀어낼 때 서로의 몸 반경을 합쳐 최소 거리를 만든다.
        // 좀비(0.28)+좀비(0.28)+여유(0.06) = 0.62 로 기존 SeparationDist 와 같다.
        public const float SeparationPad = 0.06f;

        // ── 스폰 디렉터 (GDD §09) ───────────────────────────────────────
        // 프로토는 적마다 Animator를 쓰는 MonoBehaviour 구조라 상한을 90으로 낮췄다.
        // 기획서의 350은 VAT + Job 구조에서만 가능한 수치다 (GDD §16).
        // 어떤 적이 언제 섞여 나오는지는 EnemyDef(unlockAt·chance·maxAlive)와 EnemyTable(일정)에 있다.
        public const int   BudgetBase   = 30;
        public const float BudgetPerMin = 8f;
        public const int   BudgetCap    = 90;
        public const float SpawnRingPad = 3f;     // 가시 영역 외곽 +3m
        public const float SpawnInterval= 0.25f;
        // **const 가 아니다.** 배경 스트리밍(ProtoFieldStreamer)이 청크 월드 크기로
        // 넘겨써야 하기 때문이다 — 19m 고정이면 청크를 아무리 만들어도 플레이어가 20m 에서 막힌다.
        // 스트리머가 OnDestroy 에서 원래 값으로 되돌린다(도메인 리로드가 꺼져 있을 때를 대비).
        public static float ArenaHalf   = 19f;    // 40×40 아레나 안쪽 여유

        // ── 런 ──────────────────────────────────────────────────────────
        // 프로토는 5분 판 (기획서 챕터1은 15분). 2026-10-13 부터 **시간이 다 돼도 클리어가 아니다** —
        // 최종 보스를 잡아야 끝난다. 이 값은 보스 시각을 잡는 기준으로만 남아 있다.
        public const float RunDuration = 300f;

        // ── 좀비 상호 밀어내기 ──────────────────────────────────────────
        public const float SeparationDist     = 0.62f;  // 두 좀비 중심이 이보다 가까우면 겹침
        public const float SeparationStrength = 0.55f;  // 1이면 즉시 완전 분리 (떨림 방지로 낮춤)
        public const float GridCellSize       = 1.0f;   // 분리 거리보다 크게 → 3×3 이웃으로 충분
        public const float GridWorldHalf      = 22f;

        // ── 프레임 시간 상한 ────────────────────────────────────────────
        // 씬 로드 직후·에디터 포커스 복귀 직후 첫 프레임의 deltaTime 은 수백 ms에 달할 수 있다.
        // 그대로 이동에 곱하면 캐릭터가 순간이동해서 "튕기는" 것처럼 보인다.
        public const float MaxDeltaTime = 0.05f;   // 20fps 상당으로 고정

        /// 이동·보간에 쓸 안전한 델타. Time.deltaTime 을 직접 쓰지 말 것.
        public static float Delta      => Mathf.Min(Time.deltaTime,        MaxDeltaTime);
        public static float UnscaledDelta => Mathf.Min(Time.unscaledDeltaTime, MaxDeltaTime);

        // ── 밸런스 수식 (GDD §13) ───────────────────────────────────────
        /// 적 체력 스케일: base × (1 + 0.18 × 경과 분)
        public static float EnemyHPScale(float elapsedSeconds)
        {
            return 1f + 0.18f * (elapsedSeconds / 60f);
        }

        /// 레벨업 필요 경험치: 8 + 4(L-1) + 0.35(L-1)²
        public static float XPNeeded(int level)
        {
            float n = level - 1;
            return 8f + 4f * n + 0.35f * n * n;
        }
    }
}
