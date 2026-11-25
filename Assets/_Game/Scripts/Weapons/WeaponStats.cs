using UnityEngine;

/// <summary>주무기가 화력을 내는 방식. 수치만으로는 갈라지지 않는 것들이다.</summary>
public enum FireMode
{
    /// <summary>단일 투사체 (탕탕 소총, 탕탕 캐논).</summary>
    Single,
    /// <summary>부채꼴로 펠릿 여러 발 (산탄총).</summary>
    Shotgun,
    /// <summary>단일 투사체 + 착탄 지점 폭발 (로켓 발사기).</summary>
    Rocket,
    /// <summary>투사체 없이 전방 부채꼴을 계속 지지고 화상을 남긴다 (화염방사기).</summary>
    Cone,
    // 아래 둘은 <b>보조 장비 전용</b>이다 — 주무기가 아니라 레벨업 카드로 얻는다(§07).
    // 손에 쥐지도 조준하지도 않아서 나머지 넷과 조작 감각이 다른 축에 있고,
    // 그래서 캐릭터에 고정하지 않고 누구나 카드로 붙일 수 있게 했다.
    /// <summary>몸 주위를 도는 블레이드. <b>조준하지 않는다</b> (회전 블레이드).</summary>
    Orbit,
    /// <summary>따라다니는 소환물이 독립적으로 조준해 쏜다 (지원 드론).</summary>
    Companion,
    /// <summary>직접 쏘지 않고 헬기를 불러 화면 여러 지점을 폭격한다 (융단 폭격 무전기 — 로켓 발사기 진화).
    /// damage = 지점당 피해, interval = 회차 간격. 지점 수·반경은 <c>ProtoAirstrike</c> 가 정한다.</summary>
    Airstrike,
    /// <summary>보조 장비 — 좀비가 가장 많이 모인 곳에 수류탄을 던진다 (2026-10-28).
    /// damage = 폭발 1회 피해, interval = 투척 간격, range = 던지는 거리, aoeRadius = 폭발 반경,
    /// levelCounts = 한 번에 던지는 개수.</summary>
    Grenade,
    /// <summary>보조 장비 — 쿨타임마다 공격 헬기를 불러 기총 소사시킨다 (헬리콥터 공습, 2026-10-28).
    /// 바닥 경고 띠 → 헬기가 띠를 따라 낮게 지나가며 양 날개 기관포로 쏜다.
    /// damage = 기관포 한 발 피해, interval = 쿨타임(공속과 무관), range = 이 안에 좀비가 있어야 부른다,
    /// levelCounts = 한 번 부를 때 지나가는 횟수. 연사·띠·연출은 <c>ProtoHeliGunship</c>.</summary>
    HeliCall,
}

/// <summary>
/// 주무기 1종의 <b>전투 수치</b>. 기획서 §05 표가 그대로 들어온다.
///
/// <para><b>왜 <see cref="WeaponModelDef"/> 와 다른 에셋인가.</b> 모델과 수치는 바뀌는 이유와
/// 주기가 다르다. 아트가 FBX 를 다시 뽑는 일과 밸런스가 피해량을 만지는 일이 한 파일에서 만나면
/// 서로의 변경을 덮어쓴다. 참조로만 묶는다(`WeaponModelDef.stats`).</para>
///
/// <para><b>DPS 는 필드가 아니다.</b> <see cref="Dps"/> 로 계산한다 — 표에 적어두면 피해량을 고칠 때
/// 반드시 한쪽만 고쳐져서, 기획서와 대조할 때 어느 쪽이 진짜인지 알 수 없게 된다.</para>
/// </summary>
[CreateAssetMenu(menuName = "좀비 오버런/무기 수치", fileName = "WS_NewWeapon")]
public class WeaponStats : ScriptableObject
{
    [Header("공통 (§05)")]
    public FireMode mode = FireMode.Single;

    [Tooltip("1회 판정당 피해. 산탄은 펠릿 1발, 화염·블레이드는 1틱 값이다.")]
    public float damage = 12f;

    [Tooltip("발사(또는 틱) 간격 초. 공속 계수로 나뉜다.")]
    public float interval = 0.35f;

    [Tooltip("사거리 m. 타깃 탐색 반경이기도 하다.")]
    public float range = 9f;

    [Tooltip("이 무기의 넉백 배율. 산탄총은 '넉백 강'이라 1보다 크다.")]
    public float knockbackMult = 1f;

