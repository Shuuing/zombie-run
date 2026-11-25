using UnityEngine;

namespace Proto
{
    /// 입 메시 UV 가 입모양 시트(Character_Mouth.png, 8×8)의 **몇 번 칸**을 덮고 있는지 — 입 렌더러에 붙는다.
    ///
    /// 왜 미리 적어 두나: 입 메시는 읽기 불가(isReadable=false)라 빌드에서는 UV 를 못 읽는다.
    /// 캐릭터마다 기본 칸이 다르다(Breaker 는 4열 3행, 나머지는 0열 0행) — ProtoMouthFace 가 이 칸을 기준으로 오프셋을 잰다.
    /// 값은 Tools/좀비 오버런/입모양 기준 칸 굽기 가 메시 UV 로 재서 넣는다.
    public class ProtoMouthCell : MonoBehaviour
    {
        [Tooltip("열(왼쪽 0) · 행(위쪽 0)")]
        public Vector2Int cell;
    }
}
