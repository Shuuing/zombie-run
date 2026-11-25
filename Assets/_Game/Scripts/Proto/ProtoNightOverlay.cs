using UnityEngine;

namespace Proto
{
    /// 밤 어둠 막 구동기 — 카메라에 붙인다. 화면을 덮는 사각형을 만들고, 매 프레임 캐릭터 위치와 가까운 가로등 위치를
    /// 셰이더(Proto/NightOverlay) 전역으로 올린다. 조명을 쓰지 않아 모든 품질 프리셋에서 같고, 캐릭터는 원래 색 그대로다.
    [DefaultExecutionOrder(1100)]   // 카메라 추적(LateUpdate) 뒤 — 이번 프레임 카메라·캐릭터 위치로 구멍을 뚫는다
    public class ProtoNightOverlay : MonoBehaviour
    {
        public const int MaxLights = 16;   // 셰이더 NIGHT_MAX_LIGHTS 와 같아야 한다. 바꾸면 에디터 재시작(전역 배열 길이 고정)

        public Material overlayMaterial;
        [Tooltip("캐릭터 둘레 밝은 원 반경(m, 월드).")]
        [Range(1f, 20f)] public float playerRadius = 3.4f;   // 게임 카메라에 보이는 땅이 가로 ~10m 라 6.5m 면 화면 전체가 원 안이었다
        [Tooltip("원 가장자리 부드러움(반경 대비). 클수록 넓게 번진다.")]
        [Range(0.05f, 1f)] public float playerEdge = 0.6f;
        [Tooltip("원 중심 높이(m) — 캐릭터 몸 가운데. 발(0)로 두면 원이 캐릭터 아래로 치우쳐 보인다.")]
        public float playerHeight = 1.0f;
        [Tooltip("가로등 구멍 반경(m).")]
        [Range(0.5f, 12f)] public float lampRadius = 4.5f;
        [Range(0.05f, 1f)] public float lampEdge = 0.7f;
        [Tooltip("가로등 구멍 밝기(0~1). 1 이면 가로등 아래가 원 안처럼 완전히 밝다.")]
        [Range(0f, 1f)] public float lampStrength = 0.4f;
        [Tooltip("가로등 바닥 위치(CityBuilder 가 채운다).")]
        public Vector3[] lamps = new Vector3[0];

        // ── 착지 후 연출 (2026-10-21 사용자 — 참고 이미지: 밝은 곳이 부드럽게 퍼지고 화면 끝으로 갈수록 어둡다) ──
        // 시작 연출(비행기 → 낙하산 → 착지) 동안은 위의 또렷한 원 그대로, 끝나면 spreadTime 초에 걸쳐 넘어간다.
        [Header("착지 후 — 캐릭터에서 화면 끝까지 부드럽게 어두워진다")]
        // 캐릭터에서 화면 거리로 하나의 지수 곡선만 쓴다 — 월드 반경 빛에 화면 가장자리 어둠을 따로 겹쳤더니
        // 둘이 만나는 곳에서 밝기가 꺾여 큰 타원 경계로 보였다(2026-10-21 사용자).
        //
        // 2026-11-10 사용자: "캐릭터 주변만 좀 밝고 기본적으로 밤인데 좀 더 어둡게". 예전 값 0.04 / 4 / 0.95 →
        // 밝은 중심은 조금 넓히고(캐릭터 둘레 ~1.2m 는 그대로 밝다) 그 밖은 더 빨리, 더 깊게 어두워진다.
        // 캐릭터에서 3m 는 거의 그대로, 5m 는 밝기 0.32 → 0.23, 8m 는 0.15 → 0.10, 화면 끝은 0.05 → 0.03(눈 기준).
        // 좀비는 4~6m 까지 흐리게라도 보여야 한다(CityBuilder 밤 설정의 원래 조건) — 더 어둡게 하려면 그걸 같이 볼 것.
        // 2026-11-12 사용자: "기본도 살짝 밝게" → 0.08 / 6 / 0.97 에서 어두워지는 빠르기 5, 화면 끝 어둠 0.94 로(그 둘 사이쯤).
        // 5m 밝기 0.23 → 0.29, 8m 0.10 → 0.15, 화면 끝 0.03 → 0.06(눈 기준).
        // **필드 이름을 spread* 에서 night* 로 바꾼 이유**: 씬(Proto_City_Test · FX_Showcase)에 옛 값이 저장돼 있고, 에디터가 열린 채로는
        // 코드 기본값만 바꿔서는 열린 씬에 반영되지 않는다. 이름을 바꿔 옛 값을 버렸다. 다음부터는 이름 대신 아래 Tuning 을 올린다.
        [Tooltip("완전히 밝은 중심 반경(화면 높이 비율). 0.05 면 캐릭터 둘레만.")]
        [Range(0f, 0.4f)] public float nightCore = DefaultCore;
        [Tooltip("어두워지는 빠르기. 클수록 밝은 곳이 좁고 화면 끝이 빨리 어두워진다.")]
        [Range(0.5f, 12f)] public float nightFalloff = DefaultFalloff;
        [Tooltip("가장 어두운 곳(화면 끝)의 어둠 0~1.")]
        [Range(0f, 1f)] public float nightMaxDark = DefaultMaxDark;