    [Header("투사체 (Single · Shotgun · Rocket)")]
    public float projectileSpeed = 24f;
    public float projectileRadius = 0.45f;
    public float projectileLife = 1.2f;
    [Tooltip("눈에 보이는 크기. 로켓은 크게, 펠릿은 작게.")]
    public float projectileSize = 0.22f;

    // 발사체 모델은 **길이 1 단위**로 만든다(손에 드는 무기의 5 단위 규약과 다르다).
    // 그러면 위 projectileSize 가 그대로 인게임 길이가 되어 숫자 하나로 읽힌다.
    // 코는 +Z 로 만든다 — Quaternion.LookRotation 이 +Z 를 겨누므로 보정이 필요 없다.
    [Tooltip("발사체 모델. 비우면 기본 노란 구슬이 날아간다.")]
    public GameObject projectileModel;

    [Tooltip("모델이 진행 방향을 향하게 회전시킨다. 구슬처럼 방향이 없는 것은 꺼도 된다.")]
    public bool projectileFacesTravel = true;
    [Tooltip("추가로 관통할 적 수. 0 이면 첫 명중에서 사라진다.")]
    public int pierce = 0;

    // 진화 무기가 같은 노란 예광탄을 쏘면 "무기는 바뀌었는데 총알은 그대로"로 보인다(2026-10-21 사용자).
    // 모델이 없는 기본 예광탄에만 쓴다. 알파 0 = 기본 노랑 그대로라 기존 에셋은 손대지 않아도 된다.
    [Tooltip("기본 예광탄 색. 알파 0 이면 기본 노랑. 1 을 넘기면 밤에 더 밝게 번진다.")]
    [ColorUsage(true, true)] public Color tracerColor = new Color(1f, 1f, 1f, 0f);
    [Tooltip("기본 예광탄 꼬리 길이 배율. 1 이 기본.")]
    public float tracerLength = 1f;
    [Tooltip("기본 예광탄 굵기 배율. 1 이 기본(길이는 그대로 두고 폭만).")]
    public float tracerWidth = 1f;

    // 슬러그 레일(2026-10-21 사용자): 날아가는 탄이 아니라 쏘는 순간 사거리 끝까지 그어지는 광선.
    [Tooltip("켜면 투사체 대신 즉발 광선(Single 전용). 사거리 끝까지 직선 위의 적을 가까운 순으로 pierce+1 마리까지 동시에 맞춘다. 판정 폭은 projectileRadius, 색은 tracerColor.")]
    public bool beam = false;

    [Header("Shotgun")]
    [Tooltip("한 번에 나가는 펠릿 수")]
    public int pellets = 5;

    // 기본값이 0 인 이유: Single 무기도 이 값을 쓴다(탕탕 캐논의 스프레드 8°).
    // 기본을 45 로 두면 새로 만든 소총류가 조용히 부채꼴로 흩어진다.
    [Tooltip("Shotgun 은 펠릿을 이 각도에 균등 배치하고, Single 은 이 범위 안에서 무작위로 흩뿌린다. 0 이면 정확히 직선.")]
    public float spreadDegrees = 0f;

    [Header("Rocket")]
    [Tooltip("착탄 폭발 반경 m. 0 이면 폭발하지 않는다.")]
    public float aoeRadius = 0f;
    [Tooltip("켜면 착탄 폭발이 화면을 흔들지 않는다 (탱크 포).")]
    public bool noShake = false;

    // 데몰리셔 진화(2026-10-28 사용자): 폭격만 계속 나오고 캐릭터가 안 쏘는 게 이상하다 —
    // 로켓은 그대로 쏘고, 폭격은 이 간격마다 한 회차씩 따로 부른다.
    [Tooltip("0 보다 크면 이 간격(초)마다 융단 폭격 한 회차를 부른다(사거리 안에 적이 있을 때). 공속과 무관.")]
    public float airstrikeEvery = 0f;
    [Tooltip("폭격 지점당 피해. 지점 수·반경은 ProtoAirstrike.")]
    public float airstrikeDamage = 260f;

    [Header("Cone (화염방사기)")]
    [Tooltip("부채꼴 전체 각도")]
    public float coneDegrees = 50f;
    [Tooltip("화상 지속 초. 0 이면 화상이 없다.")]
    public float burnDuration = 0f;
    [Tooltip("화상 초당 피해")]
    public float burnDps = 0f;
    [Tooltip("불 색 곱. 흰색이면 기본 주황 불. 진화 무기(인페르노)는 푸르게 — 더 뜨거운 불로 읽힌다.")]
    [ColorUsage(false, true)] public Color flameTint = Color.white;

