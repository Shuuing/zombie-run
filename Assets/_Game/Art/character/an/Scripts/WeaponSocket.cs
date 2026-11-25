using UnityEngine;

/// <summary>
/// 캐릭터 손에 무기를 매다는 소켓.
///
/// <para><b>왜 빈 오브젝트를 한 겹 두는가.</b> 무기를 손 본에 직접 붙이면 본의 스케일을 그대로
/// 상속받는다. 이 캐릭터의 손 본은 월드 스케일이 <b>0.0996</b> 이라(블렌더 아마추어 축소가
/// FBX 를 거치며 본에 남았다) 무기가 10분의 1로 줄어든다. 소켓이 그 보정을 한 곳에서 흡수하고,
/// 무기 프리팹 자체는 스케일 1 로 제작할 수 있다.</para>
///
/// <para>보정값은 <see cref="autoCompensate"/> 가 <b>런타임에 실측</b>한다 — 캐릭터를 다시
/// 익스포트해 본 스케일이 달라져도 숫자를 손으로 고칠 필요가 없다.</para>
///
/// <para><b>어떤 무기를 어떻게 쥐는지는 <see cref="WeaponModelDef"/> 에셋이 들고 있다.</b>
/// 무기가 5종이 되면서 그 값이 캐릭터 프리팹에 있으면 안 되기 때문이다(에셋 주석 참조).
/// <see cref="weapon"/> 이 비어 있으면 아래 legacy 필드로 동작한다 — 소총 1종 시절 프리팹이
/// 그대로 돌아가야 해서 남겨둔 경로다.</para>
/// </summary>
public class WeaponSocket : MonoBehaviour
{
    [Tooltip("이 캐릭터가 드는 무기. 비우면 아래 legacy 필드를 쓴다.")]
    public WeaponModelDef weapon;

    [Tooltip("무기가 붙을 손. 보통 오른손이다.")]
    public HumanBodyBones hand = HumanBodyBones.RightHand;

    [Tooltip("손 본 스케일을 실측해 자동으로 상쇄한다. 끄면 소켓 스케일을 직접 관리해야 한다.")]
    public bool autoCompensate = true;

    [Tooltip("손 기준 오프셋. 소켓 자체를 손에서 옮기고 싶을 때 쓴다.")]
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localEuler = Vector3.zero;

    [Header("Legacy — weapon 이 비었을 때만 쓰인다")]
    [Tooltip("무기 프리팹. WeaponModelDef 로 옮기는 것이 맞다.")]
    public GameObject weaponPrefab;

    // 0.1 은 총 길이를 키의 48% 로 맞추는 값이고, 0.90277 은 사용자가 씬에서 눈으로 줄인 몫이다.
    public float weaponScale = 0.090277f;

    // 사용자가 씬에서 직접 맞춘 배치에서 역산한 값이다(왕복 오차 0). 내가 지오메트리로 추정한
    // (1.20, -0.37, -0.28) 은 손잡이를 잘못 짚었다 — 아래로 뻗은 세 덩어리 중 가장 뒤를 골랐는데
    // 실제 그립은 그보다 앞쪽이었다. 추정치는 남기지 않는다.
    public Vector3 gripPoint = new Vector3(0.3190f, -0.5278f, -0.0440f);

    // 축만 보면 Y 180도로 총구가 정면을 보지만, 손이 총을 쥐는 각도는 그것만으로 안 나온다 —
    // 손목이 꺾인 채 총을 비스듬히 잡기 때문에 세 축이 모두 필요하다.
    public Vector3 weaponEuler = new Vector3(2.27f, -104.188f, -90.576f);

    GameObject _weapon;
    Transform _hand;

    void Awake()
    {
        if (!AttachToHand()) return;
        Equip(weapon);
    }

    bool AttachToHand()
    {
        var animator = GetComponentInParent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[WeaponSocket] 부모에 Animator 가 없습니다.");
            return false;
        }

        _hand = animator.GetBoneTransform(hand);
        if (_hand == null)
        {
            Debug.LogError("[WeaponSocket] " + hand + " 본을 찾지 못했습니다. 리그가 Humanoid 인지 확인하세요.");
            return false;
        }

