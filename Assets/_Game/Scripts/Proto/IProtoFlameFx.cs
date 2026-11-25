using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 화염방사기의 불을 그리는 쪽. 구현이 둘이라 <see cref="ProtoShooter"/> 가
    /// 하나를 골라 쓴다 — <see cref="ProtoFlameCone"/>(파티클)과
    /// <see cref="ProtoFlameSprite"/>(플립북 스프라이트).
    ///
    /// <para><b>판정은 여기 없다.</b> 두 구현 모두 그림만 그린다. 데미지는
    /// <see cref="ProtoShooter"/> 가 콘 수치로 직접 계산하므로, 어느 쪽을 켜도
    /// 게임플레이는 한 글자도 달라지지 않는다 — 그래야 둘을 비교하는 의미가 있다.</para>
    /// </summary>
    public interface IProtoFlameFx
    {
        /// <summary>
        /// 무기 수치가 바뀌었을 때 불을 다시 맞춘다. 같은 값이면 아무것도 하지 않아야 한다.
        /// </summary>
        /// <param name="muzzle">
        /// 불이 나가는 지점. <see cref="ProtoShooter.muzzle"/> 로, <b>투사체가 떠나는 점과 같다</b> —
        /// 무기마다 다른 자리에서 나가면 같은 총을 들고도 총구가 옮겨 다니는 것처럼 보인다.
        /// 비어 있으면 발밑에서 <see cref="ProtoFlameCone"/> 의 기본 높이만큼 띄운다.
        /// </param>
        void Configure(WeaponStats st, Transform muzzle);

        /// 이번 틱에 분사했다. 발사 간격마다 불린다 (연속이 아니라 <b>0.2초에 한 번</b>이다).
        void Pulse();
    }
}
