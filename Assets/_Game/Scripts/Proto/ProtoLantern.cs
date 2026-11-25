using UnityEngine;

namespace Proto
{
    /// 캐릭터가 든 랜턴 — 포인트 라이트의 밝기·반경을 살짝 일렁이게 한다. 불꽃이 고정된 전구처럼 보이면 밤 분위기가 죽는다.
    /// 라이트 자체는 CityBuilder(밤 설정)가 Player 자식으로 붙인다.
    [RequireComponent(typeof(Light))]
    public class ProtoLantern : MonoBehaviour
    {
        [Tooltip("밝기 흔들림 비율. 0.08 이면 ±8%. 크게 하면 촛불, 작게 하면 전등.")]
        [Range(0f, 0.4f)] public float flicker = 0.08f;
        [Tooltip("흔들림 빠르기.")]
        [Range(0.2f, 12f)] public float speed = 3.2f;

        Light _light;
        float _baseIntensity, _baseRange, _seed;

        void Awake()
        {
            _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;
            _baseRange = _light.range;
            _seed = Random.value * 100f;
        }

        void Update()
        {
            // 두 주기를 겹쳐 규칙적인 맥동처럼 안 보이게
            float t = Time.time * speed + _seed;
            float n = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 0.7f + (Mathf.PerlinNoise(t * 3.1f, 5.1f) - 0.5f) * 0.3f;
            // 시작 연출 중엔 캐릭터 둘레 원과 같이 꺼져 있다가 같이 켜진다(ProtoNightOverlay.PlayerLightScale)
            _light.intensity = _baseIntensity * (1f + n * 2f * flicker) * Mathf.Clamp01(ProtoNightOverlay.PlayerLightScale);
            _light.range = _baseRange * (1f + n * flicker * 0.5f);
        }
    }
}
