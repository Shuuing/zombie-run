using UnityEngine;
using Proto;

/// <summary>
/// 적 테이블 — 어떤 적이 있고 <b>언제</b> 나오는가. <c>Resources/Data/EnemyTable.asset</c> 하나.
///
/// <para>적 7종은 칸이 정해져 있다(목록이 아니다). 프로토는 종류가 적고 스폰 디렉터가 종류마다 다른
/// 규칙(원거리 상한, 개 상한, 러너 확률)으로 섞기 때문에, 칸으로 두는 편이 "어느 칸을 비웠나"가 바로 보인다.
/// 종류가 10개를 넘어가면 목록 + 가중치 표로 바꿀 때다.</para>
///
/// <para>일정(보스·습격·엘리트 시각)은 적이 아니라 <b>런의 성질</b>이지만, 프로토는 챕터가 하나라
/// 여기에 같이 둔다. 챕터가 늘면 챕터 에셋으로 분리한다.</para>
/// </summary>
[CreateAssetMenu(menuName = "좀비 오버런/적 테이블", fileName = "EnemyTable")]
public class EnemyTable : ScriptableObject
{
    public const string ResourcePath = "Data/EnemyTable";

    [Header("적 7종")]
    public EnemyDef walker;
    public EnemyDef runner;
    public EnemyDef ranged;
    public EnemyDef dog;
    [Tooltip("워커 움직임 + 큰 덩치. 잡으면 진화 상자.")]
    public EnemyDef elite;
    public EnemyDef midBoss;
    public EnemyDef finalBoss;

    [Header("보스 일정 (초)")]
    [Tooltip("중간 보스 등장.")] public float midBossAt = 150f;
    [Tooltip("최종 보스 등장. 중간 보스가 살아 있으면 죽은 뒤에.")] public float finalBossAt = 240f;
    [Tooltip("중간 보스를 늦게 잡았을 때 최종 보스까지 최소 간격.")] public float finalBossAfterMid = 15f;
    [Tooltip("최종 보스 직전 바닥 젬을 모으는 시간.")] public float finalGatherTime = 1f;

    [Header("대량 습격")]
    [Tooltip("경고 문구를 몇 초 전에 띄우나.")] public float hordeWarnLead = 2f;
    [Tooltip("① 한 방향 돌격 시각.")] public float rushAt = 90f;
    public int rushCount = 53;
    [Tooltip("돌격 러너 속도. 0 이면 러너 기본 속도.")] public float rushSpeed = 3.4f;
    [Tooltip("② 포위 시각.")] public float ringAt = 195f;
    public int ringCount = 54;
    public float ringRadius = 9f;

    [Header("엘리트 일정")]
    public float[] eliteSpawnTimes = { 65f, 205f };
    public float eliteWarnLead = 2f;
    [Tooltip("최종 보스까지 이 초 이하로 남으면 엘리트 바에 'N초 뒤 도망'.")] public float eliteFleeShowAt = 60f;

    /// 움직임 종류로 기본 칸을 고른다(디버그 핫키·강제 스폰용). 엘리트·최종 보스는 칸을 직접 쓴다.
    public EnemyDef Of(ZombieKind k)
    {
        switch (k)
        {
            case ZombieKind.Runner: return runner;
            case ZombieKind.Ranged: return ranged;
            case ZombieKind.Dog:    return dog;
            case ZombieKind.Boss:   return midBoss;
            default:                return walker;
        }
    }

    static EnemyTable _loaded;
    static bool _tried;

    /// Resources 에서 한 번 읽는다. 없으면 null — 부르는 쪽이 스폰을 건너뛴다.
    public static EnemyTable I
    {
        get
        {
            if (_loaded == null && !_tried)
            {
                _tried = true;
                _loaded = Resources.Load<EnemyTable>(ResourcePath);
                if (_loaded == null) Debug.LogError("[적 테이블] Resources/" + ResourcePath + " 이 없다 — 적이 나오지 않는다. Tools/좀비 오버런/데이터 테이블/검증 을 돌릴 것.");
            }
            return _loaded;
        }
    }

    /// 에디터에서 에셋을 새로 만들거나 지웠을 때 다시 읽게 한다.
    public static void Reload() { _loaded = null; _tried = false; }
}