    [Header("Cone 연료 (파이로)")]
    // **이 게임은 사격이 완전 자동이다**(§03). 플레이어가 방아쇠를 놓을 수 없으므로,
    // 연료를 아끼는 유일한 수단은 **적에게서 떨어지는 것**이다. 즉 이 수치들은
    // "얼마나 오래 붙어 있을 수 있는가 / 얼마나 자주 물러나야 하는가" 를 정한다.
    // 파이로에게만 '붙었다 떨어졌다' 리듬을 강제하는 장치다.
    [Tooltip("연속 분사할 수 있는 시간 초. 0 이면 연료 제한이 없다(다른 무기는 전부 0).")]
    public float fuelSeconds = 0f;

    [Tooltip("연료가 바닥나면 강제로 쉬는 시간 초. 이 시간이 끝나면 가득 찬다.")]
    public float refuelSeconds = 2f;

    // 회복이 없으면 교전이 이어질 때 연료가 조금씩 줄다가 엉뚱한 순간에 잠긴다.
    // 회복을 두면 "잠깐 물러나면 다시 지질 수 있다" 가 되어 위치 선정이 실력이 된다.
    [Tooltip("분사하지 않는 동안 초당 회복되는 연료(초). 0 이면 바닥날 때까지 회복이 없다.")]
    public float fuelRegenPerSecond = 0.8f;

    /// <summary>연료 제한이 있는 무기인가.</summary>
    public bool HasFuel => mode == FireMode.Cone && fuelSeconds > 0.01f;

    [Header("Orbit (회전 블레이드)")]
    [Tooltip("궤도 반경 m")]
    public float orbitRadius = 2.5f;
    [Tooltip("궤도에 도는 블레이드 수. DPS = damage / interval × 이 수.")]
    public int orbitCount = 2;
    [Tooltip("분당 회전수")]
    public float orbitRpm = 42f;
    [Tooltip("블레이드가 적을 때리는 반경 m")]
    public float orbitHitRadius = 0.55f;
    [Tooltip("궤도 높이 m")]
    public float orbitHeight = 0.8f;

    [Header("Companion (지원 드론)")]
    [Tooltip("플레이어 기준 소환물 위치 (로컬)")]
    public Vector3 companionOffset = new Vector3(0.85f, 1.6f, -0.5f);
    [Tooltip("소환물이 자리를 따라잡는 속도. 낮으면 뒤늦게 따라온다.")]
    public float companionFollowLerp = 6f;

    [Header("모델 축 보정 (Orbit · Companion)")]
    // 모델이 어느 축을 향해 만들어졌는지는 아트가 정하고, 우리는 그것을 데이터로 흡수한다.
    // 실측: 드론은 코가 +X 인데 Quaternion.LookRotation 은 +Z 를 겨누므로 Y −90.
    //       블레이드 디스크는 법선이 +Z(세워진 상태)라 X 90 으로 눕혀야 수평이 된다.
    // 코드에 숫자를 박으면 모델을 다시 뽑을 때 반드시 어긋난다.
    [Tooltip("모델을 제 방향으로 돌리는 보정 각. 목표 회전에 곱해진다.")]
    public Vector3 modelRotationOffset = Vector3.zero;

    [Header("보조 장비 레벨 성장 (Orbit · Companion)")]
    // 보조 장비는 카드로 레벨이 오른다. 성장을 수식이 아니라 표로 두는 이유는,
    // "3레벨에 블레이드가 3개가 되나?" 를 코드를 읽지 않고 답할 수 있어야 하기 때문이다.
    // 길이는 최대 레벨과 같아야 하고, 모자라면 마지막 값을 계속 쓴다.
    [Tooltip("레벨별 개수 (궤도 블레이드 수 / 드론 수). 인덱스 0 = Lv1.")]
    public int[] levelCounts = { 1, 2, 3, 3, 4 };

    [Tooltip("레벨별 피해 배율. 인덱스 0 = Lv1.")]
    public float[] levelDamageMult = { 1f, 1f, 1f, 1.5f, 1.5f };

    [Tooltip("레벨별 폭발 반경 배율 (Grenade). 인덱스 0 = Lv1.")]
    public float[] levelRadiusMult = { 1f, 1f, 1f, 1f, 1f };