        // ── 값 판 ── 코드에서 위 세 값의 기본값을 다시 정하면 Tuning 을 하나 올린다. 씬에 저장된(또는 열린 에디터가 들고 있는) 옛 값이
        // 판이 낮으면 새 기본값으로 바뀐다 — 필드 이름을 바꾸지 않아도 된다. 인스펙터에서 직접 만진 값도 판을 올리면 덮어쓴다.
        // 2026-11-13 사용자: "착륙하고 나서 밤 배경을 조금만 밝게" → 판 3: 어두워지는 빠르기 5 → 4.5, 화면 끝 어둠 0.94 → 0.91(화면 끝 밝기 0.06 → 0.09, 눈 기준).
        // 11-12 와 같은 크기의 한 걸음만 — 밝기를 여러 번 오갔다(11-10 어둡게 → 11-12 살짝 밝게 → 11-13 조금 더).
        const float DefaultCore = 0.08f, DefaultFalloff = 4.5f, DefaultMaxDark = 0.91f;
        const int Tuning = 3;   // 1 = 0.08 / 6 / 0.97 (11-10), 2 = 0.08 / 5 / 0.94 (11-12), 3 = 0.08 / 4.5 / 0.91 (11-13)
        [SerializeField, HideInInspector] int _tuning;

        void ApplyTuning()
        {
            if (_tuning >= Tuning) return;
            nightCore = DefaultCore; nightFalloff = DefaultFalloff; nightMaxDark = DefaultMaxDark;
            _tuning = Tuning;
        }
        void Awake() => ApplyTuning();
#if UNITY_EDITOR
        void OnValidate() => ApplyTuning();
#endif

        /// 화면 끝(가장 어두운 곳)의 눈에 보이는 밝기 0~1. 시작 연출이 이 밝기로 내려앉는다.
        public float FloorSeen => 1f - Mathf.Clamp01(nightMaxDark);
        [Tooltip("시작 연출이 끝난 뒤 새 연출로 넘어가는 시간(초).")]
        public float spreadTime = 1.5f;
        float _spread;   // 0 = 시작 연출 모습, 1 = 착지 후 모습

        /// 캐릭터 둘레 빛의 세기(0 = 꺼짐, 1 = 평소). 시작 연출(ProtoIntro)이 낙하 높이에 맞춰 0 에서 1 로 서서히 올린다 —
        /// 안 그러면 캐릭터가 아직 하늘에 있는데 땅의 시작 지점만 밝다. 랜턴(ProtoLantern)도 이 값을 곱한다.
        public static float PlayerLightScale = 1f;

        /// 화면 전체에 깔리는 바닥 밝기(0 = 평소, 1 = 어둠 없음). 시작 연출(ProtoIntro)만 쓴다 — 떨어지기 전에는 이 막을 거의 걷어 두고
        /// 어둠은 연출의 막(Proto/IntroVeil)이 화면 전체에 고르게 맡는다. 캐릭터가 내려올수록 0 으로 내려가 평소 밤 화면이 된다. 2026-11-12
        public static float IntroAmbient = 0f;

        // 도메인 리로드 없이 플레이를 다시 누르면 static 이 남는다 — 연출 중에 멈췄으면 0 으로 남아 원이 안 켜진다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { PlayerLightScale = 1f; LampsOnAt = 0f; IntroAmbient = 0f; }

        /// 가로등이 켜진 시각(Time.time). <see cref="float.MaxValue"/> 면 전부 꺼짐. 시작 연출이 낙하 중엔 끄고
        /// 캐릭터가 내려오면 켠다 — 캐릭터에서 가까운 것부터 차례로(거리 1m 당 LampStagger 초) 퍼지며 들어온다.
        public static float LampsOnAt = 0f;
        const float LampStagger = 0.035f;
        const float LampFadeIn  = 0.3f;

        /// 캐릭터에서 dist(m) 떨어진 가로등의 켜짐 정도 0~1(살짝 넘침). 바닥 빛 웅덩이(ProtoIntro)도 같은 값을 쓴다.
        public static float LampK(float dist)
        {
            if (LampsOnAt == float.MaxValue) return 0f;
            float u = Mathf.Clamp01((Time.time - LampsOnAt - dist * LampStagger) / LampFadeIn);
            if (u >= 1f) return 1f;
            const float s = 1.55f;   // BackOut — 살짝 넘쳤다 자리 잡는다
            float p = u - 1f;
            return 1f + (s + 1f) * p * p * p + s * p * p;
        }

        static readonly int IdPlayer = Shader.PropertyToID("_Night_Player");
        static readonly int IdEdge   = Shader.PropertyToID("_Night_Edge");
        static readonly int IdLights = Shader.PropertyToID("_Night_Lights");
        static readonly int IdCount  = Shader.PropertyToID("_Night_LightCount");
        static readonly int IdSpread = Shader.PropertyToID("_Night_Spread");
        static readonly int IdAmbient = Shader.PropertyToID("_Night_Ambient");

