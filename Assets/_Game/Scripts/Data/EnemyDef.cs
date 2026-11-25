using UnityEngine;
using Proto;

/// <summary>
/// 적 1종의 <b>수치</b>. 예전에는 <c>ProtoConfig</c> 의 Walker*/Runner*/Ranged*/Dog*/Boss*/Elite* 상수였다.
///
/// <para><b>행동은 코드, 숫자는 여기.</b> <see cref="behaviour"/> 가 코드에 있는 다섯 가지 움직임
/// (걷기·자폭·원거리·물기·보스) 중 하나를 고르고, 나머지 칸이 그 움직임의 숫자를 채운다.
/// 새 적을 "같은 움직임, 다른 숫자"로 만들 때는 에셋만 복제하면 된다 — 엘리트가 그 예다
/// (워커 움직임 + 큰 덩치 + 높은 체력). 새 움직임이 필요할 때만 코드(<c>ProtoZombie</c>)를 건드린다.</para>
///
/// <para>모델(프리팹)은 아직 스폰 디렉터 인스펙터에 있다 — 풀이 프리팹 단위라서 함께 옮기려면
/// 풀 구조를 바꿔야 한다. 다음 단계.</para>
/// </summary>
[CreateAssetMenu(menuName = "좀비 오버런/적", fileName = "E_NewEnemy")]
public class EnemyDef : ScriptableObject
{
    [Tooltip("기획서·로그에 쓰는 이름")]
    public string displayName;

    [Tooltip("움직임. 코드에 있는 다섯 가지 중 하나 — 숫자가 아니라 '어떻게 움직이나'를 고른다.")]
    public ZombieKind behaviour = ZombieKind.Walker;

    [Header("스탯")]
    [Tooltip("기본 체력. 경과 시간 스케일(ProtoConfig.EnemyHPScale)이 곱해진다.")]
    public float hp = 12f;
    public float moveSpeed = 1.6f;
    [Tooltip("접촉(물기) 1회 피해. 붙어서 때리지 않는 적(원거리)은 0.")]
    public float contactDamage = 8f;
    [Tooltip("이 거리 안이면 멈춰서 때린다(m). 개는 몸이 길어 더 멀다.")]
    public float contactRange = 0.85f;
    [Tooltip("접촉 공격 간격(초).")]
    public float contactCooldown = 0.8f;
    [Tooltip("처치 경험치.")]
    public int xp = 1;

    [Header("몸")]
    [Tooltip("덩치 배율. 모델 스케일과 몸 반경에 같이 곱한다 (1 = 잡좀비).")]
    public float scale = 1f;
    [Tooltip("몸 반경(m). 0 이면 잡좀비 기준(ProtoConfig.BaseBodyRadius). scale 이 곱해진다.")]
    public float bodyRadius = 0f;
    [Tooltip("총알 한 발에 밀리는 정도. 1 = 잡좀비, 0 = 안 밀린다.")]
    public float knockback = 1f;
    [Tooltip("밀어내기에서 이 개체가 밀릴 비중. 0 이면 잡좀비가 밀어도 꿈쩍 안 한다(보스).")]
    public float separationWeight = 1f;
    [Tooltip("사망 애니메이션 재생 후 풀로 돌아가기까지(초).")]
    public float deathLinger = 2.2f;
    [Tooltip("스킨 교체. 비우면 프리팹 머티리얼 그대로. (러너의 빨간 스킨)")]
    public Material skin;

    [Header("일반 스폰 (디렉터가 섞어 내는 종류만)")]
    [Tooltip("이 초부터 섞여 나온다.")]
    public float unlockAt = 0f;
    [Tooltip("스폰 1회당 이 종류가 나올 확률.")]
    [Range(0f, 1f)] public float chance = 0f;
    [Tooltip("동시에 이 이상은 안 나온다. 0 = 무제한.")]
    public int maxAlive = 0;

    [Header("원거리 (behaviour = Ranged)")]
    public RangedBlock ranged = new RangedBlock();

    [Header("물기 (behaviour = Dog)")]
    public BiteBlock bite = new BiteBlock();

    [Header("독가스 자폭 (behaviour = Runner)")]
    public GasBlock gas = new GasBlock();

    [TextArea(2, 6)]
    [Tooltip("메모. 왜 이 값인지, 기획서와 다르면 그 이유.")]
    public string note;

    [System.Serializable]
    public class RangedBlock
    {
        [Tooltip("이보다 가까우면 멈춰서 쏜다.")] public float stopRange = 8f;
        [Tooltip("이보다 멀어지면 다시 쫓아간다(사이는 히스테리시스).")] public float chaseRange = 9.5f;
        [Tooltip("시전 시작 간격(초).")] public float cooldown = 3.2f;
        [Tooltip("시전 시작 → 구슬이 손을 떠나는 순간(초).")] public float release = 1.2f;
        [Tooltip("시전 동작 길이(초). 이 동안 움직이지 않는다.")] public float castTime = 2.5f;
        public float boltSpeed = 6.5f;
        public float boltDamage = 9f;
    }

    [System.Serializable]
    public class BiteBlock
    {
        [Tooltip("물기 시작 → 피해(초). 클립에서 고개를 올려치는 순간.")] public float delay = 0.3f;
        [Tooltip("물리면 이동속도 배율.")] public float slowMult = 0.55f;
        [Tooltip("감속 지속(초). 다시 물리면 갱신.")] public float slowTime = 1.5f;
    }

    [System.Serializable]
    public class GasBlock
    {
        [Tooltip("끄면 자폭하지 않고 그냥 때린다.")] public bool enabled = true;
        [Tooltip("이 거리 안이면 멈춰서 예고 시작.")] public float triggerRange = 1.1f;
        [Tooltip("예고(부풀며 깜빡임) → 터짐(초).")] public float fuseTime = 0.45f;
        [Tooltip("판정 반경 = 바닥 원 테두리.")] public float radius = 1.8f;
        [Tooltip("터지는 순간 피해.")] public float burstDamage = 10f;
        [Tooltip("구름 안에 있으면 틱마다.")] public float tickDamage = 4f;
        [Tooltip("틱 간격(초). 피격 무적(0.6)보다 길게.")] public float tickInterval = 0.7f;
        [Tooltip("구름 지속(초).")] public float life = 3f;
    }

    /// 실제 몸 반경 — 비워 두면 잡좀비 기준, 덩치 배율까지 곱한 값.
    public float BodyRadius => (bodyRadius > 0f ? bodyRadius : ProtoConfig.BaseBodyRadius) * Mathf.Max(0.01f, scale);
}
