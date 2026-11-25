using UnityEngine;

namespace Proto
{
    /// 캐릭터 교체 연출 — 하늘에서 내리꽂히는 번개.
    ///
    /// <para><b>두 겹이다.</b> 번개 줄기는 16장 플립북(카메라를 보는 세로 쿼드)이고, 지면 타격은
    /// 파티클 프리팹 <c>Resources/VFX/FX_LightningGround</c>(지면에 눕는 링 + 불똥)이다.
    /// 나눈 이유: 가지치는 번개는 절차적으로 못 만들고(노이즈 지그재그는 굵기가 일정해서 전선이 된다),
    /// 반대로 지면 섬광을 플립북에 그려 넣으면 세로 쿼드에 그려지는 탓에 바닥에 눕지 않는다.</para>
    ///
    /// <para><b>왜 SpriteRenderer 가 아니라 메시인가 — 이게 이 클래스의 핵심이다.</b>
    /// 번개는 화면 <b>바깥</b>에서 내려와야 한다. 화면 안에서 끝나면 "하늘에서 내려온" 게 아니라
    /// "허공에서 생긴" 것으로 보인다. 그런데 그러려면 월드 높이가 얼마여야 하는지는
    /// <b>카메라 각도와 FOV 가 정한다</b> — 이 프로젝트만 해도 <see cref="ProtoCameraFollow"/> 의
    /// 각도1(58°)은 8.0m, 각도2(37.3°)는 3.9m 다. 상수로 박으면 한쪽에서 반드시 틀린다.</para>
    ///
    /// <para>그렇다고 스프라이트를 통째로 세로로 늘리면 <b>지면 섬광까지 같이 늘어나서</b>
    /// 바닥 폭발이 세로로 솟구친 기둥이 된다. 그래서 쿼드를 가로 3줄로 나눈 메시를 쓴다:
    /// 이음매(<see cref="SeamV"/>) 아래 — 섬광 구역 — 은 항상 1:1 이고, 그 위 줄기 구역만
    /// 필요한 만큼 늘린다. 배율이 1 이든 6 이든 섬광 비율은 그대로다.</para>
    ///
    /// <para><b>씬 배선이 필요 없다.</b> <see cref="Play"/> 를 처음 부르면 스스로 GameObject 를
    /// 만들어 붙고, 스프라이트·셰이더·프리팹도 코드가 잡는다
    /// (<see cref="ProtoLevelUpBeam"/>·<see cref="ProtoAudio"/> 와 같은 방식).</para>
    ///
    /// <para><b>왜 Animator 를 안 쓰는가.</b> <c>FX_Lightning_Strike.anim</c> 클립을 같이 만들어
    /// 뒀지만(Timeline 용) 런타임은 그 클립을 쓰지 않는다. Animator 는 <c>Time.timeScale</c> 을
    /// 따르므로 게임이 멈춘 상태(레벨업 카드)에서 교체가 일어나면 번개가 얼어붙는다.
    /// 여기서는 <see cref="ProtoConfig.UnscaledDelta"/> 로 직접 넘긴다. 지면 파티클도 같은
    /// 이유로 <c>useUnscaledTime</c> 을 켠다.</para>
    /// </summary>
    public class ProtoLightningStrike : MonoBehaviour
    {
        /// Resources 아래 경로. 텍스처·프리팹이 <c>Assets/_Game/Resources/VFX/</c> 에 있는 이유는
        /// 씬·프리팹 배선 없이 불러오기 위해서다 (SFX 와 같은 이유).
        public const string SheetPath  = "VFX/FX_Lightning_Strike";
        public const string GroundPath = "VFX/FX_LightningGround";

        /// 지면 링이 퍼지는 최대 반경(m). <c>FX_LightningGround</c> 프리팹에서 실측한 값이다.
        /// 부르는 쪽이 <b>판정 반경을 여기에 맞추면 링 테두리가 곧 판정선</b>이 된다
        /// (착탄 폭발과 같은 원칙). 프리팹의 <c>lightning01</c> 스케일을 바꾸면 이 값도 바꿔라.
        public const float GroundRingRadius = 1.86f;

        /// 줄기가 지면에 닿기까지 걸리는 시간(초). 교체·피해 타이밍을 여기에 맞추면
        /// 연출과 판정이 어긋나지 않는다. 아직 한 번도 재생 전이면 기본값을 돌려준다.
        public static float ImpactDelay
        {
            get
            {
                if (_instance == null) return 3f / 30f;
                return _instance.impactFrame / Mathf.Max(_instance.fps, 1f);
            }
        }

