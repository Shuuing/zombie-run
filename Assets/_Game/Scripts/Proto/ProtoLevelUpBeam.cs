using UnityEngine;

namespace Proto
{
    /// 레벨업 카드 선택 연출 — 캐릭터에서 하늘로 솟는 빛기둥.
    ///
    /// 메시도 머티리얼도 코드로 만든다 (발밑 링 `ProtoGroundRing` 과 같은 방식).
    /// 텍스처·프리팹·파티클 시스템이 없으므로 씬을 다시 생성할 필요가 없다.
    ///
    /// **씬 배선이 필요 없다.** `Play()` 를 처음 부르면 자기 GameObject 를 만들어 붙는다.
    /// `_ProtoSystems` 에 손으로 얹어도 되지만, 그러려면 `프로토타입 씬 생성` 을 다시
    /// 돌려야 하고 지금 물 위치가 어긋난 상태라 그 실행이 다른 변경을 끌고 온다.
    public class ProtoLevelUpBeam : MonoBehaviour
    {
        // ── 타이밍 (초) ──────────────────────────────────────────────────
        // 솟구침은 빨라야 "번쩍"으로 읽힌다. 0.18 이상 주면 기둥이 천천히 자라는
        // 것이 보여서 타격감이 아니라 로딩바처럼 보인다.
        [Header("타이밍")]
        public float riseTime = 0.13f;
        public float holdTime = 0.16f;
        public float fadeTime = 0.42f;

        [Header("크기 (m)")]
        // 8m 은 화면에 딱 맞춰 정한 값이다. 카메라가 피치 58° 라 세로 기둥이 화면에서는
        // cos58° ≈ 53% 로 짧아 보이는 대신 위쪽으로 길게 뻗는다. 13m 로 뒀더니 창끝이
        // 화면 위로 빠져나가 기둥이 그냥 잘린 띠처럼 보였다.
        public float height = 8f;
        public float width  = 2.4f;
        /// 솟구치는 동안 폭이 이 배율까지 좁아진다. 자라면서 얇아지면 위로 쏘는 느낌이 강해진다.
        public float widthNarrow = 0.65f;

        [Header("색")]
        public Color beamColor = new Color(1.00f, 0.86f, 0.35f, 1f);
        public Color coreColor = new Color(1.00f, 1.00f, 0.92f, 1f);
        [Tooltip("피크 밝기. 가법 블렌딩이라 1 을 넘겨야 코어가 하얗게 탄다")]
        public float peakIntensity = 2.2f;

        [Header("밑동 섬광")]
        public bool  groundFlash      = true;
        public float groundFlashRadius = 2.2f;
        [Tooltip("원판 분할 수. 낮으면 테두리가 다각형으로 보인다")]
        public int   flashSegments = 40;

        static ProtoLevelUpBeam _instance;

        Transform _beam, _flash;
        Material  _beamMat, _flashMat;
        float _t = -1f;                 // -1 = 재생 중 아님
        Vector3 _origin;

        /// 빛기둥을 한 번 재생한다. 재생 중에 다시 부르면 처음부터 다시 시작한다
        /// (레벨이 한 번에 여러 개 밀려 올라갈 때 기둥이 겹쳐 쌓이지 않게).
        public static void Play(Vector3 worldPos, Color? color = null)
        {
            if (_instance == null)
            {
                var go = new GameObject("_LevelUpBeam");
                _instance = go.AddComponent<ProtoLevelUpBeam>();
            }
            _instance.PlayAt(worldPos, color);
        }

