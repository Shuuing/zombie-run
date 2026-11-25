using UnityEngine;

/// <summary>무기가 캐릭터에 붙는 방식. 손에 쥐지 않는 무기가 있어서 필요하다.</summary>
public enum WeaponMount
{
    /// <summary>손에 쥔다. 소켓이 그립 좌표로 배치한다.</summary>
    Hand,
    /// <summary>몸 주위를 돈다(회전 블레이드). 궤도는 런타임이 정한다.</summary>
    Orbit,
    /// <summary>따라다닌다(지원 드론). 위치는 런타임이 정한다.</summary>
    Companion,
}

/// <summary>
/// 무기 하나의 <b>외형과 손에 물리는 방법</b>을 담는 에셋.
///
/// <para><b>왜 컴포넌트가 아니라 에셋인가.</b> 예전에는 그립 값이 캐릭터 프리팹의
/// <see cref="WeaponSocket"/> 인스펙터에 박혀 있었다. 무기가 소총 1종일 때는 그게 가장 짧은 길이였지만,
/// 무기 5종 × 캐릭터 6종이 되면 같은 숫자를 30곳에 복사해 두는 꼴이 된다 — 무기 모델을 다시 뽑는 순간
/// 30곳을 다 고쳐야 한다. 그립은 <b>무기의 성질</b>이지 캐릭터의 성질이 아니므로 무기 쪽에 둔다.</para>
///
/// <para>여기에는 <b>외형만</b> 담는다. 피해·발사 간격·사거리 같은 전투 수치는 별도 에셋으로 간다
/// (기획서 §05). 둘을 한 에셋에 합치면 아트가 모델을 교체할 때마다 밸런스 에셋이 함께 흔들린다.</para>
/// </summary>
[CreateAssetMenu(menuName = "좀비 오버런/무기 모델", fileName = "W_NewWeapon")]
public class WeaponModelDef : ScriptableObject
{
    [Tooltip("기획서에 쓰는 이름. 로그와 툴 목록에만 쓴다.")]
    public string displayName;

    [Tooltip("무기 모델. FBX 를 그대로 넣어도 되고 프리팹이어도 된다.")]
    public GameObject model;

    // 수치는 참조로만 물린다. 같은 파일에 넣으면 아트가 모델을 갈 때와 밸런스가 피해량을 만질 때가
    // 한 에셋에서 만나 서로를 덮어쓴다 (WeaponStats 주석 참조).
    [Tooltip("이 무기의 전투 수치(§05). 비우면 ProtoConfig 전역값으로 떨어진다.")]
    public WeaponStats stats;

    // 이 값이 Hand 가 아니면 아래 gripPoint·weaponEuler·calibrated 는 의미가 없다.
    // 소켓이 손에 물리지 않으므로 그 좌표를 채워도 아무 데도 쓰이지 않는다.
    [Tooltip("붙는 방식. Hand 만 손 소켓에 물린다.")]
    public WeaponMount mount = WeaponMount.Hand;

    // 아티스트가 5종을 같은 단위계(길이 약 5)로 만들어 왔으므로 배율은 전 무기 공통값이 기본이다.
    // 무기마다 다른 값을 넣으면 아티스트가 잡아둔 상대 크기(로켓은 길고 샷건은 짧다)가 무너진다.
    [Tooltip("손에 물렸을 때의 최종 크기 배율. 손 본 스케일 보정과 곱해진다.")]
    public float weaponScale = 0.090277f;

    // <b>소켓 공간</b>의 좌표다 — 모델 루트의 자체 스케일(FBX 에 구워진 1.37 같은 값)이 이미 곱해진 뒤의 값이다.
    // 이 점이 손 위치로 오도록 무기를 반대로 민다.
    [Tooltip("손이 잡는 지점(손잡이). 소켓 공간 좌표. '무기 그립 맞추기' 툴이 씬 배치에서 역산해 채운다.")]
    public Vector3 gripPoint;

    [Tooltip("무기 자체의 회전 보정. 총구가 캐릭터 정면을 보게 맞춘다.")]
    public Vector3 weaponEuler;

    // 총알·화염·미사일이 나가는 점. 예전에는 런타임이 메시 바운드의 '손에서 먼 끝 면의 중심'으로 추정했는데,
    // 그 점은 총구 구멍이 아니라 상자 면 한가운데라 무기에 따라 총열 아래·위로 어긋났다.
    [Tooltip("총구 지점을 저장했는지. 끄면 런타임이 메시 바운드로 추정한다.")]
    public bool hasMuzzle;

    [Tooltip("총구(발사 지점). 무기 모델 루트의 로컬 좌표. '무기 그립 맞추기' 툴의 빨간 마커로 맞춘다.")]
    public Vector3 muzzlePoint;

    // 눈으로 맞추지 않은 값을 '맞춰진 값'처럼 보이게 두면 나중에 아무도 구분하지 못한다.
    // 지난번에 내가 지오메트리로 손잡이를 추정했다가 엉뚱한 덩어리를 짚은 적이 있어 이 플래그를 둔다.
    [Tooltip("씬에서 눈으로 맞춰 저장한 값인지. 꺼져 있으면 아직 추정/기본값이다.")]
    public bool calibrated;

    [TextArea(2, 5)]
    [Tooltip("메모. 왜 이 값인지, 어떤 자세에서 맞췄는지.")]
    public string note;
}
