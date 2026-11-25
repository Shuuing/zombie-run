using UnityEngine;

namespace Proto
{
    /// 프롭의 바닥 발자국(수평 회전 사각형) — **메시가 합쳐지기 전에 미리 재 둔 값**.
    ///
    /// 왜 필요한가(2026-10-13, 사용자: "옆으로 이동이 안 된다", 폰에서도 같음):
    /// 정적 배칭은 프롭 메시를 "Combined Mesh (root: scene)" 하나로 합쳐 각 MeshFilter 에 꽂는다.
    /// 폰 빌드는 **빌드할 때** 합쳐 두고, 에디터는 **플레이 도중** 합친다. 그 뒤 메시 바운드로 충돌을 재면
    /// 도시 한 구역만 한 박스가 생겨 캐릭터가 밀려나고 길이 막힌다.
    /// 그래서 씬을 처리하는 단계(배칭보다 먼저 — Editor/FootprintBaker)에서 재서 여기에 적어 둔다.
    /// ProtoObstacles · ProtoDestructibles 는 이 값이 있으면 메시를 보지 않는다.
    public class ProtoFootprint : MonoBehaviour
    {
        public Vector2 center;   // 월드 XZ
        public Vector2 half;     // 축소율(shrink) 적용 전 반 크기
        public float yaw;        // 라디안, 유니티 Euler Y 규약 — half.x 축 = (cos, 0, -sin), half.y 축 = (sin, 0, cos)
        public float height;     // 바닥에서 위까지(m)
    }
}