    [Header("연출")]
    // 소리 키와 같은 이유로 데이터에 둔다 — 어느 효과를 얼마나 크게 낼지는 무기의 성질이고, 아트가 코드를 열지 않고 바꿔야 한다.
    // 예전에는 ProtoShooter 가 "캐논이면 1.35, 샷건이면 1.25" 를 코드로 갈랐다.
    [Tooltip("이 무기의 연출 세트(총구·탄피·피격·처치·폭발). 비우면 기본 세트(공통 효과, 배율 1). 'FX 툴/카탈로그·시트 만들기' 가 빠진 무기에 하나씩 만들어 물린다.")]
    public WeaponFxSet fx;

    [Header("소리")]
    // 발사 소리를 코드가 아니라 데이터로 정한다. 모델 축 보정(modelRotationOffset)과 같은 이유다:
    // 탕탕 소총과 탕탕 캐논은 둘 다 FireMode.Single 이라 모드만 보면 구분되지 않는다.
    // 에셋 이름으로 분기하면 파일을 다시 뽑는 순간 조용히 소리가 바뀐다.
    [Tooltip("Resources/SFX 의 키. 비우면 발사 방식에 맞는 기본 소리가 난다. 예: cannon")]
    public string sfxKey;

    [TextArea(2, 4)]
    [Tooltip("기획서 §05 '특성' 칸. 구현이 이 설명과 어긋나면 둘 중 하나가 틀린 것이다.")]
    public string note;

    /// <summary>
    /// 발사 소리 키. <see cref="sfxKey"/> 가 비어 있으면 발사 방식으로 고른다 —
    /// 새 무기를 만들면 아무것도 적지 않아도 소리가 나고, 특별한 소리가 필요할 때만 적는다.
    /// </summary>
    public string SfxKey
    {
        get
        {
            if (!string.IsNullOrEmpty(sfxKey)) return sfxKey;
            switch (mode)
            {
                case FireMode.Shotgun:   return "shotgun";
                case FireMode.Rocket:    return "rocket_fire";
                case FireMode.Cone:      return "flame_loop";
                case FireMode.Orbit:     return "blade";
                case FireMode.Companion: return "drone";
                default:                 return "rifle";
            }
        }
    }

    /// <summary>보조 장비 레벨(1부터)에 해당하는 개수. 표가 짧으면 마지막 값을 쓴다.</summary>
    public int CountAtLevel(int level)
    {
        if (levelCounts == null || levelCounts.Length == 0) return Mathf.Max(1, orbitCount);
        return levelCounts[Mathf.Clamp(level - 1, 0, levelCounts.Length - 1)];
    }

    /// <summary>보조 장비 레벨(1부터)에 해당하는 피해 배율.</summary>
    public float DamageMultAtLevel(int level)
    {
        if (levelDamageMult == null || levelDamageMult.Length == 0) return 1f;
        return levelDamageMult[Mathf.Clamp(level - 1, 0, levelDamageMult.Length - 1)];
    }

    /// <summary>보조 장비 레벨(1부터)에 해당하는 폭발 반경 배율.</summary>
    public float RadiusMultAtLevel(int level)
    {
        if (levelRadiusMult == null || levelRadiusMult.Length == 0) return 1f;
        return levelRadiusMult[Mathf.Clamp(level - 1, 0, levelRadiusMult.Length - 1)];
    }

    /// <summary>보조 장비인가 (카드로 얻는 자동 공격 장치).</summary>
    public bool IsSupportGear => mode == FireMode.Orbit || mode == FireMode.Companion || mode == FireMode.Grenade || mode == FireMode.HeliCall;

    /// <summary>기획서 DPS 칸과 대조할 값. 궤도형은 블레이드 수를 곱한다.</summary>
    public float Dps
    {
        get
        {
            if (interval <= 0.0001f) return 0f;
            float perShot = damage * (mode == FireMode.Shotgun ? Mathf.Max(1, pellets) : 1);
            if (mode == FireMode.Orbit) perShot = damage * Mathf.Max(1, orbitCount);
            return perShot / interval;
        }
    }

    /// <summary>플레이어가 적을 향해 몸을 돌리는 무기인지. 궤도·소환물은 조준하지 않는다(§05).</summary>
    public bool AimsAtTarget => mode != FireMode.Orbit && mode != FireMode.Companion && mode != FireMode.Airstrike && mode != FireMode.Grenade && mode != FireMode.HeliCall;
}