        readonly Vector4[] _lights = new Vector4[MaxLights];
        readonly float[] _dist = new float[MaxLights];
        [Tooltip("밝은 원의 중심. 비우면 ProtoPlayer 를 찾는다 — 플레이어가 없는 씬(FX 쇼케이스)에서 무대를 지정할 때 쓴다.")]
        public Transform target;
        Transform _target;
        GameObject _quad;

        void OnEnable()
        {
            if (_quad == null) _quad = BuildQuad();
            _quad.SetActive(true);
        }

        void OnDisable()
        {
            if (_quad != null) _quad.SetActive(false);
            Shader.SetGlobalVector(IdPlayer, Vector4.zero);
            Shader.SetGlobalFloat(IdCount, 0f);
            Shader.SetGlobalVector(IdSpread, Vector4.zero);
            Shader.SetGlobalFloat(IdAmbient, 0f);
        }

        /// 화면 덮개 — 정점은 셰이더가 클립 공간에 그대로 까니 위치는 상관없지만, 컬링에 안 걸리게 카메라 앞에 두고 바운즈를 크게.
        GameObject BuildQuad()
        {
            var go = new GameObject("_NightOverlayQuad") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 1f);
            var mesh = new Mesh { name = "NightOverlayQuad" };
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(1, -1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = overlayMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        void LateUpdate()
        {
            if (_target == null)
            {
                if (target != null) _target = target;
                else
                {
                    var p = FindFirstObjectByType<ProtoPlayer>();
                    _target = p != null ? p.transform : null;
                }
            }
            Vector3 c = _target != null ? _target.position : transform.position;
            // 시작 연출이 낙하 높이로 정하는 캐릭터 빛의 세기(0 = 꺼짐, 1 = 평소)
            float level = Mathf.Clamp01(PlayerLightScale);
            // w(반경)는 0 보다 커야 셰이더가 '캐릭터 빛 있음'으로 본다 — 꺼져 있을 때도 아주 작은 값으로 남기고 세기는 Edge.w 로 줄인다.
            float pr = Mathf.Max(playerRadius * level, 0.001f);
            Shader.SetGlobalVector(IdPlayer, _target != null ? new Vector4(c.x, c.y + playerHeight, c.z, pr) : Vector4.zero);
            Shader.SetGlobalVector(IdEdge, new Vector4(playerEdge, lampEdge, lampStrength, 1f - level));

            // 판(ProtoRun)이 있으면 시작 연출 중에도 착지 후 모습(부드럽게 퍼지는 빛)을 쓴다 — 2026-11-10 사용자:
            // "캐릭터가 바닥에 가까워질수록 자연스럽게 밝아지게". 밝기만 PlayerLightScale(낙하 높이)로 서서히 올리고,
            // 약할 때는 빛이 좁다가(빠르게 어두워짐) 세질수록 넓게 퍼진다. 그전에는 연출 중 또렷한 원(_spread 0)을 쓰다가
            // 착지 뒤 spreadTime 초에 걸쳐 넘어갔다 — 그 또렷한 원은 이제 판도 target 도 없는 씬에서만 나온다.
            var run = ProtoRun.I;
            // 판이 없어도 target 을 직접 준 씬(FX 쇼케이스)은 착지 후 모습으로 본다 — 효과를 게임과 같은 어둠 아래에서 보려고.
            bool crisp = run == null && target == null;
            if (crisp) _spread = 0f;
            else if (run != null && run.state == RunState.Intro) _spread = 1f;
            else _spread = Mathf.MoveTowards(_spread, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, spreadTime));
            float sp = Mathf.SmoothStep(0f, 1f, _spread);
            Shader.SetGlobalVector(IdSpread, new Vector4(sp, nightFalloff * Mathf.Lerp(2.2f, 1f, level), nightCore, nightMaxDark));
            Shader.SetGlobalFloat(IdAmbient, Mathf.Clamp01(IntroAmbient));

            // 가까운 가로등 MaxLights 개 — 선택 정렬(개수가 작아 충분), 할당 없음
            int n = 0;
            for (int i = 0; i < lamps.Length; i++)
            {
                float d = (lamps[i] - c).sqrMagnitude;
                float lk = LampK(Mathf.Sqrt(d));
                if (lk <= 0.01f) continue;   // 아직 안 켜진 가로등
                float lr = lampRadius * lk;
                if (n < MaxLights) { _lights[n] = new Vector4(lamps[i].x, lamps[i].y, lamps[i].z, lr); _dist[n] = d; n++; continue; }
                int worst = 0;
                for (int k = 1; k < MaxLights; k++) if (_dist[k] > _dist[worst]) worst = k;
                if (d < _dist[worst]) { _lights[worst] = new Vector4(lamps[i].x, lamps[i].y, lamps[i].z, lr); _dist[worst] = d; }
            }
            for (int k = n; k < MaxLights; k++) _lights[k] = Vector4.zero;
            Shader.SetGlobalVectorArray(IdLights, _lights);
            Shader.SetGlobalFloat(IdCount, n);
        }
    }
}
