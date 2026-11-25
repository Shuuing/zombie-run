using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 원작에서 가져온 애니메이션의 이벤트(<c>Listen</c>, 원작 AnimatorListen 이 받던 것)를 받아 버리는 빈 수신기.
    /// 받는 곳이 없으면 이벤트가 불릴 때마다 "AnimationEvent 'Listen' … has no receiver!" 오류가 찍힌다.
    /// <see cref="ProtoSkillSelectPopup"/> 가 애니메이터마다 붙인다.
    /// </summary>
    public class ProtoAnimEventSink : MonoBehaviour
    {
        public void Listen(string arg) { }
        public void AnimationEnd() { }   // 원작 결과 화면 클립(BattleResultMainShowReward)의 이벤트
    }
}
