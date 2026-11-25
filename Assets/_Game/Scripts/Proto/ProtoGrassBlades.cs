using UnityEngine;

namespace Proto
{
    /// 구운 풀잎 블록들의 루트(_City/_GrassBlades — CityGrassBaker 가 붙인다). 풀이 있는 씬에서만 상호작용 필드와
    /// 풀 조각 튀기(ProtoGrassKick)를 켠다.
    public class ProtoGrassBlades : MonoBehaviour
    {
        [Tooltip("풀 자리 지도 — CityGrassBaker 가 같이 구워 넣는다. 비어 있으면 풀 조각이 안 튄다(풀이 눕는 건 그대로).")]
        public ProtoGrassMask mask;

        /// 지금 켜져 있는 풀 지도. 풀 없는 씬이면 null.
        public static ProtoGrassMask Mask { get; private set; }

        void OnEnable()
        {
            // 풀이 눕는 필드(매 프레임 256² 블릿 + 밟힌 자리 찍기)는 Proto/Grass 셰이더만 읽는다 — 그려지는 풀잎이 있을 때만 켠다.
            // 풀 최종본(팩 풀 패치, CityGrassVersions)은 풀잎 렌더러를 다 끄고 메시도 빼 두므로 필드가 헛돈다(2026-11-13 최적화).
            if (AnyBladeDrawn()) ProtoGrassField.Ensure();
            Mask = mask;
            if (mask != null) ProtoGrassKick.Ensure();
        }

        void OnDisable() { if (Mask == mask) Mask = null; }

        bool AnyBladeDrawn()
        {
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue;
                var f = r.GetComponent<MeshFilter>();
                if (f != null && f.sharedMesh != null) return true;
            }
            return false;
        }
    }
}
