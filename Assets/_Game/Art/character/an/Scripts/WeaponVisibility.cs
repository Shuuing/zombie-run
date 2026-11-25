using UnityEngine;

/// <summary>
/// Animator 상태에 붙여 그 상태 동안 무기를 켜고 끄고, 왼손 그립 보정 레이어의 세기를 정한다.
///
/// <para><b>왜 클립이 아니라 상태에 붙이는가.</b> 애니메이션 클립은 본만 움직일 뿐 무기의
/// 존재를 모른다. 그래서 "이 동작에는 총이 있다"는 정보는 클립이 아니라 <b>상태</b>에 있어야
/// 하고, 클립을 갈아끼워도 설정이 살아남는다.</para>
///
/// <para>Animator 창에서 상태를 고르고 Add Behaviour → WeaponVisibility, 총이 필요한 상태는
/// <see cref="showWeapon"/> 체크. 총이 없는 상태는 체크 해제.</para>
///
/// <para>맨손 클립에서 총을 켜면 펴진 손 위에 총이 떠 있게 된다 — 반대(총 자세인데 총이 없음)는
/// 잘 보이지 않으므로, 애매하면 끄는 쪽이 안전하다.</para>
/// </summary>
public class WeaponVisibility : StateMachineBehaviour
{
    [Tooltip("이 상태에서 무기를 보이게 할지")]
    public bool showWeapon = true;

    [Header("왼손 그립 보정")]
    // Mixamo 클립 일부는 왼손 손가락 머슬이 망가져 있다. Rifle Idle 이 그렇다 —
    // 손가락이 접히기는커녕(Stretched +1.0 = 완전히 펴짐) Spread 가 8.59 / -10.98 로
    // 정상 범위 ±1 을 한참 벗어나, 곧게 뻗은 손가락이 부채처럼 벌어진 모양이 된다.
    // 클립을 고치면 새 클립을 받을 때마다 반복이므로, 마스크 레이어로 왼손 손가락만 덮어쓴다.
    [Tooltip("이 상태에서 왼손 손가락을 그립 포즈로 덮어쓸지. 클립의 왼손 손가락이 망가진 상태에만 켠다.")]
    public bool leftHandGrip = false;

    [Tooltip("그립 포즈가 든 오버라이드 레이어 이름. 컨트롤러의 레이어 이름과 같아야 한다.")]
    public string gripLayerName = "LeftHandGrip";

    [Tooltip("레이어 세기가 0↔1 로 가는 데 걸리는 시간. 0 이면 즉시 전환해 톡 튄다.")]
    public float gripBlendTime = 0.15f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var socket = animator.GetComponentInChildren<WeaponSocket>(true);
        if (socket != null) socket.Show(showWeapon);
    }

    // 세기를 OnStateEnter 에서 한 번에 넣지 않고 매 프레임 밀어준다. 한 번에 넣으면 손가락이
    // 톡 튀고, 무엇보다 이 클래스는 Animator 마다 복제되므로 진행 상태를 필드에 들고 있으면
    // 안 된다 — 현재 세기를 Animator 에서 직접 읽어 목표로 밀면 그 문제가 없다.
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        int idx = animator.GetLayerIndex(gripLayerName);
        if (idx < 0) return;   // 레이어가 없는 컨트롤러에서도 그냥 무시하고 지나간다.

        float target = leftHandGrip ? 1f : 0f;
        float speed = gripBlendTime > 0.0001f ? Time.deltaTime / gripBlendTime : 1f;
        animator.SetLayerWeight(idx, Mathf.MoveTowards(animator.GetLayerWeight(idx), target, speed));
    }
}