        transform.SetParent(_hand, false);
        transform.localPosition = localPosition;
        transform.localEulerAngles = localEuler;
        return true;
    }

    /// <summary>
    /// 무기를 갈아끼운다. 스쿼드 순차 교체(기획서 §04)가 이 함수를 캐릭터마다 부른다.
    /// null 을 넘기면 legacy 필드로 동작한다.
    /// </summary>
    public void Equip(WeaponModelDef def)
    {
        weapon = def;

        // 손에 쥐지 않는 무기(회전 블레이드·지원 드론)는 소켓이 관여하지 않는다.
        // legacy fallback 까지 건너뛰어야 한다 — 안 그러면 리퍼가 소총을 든다.
        if (def != null && def.mount != WeaponMount.Hand)
        {
            if (_weapon != null && _weapon.transform.parent == transform) Destroy(_weapon);
            _weapon = null;
            return;
        }

        var prefab = def != null ? def.model : weaponPrefab;
        float scale = def != null ? def.weaponScale : weaponScale;

        // 손 본이 이미 축소돼 있으므로 그 역수를 소켓에 준다. 그러면 자식 무기는 '캐릭터 배율 × scale' 로 보인다.
        // 캐릭터 루트 배율은 남긴다 — 씬에 처음부터 1.598배로 놓인 캐릭터(Player/Visual)는 Awake 때 이미 커져 있어,
        // 손 스케일만 보고 상쇄하면 그 1.598배까지 지워져 총이 작게 나왔다(교체로 생긴 캐릭터는 Awake 뒤에 커져서 정상).
        var animator = GetComponentInParent<Animator>();
        transform.localScale = Vector3.one * CompensationFor(_hand, autoCompensate, animator != null ? animator.transform : null) * scale;

        // 보이던 무기를 갈아끼우면(무기 진화) 새 무기도 보인 채로 둔다. 숨김은 "다음 상태 진입"에서야 풀리는데,
        // 쏘는 동작이 없는 무기(융단 폭격 무전기)는 가만히 서 있는 동안 영영 안 보였다.
        bool wasShown = _weapon != null && _weapon.activeSelf;
        if (_weapon != null && _weapon.transform.parent == transform) Destroy(_weapon);
        _weapon = null;

        if (prefab != null)
            _weapon = Instantiate(prefab, transform);
        else if (transform.childCount > 0)
            _weapon = transform.GetChild(0).gameObject;   // 이미 자식으로 넣어둔 것을 그대로 쓴다.

        if (_weapon != null)
            ApplyPlacement(_weapon.transform,
                           def != null ? def.gripPoint : gripPoint,
                           def != null ? def.weaponEuler : weaponEuler);

        Show(wasShown);   // 기본은 숨김(처음 장착). 총이 필요한 상태가 켠다.
    }

    /// <summary>
    /// 손 본 스케일의 역수 × 캐릭터 루트 배율. 에디터 툴도 같은 값을 써야 하므로 밖으로 뺐다.
    /// <paramref name="characterRoot"/>(Animator 가 붙은 Visual)의 배율은 상쇄하지 않고 남긴다 — 캐릭터를 키우면 무기도 같이 커진다.
    /// </summary>
    public static float CompensationFor(Transform handBone, bool enabled, Transform characterRoot = null)
    {
        if (!enabled || handBone == null) return 1f;
        float s = handBone.lossyScale.x;
        float root = characterRoot != null ? characterRoot.lossyScale.x : 1f;
        return s > 0.0001f ? root / s : 1f;
    }

    /// <summary>
    /// 무기를 소켓 안에 배치한다. 무기를 gripPoint 만큼 반대로 밀면 그 점이 소켓 원점(=손)에 온다.
    /// 회전을 먼저 적용해야 밀어내는 방향도 함께 돈다.
    /// <b>모델 루트의 자체 스케일은 건드리지 않는다</b> — FBX 에 구워진 스케일(화염방사기 1.37 등)을
    /// 1 로 덮으면 아티스트가 잡아둔 무기별 크기 비율이 통째로 사라진다.
    /// </summary>
    public static void ApplyPlacement(Transform weapon, Vector3 gripPoint, Vector3 weaponEuler)
    {
        weapon.localRotation = Quaternion.Euler(weaponEuler);
        weapon.localPosition = weapon.localRotation * (-gripPoint);
    }

    /// <summary>
    /// <see cref="ApplyPlacement"/> 의 역함수. 씬에서 손으로 맞춘 배치에서 그립 값을 뽑는다.
    /// <paramref name="scaleRatio"/> 는 무기 루트를 원본 스케일에서 얼마나 키웠는지다(보통 1).
    /// </summary>
    public static void ReadPlacement(Transform weapon, float scaleRatio, out Vector3 gripPoint, out Vector3 weaponEuler)
    {
        var rot = weapon.localRotation;
        weaponEuler = rot.eulerAngles;
        if (scaleRatio < 0.0001f) scaleRatio = 1f;
        gripPoint = Quaternion.Inverse(rot) * (-weapon.localPosition / scaleRatio);
    }

    /// <summary>무기 보이기/숨기기. 애니메이션과 무관하게 언제든 호출할 수 있다.</summary>
    public void Show(bool on)
    {
        if (_weapon != null) _weapon.SetActive(on);
    }

    public bool IsShown => _weapon != null && _weapon.activeSelf;
}
