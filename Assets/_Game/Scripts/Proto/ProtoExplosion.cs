using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 로켓 착탄 폭발 연출 (GDD §05).
    ///
    /// <para><b>씬 배선이 필요 없다.</b> <see cref="Play"/> 를 처음 부르면 관리자가 자기
    /// GameObject 를 만들어 붙는다 — <see cref="ProtoLevelUpBeam"/> 과 같은 방식이다.
    /// 다만 빛기둥은 한 번에 하나뿐인데 <b>폭발은 동시에 여러 개</b> 터질 수 있어서
    /// (융단 폭격 진화는 6지점 동시) 풀을 둔다.</para>
    ///
    /// <para><b>네 겹이 서로 다른 시각에 시작하고 끝난다.</b> 이게 이 연출의 핵심이다 —
    /// 예전에는 모든 겹이 0.34초 곡선 하나를 공유해서 같이 나타나 같이 사라졌고,
    /// 그래서 "터졌다 → 남았다"가 없이 평평하게 보였다. 지금은
    /// 섬광 0.07s → 화구 0.30s → 링 0.47s → 연기 1.00s 로 계단을 만든다.
    /// 뒤 겹이 앞 겹보다 오래 남는 순서가 중요하다.</para>
    ///
    /// <para>지면 그을음 자국이 한때 있었지만 <b>제거했다</b> — 도로가 어두워지는 게
    /// 보기 싫다는 판단이다. 지금 폭발은 1.00초 안에 흔적 없이 사라진다.</para>
    ///
    /// <para>정보 전달 담당은 여전히 <b>지면 충격파 링</b>이다. 그 테두리가 정확히 폭발
    /// 반경이라 "무엇이 맞았는지"가 읽힌다 (§15 — 데미지 숫자를 안 쓰는 게임이라 이 정보가
    /// 링에만 있다). 그래서 링만 노이즈로 흐트리지 않는다.</para>
    /// </summary>
    public class ProtoExplosion : MonoBehaviour
    {
        // 레이어 인덱스. 배열 순서 = 자식 생성 순서다.
        const int L_FLASH = 0, L_BALL = 1, L_RING = 2, L_SMOKE = 3;
        const int LayerCount = 4;

        [Header("타이밍 (초) — 시작 지연 / 길이")]
        [Tooltip("섬광: 첫 프레임의 열감. 길면 화면이 하얗게 뜬다")]
        public float flashLen = 0.07f;
        [Tooltip("화구: 타격감 담당")]
        public float ballLen = 0.30f;
        public float ringDelay = 0.02f;
        [Tooltip("링은 화구보다 오래 남아야 한다 — 반경을 읽을 시간을 준다")]
        public float ringLen = 0.45f;
        public float smokeDelay = 0.10f;
        [Tooltip("연기가 가장 오래 남는 겹이다 = 폭발 전체 수명")]
        public float smokeLen = 0.90f;

        [Header("크기 (폭발 반경 대비 지름 배수)")]
        [Tooltip("실루엣을 안쪽으로 침식하므로(_Lobe) 예전 0.85 보다 키워 겉보기 크기를 맞춘다")]
        public float fireballScale = 1.05f;
        [Tooltip("화구가 지면에서 떠 있는 높이 m")]
        public float fireballHeight = 0.55f;
        public float flashScale = 1.20f;
        public float smokeScale = 1.30f;
        [Tooltip("연기가 올라가는 높이 m")]
        public float smokeRise = 1.35f;

        [Header("성능")]
        [Tooltip("동시 폭발이 이 수를 넘으면 이후 폭발은 화구+링만 그린다. " +
                 "융단 폭격은 2.2초마다 6지점을 동시에 때리므로 여기서 막지 않으면 " +
                 "그 순간 반투명 오버드로가 6배로 곱해진다")]
        public int detailBudget = 3;

        // (화면 흔들림 값 shakePerRadius · shakeDuration 은 10-13 부터 안 썼다 — 폭발은 ProtoCameraFollow.Impact.Medium 으로 흔든다. 2026-11-20 뺌)

        static readonly int ProgressID = Shader.PropertyToID("_Progress");
        static readonly int SeedID     = Shader.PropertyToID("_Seed");
        static readonly int KindID     = Shader.PropertyToID("_Kind");

        static ProtoExplosion _instance;

        class Fx
        {
            public Transform root;
            public Transform[] tr = new Transform[LayerCount];
            public Renderer[]  rd = new Renderer[LayerCount];
            public float age;        // 초. -1 이면 쉬는 중
            public float life;       // 이 인스턴스가 살아 있어야 하는 총 시간
            public float radius;
            public float seed;
            public bool  lean;       // true 면 섬광·연기를 끈다
        }

        readonly List<Fx> _live = new List<Fx>();
        readonly Queue<Fx> _pool = new Queue<Fx>();
        Material[] _mats;
        Mesh _disc;
        MaterialPropertyBlock _mpb;
        Camera _cam;
        bool _ready;

        /// <summary>폭발을 한 번 재생한다. radius 는 §05 의 착탄 반경(로켓 2.4m).</summary>
        // shake=false 면 화면을 흔들지 않는다(탱크 포탄 — 2026-10-13 사용자 요청).
        public static void Play(Vector3 worldPos, float radius, bool ignoreDuck = false, bool shake = true)
        {
            if (_instance == null)
            {
                var go = new GameObject("_Explosions");
                _instance = go.AddComponent<ProtoExplosion>();
            }
            _instance.PlayAt(worldPos, radius, shake);
            Aftermath(worldPos, radius, ignoreDuck);
        }

        /// <summary>그림 없이 폭발의 나머지(풀 눕힘 · 풀 조각 · 폭발음)만. 그림을 프리팹이 대신할 때 —
        /// 탈것 폭발(<see cref="VehicleBlastFx"/>, 2026-11-13). <see cref="Play"/> 도 이걸 부른다.</summary>
        public static void Aftermath(Vector3 worldPos, float radius, bool ignoreDuck = false)
        {
            ProtoGrassField.Blast(worldPos, radius);   // 주변 풀을 눕히고 그을린다(풀 없는 씬이면 무시)
            ProtoGrassKick.Burst(worldPos, radius);    // 풀 조각이 바깥으로 흩날린다(풀 없는 자리면 무시)
            // 소리도 여기서 낸다. 폭발을 부르는 곳이 늘어날 때마다(현재는 로켓 착탄만)
            // 소리를 따로 붙이는 것을 잊지 않게, 연출과 같은 지점에 묶어 둔다.
            // 반경이 크면 조금 더 크게 — 링 테두리가 판정 반경이므로 눈과 귀가 같이 커진다.
            ProtoAudio.Play("explosion", worldPos, Mathf.Clamp(radius / 2.4f, 0.7f, 1.3f), ignoreDuck);
        }

        void Awake()
        {
            if (_instance == null) _instance = this;
            Build();
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        void Build()
        {
            var sh = Shader.Find("Proto/Explosion");
            if (sh == null)
            {
                Debug.LogError("[폭발] Proto/Explosion 셰이더를 못 찾았다 — 연출이 안 나온다.");
                enabled = false;
                return;
            }
            _disc = BuildDisc(64);
            _mpb = new MaterialPropertyBlock();

            // 머티리얼은 겹마다 하나씩. `_Kind` 와 색은 머티리얼에 굽고, 인스턴스마다 달라지는
            // `_Progress`·`_Seed` 만 MPB 로 넣는다 — 그러면 폭발마다 머티리얼을 복제하지 않는다.
            _mats = new Material[LayerCount];

            _mats[L_FLASH] = MakeMat(sh, 3f,
                new Color(1.00f, 1.00f, 0.96f), new Color(1.00f, 0.98f, 0.86f), new Color(1.00f, 0.90f, 0.62f),
                bands: 2, lobe: 0f, alpha: 0.95f);

            _mats[L_BALL] = MakeMat(sh, 0f,
                new Color(1.00f, 0.97f, 0.78f), new Color(1.00f, 0.62f, 0.16f), new Color(0.82f, 0.20f, 0.07f),
                bands: 4, lobe: 0.42f, alpha: 1f);

            _mats[L_RING] = MakeMat(sh, 1f,
                new Color(1.00f, 0.97f, 0.78f), new Color(1.00f, 0.62f, 0.16f), new Color(0.82f, 0.20f, 0.07f),
                bands: 4, lobe: 0f, alpha: 1f);
            _mats[L_RING].SetFloat("_RingWidth", 0.34f);

            _mats[L_SMOKE] = MakeMat(sh, 2f,
                new Color(0.36f, 0.34f, 0.32f), new Color(0.24f, 0.23f, 0.22f), new Color(0.14f, 0.14f, 0.14f),
                bands: 3, lobe: 0.62f, alpha: 0.52f);

            _ready = true;
        }

        Material MakeMat(Shader sh, float kind, Color core, Color mid, Color edge,
                         float bands, float lobe, float alpha)
        {
            var m = new Material(sh);
            m.SetFloat(KindID, kind);
            m.SetColor("_ColorCore", core);
            m.SetColor("_ColorMid",  mid);
            m.SetColor("_ColorEdge", edge);
            m.SetFloat("_Bands", bands);
            m.SetFloat("_Lobe",  lobe);
            m.SetFloat("_Alpha", alpha);
            m.SetFloat("_NoiseScale", 1.15f);
            return m;
        }

        void PlayAt(Vector3 pos, float radius, bool shake)
        {
            if (!_ready) { Build(); if (!_ready) return; }
            if (radius <= 0.01f) radius = 1f;

            // **카메라를 먼저 잡는다.** Apply 안에서 처음 잡으면 그 프레임의 화구가 빌보드되지
            // 않은 채(회전 identity = XY 평면에 세로로 선 원판) 그려진다. 피치 58° 카메라에서
            // 그건 도로를 가로지르는 납작한 띠로 보인다 — 진단 중에 실제로 본 증상이다.
            ResolveCamera();

            var fx = _pool.Count > 0 ? _pool.Dequeue() : CreateFx();
            fx.radius = radius;
            fx.age = 0f;
            fx.seed = Random.value * 64f;   // 폭발마다 실루엣이 달라야 한다. 같으면 복사한 티가 난다
            fx.lean = _live.Count >= Mathf.Max(1, detailBudget);
            fx.root.position = pos;
            fx.root.gameObject.SetActive(true);

            for (int i = 0; i < LayerCount; i++)
            {
                bool on = !fx.lean || i == L_BALL || i == L_RING;
                fx.rd[i].enabled = on;
            }

            fx.life = fx.lean
                ? Mathf.Max(ballLen, ringDelay + ringLen)
                : Mathf.Max(Mathf.Max(ballLen, ringDelay + ringLen), smokeDelay + smokeLen);

            // 지면에 눕는 겹은 링뿐이다. 0.09 는 도로 차선과 Z파이팅이 나는 높이(0.00~0.05) 위다.
            SetGround(fx.tr[L_RING], 90f, 0.09f - pos.y);

            // 화면 흔들림 (§03: 4단계, 진폭 상한 0.45m, 접근성 옵션으로 0). 탱크 포탄은 끈다.
            if (shake) ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Medium);   // 로켓·융단 폭격 (2026-10-13 단계화 — 예전 반경×0.045 는 0.11m 로 거의 안 보였다)

            Apply(fx);
            _live.Add(fx);
        }

        static void SetGround(Transform t, float pitch, float y)
        {
            t.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            t.localPosition = new Vector3(0f, y, 0f);
        }

        Fx CreateFx()
        {
            var root = new GameObject("Explosion").transform;
            root.SetParent(transform, false);
            var fx = new Fx { root = root, age = -1f };

            string[] names = { "Flash", "Fireball", "Shockwave", "Smoke" };
            for (int i = 0; i < LayerCount; i++)
            {
                fx.tr[i] = MakeQuad(names[i], _mats[i], root);
                fx.rd[i] = fx.tr[i].GetComponent<Renderer>();
            }
            return fx;
        }

        Transform MakeQuad(string name, Material mat, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _disc;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go.transform;
        }

        void Update()
        {
            if (_live.Count == 0) return;
            float dt = ProtoConfig.Delta;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var fx = _live[i];
                fx.age += dt;
                if (fx.age >= fx.life)
                {
                    fx.age = -1f;
                    fx.root.gameObject.SetActive(false);
                    _live.RemoveAt(i);
                    _pool.Enqueue(fx);
                    continue;
                }
                Apply(fx);
            }
        }

        /// 씬 빌더가 만든 카메라에는 MainCamera 태그가 없을 수 있어 디렉터 것을 먼저 본다
        /// (젬 스프라이트 빌보드와 같은 이유).
        void ResolveCamera()
        {
            if (_cam != null && _cam.isActiveAndEnabled) return;
            var d = ProtoSpawnDirector.I;
            _cam = (d != null && d.viewCamera != null) ? d.viewCamera : Camera.main;
        }

        /// 겹별 진행도. 아직 시작 전이면 -1, 이미 끝났으면 1 을 넘긴다.
        static float Local(float age, float delay, float len)
        {
            if (age < delay) return -1f;
            return (age - delay) / Mathf.Max(0.01f, len);
        }

        void Apply(Fx fx)
        {
            float age = Mathf.Max(0f, fx.age);
            float dia = fx.radius * 2f;

            ResolveCamera();
            Quaternion billboard = _cam != null
                ? Quaternion.LookRotation(-_cam.transform.forward, Vector3.up)
                : Quaternion.identity;

            // ── 섬광 ── 즉시 최대 크기. 커지는 걸 볼 시간이 없으니 크기를 굽히지 않는다.
            float pf = Local(age, 0f, flashLen);
            if (Push(fx, L_FLASH, pf))
            {
                float s = dia * flashScale;
                fx.tr[L_FLASH].localScale = new Vector3(s, s, 1f);
                fx.tr[L_FLASH].localPosition = new Vector3(0f, fireballHeight * 0.6f, 0f);
                fx.tr[L_FLASH].rotation = billboard;
            }

            // ── 화구 ── 톡 튀어나왔다 살짝 줄어든다(ease-out-back).
            // 커지기만 하면 "번지는 연기"로 보인다.
            float pb = Local(age, 0f, ballLen);
            if (Push(fx, L_BALL, pb))
            {
                float u = Mathf.Clamp01(pb) - 1f;
                const float back = 1.6f;
                float pop = 1f + (back + 1f) * u * u * u + back * u * u;   // 0→1, 1 을 살짝 넘김
                float s = dia * fireballScale * Mathf.Lerp(0.45f, 1f, pop);
                fx.tr[L_BALL].localScale = new Vector3(s, s, 1f);
                fx.tr[L_BALL].localPosition = new Vector3(0f, fireballHeight * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(pb)), 0f);
                fx.tr[L_BALL].rotation = billboard;
            }

            // ── 충격파 ── 반경까지 빠르게 퍼진다. sqrt 로 앞을 빠르게 — 등속이면 느려 보인다.
            float pr = Local(age, ringDelay, ringLen);
            if (Push(fx, L_RING, pr))
            {
                float s = dia * Mathf.Sqrt(Mathf.Lerp(0.04f, 1f, Mathf.Clamp01(pr)));
                fx.tr[L_RING].localScale = new Vector3(s, s, 1f);
            }

            // ── 연기 ── 계속 부풀며 올라간다. 화구가 멈춘 뒤에도 움직이는 게 있어야
            // 폭발이 "끝났다"가 아니라 "지나갔다"로 읽힌다.
            float ps = Local(age, smokeDelay, smokeLen);
            if (Push(fx, L_SMOKE, ps))
            {
                float k = Mathf.Clamp01(ps);
                float s = dia * smokeScale * Mathf.Lerp(0.55f, 1.25f, Mathf.Sqrt(k));
                fx.tr[L_SMOKE].localScale = new Vector3(s, s, 1f);
                fx.tr[L_SMOKE].localPosition = new Vector3(0f, fireballHeight + smokeRise * k, 0f);
                fx.tr[L_SMOKE].rotation = billboard;
            }
        }

        /// 겹 하나의 진행도를 MPB 로 밀어넣는다. 아직 시작 전이거나 끝났으면 렌더러를 끄고 false.
        /// MPB 는 블록 전체를 갈아끼우므로 겹마다 다시 채운다 — 하나를 공유하면 마지막 값이
        /// 모든 겹에 들어간다 (이게 예전에 모든 겹이 같은 시간을 쓰던 이유다).
        bool Push(Fx fx, int layer, float p)
        {
            var rd = fx.rd[layer];
            bool active = !fx.lean || layer == L_BALL || layer == L_RING;
            if (!active) return false;

            if (p < 0f || p >= 1f)
            {
                if (rd.enabled) rd.enabled = false;
                return false;
            }
            if (!rd.enabled) rd.enabled = true;

            _mpb.Clear();
            _mpb.SetFloat(ProgressID, p);
            _mpb.SetFloat(SeedID, fx.seed + layer * 13.7f);
            rd.SetPropertyBlock(_mpb);
            return true;
        }

        /// 중심 정점 + 테두리 링. UV 가 `length(uv-0.5)*2 == 1` 로 테두리에서 정확히 1 이 되게 굽는다
        /// (셰이더가 그 규약으로 반경을 잰다 — ProtoLevelUpBeam.BuildDisc 와 같다).
        /// 세그먼트를 64 로 올린 이유: 실루엣을 노이즈로 침식하면 테두리 다각형의 직선 구간이
        /// 혹의 곡선과 겹쳐 보인다.
        static Mesh BuildDisc(int segments)
        {
            segments = Mathf.Max(8, segments);
            var verts = new Vector3[segments + 1];
            var uvs   = new Vector2[segments + 1];
            var tris  = new int[segments * 3];

            verts[0] = Vector3.zero;
            uvs[0]   = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                verts[i + 1] = new Vector3(ca * 0.5f, sa * 0.5f, 0f);
                uvs[i + 1]   = new Vector2(0.5f + ca * 0.5f, 0.5f + sa * 0.5f);
            }
            for (int i = 0; i < segments; i++)
            {
                int t = i * 3;
                tris[t + 0] = 0;
                tris[t + 1] = i + 1;
                tris[t + 2] = (i + 1) % segments + 1;
            }

            var mesh = new Mesh { name = "ExplosionDisc" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
