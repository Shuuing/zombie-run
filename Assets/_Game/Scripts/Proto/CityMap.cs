using UnityEngine;

namespace Proto
{
    /// 도시 테스트 씬의 이동 경계. 플레이어·젬·스폰이 전부 ProtoConfig.ArenaHalf 로 위치를 클램프하는데,
    /// 기본값(19m, 40×40 아레나)이면 도시 한가운데서 막힌다. 씬에 있을 때만 넓히고 나갈 때 되돌린다
    /// (도메인 리로드를 끈 에디터에선 static 이 씬을 넘어 남는다 — ProtoFieldStreamer 와 같은 이유).
    public class CityMap : MonoBehaviour
    {
        [Tooltip("도시 한 변의 절반(m). CityBuilder 가 채운다.")]
        public float halfSize = 78f;
        // 2 로 둔다. 2026-11-10 에 도시 바깥 허공이 덜 비치게 5 로 늘려 봤다가 같은 날 되돌렸다 —
        // 눈에 보이는데 못 가는 바깥 띠가 넓어져 맵이 필요 이상으로 넓어 보였다(사용자).
        // 바깥이 비치는 양은 벽이 아니라 카메라(ProtoCameraFollow.edgeDrift)로만 조절한다.
        [Tooltip("가장자리에서 이만큼 안쪽까지만 다닐 수 있다.")]
        public float edgeMargin = 2f;

        float _backup;

        void Awake()
        {
            _backup = ProtoConfig.ArenaHalf;
            ProtoConfig.ArenaHalf = halfSize - edgeMargin;
        }

        void OnDestroy() { ProtoConfig.ArenaHalf = _backup; }
    }
}