        /// 늘리기 시작하는 이음매. 셀 아래쪽이 0, 위쪽이 1.
        /// <b><c>ArtSource/VFX/bake_lightning.py</c> 가 굽고 나서 출력하는 SEAM_V 값이다.</b>
        /// 지면 섬광 몸통 전체가 이 아래 들어가야 한다 — 올리면 섬광 윗부분이 늘어나 찌그러진다.
        public const float SeamV = 0.3913f;

        [Header("타이밍")]
        [Tooltip("초당 프레임 수. 16장이므로 30 이면 약 0.53초. 20 아래로 내리면 컷이 눈에 보인다.")]
        public float fps = 30f;

        [Tooltip("번개가 지면에 닿는 프레임. 지면 링·불똥이 이 프레임에 터진다.")]
        public int impactFrame = 3;

        [Tooltip("마지막 이 비율 구간에서 서서히 지운다. 그림 자체가 옅어지지만 딱 끊으면 티가 난다.")]
        [Range(0f, 0.6f)] public float tailFade = 0.22f;

        [Header("화면 맞춤")]
        [Tooltip("끌 이유는 거의 없다. 끄면 줄기를 안 늘려서 카메라 각도에 따라 화면 안에서 잘린다.")]
        public bool autoFitToScreen = true;

        [Tooltip("화면 꼭대기보다 이 배수만큼 더 올라간다. 1.0 이면 딱 걸쳐서 끝이 보일 수 있다.")]
        [Range(1f, 2f)] public float screenMargin = 1.15f;

        [Tooltip("줄기 세로 배율 상한. 너무 늘리면 지그재그가 실처럼 얇아 보인다.")]
        [Range(1f, 12f)] public float maxStretch = 6f;

        [Header("크기·색")]
        [Tooltip("스프라이트 기본 크기(4.27m)에 곱한다. 1 이면 캐릭터(1.7m)의 약 2.2배 높이.")]
        public float scale = 1f;

        [Tooltip("가법 블렌딩이라 1 을 넘겨야 코어가 하얗게 탄다")]
        public float intensity = 1.6f;

        public Color tint = new Color(0.62f, 0.84f, 1.00f, 1f);

        static ProtoLightningStrike _instance;

        Sprite[] _frames;
        Transform _bolt;
        Mesh _mesh;
        Material _mat;
        ParticleSystem[] _groundPs;
        bool _groundFired;
        float _t = -1f;             // -1 = 재생 중 아님

        float _h;                   // 셀 한 칸의 월드 높이(=폭). 로컬 단위
        float _groundV;             // 지면선의 v 좌표 (스프라이트 피벗에서 읽는다)

        /// 번개를 한 번 내리친다. <paramref name="groundPos"/> 는 <b>발밑</b> 좌표다.
        /// 재생 중에 다시 부르면 처음부터 다시 시작한다 — 숫자키를 연타해도 겹쳐 쌓이지 않는다.
        public static void Play(Vector3 groundPos, float scaleMul = 1f)
        {
            if (_instance == null)
            {
                var go = new GameObject("_LightningStrike");
                _instance = go.AddComponent<ProtoLightningStrike>();
            }
            _instance.PlayAt(groundPos, scaleMul);
        }

        void Awake()
        {
            if (_instance == null) _instance = this;
            Build();
            SetVisible(false);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_mesh != null) Destroy(_mesh);
            if (_mat  != null) Destroy(_mat);
        }

        void Build()
        {
            if (_bolt != null) return;

            // LoadAll 의 반환 순서는 보장되지 않는다. 이름이 _00~_15 이라 정렬하면 곧 프레임 순서다.
            _frames = Resources.LoadAll<Sprite>(SheetPath);
            if (_frames == null || _frames.Length == 0)
            {
                Debug.LogError("[번개] " + SheetPath + " 스프라이트를 찾지 못했다 — 연출이 안 나온다");
                enabled = false;
                return;
            }
            System.Array.Sort(_frames, (a, b) => string.CompareOrdinal(a.name, b.name));

            var shader = Shader.Find("Proto/SpriteFlash");
            if (shader == null)
            {
                Debug.LogError("[번개] Proto/SpriteFlash 셰이더를 찾지 못했다 — 연출이 안 나온다");
                enabled = false;
                return;
            }

            var s0 = _frames[0];
            _h = s0.bounds.size.y;                              // 셀은 정사각이라 폭도 같다
            _groundV = s0.pivot.y / s0.rect.height;             // 굽는 단계에서 박은 지면선

            _mat = new Material(shader);
            _mat.SetTexture(ProtoShaderIds._MainTex, s0.texture);
            // 아틀라스에서 한 칸만 쓰도록 타일링을 고정한다. 오프셋은 프레임마다 바뀐다.
            _mat.SetTextureScale("_MainTex", new Vector2(s0.rect.width  / s0.texture.width,
                                                         s0.rect.height / s0.texture.height));

            _mesh = new Mesh { name = "LightningBoltQuad" };
            _mesh.MarkDynamic();

            var go = new GameObject("Bolt");
            go.transform.SetParent(transform, false);
            _bolt = go.transform;
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            RebuildMesh(1f);
            BuildGround();
        }

