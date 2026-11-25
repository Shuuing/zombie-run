using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 비. <b>빗줄기는 카메라 앞에 붙는 쿼드 한 장</b>이고, 바닥 튐만 파티클이다.
    ///
    /// <para><b>왜 파티클을 버렸나.</b> 재 보니 비의 비용은 화면 채우기가 아니라 <b>입자 개수</b>였다 —
    /// 크기를 2배(면적 4배)로 키워도 비용이 그대로인데, 개수를 1/4 로 줄이면 55% 가 빠졌다.
    /// 빌트인 파티클이 매 프레임 CPU 에서 정점을 새로 쓰기 때문이다(823개 = 정점 3,300개).
    /// 쿼드 한 장이면 정점 4개이고 시뮬레이션이 아예 없다.</para>
    ///
    /// <para><b>비는 번개와 성격이 반대다.</b> 번개는 가지치는 고유한 실루엣이 있어서 플립북이
    /// 아니면 안 됐다. 빗줄기는 통계적으로 균일해서 타일링 텍스처를 흘리는 것과 입자를 뿌리는 것이
    /// 눈으로 구분되지 않는다.</para>
    ///
    /// <para><b>화염에 대해 여기 적어 뒀던 예측은 틀렸다.</b> "화염은 비용이 채우기 쪽이니
    /// 쿼드로 바꿔도 안 싸진다"고 썼었는데, 실제로 만들어 재 보니(<see cref="ProtoFlameSprite"/>)
    /// 진한 불에서는 파티클 +0.15ms 대 쿼드 +0.0ms 로 크게 벌어졌다.
    /// <b>'채우기 지배'는 플립북이 소용없다는 뜻이 아니다</b> — 파티클의 채우기는 대부분
    /// 입자끼리 <b>겹쳐 그리는</b> 양이고, 플립북은 그 겹침을 1배로 눌러 채우기 자체를 줄인다.
    /// 입자 크기를 줄였을 때 비용이 빠진 것도 같은 이유였는데 그때 한 단계를 건너뛰고 읽었다.</para>
    ///
    /// <para><b>바닥 튐은 파티클로 남겼다.</b> 240개뿐이고, 비가 "바닥에 닿는다"는 느낌은
    /// 이쪽이 만든다 — 카메라 앞 쿼드로는 원리상 못 만든다.</para>
    ///
    /// <para><b>원경·근경은 쿼드 두 장이 아니라 한 패스의 두 샘플이다.</b> 두 장으로 만들면
    /// 채우기가 두 배가 되는데 모바일에서 진짜 위험한 게 그거다. 같은 픽셀에서 UV 만 다르게
    /// 두 번 읽으면 시차는 그대로 얻고 채우기는 한 장 값만 낸다 (<c>Proto/RainSheet</c>).</para>
    ///
    /// <para>옵션 <see cref="ProtoSettings.RainEnabled"/> 로 끈다.</para>
    /// </summary>
    public class ProtoRainFx : MonoBehaviour
    {
        /// Resources 아래 경로 — 바닥 튐 파티클만 들어 있다.
        public const string FxPath = "VFX/FX_Rain";

        [Tooltip("빗줄기 쿼드를 카메라 앞 이 거리(m)에 둔다. 가까울수록 작아도 화면을 덮는다.")]
        public float sheetDistance = 4f;

        [Tooltip("쿼드가 화면을 덮는 비율. 1.06 이면 가장자리에 여유를 둬 회전할 때 틈이 안 보인다.")]
        public float sheetOversize = 1.06f;

        [Tooltip("바닥 튐이 따라갈 대상. 비어 있으면 플레이어를 찾는다.")]
        public Transform follow;

        [Tooltip("튐이 진행 방향으로 미리 나가는 거리(m).")]
        public float leadZ = 2.5f;

        [Tooltip("약한 비일 때 바닥 튐을 이 비율로 줄인다.")]
        [Range(0f, 1f)] public float lightSplashScale = 0.28f;

        // ── 바닥 빗방울 고리 · 물웅덩이 파문 (2026-11-12 사용자: "비가 오면 바닥에 빗물이 튀기고 물 웅덩이에도 빗물 떨어지게") ──
        // 바닥 튐(물방울 0.12~0.3m)은 게임 카메라(18m)에서 몇 픽셀이라 비가 바닥에 닿는 게 잘 안 보였다 →
        // 떨어진 자리에 납작한 고리가 퍼지는 파티클을 하나 더 둔다(코드로 짓는다, 프리팹 그대로). 밤 어둠 막(2900) 아래(2895)에 그려
        // 캐릭터 둘레 빛 안에서만 또렷하다 — 위에 그리면 어두운 곳에서 고리만 빛났다.
        // 물웅덩이는 물 셰이더가 직접 그린다(Art/Shaders/RainRipple.hlsl) — 여기선 세기만 전역 _Rain_Ripple 로 올린다.
        [Tooltip("강한 비의 바닥 고리 수(초당). 약한 비는 lightRingScale 배.")]
        public float ringRate = 140f;   // 200 에 또렷한 흰 고리는 "너무 인위적으로 튄다"(사용자) — 옅고 흐린 고리로 바꾸며 수도 줄였다
        [Range(0f, 1f)] public float lightRingScale = 0.4f;

        static readonly int IdRainRipple = Shader.PropertyToID("_Rain_Ripple");
        // x 세기, y 차례마다 떨어질 확률, z 칸 크기 m, w 칸 하나의 1초당 차례 수 (RainRipple.hlsl)
        static readonly Vector4 HeavyRipple = new Vector4(1f, 0.85f, 1.4f, 1.6f);   // 칸 1.4m — 1.1m 는 게임 카메라에서 고리가 너무 작았다
        static readonly Vector4 LightRipple = new Vector4(0.8f, 0.45f, 1.4f, 1.1f);

        static ProtoRainFx _instance;
        ParticleSystem _rings;

        GameObject _splash;
        ParticleSystem[] _splashPs;
        Transform _sheet;
        Camera _sheetCam;
        float _builtFov, _builtAspect, _builtDist;
        int _builtLevel = -1;
        float _splashBaseRate = -1f;
        int _splashLevel = -1;
        bool _playing;

        /// 없으면 만들어 둔다. <see cref="ProtoRun"/> 이 런 시작에 한 번 부른다.
        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("_RainFx");
            _instance = go.AddComponent<ProtoRainFx>();
        }

        void Awake()
        {
            if (_instance == null) _instance = this;
            Build();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            // 쿼드는 카메라 자식이라 이 오브젝트와 같이 사라지지 않는다. 직접 치운다.
            if (_sheet != null) Destroy(_sheet.gameObject);
            Shader.SetGlobalVector(IdRainRipple, Vector4.zero);   // 물웅덩이 파문도 멈춘다
        }

        void Build()
        {
            if (_splash == null)
            {
                var prefab = Resources.Load<GameObject>(FxPath);
                if (prefab != null)
                {
                    _splash = Instantiate(prefab, transform);
                    _splash.name = "Splash";
                    _splash.transform.localPosition = Vector3.zero;
                    _splashPs = _splash.GetComponentsInChildren<ParticleSystem>(true);
                }
                else
                {
                    Debug.LogWarning("[비] " + FxPath + " 가 없다 — 바닥 튐 없이 빗줄기만 나간다");
                    _splashPs = new ParticleSystem[0];
                }
            }
            if (_rings == null) _rings = BuildRings();
            SetPlaying(false);
        }

        /// 바닥 빗방울 고리 — 땅과 나란히 누운 고리가 퍼지며 옅어진다. 머티리얼은 바닥 튐 것을 복사해 셰이더(URP 파티클)가 빌드에 이미 들어 있다.
        ParticleSystem BuildRings()
        {
            var go = new GameObject("SplashRings");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);   // 다 퍼졌을 때 지름 m
            main.startColor = new Color(0.82f, 0.9f, 1f, 1f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // 튐처럼 — 떨어진 자리에 남고 방출 상자만 따라간다
            main.maxParticles = 300;
            var em = ps.emission; em.rateOverTime = ringRate;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(22f, 0f, 22f);   // 바닥 튐 상자와 같은 넓이
            sh.position = new Vector3(0f, 0.03f, 0f);
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f, 0f, 2.4f), new Keyframe(1f, 1f, 0f, 0f)));   // 빠르게 퍼지다 멈춘다
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.1f), new GradientAlphaKey(0.15f, 0.5f), new GradientAlphaKey(0f, 1f) });   // 은은하게 — 처음 판(0.85)은 또렷한 흰 고리가 인위적이었다
            col.color = new ParticleSystem.MinMaxGradient(g);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Material baseMat = null;
            if (_splashPs != null)
                foreach (var sp in _splashPs) { var sr = sp.GetComponent<ParticleSystemRenderer>(); if (sr != null && sr.sharedMaterial != null) { baseMat = sr.sharedMaterial; break; } }
            var mat = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("ZombieOverrun/FX/Particle"));   // 게임 파티클 셰이더 하나 (2026-11-20)
            mat.name = "RainRing";
            var tex = RingTexture();
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.renderQueue = 2895;   // 밤 어둠 막(2900) 아래 — 주변과 같이 어두워진다
            // 바닥 튐 머티리얼은 물방울을 옅게(기본색 알파 0.4) · 뒷면 안 그림 — 그대로 쓰면 고리가 거의 안 보였다(임시 프로젝트 렌더)
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.9f, 0.95f, 1f, 1f));
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            r.sharedMaterial = mat;
            return ps;
        }

        static Texture2D RingTexture()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "RainRingTex", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Exp(-Mathf.Pow((d - 0.75f) / 0.2f, 2f));        // 흐린 고리(가장자리가 번진다)
                    float inner = 0.05f * Mathf.Clamp01(1f - d / 0.75f);               // 안쪽은 아주 옅게 젖은 듯
                    float a = d > 1f ? 0f : Mathf.Clamp01(ring + inner);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// 빗줄기 쿼드를 카메라 자식으로 만든다. 시야각·화면비가 바뀌면 다시 만든다.
        void EnsureSheet(Camera cam)
        {
            bool needRebuild = _sheet == null || _sheetCam != cam
                             || _builtLevel != ProtoSettings.RainLevel
                             || !Mathf.Approximately(_builtFov, cam.fieldOfView)
                             || !Mathf.Approximately(_builtAspect, cam.aspect)
                             || !Mathf.Approximately(_builtDist, sheetDistance);
            if (!needRebuild) return;

            if (_sheet != null) DestroyImmediate(_sheet.gameObject);

            // 세기별로 시트가 다르다. **불투명도만 낮추면 '옅은 폭우'가 되지 가랑비가 안 된다** —
            // 가닥 수 자체가 다른 텍스처를 쓴다(ArtSource/VFX/bake_rainsheet.py 의 PRESETS).
            var mat = Resources.Load<Material>(ProtoSettings.RainLevel >= 2 ? "VFX/M_RainSheet" : "VFX/M_RainSheet_Light");
            if (mat == null)
            {
                // Resources 밖에 있는 머티리얼은 셰이더로 직접 만든다 — 배선을 늘리지 않는다.
                var sh = Shader.Find("Proto/RainSheet");
                if (sh == null) { Debug.LogWarning("[비] Proto/RainSheet 셰이더가 없다"); return; }
                mat = new Material(sh);
                var tex = Resources.Load<Texture2D>("VFX/T_RainStreak");
                if (tex != null) mat.SetTexture("_MainTex", tex);
            }

            // 카메라 앞 sheetDistance 에서 화면을 꽉 채우는 크기를 구한다.
            float h = 2f * sheetDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * sheetOversize;
            float w = h * cam.aspect;

            var go = new GameObject("RainSheet");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, sheetDistance);
            go.transform.localRotation = Quaternion.identity;

            var mesh = new Mesh { name = "RainSheetQuad" };
            mesh.vertices = new[] {
                new Vector3(-w * 0.5f, -h * 0.5f, 0f), new Vector3(w * 0.5f, -h * 0.5f, 0f),
                new Vector3(-w * 0.5f,  h * 0.5f, 0f), new Vector3(w * 0.5f,  h * 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            _sheet = go.transform;
            _sheetCam = cam;
            _builtFov = cam.fieldOfView;
            _builtAspect = cam.aspect;
            _builtDist = sheetDistance;
            _builtLevel = ProtoSettings.RainLevel;
        }

        void SetPlaying(bool on)
        {
            if (_playing == on) return;
            _playing = on;
            if (_sheet != null) _sheet.gameObject.SetActive(on);
            if (_rings != null) { if (on) _rings.Play(); else _rings.Stop(false, ParticleSystemStopBehavior.StopEmitting); }
            if (_splashPs == null) return;
            foreach (var ps in _splashPs)
            {
                if (on) ps.Play();
                // 방출만 멈춘다 — 이미 튀고 있는 물방울이 공중에서 사라지면 눈에 띈다.
                else ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// 바닥 튐 양을 세기에 맞춘다. 프리팹 원본이 아니라 **인스턴스**를 건드리므로 에셋은 안 바뀐다.
        void ApplySplashLevel()
        {
            int lvl = ProtoSettings.RainLevel;
            if (_splashLevel == lvl || _splashPs == null) return;
            _splashLevel = lvl;
            float k = lvl >= 2 ? 1f : lightSplashScale;
            foreach (var ps in _splashPs)
            {
                var em = ps.emission;
                if (_splashBaseRate < 0f) _splashBaseRate = em.rateOverTime.constant;
                em.rateOverTime = _splashBaseRate * k;
            }
            if (_rings != null) { var em = _rings.emission; em.rateOverTime = ringRate * (lvl >= 2 ? 1f : lightRingScale); }
        }

        void LateUpdate()
        {
            bool want = ProtoSettings.RainEnabled;

            var cam = Camera.main;
            if (want && cam != null) EnsureSheet(cam);
            SetPlaying(want && _sheet != null);
            // 물웅덩이 빗방울 파문 세기 — 물 셰이더가 읽는다(RainRipple.hlsl). 비가 꺼지면 0
            Shader.SetGlobalVector(IdRainRipple, !want || _sheet == null ? Vector4.zero : (ProtoSettings.RainLevel >= 2 ? HeavyRipple : LightRipple));
            if (!want) return;
            ApplySplashLevel();

            if (follow == null)
            {
                var pl = UnityEngine.Object.FindObjectOfType<ProtoPlayer>();
                if (pl != null) follow = pl.transform;
            }
            // 튐은 World 공간이라 이미 튄 물방울은 그 자리에 남는다. 방출 상자만 옮긴다.
            if (follow != null)
            {
                Vector3 p = follow.position;
                transform.position = new Vector3(p.x, 0f, p.z + leadZ);
            }
        }
    }
}
