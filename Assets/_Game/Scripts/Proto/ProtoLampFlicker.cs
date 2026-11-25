using UnityEngine;

namespace Proto
{
    /// 고장 난 가로등 깜박임 — 전구 발광(_EmissionColor)과 바닥 빛 웅덩이(_Intensity)를 같이 흔든다.
    /// CityBuilder 가 도시 가로등 중 1~2개를 따로 떼어 붙인다(나머지는 켜짐·꺼짐 고정). 공유 머티리얼은 건드리지 않고 MPB 로만.
    /// 시작 연출 중 가로등이 꺼져 있으면(ProtoNightOverlay.LampsOnAt) 같이 꺼져 있다가, 켜질 차례가 오면 깜박이기 시작한다.
    public class ProtoLampFlicker : MonoBehaviour
    {
        public Renderer bulb;
        public Renderer pool;
        public Color emission = new Color(1.6f, 1.25f, 0.72f);
        public float poolIntensity = 0.35f;
        public int seed = 1;

        static readonly int IdEmission  = Shader.PropertyToID("_EmissionColor");
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");

        MaterialPropertyBlock _mpb;
        System.Random _rng;
        Transform _player;
        float _timer, _level = 1f;
        int _burst;   // 남은 빠른 깜박임 횟수

        void OnEnable()
        {
            _mpb = new MaterialPropertyBlock();
            _rng = new System.Random(seed);
            _timer = Range(0.3f, 2f);
            var p = FindFirstObjectByType<ProtoPlayer>();
            _player = p != null ? p.transform : null;
        }

        float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) Step();

            Vector3 c = _player != null ? _player.position : Vector3.zero;
            Vector3 d = transform.position - c; d.y = 0f;
            float k = _level * ProtoNightOverlay.LampK(d.magnitude);

            if (bulb != null) { bulb.GetPropertyBlock(_mpb); _mpb.SetColor(IdEmission, emission * k); bulb.SetPropertyBlock(_mpb); }
            if (pool != null) { pool.GetPropertyBlock(_mpb); _mpb.SetFloat(IdIntensity, poolIntensity * k); pool.SetPropertyBlock(_mpb); }
        }

        /// 대부분 켜져 있다가 가끔 다다닥 깜박이고, 이따금 잠깐 꺼진다.
        void Step()
        {
            if (_burst > 0)
            {
                _burst--;
                _level = _level > 0.5f ? Range(0f, 0.15f) : 1f;
                _timer = Range(0.03f, 0.12f);
                if (_burst == 0) { _level = 1f; _timer = Range(0.8f, 3.5f); }
                return;
            }
            if (_level < 1f) { _level = 1f; _timer = Range(0.8f, 3.5f); return; }   // 꺼짐·흐려짐 뒤엔 다시 켜져 한동안 버틴다
            double r = _rng.NextDouble();
            if (r < 0.7) { _burst = _rng.Next(2, 7) * 2; _level = 0.1f; _timer = Range(0.03f, 0.1f); }   // 다다닥
            else if (r < 0.9) { _level = 0f; _timer = Range(0.3f, 1.2f); _burst = 0; }                    // 잠깐 꺼짐
            else { _level = 0.55f; _timer = Range(0.15f, 0.4f); }                                       // 흐려졌다 돌아옴
        }

        void OnDisable()
        {
            if (bulb != null) bulb.SetPropertyBlock(null);
            if (pool != null) pool.SetPropertyBlock(null);
        }
    }
}