        void Awake()
        {
            if (_instance == null) _instance = this;
            Build();
            SetVisible(false);
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        void Build()
        {
            var shader = Shader.Find("Proto/LightBeam");
            if (shader == null)
            {
                Debug.LogError("[LevelUpBeam] Proto/LightBeam 셰이더를 찾지 못했다 — 연출이 안 나온다");
                enabled = false;
                return;
            }

            // ── 기둥: 밑동이 원점에 오는 세로 쿼드 ────────────────────────
            _beamMat = new Material(shader);
            _beam = MakeQuad("Beam", BuildBeamQuad(), _beamMat);

            // ── 밑동 섬광: 지면에 눕는 원판 ──────────────────────────────
            // **반경 모드(_Radial)를 반드시 켜야 한다.** 기둥 셰이더의 세로 감쇠는
            // 아래→위 단방향이라, 그대로 지면에 눕히면 한쪽 변에서 뚝 끊긴 사각형이 된다.
            if (groundFlash)
            {
                _flashMat = new Material(shader);
                _flashMat.SetFloat(ProtoShaderIds._Radial, 1f);
                _flashMat.SetFloat(ProtoShaderIds._CoreWidth, 0.42f);   // 중심의 밝은 심지 크기
                _flashMat.SetFloat(ProtoShaderIds._GlowStrength, 1.15f);
                _flashMat.SetFloat(ProtoShaderIds._RayStrength, 0.22f); // 방사형 줄무늬를 약하게
                _flashMat.SetFloat(ProtoShaderIds._RayCount, 12f);
                _flash = MakeQuad("GroundFlash", BuildDisc(flashSegments), _flashMat);
                // 지면에 살짝 띄운다 — 도로 차선과 Z파이팅이 나는 높이가 0.05 대다
                _flash.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _flash.localPosition = new Vector3(0f, 0.08f, 0f);
            }
        }

        Transform MakeQuad(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go.transform;
        }

        void PlayAt(Vector3 worldPos, Color? color)
        {
            // Awake 가 아직 안 돈 상태로 불릴 수 있다 — 에디터에서 AddComponent 로 붙이면
            // 플레이 모드가 아닌 동안 Awake 가 호출되지 않는다. 여기서 스스로 메꾼다.
            if (_beam == null) { Build(); SetVisible(false); }
            if (_beam == null) return;   // Build 가 셰이더를 못 찾은 경우

            _origin = worldPos;
            if (color.HasValue && _beamMat != null) _beamMat.SetColor(ProtoShaderIds._Color, color.Value);
            if (_beamMat != null) _beamMat.SetColor(ProtoShaderIds._CoreColor, coreColor);
            if (_flashMat != null) _flashMat.SetColor(ProtoShaderIds._Color, color ?? beamColor);
            _t = 0f;
            SetVisible(true);
            Tick(0f);
        }

        void LateUpdate()
        {
            if (_t < 0f) return;

            // **반드시 unscaled.** 레벨업 카드 화면은 `Time.timeScale = 0` 으로 멈춰 있고,
            // 카드를 고른 직후 프레임에도 아직 0 일 수 있다. scaled 를 쓰면 연출이 얼어붙는다.
            _t += ProtoConfig.UnscaledDelta;

            float total = riseTime + holdTime + fadeTime;
            if (_t >= total) { _t = -1f; SetVisible(false); return; }
            Tick(_t);
        }

        void Tick(float t)
        {
            // 진행도: 0 → 1 로 자라 오른다. 감속(1-(1-x)^3)을 걸어 처음이 가장 빠르다.
            float rise = riseTime > 0f ? Mathf.Clamp01(t / riseTime) : 1f;
            float progress = 1f - Mathf.Pow(1f - rise, 3f);

            // 밝기: 자라는 동안 최대, hold 유지, 그 뒤 페이드.
            float intensity;
            float afterHold = t - riseTime - holdTime;
            if (afterHold <= 0f) intensity = peakIntensity;
            else
            {
                float f = fadeTime > 0f ? Mathf.Clamp01(afterHold / fadeTime) : 1f;
                // 제곱 페이드 — 선형이면 끝에서 뿌옇게 남아 사라진 티가 안 난다
                intensity = peakIntensity * (1f - f) * (1f - f);
            }

            // 위치: 밑동을 캐릭터에 두고, 페이드 동안 살짝 떠오르게 한다.
            float lift = afterHold > 0f && fadeTime > 0f
                       ? Mathf.Clamp01(afterHold / fadeTime) * 1.6f : 0f;
            transform.position = _origin + new Vector3(0f, lift, 0f);

            // 카메라를 향해 Y축으로만 돌린다 — 세로 기둥이 눕지 않아야 한다.
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 1e-4f)
                    transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
            }

            if (_beam != null)
            {
                // 자라면서 얇아진다
                float w = width * Mathf.Lerp(1f, widthNarrow, progress);
                _beam.localScale = new Vector3(w, height, 1f);
                _beamMat.SetFloat(ProtoShaderIds._Progress, progress);
                _beamMat.SetFloat(ProtoShaderIds._Intensity, intensity);
            }

            if (_flash != null)
            {
                // 밑동 섬광은 기둥보다 빠르게 퍼지고 빠르게 죽는다
                float fr = groundFlashRadius * (0.35f + 0.65f * progress);
                _flash.localScale = new Vector3(fr * 2f, fr * 2f, 1f);
                _flashMat.SetFloat(ProtoShaderIds._Progress, 1f);
                _flashMat.SetFloat(ProtoShaderIds._Intensity, intensity * 0.8f);
            }
        }

        void SetVisible(bool on)
        {
            if (_beam  != null) _beam.gameObject.SetActive(on);
            if (_flash != null) _flash.gameObject.SetActive(on);
        }

        /// 밑동이 원점, 위로 height=1 만큼 뻗는 세로 쿼드. 스케일로 크기를 잡는다.
        /// 원점을 중심이 아니라 **밑면**에 두는 이유: 캐릭터 발밑에 그냥 놓으면 맞기 때문.
        static Mesh BuildBeamQuad()
        {
            var mesh = new Mesh { name = "LightBeamQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// 원점이 중심인 반지름 0.5 원판(팬). 지면 섬광은 캐릭터를 가운데 두고 사방으로 퍼진다.
        ///
        /// 쿼드에 원형 감쇠만 걸어도 모서리는 가법 블렌딩에서 0 이 되어 안 보이지만,
        /// 원판으로 만들면 **모서리 픽셀을 아예 안 그린다** — 밑동 섬광은 화면에서 꽤 크고
        /// 매 프레임 가법으로 겹쳐 그리므로 낭비되는 픽셀을 줄이는 게 맞다.
        static Mesh BuildDisc(int segments)
        {
            segments = Mathf.Max(8, segments);

            var verts = new Vector3[segments + 1];
            var uvs   = new Vector2[segments + 1];
            var tris  = new int[segments * 3];

            verts[0] = Vector3.zero;
            uvs[0]   = new Vector2(0.5f, 0.5f);   // 중심 = 반경 0

            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                verts[i + 1] = new Vector3(ca * 0.5f, sa * 0.5f, 0f);
                // 셰이더가 length(uv - 0.5) * 2 로 반경을 재므로 테두리가 정확히 1 이 된다
                uvs[i + 1]   = new Vector2(0.5f + ca * 0.5f, 0.5f + sa * 0.5f);
            }

            for (int i = 0; i < segments; i++)
            {
                int t = i * 3;
                tris[t + 0] = 0;
                tris[t + 1] = i + 1;
                tris[t + 2] = (i + 1) % segments + 1;
            }

            var mesh = new Mesh { name = "GroundFlashDisc" };
            mesh.vertices  = verts;
            mesh.uv        = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
