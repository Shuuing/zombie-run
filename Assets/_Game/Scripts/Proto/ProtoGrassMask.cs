using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 풀 자리 지도 — 0.5m 칸마다 풀 밀도(0~255). 풀잎을 구울 때(CityGrassBaker) 같이 만든다.
    /// 런타임에는 풀잎 메시만 있어서 "여기가 풀밭인가"를 알 길이 없다 — 풀 조각 튀기(<see cref="ProtoGrassKick"/>)가 이걸 읽는다.
    /// 잔디 = 1, 길가 잡초 = 잡초 밀도 비율(약 0.45), 풀 없음 = 0.
    /// </summary>
    public class ProtoGrassMask : ScriptableObject
    {
        public float half;       // 도시 반폭(m) — 지도는 (-half, -half) 에서 시작한다
        public float cell = 0.5f;
        public int   n;
        public byte[] density;
        public byte[] lawn;      // 1 = 잔디(초록), 0 = 길가 잡초(마른 색) — 튀는 조각 색을 고른다

        public float DensityAt(Vector3 p, out bool isLawn)
        {
            isLawn = false;
            if (density == null || n <= 0) return 0f;
            int ix = Mathf.FloorToInt((p.x + half) / cell), iz = Mathf.FloorToInt((p.z + half) / cell);
            if (ix < 0 || iz < 0 || ix >= n || iz >= n) return 0f;
            int i = iz * n + ix;
            isLawn = lawn != null && i < lawn.Length && lawn[i] != 0;
            return density[i] / 255f;
        }
    }
}
