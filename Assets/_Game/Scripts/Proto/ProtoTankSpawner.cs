using UnityEngine;

namespace Proto
{
    /// 탱크 재등장 예약을 굴리는 빈 오브젝트(프로토 한정). 탱크가 없는 동안에도 시간이 흘러야 해서
    /// 탱크 자신이 아니라 여기서 돈다. 판단은 전부 <see cref="ProtoTank.TickSpawner"/> 에 있다.
    public class ProtoTankSpawner : MonoBehaviour
    {
        void Update() => ProtoTank.TickSpawner();
    }
}