        /// 가로 3줄짜리 쿼드. 아래 두 줄(바닥~이음매)은 항상 1:1, 윗줄만 <paramref name="k"/> 배로 늘린다.
        /// 원점이 지면선이라 <c>transform.position</c> 에 발밑 좌표를 그대로 넣으면 맞는다.
        void RebuildMesh(float k)
        {
            float halfW   = _h * 0.5f;
            float yBottom = (0f    - _groundV) * _h;   // 지면선 아래 (음수)
            float ySeam   = (SeamV - _groundV) * _h;
            float yTop    = ySeam + (1f - SeamV) * _h * k;

            var verts = new[]
            {
                new Vector3(-halfW, yBottom, 0f), new Vector3(halfW, yBottom, 0f),
                new Vector3(-halfW, ySeam,   0f), new Vector3(halfW, ySeam,   0f),
                new Vector3(-halfW, yTop,    0f), new Vector3(halfW, yTop,    0f),
            };

            // UV 는 0~1 정사각. 어느 칸을 볼지는 머티리얼의 _MainTex 오프셋이 정한다.
            var uvs = new[]
            {
                new Vector2(0f, 0f),    new Vector2(1f, 0f),
                new Vector2(0f, SeamV), new Vector2(1f, SeamV),
                new Vector2(0f, 1f),    new Vector2(1f, 1f),
            };

            // 셰이더가 IN.color 를 곱한다. 메시에 색 채널이 없으면 값이 정의되지 않으므로 흰색을
            // 넣어 둔다 (페이드아웃은 _Tint 의 알파로 넣는다).
            var cols = new Color[6];
            for (int i = 0; i < 6; i++) cols[i] = Color.white;

            _mesh.Clear();
            _mesh.vertices  = verts;
            _mesh.uv        = uvs;
            _mesh.colors    = cols;
            _mesh.triangles = new[] { 0, 2, 1, 2, 3, 1, 2, 4, 3, 4, 5, 3 };
            _mesh.RecalculateBounds();
        }

        /// 지면 타격(링 + 불똥). NamuFX 팩에서 떼어낸 프리팹이라 팩을 지우면 같이 사라진다 —
        /// 그때는 <b>경고 한 줄만 남기고 번개 줄기만으로 계속 간다.</b> 연출 하나 때문에
        /// 캐릭터 교체 자체가 막히면 안 된다.
        void BuildGround()
        {
            var prefab = Resources.Load<GameObject>(GroundPath);
            if (prefab == null)
            {
                Debug.LogWarning("[번개] " + GroundPath + " 가 없다 — 지면 타격 없이 번개 줄기만 나간다");
                _groundPs = new ParticleSystem[0];
                return;
            }

            var g = Instantiate(prefab, transform);
            g.name = "Ground";
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
            _groundPs = g.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in _groundPs)
            {
                var m = ps.main;
                // 번개 줄기는 unscaled 로 도는데 파티클만 timeScale 을 따르면, 게임이 멈춘 동안
                // 줄기는 흐르고 지면만 정지한다. 둘의 시간축을 맞춘다.
                m.useUnscaledTime = true;
                m.playOnAwake = false;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r != null)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
        }

        /// <paramref name="ground"/> 에서 수직으로 올라갈 때 화면 위로 빠져나가는 월드 높이.
        /// 뷰포트 y = 1 은 <c>clipY == clipW</c> 인 지점이고 둘 다 높이의 1차식이라,
        /// 두 점만 찍으면 반복 없이 정확히 풀린다. 해상도·화면비·FOV·카메라 각도가 전부 반영된다.
        static float ScreenTopHeight(Camera cam, Vector3 ground)
        {
            Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            Vector4 a = vp * new Vector4(ground.x, ground.y,      ground.z, 1f);
            Vector4 b = vp * new Vector4(ground.x, ground.y + 1f, ground.z, 1f);
            float f0 = a.y - a.w;
            float f1 = b.y - b.w;
            float d  = f1 - f0;
            if (Mathf.Abs(d) < 1e-6f) return -1f;     // 화면 위 경계와 만나지 않는 카메라
            return -f0 / d;
        }

        /// 지금 카메라에서 줄기가 화면 밖으로 나가려면 필요한 세로 배율.
        float FitStretch(Vector3 groundPos, float rootScale)
        {
            if (!autoFitToScreen) return 1f;
            var cam = Camera.main;
            if (cam == null) return 1f;

            float need = ScreenTopHeight(cam, groundPos);
            if (need <= 0f) return 1f;                 // 위로 안 나가는 카메라 — 늘려봐야 소용없다

            // 메시는 로컬 단위다. 루트 스케일만큼 나눠서 같은 자로 잰다.
            float needLocal = (need * screenMargin) / Mathf.Max(rootScale, 1e-4f);
            float ySeam = (SeamV - _groundV) * _h;
            float span  = (1f - SeamV) * _h;           // 늘릴 구간의 1:1 높이
            if (span <= 1e-4f) return 1f;
            return Mathf.Clamp((needLocal - ySeam) / span, 1f, maxStretch);
        }

        void PlayAt(Vector3 groundPos, float scaleMul)
        {
            // 에디터에서 AddComponent 로 붙이면 플레이 모드 밖에서는 Awake 가 안 돈다 — 스스로 메꾼다.
            if (_bolt == null) { Build(); SetVisible(false); }
            if (_bolt == null) return;

            // **회전은 루트가 아니라 Bolt 자식에만 건다.** 루트를 돌리면 지면 링과 불똥까지
            // 카메라를 따라 같이 돌아서, 바닥에 누운 링이 매 프레임 홱홱 돌아간다.
            transform.position = groundPos;
            transform.rotation = Quaternion.identity;
            float rootScale = scale * scaleMul;
            transform.localScale = Vector3.one * rootScale;

            // 배율은 재생 시작에 한 번만 잡는다. 매 프레임 다시 재면 카메라가 따라오는 동안
            // 줄기 길이가 늘었다 줄었다 해서 숨 쉬는 것처럼 보인다.
            RebuildMesh(FitStretch(groundPos, rootScale));

            _mat.SetFloat(ProtoShaderIds._Intensity, intensity);
            _groundFired = false;
            StopGround();
            _t = 0f;
            SetVisible(true);
            Tick(0f);
        }

        void LateUpdate()
        {
            if (_t < 0f) return;

            _t += ProtoConfig.UnscaledDelta;
            float total = FrameCount / Mathf.Max(fps, 1f);
            if (_t >= total) { _t = -1f; SetVisible(false); return; }
            Tick(_t);
        }

        int FrameCount { get { return _frames != null ? _frames.Length : 0; } }

        void Tick(float t)
        {
            float total = FrameCount / Mathf.Max(fps, 1f);
            float u = total > 0f ? Mathf.Clamp01(t / total) : 1f;

            int i = Mathf.Clamp(Mathf.FloorToInt(t * fps), 0, FrameCount - 1);
            var sp = _frames[i];
            _mat.SetTextureOffset(ProtoShaderIds._MainTex, new Vector2(sp.rect.x / sp.texture.width,
                                                          sp.rect.y / sp.texture.height));

            // 지면 타격은 줄기가 바닥에 닿는 프레임에 한 번만 터진다.
            if (!_groundFired && i >= impactFrame)
            {
                _groundFired = true;
                if (_groundPs != null)
                    foreach (var ps in _groundPs) { ps.Clear(true); ps.Play(false); }
                // 가까이 떨어진 벼락이라 섬광과 소리 사이에 지연이 없다 — 같은 프레임이 맞다.
                ProtoAudio.Play2D("thunder");
            }

            // 꼬리 페이드. SpriteRenderer 가 아니라 메시라 정점 색 대신 _Tint 알파로 넣는다.
            float a = 1f;
            if (tailFade > 0f && u > 1f - tailFade)
            {
                float f = (u - (1f - tailFade)) / tailFade;
                a = (1f - f) * (1f - f);   // 제곱 — 선형이면 끝에서 뿌옇게 남는다
            }
            var c = tint; c.a *= a;
            _mat.SetColor(ProtoShaderIds._Tint, c);

            // 카메라를 향해 **Y축으로만** 돌린다. 완전 빌보드로 만들면 카메라 피치만큼 번개가
            // 뒤로 누워서 하늘에서 내리꽂히는 게 아니라 바닥에 깔린 것처럼 보인다.
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 1e-4f)
                    _bolt.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
            }
        }

        void StopGround()
        {
            if (_groundPs == null) return;
            foreach (var ps in _groundPs)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
            }
        }

        void SetVisible(bool on)
        {
            if (_bolt != null) _bolt.gameObject.SetActive(on);
            // 지면 불똥은 줄기(0.53초)보다 조금 더 남는 게 자연스럽다. 그래서 여기서 끄지 않고
            // 스스로 수명을 다하게 둔다 — 다음 재생 때 PlayAt 이 StopGround 로 지운다.
        }
    }
}
